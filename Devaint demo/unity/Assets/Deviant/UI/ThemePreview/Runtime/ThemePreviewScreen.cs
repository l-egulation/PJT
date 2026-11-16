using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using TMPro;

namespace Deviant.ThemePreview
{
    [DisallowMultipleComponent]
    public sealed partial class ThemePreviewScreen : MonoBehaviour
    {
        public PreviewFocus Focus { get; private set; } = new PreviewFocus();
        public int Frames { get; private set; }
        public int LastKeyFrame { get; private set; }
        public string Error { get; private set; }
        public int ScrollLine { get; private set; }
        PreviewAssets art;
        RectTransform canvasRoot, page;
        bool oldBackground;
        GameObject eventRoot;
        readonly string[] colors = { "red", "yellow", "green", "blue", "purple", "neutral" };
        readonly string[] colorNames = { "빨강", "노랑", "초록", "파랑", "보라", "무색" };
        readonly int[] glyphs = { 7, 0, 1, 9, 3 };
        readonly Color paper = new Color32(243, 239, 229, 255), muted = new Color32(174, 182, 192, 255);
        public static string ConfigPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../../outputs/ui-theme-preview/config.json"));
        void Start()
        {
            oldBackground = Application.runInBackground; Application.runInBackground = true;
            try
            {
                var config = JsonUtility.FromJson<PreviewConfig>(File.ReadAllText(ConfigPath));
                art = new PreviewAssets(config);
                var root = new GameObject("ThemePreviewCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
                root.transform.SetParent(transform, false); canvasRoot = root.GetComponent<RectTransform>();
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.pixelPerfect = true;
                var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
                eventRoot = new GameObject("ThemePreviewEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventRoot.transform.SetParent(transform, false);
                var module = eventRoot.GetComponent<InputSystemUIInputModule>(); module.AssignDefaultActions();
                // Mouse is supported; keyboard is handled once by the preview focus controller.
                module.move = null; module.submit = null; module.cancel = null;
                if(config.extendedScreens) ResetExtended(); else if(config.coreScreens) ResetCore(); else Render();
            }
            catch (Exception ex) { Error = ex.ToString(); Debug.LogException(ex); }
        }
        void Update()
        {
            Frames++;
            if (art == null || Error != null) return;
            TickExtended();
            var k = Keyboard.current; if (k == null) return;
            if(ExtendedMode&&ExtendedKey=="rebind"){foreach(var key in k.allKeys)if(key.wasPressedThisFrame){HandleExtended(key.keyCode==Key.Escape?"Esc":key.displayName);return;}return;}
            if (CoreMode && RenameOpen) { UpdateRename(k); return; }
            if (k.escapeKey.wasPressedThisFrame) Handle("Esc");
            else if (k.qKey.wasPressedThisFrame) Handle("Q");
            else if (k.eKey.wasPressedThisFrame) Handle("E");
            else if (k.leftArrowKey.wasPressedThisFrame) Handle("Left");
            else if (k.rightArrowKey.wasPressedThisFrame) Handle("Right");
            else if (k.upArrowKey.wasPressedThisFrame) Handle("Up");
            else if (k.downArrowKey.wasPressedThisFrame) Handle("Down");
            else if (k.fKey.wasPressedThisFrame) Handle("F");
            else if (ExtendedMode&&k.equalsKey.wasPressedThisFrame) Handle("+");
            else if (ExtendedMode&&k.minusKey.wasPressedThisFrame) Handle("-");
            else if (ExtendedMode&&k.cKey.wasPressedThisFrame) Handle("C");
            else if (ExtendedMode&&k.vKey.wasPressedThisFrame) Handle("V");
            else if (ExtendedMode&&k.homeKey.wasPressedThisFrame) Handle("Home");
            else if (ExtendedMode&&k.mKey.wasPressedThisFrame) Handle("M");
            else if (k.gKey.wasPressedThisFrame) Handle("G");
            else if (k.wKey.wasPressedThisFrame) Handle("W");
            else if (k.sKey.wasPressedThisFrame) Handle("S");
        }
        public void Handle(string key)
        {
            if(ExtendedMode){HandleExtended(key);return;}
            if(CoreMode){HandleCore(key);if(coreClosed&&extendedCoreReturn!=null){var back=extendedCoreReturn;extendedCoreReturn=null;if(back=="commerce-finish"){FinishCommerceReceipt();return;}int pick=ExtendedPick;OpenExtended(back,false);ExtendedPick=pick;selectedCandidate=extendedCoreCandidate;Render();}return;}
            LastKeyFrame = Frames;
            if (key == "Esc") Focus.Back();
            else if (key == "F") Focus.Confirm();
            else if (!Focus.Closed)
            {
                if (key == "Q") { Focus.SwitchTab(-1); ScrollLine = 0; }
                if (key == "E") { Focus.SwitchTab(1); ScrollLine = 0; }
                if (key == "Right" || key == "Down") Focus.Move(1);
                if (key == "Left" || key == "Up") Focus.Move(-1);
                if (Focus.Tab == 2 && key == "W") ScrollLine = Mathf.Max(0, ScrollLine - 1);
                if (Focus.Tab == 2 && key == "S") ScrollLine = Mathf.Min(5, ScrollLine + 1);
            }
            Render();
        }
        RectTransform Box(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform)); var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h); return rt;
        }
        UnityEngine.UI.Image Fill(RectTransform parent, Color color)
        {
            var im = parent.gameObject.AddComponent<UnityEngine.UI.Image>(); im.color = color; im.raycastTarget = false; return im;
        }
        TextMeshProUGUI Text(Transform parent, string name, string value, float x, float y, float w, float h, float size, Color? color = null)
        {
            var rt = Box(name, parent, x, y, w, h); var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = art.Font; t.text = value; t.fontSize = size; t.color = color ?? paper; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Truncate;
            return t;
        }
        void Art(Transform parent, string id, float x, float y, float w, float h, float alpha = 1)
        {
            var viewport = Box(id, parent, x, y, w, h); viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var s = art.Find(id); var v = s.view; var r = s.rect ?? v;
            var inner = Box("SourcePixels", viewport, (r[0]-v[0])*w/v[2], (r[1]-v[1])*h/v[3], r[2]*w/v[2], r[3]*h/v[3]);
            var img = inner.gameObject.AddComponent<UnityEngine.UI.Image>(); img.sprite = art.Sprite(id); img.raycastTarget = false; img.color = new Color(1,1,1,alpha);
        }
        void Wash(Transform parent, float x, float y, float w, float h) => Art(parent, "common/maintenance-palette-v1__assets__selection-wash-v1", x, y, w, h, .48f);
        void LabelWash(TextMeshProUGUI label, float paddingX = 14, float paddingY = 10)
        {
            // A control's hit area is deliberately larger than its visible label.
            // Fit the approved brush to the text, never to that hit area.
            var rt = label.rectTransform;
            var preferred = label.GetPreferredValues(label.text, rt.rect.width, Mathf.Infinity);
            float width = Mathf.Min(rt.rect.width, preferred.x);
            float height = Mathf.Min(rt.rect.height, preferred.y);
            int index = rt.GetSiblingIndex();
            float alignOffset=(label.alignment==TextAlignmentOptions.Top||label.alignment==TextAlignmentOptions.Center)?(rt.rect.width-width)/2:0;
            Wash(rt.parent, rt.anchoredPosition.x+alignOffset-paddingX, -rt.anchoredPosition.y-paddingY,
                width+paddingX*2, height+paddingY*2);
            rt.parent.GetChild(rt.parent.childCount-1).SetSiblingIndex(index);
        }
        void Keycap(Transform parent,string key,float x,float y,float width=35,float height=36,float size=22)
        {
            var frame=Box("Keycap-"+key,parent,x,y,width,height);
            var edge=new Color32(147,156,166,255);
            float scale=Mathf.Min(UnityEngine.Screen.width/1920f,UnityEngine.Screen.height/1080f);
            float stroke=Mathf.Max(1,1/Mathf.Max(.1f,scale));
            Fill(Box("Top",frame,0,0,width,stroke),edge);Fill(Box("Bottom",frame,0,height-stroke,width,stroke),edge);
            Fill(Box("Left",frame,0,0,stroke,height),edge);Fill(Box("Right",frame,width-stroke,0,stroke,height),edge);
            var text=Text(frame,"Key",key,0,0,width,height,size,muted);
            text.alignment=TextAlignmentOptions.Center;text.textWrappingMode=TextWrappingModes.NoWrap;
            text.margin=Vector4.zero;text.extraPadding=false;text.enableAutoSizing=false;
        }
        void Button(Transform parent, string label, float x, float y, float width, bool selected, Action click, bool enabled = true)
        {
            var host=Box(label,parent,x,y,width,67);
            var t=Text(host,"Label",label,selected&&enabled?74:26,14,width-(selected&&enabled?74:26),45,29,enabled?paper:muted);
            var tw=t.GetPreferredValues(label).x;
            if(selected&&enabled){Wash(host,0,4,Mathf.Min(width,tw+100),58);host.GetChild(host.childCount-1).SetAsFirstSibling();Keycap(host,"F",26,16,35,36,24);}
            var hit=host.gameObject.AddComponent<UnityEngine.UI.Image>();hit.color=Color.clear;
            var button=host.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=hit;button.interactable=enabled;
            button.transition=UnityEngine.UI.Selectable.Transition.None;button.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None};
            button.onClick.AddListener(()=>click());
        }
        void CoreBackdrop()
        {
            var texture=art.Backdrop();var image=Box("ApprovedVillage",page,0,0,1920,1080).gameObject.AddComponent<UnityEngine.UI.RawImage>();
            image.texture=texture;image.raycastTarget=false;
            float aspect=(float)texture.width/texture.height,target=1920f/1080;
            image.uvRect=aspect>target?new Rect((1-target/aspect)/2,0,target/aspect,1):new Rect(0,(1-aspect/target)/2,1,aspect/target);
            // HTML world: rgba(36,39,43,.96) to rgba(36,39,43,.95).
            // Backdrop contains the approved sRGB tint; no second overlay.
        }
        public void ResetFoundation(){ExtendedMode=false;CoreMode=false;Focus=new PreviewFocus();ScrollLine=0;Render();}
        public void Render()
        {
            if (canvasRoot == null) return;
            if(page != null) DestroyImmediate(page.gameObject);
            page=Box("PreviewPage",canvasRoot,0,0,1920,1080);
            Fill(Box("Graphite",page,0,0,1920,1080),new Color32(43,47,51,255));
            if(ExtendedMode){DrawExtended();return;}
            if(CoreMode){CoreBackdrop();DrawCore();return;}
            if(Focus.Closed)
            {
                Text(page,"Closed","테스트 화면 닫힘",650,400,700,80,45);
                Button(page,"다시 열기",720,525,460,true,()=>Handle("F")); return;
            }
            var tabs=new[]{"프레임","팔레트","글자·입력"};
            for(int i=0;i<tabs.Length;i++)
            {
                int tab=i;float x=140+i*300;
                var t=Text(page,"Tab"+i,tabs[i],x,77,260,70,Focus.Tab==i?43:36,Focus.Tab==i?paper:muted);
                if(Focus.Tab==i)LabelWash(t,14,10);
                t.raycastTarget=true;var b=t.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=t;b.transition=UnityEngine.UI.Selectable.Transition.None;b.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None};b.onClick.AddListener(()=>{Focus.SwitchTab(tab-Focus.Tab);Render();});
            }
            if(Focus.Tab==0) DrawFrames(); else if(Focus.Tab==1) DrawPalettes(); else DrawTypography();
            DrawKeyboardFooter();
        }
        void DrawKeyboardFooter()
        {
            Rule(130,983,1660);float cursor=130;
            string[] keys={"방향키","F","Esc","Q / E","W / S"},captions={"이동","선택 / 실행","돌아가기","탭 전환","설명 스크롤"};
            float[] widths={71.352f,34,49.242f,63.125f,67.703f},advances={181.828f,200.086f,195.484f,196.711f,220};
            for(int i=0;i<keys.Length;i++){Keycap(page,keys[i],cursor,1004,widths[i],41.195f);Text(page,"Help"+i,captions[i],cursor+widths[i]+15,1010,150,38,23,muted);cursor+=advances[i];}
        }
        void Frame(float x,float y,float w,float h)
        {
            Deviant.UI.Theme.ApprovedTheme.CreateFrame(page,x,y,w,h,2);
        }
        void Speech(float x,float y,float w,float h)
        {
            var spec=art.Find("common/sketch-v3__assets__speech-frame-v1");var texture=art.Texture(spec.file);
            float[] xs={0,220,900,1080,1763,1983},ys={0,180,570,793};
            float[] widths={44,(w-124)/2,36,(w-124)/2,44},heights={36,h-80.6f,44.6f};
            var parent=Box("FixedTailSpeech",page,x,y,w,h);float dy=0;
            for(int row=0;row<3;row++) {float dx=0;for(int col=0;col<5;col++)
            {var cell=Box("Cell"+row+col,parent,dx,dy,widths[col],heights[row]);var im=cell.gameObject.AddComponent<UnityEngine.UI.Image>();
             im.sprite=art.Slice(texture,new Rect(xs[col],ys[row],xs[col+1]-xs[col],ys[row+1]-ys[row]));im.raycastTarget=false;dx+=widths[col];}dy+=heights[row];}
        }
        void DrawFrames()
        {
            Text(page,"FrameIntro","모서리는 고정하고 가운데만 늘립니다.",140,186,1400,60,34);
            Frame(140,280,720,240); Frame(990,280,790,240);
            Text(page,"Small","기본 프레임 / 720 × 240",200,350,620,60,32);
            Text(page,"Wide","넓은 프레임 / 790 × 240",1050,350,650,60,32);
            Speech(140,615,720,220);Speech(990,615,790,220);
            Text(page,"Speech1","[ F ]  상자 열기",210,675,550,65,34);
            Text(page,"Speech2","[ F ]  이야기 듣기",1070,675,550,65,34);
            Text(page,"SpeechNote","꼬리와 모서리는 두 크기에서 동일합니다.",140,873,1500,60,27,muted);
        }
        void Palette(Transform parent,int slot,float x,float y,bool selected)
        {
            float w=selected?176:140.8f,h=w*155/220;
            var rt=Box("Palette"+slot,parent,x+(176-w)/2,y+(124-h)/2,w,h);
            float alpha=Focus.Depth==2?.48f:selected?1:.65f;
            Art(rt,"palette/bases/"+colors[slot],0,0,w,h,alpha);
            var applied=Focus.Applied[slot];if(applied>=0)
            {
                Art(rt,"palette/paint/"+colors[applied],0,0,w,h,alpha);
                Art(rt,"ui-assets-v1/ink-symbol-"+glyphs[applied],70*w/220,38*h/155,80*w/220,80*h/155,alpha);
            }
            var label=Text(parent,"SlotName"+slot,applied<0?"빈 칸":colorNames[applied]+" 잉크",x,y+150,185,55,29,selected?paper:muted);
            label.alignment=TextAlignmentOptions.Top;
            if(selected&&Focus.Depth==1)LabelWash(label,14,10);
        }
        void InkBottle(int index, float x, float y, float height, float alpha)
        {
            var ink=art.Package.inks.First(i=>i.color==colors[index]&&i.effect=="ui-assets-v1/ink-symbol-"+glyphs[index]);
            var v=ink.view;float scale=height/v[3], width=v[2]*scale;
            Art(page,ink.bottle,x,y,width,height,alpha);
            foreach(var pair in new[]{(ink.effect,ink.label),(ink.condition,ink.badge)})
            {
                var mark=pair.Item2;
                Art(page,pair.Item1,x+(mark.center[0]-v[0]-mark.size/2)*scale,y+(mark.center[1]-v[1]-mark.size/2)*scale,mark.size*scale,mark.size*scale,alpha);
            }
        }
        void DrawPalettes()
        {
            Text(page,"PaletteState",Focus.Depth==0?"F로 팔레트 선택 시작":Focus.Depth==1?"슬롯 선택 · F로 잉크 선택":"잉크 선택 · F로 임시 적용",140,190,1600,60,34,new Color32(225,207,133,255));
            for(int i=0;i<6;i++)Palette(page,i,140+i*275,300,Focus.Depth>0&&Focus.Slot==i);
            Fill(Box("Separation",page,140,565,1640,1),new Color32(91,99,105,255));
            Text(page,"InkHeader","적용할 잉크",140,610,500,55,32,Focus.Depth==2?paper:muted);
            for(int i=0;i<5;i++)
            {
                bool selected=Focus.Depth==2&&Focus.Ink==i;float size=selected?140:112;float x=140+i*190;
                InkBottle(i,x,680,size,Focus.Depth==2?(selected?1:.6f):.55f);
                Text(page,"InkName"+i,colorNames[i],x,835,165,48,28,selected?paper:muted);
            }
            Text(page,"LayerNote","슬롯 바탕 → 잉크 칠 → 효과 문양\n다른 색과 같은 색, 빈 칸을 함께 비교합니다.",1180,665,600,140,29,muted);
            Button(page,"잠긴 항목",1240,835,370,false,()=>{},false);
        }
        void DrawTypography()
        {
            Text(page,"FontHeading","한글 · 숫자 · 긴 효과 설명",140,190,1600,60,36);
            Frame(140,285,1640,585);
            Text(page,"FontSample","가방   제작   장비    +15%    63%    1.5초",210,335,1490,70,40);
            Text(page,"Effect","<color=#ACD4E0>기본 공격 시</color> <color=#B4D38D>3초</color> 동안 <color=#B4D38D>이동속도가 15% 증가</color>합니다.",210,430,1490,75,34);
            string[] lines={"1. 작은 화면에서도 제목과 본문을 구분합니다.","2. 긴 설명은 여백을 줄이지 않고 스크롤합니다.","3. 색상 일치 수치는 별도의 노란색으로 표시합니다.","4. 메뉴를 열어도 게임의 저장 데이터는 바뀌지 않습니다.","5. 선택한 영역만 강조하고 비활성 영역은 옅게 표시합니다.","6. Esc를 누르면 이전 선택 위치로 돌아갑니다.","7. 한 번 누른 키는 한 번만 처리합니다.","8. 마지막 줄까지 읽을 수 있습니다."};
            for(int i=0;i<3;i++)Text(page,"Line"+i,lines[ScrollLine+i],210,550+i*80,1450,65,30,muted);
            Text(page,"ScrollPosition",$"W / S   {ScrollLine+1} - {ScrollLine+3} / 8",1460,790,300,50,25,muted);
        }
        void OnDestroy()
        {
            DisposeNative();
            if(renameKeyboard!=null)renameKeyboard.onIMECompositionChange-=RenameComposition;
            if(canvasRoot!=null)DestroyImmediate(canvasRoot.gameObject);
            if(eventRoot!=null)DestroyImmediate(eventRoot);
            art?.Dispose();art=null;Application.runInBackground=oldBackground;
        }
    }
}
