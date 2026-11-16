using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Input.Contracts;
using Deviant.UI.Theme;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        [Serializable] sealed class RecordStoryPage { public string id, image, heading, note, text; public string[] paragraphs; }
        [Serializable] sealed class RecordStory { public string id, title, subtitle, preview, unlock, readKey; public bool cutscene; public RecordStoryPage[] pages; }
        [Serializable] sealed class RecordStories { public int schemaVersion; public RecordStory[] stories; }
        [Serializable] sealed class RecordNote { public string[] lines, afterFirstRun, afterFirstInk; }
        sealed class RecordRow { public RecordStory story; public Button button; public Image fill, dot; public Image[] border; public Text title, subtitle; public GameObject locked; }
        sealed class RecordAction
        {
            public Button button; public Image surface, cap; public Text label, hint; public Image[] border, capBorder;
            public float right, top; public bool framed, keyFirst, page;
        }
        readonly List<RecordAction> recordActions = new List<RecordAction>();
        Font recordFont;
        Image recordFooterKey;
        Text recordFooterPlay;
        static RecordStories recordStories;
        static RecordNote recordNote;
        RectTransform recordList, recordListContent, recordDetail, recordPreview;
        Text recordTitle, recordCount, recordPreviewTitle, recordPreviewText, recordPageLabel, recordBody, recordPlayingTitle;
        RawImage recordIllustration, recordPreviewArt;
        GameObject recordLockedPreview, recordPreviewTag;
        Button recordClose, recordReturn, recordPlay, recordNext;
        CanvasGroup recordPageFade;
        readonly List<RecordRow> recordRows = new List<RecordRow>();
        Coroutine recordTransition;
        bool recordBusy;
        int recordStoryIndex = -1, recordStoryPage, recordSelectedIndex, recordMoveSign, recordInputFrame = -1;
        float recordMoveRepeat;
        static readonly Color ArchiveInk = ApprovedTheme.Paper, ArchiveMuted = ApprovedTheme.Muted, ArchiveGold = ApprovedTheme.Gold;
        static readonly Color ArchiveRule = new Color32(84,90,96,255);
        static readonly Color ArchiveAccent = ApprovedTheme.CharacterEye;
        public bool RecordDetailShowing => recordStoryIndex >= 0;
        public bool RecordBusy => recordBusy;
        public int RecordStoryCount => StoryCatalog.stories.Length;
        public int RecordUnlockedCount => AvailableRecordStories().Length;
        public bool RecordSelectionLocked => !RecordStoryUnlocked(StoryCatalog.stories[recordSelectedIndex]);
        public int RecordPageCount => RecordDetailShowing ? StoryCatalog.stories[recordStoryIndex].pages.Length : 0;
        public string RecordSelectedStory => StoryCatalog.stories[RecordDetailShowing ? recordStoryIndex : recordSelectedIndex].id;
        public int RecordBookSpread => recordStoryPage + 1;
        public int RecordJournalStage => InkReturned(1) && Profile.Snapshot.HasSeen(FirstRunVisitStory) ? 2 : Profile.Snapshot.HasSeen(FirstRunVisitStory) ? 1 : 0;
        public bool RecordHasUpdate => AvailableRecordStories().Any(s => !Profile.Snapshot.HasSeen(s.readKey));
        static RecordStories StoryCatalog
        {
            get
            {
                if (recordStories != null) return recordStories;
                var source=Resources.Load<TextAsset>("HubRecordStories");
                if (!source) throw new InvalidOperationException("HubRecordStories missing; run tools/assets/build_hub_bindings.py.");
                return recordStories=JsonUtility.FromJson<RecordStories>(source.text);
            }
        }
        static RecordNote RecordNoteData()
        {
            if(recordNote!=null)return recordNote;
            var source=Resources.Load<TextAsset>("HubRecordNote");
            return recordNote=source?JsonUtility.FromJson<RecordNote>(source.text):new RecordNote();
        }
        bool RecordStoryUnlocked(RecordStory story) => story.unlock switch {
            "arrival" => true,
            "first-run" => Profile.Snapshot.HasSeen(FirstRunVisitStory),
            "chapter1-clear" => Profile.Snapshot.Chapter1Clears > 0,
            "first-ink" => RecordJournalStage >= 2,
            _ => false
        };
        RecordStory[] AvailableRecordStories() => StoryCatalog.stories.Where(RecordStoryUnlocked).ToArray();
        // Approved font uses the same canvas units as the other game menus.
        const float ArchiveTextScale=1f;
        static float ArchiveTextWidth(Text text)=>text.preferredWidth*ArchiveTextScale;
        static void PlaceArchiveText(Text text,float x,float y,float w,float h)
        {Place(text,x,y,w/ArchiveTextScale,h/ArchiveTextScale);text.rectTransform.localScale=Vector3.one*ArchiveTextScale;}
        Text ArchiveText(string text,Transform parent,float x,float y,float w,float h,int size,Color colour,TextAnchor align=TextAnchor.MiddleLeft)
        {
            var label=Label(text,parent,x,y,w,h,size,colour);label.font=recordFont?recordFont:font;label.fontSize=size;label.fontStyle=FontStyle.Normal;label.verticalOverflow=VerticalWrapMode.Overflow;label.horizontalOverflow=HorizontalWrapMode.Overflow;label.alignment=align;label.supportRichText=false;label.raycastTarget=false;PlaceArchiveText(label,x,y,w,h);return label;
        }
        Image[] ArchiveBorder(Transform parent,float w,float h,Color colour) => new[] {
            Fill(parent,0,0,w,1,colour),Fill(parent,0,h-1,w,1,colour),Fill(parent,0,0,1,h,colour),Fill(parent,w-1,0,1,h,colour)
        };
        // CSS-like content widths: key badges fit the label instead of stretching into a second button.
        static float ArchiveTintAlpha(float alpha)
        {
            if(QualitySettings.activeColorSpace!=ColorSpace.Linear)return alpha;
            const float background=.14f;
            return (Mathf.GammaToLinearSpace(Mathf.Lerp(background,1,alpha))-Mathf.GammaToLinearSpace(background))/(1-Mathf.GammaToLinearSpace(background));
        }
        Image ArchiveKey(Transform parent,string binding,float x,float y,int size=MenuHintFontSize,float height=MenuHintHeight,float minimum=19,bool actionKey=false)
        {
            var colour=actionKey?ApprovedTheme.Paper:ApprovedTheme.Muted;
            var cap=Plain(parent,x,y,minimum,height,new Color32(36,39,43,204),"Key");
            var label=ArchiveText("",cap.transform,0,0,minimum,height,size,colour,TextAnchor.MiddleCenter);
            if(binding.StartsWith("{"))BindPrompt(label,binding);else label.text=binding;
            float width=Mathf.Max(minimum,ArchiveTextWidth(label)+12);cap.rectTransform.sizeDelta=new Vector2(width,height);PlaceArchiveText(label,0,0,width,height);
            ArchiveBorder(cap.transform,width,height,actionKey?ArchiveAccent:ArchiveRule);return cap;
        }
        GameObject ArchiveLock(Transform parent,float x,float y,float scale=1)
        {
            var root=Rect("자물쇠",parent,x,y,16*scale,16*scale);
            foreach(var r in new[]{new Rect(5,1,6,2),new Rect(4,2,2,5),new Rect(10,2,2,5),new Rect(2,7,12,8)})Fill(root,r.x*scale,r.y*scale,r.width*scale,r.height*scale,ArchiveMuted);
            Fill(root,7*scale,10*scale,2*scale,3*scale,ApprovedTheme.Surface);return root.gameObject;
        }
        Button ArchiveButton(string text,Transform parent,float x,float y,float w,Action clicked,string key,bool gold=false)
        {
            var image=Plain(parent,x,y,w,gold?46:21,Color.clear,text);image.raycastTarget=true;
            var label=ArchiveText(text,image.transform,0,0,w,20,gold?13:11,gold?ArchiveInk:ArchiveMuted,TextAnchor.MiddleLeft);
            bool keyFirst=text=="닫기";
            var cap=ArchiveKey(image.transform,key,0,0,keyFirst?MenuHintFontSize:10,keyFirst?MenuHintHeight:18,keyFirst?19:26,!keyFirst);
            var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;button.transition=Selectable.Transition.None;button.navigation=new Navigation{mode=Navigation.Mode.None};button.onClick.AddListener(()=>clicked());
            var action=new RecordAction{button=button,surface=image,label=label,cap=cap,hint=cap.GetComponentInChildren<Text>(),right=x+w,top=y,framed=gold,keyFirst=keyFirst,page=string.IsNullOrEmpty(text),border=gold?ArchiveBorder(image.transform,w,46,ArchiveGold):Array.Empty<Image>(),capBorder=cap.GetComponentsInChildren<Image>().Where(i=>i!=cap).ToArray()};
            recordActions.Add(action);LayoutRecordActions();return button;
        }
        void LayoutRecordActions()
        {
            if(recordFooterKey)
            {
                var hint=recordFooterKey.GetComponentInChildren<Text>();float width=Mathf.Max(19,ArchiveTextWidth(hint)+12);
                Place(recordFooterKey,550.6f,483,width,MenuHintHeight);PlaceArchiveText(hint,0,0,width,MenuHintHeight);
                var edges=recordFooterKey.GetComponentsInChildren<Image>().Where(i=>i!=recordFooterKey).ToArray();
                Place(edges[0],0,0,width,1);Place(edges[1],0,MenuHintHeight-1,width,1);Place(edges[2],0,0,1,MenuHintHeight);Place(edges[3],width-1,0,1,MenuHintHeight);
                PlaceArchiveText(recordFooterPlay,554.6f+width,483,25,MenuHintHeight);
            }
            foreach(var a in recordActions)
            {
                bool footer=!a.framed&&a.top>440;
                if(footer){a.hint.fontSize=MenuHintFontSize;a.label.fontSize=MenuHintFontSize;}
                bool hasKey=a.cap.gameObject.activeSelf;float keyHeight=footer?MenuHintHeight:18;
                float keyWidth=Mathf.Max(a.keyFirst?19:26,ArchiveTextWidth(a.hint)+12),textWidth=ArchiveTextWidth(a.label);
                float gap=a.framed||a.page?9:a.keyFirst?4:0,padding=a.framed?17:6;
                float height=a.framed?46:a.keyFirst?21:20,width=padding*2+textWidth+(hasKey?keyWidth+gap:0);
                Place(a.surface,a.right-width,a.top,width,height);
                PlaceArchiveText(a.label,a.keyFirst?padding+keyWidth+gap:padding,(height-(a.framed?17:15))/2,textWidth,a.framed?17:15);
                Place(a.cap,a.keyFirst?padding:padding+textWidth+gap,(height-keyHeight)/2,keyWidth,keyHeight);PlaceArchiveText(a.hint,0,0,keyWidth,keyHeight);
                if(a.capBorder.Length==4){Place(a.capBorder[0],0,0,keyWidth,1);Place(a.capBorder[1],0,keyHeight-1,keyWidth,1);Place(a.capBorder[2],0,0,1,keyHeight);Place(a.capBorder[3],keyWidth-1,0,1,keyHeight);}
                if(a.framed)
                {
                    Color edge=a.button.interactable?ArchiveAccent:ArchiveRule;
                    a.surface.color=new Color(ArchiveAccent.r,ArchiveAccent.g,ArchiveAccent.b,ArchiveTintAlpha(a.button.interactable?.10f:.025f));
                    Place(a.border[0],0,0,width,1);Place(a.border[1],0,height-1,width,1);Place(a.border[2],0,0,1,height);Place(a.border[3],width-1,0,1,height);
                    foreach(var border in a.border)border.color=edge;
                    a.label.color=a.button.interactable?ArchiveInk:ArchiveMuted;
                }
            }
        }
        void BuildRecordArchive()
        {
            recordFont=ApprovedTheme.Font ?? font;
            record=Rect("기록 농가 / 이야기",MenuStage,0,0,960,540).gameObject;
            // Use the workshop veil so the village remains equally visible behind both facilities.
            var veil=RegisterScreenBackdrop(Plain(record.transform,0,0,960,540,Veil,"Archive dark veil"));veil.raycastTarget=true;
            recordList=Rect("목록과 미리보기",record.transform,0,0,960,540);
            recordTitle=ArchiveText("기록",recordList,72,68,816,36,26,ArchiveInk,TextAnchor.MiddleCenter);
            ArchiveText("이야기 기록",recordList,72,113,62,16,10,ArchiveMuted);
            ArchiveKey(recordList,"↑",138.34f,113.67f,9,16);ArchiveKey(recordList,"↓",171.34f,113.67f,9,16);
            recordCount=ArchiveText("",recordList,610,113,278,14,10,ArchiveMuted,TextAnchor.MiddleRight);
            recordListContent=JournalScroll("스토리 목록",recordList,72,144,224,292);
            recordPreview=Rect("미리보기",recordList,334,144,554,292);
            var artFrame=Plain(recordPreview,0,0,554,194,new Color(0,0,0,.12f),"Preview frame");ArchiveBorder(artFrame.transform,554,194,new Color(ArchiveRule.r,ArchiveRule.g,ArchiveRule.b,.6f));
            recordPreviewArt=Rect("미리보기 그림",artFrame.transform,0,0,554,194).gameObject.AddComponent<RawImage>();recordPreviewArt.raycastTarget=false;recordPreviewArt.color=new Color(1,1,1,.92f);
            var tag=Plain(artFrame.transform,13,161,54,22,new Color32(36,39,43,230),"Preview tag");recordPreviewTag=tag.gameObject;ArchiveText("미리보기",tag.transform,7,4,40,14,10,ArchiveInk);
            recordLockedPreview=Rect("잠긴 미리보기",artFrame.transform,0,0,554,194).gameObject;
            ArchiveLock(recordLockedPreview.transform,261,65.5f,2);
            ArchiveText("아직 열리지 않은 이야기",recordLockedPreview.transform,0,112.5f,554,16,12,ArchiveMuted,TextAnchor.MiddleCenter);
            recordPreviewTitle=ArchiveText("",recordPreview,0,208,398,24,17,ArchiveInk);
            recordPreviewText=ArchiveText("",recordPreview,0,235,398,20,11,ArchiveMuted);
            recordPlay=ArchiveButton("이야기 재생",recordPreview,389,208.5f,165,()=>OpenRecordStory(recordSelectedIndex),"F",true);
            ArchiveText("남겨진 이야기를 다시 살펴봅니다.",recordList,72,482,380,21,11,ArchiveMuted);
            ArchiveKey(recordList,"↑",466.6f,483);ArchiveKey(recordList,"↓",493.6f,483);
            ArchiveText("선택",recordList,520.6f,483,22,MenuHintHeight,MenuHintFontSize,ArchiveMuted);
            recordFooterKey=ArchiveKey(recordList,"F",550.6f,483);
            recordFooterPlay=ArchiveText("재생",recordList,573.6f,483,25,MenuHintHeight,MenuHintFontSize,ArchiveMuted);
            recordClose=ArchiveButton("닫기",recordList,792,482,96,CloseHubPanel,"Esc");
            recordDetail=Rect("스토리 재생",record.transform,0,0,960,540);Plain(recordDetail,0,0,960,540,Color.black,"Cutscene black");
            var page=Rect("컷씬 페이지",recordDetail,0,0,960,540);recordPageFade=page.gameObject.AddComponent<CanvasGroup>();recordPageFade.interactable=false;recordPageFade.blocksRaycasts=false;
            recordIllustration=Rect("스토리 그림",page,0,0,960,540).gameObject.AddComponent<RawImage>();recordIllustration.raycastTarget=false;
            recordBody=ArchiveText("",page,56,486,673,48,14,ArchiveInk,TextAnchor.UpperLeft);recordBody.horizontalOverflow=HorizontalWrapMode.Wrap;recordBody.lineSpacing=22f/(ArchiveTextScale*14f*recordBody.font.lineHeight/recordBody.font.fontSize);recordBody.verticalOverflow=VerticalWrapMode.Truncate;
            recordPlayingTitle=ArchiveText("",recordDetail,72,19,620,20,12,ArchiveMuted);
            recordReturn=ArchiveButton("목록으로",recordDetail,760,19,128,ShowRecordList,"Esc");
            recordNext=ArchiveButton("",recordDetail,732,502,172,()=>TurnRecordStory(1),"F");
            recordPageLabel=recordNext.GetComponentsInChildren<Text>()[0];
            RegisterScreen(record,recordClose,"BuildRecord");ShowRecordList();
        }
        void StopRecordTransition()
        {
            if(recordTransition!=null)StopCoroutine(recordTransition);recordTransition=null;recordBusy=false;if(recordNext)recordNext.interactable=true;
        }
        void OpenRecordArchive(){recordInputFrame=Time.frameCount;ShowRecordList();OpenHubPanel(record);record.transform.SetAsLastSibling();MarkSelectedRecordRead();}
        // Choosing an unlocked story (the amber box) reads it: its dot goes, and the farmhouse "!" goes with the last one.
        void MarkSelectedRecordRead()
        {
            var story=StoryCatalog.stories[recordSelectedIndex];
            if(!RecordStoryUnlocked(story)||Profile.Snapshot.HasSeen(story.readKey))return;
            Profile.MarkStorySeen(story.readKey);RefreshRecordList();
        }
        void ShowRecordList()
        {
            StopRecordTransition();recordStoryIndex=-1;recordStoryPage=0;recordMoveSign=0;
            if(!recordList)return;recordList.gameObject.SetActive(true);recordDetail.gameObject.SetActive(false);RefreshRecordList();
        }
        void RefreshRecordList()
        {
            if(recordRows.Count!=StoryCatalog.stories.Length)
            {
                ClearGrowth(recordListContent);recordRows.Clear();
                for(int i=0;i<StoryCatalog.stories.Length;i++)
                {
                    int index=i;var story=StoryCatalog.stories[i];var fill=Plain(recordListContent,1,i*72+1,218,63,Color.clear,story.title);fill.raycastTarget=true;
                    var button=fill.gameObject.AddComponent<Button>();button.targetGraphic=fill;button.transition=Selectable.Transition.None;button.navigation=new Navigation{mode=Navigation.Mode.None};button.onClick.AddListener(()=>SelectRecordStory(index));
                    var row=new RecordRow{story=story,button=button,fill=fill,border=ArchiveBorder(fill.transform,218,63,ArchiveMuted)};
                    row.title=ArchiveText(story.title,fill.transform,17,11,184,23,17,ArchiveInk);
                    row.subtitle=ArchiveText("",fill.transform,17,34,184,18,10,ArchiveMuted);
                    row.dot=Fill(fill.transform,200,17,4,4,ArchiveGold);
                    row.locked=ArchiveLock(fill.transform,188,20);recordRows.Add(row);
                }
                recordListContent.sizeDelta=new Vector2(224,Mathf.Max(292,StoryCatalog.stories.Length*72));
            }
            recordSelectedIndex=Mathf.Clamp(recordSelectedIndex,0,recordRows.Count-1);
            recordCount.text="열린 이야기 "+RecordUnlockedCount+" / "+recordRows.Count;
            for(int i=0;i<recordRows.Count;i++)
            {
                var row=recordRows[i];bool locked=!RecordStoryUnlocked(row.story),chosen=i==recordSelectedIndex;
                row.title.color=locked?new Color32(122,130,140,255):ArchiveInk;
                row.subtitle.text=Deviant.UI.Theme.ApprovedInkText.Display(locked?"아직 열리지 않은 이야기":row.story.subtitle??"");row.subtitle.color=locked?new Color32(105,113,123,255):ArchiveMuted;
                row.locked.SetActive(locked);row.dot.gameObject.SetActive(!locked&&!Profile.Snapshot.HasSeen(row.story.readKey));
                row.fill.color=chosen?new Color(ArchiveAccent.r,ArchiveAccent.g,ArchiveAccent.b,ArchiveTintAlpha(locked?.035f:.12f)):Color.clear;
                foreach(var edge in row.border)edge.color=chosen?(locked?ArchiveRule:ArchiveAccent):new Color(ArchiveRule.r,ArchiveRule.g,ArchiveRule.b,locked?.3f:.6f);
            }
            RefreshRecordPreview();
        }
        void SelectRecordStory(int index)
        {
            recordSelectedIndex=(index%StoryCatalog.stories.Length+StoryCatalog.stories.Length)%StoryCatalog.stories.Length;RefreshRecordList();MarkSelectedRecordRead();
            var scroll=recordListContent.GetComponentInParent<ScrollRect>();
            if(recordListContent.rect.height>292)scroll.verticalNormalizedPosition=1-(float)recordSelectedIndex/(StoryCatalog.stories.Length-1);
        }
        void RefreshRecordPreview()
        {
            var story=StoryCatalog.stories[recordSelectedIndex];bool locked=!RecordStoryUnlocked(story);
            recordLockedPreview.SetActive(locked);recordPreviewTag.SetActive(!locked);recordPreviewArt.enabled=!locked;recordPlay.interactable=!locked;
            recordPlay.GetComponentsInChildren<Text>()[0].text=locked?"잠겨 있음":"이야기 재생";
            recordPlay.transform.Find("Key").gameObject.SetActive(!locked);
            recordPreviewTitle.text=Deviant.UI.Theme.ApprovedInkText.Display(locked?"잠긴 이야기":story.subtitle??story.title);
            recordPreviewTitle.color=locked?ArchiveMuted:ArchiveInk;
            recordPreviewText.text=locked?"게임을 진행하면 이야기가 열립니다.":story.pages.Length+"페이지";
            if(locked){recordPreviewArt.texture=null;return;}
            SetRecordPicture(recordPreviewArt,string.IsNullOrEmpty(story.preview)?story.pages[0].image:story.preview,1,1,552,192);
        }
        void OpenRecordStory(int index)
        {
            if(recordBusy||index<0||index>=StoryCatalog.stories.Length||!RecordStoryUnlocked(StoryCatalog.stories[index]))return;
            recordSelectedIndex=recordStoryIndex=index;recordStoryPage=0;recordList.gameObject.SetActive(false);recordDetail.gameObject.SetActive(true);
            DrawRecordStory();recordBusy=true;recordNext.interactable=false;recordTransition=StartCoroutine(FadeRecordStart());
        }
        IEnumerator FadeRecordStart(){recordPageFade.alpha=0;yield return FadeRecordPage(0,1,.38f);recordBusy=false;recordNext.interactable=true;recordTransition=null;}
        IEnumerator FadeRecordPage(float from,float to,float seconds)
        {
            float time=0;while(time<seconds){time+=Time.unscaledDeltaTime;recordPageFade.alpha=Mathf.SmoothStep(from,to,Mathf.Clamp01(time/seconds));yield return null;}recordPageFade.alpha=to;
        }
        void TurnRecordStory(int direction)
        {
            if(!RecordDetailShowing||recordBusy)return;recordBusy=true;recordNext.interactable=false;recordTransition=StartCoroutine(TurnRecordPage(direction));
        }
        IEnumerator TurnRecordPage(int direction)
        {
            yield return FadeRecordPage(1,0,.28f);
            var story=StoryCatalog.stories[recordStoryIndex];
            if(direction>0&&recordStoryPage+1>=story.pages.Length)
            {Profile.MarkStorySeen(story.readKey);recordTransition=null;ShowRecordList();yield break;}
            recordStoryPage=Mathf.Clamp(recordStoryPage+direction,0,story.pages.Length-1);DrawRecordStory();
            yield return FadeRecordPage(0,1,.38f);recordBusy=false;recordNext.interactable=true;recordTransition=null;
        }
        void DrawRecordStory()
        {
            var story=StoryCatalog.stories[recordStoryIndex];var page=story.pages[recordStoryPage];recordPlayingTitle.text=Deviant.UI.Theme.ApprovedInkText.Display(story.title+" / "+(story.subtitle??""));
            string[] paragraphs=page.paragraphs;
            if(!string.IsNullOrEmpty(page.note)){var note=RecordNoteData();paragraphs=page.note=="lines"?note.lines:page.note=="afterFirstRun"?note.afterFirstRun:note.afterFirstInk;}
            recordBody.text=page.text??string.Join("\n",paragraphs??Array.Empty<string>());
            recordPageLabel.text=(recordStoryPage+1)+" / "+story.pages.Length+" 페이지";
            SetRecordPicture(recordIllustration,page.image,0,story.cutscene?0:60,960,story.cutscene?540:420);
        }
        void SetRecordPicture(RawImage image,string path,float x,float y,float w,float h)
        {
            var texture=string.IsNullOrEmpty(path)?null:Resources.Load<Texture2D>(path);var crop=texture?new Rect(0,0,texture.width,texture.height):default;
            image.texture=texture;image.enabled=texture;
            if(!texture)return;
            image.uvRect=new Rect(crop.x/texture.width,crop.y/texture.height,crop.width/texture.width,crop.height/texture.height);
            float scale=Mathf.Min(w/crop.width,h/crop.height);image.rectTransform.sizeDelta=new Vector2(Mathf.Round(crop.width*scale),Mathf.Round(crop.height*scale));
            image.rectTransform.anchoredPosition=new Vector2(Mathf.Round(x+(w-crop.width*scale)/2),-Mathf.Round(y+(h-crop.height*scale)/2));
        }
        void TickRecordInput()
        {
            var input=Controls;if(!input)return;
            if(TakeBack()){if(RecordDetailShowing)ShowRecordList();else CloseHubPanel();return;}
            // This archive owns menu input directly (the automatic uGUI module is disabled here).
            // World Interact may be rebound; opening/confirming a record remains fixed to F / gamepad South.
            input.Take(GameAction.Interact);bool next=input.Take(GameAction.Advance);input.Take(GameAction.Submit);
            bool confirm=LiveSubmitPressed;
            if(Time.frameCount!=recordInputFrame)
            {
                if(RecordDetailShowing){if(confirm||next)TurnRecordStory(1);}
                else
                {
                    var keyboard=UnityEngine.InputSystem.Keyboard.current;
                    float y=keyboard?.upArrowKey.isPressed==true?1:keyboard?.downArrowKey.isPressed==true?-1:input.UsingGamepad?input.Read(GameAction.Navigate).Y:0;
                    int sign=y>.5f?-1:y<-.5f?1:0;
                    if(sign!=0&&(sign!=recordMoveSign||Time.unscaledTime>=recordMoveRepeat)){SelectRecordStory(recordSelectedIndex+sign);recordMoveRepeat=Time.unscaledTime+(sign==recordMoveSign?.12f:.35f);}recordMoveSign=sign;
                    if(confirm)OpenRecordStory(recordSelectedIndex);
                }
            }
            input.SuppressGameplay();
        }
        void RefreshRecord(){if(bannerGroup)bannerGroup.alpha=0;if(!RecordDetailShowing)RefreshRecordList();LayoutRecordActions();}
    }
}
