using Deviant.UI.Theme;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Deviant.Game
{
    public sealed class ApprovedTabFocus : MonoBehaviour, ISelectHandler
    {
        public System.Action Selected;
        bool pending;
        public void OnSelect(BaseEventData data) => pending=true;
        void LateUpdate()
        {
            if(!pending)return;pending=false;
            if(EventSystem.current&&EventSystem.current.currentSelectedGameObject==gameObject)Selected?.Invoke();
        }
        void OnDisable()=>pending=false;
    }
    // Keep captions adjacent when a keyboard/pad binding changes while a menu is open.
    public sealed class ApprovedKeycapCaption : MonoBehaviour
    {
        public RectTransform Caption;
        public float Gap;
        public void Refresh()
        {
            if(!Caption)return;
            var key=(RectTransform)transform;var position=Caption.anchoredPosition;
            position.x=key.anchoredPosition.x+key.sizeDelta.x+Gap;Caption.anchoredPosition=position;
        }
    }
    // Keeps hit geometry independent from the measured text-sized selection wash.
    public sealed class ApprovedActionVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        Button button; Text label; RawImage wash;
        public bool PersistentSelection;
        public bool DrawWash = false;
        public void OnPointerEnter(PointerEventData data)
        {
            if(button&&button.interactable&&EventSystem.current&&!EventSystem.current.alreadySelecting)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }
        public void OnPointerExit(PointerEventData data) { }
        public Color? NormalColor { get; set; }
        public Color? SelectedColor { get; set; }
        public float LeadingHighlightWidth;
        void Awake()
        {
            button=GetComponent<Button>();label=GetComponentInChildren<Text>();
            var go=new GameObject("Approved selection",typeof(RectTransform),typeof(RawImage));go.transform.SetParent(transform,false);go.transform.SetAsFirstSibling();
            wash=go.GetComponent<RawImage>();wash.texture=ApprovedTheme.SelectionWash;wash.raycastTarget=false;wash.color=new Color(1,1,1,.48f);
            wash.enabled=false;wash.rectTransform.sizeDelta=Vector2.zero;
        }
        void OnEnable() => Canvas.preWillRenderCanvases += RefreshVisual;
        void OnDisable()
        {
            Canvas.preWillRenderCanvases -= RefreshVisual;
            if(wash)wash.enabled=false;
        }
        // Navigation rebuilds buttons. Enable and size the wash BEFORE uGUI builds
        // its graphics; willRenderCanvases runs after that pass and leaves a blank frame.
        void RefreshVisual()
        {
            if(!button||!label)return;
            bool selected=button.interactable&&(PersistentSelection||EventSystem.current&&EventSystem.current.currentSelectedGameObject==gameObject);
            if(button.image){button.image.sprite=null;button.image.color=Color.clear;}
            label.font=ApprovedTheme.Font?ApprovedTheme.Font:label.font;
            label.color=!button.interactable?new Color32(110,116,122,255):selected?SelectedColor??new Color32(217,203,132,255):NormalColor??new Color32(174,182,192,255);
            if(!selected||!DrawWash){wash.enabled=false;return;}
            var buttonRect=(RectTransform)transform;var textRect=label.rectTransform;var bounds=textRect.rect;
            float width=Mathf.Min(bounds.width,label.preferredWidth),height=Mathf.Min(bounds.height,label.preferredHeight);
            int horizontal=(int)label.alignment%3,vertical=(int)label.alignment/3;
            float x=horizontal==0?bounds.xMin+width/2:horizontal==2?bounds.xMax-width/2:bounds.center.x;
            float y=vertical==0?bounds.yMax-height/2:vertical==2?bounds.yMin+height/2:bounds.center.y;
            var center=buttonRect.InverseTransformPoint(textRect.TransformPoint(new Vector3(x,y,0)));
            var r=wash.rectTransform;r.anchorMin=r.anchorMax=buttonRect.pivot;r.pivot=Vector2.one*.5f;
            center.x-=LeadingHighlightWidth/2;
            r.localPosition=center;r.sizeDelta=new Vector2(Mathf.Min(buttonRect.rect.width+12,width+24+LeadingHighlightWidth),Mathf.Min(buttonRect.rect.height,height+8));
            wash.enabled=true;
        }
    }
}
