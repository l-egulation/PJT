using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace RuralNature32.Editor
{
    // All coordinates here are native pixels with Y down. The exporter converts
    // cell (x,row) to Unity cell (x,-row-1). No artwork is resized or recolored.
    public sealed class NatureComposer
    {
        public const int Size = 32;
        public sealed class NativeImage { public int width, height; public Color32[] pixels; }
        public readonly Dictionary<Vector2Int, Color32[]> tiles = new Dictionary<Vector2Int, Color32[]>();
        readonly HashSet<Vector2Int> ground, platforms;
        readonly Dictionary<string, NativeImage> images;
        readonly int platformStyle,groundStyle;
        readonly RuralNatureTerrain.SketchRegion[] sketchRegions;
        public readonly HashSet<Vector2Int> sketchTiles=new HashSet<Vector2Int>();
        public int sketchChangedPixels;
        readonly int[] order = {0,1,2,1,3,2,0,3,1,0,2};
        readonly Color32 earth = new Color32(33,27,36,255);
        int minX, maxX, minY, maxY;
        public int flippedDraws;
        public NatureComposer(HashSet<Vector2Int> ground, HashSet<Vector2Int> platforms, Dictionary<string, NativeImage> images,int platformStyle=0,int groundStyle=0,RuralNatureTerrain.SketchRegion[] sketchRegions=null)
        {
            this.ground=ground;this.platforms=platforms;this.images=images;this.platformStyle=platformStyle;this.groundStyle=groundStyle;
            this.sketchRegions=sketchRegions??new RuralNatureTerrain.SketchRegion[0];
            if((platformStyle>0||groundStyle>0)&&!images.ContainsKey("clean_platform_left_0"))foreach(var pair in LoadImages(NatureModular.Root+"/Textures"))images.Add(pair.Key,pair.Value);
        }
        void Stamp(string name,int col,int row,bool emptyOnly)
        {
            if(emptyOnly&&G(col,row))return;var im=images[name];
            if(im.width!=32||im.height!=32)throw new InvalidOperationException("Modular tile must be 32x32: "+name);
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)Pixel(col*32+x,row*32+y,im.pixels[y*32+x]);
        }
        public static int FloorDiv(int value,int divisor) => (int)Math.Floor((double)value/divisor);
        static int Mod(int a,int b) => (a%b+b)%b;
        bool G(int x,int y) => ground.Contains(new Vector2Int(x,y));
        bool P(int x,int y) => !G(x,y)&&platforms.Contains(new Vector2Int(x,y));
        int Depth(int x,int y,int direction) { int n=0; while(G(x,y)){n+=32;y+=direction;} return n; }
        int Width(int x,int y,int direction) { int n=0; while(n<64&&G(x,y)){n+=32;x+=direction;} return n; }
        int WallDepth(int x,int y,int direction) { int n=0; while(G(x,y)){n+=32;x+=direction;} return n; }
        void Pixel(int x,int y,Color32 c)
        {
            if(c.a==0)return;
            var key=new Vector2Int(FloorDiv(x,32),FloorDiv(y,32));
            if(!tiles.TryGetValue(key,out var t)){t=new Color32[1024];tiles.Add(key,t);}
            int i=Mod(y,32)*32+Mod(x,32);
            if(c.a==255||t[i].a==0){t[i]=c;return;}
            var d=t[i]; float sa=c.a/255f, da=d.a/255f, a=sa+da*(1-sa);
            t[i]=new Color32((byte)Mathf.RoundToInt((c.r*sa+d.r*da*(1-sa))/a),(byte)Mathf.RoundToInt((c.g*sa+d.g*da*(1-sa))/a),(byte)Mathf.RoundToInt((c.b*sa+d.b*da*(1-sa))/a),(byte)Mathf.RoundToInt(a*255));
        }
        void Fill(int x,int y,int w,int h) { for(int yy=y;yy<y+h;yy++)for(int xx=x;xx<x+w;xx++)Pixel(xx,yy,earth); }
        void Art(string name,int x,int y,int left=int.MinValue,int right=int.MaxValue,int top=int.MinValue,int bottom=int.MaxValue,bool flip=false,int quarterTurn=0,Func<int,int,bool> mask=null)
        {
            var im=images[name];
            int turn=Mod(quarterTurn,4),w=turn%2==0?im.width:im.height,h=turn%2==0?im.height:im.width;
            int l=Math.Max(x,left),r=Math.Min(x+w,right),t=Math.Max(y,top),b=Math.Min(y+h,bottom);
            if(flip&&l<r&&t<b)flippedDraws++;
            for(int py=t;py<b;py++)for(int px=l;px<r;px++)
            {
                if(mask!=null&&!mask(px,py))continue;
                int u=px-x,v=flip?h-1-(py-y):py-y,sx=u,sy=v;
                // Exact integer rotations; no interpolation, scaling or recoloring.
                if(turn==1){sx=v;sy=im.height-1-u;}
                else if(turn==2){sx=im.width-1-u;sy=im.height-1-v;}
                else if(turn==3){sx=im.width-1-v;sy=u;}
                Pixel(px,py,im.pixels[sy*im.width+sx]);
            }
        }
        // Split only the clipping rectangle at cell boundaries. Texture sampling
        // stays continuous. A thin spot cannot truncate an entire long surface.
        void Surface(string name,int drawX,int drawY,int l,int r,int row,bool underside)
        {
            for(int col=FloorDiv(l,32);col*32<r;col++)
            {
                int a=Math.Max(l,col*32),b=Math.Min(r,(col+1)*32);
                if(underside)Art(name,drawX,drawY,a,b,(row+1)*32-Depth(col,row,-1)/2,int.MaxValue,true);
                else Art(name,drawX,drawY,a,b,int.MinValue,row*32+(Depth(col,row,1)+1)/2);
            }
        }
        void WallSurface(string name,int drawX,int drawY,int t,int b,int column,bool right)
        {
            for(int row=FloorDiv(t,32);row*32<b;row++)
            {
                int a=Math.Max(t,row*32),z=Math.Min(b,(row+1)*32),X=column*32;
                int depth=WallDepth(right?column-1:column,row,right?-1:1);
                Art(name,drawX,drawY,right?X-depth/2:int.MinValue,right?int.MaxValue:X+(depth+1)/2,a,z,quarterTurn:right?1:-1);
            }
        }
        void Outer(int x,int y,bool right,bool bottom)
        {
            int col=right?x-1:x,row=bottom?y-1:y,X=x*32,Y=y*32;
            int reach=Math.Min(32,Width(col,row,right?-1:1)/2);
            int depth=Math.Min(42,Depth(col,row,bottom?-1:1)/2);
            int l=right?X-reach:int.MinValue,r=right?int.MaxValue:X+reach,t=bottom?Y-depth:int.MinValue,b=bottom?int.MaxValue:Y+depth;
            Func<int,int,int> miter=(px,py)=>(right?X-1-px:px-X)-(bottom?Y-1-py:py-Y);
            // Join the horizontal arm and a rotated matching corner along a
            // diagonal. Two native pixels overlap so the alpha silhouettes meet.
            Art("rim_outer_"+(right?"left":"right"),X-(right?46:18),Y-32,l,r,t,b,bottom,right?1:-1,(px,py)=>miter(px,py)<=1);
            Art("rim_outer_"+(right?"right":"left"),X-32,Y-(bottom?46:18),l,r,t,b,bottom,mask:(px,py)=>miter(px,py)>=-1);
        }
        void Inner(int X,int Y,bool right,bool bottom)
        {
            Func<int,int,int> miter=(px,py)=>(right?px-X:X-1-px)-(bottom?py-Y:Y-1-py);
            Art("rim_inner_"+(right?"left":"right"),X-(right?48:32),Y-32,flip:bottom,quarterTurn:right?1:-1,mask:(px,py)=>miter(px,py)<=1);
            Art("rim_inner_"+(right?"right":"left"),X-32,Y-(bottom?48:32),flip:bottom,mask:(px,py)=>miter(px,py)>=-1);
        }
        public void Render()
        {
            if(ground.Count+platforms.Count==0)return;
            var all=ground.Concat(platforms).ToArray();
            minX=all.Min(p=>p.x);maxX=all.Max(p=>p.x)+1;minY=all.Min(p=>p.y);maxY=all.Max(p=>p.y)+1;
            if((long)(maxX-minX)*(maxY-minY)>4000000)throw new InvalidOperationException("Terrain bounds are too sparse. Use a separate Rural Nature grid for distant regions.");
            if(groundStyle==0||groundStyle==3)
            {
            foreach(var c in ground)
            {
                int x=c.x,y=c.y,l=G(x-1,y)?0:9,r=G(x+1,y)?0:9,t=G(x,y-1)?0:10,b=G(x,y+1)?0:10;
                Fill(x*32+l,y*32+t,32-l-r,32-t-b);
                if(G(x-1,y)&&G(x,y-1)&&!G(x-1,y-1))Fill(x*32-9,y*32,18,16);
                if(G(x+1,y)&&G(x,y-1)&&!G(x+1,y-1))Fill((x+1)*32-9,y*32,18,16);
                // Symmetric backing at concave ceiling corners.
                if(G(x-1,y)&&G(x,y+1)&&!G(x-1,y+1))Fill(x*32-9,(y+1)*32-16,18,16);
                if(G(x+1,y)&&G(x,y+1)&&!G(x+1,y+1))Fill((x+1)*32-9,(y+1)*32-16,18,16);
            }
            for(int y=minY;y<maxY;y++)for(int x=minX;x<maxX;)
            {
                if(!G(x,y)||G(x,y-1)){x++;continue;}
                int start=x;while(x<maxX&&G(x,y)&&!G(x,y-1))x++;
                int l=start*32+(!G(start-1,y)?22:0),r=x*32-(!G(x,y)?22:0);
                for(int px=FloorDiv(l-16,96)*96;px<r;px+=96)
                { int id=order[Mod(FloorDiv(px,96)+y*3,order.Length)]+1;Surface("rim_top_0"+id,px,y*32-18,l,r,y,false); }
            }
            for(int x=minX;x<=maxX;x++)for(int y=minY;y<maxY;)
            {
                bool right=G(x-1,y)&&!G(x,y),left=G(x,y)&&!G(x-1,y);
                if(!right&&!left){y++;continue;} int start=y;
                while(y<maxY&&(right?(G(x-1,y)&&!G(x,y)):(G(x,y)&&!G(x-1,y))))y++;
                int t=start*32+22,b=y*32-22;
                for(int py=FloorDiv(t-16,96)*96;py<b;py+=96)
                {int id=order[Mod(FloorDiv(py,96)+x*3,order.Length)]+1;WallSurface("rim_top_0"+id,x*32-(right?46:18),py,t,b,x,right);}
            }
            for(int y=minY;y<maxY;y++)for(int x=minX;x<maxX;)
            {
                if(!G(x,y)||G(x,y+1)){x++;continue;}int start=x;
                while(x<maxX&&G(x,y)&&!G(x,y+1))x++;
                int l=start*32+22,r=x*32-22;
                for(int px=FloorDiv(l-16,96)*96;px<r;px+=96)
                {int id=order[Mod(FloorDiv(px,96)+(y+1)*3,order.Length)]+1;Surface("rim_top_0"+id,px,(y+1)*32-46,l,r,y,true);}
            }
            for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)
            {
                bool nw=G(x-1,y-1),ne=G(x,y-1),sw=G(x-1,y),se=G(x,y);int X=x*32,Y=y*32;
                if(!ne&&!sw&&se)Outer(x,y,false,false); // Also handles diagonal-only contacts.
                if(!nw&&!se&&sw)Outer(x,y,true,false);
                if(!nw&&!se&&ne)Outer(x,y,false,true);
                if(!ne&&!sw&&nw)Outer(x,y,true,true);
                if(!nw&&ne&&sw&&se)Inner(X,Y,false,false);
                if(nw&&!ne&&sw&&se)Inner(X,Y,true,false);
                if(nw&&ne&&!sw&&se)Inner(X,Y,false,true);
                if(nw&&ne&&sw&&!se)Inner(X,Y,true,true);
            }
            }
            else NatureModular.RenderGround(ground,groundStyle,Stamp);
            if(groundStyle==3)sketchChangedPixels+=NaturePartialSketch.Apply(tiles,sketchRegions.Where(r=>!r.platform).ToArray(),sketchTiles);
            for(int y=minY;y<maxY;y++)for(int x=minX;x<maxX;)
            {
                if(!P(x,y)){x++;continue;}int start=x;while(x<maxX&&P(x,y))x++;
                int l=start*32,r=x*32,Y=y*32,cap=Math.Min(32,(r-l)/2),tail=Math.Min(58,(r-l)/2);
                if(platformStyle>0)
                {
                    int style=platformStyle==4?Mod(start+y*2,3)+1:platformStyle;
                    NatureModular.RenderPlatform(start,y,x-start,style,Stamp);
                    continue;
                }
                Art("rim_platform_left",l,Y-17,right:l+cap);
                for(int px=l+cap,k=0;px<r-tail;px+=128,k++)Art("rim_platform_mid_0"+(k%2+1),px,Y-17,right:r-tail);
                Art("rim_platform_right",r-58,Y-17,left:r-tail);
            }
            if(platformStyle==6)sketchChangedPixels+=NaturePartialSketch.Apply(tiles,sketchRegions.Where(r=>r.platform).ToArray(),sketchTiles);
        }
        public static Dictionary<string,NativeImage> LoadImages(string folder)
        {
            var result=new Dictionary<string,NativeImage>();
            foreach(var file in Directory.GetFiles(folder,"*.png"))
            {
                var tex=new Texture2D(2,2,TextureFormat.RGBA32,false);
                try
                {
                    if(!tex.LoadImage(File.ReadAllBytes(file)))throw new IOException(file);
                    var bottomUp=tex.GetPixels32();var topDown=new Color32[bottomUp.Length];
                    for(int y=0;y<tex.height;y++)Array.Copy(bottomUp,(tex.height-1-y)*tex.width,topDown,y*tex.width,tex.width);
                    result.Add(Path.GetFileNameWithoutExtension(file),new NativeImage{width=tex.width,height=tex.height,pixels=topDown});
                }
                finally{UnityEngine.Object.DestroyImmediate(tex);}
            }
            return result;
        }
    }
}
