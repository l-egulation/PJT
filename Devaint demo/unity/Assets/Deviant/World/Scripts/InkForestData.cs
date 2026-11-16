using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Deviant.World
{
    [Serializable] public class MotionClip
    {
        public string id;
        public Sprite[] frames;
        public float[] seconds;
        public int[] feet;
        public bool loop;
        public float Duration { get { float sum=0; foreach(float t in seconds) sum+=t; return sum; } }
        public int FrameAt(float elapsed)
        {
            if(loop) elapsed=Mathf.Repeat(elapsed,Duration);
            float sum=0;
            for(int i=0;i<seconds.Length;i++){sum+=seconds[i]; if(elapsed<sum-0.000001f)return i;}
            return frames.Length-1;
        }
    }
    [Serializable] public class ForestTheme
    {
        public string id;
        public Sprite far,mid,front;
        public string[] tileIds;
        public Tile[] tiles;
        public Tile Get(string id) { return tiles[Array.IndexOf(tileIds,id)]; }
    }
    [Serializable] public class GroundCell {public string id;public int x,y;}
    [Serializable] public class AirPlatform {public float x,top; public int width;}
    [Serializable] public class IntegrationSource { public MotionClip[] clips;public GroundCell[] tiles;public AirPlatform[] platforms; }
    public class InkForestData : ScriptableObject
    {
        public MotionClip[] clips;
        public ForestTheme[] themes;
        public GroundCell[] cells;
        public AirPlatform[] platforms;
        public Material material;
        public PhysicsMaterial2D frictionless;
        public MotionClip Clip(string id) { return Array.Find(clips,c=>c.id==id); }
        public float FloorTop(float x)
        {
            int col=Mathf.FloorToInt(Mathf.Repeat(x,512)); int top=-100;
            foreach(var cell in cells)if(cell.x==col)top=Mathf.Max(top,cell.y+1);
            return top;
        }
    }
}
