using System;
using System.Collections.Generic;
using ForestCombatV2;
using UnityEngine;

namespace Stage1VillageExperiment
{
    // Recessed scenery only: no tilemap, occupancy writes, collider or gameplay layer.
    public static class VillageBackdropWalls
    {
        [Serializable] sealed class TileKit
        {
            public string left,right,underside;
            public float cornerRadius,joinOverlap,fade;
            public int maxPerRoom;
            public float[] beamDepths,columnWidths;
            public float maxDrop,minSpacing;
        }
        public static GameObject Build(Transform parent,ForestData data,ICollection<UnityEngine.Object> owned)
        {
            var root=new GameObject("Pass-through pale background walls");root.transform.SetParent(parent,false);
            const string art="Village/ReferenceGround/";
            var soil=Resources.Load<Texture2D>(art+"Bright/soil");
            var sketch=Resources.Load<Texture2D>(art+"body");
            var shader=Resources.Load<Shader>(art+"ReferenceGround");
            if(!soil||!sketch||!shader)throw new InvalidOperationException("Approved terrain soil and hatch pair is required.");
            var kit=JsonUtility.FromJson<TileKit>(Resources.Load<TextAsset>(art+"backdrop-tiles").text);
            var library=JsonUtility.FromJson<VillageReferenceGround.Library>(Resources.Load<TextAsset>(art+"library").text);
            var material=new Material(shader){name="Approved soil - pale pass-through wall",mainTexture=sketch};
            material.SetTexture("_SoilTex",soil);material.SetFloat("_Body",1);
            material.SetFloat("_BackdropFade",kit.fade);material.SetFloat("_BackdropTiles",1);
            material.SetFloat("_BackdropCorner",kit.cornerRadius);
            VillageReferenceGround.Entry Edge(string name,string slot)
            {
                var entry=Array.Find(library.entries,e=>e.name==name);
                var texture=Resources.Load<Texture2D>(art+name);
                if(entry==null||!texture)throw new InvalidOperationException("Missing approved backdrop edge tile: "+name);
                material.SetTexture(slot+"Tex",texture);
                material.SetVector(slot+"Canvas",new Vector4(entry.width,entry.height,entry.anchorX,entry.anchorY));
                return entry;
            }
            var left=Edge(kit.left,"_BackdropLeft");Edge(kit.right,"_BackdropRight");
            var underside=Edge(kit.underside,"_BackdropBottom");
            material.SetVector("_BackdropEdgePeriods",new Vector4(left.logicalHeight,underside.logicalWidth));
            // Reuse the terrain's actual artwork, scale, world UVs and depth shading.
            material.SetVector("_Period",new Vector4(sketch.width,sketch.height));
            material.SetVector("_SoilPeriod",new Vector4(soil.width,soil.height));owned.Add(material);
            bool Solid(int x,int y)=>x>=0&&y>=0&&x<data.cols&&y<data.rows&&data.ground[y*data.cols+x]!=0;
            var counts=new Dictionary<int,int>();
            var placements=new Dictionary<int,List<Vector2>>();
            int index=0;
            foreach(var p in data.platforms??Array.Empty<FoliagePlatform>())
            {
                // A vanishing platform disappears; a soil column under it would be left floating in the air.
                if(p.w<96||p.vanishing)continue;
                ForestRoom room=null;
                foreach(var candidate in data.rooms)
                    if(p.x+p.w*.5f>=candidate.x*32&&p.x+p.w*.5f<(candidate.x+candidate.w)*32&&p.y>=candidate.y*32&&p.y<(candidate.y+candidate.h)*32){room=candidate;break;}
                if(room==null)continue;
                counts.TryGetValue(room.id,out int count);if(count>=kit.maxPerRoom)continue;
                if(!placements.TryGetValue(room.id,out var prior))placements[room.id]=prior=new List<Vector2>();
                var anchor=new Vector2(p.x+p.w*.5f,p.y);
                bool crowded=false;
                foreach(var other in prior)
                    if(Mathf.Abs(other.x-anchor.x)<kit.minSpacing&&Mathf.Abs(other.y-anchor.y)<96){crowded=true;break;}
                if(crowded)continue;
                // Alternate substantial soil masses and open windows without
                // rescaling the original contour/hatching texture pixels.
                int variant=(room.id+count*2)%kit.beamDepths.Length;
                float beamDepth=p.w<144?64:kit.beamDepths[variant];
                float requestedWidth=Mathf.Min(kit.columnWidths[variant%kit.columnWidths.Length],p.w*.6f);
                float columnWidth=requestedWidth,columnX=0,floor=0;bool attached=false;
                for(int widthPass=0;widthPass<3&&!attached;widthPass++)
                {
                    columnWidth=Mathf.Min(requestedWidth,widthPass==0?requestedWidth:widthPass==1?96:64);
                    for(int side=0;side<2&&!attached;side++)
                    {
                        bool right=(index+side)%2==1;columnX=right?p.x+p.w-columnWidth:p.x;
                        int firstColumn=Mathf.CeilToInt(columnX/32),last=Mathf.FloorToInt((columnX+columnWidth)/32);
                        for(int row=Mathf.CeilToInt((p.y+80)/32);row<Mathf.Min(data.rows,Mathf.FloorToInt((p.y+kit.maxDrop)/32));row++)
                        {
                            bool supported=true;
                            for(int x=firstColumn;x<last;x++)if(!Solid(x,row)||Solid(x,row-1)){supported=false;break;}
                            if(!supported)continue;
                            floor=row*32;attached=true;break;
                        }
                    }
                }
                if(!attached)continue;
                float top=p.y+16,beamBottom=top+Mathf.Min(beamDepth,Mathf.Max(32,floor-top-32));
                float bottom=floor+2,rightEdge=p.x+p.w;
                // The canvas extends beyond logical bounds: native contour pixels
                // are never cut by a 32px cell or by the old rectangular mesh edge.
                const float margin=8;
                var mesh=new Mesh{name="Joined native soil edge tile canvas"};
                mesh.vertices=new[]{new Vector3((p.x-margin)/32,-(top-margin)/32),
                    new Vector3((rightEdge+margin)/32,-(top-margin)/32),
                    new Vector3((rightEdge+margin)/32,-(bottom+margin)/32),
                    new Vector3((p.x-margin)/32,-(bottom+margin)/32)};
                mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
                mesh.triangles=new[]{0,1,2,0,2,3};
                mesh.colors=new[]{Color.white,Color.white,Color.white,Color.white};
                mesh.RecalculateBounds();owned.Add(mesh);
                var node=new GameObject("Backdrop support room "+room.id,typeof(MeshFilter),typeof(MeshRenderer));node.transform.SetParent(root.transform,false);
                node.GetComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=node.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=-48;
                var properties=new MaterialPropertyBlock();
                properties.SetVector("_BackdropBeam",new Vector4(p.x,top,rightEdge,beamBottom));
                properties.SetVector("_BackdropColumn",new Vector4(columnX,beamBottom-kit.joinOverlap,columnX+columnWidth,bottom));
                renderer.SetPropertyBlock(properties);
                counts[room.id]=count+1;prior.Add(anchor);index++;
            }
            return root;
        }
    }
}
