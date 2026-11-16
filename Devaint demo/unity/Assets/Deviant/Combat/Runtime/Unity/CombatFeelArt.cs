using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deviant.Combat
{
    // Optional test-branch media. Does not alter the captured combat timeline.
    public static class CombatFeelArt
    {
        static Sprite[] hit, up, down, cleave;
        static Sprite[] variants;
        public static Sprite[] ImpactVariants {
            get {
                if(variants!=null)return variants;
                variants=new Sprite[6];
                for(int i=0;i<6;i++) {
                    variants[i]=Resources.Load<Sprite>("CombatFeelArt/HitVariants/"+i.ToString("00"));
                    if(!variants[i]){variants=null;throw new InvalidOperationException("Export the six impact variants first.");}
                }
                return variants;
            }
        }
        public static bool HasGeneratedCleave => Resources.Load<Sprite>("CombatFeelArt/HeavyCleave/00");
        public static Sprite[] Cleave => cleave ?? (cleave=HasGeneratedCleave?Load("HeavyCleave"):FitCleaveFallback());
        static Sprite[] FitCleaveFallback()
        {
            var source=down??(down=Load("SwordDown"));int x0=128,y0=128,x1=0,y1=0;
            foreach(var sprite in source) {
                var pixels=sprite.texture.GetPixels32();int stride=sprite.texture.width;
                for(int y=0;y<128;y++)for(int x=0;x<128;x++)if(pixels[y*stride+x].a>=16) {
                    x0=Mathf.Min(x0,x);y0=Mathf.Min(y0,y);x1=Mathf.Max(x1,x);y1=Mathf.Max(y1,y);
                }
            }
            var crop=new Rect(x0,y0,x1-x0+1,y1-y0+1);var result=new Sprite[4];
            for(int i=0;i<4;i++){result[i]=Sprite.Create(source[i].texture,crop,Vector2.one*.5f,32);result[i].name="Range fitted ink cleave "+i;}
            return result;
        }
        static readonly Dictionary<Sprite, Vector3> centers = new Dictionary<Sprite, Vector3>();
        public static Sprite[] Hit => hit ?? (hit = Load("Hit"));
        static Sprite[] Load(string directory)
        {
            var frames = new Sprite[4];
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = Resources.Load<Sprite>("CombatFeelArt/" + directory + "/" + i.ToString("00"));
                if (!frames[i]) throw new InvalidOperationException("Missing combat-feel frame: " + directory + "/" + i);
            }
            return frames;
        }
        internal static bool CanReplace(AttackMotionSnapshot media)
            => media != null && media.Form == "base_sword" && (media.State == "attack1" || media.State == "attack2") &&
                media.Effect?.frames != null && media.Effect.frames.Length > 0 &&
                media.Effect.frames[0].StartsWith("EffectsApproved11/", StringComparison.Ordinal);
        internal static CharacterEffect Sword(AttackMotionSnapshot media)
        {
            bool rising = media.State == "attack1";
            var sprites = rising ? up ?? (up = Load("SwordUp")) : down ?? (down = Load("SwordDown"));
            return new CharacterEffect {
                name = "Ink crescent test", attack = media.State,
                frames = new[] { "CombatFeel/ink/0", "CombatFeel/ink/1", "CombatFeel/ink/2", "CombatFeel/ink/3" },
                seconds = new[] { .03f, .036f, .038f, .036f },
                x = new[] { .125f, .125f, .125f, .125f }, y = new[] { 1.1375f, 1.1375f, 1.1375f, 1.1375f },
                sprites = sprites, scale = 1.1583333f, flipX = false
            };
        }
        public static Vector3 WorldCenter(SpriteRenderer visual)
        {
            if (!visual || !visual.sprite) return visual ? visual.transform.position : Vector3.zero;
            var sprite = visual.sprite;
            if (!centers.TryGetValue(sprite, out var local))
            {
                local = sprite.bounds.center;
                var texture = sprite.texture;
                if (texture && texture.isReadable && !sprite.packed)
                {
                    var pixels = texture.GetPixels32(); var rect = sprite.rect;
                    int width = (int)rect.width, height = (int)rect.height;
                    int minX = width, minY = height, maxX = -1, maxY = -1;
                    for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        if (pixels[((int)rect.y + y) * texture.width + (int)rect.x + x].a < 16) continue;
                        minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                        minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                    }
                    if (maxX >= minX) local = new Vector3((minX + maxX + 1) * .5f - sprite.pivot.x,
                        (minY + maxY + 1) * .5f - sprite.pivot.y, 0) / sprite.pixelsPerUnit;
                }
                centers[sprite] = local;
            }
            if (visual.flipX) local.x = -local.x;
            if (visual.flipY) local.y = -local.y;
            return visual.transform.TransformPoint(local);
        }
    }
}
