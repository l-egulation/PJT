using System;
using System.Linq;
using Deviant.Run;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Deviant.Game.Editor
{
    public static partial class ApprovedCoreVerification
    {
        [MenuItem("Tools/deviant/Approved UI/Verify inventory flow and capture")]
        public static void RunInventory()
        {
            Begin("InventoryFlowVerification");
        }
        static void PrepareInventoryFlowChecks()
        {
            checks.Enqueue(()=>
            {
                foreach(var entry in model.Catalog.inks)if(!model.State.Arsenal.Registered(entry.id))model.Register(entry.id);
                model.DebugConfigureWeapon(0,"base_sword",0);model.DebugConfigureWeapon(1,"whip",0);
                var spare=model.State.Arsenal.Acquire(draft.Form,draft.Model.Skill,new string[4]);model.Confirm(spare.Id);
                model.Rename(spare.Id,"가나다라마바사아자차카타파하테스트검");
                host.Presenter.Open();Call("SetLiveTab",0);gui.Refresh();
                Check(!Button("Core tab 1")&&Button("Core tab 0")&&Button("Core tab 2"),"Only bag and equipment tabs are exposed");
                Check(Get<bool>("approvedBagCategory"),"Bag starts on the category rather than selecting its first item");
                Press(Key.RightArrow);Check(Get<int>("liveFilter")==1&&Get<bool>("approvedBagCategory"),"Changing ink category does not enter the first ink");
                Check(!Button("Inventory primary"),"Ink detail action stays hidden until entering the list");
                Press(Key.DownArrow);Check(!Get<bool>("approvedBagCategory"),"Down enters the ink list");
                var first=model.Catalog.inks.First(i=>model.State.Arsenal.Registered(i.id));
                Check(gui.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("남은 횟수")&&t.text.Contains("총 횟수")),"Ink detail displays one remaining and total count");
                Capture("bag-inks",1920,1080);
                Press(Key.UpArrow);Press(Key.LeftArrow);Press(Key.DownArrow);
                Set("livePick",Array.FindIndex((RunInkWeapon[])Call("LiveWeapons"),w=>w.Id==draft.Id));gui.Refresh();
                Press(Key.F);Check(Get<string>("liveStage")=="palette"&&Get<Guid>("liveWeapon")==draft.Id,"F opens the selected actual draft");
                Check(gui.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("Craft slot "))==6,"All six actual palette slots are reachable without horizontal clipping");
                Check(!Button("Apply selected ink"),"Right apply action is hidden before F enters inks");
                Capture("craft-slot",1920,1080);
            });
            checks.Enqueue(()=>
            {
                Press(Key.F);Check(Get<string>("liveStage")=="ink","F on a palette slot enters the right ink grid");
                Set("livePick",Array.FindIndex((RunInkDefinition[])Call("LiveInks"),i=>i.id==ink));gui.Refresh();
                Check(Button("Apply selected ink"),"Ink mode advertises its F apply action");
                Capture("craft-ink",1920,1080);
                int remaining=model.RemainingInkUses(ink);Press(Key.F);
                Check(draft.Slots[0].Ink?.id==ink&&model.RemainingInkUses(ink)==remaining&&model.ReservedInkUses(ink)==1,"F applies and reserves an ink without spending it");
                Check(Get<string>("liveStage")=="palette","Applying returns focus to the palette");
                Press(Key.R);Check(draft.Slots[0].Ink==null&&model.ReservedInkUses(ink)==0,"R clears the selected slot and releases the reservation");
                Press(Key.F);Set("livePick",Array.FindIndex((RunInkDefinition[])Call("LiveInks"),i=>i.id==ink));Press(Key.F);
                Press(Key.DownArrow);Check(Get<int>("liveAction")==1,"Down selects crafting instead of a second competing F action");
                Capture("craft-action",1280,720);
                Press(Key.F);Check(Get<string>("liveStage")=="review"&&Get<int>("liveAction")==1,"Craft opens confirmation with confirm selected by default");
                var warning=gui.GetComponentsInChildren<Text>().Single(t=>t.text.StartsWith("확정 시 잉크가 소모"));
                Check(warning.preferredHeight<=warning.rectTransform.rect.height,"Confirmation warning fits a single compact line alongside actions");
                Capture("craft-confirm",1920,1080);
                Press(Key.RightArrow);Press(Key.F);Check(!draft.Confirmed&&Get<string>("liveStage")=="palette","Continue editing leaves the draft and ink uses intact");
                Press(Key.F);Press(Key.F);
                Check(draft.Confirmed&&Get<string>("liveStage")=="craftTarget","Confirmation fixes the real weapon and directly opens the merged completion");
                Check(model.RemainingInkUses(ink)==remaining-1,"Confirmation spends exactly one use");
                Check(Get<int>("liveTarget")==-1&&!Button("Complete equip"),"Completion starts centrally without a destination or premature F action");
                Check(Mathf.Approximately(Get<RectTransform>("craftTransferCard").anchoredPosition.x,298),"New weapon starts between the two destination slot centers");
                Check(!Button("Core tab 0")&&!Button("Core tab 2"),"Completion omits the shared tabs and footer");
                int target=Get<int>("liveTarget");Press(Key.DownArrow);Check(Get<int>("liveTarget")==target&&Get<int>("liveAction")==0,"Completion ignores down rather than focusing F/C actions");
                Capture("craft-complete",1920,1080);
                var beforeLeft=model.Equipped(0);var beforeRight=model.Equipped(1);
                Press(Key.LeftArrow);Check(Get<int>("liveTarget")==0&&Button("Complete equip"),"Left selects slot one and exposes F on the transfer cue");
                Press(Key.RightArrow);Check(Get<int>("liveTarget")==1,"Right selects slot two for the moving weapon preview");
                Check(model.Equipped(0)==beforeLeft&&model.Equipped(1)==beforeRight,"Moving the preview does not mutate the actual loadout");
                Press(Key.C);Check(!host.Presenter.IsOpen&&model.Equipped(0)!=draft&&model.Equipped(1)!=draft,"C stores and immediately returns to combat");
            });
            checks.Enqueue(()=>
            {
                host.Presenter.Open();Call("SetLiveTab",2);gui.Refresh();
                int slot=model.Equipped(0)==null?0:1;Set("liveTarget",slot);gui.Refresh();
                Press(Key.F);Check(Get<bool>("inventoryTargetLocked")&&Get<string>("liveStage")=="weapons","Slot-first F remembers the selected destination");
                Set("livePick",Array.FindIndex((RunInkWeapon[])Call("LiveWeapons"),w=>w.Id==draft.Id));gui.Refresh();
                Capture("equipment-slot-first",1920,1080);
                Press(Key.F);Check(model.Equipped(slot)==draft&&Get<string>("liveStage")=="target","Slot-first F equips immediately without asking for a destination again");
                Check(!((RunInkWeapon[])Call("LiveWeapons")).Contains(draft),"Stored equipment excludes the equipped weapon");
                Check(model.State.Arsenal.ChangeCooldownPending&&model.ChangeCooldownRemaining<=0,"Bag changes stay free until the bag closes and starts the shared cooldown");
                Capture("equipment-equipped",1920,1080);
                Press(Key.X);Check(model.Equipped(1-slot)==draft,"X swaps the actual equipped slot order");
                model.Tick(60);
                Press(Key.UpArrow);Press(Key.LeftArrow);Press(Key.DownArrow);
                Check(Get<int>("liveTab")==0,"Arrows traverse the equipment and bag tabs");
                Press(Key.Q);Press(Key.E);Check(Get<int>("liveTab")==0,"Q and E do not switch inventory tabs");
                Capture("bag-all-weapons",1280,720);
            });
            checks.Enqueue(()=>
            {
                var next=model.Weapons.First(w=>!w.Destroyed&&!w.Confirmed);
                Set("liveFilter",0);Set("approvedBagCategory",false);Set("livePick",Array.FindIndex((RunInkWeapon[])Call("LiveWeapons"),w=>w.Id==next.Id));gui.Refresh();
                Press(Key.F);Press(Key.DownArrow);Press(Key.F);Press(Key.F);
                Check(next.Confirmed,"An ink-free draft can still be confirmed");
                model.Tick(60);int free=model.Equipped(0)==null?0:1;Set("liveTarget",free);gui.Refresh();Press(Key.F);
                Check(model.Equipped(free)==next&&!host.Presenter.IsOpen,"F from completion equips and closes directly to combat");
                host.Presenter.Open();Call("SetLiveTab",2);gui.Refresh();
                PressPad(GamepadButton.DpadRight);Check(Get<int>("liveTarget")==1,"Gamepad direction navigates equipped slots");
                Capture("equipment-final",1280,720);
                model.Tick(60);Press(Key.DownArrow);Check(Get<string>("liveStage")=="weapons"&&!Get<bool>("inventoryTargetLocked"),"Down selects a stored weapon without locking a destination");
                Press(Key.F);Check(Get<string>("liveStage")=="mountTarget","Weapon-first F asks for its destination exactly once");
                Press(Key.LeftArrow);var chosen=Get<Guid>("liveWeapon");Press(Key.F);
                Check(model.Equipped(0)?.Id==chosen&&Get<string>("liveStage")=="target","Weapon-first F equips the chosen stable ID into its selected slot");
                model.Tick(60);Press(Key.R);Check(model.Equipped(0)?.Id==chosen&&!Button("Equipment unequip"),"There is no unequip input; R leaves the equipped weapon in place");
                Check(Button("Equipment primary").GetComponentInChildren<Text>().text=="교체하기","An occupied slot uses replace wording");
                model.Tick(60);Press(Key.DownArrow);Press(Key.N);Check(Get<RectTransform>("liveRenamePanel"),"N opens rename for the current stored weapon");
                Get<InputField>("liveRenameInput").text="별칭 검";
                Check(Get<Button>("liveRenameSave").GetComponentsInChildren<Text>().Any(t=>t.text=="F"),"Rename save shows a bordered F keycap");
                Check(Get<Button>("liveRenameCancel").GetComponentsInChildren<Text>().Any(t=>t.text=="Esc"),"Rename cancel shows a bordered Esc keycap");
                Press(Key.Enter);Check(Get<RectTransform>("liveRenamePanel")&&!model.Weapons.Any(w=>w.CustomName=="별칭 검"),"Enter does not save or close the rename modal");
                Press(Key.DownArrow);Press(Key.F);
                Check(!Get<RectTransform>("liveRenamePanel")&&model.Weapons.Any(w=>w.CustomName=="별칭 검"),"F saves the selected action and preserves the actual weapon ID");
                Capture("equipment-stored",1920,1080);
            });
        }
    }
}
