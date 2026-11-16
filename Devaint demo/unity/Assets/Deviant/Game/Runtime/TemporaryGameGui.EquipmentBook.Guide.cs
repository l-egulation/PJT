using System.Linq;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        void RefreshNativeInkGuide()
        {
            if(!bag)return;
            if(!inkGuidePrompt){inkGuidePrompt=Words(InkGuideBagLine,bag.transform,32,416,736,20,13,true,JInk);inkGuidePrompt.name="First-run ink prompt";}
            var guide=InkGuide;int weapon=-1,index=-1;
            bool teaching=Presenter.IsOpen&&guide&&guide.Current==Chapter1FirstRunInkGuide.Stage.InBag&&guide.TargetSlot(out weapon,out index);
            if(!Presenter.IsOpen)inkGuideTabSet=false;
            if(teaching&&!inkGuideTabSet){inkGuideTabSet=true;nativeBag.Weapon=weapon;nativeBag.Tab=0;nativeBag.Key=null;Presenter.SelectWeapon(weapon);}
            inkGuidePrompt.gameObject.SetActive(teaching);
            foreach(var button in nativeBag.Body.GetComponentsInChildren<Button>())
                if(button.name.StartsWith("Palette slot "))InkGuideRingOn(button,teaching&&nativeBag.Weapon==weapon&&button.name=="Palette slot "+index,.7f+.3f*Mathf.Cos(Time.unscaledTime*Mathf.PI*2/InkGuideBlinkSeconds));
        }
    }
}
