using System;
using System.Linq;
using Deviant.Input.Contracts;
using Deviant.Run;
using Deviant.UI.Theme;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        SketchChoice liveShopReceipt;
        bool liveShopConfiguring;
        bool liveShopWasOpen, liveShopInputReady;
        int liveAugmentPick,liveShopHintMode=-1;
        int liveOwnedScrollPick=-1;
        float liveOwnedScrollOffset;
        bool ApprovedShopConfiguring => liveShopConfiguring && Presenter.IsOpen;
        void BuildApprovedSketchShop()
        {
            var b=new EquipmentBook{Shop=true};b.Root=Rect("Approved shop",MenuStage,0,0,960,540).gameObject;
            RegisterScreenBackdrop(Fill(b.Root.transform,0,0,960,540,ApprovedTheme.MenuBackdrop)).raycastTarget=true;
            b.Body=Rect("Approved shop content",b.Root.transform,65,110,830,360);
            // Shared panel visibility owns the container; the owned-augment header owns the gold text.
            var goldRoot=Rect("Approved shop gold",b.Root.transform,790,76,100,24);
            b.Gold=LiveWords("",goldRoot,0,0,100,24,16,CoreGold,TextAnchor.MiddleRight);shopGoldChip=goldRoot.gameObject;
            b.Tabs=new Button[2];
            for(int i=0;i<2;i++){int tab=i;b.Tabs[i]=LiveButton("Shop tab "+i,i==0?"상점":"증강",b.Root.transform,65+i*100,30,85,34,()=>{b.Tab=tab;b.Key=null;},i==0,size:22);}
            LiveRule(b.Root.transform,65,492,830);LiveWords("방향키  상품 선택     F  구매     Esc  닫기     Q / E  탭     W / S  설명 스크롤",b.Root.transform,65,503,830,MenuHintHeight,MenuHintFontSize,CoreMuted).name="Shop keyboard help";
            nativeShop=b;shopPanel=b.Root;RegisterScreen(b.Root,b.Tabs[0]);
        }
        void DrawApprovedSketchShop(EquipmentBook b,SketchEquipment model)
        {
            if(liveShopReceipt!=null){DrawApprovedShopReceipt(b);return;}
            var stock=model.Shop(shop.SketchFacility);b.Offer=Mathf.Clamp(b.Offer,0,Math.Max(0,stock.Count-1));
            int first=b.Offer/6*6;
            for(int i=first;i<Math.Max(6,Math.Min(first+6,stock.Count));i++)
            {
                int index=i;float x=(i-first)%3*116,y=(i-first)/3*151;bool selected=i==b.Offer;var item=i<stock.Count?stock[i]:null;
                if(item==null){LiveWords("진열할 잉크 없음",b.Body,x,y+65,106,40,13,CoreMuted,TextAnchor.MiddleCenter);continue;}
                float iconSize=selected?88:72;LiveArt(item.Weapon!=null?LiveWeaponArt(item.Weapon):ApprovedTheme.Ink(item.Ink?.id),b.Body,x+(106-iconSize)/2,y,iconSize,84,selected?1:.6f);
                var button=LiveButton("Shop item "+i,item.Weapon!=null?LiveWeaponName(item.Weapon):item.Ink?.name??"품절",b.Body,x,y+91,106,38,()=>{b.Offer=index;b.Key=null;},selected,size:selected?15:13);button.GetComponentInChildren<Text>().alignment=TextAnchor.UpperCenter;
                LiveWords(item.Sold?"품절":item.Price<0?"가격 미정":item.Price+" G",b.Body,x,y+132,106,22,14,item.Sold||item.Price<0?CoreMuted:CoreGold,TextAnchor.MiddleCenter);
            }
            LiveWords($"{b.Offer+1} / {stock.Count}",b.Body,0,332,350,22,12,CoreMuted,TextAnchor.MiddleCenter);
            var perks=SketchBinding?SketchBinding.Perks:null;
            if(perks!=null&&perks.Settings.SharedRerolls>0){LiveButton("Reroll weapons",$"무기 새로고침 ({perks.RerollsRemaining})",b.Body,0,365,175,23,()=>RerollLiveShop(true),liveAction==1,perks.RerollsRemaining>0,12);LiveButton("Reroll inks",$"잉크 새로고침 ({perks.RerollsRemaining})",b.Body,185,365,175,23,()=>RerollLiveShop(false),liveAction==2,perks.RerollsRemaining>0,12);}
            Fill(b.Body,385,0,.6f,344,new Color(.5f,.55f,.6f,.25f));
            if(stock.Count==0)return;var selectedItem=stock[b.Offer];
            var content=JournalScroll("Shop item description",b.Body,410,0,420,282);liveDetail=content;float yy=0;
            LiveArt(selectedItem.Weapon!=null?LiveWeaponArt(selectedItem.Weapon):ApprovedTheme.Ink(selectedItem.Ink?.id),content,0,0,66,70);
            LiveWords(selectedItem.Weapon!=null?LiveWeaponName(selectedItem.Weapon):selectedItem.Ink?.name??"품절",content,90,4,290,60,21);yy=92;
            if(selectedItem.Weapon!=null)
            {
                yy=LiveParagraph(content,"스킬 / "+LiveSkill(selectedItem.Weapon),yy,16);yy=LiveParagraph(content,LiveSkillDescription(selectedItem.Weapon),yy+16,15);yy=LiveParagraph(content,"팔레트",yy+28,16);
                for(int i=0;i<selectedItem.Weapon.Slots.Count;i++)LiveArt(ApprovedTheme.Palette(selectedItem.Weapon.Slots[i].Color),content,i*61,yy+12,54,40);yy+=62;
            }
            else if(selectedItem.Ink!=null){yy=LiveParagraph(content,$"획득 시 +{model.Catalog.inkUsesPerAcquisition}회",yy,14,CoreGold);yy=LiveParagraph(content,LiveInkText(selectedItem.Ink),yy+16,16);}
            content.sizeDelta=new Vector2(420,Mathf.Max(282,yy+16));
            LiveRule(b.Body,410,303,420);string blocked=selectedItem.Sold?"품절":selectedItem.Price<0?"가격 미정":model.Economy.Gold<selectedItem.Price?"골드 부족":null;
            LiveButton("Buy selected shop item",blocked??"F  구매 / "+selectedItem.Price+" G",b.Body,420,314,400,35,()=>PurchaseLiveShop(b),liveAction==0,blocked==null);
        }
        void PurchaseLiveShop(EquipmentBook b)
        {
            var stock=Sketch.Shop(shop.SketchFacility);if(b.Offer<0||b.Offer>=stock.Count)return;var item=stock[b.Offer];if(item.Sold||item.Price<0||Sketch.Economy.Gold<item.Price)return;
            LiveTrade(b,()=>{Sketch.Buy(shop.SketchFacility,b.Offer);liveShopReceipt=null;liveAction=0;});
        }
        void RerollLiveShop(bool weapons)
        {var perks=SketchBinding?SketchBinding.Perks:null;if(perks==null||perks.RerollsRemaining<=0)return;LiveTrade(nativeShop,()=>{if(!perks.TryReroll(()=>Sketch.RerollShop(shop.SketchFacility,weapons)))throw new InvalidOperationException("새로고침할 상품이 없습니다.");});}
        void DrawApprovedShopReceipt(EquipmentBook b)
        {
            var item=liveShopReceipt;LiveWords("획득",b.Body,165,10,460,25,14,CoreBlue);
            LiveArt(item.Weapon!=null?LiveWeaponArt(item.Weapon):ApprovedTheme.Ink(item.Ink.id),b.Body,165,48,70,70);
            LiveWords(item.Weapon!=null?LiveWeaponName(item.Weapon):item.Ink.name,b.Body,260,52,460,50,22);
            LiveWords(item.Weapon!=null?"가방에 보관되었습니다.":$"잉크 부여 횟수 +{Sketch.Catalog.inkUsesPerAcquisition}회",b.Body,260,106,460,28,14,CoreMuted);
            var content=JournalScroll("Purchased item description",b.Body,165,160,500,126);liveDetail=content;
            float bottom=LiveParagraph(content,item.Weapon!=null?LiveSkillDescription(item.Weapon):LiveInkText(item.Ink),0,16,width:488);content.sizeDelta=new Vector2(500,Math.Max(126,bottom+12));
            LiveRule(b.Body,165,306,500);
            LiveButton("Continue shop receipt",(liveAction==0?"F  ":"")+"계속",b.Body,165,320,180,36,()=>{liveShopReceipt=null;b.Key=null;},liveAction==0);
            LiveButton("Configure shop receipt",(liveAction==1?"F  ":"")+(item.Weapon!=null?"지금 구성하기":"지금 부여하기"),b.Body,390,320,260,36,()=>ConfigureLiveShopReceipt(),selected:liveAction==1,enabled:item.Weapon!=null||Sketch.Weapons.Any(w=>!w.Destroyed&&!w.Confirmed));
        }
        void ConfigureLiveShopReceipt()
        {
            var item=liveShopReceipt;if(item==null||item.Weapon==null&&!Sketch.Weapons.Any(w=>!w.Destroyed&&!w.Confirmed))return;SelectLiveReceipt(item.Weapon?.Id??Guid.Empty,item.Ink?.id);liveShopReceipt=null;liveShopConfiguring=true;Presenter.Open();nativeShop.Key=null;
        }
        void RefreshApprovedShopHeader(EquipmentBook b)
        {
            bool owned=b.Tab==1;
            foreach(var tab in b.Tabs)tab.gameObject.SetActive(!owned);
            b.Gold.gameObject.SetActive(!owned);
            var legacy=b.Root.transform.Find("Shop keyboard help");if(legacy)legacy.gameObject.SetActive(false);
            int mode=owned?1:liveShopReceipt!=null?2:0;
            if(liveShopHintMode!=mode)
            {
                var hints=b.Root.transform.Find("Approved shop hints") as RectTransform;
                if(!hints)hints=Rect("Approved shop hints",b.Root.transform,65,499,830,27);else ClearGrowth(hints);
                ApprovedMenuHints(hints,0,0,new[]{owned?"증강 선택":mode==2?"버튼 선택":"상품 선택",owned?"돌아가기":mode==2?"실행":"구매","닫기","탭","설명 스크롤"});
                liveShopHintMode=mode;
            }
        }
        void DrawApprovedOwnedAugments(EquipmentBook b)
        {
            var augments=NativeGrowth?.Augments;var owned=augments?.Owned.ToArray()??Array.Empty<AugmentDefinition>();
            liveAugmentPick=Mathf.Clamp(liveAugmentPick,0,Math.Max(0,owned.Length-1));
            LiveWords("보유 증강",b.Body,0,-80,350,38,24);
            LiveWords("일시정지",b.Body,750,-74,80,25,12,CoreMuted,TextAnchor.MiddleRight);
            var content=JournalScroll("Owned augment list",b.Body,85,-28,660,324);liveDetail=content;
            float y=0,selectedTop=0,selectedBottom=0;
            for(int i=0;i<owned.Length;i++)
            {
                int index=i;var card=owned[i];bool selected=i==liveAugmentPick;
                var row=Rect("Owned augment row "+i,content,0,y,640,107);
                LiveArt(ApprovedTheme.Texture("ui-assets-v1/"+card.id),row,6,18,selected?43:38,43,selected?1:.7f);
                var button=LiveButton("Owned augment "+i,card.name,row,64,14,565,34,()=>{liveAugmentPick=index;},selected,size:selected?18:16);
                var name=button.GetComponentInChildren<Text>();
                float headerHeight=Mathf.Max(34,name.preferredHeight+4);button.GetComponent<RectTransform>().sizeDelta=new Vector2(565,headerHeight);name.rectTransform.sizeDelta=new Vector2(565,headerHeight);
                var body=Rect("Owned augment text",row,64,headerHeight+25,565,1);
                float bottom=LiveParagraph(body,card.gain,0,15,width:565);
                if(!card.Stable)bottom=LiveParagraph(body,card.cost,bottom+12,14,new Color32(218,164,150,255),565);
                float height=Mathf.Max(107,headerHeight+25+bottom+22);row.sizeDelta=new Vector2(640,height);
                LiveRule(row,0,height-11,640);
                if(selected){selectedTop=y;selectedBottom=y+height;}
                y+=height;
            }
            if(owned.Length==0)LiveWords("아직 획득한 증강이 없습니다.",content,12,42,620,55,17,CoreMuted);
            content.sizeDelta=new Vector2(660,Mathf.Max(324,y+8));
            if(liveOwnedScrollPick!=liveAugmentPick)
            {
                if(selectedTop<liveOwnedScrollOffset)liveOwnedScrollOffset=selectedTop;
                else if(selectedBottom>liveOwnedScrollOffset+324)liveOwnedScrollOffset=Mathf.Min(selectedTop,selectedBottom-324);
            }
            liveOwnedScrollPick=liveAugmentPick;
            liveOwnedScrollOffset=Mathf.Clamp(liveOwnedScrollOffset,0,Mathf.Max(0,content.rect.height-324));
            content.anchoredPosition=new Vector2(0,liveOwnedScrollOffset);
            LiveRule(b.Body,85,303,660);
            LiveButton("Return from owned augments","F  돌아가기",b.Body,90,317,230,35,()=>{b.Tab=0;liveAction=0;b.Key=null;},true);
        }
        bool TickApprovedShopInput()
        {
            if(nativeShop==null||shop==null||!shop.IsOpen){liveShopWasOpen=false;liveShopInputReady=false;return false;}
            if(!liveShopWasOpen){liveShopWasOpen=true;liveShopInputReady=false;}
            liveShopConfiguring=false;
            if(liveInputFrame==Time.frameCount)return true;liveInputFrame=Time.frameCount;
            var input=Controls;if(input){input.SuppressGameplay();input.Take(GameAction.Submit);input.Take(GameAction.Interact);input.Take(GameAction.Cancel);input.Take(GameAction.Pause);}
            // Refresh can re-enter input in the same frame that F opens the shop.
            // Raw device edges survive action consumption: wait for release before accepting a purchase.
            if(!liveShopInputReady)
            {
                liveShopInputReady=Keyboard.current?.fKey.isPressed!=true && Gamepad.current?.buttonSouth.isPressed!=true;
                return true;
            }
            var b=nativeShop;
            if(LiveBackPressed){if(liveShopReceipt!=null)liveShopReceipt=null;else shop.Close();b.Key=null;return true;}
            if(LivePreviousTabPressed||LiveNextTabPressed){b.Tab=1-b.Tab;liveAction=0;b.Key=null;return true;}
            if(b.Tab==1)
            {
                if(LiveSubmitPressed){b.Tab=0;liveAction=0;b.Key=null;return true;}
                int direction=TakeLiveNavigation();int change=direction==-2?-1:direction==2?1:0;
                if(change!=0){liveAugmentPick=Mathf.Clamp(liveAugmentPick+change,0,Math.Max(0,(NativeGrowth?.Augments?.Owned.Count??0)-1));b.Key=null;}
                ScrollLiveDetail();
                if(liveDetail && liveDetail.parent.name=="Owned augment list")liveOwnedScrollOffset=Mathf.Max(0,liveDetail.anchoredPosition.y);
                return true;
            }
            if(LiveSubmitPressed){if(liveShopReceipt!=null){if(liveAction==1)ConfigureLiveShopReceipt();else liveShopReceipt=null;}else if(liveAction>0)RerollLiveShop(liveAction==1);else PurchaseLiveShop(b);b.Key=null;return true;}
            int move=TakeLiveNavigation();
            if(liveShopReceipt!=null)
            {if(move==-1)liveAction=0;if(move==1)liveAction=1;}
            else
            {
                int d=Math.Abs(move)==2?move/2*3:move;
                if(d!=0){var count=Sketch.Shop(shop.SketchFacility).Count;var perks=SketchBinding?SketchBinding.Perks:null;if(liveAction>0){if(d==-3)liveAction=0;else liveAction=d<0?1:2;}else if(d==3&&b.Offer+3>=count&&perks!=null&&perks.Settings.SharedRerolls>0)liveAction=1;else b.Offer=Mathf.Clamp(b.Offer+d,0,Math.Max(0,count-1));b.Key=null;}
            }
            ScrollLiveDetail();
            return true;
        }
    }
}
