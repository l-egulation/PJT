using System;
using System.Collections.Generic;
using System.Linq;

namespace Deviant.UI.Integration
{
    // Session-local capability: retain this object with the rendered card, never substitute a list index.
    public sealed class CommerceKey { internal CommerceKey() { } }

    public sealed class CommerceItem
    {
        public string FormId { get; }
        public string SkillId { get; }
        public string InkId { get; }
        public bool IsInk => InkId != null;
        public IReadOnlyList<string> EnhancedSlots { get; }
        internal CommerceItem(string form, string skill, string ink, IEnumerable<string> slots)
        { FormId = form; SkillId = skill; InkId = ink; EnhancedSlots = Array.AsReadOnly((slots ?? Array.Empty<string>()).ToArray()); }
    }

    public sealed class CommerceCard
    {
        public CommerceKey Key { get; }
        public int Slot { get; }
        public CommerceItem Item { get; }
        public int? Price { get; }
        public bool Sold { get; }
        public bool Empty => Item == null;
        public bool CanAct { get; }
        internal CommerceCard(CommerceKey key, int slot, CommerceItem item, int? price, bool sold, bool canAct)
        { Key = key; Slot = slot; Item = item; Price = price; Sold = sold; CanAct = canAct; }
    }

    public sealed class CommerceReceipt
    {
        public CommerceKey Key { get; } = new CommerceKey();
        public CommerceItem Item { get; }
        public Guid? WeaponId { get; }
        public bool FromShop { get; }
        internal CommerceReceipt(CommerceItem item, Guid? weaponId, bool fromShop)
        { Item = item; WeaponId = weaponId; FromShop = fromShop; }
    }

    // Remaining includes draft reservations; placing does not spend, confirming does.
    public sealed class CommerceInkStock
    {
        public string InkId { get; }
        public int Remaining { get; }
        public int Reserved { get; }
        public int Available { get; }
        internal CommerceInkStock(string id, int remaining, int reserved, int available)
        { InkId = id; Remaining = remaining; Reserved = reserved; Available = available; }
    }

    public sealed class CommerceSnapshot
    {
        public int Stage { get; }
        public int Gold { get; }
        public int BankedRewards { get; }
        public bool IsBossReward { get; }
        public bool BagOpen { get; }
        public CommerceReceipt Receipt { get; }
        public IReadOnlyList<CommerceCard> Rewards { get; }
        public IReadOnlyList<CommerceCard> Shop { get; }
        public IReadOnlyList<CommerceInkStock> Inks { get; }
        internal CommerceSnapshot(int stage, int gold, int banked, bool boss, bool bag, CommerceReceipt receipt,
            CommerceCard[] rewards, CommerceCard[] shop, CommerceInkStock[] inks)
        { Stage = stage; Gold = gold; BankedRewards = banked; IsBossReward = boss; BagOpen = bag; Receipt = receipt;
            Rewards = Array.AsReadOnly(rewards); Shop = Array.AsReadOnly(shop); Inks = Array.AsReadOnly(inks); }
    }

    public enum CommerceResult { Applied, Stale, Busy, SoldOut, InsufficientGold, Unavailable }

    public interface IRunCommerceHost
    {
        // Own one named control scope; releasing it must not release a shop/tutorial/other owner's pause.
        void SetCommercePaused(bool paused);
        // null/null means compare; otherwise select the already acquired item. Closing calls adapter.BagClosed().
        void OpenBag(Guid? weaponId, string inkId);
    }
}
