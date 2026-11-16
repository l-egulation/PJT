using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Deviant.Chapter2
{
    // Visual selection only: authored surfaces, colliders and movement remain owned by Chapter2Terrain.
    public sealed class Chapter2WaterwayArt : MonoBehaviour
    {
        public const string Root="Chapter2Waterway/";
        sealed class Batch { public readonly List<Vector3> v=new List<Vector3>();public readonly List<Vector2> uv=new List<Vector2>();public readonly List<int> t=new List<int>(); }
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly Dictionary<int,int[]> contexts=new Dictionary<int,int[]>();
        Texture2D atlas;int atlasRows;readonly Dictionary<Texture,Batch> batches=new Dictionary<Texture,Batch>();
        readonly HashSet<Vector2Int> solid=new HashSet<Vector2Int>();
        static int Hash(int x,int y){unchecked{return (int)(((uint)x*73856093u^(uint)y*19349663u)*2654435761u)&int.MaxValue;}}
        bool Solid(int x,int y)=>solid.Contains(new Vector2Int(x,y));
        int Pattern(int x,int y){int p=0;for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++)if(Solid(x+dx,y-dy))p|=1<<((dy+2)*5+dx+2);return p;}
        static int Bits(int v){int n=0;while(v!=0){v&=v-1;n++;}return n;}
        int Cell(int x,int y)
        {
            // Ordered wide-moss motifs survive random placement; the lower half follows its surface.
            int topY=!Solid(x,y+1)?y:!Solid(x,y+2)?y+1:int.MinValue;
            if(topY!=int.MinValue&&Solid(x-1,topY)&&Solid(x+1,topY)&&Solid(x,topY-1)){
                int block=Mathf.FloorToInt(x/7f),offset=x-block*7,h=Hash(block,topY),kind=h%100<13?5+h%2:h%100<38?3+h%2:h%3;
                int width=WaterwayTileData.MotifWidths[kind];if(width>1&&offset>=width){kind=Hash(x,topY)%3;width=1;}
                int col=width>1?offset:0;return WaterwayTileData.MotifStarts[kind]+(y==topY?0:width)+col;
            }
            int pattern=Pattern(x,y);
            if(!contexts.TryGetValue(pattern,out var candidates)){
                int best=int.MaxValue,index=0,cardinal=(1<<7)|(1<<11)|(1<<13)|(1<<17);
                for(int i=0;i<WaterwayTileData.Patterns.Length;i++){int diff=pattern^WaterwayTileData.Patterns[i];int cost=Bits(diff)+Bits(diff&cardinal)*40;if(cost<best){best=cost;index=i;}}
                contexts[pattern]=candidates=WaterwayTileData.Choices[index];
            }
            return candidates[Hash(x,y)%candidates.Length];
        }
        void Quad(Texture texture,Rect uv,float x,float y,float w,float h,int order=0)
        {
            if(!batches.TryGetValue(texture,out var b))batches[texture]=b=new Batch();int i=b.v.Count;
            b.v.AddRange(new[]{new Vector3(x,y),new Vector3(x+w,y),new Vector3(x+w,y+h),new Vector3(x,y+h)});
            b.uv.AddRange(new[]{new Vector2(uv.xMin,uv.yMin),new Vector2(uv.xMax,uv.yMin),new Vector2(uv.xMax,uv.yMax),new Vector2(uv.xMin,uv.yMax)});b.t.AddRange(new[]{i,i+2,i+1,i,i+3,i+2});
        }
        void Tile(int tile,float x,float y,float w,float h,float sx,float sy){float u=tile%32/32f,v=1-(tile/32+1)/(float)atlasRows;Quad(atlas,new Rect(u+sx/32,v+sy/atlasRows,w/32,h/atlasRows),x,y,w,h);}
        // v26 화풍(2026-11-12 채택): 땅 속은 월드에 고정한 어두운 바위, 그 위에 칸마다 주변 8칸 모양(256)×변형 4 의 32×48 타일
        // (위아래 8px 는 공기 칸으로 넘치는 이끼·물때), 통과 발판은 같은 램프의 돌판 32×18. 부품이 배치돼 있지 않으면 예전 그림을 쓴다.
        public const string ModernRoot=Root+"Modern/";
        static readonly Vector2Int[] Around={new Vector2Int(-1,1),new Vector2Int(0,1),new Vector2Int(1,1),new Vector2Int(-1,0),new Vector2Int(1,0),new Vector2Int(-1,-1),new Vector2Int(0,-1),new Vector2Int(1,-1)};
        public bool Modern{get;private set;}
        void BuildModern(C2Layout layout,Shader shader,Texture2D tiles)
        {
            Modern=true;var rock=Resources.Load<Texture2D>(ModernRoot+"rock");if(!rock)throw new InvalidOperationException("v26 sewer rock missing; restore deployment assets.");
            foreach(var s in layout.surfaces.Where(s=>!s.oneWay))for(int x=Mathf.FloorToInt(s.x);x<Mathf.CeilToInt(s.x+s.w);x++)for(int y=Mathf.FloorToInt(s.y);y<Mathf.CeilToInt(s.y+s.h);y++)solid.Add(new Vector2Int(x,y));
            float cw=32f/tiles.width,ch=48f/tiles.height;int columns=tiles.width/32;
            var drawn=solid.ToList();
            // 보스방 바닥 3u 는 전장(RoseBlueStage)이 그림 바닥으로 그리므로 지형에서 빠져 있다. 그대로면 그 아래·옆 땅이 빈 칸을 보고
            // 윗면 이끼·옆면 테두리를 그려 바닥 밑에 턱이 생긴다(사용자 2026-11-13). 모양 판정에만 땅으로 치고 그리지는 않는다.
            // 바닥 입구(아래쪽 통로가 보스방으로 올라오는 구멍)는 실제로 비어 있으니 뺀다.
            if(layout.boss!=null){var holes=(layout.ports??new C2Port[0]).Where(p=>p.roomId=="B"&&p.enabled&&p.side=="bottom").ToArray();
                for(int x=Mathf.FloorToInt(layout.boss.x);x<Mathf.CeilToInt(layout.boss.x+RoseBlueStage.Width);x++){if(holes.Any(h=>x+.5f>h.x-h.opening/2&&x+.5f<h.x+h.opening/2))continue;for(int y=Mathf.FloorToInt(layout.boss.y)-3;y<Mathf.FloorToInt(layout.boss.y);y++)solid.Add(new Vector2Int(x,y));}}
            foreach(var c in drawn)
            {
                Quad(rock,new Rect(c.x*32f/rock.width,c.y*32f/rock.height,32f/rock.width,32f/rock.height),c.x,c.y,1,1);
                int mask=0;for(int k=0;k<8;k++)if(Solid(c.x+Around[k].x,c.y+Around[k].y))mask|=1<<k;
                int i=mask*4+Hash(c.x,c.y)%4;
                Quad(tiles,new Rect(i%columns*cw,1-(i/columns+1)*ch,cw,ch),c.x,c.y-.25f,1,1.5f);
            }
            foreach(var s in layout.surfaces.Where(s=>s.oneWay))
            {
                float top=s.y+s.h;bool lw=Solid(Mathf.FloorToInt(s.x-.01f),Mathf.FloorToInt(top-.1f)),rw=Solid(Mathf.FloorToInt(s.x+s.w+.01f),Mathf.FloorToInt(top-.1f));
                int count=Mathf.CeilToInt(s.w-.001f);
                for(int n=0;n<count;n++)
                {
                    string kind=count==1?(lw&&rw?"single-both-wall":lw?"single-left-wall":rw?"single-right-wall":"single"):n==0?(lw?"left-wall":"left-free"):n==count-1?(rw?"right-wall":"right-free"):"middle-"+"abc"[(n-1)%3];
                    var tex=Resources.Load<Texture2D>(ModernRoot+"deck-"+kind);if(!tex)throw new InvalidOperationException("v26 sewer deck missing: "+kind);
                    float w=Mathf.Min(1,s.w-n),u=n==count-1&&n>0?1-w:0;
                    Quad(tex,new Rect(u,0,w,1),s.x+n,top-15/32f,w,18/32f);
                }
            }
            foreach(var pair in batches){var go=new GameObject("Sewer v26 terrain · "+pair.Key.name);go.transform.SetParent(transform,false);var m=new Mesh{indexFormat=IndexFormat.UInt32};m.SetVertices(pair.Value.v);m.SetUVs(0,pair.Value.uv);m.SetTriangles(pair.Value.t,0);m.RecalculateBounds();var mat=new Material(shader){mainTexture=pair.Key};owned.Add(m);owned.Add(mat);go.AddComponent<MeshFilter>().sharedMesh=m;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.sortingOrder=pair.Key==rock?0:pair.Key==tiles?1:2;}
        }
        public void Build(C2Layout layout,Shader shader)
        {
            var modern=Resources.Load<Texture2D>(ModernRoot+"terrain-atlas");if(modern){BuildModern(layout,shader,modern);return;}
            atlas=Resources.Load<Texture2D>(Root+"WaterwayRuntimeAtlas");if(!atlas)throw new InvalidOperationException("Approved Chapter2 waterway atlas missing; restore deployment assets.");atlasRows=atlas.height/32;
            foreach(var s in layout.surfaces.Where(s=>!s.oneWay))for(int x=Mathf.FloorToInt(s.x);x<Mathf.CeilToInt(s.x+s.w);x++)for(int y=Mathf.FloorToInt(s.y);y<Mathf.CeilToInt(s.y+s.h);y++)solid.Add(new Vector2Int(x,y));
            int core=WaterwayTileData.Choices[Array.IndexOf(WaterwayTileData.Patterns,(1<<25)-1)][0];
            foreach(var s in layout.surfaces){
                if(s.oneWay){int count=Mathf.CeilToInt(s.w);for(int n=0;n<count;n++){bool last=n==count-1;string key=n==0?"platform-left":last?"platform-right":"platform-middle";var tex=Resources.Load<Texture2D>(Root+key);float w=Mathf.Min(1,s.w-n);float u=last&&n>0?1-w:0;Quad(tex,new Rect(u,0,w,1),s.x+n,s.y+s.h-1.5f,w,2);}continue;}
                for(int x=Mathf.FloorToInt(s.x);x<Mathf.CeilToInt(s.x+s.w);x++)for(int y=Mathf.FloorToInt(s.y);y<Mathf.CeilToInt(s.y+s.h);y++){
                    float xx=Mathf.Max(x,s.x),yy=Mathf.Max(y,s.y),w=Mathf.Min(x+1,s.x+s.w)-xx,h=Mathf.Min(y+1,s.y+s.h)-yy;
                    Tile(core,xx,yy,w,h,xx-x,yy-y);Tile(Cell(x,y),xx,yy,w,h,xx-x,yy-y);
                }
            }
            foreach(var pair in batches){var go=new GameObject("Approved sewer terrain · "+pair.Key.name);go.transform.SetParent(transform,false);var m=new Mesh{indexFormat=IndexFormat.UInt32};m.SetVertices(pair.Value.v);m.SetUVs(0,pair.Value.uv);m.SetTriangles(pair.Value.t,0);m.RecalculateBounds();var mat=new Material(shader){mainTexture=pair.Key};owned.Add(m);owned.Add(mat);go.AddComponent<MeshFilter>().sharedMesh=m;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.sortingOrder=pair.Key==atlas?0:1;}
        }
        void OnDestroy(){foreach(var o in owned)if(o)Destroy(o);}
    }
}
