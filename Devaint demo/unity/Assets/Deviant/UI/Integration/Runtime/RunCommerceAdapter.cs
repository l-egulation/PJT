using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Run;

namespace Deviant.UI.Integration
{
    // Main-thread pull adapter. The host refreshes after external state changes; do not render from the model's
    // synchronous Changed events (Choose/Buy publish intermediate arsenal/economy states before completing).
    public sealed class RunCommerceAdapter : IDisposable
    {
        readonly SketchRunState state;
        readonly RunEconomy economy;
        readonly IRunCommerceHost host;
        readonly SketchEquipment equipment;
        SketchCandidate[] rewardSource;
        CommerceKey[] rewardKeys = Array.Empty<CommerceKey>();
        readonly Dictionary<string, Shelf> shelves = new Dictionary<string, Shelf>();
        CommerceReceipt receipt;
        bool busy, bagOpen, paused, disposed;

        sealed class Shelf
        {
            public int Stage;
            public SketchShopOffer[] Offers = Array.Empty<SketchShopOffer>();
            public string[] Inks = Array.Empty<string>();
            public CommerceKey[] Keys = Array.Empty<CommerceKey>();
        }

        public RunCommerceAdapter(SketchRunState state, RunEconomy economy, IRunCommerceHost host)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.economy = economy ?? throw new ArgumentNullException(nameof(economy));
            this.host = host ?? throw new ArgumentNullException(nameof(host));
        }
        // Preferred for the current game: preserve SketchEquipment's preview adoption and runtime ink wrappers.
        public RunCommerceAdapter(SketchEquipment equipment, IRunCommerceHost host)
            : this((equipment ?? throw new ArgumentNullException(nameof(equipment))).State, equipment.Economy, host)
        { this.equipment = equipment; }

        public CommerceSnapshot Snapshot(string facility = null)
        {
            RequireAlive();
            if (busy) throw new InvalidOperationException("Refresh commerce after the current command completes.");
            RefreshRewards();
            var rewards = receipt == null && rewardSource != null
                ? rewardSource.Select((c, i) => new CommerceCard(rewardKeys[i], i, Item(c), null, false, !bagOpen)).ToArray()
                : Array.Empty<CommerceCard>();
            var shop = facility == null ? Array.Empty<CommerceCard>() : ReadShop(facility);
            UpdatePause();
            return new CommerceSnapshot(state.Stage, economy.Gold, state.Banked, receipt == null && state.PendingIsBoss,
                bagOpen, receipt, rewards, shop, state.Arsenal.Codex.Select(id => new CommerceInkStock(id,
                    state.Arsenal.RemainingInkUses(id), state.Arsenal.ReservedInkUses(id), state.Arsenal.AvailableInkUses(id))).ToArray());
        }

        public CommerceResult Choose(CommerceKey key)
        {
            RequireAlive();
            if (Blocked) return CommerceResult.Busy;
            RefreshRewards();
            int index = Array.IndexOf(rewardKeys, key);
            if (key == null || rewardSource == null || index < 0) return CommerceResult.Stale;
            var item = Item(rewardSource[index]);
            busy = true;
            try
            {
                var weapon = equipment == null ? state.Choose(index) : equipment.ChooseReward(index).Weapon?.Model;
                receipt = new CommerceReceipt(item, weapon?.Id, false);
                return CommerceResult.Applied;
            }
            finally { busy = false; UpdatePause(); }
        }

        public CommerceResult Buy(string facility, CommerceKey key)
        {
            RequireAlive();
            if (Blocked || state.PendingOffer != null) return CommerceResult.Busy;
            if (key == null || string.IsNullOrEmpty(facility)) return CommerceResult.Stale;
            // A reroll can replace a weapon offer or mutate an ink offer; validate its current displayed identity.
            var cards = ReadShop(facility);
            var card = cards.FirstOrDefault(c => ReferenceEquals(c.Key, key));
            if (card == null) return CommerceResult.Stale;
            if (card.Empty || card.Sold) return CommerceResult.SoldOut;
            if (economy.Gold < card.Price.Value) return CommerceResult.InsufficientGold;
            busy = true;
            try
            {
                var weapon = equipment == null ? state.Buy(facility, card.Slot, economy) : equipment.Buy(facility, card.Slot).Weapon?.Model;
                receipt = new CommerceReceipt(card.Item, weapon?.Id, true);
                return CommerceResult.Applied;
            }
            finally { busy = false; UpdatePause(); }
        }

