using System;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Run;
using UnityEngine;

namespace Deviant.Inventory.Unity
{
    // Session composition/Unity ports. Inventory ownership and UI selection stay in plain C#.
    public sealed class InventoryRuntimeHost : MonoBehaviour, IInkEquipmentPort
    {
        public CombatArena Arena { get; private set; }
        public InventoryContent Content { get; private set; }
        public InkInventory Inventory { get; private set; }
        public InventoryPresenter Presenter { get; private set; }
        public Sprite FallbackIcon { get; private set; }
        public PixelInventoryTheme Theme { get; private set; }
        public IReadOnlyList<GroundInkPickup> Drops => drops.AsReadOnly();
        public GroundInkPickup PendingExchange { get; private set; }
        readonly List<GroundInkPickup> drops = new List<GroundInkPickup>();
        readonly Dictionary<TestEnemy, DeathState> deaths = new Dictionary<TestEnemy, DeathState>();
        readonly Dictionary<string, InkDefinition> definitions = new Dictionary<string, InkDefinition>();
        readonly int[] baseForms = new int[2], baseSpecials = new int[2];
        readonly int[][] baseStats = { new int[5], new int[5] };
        Texture2D fallbackTexture;
        bool initialized, applying;
        WeaponGrowth boundGrowth;
        Func<int> readBagUsage;
        public WeaponGrowth Growth { get { BindGrowth(); return boundGrowth; } }
        public int TotalStored => Inventory.Used+(Growth?.Inks.Count??0);
        void BindGrowth()
        {
            var next=Arena?Arena.MvpGrowth:null;
            if(ReferenceEquals(next,boundGrowth))return;
            if(boundGrowth!=null){boundGrowth.Acquired-=ColorAcquired;if(ReferenceEquals(boundGrowth.ExternalUsage,readBagUsage))boundGrowth.ExternalUsage=null;}
            boundGrowth=next;
            if(boundGrowth!=null){boundGrowth.ExternalUsage=readBagUsage;boundGrowth.Acquired+=ColorAcquired;}
        }
        // The bag takes the run's departure size from the run growth (village bag unlock). Outside a change notification,
        // so the bag never resizes while it publishes.
        void SyncBagCapacity(){if(Inventory!=null&&boundGrowth!=null&&Inventory.Capacity<boundGrowth.InkCapacity)Inventory.SetCapacity(boundGrowth.InkCapacity);}
        void ColorAcquired(GrowthInk ink){var message=InventoryPresenter.AcquisitionText(ink.Part.name,"잉크");Presenter.Notify(message);Presenter.NotifyAcquisition(message);}
        // Colour inks are equipped and freely replaced from the bag at any time, in combat too; the replaced ink returns to
        // the owned inks (user 2026-11-03). Combat reads the palette per action, so the change applies to the next one.
        public bool TryEquipColor(Guid ink,int weapon,int slot,out string error)
        {
            error=null;
            if(!Arena||!Arena.Player||Arena.Player.Dead){error="지금은 장착할 수 없습니다.";return false;}
            if(Growth==null||weapon<0||weapon>=Arena.Player.weapons.Count){error="팔레트가 없습니다.";return false;}
            try{Growth.Insert(Arena.Player.weapons[weapon].InstanceId,slot,ink);return true;}catch(InvalidOperationException e){error=e.Message;return false;}
        }
        // Unequipping a mounted colour ink is free from the bag at any time, in combat too (user 2026-11-05).
        public bool TryUnequipColor(int weapon,int slot,out string error)
        {
            error=null;
            if(!Arena||!Arena.Player||Arena.Player.Dead){error="지금은 해제할 수 없습니다.";return false;}
            if(Growth==null||weapon<0||weapon>=Arena.Player.weapons.Count){error="팔레트가 없습니다.";return false;}
            try{Growth.Detach(Arena.Player.weapons[weapon].InstanceId,slot);return true;}catch(InvalidOperationException e){error=e.Message;return false;}
        }
        // Destroying an owned colour ink is also allowed from the bag; only explicit destruction pays the recovery bonus.
        public bool TryDestroyColor(Guid ink,out string error)
        {
            error=null;
            if(!Arena||!Arena.Player||Arena.Player.Dead||Growth==null){error="지금은 파괴할 수 없습니다.";return false;}
            try{Growth.Destroy(ink);return true;}catch(InvalidOperationException e){error=e.Message;return false;}
        }
        sealed class DeathState { public bool dead, paid; public Vector2 position; }
        public void Initialize(CombatArena arena, InventoryContent content)
        {
            if (initialized) throw new InvalidOperationException("Inventory already initialized.");
            if (!arena || !arena.Player || !content) throw new ArgumentException("Initialized arena/content required.");
            content.Validate(); Arena = arena; Content = content;
            Theme = Resources.Load<PixelInventoryTheme>("PixelInventory/Theme");
            if (Theme && !Theme.Ready) { Debug.LogWarning("Pixel inventory art is incomplete. Restore the inventory UI asset ZIP."); Theme = null; }
            foreach (var entry in content.items) definitions.Add(entry.id, entry.CreateDefinition());
            var categories = new string[2];
            for (int w = 0; w < 2; w++)
            {
                baseForms[w] = arena.Player.weapons[w].FormIndex; baseSpecials[w] = arena.Player.weapons[w].SpecialIndex;
                for (int slot = 0; slot < 5; slot++) baseStats[w][slot] = arena.Player.weapons[w].StatSlots[slot];
                categories[w] = arena.catalog.forms[baseForms[w]].category;
            }
            Inventory = new InkInventory(categories, this, content.capacity);
            readBagUsage=()=>Inventory.Used;
            Inventory.ExternalUsage=()=>{BindGrowth();return boundGrowth?.Inks.Count??0;};
            Presenter = new InventoryPresenter(Inventory, arena.Controls);
            Presenter.Discard = DiscardAt;
            Presenter.Destroy = DestroyAt;
            fallbackTexture = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color[256];
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                pixels[y * 16 + x] = Math.Abs(x - 7.5f) <= (y < 8 ? y * .65f : (16 - y) * .85f) ? Color.white : Color.clear;
            fallbackTexture.SetPixels(pixels); fallbackTexture.Apply();
            FallbackIcon = Sprite.Create(fallbackTexture, new Rect(0, 0, 16, 16), Vector2.one * .5f, 32);
            Arena.ResetCompleted += ResetSession; initialized = true;
            Arena.Player.WeaponOrderSwapped += SwapWeaponSlots;
            BindGrowth(); SyncBagCapacity();
            var keyboard = GetComponent<InventoryKeyboardInput>();
            if (!keyboard) keyboard = gameObject.AddComponent<InventoryKeyboardInput>();
            keyboard.Host = this;
        }
        void Update() { if (initialized) Advance(); }
        public void Advance()
        {
            if (!initialized || !Arena || !Arena.Player) return;
            BindGrowth(); SyncBagCapacity();
            CaptureUnmodifiedBase();
            Presenter.Tick(Time.unscaledTimeAsDouble);
            if (Arena.Player.Dead) { Presenter.Close(); if (Presenter.Paused) Presenter.TogglePause(); return; }
            if (Arena.Controls.Current.SimulationPaused) return;
            foreach (var enemy in Arena.enemies.ToArray())
            {
                if (!enemy || !enemy.gameObject.activeInHierarchy) continue;
                if (!deaths.TryGetValue(enemy, out var state)) deaths.Add(enemy, state = new DeathState());
                if (!enemy.Dead)
                {
                    if (state.dead)
                    {
                        state.dead = state.paid = false;
                        if (enemy.bodyCollider) enemy.bodyCollider.enabled = true;
                        if (enemy.hurtCollider) enemy.hurtCollider.enabled = true;
                    }
                    continue;
                }
                if (!state.dead)
                {
                    state.dead = true; state.position = enemy.Position; enemy.CancelAttack();
                    if (enemy.bodyCollider) enemy.bodyCollider.enabled = false;
                    if (enemy.hurtCollider) enemy.hurtCollider.enabled = false;
                    // Loot appears on lethal damage; the death animation continues independently.
                }
                if (!state.paid)
                {
                    state.paid = true;
                    var table = DropTable(enemy); if (table == null) continue;
                    int total = 0; foreach (var entry in table.entries) total += entry.amount;
                    int index = 0;
                    foreach (var entry in table.entries) for (int n = 0; n < entry.amount; n++, index++)
                        SpawnDrop(entry.itemId, Arena.DropPoint(state.position, (index - (total - 1) * .5f) * .8f, 1f), state.position);
                }
            }
            foreach (var drop in drops.ToArray()) if (drop) drop.Step(Arena.Now); else drops.Remove(drop);
            if (PendingExchange && (PendingExchange.Collected || Vector2.Distance(Arena.Player.Center, PendingExchange.transform.position) > 3))
                PendingExchange = null;
        }
        InkDropTable DropTable(TestEnemy enemy)
        {
            var source = enemy.GetComponent<InkDropSource>();
            string key = source ? source.tableKey : enemy is FieldMouseEnemy ? "field_mouse" : enemy.profile == 1 ? "ranged" : enemy.isBoss ? "boss" : "melee";
            return Content.Table(key);
        }
        public GroundInkPickup SpawnDrop(string id, Vector2 at, bool settled = false)
            => SpawnDrop(new InkInstance(definitions[id]), at, settled);
        // source: the defeated monster's feet; the drop is thrown from its body to `at`.
        public GroundInkPickup SpawnDrop(string id, Vector2 at, Vector2 source)
            => SpawnDrop(new InkInstance(definitions[id]), at, false, source);
        GroundInkPickup SpawnDrop(InkInstance item, Vector2 at, bool settled, Vector2? source = null)
        {
            var go = new GameObject("Ink pickup · " + item.Definition.Id); go.transform.SetParent(transform, false);
            var floor = Arena.FindFloor(at + Vector2.up * .5f, 64);
            if (floor.HasValue) at.y = floor.Value + .26f;
            var drop = go.AddComponent<GroundInkPickup>(); drop.Initialize(this, item, at, settled, source); drops.Add(drop); return drop;
        }
        public bool TryCollect(GroundInkPickup drop)
        {
            if (!initialized || !drop || drop.Owner != this || !drops.Contains(drop) || !drop.Ready || drop.Collected || Arena.Player.Dead) return false;
            if (!new Bounds(Arena.Player.Center, Arena.Player.size).Intersects(new Bounds(drop.transform.position, new Vector3(.6f, .6f, 1)))) return false;
            if (!Inventory.TryAcquire(drop.Item, out string error))
            {
                if (!drop.ReportedFull) { Presenter.NotifyPickupFailure(error); drop.ReportedFull = true; }
                PendingExchange = drop; return false;
            }
            if (PendingExchange == drop) PendingExchange = null;
            drops.Remove(drop); drop.Claim(); return true;
        }
        public bool ExchangeSelected()
        {
            var drop = PendingExchange;
            if (!drop || drop.Collected || Presenter.SelectedBag < 0 || Arena.Player.Dead ||
                Vector2.Distance(Arena.Player.Center, drop.transform.position) > 3) return false;
            if (!Inventory.TryExchangeGround(Presenter.SelectedBag, drop.Item, out var outgoing, out string error))
            { Presenter.Notify(error); return false; }
            var at = (Vector2)drop.transform.position; drops.Remove(drop); drop.Claim(); PendingExchange = null;
            SpawnDrop(outgoing, at, true); Presenter.Notify("교환했습니다. 기존 잉크는 바닥에 남습니다."); return true;
        }
        public bool HasBagRoom => initialized && Inventory.HasRoom;
        // Puts a new ink straight into the bag (maintenance draws); acquisition raises the usual pickup toast.
        public InkInstance Grant(string id)
        {
            if (!initialized || !definitions.TryGetValue(id, out var definition)) return null;
            var item = new InkInstance(definition);
            return Inventory.TryAcquire(item, out _) ? item : null;
        }
        // Leaves a bag ink at the player's feet; it is picked up again only after the player steps off it.
        public string DiscardAt(int bagSlot)
        {
            if (!initialized || !Arena.Player || Arena.Player.Dead) return "지금은 버릴 수 없습니다.";
            if (!Inventory.TryDiscard(bagSlot, out var item, out string error)) return error;
            SpawnDrop(item, Arena.Player.Position, true).WaitForPlayerToLeave();
            return null;
        }
        // Removes a bag ink for good to make room: nothing is dropped and nothing is paid.
        public string DestroyAt(int bagSlot)
        {
            if (!initialized || !Arena.Player || Arena.Player.Dead) return "지금은 파괴할 수 없습니다.";
            return Inventory.TryDiscard(bagSlot, out _, out string error) ? null : error;
        }
        public string Apply(EquipmentLayout candidate)
        {
            if (!Arena || !Arena.Player || Arena.Player.Dead || !Arena.Player.EditingAllowed || applying)
                return Arena && Arena.Player ? Arena.Player.MaintenanceUnavailableReason : "전투 중이거나 동작 중에는 정비할 수 없습니다.";
            var forms = new int[2]; var specials = new int[2]; var stats = new int[2][];
            for (int w = 0; w < 2; w++)
            {
                var shape = candidate.Get(w, 0); var special = candidate.Get(w, candidate.StatSlots + 1);
                forms[w] = shape == null ? baseForms[w] : Array.FindIndex(Arena.catalog.forms, f => f.id == shape.Definition.FormId);
                specials[w] = special == null ? baseSpecials[w] : special.Definition.Value;
                if (forms[w] < 0 || specials[w] < 0 || specials[w] >= Arena.catalog.forms[forms[w]].specials.Length)
                    return "현재 무기에 연결되지 않은 잉크입니다.";
                stats[w] = new int[5];
                for (int s = 0; s < 5; s++) stats[w][s] = candidate.Get(w, s + 1)?.Definition.Value ?? baseStats[w][s];
            }
            applying = true;
            try
            {
                for (int w = 0; w < 2; w++)
                {
                    var current = Arena.Player.weapons[w]; bool changed = current.FormIndex != forms[w] || current.SpecialIndex != specials[w];
                    for (int s = 0; s < 5; s++) changed |= current.StatSlots[s] != stats[w][s];
                    if (!changed) continue;
                    current.StatSlots.CopyFrom(stats[w]);
                    Arena.Player.Reconfigure(w, forms[w], specials[w]);
                }
            }
            finally { applying = false; }
            return null;
        }
        public string WeaponName(int slot) => Arena.catalog.forms[Arena.Player.weapons[slot].FormIndex].name;
        public Sprite ItemSprite(InkInstance item)
        { var art = Content.Find(item.Definition.Id); return art.icon ? art.icon : Theme && Theme.Item(item.Definition) ? Theme.Item(item.Definition) : FallbackIcon; }
        public Color ItemTint(InkInstance item) => ItemSprite(item) == FallbackIcon ? Content.Find(item.Definition.Id).tint : Color.white;
        public string SelectedPreview()
        {
            var item = Presenter.Selected; int w = Presenter.SelectedWeapon, slot = Presenter.SuggestedSlot;
            if (item == null || slot < 0 || !Inventory.Fits(item, w, slot)) return "";
            var current = Arena.Player.weapons[w]; var form = Arena.catalog.forms[current.FormIndex];
            int targetForm = item.Definition.Kind == InkKind.Form ? Array.FindIndex(Arena.catalog.forms, f => f.id == item.Definition.FormId) : current.FormIndex;
            if (targetForm < 0) return "";
            if (item.Definition.Kind == InkKind.Stat)
            {
                int kind = item.Definition.Value, old = current.StatSlots[slot - 1];
                if (kind == 3) return current.Speed.ToString("0.0") + " → " + (current.Speed + .1f - (old == 3 ? .1f : 0)).ToString("0.0");
                if (kind == 4) return (current.Reduction * 100).ToString("0") + "% → " + ((current.Reduction + .1f - (old == 4 ? .1f : 0)) * 100).ToString("0") + "%";
                float amount = kind == 1 ? form.basics[0].damage : form.specials[current.SpecialIndex].hits.Length > 0 ? form.specials[current.SpecialIndex].hits[0].damage : 0;
                float bonus = kind == 1 ? current.BasicBonus : current.SpecialBonus;
                if (kind == 2 && amount == 0) return Inactive(item, w) ? "" : "특수 효과 피해 강화";
                return (amount * (1 + bonus)).ToString("0.#") + " → " + (amount * (1 + bonus + .2f - (old == kind ? .2f : 0))).ToString("0.#");
            }
            if (item.Definition.Kind == InkKind.Form)
                return (form.basics[0].damage * (1 + current.BasicBonus)).ToString("0.#") + " → " + (Arena.catalog.forms[targetForm].basics[0].damage * (1 + current.BasicBonus)).ToString("0.#");
            return form.specials[item.Definition.Value].name;
        }
        public bool Inactive(InkInstance item, int weapon) => InactiveReason(item, weapon) != null;
        public string InactiveReason(InkInstance item, int weapon)
        {
            if (item == null || item.Definition.Kind != InkKind.Stat) return null;
            var state = Arena.Player.weapons[weapon]; var form = Arena.catalog.forms[state.FormIndex];
            var effect = form.specials[state.SpecialIndex].effect;
            if (item.Definition.Value == 2 && effect != null &&
                (effect.type == "owner_guard" || effect.type == "stealth" || effect.type == "smoke" || effect.type == "decoy"))
                return "피해 없는 특수 기술";
            return null;
        }
        void SwapWeaponSlots()
        {
            (baseForms[0], baseForms[1]) = (baseForms[1], baseForms[0]);
            (baseSpecials[0], baseSpecials[1]) = (baseSpecials[1], baseSpecials[0]);
            (baseStats[0], baseStats[1]) = (baseStats[1], baseStats[0]);
            Inventory.SwapWeaponSlots();
        }
        void CaptureUnmodifiedBase()
        {
            for (int w = 0; w < 2; w++)
            {
                var current = Arena.Player.weapons[w];
                if (Inventory.EquippedAt(w, 0) == null)
                {
                    baseForms[w] = current.FormIndex;
                    Inventory.SetUnmodifiedWeaponCategory(w, Arena.catalog.forms[current.FormIndex].category);
                }
                if (Inventory.EquippedAt(w, 6) == null) baseSpecials[w] = current.SpecialIndex;
                for (int s = 0; s < 5; s++) if (Inventory.EquippedAt(w, s + 1) == null) baseStats[w][s] = current.StatSlots[s];
            }
        }
        void ResetSession()
        {
            // Keep developer-supplied gear, remove only contributions owned by physical inks.
            CaptureUnmodifiedBase();
            Presenter.Reset(); Inventory.Clear(); PendingExchange = null;
            foreach (var drop in drops) if (drop) { drop.gameObject.SetActive(false); Destroy(drop.gameObject); }
            drops.Clear(); deaths.Clear();
            for (int w = 0; w < 2; w++)
            {
                var weapon = Arena.Player.weapons[w];
                weapon.StatSlots.CopyFrom(baseStats[w]);
                weapon.FormIndex = baseForms[w]; weapon.SpecialIndex = baseSpecials[w];
                var skill = Arena.catalog.forms[baseForms[w]].specials[baseSpecials[w]];
                weapon.Cooldown.Reset(skill.capacity, weapon.CooldownPeriod(skill.cooldown));
            }
            foreach (var enemy in Arena.enemies)
            { if (enemy.bodyCollider) enemy.bodyCollider.enabled = true; if (enemy.hurtCollider) enemy.hurtCollider.enabled = true; }
        }
        [ContextMenu("Spawn temporary test inks")]
        public void SpawnTestInks()
        {
            if (!initialized) return;
            for (int i = 0; i < Content.items.Length; i++) SpawnDrop(Content.items[i].id, Arena.Player.Position + Vector2.right * (1 + i * .6f));
        }
        void OnDisable() { Presenter?.Close(); if (Presenter != null && Presenter.Paused) Presenter.TogglePause(); }
        void OnDestroy()
        {
            if(boundGrowth!=null){boundGrowth.Acquired-=ColorAcquired;if(ReferenceEquals(boundGrowth.ExternalUsage,readBagUsage))boundGrowth.ExternalUsage=null;}
            if (Arena) Arena.ResetCompleted -= ResetSession;
            if (Arena && Arena.Player) Arena.Player.WeaponOrderSwapped -= SwapWeaponSlots;
            Presenter?.Dispose();
            foreach (var drop in drops) if (drop) Destroy(drop.gameObject);
            if (FallbackIcon) Destroy(FallbackIcon); if (fallbackTexture) Destroy(fallbackTexture);
        }
    }
}
