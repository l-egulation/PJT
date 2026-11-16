using System;
using System.Linq;
using Deviant.Run;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        RectTransform stampPageOwned, stampPageTargets, stampPageDetail, stampPagePreview;
        readonly Button[] stampPageFilters=new Button[3];
        Text stampPageCount,stampPageStatus;
        Button stampPageApply;
        Guid stampPageSelected;
        int stampPageWeapon,stampPageFilter;
        bool stampPageSkill;
        string stampPageState;

        void BuildStampPage(Transform page)
        {
            var ownedPage=Rect("Stamp storage",page,374,0,284,440);
            var equipmentPage=Rect("Stamp equipment",page,36,0,286,440);
            Words("보유 스탬프",ownedPage,0,108,220,26,20,true);
            stampPageCount=Words("",ownedPage,228,112,56,20,13,false,JMuted,TextAnchor.MiddleRight);
            string[] filters={"전체","형태","스킬"};
            for(int i=0;i<filters.Length;i++)
            {
                int filter=i;
                stampPageFilters[i]=Tab(filters[i],ownedPage,i*96,144,92,28,14,()=>
                {
                    stampPageFilter=filter;
                    if(filter>0){stampPageSkill=filter==2;if(PageStamp!=null&&(PageStamp.Kind==StampKind.Skill)!=stampPageSkill)stampPageSelected=Guid.Empty;}
                    transactionMessage="";
                });
            }
            // Four pixels of inset keep the selected ring inside the scroll mask.
            stampPageOwned=JournalScroll("보유 스탬프 목록",ownedPage,0,182,284,160);
            stampPageDetail=Rect("선택한 스탬프",ownedPage,0,350,284,50);
            stampPageStatus=Words("",ownedPage,0,403,284,20,12,false,JMuted);
            Words("현재 장착",equipmentPage,0,108,286,26,20,true);
            stampPageTargets=Rect("스탬프 적용 대상",equipmentPage,0,140,286,164);
            stampPagePreview=Rect("스탬프 적용 미리보기",equipmentPage,0,312,286,66);
            stampPageApply=Action("스탬프 선택",equipmentPage,0,386,286,32,ApplyPageStamp,true);
            stampPageApply.GetComponentInChildren<Text>().fontSize=16;
        }

        RunStamp PageStamp=>shop?.Service?.Stash.Stamps.FirstOrDefault(s=>s.Id==stampPageSelected);

        void SelectPageTarget(int weapon,bool skill)
        {
            stampPageWeapon=weapon;stampPageSkill=skill;stampPageFilter=skill?2:1;
            if(PageStamp!=null&&(PageStamp.Kind==StampKind.Skill)!=skill)stampPageSelected=Guid.Empty;
            transactionMessage="";
        }

        void ApplyPageStamp()
        {
            TradeAt(()=>
            {
                var stamp=PageStamp;if(stamp==null)return;
                bool done=shop.TryApplyStamp(stampPageWeapon,stamp.Id,out var error);
                transactionMessage=done?"적용했습니다.":error;
                ForestCombatV2.PresentationSfx.PlaySelected(done?"sfx_items_stamp":"sfx_ui_reject");
                if(done){stampPageSelected=Guid.Empty;growthDirty=true;offerKeys[stampPageWeapon]=null;}
                stampPageState=null;
            });
        }

        bool PageStampFits(RunStamp stamp)
        {
            string kind=shop.SlotBlueprintKind(stampPageWeapon);
            return kind!=null&&(stamp.Kind==StampKind.Skill||stamp.BlueprintKind==kind);
        }

        void RefreshStampPage()
        {
            if(shopTab!=2)return;
            var stash=shop.Service.Stash;var economy=shop.Service.Economy;var player=host.Arena.Player;
            string state=stampPageSelected+":"+stampPageWeapon+":"+stampPageSkill+":"+stampPageFilter+":"+economy.Gold+":"+economy.StampSwapCost+":"+transactionMessage+
                string.Join("/",stash.Stamps.Select(s=>s.Id))+string.Join("/",player.weapons.Select((w,i)=>w.FormIndex+":"+w.SpecialIndex+":"+player.IsWeaponSlotReady(i)+":"+stash.EquippedWeaponStamp(i)?.Id+":"+stash.EquippedSkillStamp(i)?.Id));
            if(state==stampPageState)return;stampPageState=state;
            for(int i=0;i<stampPageFilters.Length;i++)SetTab(stampPageFilters[i],i==stampPageFilter);
            ClearGrowth(stampPageOwned);ClearGrowth(stampPageTargets);ClearGrowth(stampPageDetail);ClearGrowth(stampPagePreview);
            var stamps=stash.Stamps.Where(s=>stampPageFilter==0||s.Kind==(stampPageFilter==1?StampKind.Weapon:StampKind.Skill)).ToArray();
            stampPageCount.text=stash.Stamps.Count.ToString();
            for(int i=0;i<stamps.Length;i++)
            {
                var stamp=stamps[i];
                var cell=SlotButton("Owned stamp "+stamp.Id,stampPageOwned,4+i%3*94,4+i/3*78,88,72,()=>
                {stampPageSelected=stamp.Id;stampPageSkill=stamp.Kind==StampKind.Skill;transactionMessage="";});
                SetSlot(cell.image,stamp.Id==stampPageSelected);
                var icon=StampItem(cell.transform,stamp,12,4,64,64);
                icon.color=PageStampFits(stamp)?Color.white:new Color(1,1,1,.4f);
            }
            stampPageOwned.sizeDelta=new Vector2(284,Mathf.Max(160,8+Mathf.CeilToInt(stamps.Length/3f)*78));
            if(stamps.Length==0)Words("보유한 스탬프 없음",stampPageOwned,0,48,284,28,14,false,JMuted,TextAnchor.MiddleCenter);
            for(int w=0;w<2;w++)
            {
                int weapon=w;bool ready=player.IsWeaponSlotReady(w);
                var equipped=player.weapons[w];var form=host.Arena.catalog.forms[equipped.FormIndex];
                var row=Rect("무기 "+(w+1),stampPageTargets,0,w*86,286,78);
                Words("무기 "+(w+1),row,0,0,286,18,13,true);
                for(int k=0;k<2;k++)
                {
                    bool skill=k==1;
                    var cell=SlotButton("Target "+w+" "+k,row,k*148,22,138,56,()=>SelectPageTarget(weapon,skill));
                    cell.interactable=ready;
                    SetSlot(cell.image,stampPageWeapon==w&&stampPageSkill==skill);
                    var backing=Glyph(ItemIcon(skill?"backing-stamp-skill":"backing-stamp-form"),cell.transform,4,2,50,50,ready?Color.white:new Color(1,1,1,.35f));
                    var icon=Glyph(ready?GrowthPreviewIcon(form.id,skill?equipped.SpecialIndex:-1):CombatSprite("state-empty-weapon"),cell.transform,16,22,26,26,JInk);
                    backing.preserveAspect=icon.preserveAspect=true;
                    Words(skill?"스킬":"형태",cell.transform,58,6,76,16,11,false,JMuted);
                    var name=Words(!ready?"미장착":skill?form.specials[equipped.SpecialIndex].name:form.name,cell.transform,58,22,76,30,13,true);
                    name.horizontalOverflow=HorizontalWrapMode.Wrap;
                }
            }
            var selected=PageStamp;
            bool selectedReady=player.IsWeaponSlotReady(stampPageWeapon);
            var currentWeapon=player.weapons[stampPageWeapon];var currentForm=host.Arena.catalog.forms[currentWeapon.FormIndex];
            var applied=stash.Equipped(stampPageWeapon,stampPageSkill?StampKind.Skill:StampKind.Weapon);
            bool fits=selected!=null&&PageStampFits(selected),same=selected!=null&&selected.SameEffect(applied);
            int cost=selected!=null?stash.CostOf(stampPageWeapon,selected,economy):0;
            stampPageStatus.text=Deviant.UI.Theme.ApprovedInkText.Display(transactionMessage);
            if(selected!=null)
            {
                StampItem(stampPageDetail,selected,0,0,50,50);
                string name=selected.Kind==StampKind.Weapon?host.Arena.catalog.forms.FirstOrDefault(f=>f.id==selected.FormId)?.name??selected.FormId:ForestCombatV2.Chapter1Rewards.SkillStampName(selected.Skill);
                Words(name,stampPageDetail,60,1,224,24,18,true);
                Words(selected.Kind==StampKind.Weapon?ForestCombatV2.Chapter1Rewards.BlueprintName(selected.BlueprintKind)+" / 형태 스탬프":"스킬 스탬프",stampPageDetail,60,28,224,18,12,false,JMuted);
                if(!selectedReady)stampPageStatus.text="무기 미장착";
                else if(!fits)stampPageStatus.text=ForestCombatV2.Chapter1Rewards.BlueprintName(selected.BlueprintKind)+" 전용";
                else if(same)stampPageStatus.text="이미 적용 중";
                else if(economy.Gold<cost)stampPageStatus.text="골드 부족";
                else if(applied!=null)stampPageStatus.text="기존 스탬프 소멸";
            }
            else Words("스탬프를 선택하세요",stampPageDetail,0,12,284,26,14,false,JMuted);
            for(int i=0;i<2;i++)
            {
                float x=i==0?28:206;
                Sprite sprite=null;
                if(i==0&&selectedReady)sprite=GrowthPreviewIcon(currentForm.id,stampPageSkill?currentWeapon.SpecialIndex:-1);
                if(i==1&&selected!=null)sprite=GrowthPreviewIcon(selected.Kind==StampKind.Weapon?selected.FormId:currentForm.id,selected.Kind==StampKind.Weapon?-1:selected.Skill);
                Glyph(CombatSprite("weapon-disc"),stampPagePreview,x,0,52,52,Color.white);
                if(sprite)Glyph(sprite,stampPagePreview,x+3,3,46,46,i==1&&!fits?new Color(1,1,1,.3f):Color.white);
                Words(i==0?"현재":"변경 후",stampPagePreview,x-18,53,88,14,12,false,JMuted,TextAnchor.MiddleCenter);
            }
            Words("→",stampPagePreview,126,6,34,36,24,true,JMuted,TextAnchor.MiddleCenter);
            stampPageApply.interactable=fits&&!same&&economy.Gold>=cost;
            stampPageApply.GetComponentInChildren<Text>().text=selected==null?"스탬프 선택":same?"적용 중":cost==0?"적용 / 무료":"교체";
            Paint(stampPageApply,true);
            SetActionGold(stampPageApply,selected!=null&&!same&&cost>0?(int?)cost:null,true);
        }
    }
}
