using System;
using Deviant.Chapter2;
using Deviant.Run;
using ForestCombatV2;
using UnityEngine;

namespace Deviant.Game
{
    // One GUI adapter; each chapter still owns its simulation/input scope and domain purchases.
    public sealed class CommonMaintenanceShop : MonoBehaviour
    {
        Chapter1MaintenanceShop first;Chapter2World second;
        public static CommonMaintenanceShop For(Chapter1MaintenanceShop source){if(!source)return null;var adapter=source.GetComponent<CommonMaintenanceShop>()??source.gameObject.AddComponent<CommonMaintenanceShop>();adapter.first=source;MaintenanceRoomPresentation.Attach(source.GetComponent<ForestCombatWorld>(),adapter);return adapter;}
        public static CommonMaintenanceShop For(Chapter2World source){if(!source)return null;var adapter=source.GetComponent<CommonMaintenanceShop>()??source.gameObject.AddComponent<CommonMaintenanceShop>();adapter.second=source;MaintenanceRoomPresentation.Attach(source,adapter);return adapter;}
        public MaintenanceService Service=>first?first.Service:second?second.Shop:null;
        public WeaponGrowth Growth=>first?first.Growth:second?second.arena.MvpGrowth:null;
        public string SketchFacility => first ? "chapter1:" + first.Facility : "chapter2:M";
        public bool IsOpen=>first?first.IsOpen:second&&second.ShopOpen;
        public bool ExternalInput {get=>first?first.ExternalInput:second&&second.ExternalInput;set{if(first)first.ExternalInput=value;else if(second)second.ExternalInput=value;}}
        public bool ExternalPresentation {get=>first?first.ExternalPresentation:second&&second.ExternalPresentation;set{if(first)first.ExternalPresentation=value;else if(second)second.ExternalPresentation=value;}}
        public bool TryOpen(){var presentation=GetComponent<MaintenanceRoomPresentation>();if(presentation&&presentation.UsesWorldShelves)return presentation.TryInteractMerchant();if(presentation&&!presentation.MayOpenShop())return false;return first?first.TryOpen():second&&second.TryOpenMaintenance();}
        public void Close(){if(first)first.Close();else if(second)second.CloseMaintenance();}
        public Guid GrowthWeapon(int slot)=>first?first.GrowthWeapon(slot):second.arena.Player.Loadout.Weapons[slot].InstanceId;
        public string SlotBlueprintKind(int slot)=>first?first.SlotBlueprintKind(slot):second.arena.Player.IsWeaponSlotReady(slot)?Chapter1Rewards.BlueprintKindOf(second.arena.catalog.forms[second.arena.Player.weapons[slot].FormIndex].id):null;
        public bool GrowthTransaction(Action action,out string error)
        {
            if(first)return first.GrowthTransaction(action,out error);error=null;
            if(!second||!second.Ready||!IsOpen||second.arena.Player.Dead){error="상점을 연 상태에서만 가능합니다.";return false;}
            try{action();return true;}catch(InvalidOperationException e){error=e.Message;return false;}
        }
        public System.Collections.Generic.IReadOnlyList<InkOffer> InkStock=>first?first.InkStock:second?second.InkStock:Array.Empty<InkOffer>();
        public bool TryBuyInk(int index,out string message){if(first)return first.TryBuyInk(index,out message);bool result=second&&second.TryBuyInk(index);message=second?second.Message:"정비소 없음";return result;}
        public bool TryUpgradeMvpAttack(out string error){if(first)return first.TryUpgradeMvpAttack(out error);return GrowthTransaction(()=>Growth.UpgradeAttack(),out error);}
        public bool TryApplyStamp(int slot,Guid stamp,out string error)
        {
            if(first)return first.TryApplyStamp(slot,stamp,out error);
            if(!second||!second.Ready||!IsOpen||second.arena.Player.Dead){error="상점을 연 상태에서만 가능합니다.";return false;}
            return Chapter1MaintenanceShop.ApplyStamp(second.arena.Player,second.Shop.Stash,second.Shop.Economy,slot,stamp,out error);
        }
        public string StampName(RunStamp stamp)=>first?first.StampName(stamp):Chapter1RewardPickup.StampLabel(second?second.arena:null,stamp);
        public string StallKey => first ? first.StallKey : Chapter2World.StallKey;
        readonly System.Random rerollRandom = new System.Random();
        public bool RerollInks() => IsOpen && Growth != null && Growth.RerollInkStock(StallKey, rerollRandom);
    }
}
