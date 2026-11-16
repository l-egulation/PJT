using System;
using System.Collections.Generic;
using Deviant.Control.Contracts;

namespace Deviant.Inventory
{
    // UI selection, notification timing and input ownership have no Unity dependency.
    public sealed class InventoryPresenter : IDisposable
    {
        readonly IControlScope controls;
        IControlLease input, inventoryPause, pause, pauseInput;
        readonly Queue<Notification> notifications = new Queue<Notification>();
        sealed class Notification { public InkInstance Item; public string Message; }
        double toastUntil;
        double lastToastTick = double.NaN;
        public InkInventory Inventory { get; }
        public bool IsOpen { get; private set; }
        public bool Paused { get; private set; }
        public InkKind? Filter { get; private set; }
        public int SelectedBag { get; private set; } = -1;
        public int SelectedWeapon { get; private set; }
        public int SelectedEquipmentSlot { get; private set; } = -1;
        public InkInstance Toast { get; private set; }
        public string ToastMessage { get; private set; }
        public bool ToastVisible => Toast != null || !string.IsNullOrEmpty(ToastMessage);
        public string ToastText => Toast != null ? AcquisitionText(Toast.Definition.Name,
            Toast.Definition.Kind == InkKind.Stat ? "잉크" : "스탬프") : ToastMessage ?? "";
        public static string AcquisitionText(string name, string kind) =>
            (name.EndsWith(kind, StringComparison.Ordinal) ? name : name + " " + kind) + " 획득";
        public string Message { get; private set; } = "잉크를 선택하고 장착할 슬롯을 누르세요.";
        public InkInstance Selected => SelectedBag >= 0 ? Inventory.BagAt(SelectedBag) :
            SelectedEquipmentSlot >= 0 ? Inventory.EquippedAt(SelectedWeapon, SelectedEquipmentSlot) : null;
        public InventoryPresenter(InkInventory inventory, IControlRequests controlRequests)
        {
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            controls = controlRequests?.OpenScope("Ink inventory");
            inventory.Acquired += OnAcquired;
        }
        public void Toggle() { if (IsOpen) Close(); else Open(); }
        public void Open()
        {
            if (IsOpen || Paused) return;
            input = controls?.BlockInput(InputChannels.Combat | InputChannels.Interaction);
            inventoryPause = controls?.PauseSimulation();
            IsOpen = true;
        }
        public void Close()
        {
            IsOpen = false;
            inventoryPause?.Dispose();
            input?.Dispose();
            inventoryPause = input = null;
        }
        public void TogglePause()
        {
            if (IsOpen) return;
            if (Paused) { pause?.Dispose(); pauseInput?.Dispose(); pause = pauseInput = null; Paused = false; }
            else
            {
                pause = controls?.PauseSimulation();
                pauseInput = controls?.BlockInput(InputChannels.Combat | InputChannels.Interaction);
                Paused = true;
            }
        }
        public void SetFilter(InkKind? filter) { Filter = filter; SelectedBag = -1; SelectedEquipmentSlot = -1; }
        public void SelectBag(int slot)
        {
            if (slot < 0 || slot >= Inventory.Capacity) return;
            SelectedBag = slot; SelectedEquipmentSlot = -1; destroyArmedFor = -1;
        }
        public void SelectWeapon(int weapon)
        { if (weapon >= 0 && weapon < Inventory.WeaponCount) { SelectedWeapon = weapon; SelectedEquipmentSlot = -1; } }
        public bool SelectEquipment(int weapon, int slot)
        {
            bool success; string error;
            if (SelectedBag >= 0 && Selected != null)
            {
                success = Inventory.TryEquip(SelectedBag, weapon, slot, out error);
                Message = success ? "장착했습니다. 이 잉크는 런이 끝날 때까지 빠지지 않습니다." : error;
            }
            else if (SelectedEquipmentSlot >= 0 && Selected != null && (SelectedWeapon != weapon || SelectedEquipmentSlot != slot))
            {
                success = Inventory.TryMoveEquipment(SelectedWeapon, SelectedEquipmentSlot, weapon, slot, out error);
                Message = success ? "장착 잉크를 이동·교환했습니다." : error;
            }
            else { success = true; }
            SelectedBag = -1; SelectedWeapon = weapon; SelectedEquipmentSlot = slot;
            return success;
        }
        public bool EquipSelected()
        {
            if (SelectedBag < 0 || Selected == null) return false;
            return SelectEquipment(SelectedWeapon, SuggestedSlot);
        }
        public int SuggestedSlot
        {
            get
            {
                if (SelectedBag < 0 || Selected == null) return -1;
                if (Selected.Definition.Kind == InkKind.Form) return 0;
                if (Selected.Definition.Kind == InkKind.Special) return Inventory.StatSlots + 1;
                for (int i = 1; i <= Inventory.StatSlots; i++) if (Inventory.EquippedAt(SelectedWeapon, i) == null) return i;
                return 1; // Every stat slot is taken; the equip attempt reports the permanent-slot refusal.
            }
        }
        public Func<int, string> Discard;
        // The view asks the runtime host to leave the selected bag ink at the player's feet.
        public bool DiscardSelected()
        {
            if (SelectedBag < 0 || Selected == null || Discard == null) return false;
            string error = Discard(SelectedBag);
            Message = error ?? "발밑에 내려놓았습니다.";
            if (error == null) { SelectedBag = -1; SelectedEquipmentSlot = -1; }
            return error == null;
        }
        public Func<int, string> Destroy;
        // Destroying a bag ink is permanent (no drop, no currency), so the first press only arms it; a second press on
        // the same selection destroys. Any other selection disarms (progression-and-economy 가방 포화).
        public bool DestroyArmed => destroyArmedFor >= 0 && destroyArmedFor == SelectedBag;
        int destroyArmedFor = -1;
        public bool DestroySelected()
        {
            if (SelectedBag < 0 || Selected == null || Destroy == null) return false;
            if (!DestroyArmed) { destroyArmedFor = SelectedBag; Message = "한 번 더 누르면 이 잉크를 파괴합니다. 되돌릴 수 없습니다."; return false; }
            destroyArmedFor = -1;
            string name = Selected.Definition.Name, error = Destroy(SelectedBag);
            Message = error ?? name + " 잉크를 파괴했습니다.";
            if (error == null) { SelectedBag = -1; SelectedEquipmentSlot = -1; }
            return error == null;
        }
        public bool UnequipSelected()
        {
            if (SelectedEquipmentSlot < 0) return false;
            bool success = Inventory.TryUnequip(SelectedWeapon, SelectedEquipmentSlot, out string error);
            Message = success ? "가방으로 옮겼습니다." : error;
            return success;
        }
        public void Notify(string message) { Message = message; }
        public void NotifyAcquisition(string message) { notifications.Enqueue(new Notification { Message = message }); }
        public void NotifyPickupFailure(string message)
        {
            Message = message;
            if (ToastMessage == message) return;
            foreach (var pending in notifications) if (pending.Message == message) return;
            notifications.Enqueue(new Notification { Message = message });
        }
        void OnAcquired(InkInstance item) { notifications.Enqueue(new Notification { Item = item }); }
        public void Tick(double unscaledNow)
        {
            double delta = double.IsNaN(lastToastTick) ? 0 : Math.Max(0, unscaledNow - lastToastTick);
            lastToastTick = unscaledNow;
            if (IsOpen || Paused) { if (ToastVisible) toastUntil += delta; return; }
            if (ToastVisible && unscaledNow >= toastUntil) { Toast = null; ToastMessage = null; }
            if (!ToastVisible && notifications.Count > 0)
            { var next = notifications.Dequeue(); Toast = next.Item; ToastMessage = next.Message; toastUntil = unscaledNow + 2.2; }
        }
        public void Reset()
        {
            Close(); if (Paused) TogglePause(); SelectedBag = -1; SelectedEquipmentSlot = -1; SelectedWeapon = 0; Filter = null;
            Toast = null; ToastMessage = null; lastToastTick = double.NaN; notifications.Clear(); Message = "새 테스트를 시작했습니다.";
        }
        public void Dispose() { Reset(); controls?.Dispose(); Inventory.Acquired -= OnAcquired; }
    }
}
