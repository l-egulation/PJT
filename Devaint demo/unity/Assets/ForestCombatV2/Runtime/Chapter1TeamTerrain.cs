using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ForestCombatV2
{
    public static class Chapter1TeamTerrain
    {
        static readonly int[] SurfaceVariants={0,1,4,5};
        // IDs are zero-based indices into the team's documented 15-tile palette.
        public static int SurfaceTile(ForestData data,int x,int y,out bool flipY)
        {
            bool Solid(int xx,int yy)=>xx<0||yy<0||xx>=data.cols||yy>=data.rows||data.ground[yy*data.cols+xx]!=0;
            bool up=Solid(x,y-1),down=Solid(x,y+1),left=Solid(x-1,y),right=Solid(x+1,y);
            flipY=false;
            int surface=SurfaceVariants[(x*13+y*7)&3];
            if(!up)return !left?3:!right?2:surface;
            if(!down){flipY=true;return !left?3:!right?2:surface;}
            if(!left)return 7;
            if(!right)return 6;
            if(!Solid(x+1,y-1))return 10;
            if(!Solid(x-1,y-1))return 13;
            if(!Solid(x+1,y+1)){flipY=true;return 10;}
            if(!Solid(x-1,y+1)){flipY=true;return 13;}
            return 14;
        }
        public static Tilemap Build(ForestData data,Chapter1TeamTerrainPalette palette,Transform parent,Material material,List<Tile> ownedTiles)
        {
            if(!palette)throw new InvalidOperationException("Chapter1TeamSketch palette is missing.");
            palette.Validate();
            var grid=new GameObject("Team sketch terrain",typeof(Grid));grid.transform.SetParent(parent,false);
            Tile Make(Sprite sprite,bool flip=false)
            {
                var tile=ScriptableObject.CreateInstance<Tile>();tile.sprite=sprite;tile.colliderType=Tile.ColliderType.None;
                if(flip)tile.transform=Matrix4x4.Scale(new Vector3(1,-1,1));
                ownedTiles.Add(tile);return tile;
            }
            var normal=new Tile[15];var flipped=new Tile[15];
            for(int i=0;i<15;i++){normal[i]=Make(palette.terrain[i]);flipped[i]=Make(palette.terrain[i],true);}
            var positions=new List<Vector3Int>();var fills=new List<TileBase>();var borders=new List<TileBase>();
            for(int y=0;y<data.rows;y++)for(int x=0;x<data.cols;x++)
            {
                if(data.ground[y*data.cols+x]==0)continue;
                positions.Add(new Vector3Int(x,-y-1,0)); fills.Add(normal[14]);
                int id=SurfaceTile(data,x,y,out bool flip);borders.Add((flip?flipped:normal)[id]);
            }
            // Opaque approved core underneath transparent corner art keeps every solid cell visually solid.
            Place("Sketch solid core",grid.transform,material,-1,0,positions,fills);
            var ground=Place("Sketch terrain edges",grid.transform,material,0,0,positions,borders);
            var deck=new Tile[5];for(int i=0;i<5;i++)deck[i]=Make(palette.platforms[i]);
            var groups=new Dictionary<int,(List<Vector3Int> positions,List<TileBase> tiles)>();
            for(int index=0;index<data.platforms.Length;index++)
            {
                var p=data.platforms[index];
                if(p.x%32!=0||p.w%32!=0)throw new InvalidOperationException("Team platform art needs whole-tile horizontal placement.");
                int offset=((p.y%32)+32)%32;
                // A vanishing deck owns its own tilemap so it can fade alone.
                int key=p.vanishing?-1-index:offset;
                if(!groups.TryGetValue(key,out var group)){group=(new List<Vector3Int>(),new List<TileBase>());groups.Add(key,group);}
                int width=p.w/32;
                for(int i=0;i<width;i++)
                {
                    int id=i==0?0:i==width-1?4:1+(i-1)%3;
                    group.positions.Add(new Vector3Int(p.x/32+i,-Mathf.FloorToInt(p.y/32f)-1,0));
                    group.tiles.Add(deck[id]);
                }
            }
            foreach(var pair in groups)
            {
                if(pair.Key<0)
                {
                    int index=-1-pair.Key,offset=((data.platforms[index].y%32)+32)%32;
                    Place("Vanishing platform art "+index,grid.transform,material,1,-offset/32f,pair.Value.positions,pair.Value.tiles);
                }
                else Place("Sketch drop-through platforms "+pair.Key,grid.transform,material,1,-pair.Key/32f,pair.Value.positions,pair.Value.tiles);
            }
            return ground;
        }
        static Tilemap Place(string name,Transform parent,Material material,int order,float offset,List<Vector3Int> positions,List<TileBase> tiles)
        {
            var go=new GameObject(name,typeof(Tilemap),typeof(TilemapRenderer));go.transform.SetParent(parent,false);
            go.transform.localPosition=new Vector3(0,offset,0);
            var map=go.GetComponent<Tilemap>();map.tileAnchor=new Vector3(.5f,.5f,0);
            var renderer=go.GetComponent<TilemapRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=order;
            map.SetTiles(positions.ToArray(),tiles.ToArray());map.CompressBounds();return map;
        }
    }
}
