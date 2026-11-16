using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Text.Json;

public static class LawnKit
{
    public const int Cell=32, Surface=8;
    static string root;
    static readonly int[] dx={0,1,0,-1,1,1,-1,-1},dy={-1,0,1,0,-1,1,1,-1};
    static readonly Color[] color={Color.Transparent,Color.FromArgb(63,62,60),Color.FromArgb(111,110,105),
        Color.FromArgb(160,157,150),Color.FromArgb(204,199,190),Color.FromArgb(254,247,238),Color.FromArgb(255,252,247)};
    static bool Has(int m,int flag)=>(m&flag)!=0;
    public static int Canonical(int m) {
        int result=m&15;
        for(int i=0;i<4;i++) if(Has(m,1<<i)&&Has(m,1<<((i+1)%4))&&Has(m,16<<i)) result|=16<<i;
        return result;
    }
    static string Name(int mask,int v)=>$"tile-{mask:D3}-{v}";
    static void Stamp(Bitmap image,int ox,int oy,string[] rows) {
        for(int y=0;y<rows.Length;y++) for(int x=0;x<rows[y].Length;x++) {
            char p=rows[y][x];
            if(p>='1'&&p<='6') image.SetPixel(ox+x,oy+y,color[p-'0']);
        }
    }
    // Draw from scratch on a 16x16 art grid. Every art pixel becomes 2x2 native pixels.
    static readonly string[][] blades={
        new[]{".....11..","....161..","11.161.11","161161161",".1111111."},
        new[]{".11......",".161...11","..161.161","11.16161.",".111111.."},
        new[]{"......11.",".....161.","11..161..","161161.11",".1111111."},
        new[]{"...11....","...161.11","11.161161","161.1611.",".111111.."}
    };
    static Bitmap Terrain(int m,int variant) {
        bool n=Has(m,1),e=Has(m,2),s=Has(m,4),w=Has(m,8);
        using var tile=new Bitmap(16,16,PixelFormat.Format32bppArgb);
        for(int y=0;y<16;y++) for(int x=0;x<16;x++) {
            if(!n&&y<4) continue;
            int left=w?99:x,right=e?99:15-x,bottom=s?99:15-y,top=n?99:y-4;
            if(n&&w&&!Has(m,128)&&y<4) left=x;
            if(n&&e&&!Has(m,16)&&y<4) right=15-x;
            int side=Math.Min(left,right),horizontal=Math.Min(top,bottom);
            if(side<2&&horizontal<2) {
                double cx=1.5-side,cy=1.5-horizontal;
                double distance=Math.Sqrt(cx*cx+cy*cy);
                if(distance>2) continue;
                tile.SetPixel(x,y,color[1]);continue;
            }
            int shade=5;
            int edge=Math.Min(side,Math.Min(top,bottom));
            if((x+y)%5==1 && edge<3) shade=4;
            if(edge==0) shade=1;
            if(!n&&side>0&&top>=0&&top<4) {
                int phase=(x+variant*3)%9;
                if(top==0) shade=phase<2?1:2;
                if(top==1) shade=phase==3||phase==7?3:6;
                if(top==2) shade=phase<3?2:phase<6?4:3;
                if(top==3&&phase==1) shade=4;
            }
            tile.SetPixel(x,y,color[shade]);
        }
        if(!(n&&e&&s&&w)&&variant==1) Stamp(tile,3,8,new[]{".....3..","....3...","...3..3.","..3..3..",".3..3...",".....3.."});
        if(!n) {
            for(int x=1;x<15;x++) {
                int phase=(x+variant)%5;
                tile.SetPixel(x,3,color[phase==0?2:phase==3?4:3]);
                if(phase==1||phase==4) tile.SetPixel(x,2,color[3]);
            }
            Stamp(tile,new[]{2,6,3,5}[variant],0,blades[variant]);
        }
        return Scale(tile,2);
    }
    static Bitmap Wood(string part) {
        bool left=part=="left"||part=="single", right=part=="right"||part=="single";
        using var tile=new Bitmap(16,16);
        for(int y=4;y<=11;y++) for(int x=0;x<16;x++) {
            int c=y==4||y==11?1:y==5?6:y==10?3:5;
            tile.SetPixel(x,y,color[c]);
        }
        Stamp(tile,3,6,new[]{"33333","..333","44..."});
        Stamp(tile,3,9,new[]{"..33333333","33........"});
        if(part=="middle-b") Stamp(tile,9,6,new[]{".22.","2662",".22."});
        else Stamp(tile,10,8,new[]{"33333","..44."});
        if(left) {
            for(int y=3;y<=12;y++) {tile.SetPixel(0,y,color[1]);tile.SetPixel(1,y,color[y==3||y==12?1:6]);}
            tile.SetPixel(3,7,color[2]);
        }
        if(right) {
            for(int y=3;y<=12;y++) {tile.SetPixel(15,y,color[1]);tile.SetPixel(14,y,color[y==3||y==12?1:6]);}
            tile.SetPixel(12,7,color[2]);
        }
        return Scale(tile,2);
    }
    static Bitmap Flower() {
        using var tile=new Bitmap(16,16);
        Stamp(tile,4,1,new[]{".11.11.","1661661","1621261",".12121.",".16161.","..1.1.."});
        Stamp(tile,7,6,new[]{"1...","1...","1.11","1161",".11.",".1.."});
        Stamp(tile,4,8,new[]{"11..","161.",".111"});
        return Scale(tile,2);
    }
    static Bitmap WallBlade(bool right) {
        using var tile=new Bitmap(16,16);
        if(right) Stamp(tile,7,5,new[]{"..2",".26","26.","2.."});
        else Stamp(tile,6,5,new[]{"2..","62.",".62","..2"});
        return Scale(tile,2);
    }
    static Bitmap Pebble(int variant) {
        using var tile=new Bitmap(16,16);
        if(variant==0) {
            Stamp(tile,5,5,new[]{".222.","26562",".222."});
            Stamp(tile,10,9,new[]{".22.","2562",".22."});
        }
        if(variant==1) Stamp(tile,5,6,new[]{"..22...",".26622.","2656621",".22222."});
        if(variant==2) {
            Stamp(tile,6,4,new[]{"..22.",".2661","26621",".221.","..1.."});
            Stamp(tile,3,10,new[]{".3.","353",".3."});
        }
        return Scale(tile,2);
    }
    static Bitmap PencilPatch(int variant) {
        using var tile=new Bitmap(16,16);
        string[][] strokes={
            new[]{".....3.","....3..","...3...","..3..3.",".3..3..","...3..."},
            new[]{"...4.....","..4......","......4..",".....4...","....4...."},
            new[]{"......3.",".....3..","....3.3.","...3.3..","....3...","...3....","..3....."},
            new[]{"....4","...4.","..4.."}
        };
        Stamp(tile,3+variant%2,4+variant,strokes[variant]);
        return Scale(tile,2);
    }
    static Bitmap Junction(int sx,int sy) {
        using var image=new Bitmap(16,16);
        int ox=8+(sx<0?-1:0),oy=8+(sy<0?-1:0),radius=3;
        using var g=Graphics.FromImage(image);
        using var fill=new SolidBrush(color[5]);
        using var ink=new Pen(color[1]);
        for(int t=0;t<radius;t++) {
            g.FillRectangle(fill,ox-sx,oy+sy*t,1,1);
            g.FillRectangle(fill,ox+sx*t,oy-sy,1,1);
        }
        for(int a=0;a<radius;a++) for(int b=0;b<radius;b++) {
            double u=radius-a-.5,v=radius-b-.5;
            if(u*u+v*v>=radius*radius) g.FillRectangle(fill,ox+sx*a,oy+sy*b,1,1);
        }
        var arc=new Point[radius+1];
        for(int a=-1;a<radius;a++) {
            int u=radius-1-a,b=radius-1-(int)Math.Round(Math.Sqrt(radius*radius-u*u));
            arc[a+1]=new Point(ox+sx*a,oy+sy*b);
        }
        g.DrawLines(ink,arc);
        return Scale(image,2);
    }
    static string JunctionName(int sx,int sy)=>$"inside-{(sx<0?"left":"right")}-{(sy<0?"up":"down")}.png";
    static Bitmap Load(string relative)=>new Bitmap(Path.Combine(root,"runtime",relative));
    public static Bitmap Assemble(bool[,] cells) {
        int w=cells.GetLength(0),h=cells.GetLength(1);
        bool Solid(int x,int y)=>x>=0&&y>=0&&x<w&&y<h&&cells[x,y];
        var terrain=new Bitmap(w*32,h*32);
        using var g=Graphics.FromImage(terrain);
        for(int y=0;y<h;y++) for(int x=0;x<w;x++) if(Solid(x,y)) {
            int mask=0;
            for(int i=0;i<8;i++) if(Solid(x+dx[i],y+dy[i])) mask|=1<<i;
            mask=Canonical(mask);
            uint style=unchecked((uint)x*73856093u^(uint)y*19349663u);
            style^=style>>13;
            using var tile=Load("terrain/"+Name(mask,(int)(style%4))+".png");
            g.DrawImageUnscaled(tile,x*32,y*32);
        }
        bool Body(int x,int y)=>x>=0&&y>=0&&x<w*32&&y<h*32&&Solid(x/32,y/32)&&(y%32>=Surface||Solid(x/32,y/32-1));
        for(int x=1;x<w;x++) for(int y=0;y<h;y++) foreach(int offset in new[]{0,Surface}) {
            int px=x*32,py=y*32+offset;
            int count=0,sx=0,sy=0;
            foreach(int i in new[]{-1,1}) foreach(int j in new[]{-1,1})
                if(!Body(px+(i<0?-1:0),py+(j<0?-1:0))) {count++;sx=i;sy=j;}
            if(count!=1) continue;
            using var join=Load("junctions/"+JunctionName(sx,sy));
            g.DrawImageUnscaled(join,px-16,py-16);
        }
        using var flower=Load("decor/daisy.png");
        for(int y=1;y<h;y++) for(int x=1;x<w-1;x++)
        {
            if(Solid(x,y)&&!Solid(x,y-1)&&(x*29+y*17)%17==3)
                g.DrawImageUnscaled(flower,x*32,y*32+Surface-24);
            uint mark=unchecked((uint)x*73856093u^(uint)y*19349663u);
            mark^=mark>>13;
            if(Solid(x,y)&&Solid(x,y-1)&&Solid(x,y+1)&&mark%5==2)
                foreach(int direction in new[]{-1,1}) if(!Solid(x+direction,y)) {
                    using var blade=Load("decor/wall-blade-"+(direction<0?"left":"right")+".png");
                    int pivot=direction<0?16:14;
                    g.DrawImageUnscaled(blade,(x+(direction>0?1:0))*32-pivot,y*32);
                }
            bool interior=Solid(x,y)&&Solid(x,y-1)&&Solid(x,y+1)&&Solid(x-1,y)&&Solid(x+1,y);
            if(interior&&mark%9<2) {
                using var patch=Load("decor/pencil-patch-"+mark%4+".png");
                g.DrawImageUnscaled(patch,x*32+(int)(mark%7)*2-6,y*32+(int)(mark%5)*2-4);
            }
            if(mark%19==2&&interior)
            {
                using var pebble=Load("decor/embedded-pebble-"+mark%3+".png");
                g.DrawImageUnscaled(pebble,x*32+(int)(mark%5)*2-4,y*32+(int)(mark%3)*2-2);
            }
        }
        return terrain;
    }
    static Bitmap Scale(Bitmap source,int factor) {
        var result=new Bitmap(source.Width*factor,source.Height*factor);
        using var g=Graphics.FromImage(result);
        g.InterpolationMode=InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode=PixelOffsetMode.Half;
        g.DrawImage(source,new Rectangle(0,0,result.Width,result.Height));
        return result;
    }
    static void DrawWood(Graphics g,int x,int y,int count) {
        for(int i=0;i<count;i++) {
            string part=count==1?"single":i==0?"left":i==count-1?"right":i%2==0?"middle-b":"middle-a";
            using var tile=Load("wood/"+part+".png");
            g.DrawImageUnscaled(tile,x+i*32,y);
        }
    }
    static void Samples() {
        string[][] shapes={
            new[]{"..........",".......##.",".....####.","...######.",".########.",".########.",".........."},
            new[]{"..........",".##.......",".##.......",".##.......",".########.",".########.",".........."},
            new[]{"..........",".########.",".##.......",".##.......",".##.......",".########.",".........."},
            new[]{"..........","....#.....","....#.....","....#.....","....#.....","....#.....",".........."},
            new[]{"..........","..........","..........","...###....","..........","..........",".........."}
        };
        string[] labels={"계단","ㄴ자","ㄷ자","한 칸 기둥","짧은 흙잔디 선반","나무 · 1 / 3 / 7칸"};
        using var board=new Bitmap(664,816);
        using var paper=Graphics.FromImage(board);
        using var font=new Font("Malgun Gothic",12,FontStyle.Bold);
        paper.Clear(color[5]);
        for(int s=0;s<6;s++) {
            int bx=8+s%2*332,by=8+s/2*272;
            paper.DrawString(labels[s],font,Brushes.DimGray,bx,by);
            if(s==5) {
                DrawWood(paper,bx+64,by+55,1);DrawWood(paper,bx+64,by+110,3);DrawWood(paper,bx+32,by+165,7);
                continue;
            }
            var cells=new bool[10,7];
            for(int y=0;y<7;y++) for(int x=0;x<10;x++) cells[x,y]=shapes[s][y][x]=='#';
            using var sample=Assemble(cells);
            sample.Save(Path.Combine(root,"preview",$"assembly-{s+1}-1x.png"),ImageFormat.Png);
            paper.DrawImageUnscaled(sample,bx,by+30);
        }
        board.Save(Path.Combine(root,"preview/assemblies-1x.png"),ImageFormat.Png);
        using var big=Scale(board,2);
        big.Save(Path.Combine(root,"preview/assemblies-2x.png"),ImageFormat.Png);
    }
    public static void Build(string directory) {
        root=directory;
        int slot=0;
        var entries=new List<object>();
        using var atlas=new Bitmap(512,384);
        for(int m=0;m<256;m++) if(Canonical(m)==m) for(int v=0;v<4;v++) using(var tile=Terrain(m,v)) {
            string name=Name(m,v)+".png";
            tile.Save(Path.Combine(root,"runtime/terrain",name),ImageFormat.Png);
            for(int y=0;y<32;y++) for(int x=0;x<32;x++) atlas.SetPixel(slot%16*32+x,slot/16*32+y,tile.GetPixel(x,y));
            entries.Add(new{mask=m,variant=v,file="terrain/"+name,atlasXY=new[]{slot%16*32,slot/16*32}});slot++;
        }
        atlas.Save(Path.Combine(root,"runtime/terrain-atlas.png"),ImageFormat.Png);
        foreach(string part in new[]{"left","middle-a","middle-b","right","single"}) using(var wood=Wood(part))
            wood.Save(Path.Combine(root,"runtime/wood",part+".png"),ImageFormat.Png);
        foreach(int sx in new[]{-1,1}) foreach(int sy in new[]{-1,1}) using(var join=Junction(sx,sy))
            join.Save(Path.Combine(root,"runtime/junctions",JunctionName(sx,sy)),ImageFormat.Png);
        using(var flower=Flower()) flower.Save(Path.Combine(root,"runtime/decor/daisy.png"),ImageFormat.Png);
        foreach(bool right in new[]{false,true}) using(var blade=WallBlade(right))
            blade.Save(Path.Combine(root,"runtime/decor","wall-blade-"+(right?"right":"left")+".png"),ImageFormat.Png);
        for(int variant=0;variant<3;variant++) using(var pebble=Pebble(variant))
            pebble.Save(Path.Combine(root,"runtime/decor","embedded-pebble-"+variant+".png"),ImageFormat.Png);
        for(int variant=0;variant<4;variant++) using(var patch=PencilPatch(variant))
            patch.Save(Path.Combine(root,"runtime/decor","pencil-patch-"+variant+".png"),ImageFormat.Png);
        File.WriteAllText(Path.Combine(root,"runtime/tileset.json"),JsonSerializer.Serialize(new{
            pixelsPerUnit=32,tileSize=32,surfaceInsetPx=Surface,atlas="terrain-atlas.png",
            maskBits=new{N=1,E=2,S=4,W=8,NE=16,SE=32,SW=64,NW=128},
            canonicalMaskForRawMask=Enumerable.Range(0,256).Select(Canonical),tiles=entries,
            junctionPivot=new[]{16,16},junctionRadiusPx=6,woodContactY=8,artPixelSize=2,flowerRoot=new[]{16,24}
        },new JsonSerializerOptions{WriteIndented=true}));
        Samples();
    }
}
