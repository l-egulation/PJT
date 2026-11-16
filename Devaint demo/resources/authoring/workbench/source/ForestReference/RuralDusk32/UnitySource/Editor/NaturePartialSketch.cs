using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RuralNature32.Editor
{
    // A baked material treatment of the existing pixels. The silhouette, alpha,
    // collision and texture coordinates do not move when pigment is removed.
    public static class NaturePartialSketch
    {
        static int Mod(int a,int b)=>(a%b+b)%b;
        public static float Weight(float x,float y,RuralNatureTerrain.SketchRegion r)
        {
            float dx=(x-r.x)/Math.Max(1,r.radiusX),dy=(y-r.y)/Math.Max(1,r.radiusY);
            float distance=Mathf.Sqrt(dx*dx+dy*dy);
            // World-space leaf-size lobes, independent of tile indices.
            float edge=.085f*Mathf.Sin(x*.19f+y*.11f)+.06f*Mathf.Sin(y*.33f-x*.07f);
            return Mathf.SmoothStep(0,1,Mathf.Clamp01((1-distance+edge)/.32f));
        }
        static float Light(Color32 c)=>(c.r*.30f+c.g*.59f+c.b*.11f)/255f;
        public static bool Earth(Color32 c)=>Math.Abs(c.r-33)+Math.Abs(c.g-27)+Math.Abs(c.b-36)<18;
        public static int Apply(Dictionary<Vector2Int,Color32[]> tiles,RuralNatureTerrain.SketchRegion[] regions,HashSet<Vector2Int> changedTiles=null)
        {
            if(regions==null||regions.Length==0)return 0;
            var source=tiles.ToDictionary(p=>p.Key,p=>(Color32[])p.Value.Clone());int changed=0;
            Func<int,int,Color32> sample=(x,y)=>source.TryGetValue(new Vector2Int(NatureComposer.FloorDiv(x,32),NatureComposer.FloorDiv(y,32)),out var a)?a[Mod(y,32)*32+Mod(x,32)]:new Color32();
            foreach(var pair in tiles)
            {
                int bx=pair.Key.x*32,by=pair.Key.y*32;
                var nearby=regions.Where(r=>bx+32>r.x-r.radiusX*1.2f&&bx<r.x+r.radiusX*1.2f&&by+32>r.y-r.radiusY*1.2f&&by<r.y+r.radiusY*1.2f).ToArray();
                if(nearby.Length==0)continue;
                for(int v=0;v<32;v++)for(int u=0;u<32;u++)
                {
                    int x=bx+u,y=by+v,i=v*32+u;var c=source[pair.Key][i];if(c.a==0||Earth(c))continue;
                    float weight=nearby.Max(r=>Weight(x,y,r));if(weight<=0)continue;
                    float l=Light(c),brightest=l;bool silhouette=false;
                    foreach(var d in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down}){var n=sample(x+d.x,y+d.y);brightest=Math.Max(brightest,Light(n));silhouette|=n.a<24||Earth(n);}
                    bool foliage=c.g>c.b*1.12f;
                    bool line=(silhouette&&l<.36f)||brightest-l>.18f||(foliage?l<.19f:l<.085f);
                    float shade=line?.32f:Mathf.Lerp(.88f,1,Mathf.Clamp01(l*1.3f));
                    if(!line&&Mod(x+y*2,17)==0)shade-=.055f;
                    var ink=new Color32((byte)(245*shade),(byte)(237*shade),(byte)(215*shade),c.a);
                    pair.Value[i]=new Color32((byte)Mathf.RoundToInt(Mathf.Lerp(c.r,ink.r,weight)),(byte)Mathf.RoundToInt(Mathf.Lerp(c.g,ink.g,weight)),(byte)Mathf.RoundToInt(Mathf.Lerp(c.b,ink.b,weight)),c.a);changed++;changedTiles?.Add(pair.Key);
                }
            }
            return changed;
        }
    }
}
