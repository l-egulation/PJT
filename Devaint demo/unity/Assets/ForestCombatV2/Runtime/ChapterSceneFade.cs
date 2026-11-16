using System;
using System.Collections;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Control.Contracts;
using Deviant.UI.Theme;
using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ForestCombatV2
{
    // The loading between scenes (user decision 2026-11-07, replacing the 2026-11-04 notebook): on a dark screen a round
    // window opens around the hero, who runs on the spot in the look they had when the scene was left (LoadingHeroRun)
    // while the place being entered slides past inside the window (ChapterLoadingView); the place's name is written under
    // it a letter at a time. That part always plays to the end before anything loads (playtest 2026-11-05: a load that
    // stalls the main thread froze it halfway); only then the next scene loads, with 「.」「..」「...」 going round under the
    // name for as long as it takes. It fades in over the old scene and out over the new one.
    // A scene loaded through LoadSceneBehind reads in the background but switches in only once the dots have gone round
    // at least once, on 「...」: switching in and building the new world stalls the main thread in one go (2.6 s into
    // chapter 2, measured 2026-11-06), and the dots used to stay frozen on 「.」 for the whole of it.
    // Leaving one place for another (2026-11-13), the window opens on the place being left (read from the active scene)
    // and the place being entered sweeps in behind a seam during the same intro (ChapterLoadingView.SeamStart).
    public sealed class ChapterSceneFade : MonoBehaviour
    {
        static ChapterSceneFade active;
        CanvasGroup group;
        ChapterLoadingView view;
        Text label, dots;
        string labelText;
        bool loading,accepting,failed;
        GameObject failurePanel;
        readonly Dictionary<CombatArena,IControlScope> loadingScopes=new Dictionary<CombatArena,IControlScope>();
        readonly Dictionary<CombatArena,IControlLease> failurePauses=new Dictionary<CombatArena,IControlLease>();
        float nextScopeScan;
        public static bool ShowingFailure=>active&&active.failed;
        float started=-1,loadingSince;
        AsyncOperation pending;
        public static bool Busy=>active;
        // What the screen shows, for checks: the place and the hero's form (null when no player was found).
        public static LoadingPlace? ShowingPlace=>active?active.place:(LoadingPlace?)null;
        public static string ShowingHeroForm=>active?active.hero?.Form:null;
        // The place the window opens on before sweeping to ShowingPlace; null when it shows ShowingPlace alone.
        public static LoadingPlace? ShowingLeaving=>active?active.view?.Leaving:null;
        LoadingPlace place;
        LoadingHeroRun hero;
        // SceneManager.LoadSceneAsync for the load action of Load: held until the dots have gone round once.
        public static AsyncOperation LoadSceneBehind(string path)
        {
            var op=SceneManager.LoadSceneAsync(path,LoadSceneMode.Single);
            if(op==null)throw new InvalidOperationException("Scene load did not start: "+path);
            if(op!=null&&active&&active.accepting&&active.pending==null){op.allowSceneActivation=false;active.pending=op;}
            return op;
        }
        // Goes to `place`: the hero is read from the scene being left now, before anything changes.
        public static void Load(LoadingPlace place, Action load, Func<bool> ready = null)
        {
            // A scene change asked for while one is already showing still happens, under the screen already up.
            if(active){active.TryLoad(load);return;}
            var go=new GameObject("Chapter scene fade");DontDestroyOnLoad(go);
            active=go.AddComponent<ChapterSceneFade>();
            active.place=place;
            active.labelText=ChapterLoadingView.Label(place);active.hero=LoadingHeroRun.Capture();
            active.view=ChapterLoadingView.For(place,PlaceOf(SceneManager.GetActiveScene().path));
            active.StartCoroutine(active.Transition(load, ready));
        }
        // Chapter2EntryBridge.ScenePath (the Chapter2 assembly builds on this one, not the other way round).
        public const string Chapter2Scene = "Assets/Chapter2/Scenes/Chapter2Procedural.unity";
        // The place a scene draws, for the window's opening view; null for scenes outside the run (title, test scenes).
        public static LoadingPlace? PlaceOf(string scenePath)
        {
            if(scenePath==Chapter1RunHost.VillageScene)return LoadingPlace.Village;
            if(scenePath==Chapter1RunHost.ChapterScene)return LoadingPlace.Chapter1;
            if(scenePath==Chapter2Scene)return LoadingPlace.Chapter2;
            return null;
        }
        const float WriteSeconds = .5f, WriteHold = .3f, DotSeconds = .3f, DotRounds = 1;
        // Settling after ready: this many frames in a row under SmoothFrameSeconds, giving up after SettleMaxSeconds.
        const int SmoothFrames = 6; const float SmoothFrameSeconds = 1 / 30f, SettleMaxSeconds = 2, PresentationWaitSeconds = 3;
        int smoothFrames;
        // The window's centre on the 960x540 stage; the name and the dots stand under it.
        const float WindowCentreX = 480, WindowCentreY = 240;
        static float NameY => WindowCentreY + ChapterLoadingView.Radius * ChapterLoadingView.Scale + 44;
        static readonly Color Desk = new Color32(15, 16, 17, 255), Paper = new Color32(245, 245, 243, 255);

        IEnumerator Transition(Action load, Func<bool> ready)
        {
            var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32767;canvas.pixelPerfect=true;
            var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(960,540);scaler.matchWidthOrHeight=.5f;
            gameObject.AddComponent<GraphicRaycaster>();group=gameObject.AddComponent<CanvasGroup>();group.alpha=0;
            var stage=Box("Stage",transform,0,0,960,540,Color.clear);
            var full=Box("Desk",transform,0,0,0,0,Desk);full.anchorMin=Vector2.zero;full.anchorMax=Vector2.one;full.offsetMin=full.offsetMax=Vector2.zero;full.SetAsFirstSibling();
            stage.SetAsLastSibling();stage.anchorMin=stage.anchorMax=new Vector2(.5f,.5f);stage.pivot=new Vector2(.5f,.5f);stage.anchoredPosition=Vector2.zero;
            view.Build(stage,new Vector2(WindowCentreX,WindowCentreY),hero);
            if(hero==null)Debug.LogWarning("Loading: no player to draw; the window shows the place alone.");
            var font=ApprovedTheme.Font??Resources.Load<Font>("PixelInventory/Fonts/Galmuri11-Bold")??Resources.Load<Font>("PixelInventory/Fonts/Galmuri11");
            full.GetComponent<Image>().raycastTarget=true;BuildFailure(stage,font);CaptureInputScopes();
            label=Line("Place",stage,font,NameY);dots=Line("Loading dots",stage,font,NameY+32);
            view.Tick(0);
            yield return Fade(0,1);
            // The window and the name first, whole, while the old scene still stands underneath.
            started=Time.unscaledTime;
            while(Time.unscaledTime<started+IntroSeconds)yield return null;
            // Then the next scene loads (callers use LoadSceneAsync); until it is active the old scene's world still
            // answers ready, so wait for the switch first. The dots go round meanwhile.
            float wait=0;
            while(true)
            {
                loading=true;loadingSince=Time.unscaledTime;var before=SceneManager.GetActiveScene();
                if(!TryLoad(load)){while(failed)yield return null;continue;}
                yield return null;
                if(pending!=null)
                {
                    while(pending.progress<.9f||DotStep<3*DotRounds-1||DotStep%3!=2)yield return null;
                    pending.allowSceneActivation=true;pending=null;
                }
                // A slow load remains loading; only an observed exception opens the failure choices.
                while(SceneManager.GetActiveScene()==before)yield return null;
                break;
            }
            // Once activated, retry only the destination's readiness check. Loading it again would discard
            // its player and the bridge's already-consumed carry (or bind chapter 1 to a different run).
            while(true)
            {
                while(!IsReady(ready)&&!failed)yield return null;
                if(!failed)break;
                while(failed)yield return null;
                loading=true;loadingSince=Time.unscaledTime;
            }
            // The play GUI binds a few frames after the world is ready (InventoryGameBinding waits for the player); until
            // it sets ExternalPresentation the old IMGUI HUD draws (key line, minimap, HP bars, developer buttons), which
            // showed for a moment as the screen lifted (playtest video 2026-11-06). Keep the screen up until the GUI
            // owns the screen; a scene without that GUI goes on after PresentationWaitSeconds.
            for(float held=0;held<PresentationWaitSeconds&&wait<20;held+=Time.unscaledDeltaTime,wait+=Time.unscaledDeltaTime)
            {var world=FindAnyObjectByType<ForestCombatWorld>();if(!world||world.ExternalPresentation)break;yield return null;}
            // The new world's first frames are heavy (2026-11-06, into chapter 1: 0.27 s, then 0.19 s right after it
            // answered ready), so the screen stays up, dots going, until a few frames in a row run smoothly.
            for(float settled=0;settled<SettleMaxSeconds;)
            {
                yield return null;
                float dt=Time.unscaledDeltaTime;settled+=dt;smoothFrames=dt<SmoothFrameSeconds?smoothFrames+1:0;
                if(smoothFrames>=SmoothFrames)break;
            }
            loading=false;dots.text="";
            yield return Fade(1,0);
            Destroy(gameObject);
        }

        // The window opens, then the name is written and held.
        static float IntroSeconds => ChapterLoadingView.OpenSeconds + WriteSeconds + WriteHold;
        // How many dot steps have passed since loading began (0: 「.」, 1: 「..」, 2: 「...」, 3: 「.」 again).
        int DotStep=>Mathf.FloorToInt((Time.unscaledTime-loadingSince)/DotSeconds);
        void Update()
        {
            CaptureInputScopes();
            if(failed)
            {
                var keyboard=Keyboard.current;
                if(keyboard!=null&&keyboard.fKey.wasPressedThisFrame)Retry();
                else if(keyboard!=null&&keyboard.gKey.wasPressedThisFrame)ToTitle();
                return;
            }
            if(view==null||!label)return;
            if(started<0){view.Tick(0);return;}
            float t=Time.unscaledTime-started;
            view.Tick(t);
            float writing=t-ChapterLoadingView.OpenSeconds;
            label.text=writing<=0||labelText.Length==0?"":labelText.Substring(0,Mathf.CeilToInt(Mathf.Clamp01(writing/WriteSeconds)*labelText.Length));
            if(loading)dots.text=new string('.',1+DotStep%3);
        }

        bool TryLoad(Action load)
        {
            accepting=true;
            try{if(load==null)throw new InvalidOperationException("No scene load action.");load();return true;}
            catch(Exception error){ShowFailure(error);return false;}
            finally{accepting=false;}
        }
        bool IsReady(Func<bool> ready)
        {
            try{var world=FindAnyObjectByType<ForestCombatWorld>();return ready!=null?ready():world&&world.Ready;}
            catch(Exception error){ShowFailure(error);return false;}
        }
        void ShowFailure(Exception error)
        {
            loading=false;failed=true;CaptureInputScopes(true);if(dots)dots.text="";if(failurePanel){failurePanel.transform.SetAsLastSibling();failurePanel.SetActive(true);}
            Debug.LogWarning("Scene loading failed: "+error.Message);
        }
        void Retry(){if(!failed)return;failed=false;ReleaseFailurePauses();if(failurePanel)failurePanel.SetActive(false);}
        void ToTitle()
        {
            try{SceneManager.LoadScene(Chapter1RunHost.TitleScene,LoadSceneMode.Single);Destroy(gameObject);}
            catch(Exception error){ShowFailure(error);}
        }
        void CaptureInputScopes(bool force=false)
        {
            if(!force&&Time.unscaledTime<nextScopeScan)return;nextScopeScan=Time.unscaledTime+.1f;
            foreach(var arena in FindObjectsByType<CombatArena>(FindObjectsSortMode.None))
            {
                if(!arena)continue;
                if(!loadingScopes.TryGetValue(arena,out var scope))
                {
                    scope=arena.Controls.OpenScope("approved scene loading");
                    scope.BlockInput(InputChannels.Combat|InputChannels.Interaction|InputChannels.Dialogue|InputChannels.Navigation);
                    loadingScopes.Add(arena,scope);
                }
                // Loading normally only blocks input. A confirmed failure must also freeze the world
                // behind its choices, even if the caller already released its transition pause.
                if(failed&&!failurePauses.ContainsKey(arena))failurePauses.Add(arena,scope.PauseSimulation());
            }
        }
        void ReleaseFailurePauses()
        {
            foreach(var pause in failurePauses.Values)pause.Dispose();
            failurePauses.Clear();
        }
        void BuildFailure(RectTransform stage,Font font)
        {
            var panel=Box("Loading failure",stage,0,0,960,540,new Color32(32,36,41,237));failurePanel=panel.gameObject;
            panel.GetComponent<Image>().raycastTarget=true;
            // Figma loading-failed: the centred 480px content block uses left-aligned text, not a full-width title.
            var content=Box("Failure content",panel,240,163,480,214,Color.clear);
            FailureText("Failure heading",content,font,"장면을 불러오지 못했어요.",24,22.565f,432,30.45f,21,new Color32(238,233,223,255));
            var body=FailureText("Failure description",content,font,"진행 상황은 유지됩니다.\n다시 시도하거나 타이틀로 돌아갈 수 있습니다.",24,69.515f,432,56.925f,17,new Color32(179,187,196,255));
            body.alignment=TextAnchor.UpperLeft;body.lineSpacing=28.4625f/(17*font.lineHeight/font.fontSize);
            Box("Failure separator",content,24,144.435f,432,.5f,new Color32(168,175,182,64));
            FailureButton(content,font,"F","다시 시도",21.86f,160.685f,true,Retry);
            FailureButton(content,font,null,"타이틀로",139.665f,161.435f,false,ToTitle);
            failurePanel.SetActive(false);
        }
        static Text FailureText(string name,RectTransform parent,Font font,string value,float x,float y,float w,float h,int size,Color color)
        {
            var text=Line(name,parent,font,18);text.rectTransform.anchoredPosition=new Vector2(x,-y);text.rectTransform.sizeDelta=new Vector2(w,h);
            text.text=value;text.fontSize=size;text.color=color;text.alignment=TextAnchor.MiddleLeft;
            text.horizontalOverflow=HorizontalWrapMode.Overflow;text.verticalOverflow=VerticalWrapMode.Overflow;return text;
        }
        static void FailureButton(RectTransform parent,Font font,string key,string caption,float x,float y,bool selected,Action action)
        {
            float width=selected?90:81.332f,height=selected?31.5f:30;
            var rect=Box(caption,parent,x,y,width,height,Color.clear);rect.GetComponent<Image>().raycastTarget=true;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();button.onClick.AddListener(()=>action());
            var color=selected?new Color32(250,246,233,255):new Color32(186,194,201,255);
            float captionX=selected?24.15f:23;
            var label=FailureText("Label",rect,font,caption,captionX,4.2f,selected?66.1f:58.7f,22.84f,selected?20:19,color);
            // Unity rounds the CSS font size to an integer; measure the actual caption so its last syllable stays visible.
            float captionWidth=Mathf.Max(label.rectTransform.rect.width,Mathf.Ceil(label.preferredWidth));
            label.rectTransform.sizeDelta=new Vector2(captionWidth,22.84f);
            rect.sizeDelta=new Vector2(Mathf.Max(width,captionX+captionWidth),height);
            var wash=Box("Selection wash",rect,0,0,selected?85.664f:81.332f,30,new Color(1,1,1,selected?.5f:.1f)).GetComponent<Image>();
            wash.sprite=ApprovedTheme.Sprite("common/maintenance-palette-v1__assets__selection-wash-v1");wash.transform.SetAsFirstSibling();
            if(key==null)return;
            var cap=Box("Keycap",rect,0,3.015f,19.934f,25.988f,Color.clear);var border=new Color32(186,200,206,255);
            Box("Top",cap,1,0,17.934f,.5f,border);Box("Bottom",cap,1,25.488f,17.934f,.5f,border);
            Box("Left",cap,0,1,.5f,23.988f,border);Box("Right",cap,19.434f,1,.5f,23.988f,border);
            var keyText=FailureText("Key",cap,font,key,0,0,19.934f,25.988f,19,color);
            keyText.alignment=TextAnchor.MiddleCenter;keyText.alignByGeometry=true;
        }

        static Text Line(string name,RectTransform stage,Font font,float centreY)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));go.transform.SetParent(stage,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(0,-(centreY-18));rect.sizeDelta=new Vector2(960,36);
            var text=go.GetComponent<Text>();
            if(font){text.font=font;if(!ApprovedTheme.ConfigureFontSampling(font)&&font.material&&font.material.mainTexture)font.material.mainTexture.filterMode=FilterMode.Point;}
            text.fontSize=24;text.color=Paper;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;text.text="";
            return text;
        }
        static RectTransform Box(string name,Transform parent,float x,float y,float w,float h,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);
            var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;
            return rect;
        }
        // A frame counts at most 1/30 s, so a stalled frame never skips the fade.
        IEnumerator Fade(float from,float to){for(float t=0;t<.25f;t+=Mathf.Min(Time.unscaledDeltaTime,1/30f)){group.alpha=Mathf.Lerp(from,to,t/.25f);yield return null;}group.alpha=to;}
        void OnDestroy(){ReleaseFailurePauses();foreach(var scope in loadingScopes.Values)scope.Dispose();loadingScopes.Clear();view?.Dispose();if(active==this)active=null;}
    }
}
