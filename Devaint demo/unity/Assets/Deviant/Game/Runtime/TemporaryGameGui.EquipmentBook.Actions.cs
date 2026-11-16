using System;
using System.Linq;
using Deviant.Run;
using UnityEngine;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        bool NativeFits(EquipmentBook b,GrowthInk ink,int index)
        {
            var p=NativePalette(b);if(ink==null||p==null||index<0||index>=p.Slots.Count)return false;
            return (p.Slots[index].Color=="colorless"||p.Slots[index].Color==ink.Part.color)&&
                (ink.Part.duplicate||!p.Slots.Where((s,i)=>i!=index).Any(s=>s.Part==ink.Part));
        }
        bool NativeStampFits(EquipmentBook b,RunStamp stamp)=>stamp!=null&&shop!=null&&shop.SlotBlueprintKind(b.Weapon)!=null&&
            (stamp.Kind==StampKind.Skill||stamp.BlueprintKind==shop.SlotBlueprintKind(b.Weapon));
        void NativePick(EquipmentBook b,Guid id,bool stamp)
        {
            bool recolor=b.Action==BookAction.Recolor&&!stamp;b.Selected=id;b.Message="";
            b.Action=recolor?BookAction.Recolor:BookAction.Inspect;b.Tab=stamp?2:0;
            if(stamp)
            {
                var selected=NativeStamp(b);b.Skill=selected.Kind==StampKind.Skill;
                if(!NativeStampFits(b,selected)){int other=1-b.Weapon;int current=b.Weapon;b.Weapon=other;if(!NativeStampFits(b,selected))b.Weapon=current;}
            }
            else if(!recolor)
            {
                var ink=NativeInk(b);var p=NativePalette(b);
                if(b.TargetChosen&&NativeFits(b,ink,b.Slot))return;
                var choices=p?.Slots.Select((s,i)=>i).Where(i=>NativeFits(b,ink,i)).ToArray()??Array.Empty<int>();
                var empty=choices.Where(i=>p.Slots[i].Part==null).ToArray();if(empty.Length>0)choices=empty;
                b.Slot=choices.Length==1?choices[0]:-1;b.TargetChosen=false;
            }
        }
        bool NativeTrade(EquipmentBook b,Action transaction,bool shopOnly=false)
        {
            bool done;string error=null;
            if(b.Shop){done=shop!=null&&shop.GrowthTransaction(transaction,out error);}
            else if(shopOnly||host.Arena.Player.Dead){done=false;error="정비소에서 가능합니다.";}
            else{try{transaction();done=true;}catch(InvalidOperationException e){done=false;error=e.Message;}}
            b.Message=done?"완료했습니다.":error??"지금은 사용할 수 없습니다.";b.Key=null;
            if(done&&b.Selected!=Guid.Empty&&NativeInk(b)==null&&NativeStamp(b)==null){b.Selected=Guid.Empty;b.Action=BookAction.Inspect;}
            PlayEquipResult(done);return done;
        }
        void NativeEquip(EquipmentBook b)
        {
            var ink=NativeInk(b);var stamp=NativeStamp(b);
            if(b.Action==BookAction.Sell)
            {
                if(ink!=null)NativeTrade(b,()=>NativeGrowth.Sell(ink.Id),true);
                else if(stamp!=null)NativeTrade(b,()=>{if(!NativeStash.TrySell(stamp.Id,NativeGrowth.Economy,out var error))throw new InvalidOperationException(error);},true);
                return;
            }
            if(b.Action==BookAction.Destroy){if(ink!=null)NativeTrade(b,()=>NativeGrowth.Destroy(ink.Id));return;}
            if(ink!=null)
            {
                if(NativeSlot(b)==null){b.Message="장착할 칸 선택";return;}
                if(b.Action==BookAction.Recolor)
                {
                    if(NativeTrade(b,()=>NativeGrowth.Recolor(NativeWeapon(b),b.Slot,ink.Part.color,ink.Id),true))b.Action=BookAction.Inspect;
                }
                else NativeTrade(b,()=>NativeGrowth.Insert(NativeWeapon(b),b.Slot,ink.Id));
            }
            else if(stamp!=null&&b.Shop&&shop!=null)
            {
                bool done=shop.TryApplyStamp(b.Weapon,stamp.Id,out var error);b.Message=done?"적용했습니다.":error;
                if(done){b.Selected=Guid.Empty;b.Action=BookAction.Inspect;}PlayEquipResult(done);b.Key=null;
            }
        }
        void NativeCancel(EquipmentBook b)
        {
            if(b.Action==BookAction.Sell||b.Action==BookAction.Destroy)b.Action=BookAction.Inspect;
            else{b.Selected=Guid.Empty;b.Action=BookAction.Inspect;}
            b.Message="";
        }
        void NativeUpgrade(EquipmentBook b)
        {
            if(NativeSlot(b)==null)return;
            NativeTrade(b,()=>NativeGrowth.Upgrade(NativeWeapon(b),b.Slot),true);
        }
    }
}
