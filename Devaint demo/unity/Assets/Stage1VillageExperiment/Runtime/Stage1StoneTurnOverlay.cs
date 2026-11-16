using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
namespace Stage1VillageExperiment
{
    public static class Stage1StoneTurnOverlay
    {
        public static GameObject Root;
        public static int Count;
        public static void Build(Transform parent,Sprite wallRight,Sprite wallLeft,bool[,] filled,Func<Sprite,Material> materialFor,ICollection<UnityEngine.Object> owned)
        {
            Root=new GameObject("Stone cap turns, existing soil unchanged",typeof(Grid));Root.transform.SetParent(parent,false);
            Tilemap MakeMap(Sprite sprite)
            {
                var child=new GameObject("Native stone turn "+sprite.name,typeof(Tilemap),typeof(TilemapRenderer));child.transform.SetParent(Root.transform,false);
                var map=child.GetComponent<Tilemap>();map.tileAnchor=Vector3.zero;
                var renderer=child.GetComponent<TilemapRenderer>();renderer.sharedMaterial=materialFor(sprite);renderer.sortingOrder=2;
                return map;
            }
            var rightMap=MakeMap(wallRight);var leftMap=MakeMap(wallLeft);
            var r=ScriptableObject.CreateInstance<Tile>();r.sprite=wallRight;r.colliderType=Tile.ColliderType.None;owned.Add(r);
            var l=ScriptableObject.CreateInstance<Tile>();l.sprite=wallLeft;l.colliderType=Tile.ColliderType.None;owned.Add(l);
            int cols=filled.GetLength(0),rows=filled.GetLength(1);Count=0;
            Func<int,int,bool> solid=(x,y)=>x>=0&&x<cols&&y>=0&&y<rows&&filled[x,y];
            for(int y=1;y<rows-1;y++)for(int x=0;x<cols;x++){
                if(!solid(x,y)||!solid(x,y-1)||!solid(x,y+1))continue;
                bool left=!solid(x-1,y-1)&&solid(x-1,y),right=!solid(x+1,y-1)&&solid(x+1,y);
                if(left==right)continue;
                (left?rightMap:leftMap).SetTile(new Vector3Int(left?x:x+1,-y,0),left?r:l);Count++;
            }
            if(Count==0)throw new Exception("No eligible stone cap turns found");
        }
    }
}
