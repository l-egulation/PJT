using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Deviant.ThemePreview
{
    public static partial class ThemePreviewWorkbench
    {
        [MenuItem("Deviant/UI Theme Preview/Run remaining Figma checks")]
        public static void VerifyExtended(){Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"request.txt"),"verify-extended");}
        static void BeginExtendedChecks()
        {
            File.Delete(Path.Combine(Output,"failure.txt"));File.Delete(Path.Combine(Output,"extended-checks.json"));File.Delete(Path.Combine(Output,"native-text-overflow.txt"));
            Screen.ResetExtended();passed.Clear();steps=new Queue<Action>();keyboard=InputSystem.AddDevice<Keyboard>("RemainingFigmaVerification");nextFrame=Screen.Frames+8;
            steps.Enqueue(()=>Expect(Screen.ExtendedScreens.Count==119,"All 119 remaining approved states; HUD excluded"));
            Press(Key.F);steps.Enqueue(()=>Expect(Screen.ExtendedKey=="reward","Gallery F opens reward"));
            Press(Key.RightArrow);steps.Enqueue(()=>{Expect(Screen.ExtendedPick>0,"Arrow changes candidate");Expect(Screen.FocusedNativeLabel=="스위칭 회복","Selection brush targets ink name, not category");
                var inks=UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None).Where(r=>r.name.StartsWith("inkVisual")).OrderBy(r=>r.position.x).ToArray();
                Expect(inks.Length==2&&inks[0].rect.height*inks[0].lossyScale.y>inks[1].rect.height*inks[1].lossyScale.y,"Selected ink artwork is larger than idle candidate");
                Expect(inks[0].GetComponent<CanvasGroup>().alpha==1&&inks[1].GetComponent<CanvasGroup>().alpha<1,"Selected ink restores full opacity");});
            steps.Enqueue(()=>CaptureNative("reward-selected-ink-1920.png"));
            Press(Key.RightArrow);steps.Enqueue(()=>CaptureNative("reward-selected-third-1920.png"));Press(Key.LeftArrow);
            Press(Key.F);steps.Enqueue(()=>{Expect(Screen.ExtendedKey=="reward-receipt","F confirms reward");
                var labels=UnityEngine.Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None);
                Expect(labels.Any(t=>t.text.Contains("체력을 3")),"Selected recovery ink description survives receipt");
                Expect(!labels.Any(t=>t.text.Contains("긴 찌르기")),"Ink receipt contains no stale rapier description");});
            steps.Enqueue(()=>CaptureNative("commerce-receipt-ink-1920.png"));
            steps.Enqueue(()=>Screen.OpenExtended("augment",false));Press(Key.F);
            steps.Enqueue(()=>Expect(Screen.AugmentPending&&Screen.ExtendedKey=="augment-choose","Purchase reserves candidates"));Press(Key.Escape);
            steps.Enqueue(()=>Expect(Screen.ExtendedKey=="augment-pending","Esc preserves pending candidates"));Press(Key.F);
            steps.Enqueue(()=>Expect(Screen.ExtendedKey=="augment-choose","Reopen same paid choice"));
            steps.Enqueue(()=>Screen.OpenExtended("shop-poor",false));int gold=0;
            steps.Enqueue(()=>gold=Screen.PreviewGold);Press(Key.F);steps.Enqueue(()=>Expect(Screen.PreviewGold==gold&&Screen.ExtendedKey=="shop-poor","Insufficient gold cannot purchase"));
            steps.Enqueue(()=>Screen.OpenExtended("shop-ch2",false));Press(Key.DownArrow);Press(Key.DownArrow);
            steps.Enqueue(()=>Expect(Screen.FocusedNativeLabel=="구매 · 400 G","Grid Down reaches ink row then correct purchase button"));
            steps.Enqueue(()=>{Screen.ResetExtended();Screen.OpenExtended("shop",false);Expect(Screen.CurrentOffers.Count==6,"Shop shows three weapons and three inks");Expect(VisibleText(Screen.PreviewGold+" G"),"Shop gold label uses current wallet");});
            Press(Key.DownArrow);
            steps.Enqueue(()=>{Expect(VisibleText("기절 조건 스킬 피해 강화"),"Shop lower row details follow first ink");gold=Screen.PreviewGold;});
            steps.Enqueue(()=>CaptureNative("commerce-shop-six-1920.png"));
            Press(Key.F);
            steps.Enqueue(()=>{Expect(Screen.PreviewGold==gold-220&&Screen.ExtendedKey=="reward-receipt","Shop ink charges 220 G and opens actual receipt");gold=Screen.PreviewGold;});
            Press(Key.F);steps.Enqueue(()=>Expect(Screen.ExtendedKey=="shop","Receipt continues to same shop"));
            Press(Key.DownArrow);Press(Key.F);steps.Enqueue(()=>Expect(Screen.PreviewGold==gold,"Revisit sold slot cannot charge again"));
            Press(Key.UpArrow);Press(Key.RightArrow);Press(Key.F);
            steps.Enqueue(()=>{Expect(Screen.ExtendedKey=="reward-receipt"&&Screen.CommerceInventory.Sketches.Last().Name=="기본검","Purchased selected sketch appended once");gold=Screen.PreviewGold;});
            Press(Key.RightArrow);Press(Key.F);
            steps.Enqueue(()=>Expect(Screen.CoreMode&&Screen.Inventory.Draft.Id==Screen.CommerceInventory.Sketches.Last().Id,"Configure now opens actual purchased sketch"));
            Press(Key.Escape);steps.Enqueue(()=>Expect(Screen.ExtendedKey=="shop"&&Screen.PreviewGold==gold,"Configuration close returns shop without another grant"));
            steps.Enqueue(()=>{Screen.ResetExtended();Screen.OpenExtended("shop-world",false);});
            Press(Key.LeftArrow);Press(Key.LeftArrow);Press(Key.LeftArrow);
            steps.Enqueue(()=>{Expect(VisibleText("채찍"),"Six-slot world popup follows first weapon");Expect(VisibleText("휘감아"),"World popup follows skill");});
            steps.Enqueue(()=>CaptureNative("commerce-world-six-1920.png"));
            Press(Key.RightArrow);Press(Key.RightArrow);Press(Key.RightArrow);
            steps.Enqueue(()=>{Expect(VisibleText("스킬 속박"),"World popup follows fourth slot ink");gold=Screen.PreviewGold;});Press(Key.F);
            steps.Enqueue(()=>{Expect(Screen.PreviewGold==gold-220,"World shop charges selected ink once");Expect(Screen.CommerceInventory.OwnedInkIndices.Contains(20),"World shop registers selected root ink");});
            Press(Key.RightArrow);Press(Key.F);
            steps.Enqueue(()=>Expect(Screen.CoreStage=="acquiredTargets","Ink receipt opens unconfirmed sketch targets"));
            Press(Key.F);Press(Key.F);
            steps.Enqueue(()=>Expect(Screen.Inventory.Draft.Applied.Contains(20),"Ink acquisition applies owned ink to selected draft"));
            Press(Key.Escape);steps.Enqueue(()=>Expect(Screen.ExtendedKey=="shop-world","Ink configuration returns to originating world shop"));
            steps.Enqueue(()=>{Screen.ResetExtended();Screen.OpenExtended("boss",false);gold=Screen.CommerceInventory.Sketches.Count;});
            Press(Key.Escape);steps.Enqueue(()=>Expect(Screen.ExtendedKey=="boss","Mandatory reward cannot be discarded by Esc"));
            Press(Key.F);steps.Enqueue(()=>Expect(Screen.ExtendedKey=="reward-receipt"&&Screen.CommerceInventory.Sketches.Count==gold+1,"Boss grants one weapon directly to receipt"));
            Press(Key.F);steps.Enqueue(()=>Expect(Screen.ExtendedKey=="","Boss receipt finishes without two extra ink choices"));
            steps.Enqueue(()=>{Screen.ResetExtended();Screen.OpenExtended("boss-last",false);});Press(Key.F);
            steps.Enqueue(()=>Expect(Screen.CommerceInventory.Sketches.Last().Colors.Length==5&&Screen.ExtendedKey=="reward-receipt","Chapter 2 boss grants one five-slot weapon"));Press(Key.F);
            steps.Enqueue(()=>Screen.OpenExtended("boss-all-inks",false));Press(Key.F);
            steps.Enqueue(()=>Expect(Screen.CommerceInventory.Sketches.Last().Colors.Length==6&&Screen.ExtendedKey=="reward-receipt","Chapter 3 boss grants one six-slot weapon"));Press(Key.F);
            steps.Enqueue(()=>{Screen.ResetExtended();Screen.OpenExtended("shop-no-inks",false);});Press(Key.DownArrow);Press(Key.F);
            steps.Enqueue(()=>Expect(Screen.ExtendedKey=="shop-no-inks"&&Screen.CurrentOffers.Skip(3).All(o=>o.empty),"Exhausted ink slots remain empty, not replacement weapons"));
            steps.Enqueue(()=>{Screen.ResetExtended();Screen.OpenExtended("augment",false);});Press(Key.F);
            steps.Enqueue(()=>Screen.OpenExtended("augment-pending",false));Press(Key.F);Press(Key.RightArrow);Press(Key.F);
            steps.Enqueue(()=>{Expect(Screen.OwnedAugments.Any(o=>o.Item.id=="X09"),"Chosen gamble augment added by ID");Expect(VisibleText("위험한 책갈피"),"Owned list displays newly chosen augment");Expect(VisibleText("강제로"),"Owned list retains chosen augment cost");});
            Press(Key.S);steps.Enqueue(()=>CaptureNative("commerce-owned-cost-1920.png"));
            Press(Key.Escape);
            steps.Enqueue(()=>{Expect(Screen.ExtendedKey=="augment","Owned Esc returns to purchase, not consumed choices");Expect(Screen.AugmentPrice==90,"Next purchase price advances from 60 to 90");gold=Screen.PreviewGold;});
            Press(Key.F);steps.Enqueue(()=>{Expect(Screen.PreviewGold==gold-90,"Second augment payment uses incremented price");Expect(!Screen.CurrentOffers.Any(o=>o.id=="X09"),"Owned maintenance augment cannot be offered again");gold=Screen.PreviewGold;});
            string[] pending=null;steps.Enqueue(()=>pending=Screen.CurrentOffers.Select(o=>o.id).ToArray());Press(Key.Escape);Press(Key.F);
            steps.Enqueue(()=>{Expect(Screen.PreviewGold==gold,"Reopening paid augment selection does not charge again");Expect(Screen.CurrentOffers.Select(o=>o.id).SequenceEqual(pending),"Paid offer IDs and order survive closing");});
            steps.Enqueue(()=>Screen.OpenExtended("augment-seal",false));
            steps.Enqueue(()=>{var counts=UnityEngine.Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None).Where(t=>t.text.EndsWith(" / 3중첩")).ToArray();Expect(counts.Length==3&&counts.All(t=>-t.rectTransform.anchoredPosition.y+t.rectTransform.rect.height<=((RectTransform)t.transform.parent).rect.height),"All three seal stack counts fit without scrolling");});
            Press(Key.F);
            steps.Enqueue(()=>{Expect(Screen.OwnedAugments.Any(o=>o.Item.id=="reach"&&o.Stacks==1),"Seal choice adds selected augment without gold");Expect(Screen.PreviewGold==gold,"Seal reward is free");});
            steps.Enqueue(()=>Screen.OpenExtended("augment-seal",false));Press(Key.F);
            steps.Enqueue(()=>Expect(Screen.OwnedAugments.Single(o=>o.Item.id=="reach").Stacks==2,"Seal repeat increments same owned entry"));
            steps.Enqueue(()=>Screen.OpenExtended("mixed",false));Press(Key.RightArrow);Press(Key.DownArrow);Press(Key.RightArrow);Press(Key.F);
            steps.Enqueue(()=>Expect(Screen.CoreMode&&ReferenceEquals(Screen.Inventory,Screen.CommerceInventory),"Reward bag uses acquired inventory, not a reset fixture"));
            Press(Key.Escape);steps.Enqueue(()=>Expect(Screen.ExtendedKey=="mixed","Bag Esc returns to reward selection"));
            Press(Key.UpArrow);steps.Enqueue(()=>Expect(Screen.FocusedNativeLabel=="기본 공격 속박","Bag return restores original candidate focus"));
            steps.Enqueue(()=>Screen.OpenExtended("mixed",false));Press(Key.F);
            steps.Enqueue(()=>{Expect(Screen.ReceiptItemId=="sketch/rapier","Sketch receipt records selected ID");Expect(Screen.CommerceInventory.Sketches.Last().Colors.Length==1,"Reward preserves its palette slot count");});
            steps.Enqueue(()=>CaptureNative("commerce-receipt-sketch-1920.png"));
            steps.Enqueue(()=>{SetSize(960,540);Screen.OpenExtended("augment-owned",false);});
            steps.Enqueue(()=>CaptureNative("commerce-owned-960.png"));
            steps.Enqueue(()=>SetSize(1920,1080));
            steps.Enqueue(()=>Screen.OpenExtended("display",false));Press(Key.F);
            steps.Enqueue(()=>Expect(Screen.ExtendedDepth==1,"Settings F enters rows"));Press(Key.Escape);
            steps.Enqueue(()=>Expect(Screen.ExtendedDepth==0,"Settings Esc restores category depth"));
            steps.Enqueue(()=>Screen.OpenExtended("display-confirm",false));Press(Key.RightArrow);
            steps.Enqueue(()=>Expect(Screen.ExtendedPick==0,"F/G modal ignores arrows"));Press(Key.F);
            steps.Enqueue(()=>Expect(Screen.ExtendedKey!="display-confirm","F uses fixed confirmation action"));
            steps.Enqueue(()=>Screen.OpenExtended("loading",false));Press(Key.F);steps.Enqueue(()=>Expect(Screen.LoadingLocked,"Loading blocks F input"));
            steps.Enqueue(()=>Screen.OpenExtended("augment-owned",false));Press(Key.S);
            steps.Enqueue(()=>Expect(Screen.NativeScrollAmount>0,"W/S scrolls long owned-augment content"));
            steps.Enqueue(()=>CaptureNative("owned-scrolled-1920.png"));
            foreach(var s in Screen.ExtendedScreens.ToArray())
            {
                var item=s;
                steps.Enqueue(()=>{SetSize(1920,1080);Screen.OpenExtended(item.key,false);});
                steps.Enqueue(()=>CaptureNative("figma-"+item.key+"-1920.png"));
                steps.Enqueue(()=>SetSize(960,540));
                steps.Enqueue(()=>CaptureNative("figma-"+item.key+"-960.png"));
            }
            steps.Enqueue(()=>{CoreAcquisitionChecks.RunModel();CoreAcquisitionChecks.RunScreen(Screen);Expect(true,"Acquisition model and screen checks pass");});
            steps.Enqueue(()=>{SetSize(1920,1080);Screen.ResetExtended();File.WriteAllText(Path.Combine(Output,"extended-checks.json"),"{\"passed\":true,\"checks\":["+string.Join(",",passed.Select(x=>"\""+x.Replace("\"","'")+"\""))+"],\"frame\":"+Screen.Frames+"}");});
        }
        static bool VisibleText(string text)=>UnityEngine.Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None).Any(t=>t.text.Contains(text));
        static void CaptureNative(string name)
        {
            Expect(Screen.Error==null,"No exception: "+name);
            foreach(var t in UnityEngine.Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None))
            {t.ForceMeshUpdate();if(t.textBounds.size.x>t.rectTransform.rect.width+6)File.AppendAllText(Path.Combine(Output,"native-text-overflow.txt"),name+" | "+t.name+" | "+t.textBounds.size.x+" > "+t.rectTransform.rect.width+"\n");if(t.name=="Bound text")Expect(t.textBounds.size.x<=t.rectTransform.rect.width+6&&t.preferredHeight<=t.rectTransform.rect.height+6,"Bound item text fits: "+name);Expect(!float.IsNaN(t.rectTransform.rect.width)&&t.rectTransform.rect.width>0,"Valid native text rectangle: "+t.name);}
            UnityEngine.ScreenCapture.CaptureScreenshot(Path.Combine(Output,name));
        }
    }
}
