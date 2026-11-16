using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Stage1VillageExperiment
{
    // Renders the approved 32px sprites as 16px quadrants. No collider or geometry edits.
    // The original textures are only sampled by Sprite.Create; pixels are not rescaled/redrawn.
    public static class Stage1TerrainQuarterRenderer
    {
        public struct Rule
        {
            public int sprite, rotation;
            public string role;
            public Rule(int sprite,int rotation,string role){this.sprite=sprite;this.rotation=rotation;this.role=role;}
        }
        public static Rule Select(bool verticalAir,bool horizontalAir,bool diagonalAir,int qx,int qy,int variant)
        {
            bool top=qy==1,left=qx==0;
            if(verticalAir&&horizontalAir)return new Rule(top?(left?3:2):(left?2:3),top?0:180,"outer-corner");
            if(verticalAir)return top?new Rule(variant,0,"surface"):new Rule(7,90,"ceiling");
            if(horizontalAir)return new Rule(left?7:6,0,"wall");
            if(diagonalAir)return new Rule(top?(left?13:10):(left?10:13),top?0:180,"inner-corner");
            return new Rule(14,0,"core");
        }
        // A tread meets a riser at a flat inside corner, regardless of the riser height.
        static bool StairAirRight(Func<int,int,bool> solid,int x,int y)
            =>!solid(x,y)&&solid(x,y+1)&&solid(x+1,y);
        static bool StairAirLeft(Func<int,int,bool> solid,int x,int y)
            =>!solid(x,y)&&solid(x,y+1)&&solid(x-1,y);
        public static Dictionary<string,int> Build(Transform parent,Sprite[] source,bool[,] filled,Func<Sprite,Material> materialFor,ICollection<UnityEngine.Object> owned)
        {
            var counts=new Dictionary<string,int>();
            foreach(bool _ in BuildInRows(parent,source,filled,materialFor,owned,counts)){}
            return counts;
        }
        // The same build, one map row per step, for a chapter composed over frames (VillagePresentation); the tiles are
        // set on their tilemaps after the last row.
        public static IEnumerable<bool> BuildInRows(Transform parent,Sprite[] source,bool[,] filled,Func<Sprite,Material> materialFor,ICollection<UnityEngine.Object> owned,Dictionary<string,int> counts)
        {
            VerifyRules();
            var root=new GameObject("All-neighbor terrain artwork",typeof(Grid));root.transform.SetParent(parent,false);
            root.GetComponent<Grid>().cellSize=new Vector3(.5f,.5f,1);
            var maps=new Dictionary<Texture2D,Tilemap>();
            var positions=new Dictionary<Texture2D,List<Vector3Int>>();
            var tiles=new Dictionary<Texture2D,List<TileBase>>();
            var cache=new Dictionary<string,Tile>();
            int cols=filled.GetLength(0),rows=filled.GetLength(1);int[] variants={0,1,4,5};
            Func<int,int,bool> solid=(x,y)=>x>=0&&x<cols&&y>=0&&y<rows&&filled[x,y];
            Func<int,int,int> backingId=(x,y)=>!solid(x,y-1)?(!solid(x-1,y)?3:!solid(x+1,y)?2:variants[(x*7+y*3)%4]):!solid(x-1,y)?7:!solid(x+1,y)?6:14;
            for(int y=0;y<rows;y++){for(int x=0;x<cols;x++)
            {
                if(!filled[x,y])continue;
                int variant=variants[(x*7+y*3)%4];
                bool up=!solid(x,y-1),down=!solid(x,y+1),left=!solid(x-1,y),right=!solid(x+1,y);
                var backing=up?new Rule(left?3:right?2:variant,0,"interior-gradient"):
                    left?new Rule(7,0,"interior-gradient"):right?new Rule(6,0,"interior-gradient"):
                    down?new Rule(7,90,"interior-gradient"):new Rule(14,0,"core");
                bool stairInnerLeft=StairAirRight(solid,x-1,y-1);
                bool stairInnerRight=StairAirLeft(solid,x+1,y-1);
                Sprite dedicated=null;
                if(!up&&!down&&stairInnerLeft!=stairInnerRight){
                    int northId=backingId(x,y-1),sideId=backingId(x+(stairInnerLeft?-1:1),y);
                    dedicated=Stage1StairCornerFitter.Fit(source[15],source[northId],source[sideId],northId,sideId,stairInnerLeft,owned);
                }
                for(int qy=0;qy<2;qy++)for(int qx=0;qx<2;qx++)
                {
                    int dx=qx==0?-1:1,dy=qy==1?-1:1;
                    var rule=Select(!solid(x,y+dy),!solid(x+dx,y),!solid(x+dx,y+dy),qx,qy,variant);
                    // Keep the approved full 32px soil fade on the inward half of a surface.
                    if(rule.role=="core")rule=backing;
                    // Do not mix unrelated half-rocks across a stair tread or its riser.
                    // The source full-cell sprites already contain a coherent stone silhouette.
                    if(up&&!down)rule=backing;
                    if(!up&&!down&&stairInnerLeft&&!stairInnerRight)rule=new Rule(13,0,"stair-inner-join");
                    if(!up&&!down&&stairInnerRight&&!stairInnerLeft)rule=new Rule(10,0,"stair-inner-join");
                    counts.TryGetValue(rule.role,out int count);counts[rule.role]=count+1;
                    string key=(dedicated?dedicated.name:rule.sprite.ToString())+":"+qx+":"+qy+":"+rule.rotation;
                    if(!cache.TryGetValue(key,out var tile))
                    {
                        // Inverse-rotate the destination quarter to select its source rectangle.
                        Vector2 center=new Vector2(qx==0?8:24,qy==0?8:24)-Vector2.one*16;
                        Vector2 sample=(Vector2)(Quaternion.Euler(0,0,-rule.rotation)*center)+Vector2.one*16;
                        int sx=sample.x<16?0:16,sy=sample.y<16?0:16;
                        var original=dedicated?dedicated:source[rule.sprite];var crop=Sprite.Create(original.texture,new Rect(original.rect.x+sx,original.rect.y+sy,16,16),Vector2.one*.5f,32,0,SpriteMeshType.FullRect);
                        crop.name=original.name+" quarter "+key;owned.Add(crop);
                        tile=ScriptableObject.CreateInstance<Tile>();tile.sprite=crop;tile.colliderType=Tile.ColliderType.None;
                        tile.transform=Matrix4x4.Rotate(Quaternion.Euler(0,0,rule.rotation));owned.Add(tile);cache[key]=tile;
                    }
                    var texture=tile.sprite.texture;
                    if(!maps.TryGetValue(texture,out var map))
                    {
                        var child=new GameObject("Original terrain "+texture.name,typeof(Tilemap),typeof(TilemapRenderer));child.transform.SetParent(root.transform,false);
                        map=child.GetComponent<Tilemap>();map.tileAnchor=new Vector3(.5f,.5f,0);
                        var renderer=child.GetComponent<TilemapRenderer>();renderer.sharedMaterial=materialFor(tile.sprite);renderer.sortingOrder=0;
                        maps[texture]=map;positions[texture]=new List<Vector3Int>();tiles[texture]=new List<TileBase>();
                    }
                    positions[texture].Add(new Vector3Int(x*2+qx,-y*2-2+qy,0));tiles[texture].Add(tile);
                }
            }
            yield return true;}
            foreach(var pair in maps)pair.Value.SetTiles(positions[pair.Key].ToArray(),tiles[pair.Key].ToArray());
            // Omit the pit's air-cell ornaments: they curl above the flat stair tread.
            // The join and its fade are drawn entirely within the occupied ground cells.
        }
        static void VerifyRules()
        {
            // These fail with the preceding up/left/right-only selector: exposed undersides
            // became core and the two upper concave cases also became core.
            if(Select(true,false,false,0,0,0).role!="ceiling")throw new Exception("Ceiling rule missing");
            if(Select(false,false,true,1,1,0).sprite!=10)throw new Exception("Upper-right inner join missing");
            if(Select(false,false,true,0,1,0).sprite!=13)throw new Exception("Upper-left inner join missing");
            Func<int,int,bool> stairs=(x,y)=>y>=3||(x>=1&&y>=2)||(x>=2&&y>=1);
            if(!StairAirRight(stairs,0,2)||!StairAirRight(stairs,1,1))throw new Exception("Consecutive stair tread join not recognized");
            Func<int,int,bool> mirrored=(x,y)=>stairs(2-x,y);
            if(!StairAirLeft(mirrored,2,2)||!StairAirLeft(mirrored,1,1))throw new Exception("Mirrored stair tread join not recognized");
            Func<int,int,bool> tallStairs=(x,y)=>y>=4||(x>=1&&y>=2)||(x>=2&&y>=0);
            if(!StairAirRight(tallStairs,0,3)||!StairAirRight(tallStairs,1,1))throw new Exception("Two-cell stair tread join not recognized");
            Func<int,int,bool> mirroredTall=(x,y)=>tallStairs(2-x,y);
            if(!StairAirLeft(mirroredTall,2,3)||!StairAirLeft(mirroredTall,1,1))throw new Exception("Mirrored two-cell stair tread join not recognized");
            for(int qx=0;qx<2;qx++)for(int qy=0;qy<2;qy++)
            {
                if(Select(true,true,true,qx,qy,0).role!="outer-corner")throw new Exception("Outer join missing");
                if(Select(false,false,false,qx,qy,0).sprite!=14)throw new Exception("Deep fill changed");
            }
        }
    }
}
