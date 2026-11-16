using System;
using System.Linq;
using Deviant.Run;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    // Equipment management: current palette and stamps on the left, shared storage and a contextual action on
    // the right. Stamp changes show their actual before/after names. Every action still goes through the shop.
    public sealed partial class TemporaryGameGui
    {
        GameObject growthPanel;
        RectTransform growthSlots, growthPalettes, growthInks, growthDetailContent;
        Text growthMessage, growthDetailName, growthDescription, growthCost, growthRule, growthUpgradePrice;
        Image growthDetailDrop;
        Button growthInsert, growthRecolor, growthSell, growthDestroy, growthUpgrade;
        readonly Button[] growthWeapons = new Button[2];
        // Colored inks and stamps share one storage; the last two filters show weapon and skill stamps.
        readonly Button[] growthFilters = new Button[8];
        readonly string[] growthColors = {null,"red","yellow","green","blue","purple"};
        const int WeaponStampFilter=6, SkillStampFilter=7;
        bool growthOpen, growthDirty=true, growthInspectMounted=true;
        // Ink first (playtest 2026-11-04): 장착 with no slot chosen yet waits for a slot; the next fitting slot clicked takes the ink.
        bool growthSlotPicked, growthPickSlot;
        public bool GrowthPickingSlot => growthPickSlot;
        int growthWeapon, growthSlot, growthFilter, growthLastGold=-1, growthLastRevision=-1;
        Guid growthSelectedInk;
        // 색 변경 mode (palette tab, user decision 2026-11-09): the same spread with only the colour change action; no
        // slot upgrade, equip, sale, destruction or stamps. 슬롯 강화 opens the full management mode.
        bool growthRecolorMode;
        Text growthHeading;

        void BuildGrowthPanel()
        {
            growthPanel=Book("장비 관리",true,out growthHeading,out var close);
            var page=growthPanel.transform;
            for(int i=0;i<2;i++)
            {int w=i;growthWeapons[i]=Tab("",page,LeftX+i*176,TopY,172,36,14,()=>{growthWeapon=w;growthSlot=0;growthSlotPicked=growthPickSlot=false;growthInspectMounted=true;growthDirty=true;});}
            Words("스탬프",page,LeftX,80,ColumnW,24,18,true);
            growthPalettes=Rect("장착 스탬프",page,LeftX,106,ColumnW,84);
            Fill(page,LeftX,202,ColumnW,1,JRule);
            growthPaletteButton=PaletteInfoButton(page,LeftX,212,ColumnW,SelectGrowthPalette,out growthPaletteCount);
            growthSlots=Rect("장착 슬롯",page,LeftX,244,ColumnW,88);
            growthUpgrade=Action("슬롯 강화",page,LeftX,340,94,36,()=>GrowthTrade(()=>shop.Growth.Upgrade(shop.GrowthWeapon(growthWeapon),growthSlot)),true);
            growthUpgradePrice=Price(page,LeftX+263,344,28,22,"");
            growthMessage=Words("",page,LeftX,396,ColumnW,20,14,true);
            Words("보관함",page,RightX,30,ColumnW,20,16,true);
            float filterStep=(ColumnW+3.6f)/growthFilters.Length,filterW=filterStep-3.6f;
            for(int i=0;i<growthFilters.Length;i++)
            {
                int f=i;growthFilters[i]=Tab(i==0?"전체":i==WeaponStampFilter?"형태":i==SkillStampFilter?"스킬":"",page,RightX+i*filterStep,56,filterW,28,13,()=>{growthFilter=f;growthDirty=true;});
                if(i>0&&i<WeaponStampFilter)Glyph(AffinityChip,growthFilters[i].transform,(filterW-12)/2,8,12,12,GrowthColor(growthColors[i]));
            }
            growthInks=JournalScroll("보관 아이템",page,RightX,94,ColumnW,104);
            growthDetailDrop=Drop(page,null,null,RightX,208,40,56);growthDetailDrop.name="Detail drop";
            growthDetailName=Words("",page,RightX+52,213,296,22,18,true);
            growthDetailContent=JournalScroll("잉크 효과",page,RightX+52,241,296,78);
            growthDescription=Paragraph(growthDetailContent,0,0,296,14,20,JInk);
            growthRule=Words("",page,RightX,328,ColumnW,18,13,false,JInk);
            growthCost=Words("",page,RightX,348,ColumnW,18,13,false,JWarning);
            growthInsert=Action("장착",page,RightX,368,84,44,GrowthInsert,true);
            growthRecolor=Action("색 변경",page,RightX+88,368,84,44,()=>GrowthTrade(()=>{var ink=SelectedGrowthInk();if(ink!=null)shop.Growth.Recolor(shop.GrowthWeapon(growthWeapon),growthSlot,ink.Part.color,ink.Id);}));
            growthSell=Action("판매",page,RightX+176,368,84,44,()=>GrowthTrade(()=>shop.Growth.Sell(growthSelectedInk)));
            // Explicit destruction of an owned, unmounted ink; mounted parts have no destroy button.
            growthDestroy=Action("파괴",page,RightX+264,368,84,44,()=>GrowthTrade(()=>shop.Growth.Destroy(growthSelectedInk)));
            growthStampPreview=Rect("스탬프 변경 미리보기",page,RightX,258,ColumnW,90);
            growthStampPreview.gameObject.SetActive(false);
            foreach(var b in new[]{growthInsert,growthRecolor,growthSell,growthDestroy})b.GetComponentInChildren<Text>().fontSize=14;
            RegisterScreen(growthPanel,close);
        }
        // faithful.css .affinity-chip: an octagon (corners cut at 25%).
        static Sprite AffinityChip => Drawn("affinity", 12, 12, (x, y) =>
            Mathf.Abs(x - 6) + Mathf.Abs(y - 6) <= 9 && x >= 0 && x <= 12 && y >= 0 && y <= 12 ? Color.white : Color.clear);

        static void ClearGrowth(Transform content)
        {foreach(Transform child in content){child.gameObject.SetActive(false);Destroy(child.gameObject);}}
        GrowthInk SelectedGrowthInk()=>shop?.Growth?.Inks.FirstOrDefault(i=>i.Id==growthSelectedInk);
        RunStamp SelectedStamp()=>shop?.Service?.Stash.Stamps.FirstOrDefault(s=>s.Id==growthSelectedInk);
        // A selected stamp goes onto the chosen weapon; a selected ink goes into the chosen palette slot.
        void GrowthInsert()
        {
            if(!growthOpen||!shop||!shop.IsOpen)return;
            if(!growthInspectMounted&&SelectedStamp() is RunStamp stamp)
            {
                bool accepted=shop.TryApplyStamp(growthWeapon,stamp.Id,out var error);
                transactionMessage=accepted?"스탬프를 적용했습니다.":error;
                ForestCombatV2.PresentationSfx.PlaySelected(accepted?"sfx_items_stamp":"sfx_ui_reject");
                growthDirty=true;return;
            }
            if(!growthSlotPicked||!GrowthFits(growthSlot)){growthPickSlot=true;transactionMessage="";growthDirty=true;return;}
            GrowthInsertInto(growthSlot);
        }
        void GrowthInsertInto(int slot){growthPickSlot=false;growthSlot=slot;growthSlotPicked=true;GrowthTrade(()=>shop.Growth.Insert(shop.GrowthWeapon(growthWeapon),slot,growthSelectedInk));}
        // Whether the selected owned ink can go into this slot of the shown weapon: the slot's colour (or colourless)
        // and no second copy of a non-duplicate ink.
        bool GrowthFits(int slot)
        {
            var ink=growthInspectMounted?null:SelectedGrowthInk();var palette=shop?.Growth?.Equipped(shop.GrowthWeapon(growthWeapon));
            if(ink==null||palette==null||slot<0||slot>=palette.Slots.Count)return false;var target=palette.Slots[slot];
            return (target.Color=="colorless"||target.Color==ink.Part.color)&&(ink.Part.duplicate||!palette.Slots.Where((s,i)=>i!=slot).Any(s=>s.Part==ink.Part));
        }
        void OpenStampStorage(int weapon,bool skill){growthInspectPalette=false;growthRecolorMode=false;growthWeapon=weapon;growthSlot=0;growthFilter=skill?SkillStampFilter:WeaponStampFilter;growthInspectMounted=false;growthSelectedInk=Guid.Empty;growthOpen=true;growthDirty=true;}
        // Palette tab: the colour-change spread for one weapon, starting on its first mounted slot.
        void OpenRecolor(int weapon)
        {
            growthInspectPalette=false;growthRecolorMode=true;growthWeapon=weapon;growthFilter=0;growthSelectedInk=Guid.Empty;growthPickSlot=false;
            var palette=shop?.Growth?.Equipped(shop.GrowthWeapon(weapon));
            int first=-1;if(palette!=null)for(int i=0;i<palette.Slots.Count&&first<0;i++)if(palette.Slots[i].Part!=null)first=i;
            growthSlot=Math.Max(0,first);growthSlotPicked=first>=0;growthInspectMounted=true;growthOpen=true;growthDirty=true;
        }
        // 성장 → 슬롯 강화: the full management spread (equip, slot upgrade, colour change, sale, destruction, stamps).
        void OpenManagement(){growthInspectPalette=false;growthRecolorMode=false;growthPickSlot=false;growthInspectMounted=true;growthOpen=true;growthDirty=true;}
        void GrowthTrade(Action action)
        {
            if(!growthOpen||!shop||!shop.IsOpen)return;
            bool done=shop.GrowthTransaction(action,out var error);
            transactionMessage=done?"완료했습니다.":error;
            // Insert/recolour consumes the selected owned instance; immediately show the mounted result.
            if(done&&growthSelectedInk!=Guid.Empty&&!shop.Growth.Inks.Any(i=>i.Id==growthSelectedInk))growthInspectMounted=true;
            growthDirty=true;
        }
        // 파츠 전용 아이콘이 없으면 데이터의 색상별 임시 아이콘을 쓴다.
        string InkIcon(Deviant.Run.GrowthPart part)=>Sprite("ink-"+part.id)||string.IsNullOrEmpty(part.icon)?"ink-"+part.id:part.icon;
        static Color GrowthColor(string color)=>color switch
        {
            "red"=>new Color32(190,59,57,255),"yellow"=>new Color32(192,146,31,255),
            "green"=>new Color32(56,131,80,255),"blue"=>new Color32(42,112,170,255),
            "purple"=>new Color32(130,75,155,255),_=>new Color32(114,111,100,255)
        };

        void RefreshGrowthPanel()
        {
            if(!growthOpen||!shop||shop.Growth==null)return;
            var g=shop.Growth;
            growthMessage.text=Deviant.UI.Theme.ApprovedInkText.Display(transactionMessage);
            if(growthLastGold!=g.Economy.Gold||growthLastRevision!=g.Revision)
            {growthLastGold=g.Economy.Gold;growthLastRevision=g.Revision;growthDirty=true;}
            if(!growthDirty)return;growthDirty=false;
            Guid weapon=shop.GrowthWeapon(growthWeapon);var palette=g.Equipped(weapon);
            growthPaletteButton.interactable=palette!=null;
            growthPaletteCount.text=palette!=null?$"{palette.Slots.Count}칸 / 정보 보기":"";
            for(int i=0;i<2;i++){growthWeapons[i].GetComponentInChildren<Text>().text=$"무기 {i+1} {host.WeaponName(i)}";SetTab(growthWeapons[i],i==growthWeapon);}
            if(growthRecolorMode&&growthFilter>=WeaponStampFilter)growthFilter=0;
            for(int i=0;i<growthFilters.Length;i++){SetTab(growthFilters[i],i==growthFilter);growthFilters[i].gameObject.SetActive(!growthRecolorMode||i<WeaponStampFilter);}
            growthHeading.text=growthRecolorMode?"색 변경":"장비 관리";FitTitle(growthHeading);
            ClearGrowth(growthSlots);ClearGrowth(growthPalettes);ClearGrowth(growthInks);
            if(palette==null)Words("팔레트 없음",growthSlots,0,0,ColumnW,88,16,true,JMuted,TextAnchor.MiddleCenter);
            else
            {
                growthSlot=Mathf.Clamp(growthSlot,0,palette.Slots.Count-1);
                int n=palette.Slots.Count;float w=Mathf.Floor((ColumnW-(n-1)*6)/n);
                for(int i=0;i<n;i++)
                {
                    int index=i;var slot=palette.Slots[i];
                    // In 색 변경 mode a chosen ink stays chosen while the slot to recolour is picked, in either order.
                    var b=SlotButton("GrowthSlot"+i,growthSlots,i*(w+6),0,w,88,()=>{growthInspectPalette=false;if(growthPickSlot){if(GrowthFits(index))GrowthInsertInto(index);return;}growthSlot=index;growthSlotPicked=true;growthInspectMounted=!(growthRecolorMode&&SelectedGrowthInk()!=null);growthDirty=true;});
                    // While waiting for a slot, every slot the ink fits is ringed and the others fade.
                    bool fits=growthPickSlot&&GrowthFits(i);
                    SetSlot(b.image,growthPickSlot?fits:!growthInspectPalette&&i==growthSlot);
                    if(growthPickSlot&&!fits)b.image.color=new Color(1,1,1,.45f);
                    float iconSize=Mathf.Min(76,w-12),iconTop=(88-iconSize)/2;
                    if(slot.Part==null&&slot.Color=="colorless")Words("무색",b.transform,0,iconTop,w,iconSize,12,true,JMuted,TextAnchor.MiddleCenter);
                    else
                    {
                        Drop(b.transform,slot.Part,slot.Color,Mathf.Round((w-iconSize)/2),iconTop,iconSize,iconSize);
                    }
                    SetSlotLevel(b.transform, slot.Level);
                    Words((i+1).ToString(),b.transform,6,3,18,16,11,true,JMuted);
                }
            }
            int level=palette==null?0:palette.Slots[growthSlot].Level;
            bool maxed=level>=5;int upgradeCost=palette==null?0:g.UpgradeCost(shop.GrowthWeapon(growthWeapon),growthSlot);
            growthUpgrade.GetComponentInChildren<Text>().text=maxed?"최대 강화":"슬롯 강화";
            growthUpgrade.interactable=palette!=null&&!maxed&&g.Economy.Gold>=upgradeCost;Paint(growthUpgrade,true);
            growthUpgradePrice.text=upgradeCost.ToString();growthUpgradePrice.enabled=!maxed&&!growthRecolorMode;growthUpgradePrice.transform.parent.Find("Coin").gameObject.SetActive(!maxed&&!growthRecolorMode);
            growthUpgrade.gameObject.SetActive(!growthRecolorMode);
            // The price sits flush with the page's right edge.
            float priceW=Width(growthUpgradePrice);
            ((RectTransform)growthUpgradePrice.transform.parent.Find("Coin")).anchoredPosition=new Vector2(LeftX+ColumnW-priceW-34,-344);
            growthUpgradePrice.rectTransform.anchoredPosition=new Vector2(LeftX+ColumnW-priceW,-344);
            var stash=shop.Service.Stash;
            var items=growthFilter>=WeaponStampFilter?new GrowthInk[0]:g.Inks.Where(i=>growthFilter==0||i.Part.color==growthColors[growthFilter]).ToArray();
            var stamps=growthRecolorMode?new RunStamp[0]:growthFilter==0||growthFilter>=WeaponStampFilter?stash.Stamps.Where(s=>growthFilter==0||s.Kind==(growthFilter==WeaponStampFilter?StampKind.Weapon:StampKind.Skill)).ToArray():new RunStamp[0];
            if(!items.Any(i=>i.Id==growthSelectedInk)&&!stamps.Any(s=>s.Id==growthSelectedInk))growthSelectedInk=items.Length>0?items[0].Id:stamps.Length>0?stamps[0].Id:Guid.Empty;
            for(int i=0;i<items.Length;i++)
            {
                var ink=items[i];var b=SlotButton("GrowthInk"+ink.Id,growthInks,4+i%5*69,4+i/5*70,64,60,()=>{growthInspectPalette=false;growthSelectedInk=ink.Id;growthPickSlot=false;growthInspectMounted=false;growthDirty=true;});
                SetSlot(b.image,!growthInspectMounted&&ink.Id==growthSelectedInk);
                var icon = Drop(b.transform,ink.Part,null,8,4,48,52);
                SetInkGrade(icon, ink.Part.grade);
            }
            for(int k=0;k<stamps.Length;k++)
            {
                int i=items.Length+k;var stamp=stamps[k];
                var b=SlotButton("GrowthStamp"+stamp.Id,growthInks,4+i%5*69,4+i/5*70,64,60,()=>{growthInspectPalette=false;growthSelectedInk=stamp.Id;growthPickSlot=false;growthInspectMounted=false;growthDirty=true;});
                SetSlot(b.image,!growthInspectMounted&&stamp.Id==growthSelectedInk);
                StampItem(b.transform,stamp,6,4,52,52);
            }
            int shown=items.Length+stamps.Length;
            if(shown==0)Words(growthFilter>=WeaponStampFilter?"보관한 스탬프가 없습니다":"보유한 잉크가 없습니다",growthInks,0,0,ColumnW,60,14,true,JMuted,TextAnchor.MiddleCenter);
            growthInks.sizeDelta=new Vector2(ColumnW,Mathf.Max(104,8+Mathf.CeilToInt(shown/5f)*70-10));
            RefreshGrowthStampSlots();
            if(!growthInspectMounted&&SelectedStamp() is RunStamp chosenStamp){RefreshStampDetail(chosenStamp,stash,g.Economy);return;}
            growthStampPreview.gameObject.SetActive(false);
            growthDetailContent.parent.gameObject.SetActive(true);
            Place(growthRule,RightX,328,ColumnW,18);
            if(growthInspectPalette&&palette!=null)
            {
                ShowPaletteInfo(growthDetailName,growthDescription,growthDetailDrop,palette,growthWeapon);
                float height=Mathf.Max(1,LineCount(growthDescription))*20;
                Place(growthDescription,0,0,296,height);growthDetailContent.sizeDelta=new Vector2(296,Mathf.Max(78,height));
                growthDetailContent.parent.GetComponent<ScrollRect>().verticalNormalizedPosition=1;
                growthRule.text=growthCost.text="";
                foreach(var action in new[]{growthInsert,growthRecolor,growthSell,growthDestroy,growthUpgrade})action.gameObject.SetActive(false);
                growthUpgradePrice.enabled=false;growthUpgradePrice.transform.parent.Find("Coin").gameObject.SetActive(false);
                return;
            }
            var selected=growthInspectMounted?null:SelectedGrowthInk();var target=palette?.Slots[growthSlot];
            var detail=growthInspectMounted?target?.Part:selected?.Part;
            growthDetailDrop.sprite=detail==null?null:DropArt(detail);growthDetailDrop.enabled=detail!=null;growthDetailDrop.color=Color.white;
            growthDetailName.text=detail?.name??(growthInspectMounted&&target!=null?"빈 슬롯":"");
            PaintInkDetailGrade(growthDetailName,detail);
            growthDescription.text=Deviant.UI.Theme.ApprovedInkText.Display(detail?.description??"");
            float descH=Mathf.Max(1,LineCount(growthDescription))*20;Place(growthDescription,0,0,296,descH);
            growthDetailContent.sizeDelta=new Vector2(296,Mathf.Max(78,descH));
            growthDetailContent.parent.GetComponent<ScrollRect>().verticalNormalizedPosition=1;
            bool duplicate=selected!=null&&palette!=null&&!selected.Part.duplicate&&palette.Slots.Where((s,i)=>i!=growthSlot).Any(s=>s.Part==selected.Part);
            bool matches=selected!=null&&target!=null&&(target.Color=="colorless"||target.Color==selected.Part.color);
            // Replacement is free; the replaced ink returns to the owned inks (user 2026-11-03).
            growthInsert.GetComponentInChildren<Text>().text=!growthSlotPicked||!GrowthFits(growthSlot)?"장착할 칸 선택":$"{growthSlot+1}번 칸 "+(target?.Part==null?"장착":"교체");
            // 장착 works before a slot is chosen: it then waits for a fitting slot.
            bool anyFits=selected!=null&&palette!=null&&Enumerable.Range(0,palette.Slots.Count).Any(GrowthFits);
            if(growthPickSlot&&!anyFits)growthPickSlot=false;
            growthInsert.interactable=!growthPickSlot&&anyFits;Paint(growthInsert,true);
            growthRecolor.GetComponentInChildren<Text>().text="색 변경";
            growthRecolor.interactable=selected!=null&&target?.Part!=null&&g.ColorChangesLeft>0&&target.Color!=selected.Part.color&&!duplicate&&g.Economy.Gold>=g.Data.colorChangeGold;Paint(growthRecolor,false);
            growthSell.GetComponentInChildren<Text>().text="판매";growthSell.interactable=selected!=null;Paint(growthSell,false);
            growthDestroy.GetComponentInChildren<Text>().text="파괴";growthDestroy.interactable=selected!=null;Paint(growthDestroy,false);
            LayoutGrowthActions(false,selected!=null,selected!=null&&growthSlotPicked&&target?.Part!=null&&target.Color!=selected.Part.color&&g.ColorChangesLeft>0);
            growthRule.text=growthInspectMounted&&detail!=null?$"장착 중 / {growthSlot+1}번 칸 +{target.Level}":growthRecolor.gameObject.activeSelf?$"색 변경 {g.ColorChangesLeft}회 남음":"";
            // Only what the buttons cannot show: why the action is refused, or what a replacement loses.
            if(growthRecolorMode){growthCost.text=RecolorRefusal(g,palette,target,selected,duplicate);return;}
            growthCost.text=palette==null?"팔레트가 없습니다":growthPickSlot?"장착할 슬롯을 고르세요":growthSlotPicked&&duplicate?"같은 잉크는 중복 장착할 수 없습니다":growthSlotPicked&&selected!=null&&!matches?"":
                growthSlotPicked&&target?.Part!=null&&selected!=null?"기존 잉크 반환 / 무료 교체":
                // An owned ink with no slot picked: only say why 장착 is off (no fitting slot on this weapon); else nothing.
                selected!=null&&!anyFits&&palette.Slots.Any(s=>s.Color=="colorless"||s.Color==selected.Part.color)?"같은 잉크는 중복 장착할 수 없습니다":"";
        }

        // 색 변경 mode shows only why the colour change is refused, never a how-to line.
        static string RecolorRefusal(WeaponGrowth g,GrowthPalette palette,GrowthSlot target,GrowthInk selected,bool duplicate)
        {
            if(palette==null)return "팔레트가 없습니다";
            if(g.ColorChangesLeft<=0)return $"색 변경은 런당 {g.Data.colorChangesPerRun}회입니다";
            if(selected==null||target==null)return "";
            if(target.Part==null)return "빈 칸은 색을 바꿀 수 없습니다";
            if(target.Color==selected.Part.color)return "칸과 같은 색입니다";
            if(duplicate)return "같은 잉크는 중복 장착할 수 없습니다";
            return g.Economy.Gold<g.Data.colorChangeGold?"골드가 부족합니다":"";
        }

        // Stamps change the selected weapon itself: the form for a weapon stamp, the special variant for a skill stamp.
        void RefreshStampDetail(RunStamp stamp,RunStash stash,RunEconomy economy)
        {
            var arena=host.Arena;var weapon=arena.Player.weapons[growthWeapon];var form=arena.catalog.forms[weapon.FormIndex];
            string kind=shop.SlotBlueprintKind(growthWeapon);var equipped=stash.Equipped(growthWeapon,stamp.Kind);int cost=stash.CostOf(growthWeapon,stamp,economy);
            bool fits=kind!=null&&(stamp.Kind==StampKind.Skill||stamp.BlueprintKind==kind);bool same=stamp.SameEffect(equipped);
            growthDetailDrop.sprite=StampIcon(stamp);growthDetailDrop.enabled=growthDetailDrop.sprite;growthDetailDrop.color=Color.white;
            growthDetailName.text=stamp.Kind==StampKind.Weapon
                ?arena.catalog.forms.FirstOrDefault(f=>f.id==stamp.FormId)?.name??stamp.FormId
                :ForestCombatV2.Chapter1Rewards.SkillStampName(stamp.Skill);
            PaintInkDetailGrade(growthDetailName,null);
            growthDescription.text="";
            growthDetailContent.parent.gameObject.SetActive(false);
            Place(growthRule,RightX+52,235,296,18);
            growthRule.text=stamp.Kind==StampKind.Weapon?"형태 스탬프 / 기본 공격 + 스킬":"스킬 스탬프 / "+form.name;
            var current=GrowthPreviewIcon(form.id,stamp.Kind==StampKind.Weapon?-1:weapon.SpecialIndex);
            var next=GrowthPreviewIcon(stamp.Kind==StampKind.Weapon?stamp.FormId:form.id,stamp.Kind==StampKind.Weapon?-1:stamp.Skill);
            ShowGrowthStampPreview(current,next,fits);
            growthInsert.GetComponentInChildren<Text>().text=same?"적용 중":cost==0?"변경하기 / 무료":"변경하기";
            growthInsert.interactable=fits&&!same&&economy.Gold>=cost;Paint(growthInsert,true);
            growthRecolor.interactable=growthSell.interactable=growthDestroy.interactable=false;
            Paint(growthRecolor,false);Paint(growthSell,false);Paint(growthDestroy,false);
            LayoutGrowthActions(true,false,false);
            SetActionGold(growthInsert,!same&&cost>0?(int?)cost:null,true);
            growthCost.text=kind==null?"무기 미장착":!fits?$"{ForestCombatV2.Chapter1Rewards.BlueprintName(stamp.BlueprintKind)} 전용":same?"이미 적용 중":economy.Gold<cost?"골드 부족":equipped!=null?"기존 스탬프 소멸":"";
        }
    }
}
