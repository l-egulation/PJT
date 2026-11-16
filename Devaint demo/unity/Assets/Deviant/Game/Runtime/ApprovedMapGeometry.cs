using System.Collections.Generic;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    // Live walkable space, cached until discovery changes. Screen-space strokes stay readable at every zoom.
    // No screenshot, preview geometry or second world camera is used by the game map.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ApprovedMapGeometry : MaskableGraphic
    {
        struct Patch { public Rect Box; public Color Tint; public bool Stroke; }
        readonly List<Patch> patches = new List<Patch>();
        Chapter1FullMap map;
        ForestData data;
        int discovered = -1;
        Vector2 centre;
        float scale;
        public float StrokeWidth=1.2f,FillOpacity=1;
        public int DiscoveredCount => discovered;
        public int PatchCount => patches.Count;
        static readonly Color Edge = new Color32(214,215,209,220);
        public static Color RoomColor(string kind) => kind switch
        {
            "shop" or "maintenance" => new Color32(195,173,107,45),
            "boss" => new Color32(190,100,112,49),
            "elite" => new Color32(169,140,185,50),
            "seal" => new Color32(116,185,172,48),
            "chest" => new Color32(183,162,128,49),
            _ => new Color32(181,188,196,24)
        };
        public void Present(Chapter1FullMap source, Chapter1MapViewport viewport)
        {
            int count=0;foreach(var room in source.MapData.rooms??System.Array.Empty<ForestRoom>())if(source.IsRoomDiscovered(room.id))count++;
            bool rebuild=map!=source||data!=source.MapData||count!=discovered;
            map=source;data=source.MapData;
            if(rebuild){discovered=count;Rebuild();}
            float next=viewport.Scale(rectTransform.sizeDelta);
            if(rebuild||centre!=viewport.Center||scale!=next){centre=viewport.Center;scale=next;SetVerticesDirty();}
        }
        void Rebuild()
        {
            patches.Clear();int cols=data.cols,rows=data.rows;
            var visible=new bool[cols*rows];var colors=new Color[visible.Length];
            void Reveal(int x,int y,int w,int h,Color tint)
            {
                for(int yy=Mathf.Max(0,y);yy<Mathf.Min(rows,y+h);yy++)
                    for(int xx=Mathf.Max(0,x);xx<Mathf.Min(cols,x+w);xx++)
                    {int i=yy*cols+xx;if(data.ground[i]==0){visible[i]=true;colors[i]=tint;}}
            }
            foreach(var room in data.rooms??System.Array.Empty<ForestRoom>())
                if(map.IsRoomDiscovered(room.id))Reveal(room.x,room.y,room.w,room.h,RoomColor(room.kind));
            foreach(var passage in data.passages??System.Array.Empty<ForestPassage>())
            {
                if(!map.IsRoomDiscovered(passage.from)&&!map.IsRoomDiscovered(passage.to))continue;
                if(passage.spaces!=null)foreach(var box in passage.spaces)Reveal(box.x,box.y,box.w,box.h,RoomColor(null));
                else Reveal(passage.x,passage.y,passage.w,passage.h,RoomColor(null));
            }
            // Legacy sources without rooms retain their authored terrain map rather than disclosing air outside rooms.
            bool At(int x,int y)=>x>=0&&y>=0&&x<cols&&y<rows&&visible[y*cols+x];
            // Discovery ends are open air, not walls. Stroke only a revealed cell against real solid terrain.
            bool Solid(int x,int y)=>x<0||y<0||x>=cols||y>=rows||data.ground[y*cols+x]!=0;
            bool HorizontalEdge(int x,int y)=>At(x,y)&&Solid(x,y-1)||At(x,y-1)&&Solid(x,y);
            bool VerticalEdge(int x,int y)=>At(x,y)&&Solid(x-1,y)||At(x-1,y)&&Solid(x,y);
            for(int y=0;y<rows;y++)for(int x=0;x<cols;)
            {
                if(!At(x,y)){x++;continue;}int start=x;var tint=colors[y*cols+x];
                while(x<cols&&At(x,y)&&colors[y*cols+x]==tint)x++;
                patches.Add(new Patch{Box=new Rect(start*32,y*32,(x-start)*32,32),Tint=tint});
            }
            // Merge collinear boundary segments. This keeps the UI mesh small even in a large generated chapter.
            for(int y=0;y<=rows;y++)for(int x=0;x<cols;)
            {
                if(!HorizontalEdge(x,y)){x++;continue;}int start=x++;
                while(x<cols&&HorizontalEdge(x,y))x++;
                patches.Add(new Patch{Box=new Rect(start*32,y*32,(x-start)*32,0),Tint=Edge,Stroke=true});
            }
            for(int x=0;x<=cols;x++)for(int y=0;y<rows;)
            {
                if(!VerticalEdge(x,y)){y++;continue;}int start=y++;
                while(y<rows&&VerticalEdge(x,y))y++;
                patches.Add(new Patch{Box=new Rect(x*32,start*32,0,(y-start)*32),Tint=Edge,Stroke=true});
            }
            foreach(var platform in data.platforms??System.Array.Empty<FoliagePlatform>())
                if(At(platform.x/32,platform.y/32)||At(platform.x/32,platform.y/32-1))
                    patches.Add(new Patch{Box=new Rect(platform.x,platform.y,platform.w,0),Tint=new Color32(169,186,203,200),Stroke=true});
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var bounds=rectTransform.rect;
            foreach(var patch in patches)
            {
                var a=(patch.Box.position-centre)*scale+bounds.size/2;
                var box=new Rect(a,patch.Box.size*scale);
                if(patch.Stroke){if(box.width==0){box.x-=StrokeWidth/2;box.width=StrokeWidth;}if(box.height==0){box.y-=StrokeWidth/2;box.height=StrokeWidth;}}
                // Clip on the CPU as well as RectMask2D: large maps never exceed the UI vertex limit while zoomed in.
                float x0=Mathf.Max(0,box.xMin),x1=Mathf.Min(bounds.width,box.xMax);
                float y0=Mathf.Max(0,box.yMin),y1=Mathf.Min(bounds.height,box.yMax);
                if(x1<=x0||y1<=y0)continue;
                var tint=patch.Tint;if(!patch.Stroke)tint.a=Mathf.Clamp01(tint.a*FillOpacity);
                int i=vh.currentVertCount;
                vh.AddVert(new Vector3(bounds.xMin+x0,bounds.yMax-y0),tint,Vector2.zero);
                vh.AddVert(new Vector3(bounds.xMin+x1,bounds.yMax-y0),tint,Vector2.zero);
                vh.AddVert(new Vector3(bounds.xMin+x1,bounds.yMax-y1),tint,Vector2.zero);
                vh.AddVert(new Vector3(bounds.xMin+x0,bounds.yMax-y1),tint,Vector2.zero);
                vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
            }
        }
    }
}
