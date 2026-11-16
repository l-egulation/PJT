using Deviant.Combat;
using UnityEngine;

namespace Deviant.Stage1Monsters.Game
{
    [DefaultExecutionOrder(675)]
    public sealed class Stage1GutterTrajectory : MonoBehaviour
    {
        Stage1VillageMonsterAttack attack;
        SpriteRenderer line;
        Texture2D texture;
        public float WarningHeight=>attack.Spec.projectileSize[1];
        public void Bind(Stage1VillageMonsterAttack owner)
        {
            attack=owner;
            var node=new GameObject("Gutter trajectory");node.transform.SetParent(transform,false);
            line=node.AddComponent<SpriteRenderer>();line.sharedMaterial=owner.Actor.visual.sharedMaterial;
            line.sortingLayerID=owner.Actor.visual.sortingLayerID;line.sortingOrder=owner.Actor.visual.sortingOrder-1;
            texture=new Texture2D(256,12,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
            var pixels=new Color32[256*12];
            for(int y=0;y<12;y++)for(int x=0;x<256;x++)
            {
                float edge=Mathf.Clamp01((6-Mathf.Abs(y-5.5f))/3);
                float taper=Mathf.Min(Mathf.Clamp01(x/4f),Mathf.Clamp01((255-x)/12f));
                pixels[y*256+x]=new Color32(239,81,74,(byte)(130*edge*taper));
            }
            texture.SetPixels32(pixels);texture.Apply();
            line.sprite=Sprite.Create(texture,new Rect(0,0,256,12),new Vector2(0,.5f),32);
            line.enabled=false;
        }
        void LateUpdate()=>Draw();
        public void Draw()
        {
            if(!line)return;line.enabled=false;
            var actor=attack.Actor;float age=attack.MotionAge;
            if(actor.Dead || !actor.aggressive || !actor.SpeciesAttackVisible || age<.1f || age>=attack.Spec.damageStart)return;
            float elapsed=0;int index=0;
            foreach(float duration in actor.Definition.attackFrameDurations)
            { elapsed+=duration;if(age<elapsed)break;index++; }
            float aperture=(index<14?96.5f:index<16?97.5f:98.5f)-64;
            var point=actor.Position+new Vector2(actor.facing*aperture/32,attack.Spec.muzzleOffset[1]);
            point+=(Vector2)actor.visual.transform.localPosition;
            line.transform.position=point;
            float reveal=Mathf.SmoothStep(0,1,(age-.1f)/.25f);
            // Warning thickness comes from the same dimensions used by both projectile sweeps.
            line.transform.localScale=new Vector3(actor.facing*3*reveal,WarningHeight/.375f,1);
            line.enabled=true;
        }
        void OnDisable(){if(line)line.enabled=false;}
        void OnDestroy(){if(line&&line.sprite)Destroy(line.sprite);if(texture)Destroy(texture);}
    }
}
