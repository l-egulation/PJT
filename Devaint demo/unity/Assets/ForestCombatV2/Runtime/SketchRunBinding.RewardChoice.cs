using System;
using System.Linq;
using Deviant.Combat;
using Deviant.Input.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ForestCombatV2
{
    public sealed partial class SketchRunBinding
    {
        readonly SketchRewardChoiceScreen rewardScreen = new SketchRewardChoiceScreen();
        object rewardItemsKey;
        SketchRewardChoiceScreen.Item[] rewardItems;
        int navigationDirection;
        float nextNavigation;
        // Frame on which the current offer first reached the screen. F both opens the locked chest (Interact) and takes a card,
        // so a press from that frame belongs to whatever opened the offer and never takes a card (stage-gimmicks.md 잠긴 상자).
        int offerShownFrame = -1;
        public Func<bool> SharedRewardBackdrop { get; set; }
        public bool ShowingRewardChoice => ChoosingReward && !PanelOpen;

        void RefreshRewardScreen()
        {
            rewardScreen.Chosen = index => TryChooseReward(index);
            rewardScreen.Continued = ContinueReward;
            rewardScreen.Configured = ConfigureReward;
            rewardScreen.Rerolled = () => TryRerollReward();
            rewardScreen.RerollLabel = run?.Perks != null && run.Perks.Settings.SharedRerolls > 0 ? $"[{PlayerControls.Label(Arena.Player, GameAction.RerollReward)}] 공용 리롤 {run.Perks.RerollsRemaining}" : null;
            // Legacy callers can still present a receipt; normal acquisition closes directly.
            if (justPicked != null || justRegistered != null)
            {
                object key = (object)justPicked ?? justRegistered;
                if (!ReferenceEquals(rewardItemsKey, key))
                {
                    rewardItemsKey = key;
                    rewardItems = new[] { justPicked != null ? WeaponItem(justPicked.Form, justPicked.Skill, justPicked.Slots.Select(s => s.Enhance).ToArray()) : InkItem(justRegistered) };
                }
                rewardScreen.Picked(key, rewardItems[0]);
            }
            else if (State.PendingOffer != null)
            {
                if (!ReferenceEquals(rewardItemsKey, State.PendingOffer))
                {
                    rewardItemsKey = State.PendingOffer;
                    offerShownFrame = Time.frameCount;
                    rewardItems = State.PendingOffer.Select(c => c.IsInk ? InkItem(c.Ink) : WeaponItem(c.Weapon.Form, c.Weapon.Skill, c.Weapon.Enhance.ToArray())).ToArray();
                }
                rewardScreen.Offer(State.PendingOffer, rewardItems, State.Banked, State.PendingIsBoss, State.PendingIsStartInk ? "출발 잉크" : State.PendingIsStart ? "출발 보상" : null);
            }
        }
        SketchRewardChoiceScreen.Item WeaponItem(string form, string skill, string[] slots) => new SketchRewardChoiceScreen.Item
        {
            Name = FormName(form), Kind = "스케치", FormId = form, SkillName = SkillName(form,skill),
            Detail = Arena.catalog.forms.FirstOrDefault(f=>f!=null&&f.id==form)?.specials?.FirstOrDefault(x=>x!=null&&x.id==skill)?.summary ?? "", Slots = slots,
            ArtPath = SketchRewardChoiceScreen.WeaponArt(form, skill), FallbackArtPath = SketchRewardChoiceScreen.WeaponArt(form), LegacyArtPath = "ApprovedItemIcons/" + form
        };
        SketchRewardChoiceScreen.Item InkItem(string id)
        {
            var part = InkData?.FindPart(id);
            var ink = Arena.Sketch?.Catalog.Find(id);
            return new SketchRewardChoiceScreen.Item { Name = InkName(id), Kind = ColorName(ink?.color ?? part?.color) + " 잉크", IsInk = true, InkId = id, InkUses = State.Arsenal.Rules.InkUsesPerAcquisition,
                Detail = Deviant.UI.Theme.ApprovedInkText.Description(ink?.description ?? part?.description ?? "",false), ArtPath = ink?.icon ?? SketchRewardChoiceScreen.InkArt(id), FallbackArtPath = "ApprovedItemIcons/" + id };
        }
        public bool TryChooseReward(int index)
        {
            if (TryShowFirstRewardTutorial()) return false;
            if (PanelOpen || State?.PendingOffer == null || justPicked != null || justRegistered != null || index < 0 || index >= State.PendingOffer.Length) return false;
            if(Arena.Sketch != null)Arena.Sketch.ChooseReward(index);else State.Choose(index);
            justRegistered = null; justPicked = null;
            PlayerControls.For(Arena.Player)?.SuppressGameplay();
            UpdatePause();
            return true;
        }
        // R on the offer screen: one shared reroll (roguelite-progression.md 공용 리롤), spent only when a candidate changes.
        public const Key RerollKey = Key.R;
        public bool TryRerollReward()
        {
            if (TryShowFirstRewardTutorial()) return false;
            if (PanelOpen || State?.PendingOffer == null || justPicked != null || justRegistered != null || run?.Perks == null) return false;
            return run.Perks.TryReroll(() => Arena.Sketch != null ? Arena.Sketch.RerollReward() : State.RerollPending());
        }
        public void ContinueReward()
        {
            if (PanelOpen) return;
            justPicked = null; justRegistered = null;
            UpdatePause();
        }
        public void ConfigureReward()
        {
            if (justPicked == null && justRegistered == null) return;
            if (justPicked != null) selectedWeapon = justPicked.Id; else selectedInk = justRegistered;
            SharedBagSelection?.Invoke(justPicked?.Id ?? Guid.Empty, justRegistered);
            justPicked = null; justRegistered = null; PanelOpen = true;
            UpdatePause();
        }
        public void ToggleRewardBag()
        {
            PanelOpen = !PanelOpen;
            UpdatePause();
        }
        void UpdateRewardInput()
        {
            if (TryShowFirstRewardTutorial()) return;
            if (Arena.Sketch != null && !ChoosingReward) return;
            var input = PlayerControls.For(Arena.Player);
            bool bag = input ? input.Take(GameAction.Inventory) : Keyboard.current != null && Keyboard.current[PanelKey].wasPressedThisFrame;
            if (bag) { ToggleRewardBag(); return; }
            if (!RewardModalShown) return;
            if (input)
            {
                input.SuppressGameplay();
                // Back can close a comparison bag; it cannot abandon an opened reward.
                if (input.TakeBack()) { if (PanelOpen) ToggleRewardBag(); else if(justPicked!=null||justRegistered!=null) ContinueReward(); return; }
            }
            if (PanelOpen) return;
            var keyboard = Keyboard.current;
            if(!input&&keyboard!=null&&keyboard.escapeKey.wasPressedThisFrame){if(justPicked!=null||justRegistered!=null)ContinueReward();return;}
            RefreshRewardScreen();
            // Vertical navigation scrolls only the selected candidate; horizontal navigation changes cards.
            float scrollDirection=keyboard==null?0:
                (keyboard.upArrowKey.isPressed||keyboard.wKey.isPressed?1:0)-
                (keyboard.downArrowKey.isPressed||keyboard.sKey.isPressed?1:0);
            if(input&&input.UsingGamepad)scrollDirection=input.Read(GameAction.Navigate).Y;
            if(Mathf.Abs(scrollDirection)>.1f)rewardScreen.Scroll(-scrollDirection*220*Time.unscaledDeltaTime);
            var mouse = Mouse.current;
            if (mouse != null) rewardScreen.Pointer(mouse.position.ReadValue(), Screen.width, Screen.height);
            float x = input ? input.Read(GameAction.Navigate).X : Keyboard.current == null ? 0 :
                (Keyboard.current.rightArrowKey.isPressed ? 1 : 0) - (Keyboard.current.leftArrowKey.isPressed ? 1 : 0);
            int direction = Mathf.Abs(x) > .5f ? (x > 0 ? 1 : -1) : 0;
            if (direction != 0 && (direction != navigationDirection || Time.unscaledTime >= nextNavigation))
            { rewardScreen.Move(direction); nextNavigation = Time.unscaledTime + (direction != navigationDirection ? .3f : .14f); }
            navigationDirection = direction;
            if ((input ? input.Take(GameAction.RerollReward) : keyboard != null && keyboard[RerollKey].wasPressedThisFrame) && justPicked == null && justRegistered == null) TryRerollReward();
            bool submittedByBinding = input && input.Take(GameAction.Submit);
            bool submit = (keyboard!=null&&keyboard.fKey.wasPressedThisFrame) || submittedByBinding;
            if (submit && Time.frameCount != offerShownFrame) rewardScreen.Activate();
        }
        void DrawRewardScreen()
        {
            if (TryShowFirstRewardTutorial()) return;
            // The shared uGUI bag owns its veil. An IMGUI veil here would also darken the bag itself.
            if (PanelOpen) { if (SharedRewardBackdrop?.Invoke()!=true) SketchRewardChoiceScreen.Dim(); return; }
            RefreshRewardScreen();
            var input = PlayerControls.For(Arena.Player);
            rewardScreen.Draw(Arena.uiFont, PlayerControls.Label(Arena.Player, GameAction.Inventory), input && input.UsingGamepad ? input.Label(GameAction.Submit) : "F",
                input && input.UsingGamepad ? input.Label(GameAction.Navigate) : "← →", input && input.UsingGamepad ? input.Label(GameAction.Cancel) : "Esc",SharedRewardBackdrop?.Invoke()!=true);
        }
    }
}
