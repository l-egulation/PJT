using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ForestCombatV2
{
    [Serializable] public sealed class RoundedTerrainSprite
    {
        public int x,y,width,height;
        public float pivotX,pivotY;
        public string rgbaSha256;
    }
    [Serializable] public sealed class RoundedTerrainLayout
    {
        public string schema,geometrySha256,atlasSha256;
        public int cols,rows,atlasWidth,atlasHeight;
        public RoundedTerrainSprite[] sprites;
        public VisualCell[] ground,platforms;
    }

    // Baked by the exact HTML renderer, including fillets in adjacent empty cells.
    // Only presentation is replaced; ForestCombatWorld remains the collider owner.
    public static class Chapter1RoundedTerrain
    {
        public static string GeometryHash(ForestData data)
        {
            var key=new StringBuilder().Append(data.cols).Append(',').Append(data.rows).Append('|');
            foreach(var cell in data.ground)key.Append(cell);
            key.Append('|');
            for(int i=0;i<data.platforms.Length;i++)
            {
                if(i>0)key.Append(';');var p=data.platforms[i];
                key.Append(p.x).Append(',').Append(p.y).Append(',').Append(p.w);
                if(p.earthArtwork)key.Append(":earth");
            }
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(key.ToString()))).Replace("-","").ToLowerInvariant();
        }
        public static Tilemap Build(ForestData data,TextAsset json,Texture2D atlas,Transform parent,Material material,List<Sprite> sprites,List<Tile> tiles)
        {
            if(!json || !atlas)throw new InvalidOperationException("Rounded Chapter1 artwork is incomplete; restore the terrain asset bundle.");
            var layout=JsonUtility.FromJson<RoundedTerrainLayout>(json.text);
            bool procedural=data.renderGround!=null && data.renderPlatforms!=null;
            if(layout.schema!="chapter1-rounded-runtime-v2" || (!procedural && (layout.cols!=data.cols || layout.rows!=data.rows || layout.geometrySha256!=GeometryHash(data))))
                throw new InvalidOperationException("Chapter1 terrain layout changed; run tools/chapter1-rounded-terrain/bake.cjs again.");
            if(atlas.width!=layout.atlasWidth || atlas.height!=layout.atlasHeight || atlas.filterMode!=FilterMode.Point || atlas.mipmapCount!=1)
                throw new InvalidOperationException("Chapter1 terrain atlas must retain native dimensions, Point filtering and no mipmaps.");
            var grid=new GameObject("Chapter1 rounded terrain",typeof(Grid));grid.transform.SetParent(parent,false);
            var library=new Tile[layout.sprites.Length];
            for(int i=0;i<library.Length;i++)
            {
                var s=layout.sprites[i];
                var sprite=Sprite.Create(atlas,new Rect(s.x,atlas.height-s.y-s.height,s.width,s.height),new Vector2(s.pivotX,s.pivotY),32,0,SpriteMeshType.FullRect);
                sprite.name="Chapter1 rounded "+i;sprites.Add(sprite);
                var tile=ScriptableObject.CreateInstance<Tile>();tile.sprite=sprite;tile.colliderType=Tile.ColliderType.None;tiles.Add(tile);library[i]=tile;
            }
            var ground=Place("Rounded ground and flat interior",procedural?data.renderGround:layout.ground,library,grid.transform,material,0);
            var platformCells=procedural?data.renderPlatforms:layout.platforms;
            var aligned=new List<VisualCell>();var halfTile=new List<VisualCell>();
            foreach(var cell in platformCells)(cell.offsetY>.01f?halfTile:aligned).Add(cell);
            Place("Drop-through wood platforms",aligned.ToArray(),library,grid.transform,material,1);
            if(halfTile.Count>0)Place("Half-tile wood platforms",halfTile.ToArray(),library,grid.transform,material,1,.5f);
            return ground;
        }
        static Tilemap Place(string name,VisualCell[] cells,Tile[] library,Transform parent,Material material,int order,float offsetY=0)
        {
            var go=new GameObject(name,typeof(Tilemap),typeof(TilemapRenderer));go.transform.SetParent(parent,false);
            go.transform.localPosition=new Vector3(0,-offsetY,0);
            var map=go.GetComponent<Tilemap>();map.tileAnchor=new Vector3(.5f,.5f,0);
            var renderer=go.GetComponent<TilemapRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=order;
            renderer.sortOrder=TilemapRenderer.SortOrder.TopLeft;
            var positions=new Vector3Int[cells.Length];var values=new TileBase[cells.Length];
            for(int i=0;i<cells.Length;i++){var c=cells[i];positions[i]=new Vector3Int(c.x,-c.y-1,0);values[i]=library[c.id];}
            map.SetTiles(positions,values);map.CompressBounds();return map;
        }
    }
}
