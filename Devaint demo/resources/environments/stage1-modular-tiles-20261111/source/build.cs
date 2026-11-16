using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Text.Json;
namespace ModularStone32 {
public static class Build {
const int S=32, H=16;
static string root;
static Bitmap master, approved;
static Bitmap[,] full=new Bitmap[3,4];
static Bitmap[] half=new Bitmap[4];
static Bitmap[] wood=new Bitmap[4];
static Dictionary<string,Bitmap> saved=new Dictionary<string,Bitmap>();
static List<object> selections=new List<object>();
static Color Core=Color.FromArgb(57,47,49), Mortar=Color.FromArgb(71,51,37);
static int[] dx={0,1,1,1,0,-1,-1,-1},dy={-1,-1,0,1,1,1,0,-1};
static string[] platformNames={"single-free-free","single-wall-free","single-free-wall","single-wall-wall","left-free","left-wall","middle-a","middle-b","middle-c","right-free","right-wall"};
public static int Canonical(int m){for(int i=1;i<8;i+=2)if((m&(1<<((i+7)%8)))==0||(m&(1<<((i+1)%8)))==0)m&=~(1<<i);return m;}
static bool B(int m,int i){return (m&(1<<i))!=0;}
static int Clamp(int v){return Math.Max(0,Math.Min(255,v));}
static Color Mix(Color a,Color b,double t){return Color.FromArgb(255,Clamp((int)Math.Round(a.R*(1-t)+b.R*t)),Clamp((int)Math.Round(a.G*(1-t)+b.G*t)),Clamp((int)Math.Round(a.B*(1-t)+b.B*t)));}
static Color Change(Color c,double mul,int add){return Color.FromArgb(255,Clamp((int)(c.R*mul+add)),Clamp((int)(c.G*mul+add)),Clamp((int)(c.B*mul+add)));}
static void Save(Bitmap b,string rel){string p=Path.Combine(root,rel);Directory.CreateDirectory(Path.GetDirectoryName(p));b.Save(p,ImageFormat.Png);}
static Bitmap Crop(Rectangle r,int w,int h){
 var b=new Bitmap(w,h,PixelFormat.Format32bppArgb);
 using(var g=Graphics.FromImage(b)){g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;using(var a=new ImageAttributes()){a.SetWrapMode(WrapMode.TileFlipXY);g.DrawImage(master,new Rectangle(0,0,w,h),r.X,r.Y,r.Width,r.Height,GraphicsUnit.Pixel,a);}}
 // The original has near-opaque object alpha (253), but occupied masonry is fully opaque.
 for(int y=0;y<h;y++)for(int x=0;x<w;x++){Color c=b.GetPixel(x,y);b.SetPixel(x,y,Mix(Core,Color.FromArgb(255,c.R,c.G,c.B),Math.Min(1,c.A/230.0)));}
 return b;
}
static void Materials(){
 for(int v=0;v<4;v++){
  full[0,v]=Crop(new Rectangle(67+384*v,118,250,102),32,16);
  half[v]=Crop(new Rectangle(130+384*v,354,124,125),16,16);
  full[2,v]=Crop(new Rectangle(66+384*v,598,251,103),32,16);
  full[1,v]=new Bitmap(32,16,PixelFormat.Format32bppArgb);
  for(int y=0;y<16;y++)for(int x=0;x<32;x++)full[1,v].SetPixel(x,y,Change(full[2,v].GetPixel(x,y),1.07,18));
  wood[v]=Crop(new Rectangle(49+384*v,v<2?840:839,286,v<2?87:88),32,8);
  Save(full[0,v],"source/normalized/top-"+v+".png");Save(full[1,v],"source/normalized/body-"+v+".png");Save(full[2,v],"source/normalized/under-"+v+".png");Save(half[v],"source/normalized/half-"+v+".png");Save(wood[v],"source/normalized/wood-"+v+".png");
 }
}
static Color Stone(int type,int variant,int u,int v,int w){
 // Border ownership is one pixel at the START of the brick/course.
 // Its right/bottom texel is a face/bevel texel; it is not another mortar line.
 int uu=u==w-1?w-2:u, vv=v==15?14:v;
 Color c=w==32?full[type,variant].GetPixel(uu,vv):half[variant].GetPixel(uu,vv);
 if(w==16){c=Change(c,type==2?.75:.96,0);if(v<2)c=Mix(c,type==2?Color.FromArgb(146,111,85):Color.FromArgb(190,156,116),.55);}
 if(u==0)c=Mortar;
 if(v==0)c=type==0?Color.FromArgb(250,236,205):Mortar;
 return c;
}
static bool[] Rims(int m,int row){
 bool n=B(m,0),e=B(m,2),s=B(m,4),w=B(m,6);
 bool left=!w || (row==0?!n:!s);
 bool right=!e || (row==0?!n:!s);
 if(row==0){left|=n&&w&&!B(m,7);right|=n&&e&&!B(m,1);}
 else{left|=s&&w&&!B(m,5);right|=s&&e&&!B(m,3);}
 return new[]{left,right};
}
static Color CorePixel(int x,int y,int v){
 Color sample=approved.GetPixel(350+v*90+x*2,470+y*2);
 // Quiet painted core, low-contrast variation. Fixed outside two pixels avoids square seams.
 double fade=Math.Min(1,Math.Min(Math.Min(x,31-x),Math.Min(y,31-y))/5.0);
 Color c=Mix(Core,sample,.23*fade);
 return c;
}
static Bitmap Tile(int mask,int variant){
 var b=new Bitmap(32,32,PixelFormat.Format32bppArgb);
 for(int y=0;y<32;y++)for(int x=0;x<32;x++)b.SetPixel(x,y,CorePixel(x,y,variant));
 for(int row=0;row<2;row++){
  bool[] r=Rims(mask,row);
  int type=row==0&&!B(mask,0)?0:row==1&&!B(mask,4)?2:1;
  for(int v=0;v<16;v++)for(int x=0;x<32;x++){
   int q=x/16;if(!r[q])continue;
   int width=r[0]&&r[1]?32:16,u=width==32?x:x%16;
   int localType=type;
   if(width==16&&row==1&&B(mask,4)&&((q==0&&B(mask,6)&&!B(mask,5))||(q==1&&B(mask,2)&&!B(mask,3))))localType=2;
   Color c=Stone(localType,(variant+row)%4,u,v,width);
   int y=row*16+v;
   if(x==0&&!B(mask,6)||x==31&&!B(mask,2)||y==31&&!B(mask,4))c=Color.FromArgb(55,39,29);
   // A single fitted boundary, not a second overlaid corner sprite.
   if(y==0&&!B(mask,0)&&x>0&&x<31)c=Color.FromArgb(250,236,205);
   b.SetPixel(x,y,c);
  }
 }
 return b;
}
static Bitmap Platform(string id){
 var b=new Bitmap(32,32,PixelFormat.Format32bppArgb);
 bool l=id.StartsWith("left-")||id.StartsWith("single-"),r=id.StartsWith("right-")||id.StartsWith("single-");
 bool wallL=id=="left-wall"||id=="single-wall-free"||id=="single-wall-wall";
 bool wallR=id=="right-wall"||id=="single-free-wall"||id=="single-wall-wall";
 int variant=id=="middle-b"?1:0;
 for(int y=0;y<8;y++)for(int x=0;x<32;x++){
  // Source end bevels are only present at a true end. Middle planks share the same edge colors.
  int xx=x<2?2:x>29?29:x;
  Color c=wood[variant].GetPixel(xx,y);
  if(x<2||x>29)c=wood[0].GetPixel(16,y);
  if(y==0)c=Color.FromArgb(246,207,142);
  b.SetPixel(x,y,c);
 }
 for(int y=8;y<12;y++)for(int x=13;x<=18;x++)b.SetPixel(x,y,Color.FromArgb(83,51,29));
 if(l)for(int y=0;y<12;y++)for(int x=0;x<4;x++){Color c=wood[2].GetPixel(x,Math.Min(7,y*8/12));if(wallL)c=Change(c,.82,2);b.SetPixel(x,y,c);}
 if(r)for(int y=0;y<12;y++)for(int x=28;x<32;x++){Color c=wood[3].GetPixel(x,Math.Min(7,y*8/12));if(wallR)c=Change(c,.82,2);b.SetPixel(x,y,c);}
 return b;
}
static bool At(string[] rows,int x,int y){return y>=0&&y<rows.Length&&x>=0&&x<rows[y].Length&&rows[y][x]=='#';}
static int Mask(string[] rows,int x,int y){int m=0;for(int i=0;i<8;i++)if(At(rows,x+dx[i],y+dy[i]))m|=1<<i;return Canonical(m);}
static string ID(int mask,int v){return "stone-"+mask.ToString("X2")+"-v"+v;}
static string DeckID(int i,int length,bool lw,bool rw){
 if(length==1)return "single-"+(lw?"wall":"free")+"-"+(rw?"wall":"free");
 if(i==0)return "left-"+(lw?"wall":"free");
 if(i==length-1)return "right-"+(rw?"wall":"free");
 return new[]{"middle-a","middle-b","middle-c"}[(i-1)%3];
}
static void Text(Graphics g,string s,int x,int y,int sz,Color? c=null){using(var f=new Font("Malgun Gothic",sz,FontStyle.Bold,GraphicsUnit.Pixel))using(var brush=new SolidBrush(c??Color.FromArgb(69,57,46)))g.DrawString(s,f,brush,x,y);}
static void Blit(Graphics g,Bitmap b,int x,int y,int scale){g.DrawImage(b,new Rectangle(x,y,32*scale,32*scale),0,0,32,32,GraphicsUnit.Pixel);}
static Bitmap Assemble(string[] rows,string name){
 int width=0;foreach(var s in rows)width=Math.Max(width,s.Length);
 var b=new Bitmap(width*32,rows.Length*32,PixelFormat.Format32bppArgb);
 var records=new List<object>();
 using(var g=Graphics.FromImage(b)){
  g.CompositingMode=CompositingMode.SourceCopy;
  for(int y=0;y<rows.Length;y++)for(int x=0;x<rows[y].Length;x++){
   if(rows[y][x]=='#'){
    string id=ID(Mask(rows,x,y),(x*13+y*7)&3);g.DrawImageUnscaled(saved[id],x*32,y*32);records.Add(new{x,y,id});
   }else if(rows[y][x]=='='){
    int begin=x;while(x+1<rows[y].Length&&rows[y][x+1]=='=')x++;
    int count=x-begin+1;for(int i=0;i<count;i++){
     string id=DeckID(i,count,At(rows,begin-1,y),At(rows,x+1,y));g.DrawImageUnscaled(saved[id],(begin+i)*32,y*32);records.Add(new{x=begin+i,y,id});
    }
   }
  }
 }
 selections.Add(new{name,rows,cells=records});
 Save(b,"preview/native/"+name+".png");
 // Strong provenance check: the preview must contain the exact saved sprite pixels.
 foreach(var item in records){var d=JsonDocument.Parse(JsonSerializer.Serialize(item)).RootElement;int x=d.GetProperty("x").GetInt32(),y=d.GetProperty("y").GetInt32();var t=saved[d.GetProperty("id").GetString()];for(int j=0;j<32;j++)for(int i=0;i<32;i++)if(b.GetPixel(x*32+i,y*32+j).ToArgb()!=t.GetPixel(i,j).ToArgb())throw new Exception("Assembly differs from PNG "+name);}
 return b;
}
static string[][] primary={
 new[]{"##......","##......","##......","##......","########","########"},
 new[]{"########","########","##......","##......","########","########"},
 new[]{"..#.....","..#.....","..#.....","..#.....","..#.....","..#....."},
 new[]{"#.......","#######.","#.......","#.......","########","########"},
 new[]{"........",".#......","........",".#####..","........",".#######"},
 new[]{"........",".=......","........",".=====..","........",".======="}
};
static string[] primaryIDs={"l-corner","c-corner","one-cell-pillar","wall-shelves","solid-islands","oneway-islands"};
static string[] labels={"01  ㄴ자 · 윗면 / 옆면 / 아래면","02  ㄷ자 · 안쪽 모서리 두 방향","03  한 칸 기둥 · 16px 두 단","04  벽 부착 선반 · 1칸 / 2칸 두께","05  막힌 독립 석재 · 1 / 5 / 7칸","06  통과형 목재 · 1 / 5 / 7칸"};
static void MainBoard(bool grid){
 var b=new Bitmap(1328,1550,PixelFormat.Format32bppArgb);
 using(var g=Graphics.FromImage(b)){
  g.Clear(Color.FromArgb(228,220,208));g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
  Text(g,"같은 32px PNG로 조립한 실제 샘플",38,22,31);
  Text(g,"원본 32×32px · 아래는 정수 2배 확대 · 돌 단 높이 16px",38,70,19);
  for(int k=0;k<6;k++){
   int ox=38+(k%2)*650,oy=125+(k/2)*465;
   Text(g,labels[k],ox,oy,23);
   using(var a=grid?new Bitmap(Path.Combine(root,"preview/native/"+primaryIDs[k]+".png")):Assemble(primary[k],primaryIDs[k])){
    g.DrawImage(a,new Rectangle(ox,oy+51,a.Width*2,a.Height*2),0,0,a.Width,a.Height,GraphicsUnit.Pixel);
    if(grid)using(var pen=new Pen(Color.FromArgb(120,50,130,170),1)){
     for(int x=0;x<=a.Width;x+=32)g.DrawLine(pen,ox+x*2,oy+51,ox+x*2,oy+51+a.Height*2);
     for(int y=0;y<=a.Height;y+=32)g.DrawLine(pen,ox,oy+51+y*2,ox+a.Width*2,oy+51+y*2);
    }
   }
  }
 }
 Save(b,grid?"preview/assembled-grid-2x.png":"preview/assembled-2x.png");b.Dispose();
}
static void CaseBoard(){
 var j=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"source/cases.json")));
 var b=new Bitmap(1440,1200,PixelFormat.Format32bppArgb);
 using(var g=Graphics.FromImage(b)){
  g.Clear(Color.FromArgb(228,220,208));g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
  Text(g,"같은 타일의 연결 범위 · 24개 구조",30,20,28);
  Text(g,"원본 1배 표시 / 재질·단 높이·끝처리 동일 / 빈 공간은 투명",30,62,17);
  int k=0;foreach(var c in j.RootElement.EnumerateArray()){
   var rows=new List<string>();foreach(var v in c.GetProperty("rows").EnumerateArray())rows.Add(v.GetString());
   int x=24+(k%4)*355,y=108+(k/4)*177;
   Text(g,c.GetProperty("label").GetString(),x,y,17);
   using(var a=Assemble(rows.ToArray(),"case-"+k.ToString("D2"))){g.DrawImageUnscaled(a,x,y+35);}
   k++;
  }
 }
 Save(b,"preview/all-cases-1x.png");b.Dispose();
}
static void TileSheet(){
 var b=new Bitmap(1152,960,PixelFormat.Format32bppArgb);
 using(var g=Graphics.FromImage(b)){
  g.Clear(Color.FromArgb(228,220,208));g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
  Text(g,"47개 연결형 + 목재 끝처리 / 32px 원본의 2배 표시",24,20,26);
  int i=0;foreach(var p in saved)if(p.Key.EndsWith("-v0")){
   int x=24+(i%12)*94,y=86+(i/12)*130;
   Blit(g,p.Value,x,y,2);Text(g,p.Key.Substring(6,2),x,y+68,15);i++;
  }
  i=0;foreach(string n in platformNames){int x=24+(i%6)*188,y=630+(i/6)*135;Blit(g,saved[n],x,y,2);Text(g,n.Replace("single-","1:"),x,y+70,12);i++;}
 }
 Save(b,"preview/tile-sheet-2x.png");b.Dispose();
}
static void Atlas(){
 const int stride=36,cols=16;int rows=(saved.Count+cols-1)/cols;
 var b=new Bitmap(cols*stride,rows*stride,PixelFormat.Format32bppArgb);var rects=new List<object>();
 int i=0;foreach(var t in saved){
  int ox=(i%cols)*stride+2,oy=(i/cols)*stride+2;
  for(int y=-2;y<34;y++)for(int x=-2;x<34;x++)b.SetPixel(ox+x,oy+y,t.Value.GetPixel(Math.Max(0,Math.Min(31,x)),Math.Max(0,Math.Min(31,y))));
  rects.Add(new{id=t.Key,x=ox,y=oy,width=32,height=32});i++;
 }
 Save(b,"runtime/atlas-32.png");b.Dispose();
 var meta=new{schema="deviant-modular-stone32-v1",pixelsPerUnit=32,cellSize=new[]{32,32},brickSize=new[]{32,16},halfBrickSize=new[]{16,16},mortar=1,oneWay=new{visibleHeight=12,collisionHeight=4,contactRow=0},pivot=new[]{.5,.5},atlas=new{file="atlas-32.png",padding=2,extrusion=2,stride=36,rects},canonicalLookup=MakeLookup(),platformNames,sourceNormalization="Generated material crops normalized once to 32x16, 16x16 and 32x8. No runtime scaling.",integration="not applied"};
 File.WriteAllText(Path.Combine(root,"runtime/tileset.json"),JsonSerializer.Serialize(meta,new JsonSerializerOptions{WriteIndented=true}));
}
static void JoinBoard(){
 var b=new Bitmap(1060,810,PixelFormat.Format32bppArgb);
 var crops=new[]{new Rectangle(32,96,64,64),new Rectangle(32,32,64,64),new Rectangle(64,64,32,64),new Rectangle(0,16,64,64),new Rectangle(32,96,64,32),new Rectangle(32,96,64,32)};
 string[] titles={"ㄴ자 안쪽 / 같은 16px 단","ㄷ자 아래면에서 벽으로","한 칸 기둥의 줄눈","벽과 얇은 선반의 T 접합","독립 석재 끝과 두 단","목재 끝캡 / 12px 높이"};
 using(var g=Graphics.FromImage(b)){
  g.Clear(Color.FromArgb(228,220,208));g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
  Text(g,"접합부 실제 픽셀 · 정수 4배 확대",28,20,29);
  Text(g,"확대 그림도 완성된 조립 PNG에서 추출 / 추가 리터치 없음",28,63,18);
  for(int k=0;k<6;k++){
   int ox=28+(k%3)*348,oy=113+(k/3)*350;Text(g,titles[k],ox,oy,18);
   using(var a=new Bitmap(Path.Combine(root,"preview/native/"+primaryIDs[k]+".png"))){var r=crops[k];g.DrawImage(a,new Rectangle(ox,oy+42,r.Width*4,r.Height*4),r.X,r.Y,r.Width,r.Height,GraphicsUnit.Pixel);}
  }
 }
 Save(b,"preview/junctions-4x.png");b.Dispose();
}
static int[] MakeLookup(){var a=new int[256];for(int i=0;i<256;i++)a[i]=Canonical(i);return a;}
static void Verify(){
 int solid=0,deck=0;
 foreach(var p in saved){
  if(p.Value.Width!=32||p.Value.Height!=32)throw new Exception("Dimensions");
  bool stone=p.Key.StartsWith("stone");
  for(int y=0;y<32;y++)for(int x=0;x<32;x++){
   int a=p.Value.GetPixel(x,y).A;
   if(stone&&a!=255)throw new Exception("Solid alpha hole");
   if(!stone&&y>=12&&a!=0)throw new Exception("Platform overflow");
  }
  if(stone)solid++;else deck++;
 }
 for(int length=1;length<=64;length++)for(int sides=0;sides<4;sides++){
  int width=0;for(int i=0;i<length;i++)width+=saved[DeckID(i,length,(sides&1)!=0,(sides&2)!=0)].Width;
  if(width!=length*32)throw new Exception("Deck width");
 }
 Console.WriteLine("Native PNGs: "+solid+" solid + "+deck+" one-way; all 32x32; solid alpha opaque; deck alpha clear below y12; length 1..64 x 4 end states OK.");
}
public static void Run(string folder){
 root=folder;master=new Bitmap(Path.Combine(root,"source/material-master.png"));approved=new Bitmap(Path.Combine(root,"source/approved-room.png"));Materials();
 var masks=new SortedSet<int>();for(int i=0;i<256;i++)masks.Add(Canonical(i));if(masks.Count!=47)throw new Exception("Topology");
 foreach(int m in masks)for(int v=0;v<4;v++){string id=ID(m,v),rel="runtime/tiles/"+id+".png";using(var b=Tile(m,v))Save(b,rel);saved[id]=new Bitmap(Path.Combine(root,rel));}
 foreach(string id in platformNames){string rel="runtime/platforms/"+id+".png";using(var b=Platform(id))Save(b,rel);saved[id]=new Bitmap(Path.Combine(root,rel));}
 Verify();MainBoard(false);MainBoard(true);CaseBoard();TileSheet();Atlas();JoinBoard();
 File.WriteAllText(Path.Combine(root,"preview/assembly-selection.json"),JsonSerializer.Serialize(selections,new JsonSerializerOptions{WriteIndented=true}));
 foreach(var t in saved)t.Value.Dispose();master.Dispose();approved.Dispose();
 Console.WriteLine("Preview pixels verified against reloaded exported PNGs. No repainting after assembly.");
}
}
}
