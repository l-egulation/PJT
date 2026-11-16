using System;
using System.Linq;
using Deviant.Run;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        enum BookAction { Inspect, Recolor, Sell, Destroy }
        sealed class EquipmentBook
        {
            public bool Shop, SlotService, Skill, TargetChosen;
            public int Tab, Weapon, Slot, Filter, Offer;
            public Guid Selected;
            public BookAction Action;
            public GameObject Root;
            public RectTransform Body;
            public Text Gold;
            public Button[] Tabs;
            public string Key, Message="";
        }
        EquipmentBook nativeBag,nativeShop;
        const float NativeActionY=361,NativeActionW=167,NativeActionH=39;
        WeaponGrowth NativeGrowth=>host.Growth;
        RunStash NativeStash=>shop?.Service?.Stash;
        Guid NativeWeapon(EquipmentBook b)=>host.Arena.Player.weapons[b.Weapon].InstanceId;
        GrowthPalette NativePalette(EquipmentBook b)=>NativeGrowth?.Equipped(NativeWeapon(b));
        GrowthSlot NativeSlot(EquipmentBook b){var p=NativePalette(b);return p!=null&&b.Slot>=0&&b.Slot<p.Slots.Count?p.Slots[b.Slot]:null;}
        GrowthInk NativeInk(EquipmentBook b)=>NativeGrowth?.Inks.FirstOrDefault(i=>i.Id==b.Selected);
        RunStamp NativeStamp(EquipmentBook b)=>NativeStash?.Stamps.FirstOrDefault(s=>s.Id==b.Selected);
        Sprite NativeDrop(GrowthPart part,string color=null)=>DropArt(part,color)??(part==null?CombatSprite("slot-empty"):null);

        Image NativeArt(string art,Transform parent,float x,float y,float w,float h,bool hit=false)
        {
            var image=Face(art,parent,x,y,w,h,raycast:hit);
            if(art=="bag-slot"||art=="weapon-tab"||art=="weapon-tab-active"||art=="close-button"||art=="close-icon")image.sprite=BagArt(art);
            image.preserveAspect=true;return image;
        }
        Button NativeButton(EquipmentBook b,string name,string text,Transform parent,float x,float y,float w,float h,Action action,bool enabled=true,bool primary=false,string frame=null,int? price=null)
        {
            var image=NativeArt(frame??(primary?"primary":"secondary"),parent,x,y,w,h,true);image.name=name;
            image.color=new Color(1,1,1,enabled?1:.5f);
            var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;button.transition=Selectable.Transition.None;button.interactable=enabled;
            var label=Words(text,image.transform,0,0,w,h,14,true,primary?JPaperText:JInk,TextAnchor.MiddleCenter);
            if(price.HasValue)SetActionGold(button,price,true);
            button.onClick.AddListener(()=>{if(!button.interactable)return;action();b.Key=null;Refresh();});return button;
        }
        void NativeIcon(Sprite sprite,Transform parent,float x,float y,float w,float h,float alpha=1)
        {if(!sprite)return;var image=Glyph(sprite,parent,x,y,w,h,new Color(1,1,1,alpha));image.preserveAspect=true;}
        void BuildNativeBook(bool maintenance)
        {
            if (Sketch != null || host.Arena.SketchEquipment) { if(maintenance)BuildApprovedSketchShop();else BuildApprovedSketchBook(); return; }
            var b=new EquipmentBook{Shop=maintenance};
            var root=NativeArt("WorkshopFrames/book-800x440",stage,BookX,BookY,BookW,BookH,true);b.Root=root.gameObject;
            root.name=maintenance?"Maintenance equipment book":"Inventory equipment book";
            NativeArt("WorkshopFrames/title-220x36",root.transform,290,-62,220,36);
            Words(maintenance?"상점":"인벤토리",root.transform,290,-62,220,36,18,true,JPaperText,TextAnchor.MiddleCenter);
            var goldRoot=Rect("Book gold",root.transform,680,-62,80,36);
            NativeIcon(CombatSprite("gold-coin"),goldRoot,0,9,18,18);b.Gold=Words("",goldRoot,24,0,56,36,17,true);
            var close=NativeButton(b,"Close book","",root.transform,764,-62,36,36,()=>{b.Selected=Guid.Empty;b.Action=BookAction.Inspect;if(maintenance)shop?.Close();else Presenter.Close();},frame:"close-button");
            NativeIcon(BagArt("close-icon"),close.transform,10,10,16,16);
            string[] names=maintenance?new[]{"팔레트","성장","스탬프","잉크 판매대"}:new[]{"팔레트","스탬프"};
            b.Tabs=new Button[names.Length];float start=(800-names.Length*150.857143f-(names.Length-1)*8)/2;
            for(int i=0;i<names.Length;i++)
            {
                int tab=maintenance?i:i*2;
                b.Tabs[i]=NativeButton(b,"Book tab "+tab,names[i],root.transform,start+i*158.857143f,-18,150.857143f,35.2f,()=>{b.Tab=tab;b.Selected=Guid.Empty;b.Action=BookAction.Inspect;b.Filter=tab==2?2:1;b.Message="";},frame:"tab");
                var text=b.Tabs[i].GetComponentInChildren<Text>();text.fontSize=16;float labelWidth=text.preferredWidth,groupX=(150.857143f-labelWidth-27)/2;
                NativeIcon(tab==0?ItemIcon("backing-ink-blue"):tab==1?Art("sword"):tab==2?ItemIcon("backing-stamp-form"):CombatSprite("gold-coin"),b.Tabs[i].transform,groupX,5,22,25);
                Place(text,groupX+27,0,labelWidth,35.2f);
            }
            b.Body=Rect("Book content",root.transform,0,0,BookW,BookH);
            if(maintenance){nativeShop=b;shopPanel=b.Root;shopGoldChip=goldRoot.gameObject;}else{nativeBag=b;bag=b.Root;}
            RegisterScreen(b.Root,close);
        }
        void RefreshNativeBook(EquipmentBook b)
        {
            if (Sketch != null) { RefreshSketchBook(b); return; }
            if(b.Shop?shop==null||!shop.IsOpen:!Presenter.IsOpen){b.Key=null;if(!b.Shop)inkGuideTabSet=false;return;}
            // Sketch runs: the bag key opens the sketch bag instead, and the shop keeps only its growth (augment) tab.
            bool sketch=host.Arena&&host.Arena.SketchEquipment;
            if(sketch&&!b.Shop){Presenter.Close();b.Key=null;return;}
            if(b.Shop)for(int i=0;i<b.Tabs.Length;i++)b.Tabs[i].gameObject.SetActive(!sketch||i==1);
            if(sketch&&b.Shop&&b.Tab!=1){b.Tab=1;b.Selected=Guid.Empty;}
            if(!b.Shop)RefreshInkGuideBag();
            var g=NativeGrowth;if(g==null)return;
            var stash=NativeStash;
            string key=$"{b.Tab}/{b.Weapon}/{b.Slot}/{b.Filter}/{b.Selected}/{b.Action}/{b.Offer}/{b.SlotService}/{b.Skill}/{g.Revision}/{g.Economy.Gold}/{b.Message}/"+
                string.Join("/",stash?.Stamps.Select(s=>s.Id.ToString())??Array.Empty<string>())+string.Join("/",host.Arena.Player.weapons.Select(w=>w.FormIndex+":"+w.SpecialIndex));
            if(b.Shop)key+=string.Join("/",shop.InkStock.Select(o=>o.Part.id+":"+o.Price+":"+o.Sold));
            if(key==b.Key){if(!b.Shop)RefreshInkGuideBag();return;}b.Key=key;b.Gold.text=g.Economy.Gold.ToString();
            if(b.Selected!=Guid.Empty&&NativeInk(b)==null&&NativeStamp(b)==null)b.Selected=Guid.Empty;
            for(int i=0;i<b.Tabs.Length;i++)
            {
                bool active=(b.Shop?i:i*2)==b.Tab;var button=b.Tabs[i];button.image.sprite=Art(active?"tab-active":"tab");
                button.GetComponentInChildren<Text>().color=active?JPaperText:JInk;
            }
            string focus=EventSystem.current?.currentSelectedGameObject?.name;
            if(!b.Shop) ClearInkGlossaryTarget();
            ClearGrowth(b.Body);
            if(b.Tab==3)NativeStall(b);
            else{
                if(b.Tab==1)NativeGrowthPage(b);else{NativeEquipment(b);NativeStorage(b);}
                Fill(b.Body,LeftX,236,ColumnW,1,JRule);Fill(b.Body,RightX,244,ColumnW,1,JRule);
                NativeDetail(b);
            }
            if(!b.Shop)RefreshInkGuideBag();
            if(!string.IsNullOrEmpty(b.Message))Words(b.Message,b.Body,32,405,736,16,11,false,JMuted);
            if(focus!=null&&EventSystem.current){var buttons=b.Body.GetComponentsInChildren<Button>();var next=buttons.FirstOrDefault(x=>x.name==focus&&x.interactable)??buttons.FirstOrDefault(x=>x.interactable);if(next)EventSystem.current.SetSelectedGameObject(next.gameObject);}
        }
        void NativeEquipment(EquipmentBook b)
        {
            Words("장착 장비",b.Body,32,30,348,20,15,true);
            for(int i=0;i<2;i++)
            {
                int weapon=i;
                var button=NativeButton(b,"Book weapon "+i,"무기 "+(i+1)+" "+host.WeaponName(i),b.Body,32+i*180,56,168,34,()=>{b.Weapon=weapon;b.Slot=0;b.Action=BookAction.Inspect;if(!b.Shop)Presenter.SelectWeapon(weapon);},frame:b.Weapon==i?"weapon-tab-active":"weapon-tab");
                button.GetComponentInChildren<Text>().color=b.Weapon==i?JPaperText:JInk;
                var form=host.Arena.catalog.forms[host.Arena.Player.weapons[i].FormIndex];NativeIcon(CombatSprite("weapon-disc"),button.transform,9,5,24,24);NativeIcon(GrowthPreviewIcon(form.id,-1),button.transform,12,8,18,18);
                Place(button.GetComponentInChildren<Text>(),34,0,130,34);
            }
            if(b.Tab==2)
            {
                var form=host.Arena.catalog.forms[host.Arena.Player.weapons[b.Weapon].FormIndex];
                for(int i=0;i<2;i++)
                {
                    bool skill=i==1;var stamp=NativeStash?.Equipped(b.Weapon,skill?StampKind.Skill:StampKind.Weapon);
                    Words(skill?"스킬":"형태",b.Body,32+i*180,98,168,20,12,false,JMuted,TextAnchor.MiddleCenter);
                    var cell=NativeButton(b,"Mounted stamp "+i,"",b.Body,32+i*180,120,168,86,()=>{b.Skill=skill;b.Selected=Guid.Empty;b.Action=BookAction.Inspect;},frame:null);
                    cell.image.color=Color.clear;
                    NativeIcon(stamp!=null?StampIcon(stamp):ItemIcon(skill?"backing-stamp-skill":"backing-stamp-form"),cell.transform,35,0,98,86,stamp!=null?1:.25f);
                    Words(skill?NativeSkillName(form.id,host.Arena.Player.weapons[b.Weapon].SpecialIndex):form.name,b.Body,32+i*180,210,168,20,14,true,JInk,TextAnchor.MiddleCenter);
                }
            }
            else NativePaletteCells(b,104);
        }
        void NativePaletteCells(EquipmentBook b,float y,float x=32)
        {
            var p=NativePalette(b);if(p==null){Words("팔레트 없음",b.Body,x,y,348,60,14,false,JMuted);return;}
            Words("장착 잉크",b.Body,x,y-4,240,20,12,false,JMuted);
            Words(p.Slots.Count(s=>s.Part!=null)+" / "+p.Slots.Count,b.Body,x+268,y-4,80,20,12,false,JMuted,TextAnchor.MiddleRight);
            float width=Mathf.Min(82,(348-(p.Slots.Count-1)*10)/p.Slots.Count),start=x+(348-p.Slots.Count*width-(p.Slots.Count-1)*10)/2;
            for(int i=0;i<p.Slots.Count;i++)
            {
                int index=i;var s=p.Slots[i];var button=NativeButton(b,"Palette slot "+i,"",b.Body,start+i*(width+10),y+24,width,width*96/82,()=>{b.Slot=index;b.TargetChosen=true;if(b.Action!=BookAction.Recolor)b.Action=BookAction.Inspect;},frame:"WorkshopFrames/palette-82x96");
                if(!b.Shop && b.Tab==0 && b.TargetChosen && b.Slot==i && b.Selected==Guid.Empty)
                    SetInkGlossaryTarget(button, s.Part);
                NativeIcon(NativeDrop(s.Part,s.Color),button.transform,2,4,width-4,width-4,s.Part!=null?1:.25f);
                Words((i+1).ToString(),button.transform,5,3,20,14,11,false,JMuted);
                if(s.Level>0)Words("+"+s.Level,button.transform,width-25,3,22,14,11,true,JMuted,TextAnchor.MiddleRight);
                if(b.Slot==i){var outline=button.gameObject.AddComponent<Outline>();outline.effectColor=b.Selected!=Guid.Empty?new Color32(67,126,124,255):new Color32(191,118,37,255);outline.effectDistance=new Vector2(2,-2);}
            }
        }
        void NativeStorage(EquipmentBook b)
        {
            Words("보유 아이템",b.Body,420,30,260,18,15,true);
            Words("잉크 "+host.TotalStored+" / "+host.Inventory.Capacity+"    스탬프 "+(NativeStash?.Stamps.Count??0),b.Body,562,30,206,18,12,false,JMuted,TextAnchor.MiddleRight);
            string[] filters={"전체","잉크","스탬프"};
            for(int i=0;i<3;i++){int f=i;var filter=NativeButton(b,"Bag filter "+i,filters[i],b.Body,420+i*83,54,78,34,()=>b.Filter=f,frame:"WorkshopFrames/secondary-78x34");filter.GetComponentInChildren<Text>().color=b.Filter==i?JPaperText:JInk;if(b.Filter==i)filter.image.color=new Color32(50,49,40,255);}
            var content=JournalScroll("Book owned items",b.Body,420,94,348,148);int n=0;
            if(b.Filter!=2)foreach(var ink in NativeGrowth.Inks){var copy=ink;NativeOwnedCell(b,content,n++,ink.Id,DropArt(ink.Part),ink.Part.grade,()=>NativePick(b,copy.Id,false),ink.Part);}
            if(b.Filter!=1&&b.Action!=BookAction.Recolor&&NativeStash!=null)foreach(var stamp in NativeStash.Stamps){var copy=stamp;NativeOwnedCell(b,content,n++,stamp.Id,StampIcon(stamp),null,()=>NativePick(b,copy.Id,true));}
            content.sizeDelta=new Vector2(348,Mathf.Max(148,6+Mathf.CeilToInt(n/4f)*74));
            if(n==0)Words("보유 아이템 없음",content,0,24,348,24,13,false,JMuted,TextAnchor.MiddleCenter);
        }
        void NativeOwnedCell(EquipmentBook b,Transform content,int index,Guid id,Sprite icon,int? grade,Action pressed,GrowthPart part=null)
        {
            var cell=NativeButton(b,"Owned item "+id,"",content,6+index%4*80,6+index/4*74,72,65.454545f,pressed,frame:"bag-slot");
            if(!b.Shop && b.Tab==0 && b.Selected==id) SetInkGlossaryTarget(cell, part);
            NativeIcon(icon,cell.transform,6,2,60,60);if(grade.HasValue)NativeIcon(Art("parts/ink-grade-"+grade),cell.transform,52,3,16,20);
            if(b.Selected==id){var outline=cell.gameObject.AddComponent<Outline>();outline.effectColor=new Color32(191,118,37,255);outline.effectDistance=new Vector2(2,-2);}
        }
        string NativeSkillName(string form,int skill)=>host.Arena.catalog.forms.FirstOrDefault(f=>f.id==form)?.specials.ElementAtOrDefault(skill)?.name??"기본 스킬";
        string NativeSkillDescription(string form,int skill)=>host.Arena.catalog.forms.FirstOrDefault(f=>f.id==form)?.specials.ElementAtOrDefault(skill)?.description??"";
    }
}
