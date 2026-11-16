using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.ThemePreview
{
    [Serializable] public sealed class NativeStop { public float at; public float[] c; }
    [Serializable] public sealed class NativePaint { public string t,hash,mode; public float[] c,m; public float o=1; public NativeStop[] stops; }
    [Serializable] public sealed class NativeNode
    {
        public string id,n,text,align,valign,parent,artId; public bool wrap; public float x,y,w,h,size,line,blur,o=1; public bool hide,clip,mask;
        public NativePaint[] p; [NonSerialized] public NativeNode[] ch; public string[] paths; public float[] mesh;
        public string Caption => text??string.Join(" ",(ch??Array.Empty<NativeNode>()).Select(x=>x.Caption));
    }
    [Serializable] public sealed class NativeScreen { public string id,key,name,group; public NativeNode[] nodes; [NonSerialized] public NativeNode root; }
    [Serializable] public sealed class NativeAsset { public string hash,path,sha256; }
    [Serializable] public sealed class NativeCatalogue { public NativeScreen[] screens; public NativeAsset[] assets; }

    // Native geometry, not a screenshot: each paint is independently clipped and each label is TMP.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class NativePaintGraphic : MaskableGraphic
    {
        public NativePaint Paint; public Texture Source; public float Blur; public Vector2 OriginalSize; public float[] Mesh;
        public override Texture mainTexture => Source!=null?Source:Texture2D.whiteTexture;
        public static Color Rgba(float[] c)=>c==null?Color.white:new Color(c[0],c[1],c[2],c.Length>3?c[3]:1);
        public static Color Sample(NativePaint paint,float u,float v)
        {
            if(paint.t=="s")return Rgba(paint.c);
            if(paint.t!="g"||paint.stops==null||paint.stops.Length==0)return new Color(1,1,1,paint.o);
            float t=paint.m!=null?paint.m[0]*u+paint.m[1]*v+paint.m[2]:v;
            var stops=paint.stops; if(t<=stops[0].at)return Rgba(stops[0].c);
            for(int i=1;i<stops.Length;i++)if(t<=stops[i].at)return Color.Lerp(Rgba(stops[i-1].c),Rgba(stops[i].c),Mathf.InverseLerp(stops[i-1].at,stops[i].at,t));
            return Rgba(stops[stops.Length-1].c);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(Paint==null)return;var r=rectTransform.rect;
            if(Mesh!=null&&Mesh.Length>0){for(int i=0;i<Mesh.Length;i+=2)vh.AddVert(new Vector3(r.xMin+Mesh[i],r.yMax-Mesh[i+1]),Sample(Paint,0,0)*color,Vector2.zero);for(int i=0;i<Mesh.Length/2;i+=3)vh.AddTriangle(i,i+1,i+2);return;}int steps=Paint.t=="g"||Blur>0?32:1;
            for(int y=0;y<=steps;y++)for(int x=0;x<=steps;x++)
            {
                float u=x/(float)steps,v=y/(float)steps;Vector2 uv=new Vector2(u,1-v);
                if(Paint.t=="i"&&Paint.m!=null){var m=Paint.m;uv=new Vector2(m[0]*u+m[1]*v+m[2],1-(m[3]*u+m[4]*v+m[5]));}
                var tint=Sample(Paint,u,v)*color;
                if(Blur>0){float px=u*r.width-Blur*2,py=v*r.height-Blur*2;float dx=Mathf.Max(-px,px-OriginalSize.x),dy=Mathf.Max(-py,py-OriginalSize.y);float edge=Mathf.Max(dx,dy);tint.a*=1/(1+Mathf.Exp(edge/Mathf.Max(1,Blur*.4f)));}
                vh.AddVert(new Vector3(r.xMin+u*r.width,r.yMax-v*r.height),tint,uv);
            }
            for(int y=0;y<steps;y++)for(int x=0;x<steps;x++){int i=y*(steps+1)+x;vh.AddTriangle(i,i+1,i+steps+1);vh.AddTriangle(i+1,i+steps+2,i+steps+1);}
        }
    }
    public sealed partial class ThemePreviewScreen
    {
        NativeCatalogue catalogue;
        readonly Dictionary<string,Texture2D> nativeTextures=new Dictionary<string,Texture2D>();
        readonly Dictionary<string,RectTransform> nativeRects=new Dictionary<string,RectTransform>();
        readonly Dictionary<string,NativeNode> nativeNodes=new Dictionary<string,NativeNode>();
        readonly List<NativeNode> nativeScrolls=new List<NativeNode>();
        void LoadNativeCatalogue()
        {
            if(catalogue!=null)return;
            var path=Path.Combine(Application.dataPath,"Deviant/UI/ThemePreview/Layout/figma-layouts.json");
            catalogue=JsonUtility.FromJson<NativeCatalogue>(File.ReadAllText(path));
            foreach(var screen in catalogue.screens){var children=screen.nodes.ToLookup(n=>n.parent);foreach(var node in screen.nodes)node.ch=children[node.id].ToArray();screen.root=screen.nodes.First(n=>string.IsNullOrEmpty(n.parent));}
            if(catalogue.screens.Length!=119)throw new InvalidDataException("119 approved remaining Figma states required");
        }
        Texture2D NativeTexture(string hash)
        {
            if(nativeTextures.TryGetValue(hash,out var t))return t;
            var record=catalogue.assets.FirstOrDefault(x=>x.hash==hash)??throw new InvalidDataException("Unmapped Figma asset: "+hash);
            var config=JsonUtility.FromJson<PreviewConfig>(File.ReadAllText(ConfigPath));
            var root=Path.GetFullPath(Path.Combine(config.packagePath,".."));
            var path=Path.GetFullPath(Path.Combine(root,record.path));
            if(!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.Ordinal))throw new InvalidDataException("Invalid Figma asset path");
            var bytes=File.ReadAllBytes(path);
            using(var sha=System.Security.Cryptography.SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant()!=record.sha256)throw new InvalidDataException("Figma asset differs: "+record.path);
            t=new Texture2D(2,2,TextureFormat.RGBA32,false);t.name=record.path;t.LoadImage(bytes);t.filterMode=record.path.EndsWith(".jpg")?FilterMode.Bilinear:FilterMode.Point;t.wrapMode=TextureWrapMode.Clamp;nativeTextures.Add(hash,t);return t;
        }
        NativeNode nativeBakedOverlay;
        Texture2D NativeBackdrop(NativeNode n)
        {
            var paints=n.p.Concat(nativeBakedOverlay?.p??Array.Empty<NativePaint>()).ToArray();
            var image=n.p.First(p=>p.t=="i");string key="Backdrop:"+image.hash+string.Join("",paints.Select(JsonUtility.ToJson));
            if(nativeTextures.TryGetValue(key,out var cached))return cached;
            var source=NativeTexture(image.hash);var pixels=source.GetPixels32();
            for(int y=0;y<source.height;y++)
            {
                var overlays=paints.Where(p=>p.t!="i").Select(p=>NativePaintGraphic.Sample(p,.5f,1-y/(float)(source.height-1))).ToArray();
                for(int x=0;x<source.width;x++){int i=y*source.width+x;Color c=pixels[i];foreach(var overlay in overlays){float a=overlay.a;c=new Color(c.r*(1-a)+overlay.r*a,c.g*(1-a)+overlay.g*a,c.b*(1-a)+overlay.b*a,1);}pixels[i]=c;}
            }
            var t=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);t.SetPixels32(pixels);t.Apply();t.filterMode=FilterMode.Bilinear;t.wrapMode=TextureWrapMode.Clamp;nativeTextures[key]=t;return t;
        }
        Texture2D NativeInkComposite(NativeNode root)
        {
            string key="Composite:"+root.id;if(nativeTextures.TryGetValue(key,out var cached))return cached;
            int width=Mathf.CeilToInt(root.w),height=Mathf.CeilToInt(root.h);var pixels=new Color[width*height];
            void PaintNode(NativeNode n,float px,float py,Rect clip,bool first)
            {
                if(n.hide)return;float x=px+(first?0:n.x),y=py+(first?0:n.y);
                if(n.clip){float left=Mathf.Max(clip.xMin,x),top=Mathf.Max(clip.yMin,y),right=Mathf.Min(clip.xMax,x+n.w),bottom=Mathf.Min(clip.yMax,y+n.h);clip=Rect.MinMaxRect(left,top,right,bottom);}
                if(n.p!=null)foreach(var paint in n.p)
                {
                    if(paint.t!="i")continue;var source=NativeTexture(paint.hash);
                    for(int iy=Mathf.Max(0,Mathf.CeilToInt(Mathf.Max(y,clip.yMin)-.5f));iy<Mathf.Min(height,Mathf.CeilToInt(Mathf.Min(y+n.h,clip.yMax)-.5f));iy++)
                    for(int ix=Mathf.Max(0,Mathf.CeilToInt(Mathf.Max(x,clip.xMin)-.5f));ix<Mathf.Min(width,Mathf.CeilToInt(Mathf.Min(x+n.w,clip.xMax)-.5f));ix++)
                    {
                        float u=(ix+.5f-x)/n.w,v=(iy+.5f-y)/n.h;var m=paint.m;
                        float su=m==null?u:m[0]*u+m[1]*v+m[2],sv=m==null?v:m[3]*u+m[4]*v+m[5];
                        var c=source.GetPixel(Mathf.Clamp((int)(su*source.width),0,source.width-1),Mathf.Clamp((int)((1-sv)*source.height),0,source.height-1));
                        int index=(height-1-iy)*width+ix;var old=pixels[index];float a=c.a+old.a*(1-c.a);
                        if(a>0)pixels[index]=new Color((c.r*c.a+old.r*old.a*(1-c.a))/a,(c.g*c.a+old.g*old.a*(1-c.a))/a,(c.b*c.a+old.b*old.a*(1-c.a))/a,a);
                    }
                }
                if(n.ch!=null)foreach(var child in n.ch)PaintNode(child,x,y,clip,false);
            }
            PaintNode(root,0,0,new Rect(0,0,root.w,root.h),true);
            var t=new Texture2D(width,height,TextureFormat.RGBA32,false);t.SetPixels(pixels);t.Apply();t.filterMode=FilterMode.Point;nativeTextures[key]=t;return t;
        }
        readonly Dictionary<UnityEngine.UI.RawImage,Texture2D> selectionOriginalTextures=new Dictionary<UnityEngine.UI.RawImage,Texture2D>();
        Texture2D NativeMutedTexture(Texture2D source)
        {
            string key="Muted:"+source.GetEntityId();if(nativeTextures.TryGetValue(key,out var cached))return cached;
            var pixels=source.GetPixels();for(int i=0;i<pixels.Length;i++){var c=pixels[i];float g=c.r*.2126f+c.g*.7152f+c.b*.0722f;pixels[i]=new Color(Mathf.Lerp(g,c.r,.55f),Mathf.Lerp(g,c.g,.55f),Mathf.Lerp(g,c.b,.55f),c.a);}
            var t=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);t.name=key;t.SetPixels(pixels);t.Apply();t.filterMode=source.filterMode;t.wrapMode=TextureWrapMode.Clamp;nativeTextures[key]=t;return t;
        }
        void NativeDraw(NativeNode n,Transform parent,bool root=false)
        {
            if(n.hide||n.mask||n.w<=.01f||n.h<=.01f)return;
            var rt=Box(n.n,parent,root?0:n.x,root?0:n.y,n.w,n.h);nativeRects[n.id]=rt;nativeNodes[n.id]=n;
            if(!string.IsNullOrEmpty(n.artId))
            {
                if(n.artId.StartsWith("ink:"))
                {
                    var ink=art.Package.inks.First(i=>i.id==n.artId.Substring(4));var v=ink.view;float scale=Mathf.Min(n.w/v[2],n.h/v[3]);float ox=(n.w-v[2]*scale)/2;
                    Art(rt,ink.bottle,ox,0,v[2]*scale,v[3]*scale);
                    foreach(var pair in new[]{(ink.effect,ink.label),(ink.condition,ink.badge)}){var m=pair.Item2;Art(rt,pair.Item1,ox+(m.center[0]-v[0]-m.size/2)*scale,(m.center[1]-v[1]-m.size/2)*scale,m.size*scale,m.size*scale);}
                }
                else {var v=art.Find(n.artId).view;float scale=Mathf.Min(n.w/v[2],n.h/v[3]);Art(rt,n.artId,(n.w-v[2]*scale)/2,(n.h-v[3]*scale)/2,v[2]*scale,v[3]*scale);}
                return;
            }
            if(n.n=="Key border")
            {
                float unit=1/Mathf.Max(.1f,Mathf.Min(UnityEngine.Screen.width/1920f,UnityEngine.Screen.height/1080f));
                if(n.w<=1.1f){rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,unit);if(n.x>0)rt.anchoredPosition=new Vector2(((RectTransform)parent).rect.width-unit,rt.anchoredPosition.y);}
                if(n.h<=1.1f){rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,unit);if(n.y>0)rt.anchoredPosition=new Vector2(rt.anchoredPosition.x,-((RectTransform)parent).rect.height+unit);}
            }
            if(n.o<.999f)rt.gameObject.AddComponent<CanvasGroup>().alpha=n.o;
            if(n.clip)rt.gameObject.AddComponent<RectMask2D>();
            if((n.n.StartsWith("inkVisual")||n.n=="speechSkin"))
            {var im=rt.gameObject.AddComponent<UnityEngine.UI.RawImage>();im.texture=NativeInkComposite(n);im.raycastTarget=false;return;}
            if(n.text!=null)
            {
                var t=rt.gameObject.AddComponent<TextMeshProUGUI>();t.font=art.Font;t.text=nativeOverrides.TryGetValue(n.id,out var changed)?changed:n.text;t.fontSize=n.size;t.raycastTarget=false;
                t.color=n.p!=null&&n.p.Length>0?NativePaintGraphic.Sample(n.p[0],0,0):paper;
                if(t.text.Contains("✓"))
                {
                    float mark=n.size*.65f;float left=n.align=="RIGHT"?Mathf.Max(0,n.w-t.GetPreferredValues(t.text).x):0;
                    t.text=t.text.Replace("✓"," ");var check=Box("Check mark",rt,left,Mathf.Max(0,(n.h-mark)/2),mark,mark);
                    var graphic=check.gameObject.AddComponent<NativePaintGraphic>();graphic.Paint=new NativePaint{t="s",c=new[]{t.color.r,t.color.g,t.color.b,t.color.a}};
                    float[] points={0,.5f,.13f,.37f,.42f,.68f,0,.5f,.42f,.68f,.4f,.93f,.4f,.93f,.42f,.68f,.88f,0,.4f,.93f,.88f,0,1,.12f};graphic.Mesh=points.Select(v=>v*mark).ToArray();graphic.raycastTarget=false;
                }
                rt.anchoredPosition+=Vector2.down*Mathf.Max(0,(n.line-n.h)*.5f);
                t.margin=Vector4.zero;t.textWrappingMode=n.wrap?TextWrappingModes.Normal:TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Overflow;
                t.alignment=n.align=="CENTER"?TextAlignmentOptions.Top:n.align=="RIGHT"?TextAlignmentOptions.TopRight:TextAlignmentOptions.TopLeft;
                if(n.valign=="CENTER")t.alignment=n.align=="CENTER"?TextAlignmentOptions.Center:n.align=="RIGHT"?TextAlignmentOptions.MidlineRight:TextAlignmentOptions.MidlineLeft;
                // Native keycaps use a fixed centre, independent of glyph baseline.
                if(parent.name=="KBD"||parent.name=="actionKeycap")t.alignment=TextAlignmentOptions.Center;
            }
            else if(n.n=="world"&&n.p?.Any(p=>p.t=="i")==true)
            {var im=rt.gameObject.AddComponent<UnityEngine.UI.RawImage>();im.texture=NativeBackdrop(n);im.raycastTarget=false;}
            else if(n.p!=null&&n!=nativeBakedOverlay)
            {
                foreach(var paint in n.p)
                {
                    var pr=Box("Figma paint",rt,-n.blur*2,-n.blur*2,rt.rect.width+n.blur*4,rt.rect.height+n.blur*4);if(paint.t=="i"){var im=pr.gameObject.AddComponent<UnityEngine.UI.RawImage>();im.texture=NativeTexture(paint.hash);im.raycastTarget=false;if(paint.m!=null)im.uvRect=new Rect(paint.m[2],1-paint.m[5]-paint.m[4],paint.m[0],paint.m[4]);continue;}
                    var g=pr.gameObject.AddComponent<NativePaintGraphic>();g.Paint=paint;g.Mesh=n.mesh;g.Blur=n.blur;g.OriginalSize=new Vector2(n.w,n.h);g.raycastTarget=false;
                    if(paint.t=="i")g.Source=NativeTexture(paint.hash);
                    g.SetAllDirty();
                }
            }
            if(n.ch!=null)foreach(var child in n.ch)NativeDraw(child,rt);
            if(n.clip&&n.ch!=null&&n.ch.Any(x=>x.y+x.h>n.h+4||x.n=="Scroll thumb"))nativeScrolls.Add(n);
        }
        void ScrollNative(int direction)
        {
            var candidates=nativeScrolls.Where(n=>n.n!="screen"&&n.n!="app").ToArray();if(candidates.Length==0)return;
            var selected=NativeControls.Count>0?NativeControls[Mathf.Clamp(ExtendedPick,0,NativeControls.Count-1)]:null;
            var target=selected!=null?candidates.FirstOrDefault(n=>n.id==selected.id||ContainsNode(n,selected.id)):null;
            target=target??candidates.Last();
            var children=target.ch.Where(n=>n.n!="Scroll thumb"&&n.n!="Scroll track").ToArray();
            float extent=children.Select(n=>n.y+n.h).DefaultIfEmpty(target.h).Max();
            float old=nativeScrollOffsets.TryGetValue(target.id,out var value)?value:0;
            float offset=Mathf.Clamp(old+direction*90,0,Mathf.Max(0,extent-target.h));nativeScrollOffsets[target.id]=offset;
            foreach(var n in children)if(nativeRects.TryGetValue(n.id,out var r))r.anchoredPosition=new Vector2(n.x,-n.y+offset);
            var thumb=target.ch.FirstOrDefault(n=>n.n=="Scroll thumb");if(thumb!=null&&nativeRects.TryGetValue(thumb.id,out var tr))tr.anchoredPosition=new Vector2(thumb.x,-(target.h-thumb.h)*offset/Mathf.Max(1,extent-target.h));
        }
        static bool ContainsNode(NativeNode n,string id)=>n.id==id||(n.ch?.Any(c=>ContainsNode(c,id))??false);
        public float NativeScrollAmount=>nativeScrollOffsets.Values.Sum();
        readonly Dictionary<string,float> nativeScrollOffsets=new Dictionary<string,float>();
        void DisposeNative(){foreach(var t in nativeTextures.Values)if(t!=null)Destroy(t);nativeTextures.Clear();}
    }
}
