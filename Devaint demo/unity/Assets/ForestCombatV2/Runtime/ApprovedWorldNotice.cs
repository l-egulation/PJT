using UnityEngine;
using Deviant.UI.Theme;

namespace ForestCombatV2
{
    // Presentation-only world notices. Full-width vertical falloff has no rectangular side edge.
    internal static class ApprovedWorldNotice
    {
        static Texture2D shade;
        static ApprovedWorldNoticeBackdrop backdrop;
        internal static bool HasTopNotice=>backdrop&&backdrop.Visible;
        static readonly Color Paper=new Color32(243,239,229,255),Muted=new Color32(174,182,192,255);
        static Texture2D Shade
        {
            get
            {
                if(shade)return shade;
                shade=new Texture2D(1,128,TextureFormat.RGBA32,false){name="Approved top notice falloff",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
                var pixels=new Color[128];for(int y=0;y<128;y++){float t=y/127f;pixels[y]=new Color(1,1,1,Mathf.SmoothStep(0,.94f,Mathf.Clamp01(t/.25f)));}
                shade.SetPixels(pixels);shade.Apply();return shade;
            }
        }
        // IMGUI vertex/text colors are linear in a Linear project; match the other approved menus.
        static Color DrawColor(Color color)=>QualitySettings.activeColorSpace==ColorSpace.Linear?color.linear:color;
        static void DrawShade(Rect bounds,float alpha=1)
        {
            if(Event.current.type!=EventType.Repaint)return;
            if(!backdrop)
            {
                var root=new GameObject("Approved world notice backing",typeof(RectTransform));
                backdrop=root.AddComponent<ApprovedWorldNoticeBackdrop>();
            }
            var a=GUI.matrix.MultiplyPoint3x4(new Vector3(bounds.xMin,bounds.yMin,0));
            var b=GUI.matrix.MultiplyPoint3x4(new Vector3(bounds.xMax,bounds.yMax,0));
            backdrop.Present(Shade,Rect.MinMaxRect(a.x,a.y,b.x,b.y),alpha);
        }
        internal static GUIStyle Label(float size,Color color,TextAnchor anchor=TextAnchor.MiddleCenter)=>new GUIStyle(GUI.skin.label){font=ApprovedTheme.Font,fontSize=Mathf.RoundToInt(size),normal={textColor=DrawColor(color)},alignment=anchor,wordWrap=true,padding=new RectOffset(),richText=false};
        internal static void Top(string title,string description,float alpha=1,float contentOffset=0)
        {
            var matrix=GUI.matrix;var old=GUI.color;var viewport=SketchRewardChoiceScreen.Viewport(Screen.width,Screen.height);
            GUI.matrix=Matrix4x4.TRS(viewport.position,Quaternion.identity,Vector3.one*(viewport.width/960));GUI.color=new Color(1,1,1,alpha);
            DrawShade(new Rect(0,0,960,(string.IsNullOrEmpty(description)?125:166)+contentOffset),alpha);
            GUI.Label(new Rect(130,30+contentOffset,700,46),title,Label(28,Paper));
            if(!string.IsNullOrEmpty(description))GUI.Label(new Rect(130,84+contentOffset,700,40),description,Label(17,Muted));
            GUI.color=old;GUI.matrix=matrix;
        }
        internal static void SealPages(int page,int count)
        {
            var matrix=GUI.matrix;var old=GUI.color;var viewport=SketchRewardChoiceScreen.Viewport(Screen.width,Screen.height);
            GUI.matrix=Matrix4x4.TRS(viewport.position,Quaternion.identity,Vector3.one*(viewport.width/960));GUI.color=Color.white;
            DrawShade(new Rect(0,0,960,105));
            for(int i=0;i<count;i++)
            {
                var texture=ApprovedTheme.Texture("seal-progress/"+(i<page?"complete":i==page?"current":"pending"));
                if(texture)GUI.DrawTexture(SealPageBounds(i,count,i==page),texture,ScaleMode.ScaleToFit,true);
            }
            GUI.color=old;GUI.matrix=matrix;
        }
        internal static Rect SealPageBounds(int index,int count,bool current)
        {
            float size=current?36:30,centre=480+(index-(count-1)/2f)*48;
            return new Rect(centre-size/2,44-size/2,size,size);
        }
        internal static void Interaction(Camera camera,Vector2 world,string caption,string binding)
        {
            if(!camera||!ApprovedTheme.FrameSpeech)return;
            var point=Deviant.Presentation.Unity.CameraDisplayProjection.WorldToScreenPoint(camera,new Vector3(world.x,world.y,0));if(point.z<0)return;
            var viewport=SketchRewardChoiceScreen.Viewport(Screen.width,Screen.height);float scale=viewport.width/960;
            Vector2 at=(new Vector2(point.x,Screen.height-point.y)-viewport.position)/scale;
            var matrix=GUI.matrix;var old=GUI.color;GUI.matrix=Matrix4x4.TRS(viewport.position,Quaternion.identity,Vector3.one*scale);GUI.color=Color.white;
            var text=Label(17,Paper);var keyText=Label(15,ApprovedTheme.Accent);keyText.wordWrap=false;
            float keyWidth=Mathf.Max(24,keyText.CalcSize(new GUIContent(binding)).x+14),captionWidth=text.CalcSize(new GUIContent(caption)).x;
            float groupWidth=keyWidth+10+captionWidth,width=Mathf.Clamp(groupWidth+44,160,350);
            var box=new Rect(Mathf.Clamp(at.x-width/2,8,952-width),Mathf.Clamp(at.y-80,8,444),width,76);
            ApprovedTheme.DrawSpeech(box,.08f);
            float left=box.x+(box.width-groupWidth)/2;var cap=new Rect(left,box.y+22,keyWidth,24);
            GUI.color=DrawColor(ApprovedTheme.Accent);
            GUI.DrawTexture(new Rect(cap.x,cap.y,cap.width,.5f),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cap.x,cap.yMax-.5f,cap.width,.5f),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cap.x,cap.y,.5f,cap.height),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cap.xMax-.5f,cap.y,.5f,cap.height),Texture2D.whiteTexture);
            GUI.color=Color.white;GUI.Label(cap,binding,keyText);
            GUI.Label(new Rect(cap.xMax+10,box.y+18,captionWidth,31),caption,text);
            GUI.color=old;GUI.matrix=matrix;
        }
    }
}
