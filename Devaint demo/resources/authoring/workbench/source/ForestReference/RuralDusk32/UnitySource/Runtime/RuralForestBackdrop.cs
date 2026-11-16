using System;
using UnityEngine;

namespace RuralNature32
{
    // Independent sprites, with native pixel scale and camera-relative parallax.
    // No reference to the player or its animations.
    [ExecuteAlways]
    public sealed class RuralForestBackdrop : MonoBehaviour
    {
        [Serializable] public class Layer
        {public string id;public float parallax,vertical,phase,baseCrop,minVertical,maxVertical=32;public int width=768,height=512;public bool repeatBottom,anchorToTerrain;public SpriteRenderer[] sprites;}
        [Serializable] public class SurfaceColumn { public int[] heights; }
        public SurfaceColumn[] terrainColumns;
        public float GroundReference(float cameraX,float cameraY)
        {
            if(terrainColumns==null||terrainColumns.Length==0)return cameraY+354;
            var column=terrainColumns[Mathf.Clamp(Mathf.FloorToInt((cameraX+432)/32),0,terrainColumns.Length-1)];
            if(column.heights==null||column.heights.Length==0)return cameraY+354;
            foreach(int y in column.heights)if(y>=cameraY+354-16)return y;
            return column.heights[column.heights.Length-1];
        }
        public float LayerTop(Layer layer,float cameraX,float cameraY)
        {
            if(!layer.anchorToTerrain)return -Mathf.RoundToInt(Mathf.Clamp(layer.baseCrop+cameraY*layer.vertical,layer.minVertical,layer.maxVertical));
            float rise=layer.anchorToTerrain?Mathf.Max(0,GroundReference(cameraX,cameraY)-cameraY-354):0;
            return -Mathf.RoundToInt(Mathf.Max(0,layer.baseCrop-rise));
        }
        public Camera sceneCamera;
        public Layer[] layers;
        void LateUpdate(){PositionNow();}
        public void PositionNow()
        {
            if(!sceneCamera||layers==null)return;
            float cameraX=sceneCamera.transform.position.x*32-480,cameraY=-sceneCamera.transform.position.y*32-270;
            foreach(var layer in layers)
            {
                float offset=cameraX*layer.parallax+layer.phase;int first=Mathf.FloorToInt(offset/layer.width)-1;
                float top=LayerTop(layer,cameraX,cameraY);
                for(int i=0;i<layer.sprites.Length;i++)
                {
                    int tile=first+i%3;bool bottom=layer.repeatBottom&&i>=3;var sprite=layer.sprites[i];if(!sprite)continue;
                    float x=Mathf.Round(tile*layer.width-offset)+layer.width/2f;
                    sprite.transform.position=new Vector3((cameraX+x)/32f,-(cameraY+top+layer.height*(bottom?1.5f:.5f))/32f,0);
                    sprite.flipX=((tile%2)+2)%2==1;
                    sprite.flipY=bottom;
                }
            }
        }
    }
}
