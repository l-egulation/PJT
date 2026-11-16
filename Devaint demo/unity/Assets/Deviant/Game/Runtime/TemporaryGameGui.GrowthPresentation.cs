using System.Linq;
using Deviant.Run;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        RectTransform growthStampPreview;

        void RefreshGrowthStampSlots()
        {
            var weapon=host.Arena.Player.weapons[growthWeapon];
            var form=host.Arena.catalog.forms[weapon.FormIndex];
            for(int i=0;i<2;i++)
            {
                bool skill=i==1;
                var cell=SlotButton(skill?"Current skill":"Current form",growthPalettes,i*178,0,170,84,
                    ()=>OpenStampStorage(growthWeapon,skill));
                var icon=Rect("Item",cell.transform,6,8,68,68).gameObject.AddComponent<Image>();
                icon.sprite=ItemIcon(skill?"backing-stamp-skill":"backing-stamp-form");
                icon.preserveAspect=true;icon.raycastTarget=false;
                var glyph=Rect("Equipped glyph",cell.transform,23,35,34,34).gameObject.AddComponent<Image>();
                glyph.sprite=GrowthPreviewIcon(form.id,skill?weapon.SpecialIndex:-1);
                glyph.preserveAspect=true;glyph.raycastTarget=false;glyph.color=JInk;
                Words(skill?"스킬":"형태",cell.transform,80,10,84,16,12,false,JMuted);
                var label=Words(skill?form.specials[weapon.SpecialIndex].name:form.name,
                    cell.transform,80,32,84,44,14,true);
                label.horizontalOverflow=HorizontalWrapMode.Wrap;
            }
        }

        Sprite GrowthPreviewIcon(string formId,int specialIndex)
        {
            var binding=combatBindings.weapons.FirstOrDefault(w=>w.id==formId);
            if(specialIndex<0)return CombatSprite(binding?.icon??"state-empty-weapon");
            var form=host.Arena.catalog.forms.FirstOrDefault(f=>f.id==formId);
            string runtimeId=form?.specials[specialIndex].id;
            var skill=combatBindings.skills.FirstOrDefault(s=>s.weaponId==formId&&(s.runtimeId==runtimeId||s.id==runtimeId));
            if(skill==null&&binding!=null&&specialIndex<binding.specials.Length)
                skill=combatBindings.skills.FirstOrDefault(s=>s.id==binding.specials[specialIndex]);
            return CombatSprite(skill?.icon??"state-unavailable");
        }

        void ShowGrowthStampPreview(Sprite current,Sprite next,bool compatible)
        {
            ClearGrowth(growthStampPreview);
            growthStampPreview.gameObject.SetActive(true);
            for(int i=0;i<2;i++)
            {
                float x=i==0?0:196;
                var face=Rect(i==0?"Current":"After",growthStampPreview,x,0,152,90);
                Words(i==0?"현재":"변경 후",face,0,75,152,14,12,false,JMuted,TextAnchor.MiddleCenter);
                Glyph(CombatSprite("weapon-disc"),face,40,0,72,72,Color.white);
                var icon=Rect("Preview icon",face,44,4,64,64).gameObject.AddComponent<Image>();
                icon.sprite=i==0?current:next;icon.preserveAspect=true;icon.raycastTarget=false;
                icon.color=i==1&&!compatible?new Color(1,1,1,.35f):Color.white;
            }
            Words("→",growthStampPreview,157,15,34,42,25,true,JMuted,TextAnchor.MiddleCenter);
        }

        void LayoutGrowthActions(bool stamp,bool ownedInk,bool recolor)
        {
            if(growthRecolorMode&&!stamp)
            {
                growthInsert.gameObject.SetActive(false);growthSell.gameObject.SetActive(false);growthDestroy.gameObject.SetActive(false);
                growthRecolor.gameObject.SetActive(true);Place(growthRecolor,RightX,368,ColumnW,44);
                growthRecolor.GetComponentInChildren<Text>().fontSize=16;
                SetActionGold(growthRecolor,shop.Growth.Data.colorChangeGold,true);return;
            }
            growthInsert.gameObject.SetActive(stamp||ownedInk);
            growthRecolor.gameObject.SetActive(!stamp&&ownedInk&&recolor);
            growthSell.gameObject.SetActive(!stamp&&ownedInk);
            growthDestroy.gameObject.SetActive(!stamp&&ownedInk);
            int secondaries=(growthRecolor.gameObject.activeSelf?1:0)+(growthSell.gameObject.activeSelf?1:0)+(growthDestroy.gameObject.activeSelf?1:0);
            float primaryWidth=ColumnW-secondaries*76;
            Place(growthInsert,RightX,368,primaryWidth,44);
            var label=growthInsert.GetComponentInChildren<Text>();
            Place(label,0,0,primaryWidth,44);label.fontSize=stamp?16:14;
            SetActionGold(growthInsert,null);
            float x=RightX+primaryWidth+4;
            foreach(var button in new[]{growthRecolor,growthSell,growthDestroy})
            {
                if(!button.gameObject.activeSelf)continue;
                Place(button,x,368,72,44);x+=76;
                button.GetComponentInChildren<Text>().fontSize=13;
            }
            SetActionGold(growthRecolor,shop.Growth.Data.colorChangeGold);
            SetActionGold(growthSell,shop.Growth.SalePrice,false,true);
            SetActionGold(growthDestroy,shop.Growth.DestroyedInkGoldBonus>0?(int?)shop.Growth.DestroyedInkGoldBonus:null,false,true);
        }
    }
}
