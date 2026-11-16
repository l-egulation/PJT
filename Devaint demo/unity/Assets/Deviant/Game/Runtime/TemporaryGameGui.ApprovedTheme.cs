using UnityEngine;
namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        const int MenuHintFontSize = Deviant.UI.Theme.ApprovedTheme.ControlHintFontSize;
        const float MenuHintHeight = Deviant.UI.Theme.ApprovedTheme.ControlHintHeight;
        RectTransform approvedMenuStage;
        readonly System.Collections.Generic.List<UnityEngine.UI.Image> screenBackdrops=new System.Collections.Generic.List<UnityEngine.UI.Image>();
        UnityEngine.UI.Image RegisterScreenBackdrop(UnityEngine.UI.Image image)
        {
            screenBackdrops.Add(image);return image;
        }
        void RefreshScreenBackdrop()
        {
            if(!dim)return;
            // A full-screen child owns its veil. Never multiply it by the root menu veil.
            foreach(var image in screenBackdrops)
                if(image&&image.gameObject.activeInHierarchy&&image.color.a>0)
                {dim.GetComponent<UnityEngine.UI.Image>().color=Color.clear;return;}
        }
        // Menus follow the design viewport continuously; world pixels and HUD keep integer scaling.
        RectTransform MenuStage
        {
            get
            {
                if(!approvedMenuStage)
                {
                    approvedMenuStage=Rect("Approved menu viewport",transform,0,0,960,540);
                    approvedMenuStage.anchorMin=approvedMenuStage.anchorMax=approvedMenuStage.pivot=Vector2.one*.5f;
                    approvedMenuStage.anchoredPosition=Vector2.zero;
                }
                return approvedMenuStage;
            }
        }
        void FitApprovedMenus(int width,int height)
        {
            if(!approvedMenuStage||!scaler||scaler.scaleFactor<=0)return;
            float fit=Mathf.Min(width/960f,height/540f);
            approvedMenuStage.localScale=Vector3.one*(fit/scaler.scaleFactor);
        }
        UnityEngine.UI.Image ApprovedKeycap(Transform parent,string key,float x,float y,float width=28,float height=26,int size=14)
        {
            var edge=new Color32(173,194,204,210);
            var box=Rounded("Keycap "+key,parent,x,y,width,height,3,1,Color.clear,edge);
            var glyph=Words(key,box.transform,0,0,width,height,size,false,edge,TextAnchor.MiddleCenter);
            glyph.alignByGeometry=true;
            // Single-key caps are square; longer bindings fit their actual glyphs, never a stretched button width.
            var aspect=box.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            aspect.aspectMode=UnityEngine.UI.AspectRatioFitter.AspectMode.HeightControlsWidth;
            FitApprovedKeycap(glyph);
            return box;
        }
        static float FitApprovedKeycap(UnityEngine.UI.Text glyph)
        {
            var box=glyph.transform.parent as RectTransform;
            var aspect=box?box.GetComponent<UnityEngine.UI.AspectRatioFitter>():null;
            if(!aspect)return 0;
            float height=box.sizeDelta.y;
            float width=string.IsNullOrEmpty(glyph.text)?box.sizeDelta.x:glyph.text.Length==1?height:Mathf.Max(height,Mathf.Ceil(glyph.preferredWidth)+12);
            box.sizeDelta=new Vector2(width,height);glyph.rectTransform.sizeDelta=new Vector2(width,height);
            aspect.aspectRatio=width/height;
            var caption=box.GetComponent<ApprovedKeycapCaption>();if(caption)caption.Refresh();
            return width;
        }
        static void FollowKeycap(UnityEngine.UI.Text key,UnityEngine.UI.Text caption,float gap=8)
        {
            var follow=key.transform.parent.gameObject.AddComponent<ApprovedKeycapCaption>();
            follow.Caption=caption.rectTransform;follow.Gap=gap;follow.Refresh();
        }
        void ApprovedMenuHints(Transform parent,float x,float y,string[] captions=null)
        {
            string[] keys={"↑↓←→","F","Esc","Q / E","W / S"};
            string[] labels=captions??new[]{"선택","확정","뒤로","탭","설명 스크롤"};
            float[] widths={88,44,50,65,44};
            var actions=new[]{Deviant.Input.Contracts.GameAction.Navigate,Deviant.Input.Contracts.GameAction.Submit,Deviant.Input.Contracts.GameAction.Cancel,Deviant.Input.Contracts.GameAction.Inventory,Deviant.Input.Contracts.GameAction.MapPan};
            for(int i=0;i<keys.Length;i++)
            {
                var keyText=ApprovedKeycap(parent,keys[i],x,y,widths[i],MenuHintHeight,MenuHintFontSize).GetComponentInChildren<UnityEngine.UI.Text>();
                BindMenuKey(keyText,keys[i],actions[i]);
                float width=((RectTransform)keyText.transform.parent).sizeDelta.x;
                var caption=Words(labels[i],parent,x+width+8,y,110,MenuHintHeight,MenuHintFontSize,false,new Color32(174,182,192,255));
                FollowKeycap(keyText,caption);
                float textWidth=caption.preferredWidth;caption.rectTransform.sizeDelta=new Vector2(textWidth,MenuHintHeight);
                x+=Mathf.Max(width,widths[i])+textWidth+28;
            }
        }
        static readonly Color ApprovedThemeText=new Color32(243,239,229,255);
        bool UseApprovedTheme(Transform parent)
        {
            if(oldTitle)return parent&&(parent==stage||parent.IsChildOf(stage)||approvedMenuStage&&(parent==approvedMenuStage||parent.IsChildOf(approvedMenuStage)));
            for(var p=parent;p;p=p.parent)
                if(p.name=="Gameplay HUD"||p.name=="Village currency"||p.name=="Pause")return false;
            return true;
        }
        static bool IsLegacySurface(string name)=>name=="paper"||name=="spine"||name=="title"||name=="card"||name=="primary"||name=="secondary"||name=="disabled"||name=="tab"||name=="tab-active"||name=="slot"||name=="slot-active";
        static Color ApprovedTextColor(Color color)
        {
            if(color==JWarning)return new Color32(204,155,146,255);
            if(color==JMuted||color==JDisabled||color==JSlotMark)return new Color32(174,182,192,255);
            if(color==JInk||color==JDark||color==Cream||color==PaperGuiSkin.Text)return ApprovedThemeText;
            return color;
        }
    }
}
