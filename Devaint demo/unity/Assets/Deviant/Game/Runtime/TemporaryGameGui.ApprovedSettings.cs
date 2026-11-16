using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        GameObject approvedSettings; RectTransform settingsBody; Text settingsTitle;
        int settingsTab, pendingWidth, pendingHeight, savedWidth, savedHeight;
        FullScreenMode pendingMode,savedMode; float displayDeadline; bool testingDisplay;
        GameObject displayQuestion; Text displayCountdown; int settingsInputFrame=-1;
        readonly Button[] settingsTabs = new Button[3];
        bool settingsTabsFocused=true;
        readonly Dictionary<Selectable,Text> settingsRowTitles=new Dictionary<Selectable,Text>();
        readonly Dictionary<Text,Color> settingsRowColors=new Dictionary<Text,Color>();
        void RegisterSettingsRow(Text title,params Selectable[] controls)
        {
            settingsRowColors[title]=title.color;
            foreach(var control in controls)settingsRowTitles[control]=title;
        }
        ApprovedSettingsLayerOcclusion settingsOcclusion,displayOcclusion;
        void OpenApprovedSettings()
        {
            CloseSettingsDraftQuestion(false);
            BuildApprovedSettings();
            pendingWidth=Screen.width;pendingHeight=Screen.height;pendingMode=Screen.fullScreenMode;
            RememberAcceptedDisplay();settingsTabsFocused=true;
            approvedSettings.SetActive(true);approvedSettings.transform.SetAsLastSibling();
            if(!settingsOcclusion)settingsOcclusion=approvedSettings.AddComponent<ApprovedSettingsLayerOcclusion>();
            settingsOcclusion.CoverSiblings(approvedSettings.transform.parent);RefreshApprovedSettings();
        }
        void BuildApprovedSettings()
        {
            if(!approvedSettings)
            {
                approvedSettings=Rect("설정",oldTitle?MenuStage:menu.transform,0,0,960,540).gameObject;
                var shade=RegisterScreenBackdrop(approvedSettings.AddComponent<Image>());shade.color=Deviant.UI.Theme.ApprovedTheme.NavigationBackdrop;shade.raycastTarget=true;
                settingsTitle=Words("설정",approvedSettings.transform,80,40,300,36,23,true);
                string[] names={"화면","소리","조작"};
                for(int i=0;i<3;i++){int index=i;settingsTabs[i]=SettingsAction(names[i],approvedSettings.transform,80,120+i*47,100,36,()=>RequestSettingsTab(index),18);}
                settingsBody=Rect("설정 내용",approvedSettings.transform,275,120,605,330);
                Fill(approvedSettings.transform,210,120,1,330,new Color32(84,90,96,150));
                Fill(approvedSettings.transform,80,476,800,.5f,new Color32(84,90,96,150));
                SettingsHint(approvedSettings.transform,80,"↑↓","탭/→본문",88);
                SettingsHint(approvedSettings.transform,260,"F","실행",44);
                SettingsHint(approvedSettings.transform,380,"Esc","닫기",50);
                approvedSettings.SetActive(false);
            }
        }
        void CloseApprovedSettings()
        {
            if(settingsDraftQuestion){CloseSettingsDraftQuestion();return;}
            if(testingDisplay){RestoreDisplay();return;}
            if(shortcutsInSettings)CloseShortcuts();
            if(oldTitle&&wellTitle&&wellTitle.SettingsOpen){wellTitle.CloseSettings();return;}
            if(settingsOcclusion)settingsOcclusion.Reveal();
            if(approvedSettings)approvedSettings.SetActive(false);
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject(menuShortcuts.gameObject);
        }
        void RefreshApprovedSettings()
        {
            var previousButtons=settingsBody.GetComponentsInChildren<Selectable>();
            int previousIndex=Array.FindIndex(previousButtons,b=>EventSystem.current&&EventSystem.current.currentSelectedGameObject==b.gameObject);
            DisposeInlineShortcuts();
            settingsRowTitles.Clear();settingsRowColors.Clear();
            ClearGrowth(settingsBody);
            for(int i=0;i<settingsTabs.Length;i++)
            {
                var visual=settingsTabs[i].GetComponent<ApprovedActionVisual>();
                if(visual)visual.PersistentSelection=settingsTabsFocused&&i==settingsTab;
            }
            if(settingsTab==0)
            {
                Words("화면 표시",settingsBody,0,0,600,24,12,false,JMuted);
                var modeTitle=Words("창 모드",settingsBody,0,52,180,36,15,true);
                DisplayArrows("Window mode",52,pendingMode==FullScreenMode.Windowed?"창 모드":"테두리 없는 창",_=>
                {pendingMode=pendingMode==FullScreenMode.Windowed?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed;RefreshApprovedSettings();},modeTitle);
                var resolutionTitle=Words("해상도",settingsBody,0,110,180,36,15,true);
                DisplayArrows("Resolution",110,pendingWidth+" × "+pendingHeight,StepDisplayResolution,resolutionTitle);
                Fill(settingsBody,0,94.5f,605,.5f,new Color32(168,175,182,43));
                Fill(settingsBody,0,152.5f,605,.5f,new Color32(168,175,182,43));
                Fill(settingsBody,0,182,605,.5f,new Color32(168,175,182,43));
                SettingsAction("적용",settingsBody,25,194,60,36,TestDisplay,17);
                Words("변경한 화면 설정을 적용합니다.",settingsBody,75,194,520,36,13,false,JMuted);
            }
            else if(settingsTab==1)
            {
                Words("소리 크기",settingsBody,0,0,600,24,12,false,JMuted);
                SettingsVolume("전체 음량",37,()=>Deviant.Music.GameAudioSettings.Master,Deviant.Music.GameAudioSettings.SetMaster);
                SettingsVolume("배경 음악",95,()=>Deviant.Music.GameAudioSettings.Music,Deviant.Music.GameAudioSettings.SetMusic);
                var titleSoundTitle=Words("타이틀 효과음",settingsBody,0,173.125f,230,20,15,false);
                DisplayArrows("Title sound",167.625f,WellTitlePresentation.TitleSoundEnabled?"켜짐":"꺼짐",_=>{WellTitlePresentation.TitleSoundEnabled=!WellTitlePresentation.TitleSoundEnabled;RefreshApprovedSettings();},titleSoundTitle);
                Fill(settingsBody,0,211,605,.5f,new Color32(168,175,182,43));
                Fill(settingsBody,0,239,605,.5f,new Color32(168,175,182,64));
                Words("소리 설정은 조절한 값으로 바로 저장됩니다.",settingsBody,0,259.375f,605,24,12,false,JMuted);
            }
            else
            {
                BuildInlineShortcuts(Controls);
            }

            var buttons=settingsBody.GetComponentsInChildren<Selectable>();
            var first=buttons.Length>0?buttons[Mathf.Clamp(previousIndex,0,buttons.Length-1)]:settingsTabs[settingsTab];
            for(int i=0;i<settingsTabs.Length;i++)settingsTabs[i].navigation=new Navigation {
                mode=Navigation.Mode.Explicit,selectOnUp=i>0?settingsTabs[i-1]:null,
                selectOnDown=i+1<settingsTabs.Length?settingsTabs[i+1]:null,selectOnRight=first };
            // Keep native row geometry; only connect boundary exits back to the active left tab.
            var navigation=new Dictionary<Selectable,Navigation>();
            foreach(var button in buttons)
            {
                bool InBody(Selectable candidate)=>candidate&&candidate.transform.IsChildOf(settingsBody);
                var left=button.FindSelectableOnLeft();var right=button.FindSelectableOnRight();
                var up=button.FindSelectableOnUp();var down=button.FindSelectableOnDown();
                bool slider=button is Slider;
                navigation[button]=new Navigation {mode=Navigation.Mode.Explicit,
                    selectOnLeft=slider?null:InBody(left)?left:settingsTabs[settingsTab],
                    selectOnRight=slider?null:InBody(right)?right:null,
                    selectOnUp=InBody(up)?up:settingsTabs[settingsTab],selectOnDown=InBody(down)?down:null};
            }
            foreach(var entry in navigation)entry.Key.navigation=entry.Value;
            SetSettingsContentFocus();
            var focus=settingsTabsFocused?settingsTabs[settingsTab]:first;
            if(focus&&EventSystem.current&&!EventSystem.current.alreadySelecting)EventSystem.current.SetSelectedGameObject(focus.gameObject);
        }
        void SetSettingsContentFocus()
        {
            var group=settingsBody.GetComponent<CanvasGroup>();if(!group)group=settingsBody.gameObject.AddComponent<CanvasGroup>();
            group.alpha=settingsTabsFocused?.3f:1;group.blocksRaycasts=!settingsTabsFocused;
            for(int i=0;i<settingsTabs.Length;i++)settingsTabs[i].GetComponent<ApprovedActionVisual>().PersistentSelection=settingsTabsFocused&&i==settingsTab;
            var selected=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            Text focusedTitle=null;
            var control=selected?selected.GetComponent<Selectable>():null;
            if(!settingsTabsFocused&&control)settingsRowTitles.TryGetValue(control,out focusedTitle);
            foreach(var row in settingsRowColors)if(row.Key)row.Key.color=row.Key==focusedTitle?new Color32(217,203,132,255):row.Value;
        }
        void DisplayArrows(string name,float y,string value,Action<int> change,Text rowTitle=null)
        {
            var left=SettingsAction("←",settingsBody,360,y,23,28.25f,()=>change(-1),16,TextAnchor.MiddleCenter);
            left.name=name+" previous";left.GetComponent<ApprovedActionVisual>().NormalColor=Deviant.UI.Theme.ApprovedTheme.Accent;
            Words(value,settingsBody,407.5f,y+5,150,22,16,false,Deviant.UI.Theme.ApprovedTheme.Paper,TextAnchor.MiddleCenter).name=name+" value";
            var right=SettingsAction("→",settingsBody,582.5f,y,23,28.25f,()=>change(1),16,TextAnchor.MiddleCenter);
            right.name=name+" next";right.GetComponent<ApprovedActionVisual>().NormalColor=Deviant.UI.Theme.ApprovedTheme.Accent;
            if(rowTitle)RegisterSettingsRow(rowTitle,left,right);
        }
        void StepDisplayResolution(int direction)
        {
            int[] widths={960,1280,1600,1920,2560};
            int next;
            if(direction>0){next=Array.FindIndex(widths,w=>w>pendingWidth);if(next<0)next=0;}
            else {next=Array.FindLastIndex(widths,w=>w<pendingWidth);if(next<0)next=widths.Length-1;}
            pendingWidth=widths[next];pendingHeight=pendingWidth*9/16;RefreshApprovedSettings();
        }
        void SettingsVolume(string name,float y,Func<float> get,Action<float> set)
        {
            var rowTitle=Words(name,settingsBody,0,y+20.125f,230,20,15,false);
            Fill(settingsBody,0,y+57.5f,605,.5f,new Color32(168,175,182,43));
            var root=Rect(name+" slider",settingsBody,315,y+16.25f,224,25);
            root.gameObject.AddComponent<Image>().color=Color.clear;
            var slider=root.gameObject.AddComponent<Slider>();slider.minValue=0;slider.maxValue=10;slider.wholeNumbers=true;
            var track=Fill(root,0,10,224,5,new Color32(151,177,184,64));track.raycastTarget=true;
            var fillArea=Rect("Fill area",root,0,10,224,5);
            var fill=Fill(fillArea,0,0,0,0,new Color32(151,177,184,242));
            fill.rectTransform.anchorMin=Vector2.zero;fill.rectTransform.anchorMax=Vector2.one;fill.rectTransform.anchoredPosition=Vector2.zero;
            slider.fillRect=fill.rectTransform;
            var handleArea=Rect("Handle area",root,0,.5f,224,24);
            var handle=Fill(handleArea,0,.5f,19,24,Color.white);handle.sprite=Deviant.UI.Theme.ApprovedTheme.Sprite("common/settings-v1__assets__slider-thumb");handle.preserveAspect=false;
            handle.rectTransform.anchorMin=handle.rectTransform.anchorMax=new Vector2(0,.5f);handle.rectTransform.pivot=Vector2.one*.5f;
            handle.rectTransform.sizeDelta=new Vector2(19,0);handle.rectTransform.anchoredPosition=Vector2.zero;
            slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;slider.direction=Slider.Direction.LeftToRight;
            slider.SetValueWithoutNotify(Mathf.RoundToInt(get()*10));
            var value=Words(Deviant.Music.GameAudioSettings.Percent(get()),settingsBody,555,y+19.25f,50,22,16,false,null,TextAnchor.MiddleRight);
            slider.onValueChanged.AddListener(amount=>{set(amount/10f);value.text=Deviant.Music.GameAudioSettings.Percent(get());});
            RegisterSettingsRow(rowTitle,slider);
        }
        void TestDisplay()
        {
            savedWidth=Screen.width;savedHeight=Screen.height;savedMode=Screen.fullScreenMode;
            Screen.SetResolution(pendingWidth,pendingHeight,pendingMode);displayDeadline=Time.unscaledTime+15;testingDisplay=true;
            displayQuestion=Rect("화면 설정 유지",approvedSettings.transform,0,0,960,540).gameObject;
            displayOcclusion=displayQuestion.AddComponent<ApprovedSettingsLayerOcclusion>();
            displayOcclusion.CoverSiblings(approvedSettings.transform,true);
            var image=RegisterScreenBackdrop(displayQuestion.AddComponent<Image>());image.color=Deviant.UI.Theme.ApprovedTheme.ConfirmationBackdrop;image.raycastTarget=true;
            var paper=new Color32(238,233,223,255);var blue=new Color32(181,216,228,255);
            var muted=new Color32(179,187,196,255);
            Words("이 화면 설정을 유지할까요?",displayQuestion.transform,264,185.565f,432,30.45f,21,false,paper);
            var summary=Words((pendingMode==FullScreenMode.Windowed?"창 모드":"테두리 없는 창")+" / "+pendingWidth+" × "+pendingHeight,displayQuestion.transform,264,232.515f,432,28.46f,17,false,muted);
            summary.name="Display mode summary";
            displayCountdown=Words("15초 뒤 이전 설정으로 돌아갑니다.",displayQuestion.transform,264,260.975f,432,28.46f,17,false,muted);
            Fill(displayQuestion.transform,264,307.435f,432,.5f,new Color32(168,175,182,64));
            var revert=SettingsAction("되돌리기",displayQuestion.transform,264,324.435f,99,30,RestoreDisplay,19);
            var keep=SettingsAction("유지",displayQuestion.transform,392.87f,324.435f,70,30,KeepDisplay,19);
            foreach(var button in new[]{revert,keep})
            {
                bool cancel=button==revert;var color=cancel?paper:blue;string key=cancel?"F":"G";
                button.name=cancel?"Revert display settings":"Keep display settings";
                button.navigation=new Navigation{mode=Navigation.Mode.None};
                button.GetComponent<ApprovedActionVisual>().enabled=false;
                var label=button.GetComponentInChildren<Text>();Place(label,40.5f,3.68f,cancel?59:30,27.2f);label.color=color;
                var cap=Rounded("Keycap "+key,button.transform,0,0,28.5f,30,3,1,Color.clear,color);
                Words(key,cap.transform,0,0,28.5f,30,18,false,color,TextAnchor.MiddleCenter);
            }
            EventSystem.current?.SetSelectedGameObject(keep.gameObject);
        }
        Button SettingsAction(string caption,Transform parent,float x,float y,float w,float h,Action clicked,int size,TextAnchor alignment=TextAnchor.MiddleLeft)
        {
            var button=Action(caption,parent,x,y,w,h,clicked);
            var label=button.GetComponentInChildren<Text>();label.fontSize=size;label.alignment=alignment;
            return button;
        }
        Button ApprovedKeyAction(string caption,string key,Transform parent,float x,float y,float w,float h,Action clicked,int size=16,float keyWidth=17)
        {
            var button=SettingsAction(caption,parent,x,y,w,h,clicked,size);
            var keyText=SettingsKeycap(button.transform,0,(h-keyWidth)/2,key,keyWidth,keyWidth,size);
            float fitted=((RectTransform)keyText.transform.parent).sizeDelta.x;
            var label=button.GetComponentInChildren<Text>();Place(label,fitted+12,0,w-fitted-12,h);
            return button;
        }
        void SettingsHint(Transform parent,float x,string key,string caption,float keyWidth,float y=487)
        {
            var action=key=="F"?Deviant.Input.Contracts.GameAction.Submit:key=="Esc"?Deviant.Input.Contracts.GameAction.Cancel:Deviant.Input.Contracts.GameAction.Navigate;
            var keyText=SettingsKeycap(parent,x,y,key,keyWidth,MenuHintHeight,MenuHintFontSize);
            BindMenuKey(keyText,key,action);
            float fitted=((RectTransform)keyText.transform.parent).sizeDelta.x;
            FollowKeycap(keyText,Words(caption,parent,x+fitted+6,y,65,MenuHintHeight,MenuHintFontSize,false,JMuted),6);
        }
        Text SettingsKeycap(Transform parent,float x,float y,string key,float w,float h,int size)
            =>ApprovedKeycap(parent,key,x,y,w,h,size).GetComponentInChildren<Text>();
        void KeepDisplay()
        {
            testingDisplay=false;
            RememberAcceptedDisplay();
            if(displayOcclusion)displayOcclusion.Reveal();displayOcclusion=null;
            if(displayQuestion){displayQuestion.SetActive(false);Destroy(displayQuestion);}displayQuestion=null;
            RefreshApprovedSettings();
        }
        void RestoreDisplay(){Screen.SetResolution(savedWidth,savedHeight,savedMode);pendingWidth=savedWidth;pendingHeight=savedHeight;pendingMode=savedMode;KeepDisplay();}
        bool TickApprovedSettings()
        {
            if(settingsInputFrame==Time.frameCount)return true;
            if(!approvedSettings||!approvedSettings.activeInHierarchy)return false;
            if(settingsDraftQuestion){TickSettingsDraftQuestion();return true;}
            if(shortcutsInSettings&&shortcutPanelOpen&&!shortcutCapture&&editingBinding<0&&Keyboard.current?.gKey.wasPressedThisFrame==true)
            {
                OpenShortcutResetQuestion(ApplyShortcutDefaults);Controls?.SuppressGameplay();return true;
            }
            if(testingDisplay)
            {
                settingsInputFrame=Time.frameCount;
                var key=Keyboard.current;var pad=Gamepad.current;
                if(displayCountdown)displayCountdown.text=Mathf.Max(0,Mathf.CeilToInt(displayDeadline-Time.unscaledTime))+"초 뒤 이전 설정으로 돌아갑니다.";
                Controls?.Take(Deviant.Input.Contracts.GameAction.Cancel);
                Controls?.Take(Deviant.Input.Contracts.GameAction.Pause);
                // Native submit is disabled for this fixed-action modal: F reverts, G keeps.
                // Keep the controller's usual South confirm / East or Start back semantics.
                Controls?.Take(Deviant.Input.Contracts.GameAction.Submit);
                if(Time.unscaledTime>=displayDeadline||key!=null&&(key.fKey.wasPressedThisFrame||key.escapeKey.wasPressedThisFrame)||pad!=null&&(pad.buttonEast.wasPressedThisFrame||pad.startButton.wasPressedThisFrame))RestoreDisplay();
                else if(key!=null&&key.gKey.wasPressedThisFrame||pad!=null&&pad.buttonSouth.wasPressedThisFrame)KeepDisplay();
                Controls?.SuppressGameplay();return true;
            }
            var selected=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            int tab=Array.FindIndex(settingsTabs,b=>b&&b.gameObject==selected);
            if(tab>=0){settingsTabsFocused=true;if(tab!=settingsTab)RequestSettingsTab(tab);}
            else if(selected&&selected.transform.IsChildOf(settingsBody))settingsTabsFocused=false;
            SetSettingsContentFocus();
            return false;
        }
    }

    // Hides only UI siblings; the world and the overlay's translucent shade remain visible.
    // CanvasGroup remains effective if LayoutMenu re-enables a sibling while this overlay is open.
    sealed class ApprovedSettingsLayerOcclusion : MonoBehaviour
    {
        struct SavedLayer
        {
            public CanvasGroup Group;
            public float Alpha;
            public bool Interactable,BlocksRaycasts;
        }
        readonly List<SavedLayer> saved=new List<SavedLayer>();
        Transform coveredParent;
        Image parentBackdrop;
        Color parentBackdropColor;
        bool coverParentBackdrop;
        public void CoverSiblings(Transform parent,bool hideParentBackdrop=false)
        {
            RestoreCaptured();coveredParent=parent;coverParentBackdrop=hideParentBackdrop;
            if(!parent)return;
            if(hideParentBackdrop)
            {
                parentBackdrop=parent.GetComponent<Image>();
                if(parentBackdrop){parentBackdropColor=parentBackdrop.color;parentBackdrop.color=Color.clear;}
            }
            foreach(Transform sibling in parent)
            {
                if(sibling==transform)continue;
                var group=sibling.GetComponent<CanvasGroup>();
                if(!group)group=sibling.gameObject.AddComponent<CanvasGroup>();
                saved.Add(new SavedLayer{Group=group,Alpha=group.alpha,Interactable=group.interactable,BlocksRaycasts=group.blocksRaycasts});
                group.alpha=0;group.interactable=false;group.blocksRaycasts=false;
            }
        }
        public void Reveal(){coveredParent=null;RestoreCaptured();}
        void RestoreCaptured()
        {
            if(parentBackdrop)parentBackdrop.color=parentBackdropColor;
            parentBackdrop=null;
            foreach(var layer in saved)
            {
                if(!layer.Group)continue;
                layer.Group.alpha=layer.Alpha;layer.Group.interactable=layer.Interactable;layer.Group.blocksRaycasts=layer.BlocksRaycasts;
            }
            // Newly added groups remain neutral after restore. This also makes close/reopen within
            // one frame safe: there is no deferred component destruction that could remove a new cover.
            saved.Clear();
        }
        void OnEnable(){if(coveredParent)CoverSiblings(coveredParent,coverParentBackdrop);}
        void OnDisable()=>RestoreCaptured();
        void OnDestroy()=>RestoreCaptured();
    }

}
