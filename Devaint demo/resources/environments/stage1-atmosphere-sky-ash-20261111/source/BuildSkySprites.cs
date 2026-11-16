using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

public static class BuildSkySprites {
 public static void Build(string root) {
  foreach(string kind in new[]{"clouds","birds"}) {
   using(var input=new Bitmap(Path.Combine(root,"source",kind+"-atlas-generated.png"))) {
    int w=kind=="clouds"?192:32,h=kind=="clouds"?64:16;
    var palette=kind=="clouds"?new[]{ColorTranslator.FromHtml("#DDD8CD"),ColorTranslator.FromHtml("#E6E2D8"),ColorTranslator.FromHtml("#F0EBE2")}:new[]{ColorTranslator.FromHtml("#B7AFA0")};
    for(int cell=0;cell<6;cell++) using(var image=new Bitmap(w,h,PixelFormat.Format32bppArgb)) {
     for(int y=0;y<h;y++)for(int x=0;x<w;x++) {
      double light=0;int visible=0,taps=kind=="birds"?5:3;
      for(int ty=0;ty<taps;ty++)for(int tx=0;tx<taps;tx++) {
       int px=Math.Min(input.Width-1,(int)(((cell%3)+(x+(tx+.5)/taps)/w)*input.Width/3));
       int py=Math.Min(input.Height-1,(int)(((cell/3)+(y+(ty+.5)/taps)/h)*input.Height/2));
       var c=input.GetPixel(px,py);if(c.A<140)continue;
       light+=(c.R*.299+c.G*.587+c.B*.114)/255;visible++;
      }
      if(visible<(kind=="birds"?2:5))continue;
      double t=Math.Max(0,Math.Min(1,(light/visible-.70)/.25));
      image.SetPixel(x,y,palette[(int)Math.Round(t*(palette.Length-1))]);
     }
     image.Save(Path.Combine(root,"runtime",kind,kind+"-"+cell+".png"),ImageFormat.Png);
    }
   }
  }
 }
}
