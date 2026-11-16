using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    public sealed class MaintenanceBubbleView : MonoBehaviour
    {
        Canvas canvas; CanvasScaler scaler; Camera view; RectTransform frame,bubble; Image upper,lower; Text body,nameLabel; MaintenanceBubbleGraphic graphic; Font font;
        public static MaintenanceBubbleView Create(Camera view)
        {var go=new GameObject("Maintenance first visit · pixel dialogue",typeof(RectTransform));var result=go.AddComponent<MaintenanceBubbleView>();result.Build(view);return result;}
        void Build(Camera camera)
        {
            view=camera;canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=220;canvas.pixelPerfect=true;
            scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;
            frame=new GameObject("960x540",typeof(RectTransform)).GetComponent<RectTransform>();frame.SetParent(transform,false);frame.anchorMin=frame.anchorMax=frame.pivot=Vector2.one*.5f;frame.sizeDelta=new Vector2(960,540);
            upper=Bar("Upper letterbox",0);lower=Bar("Lower letterbox",508);
            bubble=new GameObject("Speech bubble",typeof(RectTransform)).GetComponent<RectTransform>();bubble.SetParent(frame,false);bubble.anchorMin=bubble.anchorMax=new Vector2(0,1);bubble.pivot=new Vector2(0,1);
            graphic=bubble.gameObject.AddComponent<MaintenanceBubbleGraphic>();
            font=Resources.Load<Font>("PixelInventory/Fonts/Galmuri11-Bold");if(!font)font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            body=Label("Body",new Vector2(10,-9));nameLabel=Label("Merchant name",new Vector2(12,12));Hide();SetBars(false);
        }
        Image Bar(string title,int y)
        {var go=new GameObject(title,typeof(RectTransform));go.transform.SetParent(frame,false);var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(0,-y);r.sizeDelta=new Vector2(960,32);var image=go.AddComponent<Image>();image.color=new Color32(9,11,12,255);image.raycastTarget=false;return image;}
        Text Label(string title,Vector2 position)
        {var go=new GameObject(title,typeof(RectTransform));go.transform.SetParent(bubble,false);var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=position;r.sizeDelta=new Vector2(184,180);var text=go.AddComponent<Text>();text.font=font;text.fontSize=12;text.supportRichText=false;text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Overflow;text.verticalOverflow=VerticalWrapMode.Overflow;text.alignment=TextAnchor.UpperLeft;return text;}
        public void SetBars(bool show){upper.gameObject.SetActive(show);lower.gameObject.SetActive(show);Fit();}
        public void Fit()
        {
            // An overlay canvas covers the window, so scale to the window's 960x540 display rect even though the game camera
            // renders into the native 960x540 buffer; otherwise the frame stays 1x and its letterbox floats inside the picture.
            // Review captures switch to ScreenSpaceCamera on a target texture and use that size instead.
            var target=canvas.renderMode==RenderMode.ScreenSpaceOverlay||!view?null:view.targetTexture;
            var rect=Stage1VillageExperiment.VillagePixelViewport.CalculateDisplayRect(target?target.width:Screen.width,target?target.height:Screen.height);scaler.scaleFactor=canvas.scaleFactor=rect.width/960f;
        }
        float Width(string text)
        {font.RequestCharactersInTexture(text,12,FontStyle.Normal);float width=0;foreach(char ch in text)if(font.GetCharacterInfo(ch,out var info,12))width+=info.advance;return width;}
        // Explicit '\n' in a line starts a new row (the boss cutscene lines are written with their breaks).
        List<string> Wrap(string text)
        {var lines=new List<string>();foreach(var paragraph in text.Split('\n')){string line="";foreach(var word in paragraph.Split(' ')){string next=line.Length>0?line+" "+word:word;if(Width(next)<=184){line=next;continue;}if(line.Length>0)lines.Add(line);line="";foreach(char ch in word){if(line.Length>0&&Width(line+ch)>184){lines.Add(line);line="";}line+=ch;}}if(line.Length>0)lines.Add(line);}return lines;}
        // speaker null or empty draws no name tag; advance false never shows the ▼; top is the highest the bubble's top may
        // sit (below the workshop letterbox by default).
        /// <summary>The bubble last shown, in 960x540 frame pixels from the top-left (x, y, width, height).</summary>
        public RectInt Shown { get; private set; }
        /// <summary>Name tag colour for the next Show; null keeps the ink tag.</summary>
        public Color32? TagColor { get; set; }
        /// <summary>Bubble size in frame pixels for a line (same wrapping as Show).</summary>
        public Vector2Int Size(string text){var lines=Wrap(text);float width=0;foreach(var l in lines)width=Mathf.Max(width,Width(l));return new Vector2Int(Mathf.CeilToInt(width)+20,lines.Count*15+18);}
        public void Show(string text,int count,string speaker,bool sketch,Vector2 head,float time,bool advance=true,int top=53)
        {
            Fit();var size=Size(text);int w=size.x,h=size.y;
            var screen=view.WorldToViewportPoint(head);float hx=screen.x*960,hy=(1-screen.y)*540;
            int x=Mathf.RoundToInt(Mathf.Clamp(hx-w/2,8,952-w)),y=Mathf.RoundToInt(Mathf.Max(top,hy-h-10));
            Draw(text,count,speaker,sketch,x,y,Mathf.RoundToInt(Mathf.Clamp(hx-x,12,w-12)),screen.z>0,advance&&count>=text.Length&&time%.8f<.4f);
        }
        /// <summary>A bubble at a fixed frame position (top-left px). tailX &lt; 0 draws no tail (a line that cuts in over another).</summary>
        public void ShowAt(string text,int count,string speaker,bool sketch,int x,int y,int tailX){Fit();Draw(text,count,speaker,sketch,x,y,tailX,true,false);}
        void Draw(string text,int count,string speaker,bool sketch,int x,int y,int tail,bool onScreen,bool arrow)
        {
            bool named=!string.IsNullOrEmpty(speaker);var lines=Wrap(text);var size=Size(text);int w=size.x,h=size.y;
            bubble.gameObject.SetActive(onScreen);bubble.anchoredPosition=new Vector2(x,-y);bubble.sizeDelta=new Vector2(w,h);Shown=new RectInt(x,y,w,h);
            // White in both villages (playtest 2026-11-06: the cream paper read as yellowed); the ink stays per village.
            var fill=new Color32(255,255,255,255);var ink=sketch?new Color32(36,35,29,255):new Color32(31,27,25,255);
            graphic.Configure(w,h,tail,named?Mathf.CeilToInt(Width(speaker))+12:0,fill,ink,arrow,TagColor??ink);
            nameLabel.text=named?speaker:"";nameLabel.color=fill;body.color=ink;
            int offset=0;var visible=new List<string>();foreach(var line in lines){int pos=text.IndexOf(line,offset,System.StringComparison.Ordinal);offset=pos+line.Length;visible.Add(line.Substring(0,Mathf.Clamp(count-pos,0,line.Length)));}body.text=string.Join("\n",visible);
            // Fixed 15px line spacing with Galmuri's own font metrics.
            body.lineSpacing=15f/(font.lineHeight*12f/font.fontSize);
        }
        public void Hide(){if(bubble)bubble.gameObject.SetActive(false);}
    }
    // Point-filtered procedural UI pixels keep the adopted stepped border at native resolution.
    public sealed class MaintenanceBubbleGraphic : MonoBehaviour
    {
        // Kept for callers that opt out of the approved dark frame (village nameplates). Every bubble now draws the old
        // white pixel border (user 2026-11-14), so this only records the caller's choice.
        public bool UseApprovedFrame = false;
        RawImage image; Texture2D texture; string previous;
        public void Configure(int w,int h,int tail,int tag,Color fill,Color ink,bool arrow)=>Configure(w,h,tail,tag,fill,ink,arrow,ink);
        // tail < 0 draws no tail; tagInk colours the speaker name tag.
        public void Configure(int w,int h,int tail,int tag,Color fill,Color ink,bool arrow,Color tagInk)
        {
            string key=w+":"+h+":"+tail+":"+tag+":"+fill+":"+ink+":"+arrow+":"+tagInk;if(previous==key)return;previous=key;
            if(!image){var go=new GameObject("Pixel border",typeof(RectTransform));go.transform.SetParent(transform,false);go.transform.SetAsFirstSibling();image=go.AddComponent<RawImage>();image.raycastTarget=false;}
            if(texture)Destroy(texture);int height=h+21;
            texture=new Texture2D(w,height,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};var pixels=new Color32[w*height];
            void Draw(int x,int y,int width,int rows,Color color){for(int yy=Mathf.Max(0,y+14);yy<Mathf.Min(height,y+14+rows);yy++)for(int xx=Mathf.Max(0,x);xx<Mathf.Min(w,x+width);xx++)pixels[(height-1-yy)*w+xx]=color;}
            Draw(2,0,w-4,h,ink);Draw(0,2,w,h-4,ink);Draw(1,1,w-2,h-2,ink);Draw(3,2,w-6,h-4,fill);Draw(2,3,w-4,h-6,fill);
            if(tail>=0){for(int r=0;r<7;r++)Draw(tail-(14-2*r)/2,h-2+r,14-2*r,1,ink);
            for(int r=0;r<5;r++)Draw(tail-(10-2*r)/2,h-2+r,10-2*r,1,fill);}
            if(tag>0){Draw(6,-14,tag,16,tagInk);Draw(5,-13,tag+2,14,tagInk);}
            if(arrow)for(int r=0;r<4;r++)Draw(w-16+r,h-10+r,8-2*r,1,ink);
            texture.SetPixels32(pixels);texture.Apply();image.texture=texture;
            var rect=image.rectTransform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(0,14);rect.sizeDelta=new Vector2(w,height);
        }
        void OnDestroy(){if(texture)Destroy(texture);}
    }
}
