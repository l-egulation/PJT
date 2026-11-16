using Deviant.Combat;
using UnityEngine;

namespace Deviant.Stage1Monsters.Game
{
    public static class Stage1VillageProjectileVisual
    {
        static Sprite ink;
        public static void Attach(ArenaProjectile shot)
        {
            if(!ink)
            {
                var texture=new Texture2D(16,12,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
                var pixels=new Color32[192];
                for(int y=0;y<12;y++)for(int x=0;x<16;x++)
                    if(Mathf.Pow((x-7.5f)/8,2)+Mathf.Pow((y-5.5f)/6,2)<=1)
                        pixels[y*16+x]=new Color32(23,23,22,255);
                texture.SetPixels32(pixels);texture.Apply();
                ink=Sprite.Create(texture,new Rect(0,0,16,12),Vector2.one*.5f,32);
            }
            var renderer=shot.GetComponentInChildren<SpriteRenderer>();
            if(!renderer)return;
            renderer.sprite=ink;renderer.color=Color.white;
            renderer.transform.localScale=new Vector3(shot.CollisionSize.x/.5f,shot.CollisionSize.y/.375f,1);
        }
    }
}
