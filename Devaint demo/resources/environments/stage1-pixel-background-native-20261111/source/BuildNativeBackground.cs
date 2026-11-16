using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public static class BuildNativeBackground {
    static double Clamp(double v) => Math.Max(0,Math.Min(1,v));
    static double[] RGB(string hex) {
        return new[]{Convert.ToInt32(hex.Substring(1,2),16)/255.0,Convert.ToInt32(hex.Substring(3,2),16)/255.0,Convert.ToInt32(hex.Substring(5,2),16)/255.0};
    }
    static double[][] Tones(string dark,string light,double contrast,double paperMix) {
        var a=RGB(dark);var b=RGB(light);var paper=RGB("#F4F1E9");
        for(int i=0;i<3;i++) {
            double center=(a[i]+b[i])*.5;
            a[i]=(center+(a[i]-center)*contrast)*(1-paperMix)+paper[i]*paperMix;
            b[i]=(center+(b[i]-center)*contrast)*(1-paperMix)+paper[i]*paperMix;
        }
        return new[]{a,b};
    }
    static byte Byte(double v) => (byte)Math.Round(Clamp(v)*255);
    static void Layer(string root,string id,int width,int height,int grid,int levels,bool land,double[][] tone) {
        using(var source=new Bitmap(Path.Combine(root,"source",id+"-redraw.png")))
        using(var input=new Bitmap(source.Width,source.Height,PixelFormat.Format32bppArgb)) {
            using(var g=Graphics.FromImage(input))g.DrawImageUnscaled(source,0,0);
            var rect=new Rectangle(0,0,input.Width,input.Height);
            var locked=input.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            var bytes=new byte[locked.Stride*locked.Height];Marshal.Copy(locked.Scan0,bytes,0,bytes.Length);
            int stride=locked.Stride;input.UnlockBits(locked);
            int cols=width/grid,rows=height/grid;
            var logical=new int[cols*rows];var palette=new int[levels];
            for(int k=0;k<levels;k++) {
                double t=k/(double)(levels-1);
                palette[k]=Color.FromArgb(255,Byte(tone[0][0]*(1-t)+tone[1][0]*t),Byte(tone[0][1]*(1-t)+tone[1][1]*t),Byte(tone[0][2]*(1-t)+tone[1][2]*t)).ToArgb();
            }
            int taps=id=="birds"?5:3;
            for(int y=0;y<rows;y++)for(int x=0;x<cols;x++) {
                double sum=0,weight=0;int visible=0;
                for(int sy=0;sy<taps;sy++)for(int sx=0;sx<taps;sx++) {
                    int px=Math.Min(input.Width-1,(int)((x+(sx+.5)/taps)*input.Width/cols));
                    int py=Math.Min(input.Height-1,(int)((y+(sy+.5)/taps)*input.Height/rows));
                    int offset=py*stride+px*4;double alpha=bytes[offset+3]/255.0;
                    if(alpha<.55)continue;
                    visible++;sum+=(bytes[offset+2]*.299+bytes[offset+1]*.587+bytes[offset]*.114)/255.0*alpha;weight+=alpha;
                }
                double coverage=visible/(double)(taps*taps);
                double lower=land?Clamp((.14-(1-(y+.5)/rows))/.14):0;
                coverage=coverage*(1-lower)+lower;
                if(coverage<(id=="birds"?.08:.5))continue;
                double t=weight>0?Clamp((sum/weight-.70)/.25):.55;
                t=t*(1-lower)+.55*lower;
                logical[y*cols+x]=palette[(int)Math.Round(t*(levels-1))];
            }
            // Drop isolated generated specks; retain connected one-pixel wing tips.
            var cleaned=(int[])logical.Clone();
            for(int y=0;y<rows;y++)for(int x=0;x<cols;x++) {
                int n=0;
                for(int j=-1;j<=1;j++)for(int i=-1;i<=1;i++) {
                    if(i==0&&j==0)continue;int xx=x+i,yy=y+j;
                    if(xx>=0&&yy>=0&&xx<cols&&yy<rows&&logical[yy*cols+xx]!=0)n++;
                }
                if(logical[y*cols+x]!=0&&n==0)cleaned[y*cols+x]=0;
                else if(logical[y*cols+x]==0&&n>=7&&id!="birds") {
                    for(int i=-1;i<=1;i++)if(x+i>=0&&x+i<cols&&logical[y*cols+x+i]!=0){cleaned[y*cols+x]=logical[y*cols+x+i];break;}
                }
            }
            using(var output=new Bitmap(width,height,PixelFormat.Format32bppArgb)) {
                var result=new byte[width*height*4];var used=new HashSet<int>();
                for(int y=0;y<height;y++)for(int x=0;x<width;x++) {
                    int c=cleaned[(y/grid)*cols+x/grid];int pos=(y*width+x)*4;
                    result[pos]=(byte)c;result[pos+1]=(byte)(c>>8);result[pos+2]=(byte)(c>>16);result[pos+3]=(byte)(c>>24);
                    if(c!=0)used.Add(c);
                }
                var target=output.LockBits(new Rectangle(0,0,width,height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
                Marshal.Copy(result,0,target.Scan0,result.Length);output.UnlockBits(target);
                string file=Path.Combine(root,"runtime",id+".png");output.Save(file,ImageFormat.Png);
                if(used.Count>levels)throw new Exception("Palette overflow "+id);
                Console.WriteLine(id+": "+width+"x"+height+", grid "+grid+", colors "+used.Count+", binary alpha");
            }
        }
    }
    public static void Build(string root) {
        Layer(root,"near-village",666,334,2,6,true,Tones("#B6ADA3","#DFD8CE",1.35,.30));
        Layer(root,"far-village",444,222,1,4,true,Tones("#DCD5CA","#EEE7DC",1.10,0));
        Layer(root,"mountains",888,444,1,3,true,Tones("#E6E0D6","#F1ECE3",1,0));
    }
}