        public CommerceResult ContinueReceipt(CommerceKey key)
        {
            RequireAlive();
            if (busy || bagOpen) return CommerceResult.Busy;
            if (key == null || receipt == null || !ReferenceEquals(key, receipt.Key)) return CommerceResult.Stale;
            receipt = null;
            UpdatePause(); // A queued boss remains paused; banked rewards are opened only by the host's run flow.
            return CommerceResult.Applied;
        }

        public CommerceResult ConfigureReceipt(CommerceKey key)
        {
            RequireAlive();
            if (busy || bagOpen) return CommerceResult.Busy;
            if (key == null || receipt == null || !ReferenceEquals(key, receipt.Key)) return CommerceResult.Stale;
            var acquired = receipt;
            receipt = null; bagOpen = true; busy = true;
            try { UpdatePause(); host.OpenBag(acquired.WeaponId, acquired.Item.InkId); }
            finally { busy = false; UpdatePause(); }
            return CommerceResult.Applied;
        }

        public CommerceResult CompareBag()
        {
            RequireAlive();
            if (Blocked) return CommerceResult.Busy;
            if (state.PendingOffer == null) return CommerceResult.Unavailable;
            bagOpen = true; busy = true;
            try { UpdatePause(); host.OpenBag(null, null); }
            finally { busy = false; UpdatePause(); }
            return CommerceResult.Applied;
        }

        public void BagClosed()
        {
            RequireAlive();
            // A host may reject/close its bag synchronously inside OpenBag. Preserve that notification,
            // but let the opening command's finally publish pause changes after its callback returns.
            bagOpen = false;
            if (!busy) UpdatePause();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; receipt = null;
            if (paused) { paused = false; host.SetCommercePaused(false); }
        }

        bool Blocked => busy || bagOpen || receipt != null;
        void RequireAlive() { if (disposed) throw new ObjectDisposedException(nameof(RunCommerceAdapter)); }
        void RefreshRewards()
        {
            if (ReferenceEquals(rewardSource, state.PendingOffer)) return;
            rewardSource = state.PendingOffer;
            rewardKeys = rewardSource == null ? Array.Empty<CommerceKey>() : rewardSource.Select(_ => new CommerceKey()).ToArray();
        }
        CommerceCard[] ReadShop(string facility)
        {
            if (string.IsNullOrEmpty(facility)) throw new ArgumentException("A facility id is required.", nameof(facility));
            var stock = state.ShopStock(facility);
            if (!shelves.TryGetValue(facility, out var shelf) || shelf.Stage != state.Stage)
                shelves[facility] = shelf = new Shelf { Stage = state.Stage };
            // Match the actual shelf, including progression expansion and a short/empty ink catalog.
            // Resizing preserves capability keys for unchanged places.
            if (shelf.Offers.Length != stock.Count)
            {
                Array.Resize(ref shelf.Offers, stock.Count);
                Array.Resize(ref shelf.Inks, stock.Count);
                Array.Resize(ref shelf.Keys, stock.Count);
            }
            var cards = new CommerceCard[stock.Count];
            for (int i = 0; i < cards.Length; i++)
            {
                var offer = stock[i];
                if (!ReferenceEquals(shelf.Offers[i], offer) || shelf.Inks[i] != offer?.Ink)
                {
                    shelf.Offers[i] = offer; shelf.Inks[i] = offer?.Ink;
                    shelf.Keys[i] = offer == null ? null : new CommerceKey();
                }
                var item = offer == null || offer.Empty ? null : Item(offer.Weapon, offer.Ink);
                cards[i] = new CommerceCard(shelf.Keys[i], i, item, offer?.Price, offer?.Sold ?? false,
                    item != null && !offer.Sold && economy.Gold >= offer.Price && !Blocked && state.PendingOffer == null);
            }
            return cards;
        }
        void UpdatePause()
        {
            bool required = !disposed && (receipt != null || state.PendingOffer != null || bagOpen);
            if (paused == required) return;
            paused = required; host.SetCommercePaused(required);
        }
        static CommerceItem Item(SketchCandidate candidate) => Item(candidate.Weapon, candidate.Ink);
        static CommerceItem Item(SketchWeaponDraft weapon, string ink) =>
            new CommerceItem(weapon?.Form, weapon?.Skill, ink, weapon?.Enhance);
    }
}
