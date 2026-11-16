using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ForestCombatV2;

namespace Stage1VillageExperiment
{
    // Collision cells choose contact lines. Complete artwork canvases remain independent of cell size.
    public static class VillageReferenceGround
    {
        [Serializable] public sealed class Entry { public string name;public int width,height;public float anchorX,anchorY,logicalWidth,logicalHeight;public bool hardExterior; }
        [Serializable] public sealed class Library { public int ppu,bodyWidth,bodyHeight;public Entry[] entries; }
        const string Path="Village/ReferenceGround/";
        public static GameObject Build(Transform parent,bool[,] cells,ForestData data,ICollection<UnityEngine.Object> owned)
        {
            var json=Resources.Load<TextAsset>(Path+"library");if(!json)throw new InvalidOperationException("Reference ground library is missing.");
            var library=JsonUtility.FromJson<Library>(json.text);var shader=Resources.Load<Shader>(Path+"ReferenceGround");
            if(!shader)throw new InvalidOperationException("Reference ground shader is missing.");
            var root=new GameObject("Paired reference terrain - shared contacts and complete artwork");root.transform.SetParent(parent,false);
            var materials=new Dictionary<string,Material>();var entries=new Dictionary<string,Entry>();var sprites=new Dictionary<string,Sprite>();
            var soil=Resources.Load<Texture2D>(Path+"Bright/soil");
            if(!soil)throw new InvalidOperationException("Paired terrain requires the generated bright soil material.");
            foreach(var e in library.entries)
            {
                var t=Resources.Load<Texture2D>(Path+e.name);if(!t)throw new InvalidOperationException("Missing reference sprite "+e.name);
                // FullRect is intentional: no 16px quarter cropping, no original-color alpha mask.
                var border=e.name=="platform"?new Vector4(44,0,44,0):Vector4.zero;
                var s=Sprite.Create(t,new Rect(0,0,t.width,t.height),new Vector2(e.anchorX/t.width,1-e.anchorY/t.height),32,0,SpriteMeshType.FullRect,border);
                s.name=e.name;owned.Add(s);sprites[e.name]=s;entries[e.name]=e;
            }
            Material Mat(string name,int fringe)
            {
                if(materials.TryGetValue(name,out var m))return m;
                var e=entries[name];
                m=new Material(shader){name=name+" paired reference material"};
                m.SetFloat("_HardExterior",e.hardExterior?1:0);m.SetFloat("_Fringe",fringe);
                m.SetTexture("_SoilTex",soil);m.SetVector("_SoilPeriod",new Vector4(soil.width,soil.height));
                m.SetVector("_Canvas",new Vector4(e.width,e.height,e.anchorX,e.anchorY));
                bool soilEdge=fringe>=1&&fringe<=3;
                m.SetFloat("_SoilEdge",soilEdge?1:0);
                if(!soilEdge)
                {
                    var bright=Resources.Load<Texture2D>(Path+"Bright/"+name);
                    if(!bright||bright.width!=e.width||bright.height!=e.height)
                        throw new InvalidOperationException("Missing or misregistered bright terrain counterpart: "+name);
                    m.SetTexture("_BrightTex",bright);
                }
                materials[name]=m;owned.Add(m);return m;
            }
            SpriteRenderer Draw(string name,float x,float y,float sx=1,float sy=1,int order=3)
            {
                var go=new GameObject(name,typeof(SpriteRenderer));go.transform.SetParent(root.transform,false);go.transform.localPosition=new Vector3(x/32,-y/32,0);go.transform.localScale=new Vector3(sx,sy,1);
                var r=go.GetComponent<SpriteRenderer>();r.sprite=sprites[name];r.sharedMaterial=Mat(name,name=="wall-left"?1:name=="wall-right"?2:name=="underside"?3:name.StartsWith("cap")?4:0);r.sortingOrder=order;return r;
            }
            void Cap(float left,float top,float length)
            {
                // Same 24/32/40px stone family everywhere, with a small controlled variation.
                // Contact lengths are fitted using whole stones, not stretched multi-stone rows.
                int count=Mathf.Max(1,Mathf.RoundToInt(length/32));
                float[] rhythm={1f,1.2f,1f,.8f,1f};float total=0;
                for(int i=0;i<count;i++)total+=rhythm[i%rhythm.Length];
                float cursor=left;
                for(int i=0;i<count;i++)
                {
                    float width=length*rhythm[i%rhythm.Length]/total;
                    string name=width>36?"cap-wide":width<28?"cap-short":i%2==0?"cap-one-a":"cap-one-b";
                    Draw(name,cursor,top,width/entries[name].logicalWidth);cursor+=width;
                }
            }
            bool Solid(int x,int y)=>x>=0&&y>=0&&x<cells.GetLength(0)&&y<cells.GetLength(1)&&cells[x,y];
            // Continuous distance from exposed terrain boundaries, including walls and ceilings.
            // Interpolated at mesh vertices: the hatch does not restart or form bands per cell.
            int gw=cells.GetLength(0),gh=cells.GetLength(1);
            var distance=new float[gw+1,gh+1];
            for(int y=0;y<=gh;y++)for(int x=0;x<=gw;x++)
            {
                bool a=Solid(x-1,y-1),b=Solid(x,y-1),c=Solid(x-1,y),d=Solid(x,y);
                distance[x,y]=(a||b||c||d)&&!(a&&b&&c&&d)?0:1000000;
            }
            for(int y=0;y<=gh;y++)for(int x=0;x<=gw;x++)
            {
                float d=distance[x,y];
                if(x>0)d=Mathf.Min(d,distance[x-1,y]+32);
                if(y>0){d=Mathf.Min(d,distance[x,y-1]+32);if(x>0)d=Mathf.Min(d,distance[x-1,y-1]+45.255f);if(x<gw)d=Mathf.Min(d,distance[x+1,y-1]+45.255f);}
                distance[x,y]=d;
            }
            for(int y=gh;y>=0;y--)for(int x=gw;x>=0;x--)
            {
                float d=distance[x,y];
                if(x<gw)d=Mathf.Min(d,distance[x+1,y]+32);
                if(y<gh){d=Mathf.Min(d,distance[x,y+1]+32);if(x>0)d=Mathf.Min(d,distance[x-1,y+1]+45.255f);if(x<gw)d=Mathf.Min(d,distance[x+1,y+1]+45.255f);}
                distance[x,y]=d;
            }
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
            for(int y=0;y<cells.GetLength(1);y++)for(int x=0;x<cells.GetLength(0);x++)if(cells[x,y])
            {
                // Adjacent cells must share exactly the same vertices. Independent insets
                // opened two-pixel slots at stairs and ceiling/wall junctions.
                float l=x*32,r=(x+1)*32,t=y*32,b=(y+1)*32;int n=vertices.Count;
                vertices.Add(new Vector3(l/32,-t/32));vertices.Add(new Vector3(r/32,-t/32));vertices.Add(new Vector3(r/32,-b/32));vertices.Add(new Vector3(l/32,-b/32));
                uv.Add(new Vector2(distance[x,y],0));uv.Add(new Vector2(distance[x+1,y],0));uv.Add(new Vector2(distance[x+1,y+1],0));uv.Add(new Vector2(distance[x,y+1],0));indices.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            var mesh=new Mesh{name="Continuous source-paper terrain",indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();owned.Add(mesh);
            var body=new GameObject("Continuous original sketch paper",typeof(MeshFilter),typeof(MeshRenderer));body.transform.SetParent(root.transform,false);body.GetComponent<MeshFilter>().sharedMesh=mesh;
            var bodyMat=new Material(shader);bodyMat.mainTexture=Resources.Load<Texture2D>(Path+"body");bodyMat.SetFloat("_Body",1);bodyMat.SetVector("_Period",new Vector4(library.bodyWidth,library.bodyHeight));
            bodyMat.SetTexture("_SoilTex",soil);bodyMat.SetVector("_SoilPeriod",new Vector4(soil.width,soil.height));
            owned.Add(bodyMat);body.GetComponent<MeshRenderer>().sharedMaterial=bodyMat;body.GetComponent<MeshRenderer>().sortingOrder=0;
            // One complete row, not independently cropped art in each cell.
            for(int y=0;y<cells.GetLength(1);y++)for(int x=0;x<cells.GetLength(0);)
            {
                if(!Solid(x,y)||Solid(x,y-1)){x++;continue;}int start=x;while(x<cells.GetLength(0)&&Solid(x,y)&&!Solid(x,y-1))x++;
                float left=start*32,right=x*32,length=right-left,top=y*32;
                // Keep the approved stone family and scale on every stair length.
                // The connected contour underneath owns the wall, including the turn.
                Cap(left,top,length);
            }
            for(int x=0;x<cells.GetLength(0);x++)for(int side=-1;side<=1;side+=2)for(int y=0;y<cells.GetLength(1);)
            {
                if(!Solid(x,y)||Solid(x+side,y)){y++;continue;}int start=y;while(y<cells.GetLength(1)&&Solid(x,y)&&!Solid(x+side,y))y++;
                float length=(y-start)*32;int count=Mathf.Max(1,Mathf.RoundToInt(length/128));float part=length/count;
                for(int i=0;i<count;i++)Draw(side<0?"wall-left":"wall-right",(x+(side>0?1:0))*32,start*32+i*part-1,1,(part+2)/128,2);
            }
            for(int y=0;y<cells.GetLength(1);y++)for(int x=0;x<cells.GetLength(0);)
            {
                if(!Solid(x,y)||Solid(x,y+1)){x++;continue;}int start=x;while(x<cells.GetLength(0)&&Solid(x,y)&&!Solid(x,y+1))x++;
                float length=(x-start)*32,sourceWidth=entries["underside"].logicalWidth;int count=Mathf.Max(1,Mathf.RoundToInt(length/sourceWidth));float part=length/count;
                for(int i=0;i<count;i++)Draw("underside",start*32+i*part-1,(y+1)*32,(part+2)/sourceWidth,1,2);
            }
            for(int index=0;index<data.platforms.Length;index++)
            {
                var p=data.platforms[index];
                var r=Draw("platform",p.x,p.y,1,1,6);
                if(p.vanishing)r.gameObject.name="Vanishing platform art "+index;r.drawMode=SpriteDrawMode.Tiled;r.tileMode=SpriteTileMode.Continuous;
                var e=entries["platform"];r.size=new Vector2((p.w+e.width-e.logicalWidth)/32,e.height/32f);
                // Tiled sprite pivot is a fraction of the full canvas: compensate so the landing anchor stays fixed.
                r.transform.localPosition+=new Vector3((p.w-e.logicalWidth)*r.sprite.pivot.x/r.sprite.rect.width/32,0,0);
            }
            return root;
        }
    }
}
