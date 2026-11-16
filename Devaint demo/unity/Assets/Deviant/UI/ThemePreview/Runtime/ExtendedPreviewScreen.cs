using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace Deviant.ThemePreview
{
    public sealed partial class ThemePreviewScreen
    {
        public static bool LaunchExtended;
        public bool ExtendedMode { get; private set; }
        public string ExtendedKey { get; private set; }="";
        public int ExtendedPick { get; private set; }
        public int ExtendedDepth { get; private set; }
        public int PreviewGold { get; private set; }=1200;
        public bool AugmentPending { get; private set; }
        public int EventProgress { get; private set; }
        public int PreviewRewardCount { get; private set; }=2;
        public bool LoadingLocked=>ExtendedKey=="loading";
        public IReadOnlyList<NativeScreen> ExtendedScreens=>catalogue.screens;
        readonly List<NativeNode> NativeControls=new List<NativeNode>();
        sealed class NativeReturn { public string Key; public int Pick,Depth; }
        readonly Stack<NativeReturn> extendedHistory=new Stack<NativeReturn>();
        readonly HashSet<string> boughtSlots=new HashSet<string>();
        readonly HashSet<string> completedEvents=new HashSet<string>();
        string extendedCoreReturn, rewardReturn, currentEvent, pendingBinding;
        readonly Dictionary<string,string> nativeOverrides=new Dictionary<string,string>();
        int extendedGroup,extendedGalleryPick,selectedCandidate,settingCategory;
        float screenSince,previewTime=30;
        bool settingsDirty, initialiseNativeSelection;
        RectTransform nativeFocus;
        readonly string[] extendedGroups={"보상·상점·증강","설정·일시정지·결과","마을·기록·대화","월드·이벤트·오프닝"};
        public void ResetExtended()
        {
            LoadNativeCatalogue();ResetCommerce();CoreMode=false;ExtendedMode=true;ExtendedKey="";ExtendedPick=ExtendedDepth=0;
            extendedHistory.Clear();nativeScrollOffsets.Clear();Render();
        }
        public void OpenExtended(string key,bool remember=true)
        {
            LoadNativeCatalogue();if(!catalogue.screens.Any(s=>s.key==key))throw new ArgumentException("Unknown preview screen: "+key);
            if(remember&&ExtendedKey!=""&&ExtendedKey!=key)extendedHistory.Push(new NativeReturn{Key=ExtendedKey,Pick=ExtendedPick,Depth=ExtendedDepth});
            CoreMode=false;ExtendedMode=true;ExtendedKey=key;ExtendedPick=0;selectedCandidate=0;initialiseNativeSelection=true;ExtendedDepth=0;screenSince=Time.unscaledTime;nativeScrollOffsets.Clear();
            if(key=="boss-ink"){receiptOffer=null;receiptOrigin=null;}
            if(key=="survival-progress")previewTime=30;
            if(key=="display")settingCategory=0;else if(key=="sound")settingCategory=1;else if(key.StartsWith("controls"))settingCategory=2;
            Render();
        }
        void BackExtended()
        {
            if(extendedHistory.Count>0){var back=extendedHistory.Pop();OpenExtended(back.Key,false);ExtendedPick=back.Pick;ExtendedDepth=back.Depth;SelectNativeCandidate();Render();UpdateNativeFocus(true);}
            else {ExtendedKey="";Render();}
        }
        public void HandleExtended(string key)
        {
            LastKeyFrame=Frames;
            if(ExtendedKey=="")
            {
                if(key=="Q"||key=="E"){extendedGroup=(extendedGroup+(key=="E"?1:3))%4;extendedGalleryPick=0;}
                var rows=catalogue.screens.Where(s=>s.group==extendedGroups[extendedGroup]).ToArray();
                if(key=="Down")extendedGalleryPick=Math.Min(rows.Length-1,extendedGalleryPick+1);
                if(key=="Up")extendedGalleryPick=Math.Max(0,extendedGalleryPick-1);
                if(key=="Right")extendedGalleryPick=Math.Min(rows.Length-1,extendedGalleryPick+10);
                if(key=="Left")extendedGalleryPick=Math.Max(0,extendedGalleryPick-10);
                if(key=="F"){OpenExtended(rows[extendedGalleryPick].key,false);return;}Render();return;
            }
            if(LoadingLocked)return;
            if(key=="W"||key=="S"){ScrollNative(key=="S"?1:-1);return;}
            if(IsFixedConfirmation())
            {
                if(key=="F"||key=="G"||key=="Esc")ConfirmExtended(key);return;
            }
            if(ExtendedKey=="rebind")
            {
                if(key!="Esc"&&pendingBinding!=null)nativeOverrides[pendingBinding]=key;pendingBinding=null;BackExtended();return;
            }
            if(key=="Esc")
            {
                if(IsMandatoryReward())return;
                if(ExtendedKey=="reward-receipt"||ExtendedKey=="boss-ink"){FinishCommerceReceipt();return;}
                if(IsSettings()&&ExtendedDepth>0){ExtendedDepth=0;Render();return;}
                if(settingsDirty&&IsSettings()){OpenExtended("unsaved");return;}
                if(ExtendedKey=="augment-choose"&&AugmentPending){OpenExtended("augment-pending",false);return;}
                if(ExtendedKey=="rescue-progress"||ExtendedKey=="survival-progress")EventProgress=0;
                BackExtended();return;
            }
            if(IsSettings()) {HandleSettings(key);return;}
            if(ExtendedKey.StartsWith("map"))
            {
                if(key=="+"||key=="-"){OpenExtended(key=="+"?"map-zoom":"map",false);return;}
                if(key=="Home"||key=="C"){OpenExtended("map",false);return;}
                if(key=="M"){BackExtended();return;}
                if(new[]{"Left","Right","Up","Down"}.Contains(key))
                {var regions=nativeNodes.Values.Where(n=>n.n=="Editable map geometry"&&n.w>500).ToArray();foreach(var region in regions){var rt=nativeRects[region.id];rt.anchoredPosition+=new Vector2(key=="Left"?40:key=="Right"?-40:0,key=="Up"?-40:key=="Down"?40:0);}return;}
            }
            if(ExtendedKey.StartsWith("portal")){if(key=="C"){OpenExtended("portal-one",false);return;}if(key=="V"){OpenExtended("portal-ready",false);return;}}
            if(key=="Left"||key=="Right"||key=="Up"||key=="Down") {MoveNativeFocus(key);return;}
            if(key=="Q"||key=="E")
            {
                if(ExtendedKey.StartsWith("map")){OpenExtended(key=="E"?"map-zoom":"map",false);return;}
                return;
            }
            if(key=="G"&&ExtendedKey=="first-reward"){rewardReturn="first-craft";OpenExtended("mixed");return;}
            if(key=="F")ActivateExtended();
        }
        bool IsSettings()=>new[]{"sound","display","controls","controls-combat","controls-explore"}.Contains(ExtendedKey);
        bool IsFixedConfirmation()=>new[]{"display-confirm","unsaved","controls-reset","reset-confirm","pause-confirm","title-confirm","loading-failed"}.Contains(ExtendedKey);
        void ConfirmExtended(string key)
        {
            var s=ExtendedKey;
            if(s=="loading-failed"){if(key=="F")OpenExtended("loading",false);else if(key=="G"){ExtendedKey="";extendedHistory.Clear();Render();}return;}
            if(s=="display-confirm"){settingsDirty=false;BackExtended();return;}
            if(s=="unsaved"){if(key=="G")settingsDirty=false;BackExtended();return;}
            if(key=="G")
            {
                if(s=="controls-reset"){nativeOverrides.Clear();BackExtended();return;}
                if(s=="pause-confirm"){OpenExtended("village-well",false);return;}
                if(s=="title-confirm"){ExtendedKey="";extendedHistory.Clear();Render();return;}
                if(s=="reset-confirm"){ResetCommerce();completedEvents.Clear();PlayerPrefs.DeleteKey("Deviant.ThemePreview.Tutorial");BackExtended();return;}
            }
            BackExtended();
        }
        void HandleSettings(string key)
        {
            if(ExtendedDepth==0)
            {
                if(key=="Up"||key=="Down")settingCategory=Mathf.Clamp(settingCategory+(key=="Down"?1:-1),0,2);
                if(key=="F"){OpenExtended(new[]{"display","sound","controls"}[settingCategory],false);ExtendedDepth=1;Render();return;}
                Render();return;
            }
            if(key=="Up"||key=="Down"){MoveNativeFocus(key);return;}
            var n=NativeControls.Count>0?NativeControls[ExtendedPick]:null;if(n==null)return;
            if(key=="Left"||key=="Right")
            {
                settingsDirty|=ExtendedKey=="display";
                var texts=Descendants(n).Where(x=>x.text!=null).ToArray();
                var percent=texts.FirstOrDefault(t=>t.text.Contains("%"));
                if(percent!=null&&nativeRects.TryGetValue(percent.id,out var rt))
                {var t=rt.GetComponent<TextMeshProUGUI>();int val=int.Parse(Regex.Match(t.text,"[0-9]+").Value);t.text=Mathf.Clamp(val+(key=="Right"?10:-10),0,100)+"%";nativeOverrides[percent.id]=t.text;}
                else {var value=texts.FirstOrDefault(t=>t.text.Contains("켜짐")||t.text.Contains("꺼짐")||t.text.Contains("1920")||t.text.Contains("창"));if(value!=null&&nativeRects.TryGetValue(value.id,out var vr)){var t=vr.GetComponent<TextMeshProUGUI>();t.text=t.text=="켜짐"?"꺼짐":t.text=="꺼짐"?"켜짐":t.text.Contains("×")?"1280 × 720":t.text.Contains("창")?"전체 화면":value.text;nativeOverrides[value.id]=t.text;}}
                return;
            }
            if(key=="F")
            {
                if(ExtendedKey=="display")OpenExtended("display-confirm");
                else if(ExtendedKey.StartsWith("controls")){var binding=Descendants(n).FirstOrDefault(x=>x.n=="binding");pendingBinding=binding==null?null:Descendants(binding).FirstOrDefault(x=>x.text!=null)?.id;OpenExtended(n.Caption.Contains("복원")?"controls-reset":"rebind");}
            }
        }
        static IEnumerable<NativeNode> Descendants(NativeNode n){yield return n;if(n.ch!=null)foreach(var c in n.ch)foreach(var v in Descendants(c))yield return v;}
        bool IsControl(NativeNode n)
        {
            string token=n.n.Split(' ')[0];
            if(IsSettings())return ExtendedDepth==0?(token=="BUTTON"||token=="navActive")&&(n.Caption.Trim()=="화면"||n.Caption.Trim()=="소리"||n.Caption.Trim()=="조작"):(token=="row"||token=="apply"||token=="reset");
            return new[]{"card","item","action","choice","btn","upgradeEntry","worldProduct","worldGood","mapNode"}.Contains(token)||(token=="BUTTON"&&n.Caption.Length>1);
        }
        void DrawExtended()
        {
            LoadNativeCatalogue();nativeRects.Clear();nativeNodes.Clear();nativeScrolls.Clear();NativeControls.Clear();nativeFocus=null;selectionOriginalTextures.Clear();selectionOriginalSprites.Clear();
            if(ExtendedKey==""){DrawExtendedGallery();return;}
            var screen=catalogue.screens.First(s=>s.key==ExtendedKey);
            if(initialiseNativeSelection){var sourceCandidates=Descendants(screen.root).Where(n=>n.n.StartsWith("card ")||n.n.StartsWith("item ")||n.n.StartsWith("worldGood")).ToArray();int initial=Array.FindIndex(sourceCandidates,n=>n.n.Contains("selected"));selectedCandidate=Math.Max(0,initial);}
            if(initialiseNativeSelection&&IsShop())selectedCandidate=ExtendedKey.StartsWith("shop-world")?3:0;
            var displayRoot=screen.group==extendedGroups[0]?BindCommerce(screen.root):screen.root;
            nativeBakedOverlay=Descendants(displayRoot).FirstOrDefault(n=>n.w==1920&&n.h==1080&&(n.n=="dark"||n.n=="loading")&&n.p?.Any(p=>p.t=="s")==true);NativeDraw(displayRoot,page,true);
            NativeControls.AddRange(Descendants(displayRoot).Where(n=>!n.hide&&IsControl(n)));
            NativeControls.RemoveAll(n=>NativeControls.Any(parent=>parent!=n&&ContainsNode(parent,n.id)));
            if(initialiseNativeSelection){int chosen=NativeControls.FindIndex(n=>n.n.Split(' ').Contains("selected"));if(chosen>=0)ExtendedPick=chosen;if(NativeControls.Count>0&&NativeControls[ExtendedPick].n.Contains("disabled")){int enabled=NativeControls.FindIndex(n=>!n.n.Contains("disabled"));if(enabled>=0)ExtendedPick=enabled;}initialiseNativeSelection=false;SelectNativeCandidate();}
            if(IsSettings()&&ExtendedDepth==0)ExtendedPick=Math.Min(settingCategory,NativeControls.Count-1);
            ExtendedPick=Mathf.Clamp(ExtendedPick,0,Math.Max(0,NativeControls.Count-1));
            foreach(var n in NativeControls)
            {
                if(!nativeRects.TryGetValue(n.id,out var rt))continue;var target=n;
                if(n.n.Contains("disabled")){var alpha=rt.GetComponent<CanvasGroup>();if(alpha==null)alpha=rt.gameObject.AddComponent<CanvasGroup>();alpha.alpha=.4f;}
                var hit=Box("Native control hit",rt,0,0,n.w,n.h);Click(hit,()=>{ExtendedPick=NativeControls.IndexOf(target);SelectNativeCandidate();if(IsFixedConfirmation())ConfirmExtended(target.Caption.Contains("G")?"G":"F");else if(IsSettings())HandleSettings("F");else ActivateExtended();});
            }
            if(IsShop())UpdateShopSold();
            if(ExtendedKey=="rescue-progress"||ExtendedKey=="collection-progress")UpdateProgressText();
            UpdateNativeFocus(ExtendedPick>0);
        }
        void DrawExtendedGallery()
        {
            Text(page,"PreviewCatalogueTitle","독립 UI 화면 선택",130,65,1500,60,42);
            for(int i=0;i<4;i++){int g=i;var label=Text(page,"Category"+i,extendedGroups[i],130+i*430,165,405,45,27,i==extendedGroup?paper:muted);if(i==extendedGroup)LabelWash(label);Click(Box("CategoryHit",page,125+i*430,155,410,65),()=>{extendedGroup=g;extendedGalleryPick=0;Render();});}
            var rows=catalogue.screens.Where(s=>s.group==extendedGroups[extendedGroup]).ToArray();int first=extendedGalleryPick/10*10;
            for(int i=first;i<Math.Min(first+10,rows.Length);i++){int pick=i;Button(page,rows[i].name,130,245+(i-first)*67,1620,i==extendedGalleryPick,()=>{extendedGalleryPick=pick;OpenExtended(rows[pick].key,false);});}
            Text(page,"PageCount",$"{extendedGalleryPick+1} / {rows.Length}",1600,930,180,35,23,muted);DrawKeyboardFooter();
        }
        void MoveNativeFocus(string key)
        {
            if(NativeControls.Count==0)return;
            var row=NativeControls.Where(n=>n.n.StartsWith("card ")||n.n.StartsWith("item ")||n.n.StartsWith("worldGood")).ToList();
            int position=row.IndexOf(NativeControls[ExtendedPick]);
            if(position>=0&&(key=="Left"||key=="Right"))
            {ExtendedPick=NativeControls.IndexOf(row[Mathf.Clamp(position+(key=="Right"?1:-1),0,row.Count-1)]);SelectNativeCandidate();if(IsShop()){nativeScrollOffsets.Clear();Render();}UpdateNativeFocus(true);return;}
            if(position>=0&&IsShop()&&!ExtendedKey.StartsWith("shop-world")&&row.Count==6&&(key=="Down"&&position<3||key=="Up"&&position>=3))
            {ExtendedPick=NativeControls.IndexOf(row[position+(key=="Down"?3:-3)]);SelectNativeCandidate();nativeScrollOffsets.Clear();Render();UpdateNativeFocus(true);return;}
            if(position>=0&&key=="Down")
            {int actionIndex=NativeControls.FindIndex(n=>n.n.Split(' ')[0]=="action");if(actionIndex>=0){ExtendedPick=actionIndex;UpdateNativeFocus(true);return;}}
            if(position<0&&row.Count>0&&key=="Up")
            {ExtendedPick=NativeControls.IndexOf(row[Mathf.Clamp(selectedCandidate,0,row.Count-1)]);UpdateNativeFocus(true);return;}
            int old=ExtendedPick;var cur=nativeRects[NativeControls[old].id];Vector3 c=cur.position;float best=float.MaxValue;int found=-1;
            for(int i=0;i<NativeControls.Count;i++)
            {if(i==old)continue;var r=nativeRects[NativeControls[i].id];Vector3 d=r.position-c;float main=(key=="Right"?d.x:key=="Left"?-d.x:key=="Down"?-d.y:d.y);float cross=(key=="Right"||key=="Left")?Mathf.Abs(d.y):Mathf.Abs(d.x);if(main>2&&main+cross*3<best){found=i;best=main+cross*3;}}
            if(found>=0)ExtendedPick=found;
            SelectNativeCandidate();
            if(IsShop()){nativeScrollOffsets.Clear();Render();}
            UpdateNativeFocus(true);
        }
        void SelectNativeCandidate()
        {
            if(NativeControls.Count==0)return;var target=NativeControls[ExtendedPick];
            var candidates=NativeControls.Where(n=>n.n.StartsWith("card ")||n.n.StartsWith("item ")||(n.n.StartsWith("worldProduct")||n.n.StartsWith("worldGood"))).ToList();
            int index=candidates.IndexOf(target);if(index>=0)selectedCandidate=index;
        }
        NativeNode NativeName(NativeNode node)
        {
            if(node.n.StartsWith("worldGood"))return null;
            var named=Descendants(node).FirstOrDefault(n=>n.n=="name"||n.n=="label"||n.n=="itemName");
            if(named!=null)return Descendants(named).FirstOrDefault(n=>n.text!=null);
            var heading=node.ch?.FirstOrDefault(n=>n.n=="H2"||n.n=="SPAN");
            return (heading==null?Descendants(node):Descendants(heading)).FirstOrDefault(n=>n.text!=null&&n.text!="F"&&n.text!="G");
        }
        readonly Dictionary<UnityEngine.UI.Image,Sprite> selectionOriginalSprites=new Dictionary<UnityEngine.UI.Image,Sprite>();
        public string FocusedNativeLabel {get;private set;}
        void UpdateNativeFocus(bool moved)
        {
            if(NativeControls.Count==0||IsFixedConfirmation())return;
            if(nativeFocus!=null)DestroyImmediate(nativeFocus.gameObject);
            foreach(var n in nativeNodes.Values.Where(n=>n.n=="::before"))if(nativeRects.TryGetValue(n.id,out var r)&&n.p?.Any(p=>p.t=="i"&&p.hash=="fdac02901e7a20b1d60e8c7e311c5404c8d37662")==true)r.gameObject.SetActive(false);
            var candidates=NativeControls.Where(n=>n.n.StartsWith("card ")||n.n.StartsWith("item ")||(n.n.StartsWith("worldProduct")||n.n.StartsWith("worldGood"))).ToList();
            foreach(var candidate in candidates)
            {
                bool blocked=candidate.n.Contains("sold")||(IsShop()&&ShopSold(candidates.IndexOf(candidate)));
                bool selected=candidates.IndexOf(candidate)==selectedCandidate&&!blocked;
                if(nativeRects.TryGetValue(candidate.id,out var candidateRect)){var ca=candidateRect.GetComponent<CanvasGroup>();if(ca!=null)ca.alpha=blocked?.4f:1;}
                var visual=candidate.ch?.FirstOrDefault(n=>n.n=="svg"||n.n.StartsWith("inkVisual")||n.n.Contains("approvedWeapon"));
                if(visual!=null&&nativeRects.TryGetValue(visual.id,out var vr))
                {
                    bool original=candidate.n.Contains("selected");
                    float originalScale=candidate.n.StartsWith("card ")?(original?1.13f:.9f):candidate.n.StartsWith("worldGood")?(original?1.13f:1):(original?1.08f:.82f);
                    float desiredScale=candidate.n.StartsWith("card ")?(selected?1.13f:.9f):candidate.n.StartsWith("worldGood")?(selected?1.13f:1):(selected?1.08f:.82f);
                    float factor=desiredScale/originalScale;
                    vr.localScale=Vector3.one*factor;vr.anchoredPosition=new Vector2(visual.x+visual.w*(1-factor)/2,-visual.y-visual.h*(1-factor)/2);
                    foreach(var child in Descendants(visual))if(nativeRects.TryGetValue(child.id,out var cr)){var alpha=cr.GetComponent<CanvasGroup>();if(alpha!=null)alpha.alpha=1;}
                    var cg=vr.GetComponent<CanvasGroup>();if(cg==null)cg=vr.gameObject.AddComponent<CanvasGroup>();cg.alpha=selected?1:.6f;
                    foreach(var image in vr.GetComponentsInChildren<UnityEngine.UI.Image>())
                    {if(image.sprite==null)continue;if(!selectionOriginalSprites.TryGetValue(image,out var source)){source=image.sprite;selectionOriginalSprites[image]=source;}
                     image.sprite=selected?source:art.Slice(NativeMutedTexture(source.texture),new Rect(source.rect.x,source.texture.height-source.rect.yMax,source.rect.width,source.rect.height));}
                    foreach(var image in vr.GetComponentsInChildren<UnityEngine.UI.RawImage>())
                    {if(!selectionOriginalTextures.TryGetValue(image,out var source)){source=image.texture as Texture2D;selectionOriginalTextures[image]=source;}if(source!=null)image.texture=selected?source:NativeMutedTexture(source);}
                }
                foreach(var child in candidate.ch??Array.Empty<NativeNode>())if(child.n=="H2"||child.n=="effectSentence"||child.n=="rewardType"||child.n=="group")
                {if(nativeRects.TryGetValue(child.id,out var cr)){var cg=cr.GetComponent<CanvasGroup>();if(cg==null)cg=cr.gameObject.AddComponent<CanvasGroup>();cg.alpha=selected?1:.74f;}}
            }
            var node=NativeControls[ExtendedPick];var name=NativeName(node);
            if(name!=null&&nativeRects.TryGetValue(name.id,out var lr))
            {var label=lr.GetComponent<TextMeshProUGUI>();FocusedNativeLabel=label.text;LabelWash(label,14,10);nativeFocus=label.transform.parent.GetChild(label.transform.GetSiblingIndex()-1) as RectTransform;
                var keycap=Descendants(node).FirstOrDefault(n=>n.n=="actionKeycap");
                if(keycap!=null&&nativeRects.TryGetValue(keycap.id,out var kr)&&nativeFocus.parent==kr.parent)
                {float right=nativeFocus.anchoredPosition.x+nativeFocus.rect.width;float left=kr.anchoredPosition.x-14;nativeFocus.anchoredPosition=new Vector2(left,nativeFocus.anchoredPosition.y);nativeFocus.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,right-left);}}
        }
        bool IsShop()=>ExtendedKey.StartsWith("shop");
        void UpdateShopSold()
        {
            int index=0;foreach(var n in NativeControls.Where(n=>n.n.StartsWith("item ")||(n.n.StartsWith("worldProduct")||n.n.StartsWith("worldGood"))))
            {if(ShopSold(index)){var rt=nativeRects[n.id];var cg=rt.GetComponent<CanvasGroup>();if(cg==null)cg=rt.gameObject.AddComponent<CanvasGroup>();cg.alpha=.4f;foreach(var text in rt.GetComponentsInChildren<TextMeshProUGUI>())if(Regex.IsMatch(text.text,@"[0-9]+.*G"))text.text="품절";}index++;}
        }
        void ReplaceNativeText(string before,string after){foreach(var rt in nativeRects.Values){var t=rt.GetComponent<TextMeshProUGUI>();if(t!=null&&t.text.Contains(before)){t.text=t.text.Replace(before,after);if(t.preferredWidth>t.rectTransform.rect.width)t.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,Mathf.Min(900,t.preferredWidth+2));}}}
        void UpdateProgressText(){foreach(var rt in nativeRects.Values){var t=rt.GetComponent<TextMeshProUGUI>();if(t!=null&&Regex.IsMatch(t.text,@"\d\s*/\s*\d"))t.text=EventProgress+" / "+(ExtendedKey=="rescue-progress"?3:5);}}
        void ActivateExtended()
        {
            string k=ExtendedKey;var n=NativeControls.Count>0?NativeControls[ExtendedPick]:null;string caption=n?.Caption??"";
            if(n!=null&&(n.n.Contains("disabled")||n.n.Contains("locked")||n.n.Contains("sold")))return;
            if(ActivateCommerce(caption))return;
            if(caption.Contains("건너뛰")){OpenExtended(k=="opening"?"opening-movement":"opening-record",false);return;}
            if(caption.Contains("가방 확인")||k=="first-craft"){extendedCoreReturn=k;ExtendedMode=false;ResetCore();if(k=="first-craft")SwitchCore(1);return;}
            if(caption.Contains("보유 증강")){OpenExtended("augment-owned");return;}
            if(caption.Contains("닫기")||caption.Contains("돌아가기")||caption.Contains("계속하기")){BackExtended();return;}
            if(k=="augment-owned"){BackExtended();return;}
            if(k=="pause-combat"||k=="pause-village")
            {if(caption.Contains("설정")){settingCategory=0;OpenExtended("display");}else if(caption.Contains("조작"))OpenExtended("help");else if(caption.Contains("마을"))OpenExtended("pause-confirm");else if(caption.Contains("타이틀"))OpenExtended("title-confirm");else BackExtended();return;}
            if(k=="help"){BackExtended();return;}
            if(k=="augment-poor"||k=="augment-max"||k=="augment-shortage"||k=="shop-poor"||k=="shop-world-poor"||k=="shop-ch3"||k=="recovery-poor"||k=="recovery-full"||k=="recovery-empty"||k=="chest-no-key")return;
            if(k=="seal"){EventProgress=0;OpenExtended("seal-progress");return;}
            if(k=="seal-progress"){if(++EventProgress>=3)OpenExtended("seal-complete",false);else UpdateSealProgress();return;}
            if(k=="seal-complete"){OpenExtended("augment-seal");return;}
            if(k=="rescue"||k=="survival"||k=="collection"){currentEvent=k;if(k!="collection")EventProgress=0;OpenExtended(k+"-progress");return;}
            if(k=="rescue-progress"||k=="collection-progress"){EventProgress++;if(EventProgress>=(k=="rescue-progress"?3:5))CompleteEvent();else UpdateProgressText();return;}
            if(k=="danger"||k=="danger-wrong"){currentEvent="danger";if(ExtendedPick==2)CompleteEvent();else OpenExtended("danger-wrong",false);return;}
            if(k=="event-complete"){rewardReturn="boss-ready";OpenExtended("mixed",false);return;}
            if(k.StartsWith("boss-intro")){OpenExtended("boss-ready",false);return;}
            if(k=="portal"||k=="portal-tutorial"){OpenExtended("portal-one",false);return;}if(k=="portal-one"){OpenExtended("portal-ready",false);return;}
            if(k=="portal-ready"){ReplaceNativeText("1","2");return;}
            if(k=="final"||k=="result"||k=="result-boss"){OpenExtended("loading",false);return;}if(k=="result-clear"){OpenExtended("loading",false);return;}
            var routes=new Dictionary<string,string>{{"village-record","records"},{"village-well","well"},{"village-workshop","permanent"},{"village-exit","loading"},{"well","restoration"},{"dialogue","dialogue-last"},{"records","records-notebook"},{"records-first","records-notebook"},{"chest-key","mixed"},{"recovery","recovery-full"},{"permanent-dash-ready","permanent-dash-owned"},{"opening","opening-movement"},{"opening-movement","opening-chase"},{"opening-chase","opening-helper"},{"opening-helper","opening-awake"},{"opening-awake","opening-record"},{"opening-record","opening-unarmed"},{"opening-unarmed","first-reward"},{"first-durability","first-return"},{"first-return","well"},{"opening-resume","opening-movement"},{"guide","first-craft"},{"guide-durability","first-durability"}};
            if(routes.TryGetValue(k,out var next)){if(k.StartsWith("opening"))PlayerPrefs.SetString("Deviant.ThemePreview.Tutorial",next);OpenExtended(next);return;}
            if(k.StartsWith("permanent")&& !k.Contains("locked")&&!k.Contains("poor")&&!k.Contains("max")&&!k.Contains("owned")){OpenExtended("permanent-max",false);return;}
        }
        void UpdateSealProgress()
        {
            var cells=nativeNodes.Values.Where(n=>n.n.StartsWith("pageDot")).OrderBy(n=>n.x).ToArray();
            for(int i=0;i<cells.Length;i++){var source=Descendants(cells[i]).FirstOrDefault(n=>n.n=="image");if(source!=null&&nativeRects.TryGetValue(source.id,out var rt))rt.anchoredPosition=new Vector2(-source.w/3*(i<EventProgress?2:i==EventProgress?1:0),rt.anchoredPosition.y);}
        }
        void CompleteEvent(){if(completedEvents.Contains(currentEvent))return;completedEvents.Add(currentEvent);OpenExtended("event-complete",false);}
        void TickExtended()
        {
            if(!ExtendedMode||ExtendedKey=="")return;float age=Time.unscaledTime-screenSince;
            if(ExtendedKey.StartsWith("boss-intro")&&age>=4)OpenExtended("boss-ready",false);
            else if(ExtendedKey=="event-complete"&&age>=.8f){rewardReturn="boss-ready";OpenExtended("mixed",false);}
            else if(ExtendedKey=="weapon-alert"&&age>=3.5f)OpenExtended("boss-ready",false);
            else if(ExtendedKey=="loading"&&age>=1.5f)OpenExtended("village-well",false);
            else if(ExtendedKey=="display-confirm"&&age>=15){settingsDirty=false;BackExtended();}
            else if(ExtendedKey=="survival-progress")
            {previewTime=Mathf.Max(0,30-age);foreach(var rt in nativeRects.Values){var t=rt.GetComponent<TextMeshProUGUI>();if(t!=null&&Regex.IsMatch(t.text,@"^\d\d:\d\d$"))t.text="00:"+Mathf.CeilToInt(previewTime).ToString("00");}if(previewTime==0){currentEvent="survival";CompleteEvent();}}
        }
    }
}
