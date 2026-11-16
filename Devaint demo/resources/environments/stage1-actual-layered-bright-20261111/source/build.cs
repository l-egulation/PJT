using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Text.Json;
namespace RoomLayers {
public static class Build {
static string root,kit;
static Dictionary<string,Bitmap> sprites=new Dictionary<string,Bitmap>();
static int[] dx={0,1,1,1,0,-1,-1,-1},dy={-1,-1,0,1,1,1,0,-1};
static int Canon(int m){for(int i=1;i<8;i+=2)if((m&(1<<((i+7)%8)))==0||(m&(1<<((i+1)%8)))==0)m&=~(1<<i);return m;}
static Bitmap Sprite(string id){if(!sprites.ContainsKey(id))sprites[id]=new Bitmap(Path.Combine(kit,"runtime",id.StartsWith("stone-")?"tiles":"platforms",id+".png"));return sprites[id];}
static void Save(Bitmap b,string f){string p=Path.Combine(root,f);Directory.CreateDirectory(Path.GetDirectoryName(p));b.Save(p,ImageFormat.Png);}
static string Deck(int i,int n,bool l,bool r){if(n==1)return "single-"+(l?"wall":"free")+"-"+(r?"wall":"free");if(i==0)return "left-"+(l?"wall":"free");if(i==n-1)return "right-"+(r?"wall":"free");return new[]{"middle-a","middle-b","middle-c"}[(i-1)%3];}
static Bitmap Fit(Bitmap source,int w,int h,bool bottom){
 var b=new Bitmap(w,h,PixelFormat.Format32bppArgb);
 double s=Math.Max(w/(double)source.Width,h/(double)source.Height);
 int bw=(int)Math.Ceiling(source.Width*s),bh=(int)Math.Ceiling(source.Height*s);
 using(var g=Graphics.FromImage(b)){g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(source,new Rectangle((w-bw)/2,bottom?h-bh:(h-bh)/2,bw,bh));}
 return b;
}
static void Label(Graphics g,string t,int x,int y,int n){using(var f=new Font("Malgun Gothic",n,FontStyle.Bold,GraphicsUnit.Pixel))using(var b=new SolidBrush(Color.FromArgb(63,54,46)))g.DrawString(t,f,b,x,y);}
public static void Run(string r){
 root=r;kit=Path.GetFullPath(Path.Combine(root,"../stage1-modular-tiles-20261111"));
 using(var farSource=new Bitmap(Path.Combine(root,"source/far-scenery.png")))
 using(var wideSource=new Bitmap(Path.Combine(root,"source/near-wide.png")))
 using(var tallSource=new Bitmap(Path.Combine(root,"source/near-tall.png")))
 using(var hero=new Bitmap(Path.Combine(root,"source/hero.png"))){
 var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"source/rooms.json")));
 var infos=new List<object>();var audits=new List<object>();var scenes=new List<Bitmap>();var ids=new List<string>();var names=new[]{"세 단 언덕 · 계단형","낮은 터널 · 막힌 천장","계단 탑 · 좌우로 상승","삼자 누각 · 통과형 발판"};
 int roomIndex=0,total=0;
 foreach(var room in doc.RootElement.GetProperty("rooms").EnumerateArray()){
  string id=room.GetProperty("id").GetString();
  int cols=room.GetProperty("width").GetInt32(),rows=room.GetProperty("height").GetInt32(),w=cols*32,h=rows*32;
  var arr=new List<int>();foreach(var p in room.GetProperty("grid").EnumerateArray())arr.Add(p.GetInt32());
  // Original grid is Y-up. All exported PNG coordinates are Y-down.
  bool At(int x,int y)=>x<0||y<0||x>=cols||y>=rows?true:arr[(rows-1-y)*cols+x]!=0;
  var terrain=new Bitmap(w,h,PixelFormat.Format32bppArgb);var records=new List<object>();
  using(var g=Graphics.FromImage(terrain)){g.CompositingMode=CompositingMode.SourceCopy;
   for(int y=0;y<rows;y++)for(int x=0;x<cols;x++)if(At(x,y)){
    int m=0;for(int k=0;k<8;k++)if(At(x+dx[k],y+dy[k]))m|=1<<k;
    string sid="stone-"+Canon(m).ToString("X2")+"-v"+((x*13+y*7)&3);
    g.DrawImageUnscaled(Sprite(sid),x*32,y*32);records.Add(new{x=x*32,y=y*32,id=sid});total++;
   }
   foreach(var p in room.GetProperty("plats").EnumerateArray()){
    int px=p.GetProperty("x").GetInt32(),py=rows-p.GetProperty("y").GetInt32(),n=p.GetProperty("w").GetInt32();
    bool l=At(px-1,py),rr=At(px+n,py);
    // Original y=0 rung is on the lower seam; canvas cropping preserves its original location.
    for(int k=0;k<n;k++){if(py>=0&&py<rows&&At(px+k,py))continue;string sid=Deck(k,n,l,rr);g.DrawImageUnscaled(Sprite(sid),(px+k)*32,py*32);records.Add(new{x=(px+k)*32,y=py*32,id=sid});}
   }
  }
  Save(terrain,"layers/"+id+"/03-terrain.png");
  using(var far=Fit(farSource,w,h,true))
 using(var tallTrim=tallSource.Clone(new Rectangle(0,30,tallSource.Width,tallSource.Height-30),PixelFormat.Format32bppArgb))
 using(var near=Fit(cols>32?wideSource:tallTrim,w,h,false))
  using(var actors=new Bitmap(w,h,PixelFormat.Format32bppArgb)){
   // Find an actual solid landing edge near lower-left. Character is a separate scale reference layer.
   int hx=4,hy=rows-2;
   for(int yy=rows-2;yy>1;yy--)if(At(4,yy)&&!At(4,yy-1)){hy=yy;break;}
   int minX=hero.Width,minY=hero.Height,maxX=0,maxY=0;
   for(int yy=0;yy<hero.Height;yy++)for(int xx=0;xx<hero.Width;xx++)if(hero.GetPixel(xx,yy).A>40){minX=Math.Min(minX,xx);minY=Math.Min(minY,yy);maxX=Math.Max(maxX,xx);maxY=Math.Max(maxY,yy);}
   var heroRect=new Rectangle(minX,minY,maxX-minX+1,maxY-minY+1);int heroW=(int)Math.Round(64.0*heroRect.Width/heroRect.Height);
   using(var g=Graphics.FromImage(actors)){g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;g.DrawImage(hero,new Rectangle(hx*32,hy*32-64,heroW,64),heroRect,GraphicsUnit.Pixel);}
   Save(far,"layers/"+id+"/01-far.png");Save(near,"layers/"+id+"/02-architecture.png");Save(actors,"layers/"+id+"/04-character.png");
   var scene=new Bitmap(w,h,PixelFormat.Format32bppArgb);
   using(var g=Graphics.FromImage(scene)){g.Clear(Color.FromArgb(230,222,208));g.DrawImageUnscaled(far,0,0);g.DrawImageUnscaled(near,0,0);g.DrawImageUnscaled(terrain,0,0);g.DrawImageUnscaled(actors,0,0);}
   Save(scene,"preview/"+id+"-bright.png");scenes.Add(scene);ids.Add(id);
   // 1:1 detail crops keep native 32px terrain visible.
   int cx=id=="S1-04"?480:0,cy=id=="S1-04"?192:id=="S1-07"?352:Math.Max(0,h-576);
   int cw=Math.Min(1024,w),ch=Math.Min(576,h);cx=Math.Min(cx,w-cw);cy=Math.Min(cy,h-ch);
   using(var crop=scene.Clone(new Rectangle(cx,cy,cw,ch),PixelFormat.Format32bppArgb))Save(crop,"preview/"+id+"-detail-1x.png");
   infos.Add(new{id,name=names[roomIndex],width=w,height=h,worldUnits=new[]{cols,rows},far="layers/"+id+"/01-far.png",near="layers/"+id+"/02-architecture.png",terrain="layers/"+id+"/03-terrain.png",actors="layers/"+id+"/04-character.png",parallaxFar=.2,architectureWorldLocked=true});
  }
  // Check exported terrain pixels against source sprites where no other sprite overlays them.
  int checkedPixels=0;
  foreach(var item in records){var c=JsonDocument.Parse(JsonSerializer.Serialize(item)).RootElement;int x=c.GetProperty("x").GetInt32(),y=c.GetProperty("y").GetInt32();var s=Sprite(c.GetProperty("id").GetString());for(int yy=0;yy<32;yy++)for(int xx=0;xx<32;xx++){if(x+xx<0||y+yy<0||x+xx>=w||y+yy>=h)continue;Color a=s.GetPixel(xx,yy);if(a.A==0)continue;if(a.ToArgb()!=terrain.GetPixel(x+xx,y+yy).ToArgb())throw new Exception("Tile pixel modified: "+id);checkedPixels++;}}
  audits.Add(new{id,solidCells=arr.FindAll(v=>v!=0).Count,platformDefinitions=room.GetProperty("plats").GetArrayLength(),checkedPixels,placements=records});terrain.Dispose();roomIndex++;
 }
 using(var overview=new Bitmap(1600,1230,PixelFormat.Format32bppArgb)){
  using(var g=Graphics.FromImage(overview)){g.Clear(Color.FromArgb(232,225,215));g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
   Label(g,"1스테이지 실제 방 원형 + 동일 타일 + 분리 배경",28,18,30);Label(g,"전체 구조 축소 보기 · 개별 원본은 1u=32px · 출입구 전부 열린 상태",28,62,18);
   for(int i=0;i<4;i++){int x=24+(i%2)*790,y=i<2?105:580;Label(g,ids[i]+"  "+names[i],x,y,23);g.DrawImage(scenes[i],new Rectangle(x,y+42,scenes[i].Width/2,scenes[i].Height/2));}
  }Save(overview,"preview/four-rooms-overview.png");
 }
 using(var board=new Bitmap(1536,980,PixelFormat.Format32bppArgb)){
  using(var g=Graphics.FromImage(board)){g.Clear(Color.FromArgb(232,225,215));g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;Label(g,"분리 레이어 · S1-04",24,12,28);
   string[] f={"01-far.png","02-architecture.png","03-terrain.png","04-character.png"},label={"원경 · 느린 패럴랙스","배경 건축 · 창/아치 안쪽 투명","플레이 지형 · 기존 PNG","캐릭터 · 별도 레이어"};
   for(int i=0;i<4;i++){int x=24+(i%2)*768,y=65+(i/2)*450;Label(g,label[i],x,y,22);
    for(int yy=0;yy<384;yy+=16)for(int xx=0;xx<736;xx+=16)using(var brush=new SolidBrush(((xx/16+yy/16)%2)==0?Color.FromArgb(210,211,209):Color.FromArgb(237,237,233)))g.FillRectangle(brush,x+xx,y+38+yy,16,16);
    using(var a=new Bitmap(Path.Combine(root,"layers/S1-04/"+f[i])))g.DrawImage(a,new Rectangle(x,y+38,736,368));
   }
  }Save(board,"preview/layer-breakdown.png");
 }
 File.WriteAllText(Path.Combine(root,"preview/rooms.js"),"window.ROOMS="+JsonSerializer.Serialize(infos)+";");
 File.WriteAllText(Path.Combine(root,"source/tile-placements.json"),JsonSerializer.Serialize(audits,new JsonSerializerOptions{WriteIndented=true}));
 foreach(var s in scenes)s.Dispose();foreach(var s in sprites.Values)s.Dispose();
 Console.WriteLine("4 exact room grids composed; "+total+" solid cells. Exported tile pixel identity verified.");
 }
}
}}
