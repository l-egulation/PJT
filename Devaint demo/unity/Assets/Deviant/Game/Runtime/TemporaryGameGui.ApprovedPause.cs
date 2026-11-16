using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        RectTransform approvedPauseDecor,approvedResultFooter;
        void SetApprovedPauseVisible(bool visible){if(approvedPauseDecor)approvedPauseDecor.gameObject.SetActive(visible);}
        void BuildApprovedPauseDecor()
        {
            if(approvedPauseDecor)return;
            approvedPauseDecor=Rect("Pause title and key help",menu.transform,0,0,960,540);
            Words("일시정지",approvedPauseDecor,330,50,300,30,23,false,null,TextAnchor.MiddleCenter);
            Fill(approvedPauseDecor,90,463.9f,780,.5f,new Color32(168,175,182,64));
            var moveKey=ApprovedKeycap(approvedPauseDecor,"↑↓",90,474.4f,88,MenuHintHeight,MenuHintFontSize);
            BindMenuKey(moveKey.GetComponentInChildren<Text>(),"↑↓",Deviant.Input.Contracts.GameAction.Navigate);
            FollowKeycap(moveKey.GetComponentInChildren<Text>(),Words("이동",approvedPauseDecor,98+moveKey.rectTransform.sizeDelta.x,474.4f,50,MenuHintHeight,MenuHintFontSize,false,JMuted));
            var selectKey=ApprovedKeycap(approvedPauseDecor,"F",250,474.4f,44,MenuHintHeight,MenuHintFontSize);
            BindMenuKey(selectKey.GetComponentInChildren<Text>(),"F",Deviant.Input.Contracts.GameAction.Submit);
            FollowKeycap(selectKey.GetComponentInChildren<Text>(),Words("선택",approvedPauseDecor,258+selectKey.rectTransform.sizeDelta.x,474.4f,50,MenuHintHeight,MenuHintFontSize,false,JMuted));
        }
        void LayoutApprovedPause()
        {
            BuildApprovedPauseDecor();SetApprovedPauseVisible(true);
            if(approvedResultFooter)approvedResultFooter.gameObject.SetActive(false);
            menuCaption.gameObject.SetActive(false);
            var buttons=new[]{menuPrimary,menuShortcuts,menuGuide,menuSecondary};float y=162.245f;
            foreach(var button in buttons)
            {
                if(!button.gameObject.activeSelf)continue;
                Place(button,305,y,350,34.25f);y+=52.25f;
                bool selected=EventSystem.current&&EventSystem.current.currentSelectedGameObject==button.gameObject;
                var label=button.GetComponentInChildren<Text>();label.fontSize=selected?21:18;label.alignment=TextAnchor.MiddleCenter;
                Place(label,25,4,300,26);
                var resultCap=button.transform.Find("Keycap F");if(resultCap)resultCap.gameObject.SetActive(false);
                var cap=button.transform.Find("Pause action key");
                if(!cap){cap=ApprovedKeycap(button.transform,"F",285,5.75f,21.28f,22.4f,13).transform;cap.name="Pause action key";}
                var keyText=cap.GetComponentInChildren<Text>();keyText.text=InventoryKey("F");
                float capWidth=FitApprovedKeycap(keyText);
                cap.gameObject.SetActive(selected);
                if(selected)Place(cap,175+Mathf.Min(260,label.preferredWidth)/2+10,5.75f,capWidth,22.4f);
                var visual=button.GetComponent<ApprovedActionVisual>();if(visual)visual.LeadingHighlightWidth=0;
            }
            // Audio controls live in Settings; keep them out of pause navigation.
            volumeCard.gameObject.SetActive(false);
        }
        void LayoutApprovedResultFooter()
        {
            if(!approvedResultFooter)
            {
                approvedResultFooter=Rect("Result key help",menu.transform,65,491.4f,830,31.1f);
                Fill(approvedResultFooter,0,0,830,.5f,new Color32(168,175,182,64));
                var moveKey=ApprovedKeycap(approvedResultFooter,"방향키",0,10.5f,88,MenuHintHeight,MenuHintFontSize);
                BindMenuKey(moveKey.GetComponentInChildren<Text>(),"방향키",Deviant.Input.Contracts.GameAction.Navigate);
                FollowKeycap(moveKey.GetComponentInChildren<Text>(),Words("이동",approvedResultFooter,moveKey.rectTransform.sizeDelta.x+8,10.5f,45,MenuHintHeight,MenuHintFontSize,false,JMuted));
                var selectKey=ApprovedKeycap(approvedResultFooter,"F",160,10.5f,44,MenuHintHeight,MenuHintFontSize);
                BindMenuKey(selectKey.GetComponentInChildren<Text>(),"F",Deviant.Input.Contracts.GameAction.Submit);
                FollowKeycap(selectKey.GetComponentInChildren<Text>(),Words("선택 / 실행",approvedResultFooter,168+selectKey.rectTransform.sizeDelta.x,10.5f,100,MenuHintHeight,MenuHintFontSize,false,JMuted));
            }
            approvedResultFooter.gameObject.SetActive(true);
        }
    }
}
