using System;
using System.Collections.Generic;
using UnityEngine;

namespace Stage1VillageExperiment
{
    // Dedicated soil corners replace all four original quarters. When the opposite
    // wall is exposed, complete only its sketch drawing; keep the color renderer intact.
    public static class VillageSketchStairWallCompletion
    {
        static bool Solid(bool[,] filled,int cols,int rows,int x,int y)
            =>x>=0&&x<cols&&y>=0&&y<rows&&filled[x,y];

        public static void Build(Transform parent,bool[,] filled,Shader shader,ICollection<UnityEngine.Object> owned)
        {
            int cols=filled.GetLength(0),rows=filled.GetLength(1);
            Transform root=null;
            Sprite transparent=null;
            Material leftMaterial=null,rightMaterial=null;
            for(int y=1;y<rows-1;y++)for(int x=0;x<cols;x++)
            {
                if(!filled[x,y]||!filled[x,y-1]||!filled[x,y+1])continue;
                bool innerLeft=!Solid(filled,cols,rows,x-1,y-1)&&Solid(filled,cols,rows,x-1,y);
                bool innerRight=!Solid(filled,cols,rows,x+1,y-1)&&Solid(filled,cols,rows,x+1,y);
                if(innerLeft==innerRight||Solid(filled,cols,rows,x+(innerLeft?1:-1),y))continue;
                if(!root)
                {
                    root=new GameObject("Sketch-only dedicated stair walls").transform;
                    root.SetParent(parent,false);
                    var clear=new Texture2D(1,1,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
                    clear.SetPixel(0,0,Color.clear);clear.Apply();owned.Add(clear);
                    transparent=Sprite.Create(clear,new Rect(0,0,1,1),Vector2.one*.5f,1,0,SpriteMeshType.FullRect);
                    owned.Add(transparent);
                }
                var material=innerLeft?rightMaterial:leftMaterial;
                if(!material)
                {
                    string role=innerLeft?"07_wall_right_32x32":"08_wall_left_32x32";
                    var sketch=Resources.Load<Texture2D>("Village/reference-sketch/color/terrain/"+role);
                    if(!sketch)throw new InvalidOperationException("Missing registered stair wall: "+role);
                    material=new Material(shader){name="Sketch-only exposed stair "+role};
                    material.SetTexture("_SketchTex",sketch);material.SetFloat("_HasSketch",1);owned.Add(material);
                    if(innerLeft)rightMaterial=material;else leftMaterial=material;
                }
                var wall=new GameObject(innerLeft?"Sketch right wall":"Sketch left wall",typeof(SpriteRenderer));
                wall.transform.SetParent(root,false);wall.transform.localPosition=new Vector3(x+.5f,-y-.5f,0);
                var renderer=wall.GetComponent<SpriteRenderer>();renderer.sprite=transparent;
                renderer.sharedMaterial=material;renderer.sortingOrder=1;
            }
        }
    }
}
