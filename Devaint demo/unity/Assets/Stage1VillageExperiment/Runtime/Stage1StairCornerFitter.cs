using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace Stage1VillageExperiment
{
    // Native 32px inside corners fit the ACTUAL north and side sprites at each join.
    // A one-cell riser ends in a top-corner sprite; taller risers end in a wall sprite.
    public static class Stage1StairCornerFitter
    {
        public static string ExportDirectory;
        static readonly Dictionary<string,Sprite> cache=new Dictionary<string,Sprite>();
        public static int Count=>cache.Count;
        static readonly Color core=new Color32(37,34,38,255);
        static Color[] Pixels(Sprite sprite)
        {
            var result=new Color[1024];var pixels=sprite.texture.GetPixels();int width=sprite.texture.width;
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)result[y*32+x]=pixels[((int)sprite.rect.y+y)*width+(int)sprite.rect.x+x];
            return result;
        }
        public static Sprite Fit(Sprite seed,Sprite north,Sprite side,int northId,int sideId,bool left,ICollection<UnityEngine.Object> owned)
        {
            string name="inner_"+(left?"L":"R")+"_n"+northId.ToString("00")+"_f"+sideId.ToString("00")+"_32x32";
            string key=name;
            if(cache.TryGetValue(key,out var ready)&&ready)return ready;
            var raw=Pixels(seed);var top=Pixels(north);var lateral=Pixels(side);var original=new Color[1024];
            for(int y=0;y<32;y++)for(int x=0;x<32;x++){var c=raw[y*32+(left?x:31-x)];original[y*32+x]=new Color(c.r,c.g,c.b,1);}
            var boundary=(Color[])original.Clone();
            for(int i=0;i<32;i++){
                boundary[31*32+i]=top[i];
                boundary[i*32+(left?0:31)]=lateral[i*32+(left?31:0)];
                boundary[i*32+(left?31:0)]=core;
                boundary[i]=core;
            }
            // The single vertex is a contact crease shared by two different materials.
            int vertex=31*32+(left?0:31);
            Color n=top[left?0:31],s=lateral[31*32+(left?31:0)];
            boundary[vertex]=new Color(Math.Min(n.r,s.r),Math.Min(n.g,s.g),Math.Min(n.b,s.b),1);
            var correction=new Vector3[1024];var next=new Vector3[1024];
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)if(x==0||x==31||y==0||y==31){int i=y*32+x;correction[i]=new Vector3(boundary[i].r-original[i].r,boundary[i].g-original[i].g,boundary[i].b-original[i].b);next[i]=correction[i];}
            // Harmonic boundary correction distributes the edge adjustment across the full
            // corner, preserving the generated soil grain instead of pasting a one-pixel rim.
            for(int iteration=0;iteration<700;iteration++){
                for(int y=1;y<31;y++)for(int x=1;x<31;x++){int i=y*32+x;next[i]=(correction[i-1]+correction[i+1]+correction[i-32]+correction[i+32])*.25f;}
                var swap=correction;correction=next;next=swap;
            }
            var final=new Color32[1024];
            for(int y=0;y<32;y++)for(int x=0;x<32;x++){
                int i=y*32+x;Color c;
                if(x==0||x==31||y==0||y==31)c=boundary[i];
                else c=new Color(Mathf.Clamp01(original[i].r+correction[i].x),Mathf.Clamp01(original[i].g+correction[i].y),Mathf.Clamp01(original[i].b+correction[i].z),1);
                final[i]=c;
            }
            for(int i=1;i<31;i++){
                if(!final[31*32+i].Equals((Color32)top[i]))throw new Exception("North stair seam mismatch");
                if(!final[i*32+(left?0:31)].Equals((Color32)lateral[i*32+(left?31:0)]))throw new Exception("Side stair seam mismatch");
            }
            var texture=new Texture2D(32,32,TextureFormat.RGBA32,false);texture.name=name;texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Clamp;texture.SetPixels32(final);texture.Apply();owned.Add(texture);
            var sprite=Sprite.Create(texture,new Rect(0,0,32,32),Vector2.zero,32,0,SpriteMeshType.FullRect);sprite.name=name;owned.Add(sprite);cache[key]=sprite;
            if(!string.IsNullOrEmpty(ExportDirectory)){Directory.CreateDirectory(ExportDirectory);File.WriteAllBytes(Path.Combine(ExportDirectory,name+".png"),texture.EncodeToPNG());}
            return sprite;
        }
    }
}
