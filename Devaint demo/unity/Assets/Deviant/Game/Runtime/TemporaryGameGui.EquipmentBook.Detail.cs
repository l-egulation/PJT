using System;
using System.Linq;
using Deviant.Run;
using UnityEngine;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        void NativeDescription(EquipmentBook b,string origin,string title,Sprite icon,string stats,string description,int? grade=null,string note="",float x=32,float y=247,float height=90)
        {
            NativeIcon(icon,b.Body,x,y,58,58);
            Words(origin,b.Body,x+66,y,282,16,11,false,JMuted);
            var name=Words(title,b.Body,x+66,y+22,282,22,16,true);
            if(grade.HasValue)NativeIcon(Art("parts/ink-grade-"+grade),b.Body,x+66+Mathf.Min(246,name.preferredWidth+6),y+21,17,21);
            if(stats.Contains(" · ")){description="<b>"+stats.Replace(" · ","\n")+"</b>\n"+description;stats="";}
            Words(stats,b.Body,x+66,y+47,282,20,14,true).horizontalOverflow=UnityEngine.HorizontalWrapMode.Wrap;
            float contentHeight=Mathf.Max(18,height-69);
            var content=JournalScroll("Book effect description",b.Body,x,y+69,348,contentHeight);
            var text=Paragraph(content,0,0,344,13,18,JInk);text.text=Deviant.UI.Theme.ApprovedInkText.Display(description);
            float textHeight=Mathf.Max(contentHeight,LineCount(text)*18);Place(text,0,0,344,textHeight);content.sizeDelta=new Vector2(348,textHeight);
            if(note.Length>0)Words(note,b.Body,32,338,736,20,12,true,JWarning);
        }
        void NativeCompare(EquipmentBook b,Sprite before,string beforeName,string beforeStat,Sprite after,string afterName,string afterStat)
        {
            for(int i=0;i<2;i++)
            {
                float x=420+i*194;Words(i==0?"현재":"변경 후",b.Body,x,253,154,16,12,false,JMuted,TextAnchor.MiddleCenter);
                NativeIcon(i==0?before:after,b.Body,x+56,274,42,42);
                Words(i==0?beforeName:afterName,b.Body,x,322,154,19,14,true,JInk,TextAnchor.MiddleCenter);
                var value=(i==0?beforeStat:afterStat).Split(new[]{" · "},StringSplitOptions.None)[0];
                var stat=Words(value,b.Body,x,344,154,16,12,true,JInk,TextAnchor.MiddleCenter);stat.resizeTextForBestFit=true;stat.resizeTextMinSize=10;stat.resizeTextMaxSize=12;stat.verticalOverflow=VerticalWrapMode.Truncate;
            }
            Words("→",b.Body,584,290,20,24,18,false,JMuted,TextAnchor.MiddleCenter);
        }
        void NativeMainAction(EquipmentBook b,string text,Action action,bool enabled=true,int? price=null)=>
            NativeButton(b,"Book primary",text,b.Body,32,NativeActionY,NativeActionW,NativeActionH,action,enabled,true,price:price);
        void NativeItemActions(EquipmentBook b,bool ink,int sale)
        {
            bool changing=b.Action==BookAction.Recolor,confirming=b.Action==BookAction.Sell||b.Action==BookAction.Destroy;
            if(!changing&&b.Shop)NativeButton(b,"Book sell","판매",b.Body,213,NativeActionY,167,39,()=>b.Action=BookAction.Sell,!confirming,price:sale);
            if(!changing&&ink)NativeButton(b,"Book destroy","파괴",b.Body,420,NativeActionY,167,39,()=>b.Action=BookAction.Destroy,!confirming);
            NativeButton(b,"Book cancel","취소",b.Body,601,NativeActionY,167,39,()=>NativeCancel(b));
        }
        void NativeDetail(EquipmentBook b)
        {
            var g=NativeGrowth;var slot=NativeSlot(b);var ink=NativeInk(b);var stamp=NativeStamp(b);
            if(b.Tab==1&&!b.SlotService){NativeAugmentDetail(b,g);return;}
            if(ink!=null)
            {
                bool change=b.Action==BookAction.Recolor;bool duplicate=!ink.Part.duplicate&&NativePalette(b)!=null&&NativePalette(b).Slots.Where((s,i)=>i!=b.Slot).Any(s=>s.Part==ink.Part);
                bool fits=NativeFits(b,ink,b.Slot),canChange=change&&slot?.Part!=null&&ink.Part.color!=slot.Color&&!duplicate&&g.ColorChangesLeft>0&&g.Economy.Gold>=g.Data.colorChangeGold;
                string note=change?"기존 잉크는 가방으로 반환   색 변경 "+g.ColorChangesLeft+"회 남음":"";
                if(change&&slot?.Color==ink.Part.color)note="다른 색의 잉크 선택";
                NativeDescription(b,"보유 잉크",ink.Part.name,NativeDrop(ink.Part),"",ink.Part.description,ink.Part.grade,note);
                NativeCompare(b,NativeDrop(slot?.Part,slot?.Color),slot?.Part?.name??"미장착","",NativeDrop(ink.Part),ink.Part.name,"");
                string label=change?"색 바꾸고 장착":slot==null?"장착할 칸 선택":slot.Part==null?"장착 (무료)":"교체 (무료)";
                bool confirm=b.Action==BookAction.Sell||b.Action==BookAction.Destroy;
                if(confirm)label=b.Action==BookAction.Sell?"판매 확인":"파괴 확인";
                NativeMainAction(b,label,()=>NativeEquip(b),confirm||change?confirm||canChange:fits,change?(int?)g.Data.colorChangeGold:b.Action==BookAction.Sell?g.SalePrice:(int?)null);
                NativeItemActions(b,true,g.SalePrice);return;
            }
            if(stamp!=null)
            {
                var current=host.Arena.catalog.forms[host.Arena.Player.weapons[b.Weapon].FormIndex];int currentSkill=host.Arena.Player.weapons[b.Weapon].SpecialIndex;
                string nextForm=stamp.Kind==StampKind.Weapon?stamp.FormId:current.id;int nextSkill=stamp.Kind==StampKind.Skill?stamp.Skill:currentSkill;
                var equipped=NativeStash.Equipped(b.Weapon,stamp.Kind);int cost=NativeStash.CostOf(b.Weapon,stamp,g.Economy);bool same=stamp.SameEffect(equipped),fit=NativeStampFits(b,stamp);
                string name=stamp.Kind==StampKind.Weapon?host.Arena.catalog.forms.FirstOrDefault(f=>f.id==stamp.FormId)?.name??stamp.FormId:ForestCombatV2.Chapter1Rewards.SkillStampName(stamp.Skill);
                string note=b.Action==BookAction.Sell?"판매한 스탬프는 되돌릴 수 없음":equipped!=null&&!same?"교체하면 기존 스탬프 소멸":"";
                NativeDescription(b,stamp.Kind==StampKind.Weapon?"형태 스탬프":"스킬 스탬프",name,StampIcon(stamp),"변경 스킬 "+NativeSkillName(nextForm,nextSkill),NativeSkillDescription(nextForm,nextSkill),note:note);
                NativeCompare(b,GrowthPreviewIcon(current.id,stamp.Kind==StampKind.Weapon?-1:currentSkill),current.name,NativeSkillName(current.id,currentSkill),GrowthPreviewIcon(nextForm,stamp.Kind==StampKind.Weapon?-1:nextSkill),host.Arena.catalog.forms.FirstOrDefault(f=>f.id==nextForm)?.name??nextForm,NativeSkillName(nextForm,nextSkill));
                bool selling=b.Action==BookAction.Sell;
                NativeMainAction(b,selling?"판매 확인":!b.Shop?"정비소에서 적용":!fit?"호환 무기 선택":same?"적용 중":cost==0?"적용 (무료)":"교체",()=>NativeEquip(b),b.Shop&&(selling||fit&&!same&&g.Economy.Gold>=cost),selling?g.Economy.StampSaleGold:cost>0?(int?)cost:null);
                NativeItemActions(b,false,g.Economy.StampSaleGold);return;
            }
            if(b.Tab==2)
            {
                var form=host.Arena.catalog.forms[host.Arena.Player.weapons[b.Weapon].FormIndex];int skill=host.Arena.Player.weapons[b.Weapon].SpecialIndex;
                var mounted=NativeStash?.Equipped(b.Weapon,b.Skill?StampKind.Skill:StampKind.Weapon);
                NativeDescription(b,b.Skill?"장착 / 스킬":"장착 / 형태",b.Skill?NativeSkillName(form.id,skill):form.name,StampIcon(mounted)??ItemIcon(b.Skill?"backing-stamp-skill":"backing-stamp-form"),b.Skill?"현재 스킬":"현재 스킬 "+NativeSkillName(form.id,skill),NativeSkillDescription(form.id,skill),height:145);return;
            }
            if(slot==null)return;
            NativeDescription(b,"장착 / "+(b.Slot+1)+"번 칸",slot.Part?.name??ForestCombatV2.Chapter1MaintenanceShop.ColorName(slot.Color)+" 칸",NativeDrop(slot.Part,slot.Color),slot.Part==null?"슬롯 강화 +"+slot.Level:"",slot.Part?.description??"",slot.Part?.grade,height:b.Shop?100:145);
            if(b.Action==BookAction.Recolor){NativeButton(b,"Book cancel","취소",b.Body,601,361,167,39,()=>NativeCancel(b));return;}
            if(b.Shop)
            {
                int cost=g.UpgradeCost(NativeWeapon(b),b.Slot);bool max=slot.Level>=5;
                NativeButton(b,"Book upgrade",max?"최대 강화":"슬롯 강화",b.Body,32,361,167,39,()=>NativeUpgrade(b),!max&&g.Economy.Gold>=cost,price:max?(int?)null:cost);
                if(b.Tab==1)NativeCompare(b,NativeDrop(slot.Part,slot.Color),"강화 +"+slot.Level,slot.Part?.StatsAt(slot.Level)??"",NativeDrop(slot.Part,slot.Color),"강화 +"+Mathf.Min(5,slot.Level+1),slot.Part?.StatsAt(Mathf.Min(5,slot.Level+1))??"");
                else if(slot.Part!=null)NativeButton(b,"Book recolor","색 변경",b.Body,213,361,167,39,()=>{b.Action=BookAction.Recolor;b.Filter=1;b.Selected=Guid.Empty;b.TargetChosen=true;},g.ColorChangesLeft>0&&g.Economy.Gold>=g.Data.colorChangeGold,price:g.Data.colorChangeGold);
            }
            if(b.Tab==0&&slot.Part!=null)NativeButton(b,"Book unequip","해제",b.Body,302,259,78,34,()=>NativeTrade(b,()=>g.Detach(NativeWeapon(b),b.Slot)),g.HasInkRoom,frame:"WorkshopFrames/secondary-78x34");
        }
        void NativeGrowthPage(EquipmentBook b)
        {
            Words("강화할 항목",b.Body,32,30,348,20,15,true);
            NativeButton(b,"Growth attack service","증강 선택",b.Body,32,56,348,80,()=>{b.SlotService=false;b.Selected=Guid.Empty;},primary:!b.SlotService);
            NativeButton(b,"Growth slot service","팔레트 슬롯 강화",b.Body,32,146,348,80,()=>{b.SlotService=true;b.Selected=Guid.Empty;b.Slot=0;},primary:b.SlotService);
            Words(b.SlotService?"강화할 칸":"정비소 증강",b.Body,420,30,348,20,15,true);
            if(b.SlotService)
            {
                for(int i=0;i<2;i++){int w=i;NativeButton(b,"Growth weapon "+i,"무기 "+(i+1),b.Body,420+i*180,56,168,34,()=>{b.Weapon=w;b.Slot=0;},frame:b.Weapon==i?"weapon-tab-active":"weapon-tab");}
                NativePaletteCells(b,104,420);
            }
            else NativeAugmentPage(b,NativeGrowth);
        }
        void NativeStall(EquipmentBook b)
        {
            var stock=shop.InkStock;Words("잉크 후보",b.Body,32,30,348,20,15,true);
            if(stock.Count>3)Words("스크롤하여 전체 상품 보기",b.Body,155,30,225,20,12,false,JMuted,UnityEngine.TextAnchor.MiddleRight);
            if(stock.Count==0){Words("진열된 잉크 없음",b.Body,32,74,348,36,14,false,JMuted);return;}
            b.Offer=Mathf.Clamp(b.Offer,0,stock.Count-1);
            var shelf=JournalScroll("Ink shelf",b.Body,32,58,348,278);
            shelf.sizeDelta=new Vector2(348,Mathf.Max(278,Mathf.CeilToInt(stock.Count/3f)*174));
            for(int i=0;i<stock.Count;i++)
            {
                int index=i;var offer=stock[i];var cell=NativeButton(b,"Ink offer "+i,"",shelf,i%3*116,i/3*174,104,166,()=>b.Offer=index,frame:"WorkshopFrames/stock-104x166");
                NativeIcon(NativeDrop(offer.Part),cell.transform,9,8,86,86,offer.Sold?.3f:1);NativeIcon(Art("parts/ink-grade-"+offer.Part.grade),cell.transform,84,3,16,20);
                Words(offer.Part.name,cell.transform,4,100,96,20,14,true,JInk,TextAnchor.MiddleCenter);
                if(offer.Sold)Words("판매 완료",cell.transform,4,140,96,18,12,false,JMuted,TextAnchor.MiddleCenter);
                else{NativeIcon(CombatSprite("gold-coin"),cell.transform,20,140,16,16);Words(offer.Price.ToString(),cell.transform,42,136,58,24,13,true);}
            }
            var selected=stock[b.Offer];var p=selected.Part;
            NativeDescription(b,"판매 잉크",p.name,NativeDrop(p),"",p.description,p.grade,x:420,y:58,height:240);
            var economy=NativeGrowth.Economy;bool room=NativeGrowth.HasInkRoom;
            if(!selected.Sold&&(!room||economy.Gold<selected.Price))Words(!room?"가방이 가득 참":"골드 부족",b.Body,420,338,348,20,12,true,JWarning);
            NativeButton(b,"Book buy",selected.Sold?"판매 완료":"구매",b.Body,601,361,167,39,()=>{bool ok=shop.TryBuyInk(b.Offer,out var error);b.Message=error;PlayEquipResult(ok);},!selected.Sold&&room&&economy.Gold>=selected.Price,true,price:selected.Sold?(int?)null:selected.Price);
            NativeButton(b,"Ink shelf reroll",RerollText,b.Body,420,361,167,39,()=>{b.Message=shop.RerollInks()?"잉크 진열을 바꿨습니다.":"새로고침할 수 없습니다.";b.Offer=0;},RerollsLeft>0&&stock.Any(o=>!o.Sold));
        }
    }
}
