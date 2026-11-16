using System;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Control.Contracts;
using Deviant.Run;
using Deviant.UI.Theme;
using ForestCombatV2;
using UnityEngine;

namespace Deviant.Game
{
    // The maintenance stock laid out in the room (user decision and approved mock-up 2026-11-14). The shop art
    // (MaintenanceShops/stage1-shop, stage2-shop) draws a shelf plank two and a half units above the floor beside the
    // artisan; this component lays a one-way platform on that plank, stands the inks on it and the weapons on the floor
    // below, each on a price plate. The hero walks (or jumps once) up to a piece and buys it with Interact; there is no
    // shop window and no purchase animation. Runs that unlocked shared rerolls get a supply crate at the end of each row:
    // striking it rattles the crate and swaps the row's unsold pieces in a dust ring. The legacy growth path (no sketch
    // equipment) keeps the artisan's window and builds nothing here.
    public sealed class MaintenanceShelfDisplay : MonoBehaviour
    {
        // One shop drawing per row length (3-6 places: three goods, up to two more from the village's shop display unlock,
        // and the reroll crate), so the spacing never narrows (user pick 2026-11-14, plan A). In units from the artisan's
        // floor point (resources environments/maintenance-shop-shelves-20261114/runtime/layout.json): the drawing's centre,
        // the plank, the hanging sign, and how far the artisan stands left of the room's centre so the drawing stays inside.
        public struct DisplayLayout { public int columns; public float art, shift, top, from, to; public Vector2 sign; public string Art(int chapter) => "stage" + (chapter + 1) + "-shop-" + columns; }
        public const int MinColumns = 3, MaxColumns = 6;
        static readonly float[][] Widths =
        {   // art centre, artisan shift, plank end, sign x            (plank start 2.625 / 4.90625, top 2.5)
            new[] { 3.53125f, 0, 10.03125f, 11.96875f, 4.75f, -2, 12.46875f, 14.40625f, 5.96875f, -4.5f, 14.90625f, 16.84375f, 7.1875f, -7, 17.34375f, 19.28125f },
            new[] { 3f, 0, 12f, -3.84375f, 4.1875f, 0, 14.375f, -3.84375f, 5.375f, 0, 16.75f, -3.84375f, 6.5625f, -1.5f, 19.125f, -3.84375f },
        };
        public static DisplayLayout Display(int chapter, int columns)
        {
            int c = Mathf.Clamp(chapter, 0, 1), n = Mathf.Clamp(columns, MinColumns, MaxColumns), i = (n - MinColumns) * 4; var w = Widths[c];
            return new DisplayLayout { columns = n, art = w[i], shift = w[i + 1], top = 2.5f, from = c == 0 ? 2.625f : 4.90625f, to = w[i + 2],
                sign = new Vector2(w[i + 3], c == 0 ? 3.75f : 3.6875f) };
        }
        // The row length this run will lay out: three goods, the shop display unlock's extra places, the reroll crate.
        public int PlannedColumns => Mathf.Clamp(3 + (Sketch != null ? Sketch.State.ExtraShopItems : 0) + (RerollsUnlocked ? 1 : 0), MinColumns, MaxColumns);
        // Called when the rolled stock needs another row length than the drawing placed for the room (perks bound late):
        // (site index, columns) -> the artisan's new floor point.
        public Func<int, int, Vector2> Resize;
        // Reach of one piece: the hero's feet within this many units sideways, on the piece's own row.
        public const float Reach = 1.1f;
        // Price plate (pixels) and the height pieces stand at on it.
        public const int PlateWidth = 56, CratePlateWidth = 48, PlateHeight = 16;
        const float PieceBase = PlateHeight / 32f;
        // Approved art drawn at the mock-up's size: ink bottles 45 px tall, weapons 42 px on their long side.
        const int InkPixels = 45, WeaponPixels = 42;

        public enum Kind { Weapon, Ink, RerollWeapons, RerollInks }
        public sealed class Spot
        {
            public Kind Kind { get; internal set; }
            public bool Upper => Kind == Kind.Ink || Kind == Kind.RerollInks;
            public bool Reroll => Kind == Kind.RerollWeapons || Kind == Kind.RerollInks;
            // Index into the facility's stock (Sketch.Shop), or -1 for a reroll crate.
            public int Stock { get; internal set; }
            public string Facility { get; internal set; }
            // Where the hero stands to reach it (floor or shelf top) and the centre of the drawn piece.
            public Vector2 Ground { get; internal set; }
            public Vector2 Center { get; internal set; }
            public float Height { get; internal set; }
            // The centre of its price plate (the GUI writes the price or the remaining rerolls there).
            public Vector2 PlateCenter => Ground + Vector2.up * (PlateHeight / 64f);
            internal SpriteRenderer icon, plate; internal object shown; internal float changeAt = -1, rattleAt = -1; internal int puffed;
        }
        sealed class Shelf { public string key; public Rect room; public Vector2 floor; public bool built; public int columns; public float from, to, top; public Transform root; public BoxCollider2D plank; public readonly List<Spot> spots = new List<Spot>(); }
        struct Puff { public Vector2 at; public float start, duration, from, to, size, turn; public int count; public Color color; }

        readonly List<Shelf> shelves = new List<Shelf>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        readonly List<Puff> puffs = new List<Puff>();
        readonly List<SpriteRenderer> dust = new List<SpriteRenderer>();
        Dictionary<string, string> weaponIcons;
        CombatArena arena; Material material; string art; int chapter; SketchRunBinding perks; Transform dustRoot;
        public event Action<SketchChoice> Purchased;
        public event Action<string> Refused;

        public SketchEquipment Sketch => arena ? arena.Sketch : null;
        // Only real sketch runs sell from the shelves.
        public bool Active => Sketch != null;
        public int Chapter => chapter;
        public Spot Nearest { get; private set; }
        public IEnumerable<Spot> Spots { get { foreach (var s in shelves) if (s.built) foreach (var spot in s.spots) yield return spot; } }
        public RunPerks Perks => perks ? perks.Perks : null;
        public bool RerollsUnlocked => Perks != null && Perks.Settings.SharedRerolls > 0;

        public void Initialize(CombatArena owner, int chapterIndex, Material spriteMaterial)
        {
            arena = owner; material = spriteMaterial; chapter = chapterIndex; art = chapter == 0 ? "stage1" : "stage2";
            perks = GetComponent<SketchRunBinding>();
        }

        // The shelf plank becomes a one-way platform right away (the hero may land on it before the stock is rolled).
        public void AddSite(Rect room, Vector2 floor, string facility, int columns)
        {
            var s = new Shelf { room = room, key = facility };
            var go = new GameObject("Maintenance shelf plank · " + facility); go.transform.SetParent(transform, false);
            var surface = go.AddComponent<ArenaSurface>(); surface.oneWay = true; surface.footstepMaterial = FootstepMaterial.Wood;
            s.plank = go.AddComponent<BoxCollider2D>(); arena.RegisterFeedbackSurface(s.plank);
            shelves.Add(s); Place(s, floor, columns);
        }
        // Lays the plank for a row length (the drawing moved with the artisan to the given floor point).
        public void MoveSite(int index, Vector2 floor, int columns) { if (index >= 0 && index < shelves.Count) Place(shelves[index], floor, columns); }
        void Place(Shelf s, Vector2 floor, int columns)
        {
            var layout = Display(chapter, columns);
            s.floor = floor; s.columns = layout.columns; s.from = floor.x + layout.from; s.to = floor.x + layout.to; s.top = floor.y + layout.top;
            s.plank.transform.position = new Vector3((s.from + s.to) / 2, s.top - .125f); s.plank.size = new Vector2(s.to - s.from, .25f);
            Physics2D.SyncTransforms();
        }

        // True while the point is in a shop room between the artisan and the far end of its shelf (the first-visit
        // call-back waits for the hero to leave this zone).
        public bool InShopZone(Vector2 point)
        {
            foreach (var s in shelves)
                if (s.room.Contains(point) && point.x >= Mathf.Min(s.floor.x - 4.5f, s.from - .75f) && point.x <= Mathf.Max(s.floor.x + 4.5f, s.to + .75f) &&
                    point.y <= s.top + 1.5f) return true;
            return false;
        }

        public SketchChoice ChoiceAt(Spot spot)
        {
            if (spot == null || spot.Reroll || Sketch == null) return null;
            var stock = Sketch.Shop(spot.Facility); return spot.Stock >= 0 && spot.Stock < stock.Count ? stock[spot.Stock] : null;
        }

        public bool Available(Spot spot)
        {
            if (spot == null) return false;
            if (spot.Reroll) return RerollsUnlocked;
            var item = ChoiceAt(spot); return item != null && !item.Sold && (item.Weapon != null || item.Ink != null);
        }

        bool InteractionAllowed => Active && arena && arena.Player && !arena.Player.Dead && !arena.Player.InputBlocked &&
            !arena.Controls.Current.SimulationPaused && !arena.Controls.Current.Blocks(InputChannels.Interaction) && Time.timeScale > 0 &&
            !MaintenanceRoomPresentation.IsCapturing(arena);

        // Buys the piece in reach, or strikes its reroll crate. False when nothing is in reach (Interact goes on to others).
        public bool TryInteract()
        {
            var spot = InteractionAllowed ? Nearest : null;
            if (spot == null) return false;
            var player = arena.Player;
            if (player.currentCast || player.Dashing) return true;
            if (spot.Reroll) { Reroll(spot); return true; }
            var item = ChoiceAt(spot);
            if (item == null || item.Sold) return true;
            if (item.Price < 0) { Refuse("가격 미정"); return true; }
            if (Sketch.Economy.Gold < item.Price) { Refuse("골드가 부족합니다."); return true; }
            SketchChoice bought;
            try { bought = Sketch.Buy(spot.Facility, spot.Stock); arena.SyncSketchLoadout(); }
            catch (InvalidOperationException e) { Refuse(e.Message); return true; }
            PresentationSfx.PlaySelected("sfx_ui_confirm");
            Purchased?.Invoke(bought);
            return true;
        }

        void Reroll(Spot spot)
        {
            var p = Perks;
            if (p == null || p.RerollsRemaining <= 0) { Refuse("새로고침 횟수가 없습니다."); return; }
            bool weapons = spot.Kind == Kind.RerollWeapons;
            if (!p.TryReroll(() => Sketch.RerollShop(spot.Facility, weapons))) { Refuse("새로고침할 상품이 없습니다."); return; }
            PresentationSfx.PlaySelected("sfx_ui_confirm");
            // The crate rattles; the row's unsold pieces then change one after another (Refresh holds the old drawing
            // until each one's turn).
            float now = Time.unscaledTime; spot.rattleAt = now; spot.puffed = 0; int k = 0;
            foreach (var s in shelves)
                foreach (var other in s.spots)
                    if (other.Facility == spot.Facility && !other.Reroll && other.Upper == spot.Upper && other.icon.enabled) { other.changeAt = now + .24f + .2f * k++; other.puffed = 0; }
        }

        void Refuse(string message) { PresentationSfx.PlaySelected("sfx_ui_reject"); Refused?.Invoke(message); }

        void Update()
        {
            Nearest = null;
            if (!Active || !arena || !arena.Player) return;
            var p = arena.Player.Position;
            foreach (var s in shelves)
            {
                // A facility's stock is rolled on its first visit, as the window did when it first opened.
                if (!s.built && s.room.Contains(p)) Build(s);
                if (!s.built) continue;
                Refresh(s);
            }
            if (InteractionAllowed) Nearest = FindNearest(p);
            Animate();
        }

        Spot FindNearest(Vector2 feet)
        {
            Spot best = null; float distance = float.PositiveInfinity;
            foreach (var s in shelves)
            {
                if (!s.built || !s.room.Contains(feet)) continue;
                foreach (var spot in s.spots)
                {
                    if (!Available(spot)) continue;
                    float dy = feet.y - spot.Ground.y;
                    // Feet on the piece's own row: the floor below the shelf, or the shelf top.
                    if (dy < -.35f || dy > (spot.Upper ? 1.4f : Mathf.Max(1.4f, s.top - s.floor.y - .6f))) continue;
                    float dx = Mathf.Abs(feet.x - spot.Ground.x);
                    if (dx <= Reach && dx < distance) { best = spot; distance = dx; }
                }
            }
            return best;
        }

        void Build(Shelf s)
        {
            s.built = true;
            var stock = Sketch.Shop(s.key);
            var weapons = new List<int>(); var inks = new List<int>();
            for (int i = 0; i < stock.Count; i++) (stock[i].Weapon != null ? weapons : inks).Add(i);
            int columns = Mathf.Max(1, Mathf.Max(weapons.Count, inks.Count) + (RerollsUnlocked ? 1 : 0));
            // The drawing was chosen before the stock existed; if the row turned out another length, the room is laid out
            // again for it (beyond six places the spacing narrows inside the widest drawing).
            int fit = Mathf.Clamp(columns, MinColumns, MaxColumns);
            if (fit != s.columns && Resize != null) Place(s, Resize(shelves.IndexOf(s), fit), fit);
            s.root = new GameObject("Maintenance shelf · " + s.key).transform; s.root.SetParent(transform, false); s.root.position = s.floor;
            int order = arena.Player.visual.sortingOrder;
            // Pieces evenly along the plank, as the mock-up spaced them (column centres on whole pixels).
            float step = (s.to - s.from) / columns;
            for (int c = 0; c < columns; c++)
            {
                bool rerollColumn = RerollsUnlocked && c == columns - 1;
                int weapon = c < weapons.Count ? weapons[c] : -1, ink = c < inks.Count ? inks[c] : -1;
                float x = Mathf.Round((s.from + step * (c + .5f)) * 32) / 32;
                if (rerollColumn || weapon >= 0) AddSpot(s, rerollColumn ? Kind.RerollWeapons : Kind.Weapon, weapon, new Vector2(x, s.floor.y), order);
                if (rerollColumn || ink >= 0) AddSpot(s, rerollColumn ? Kind.RerollInks : Kind.Ink, ink, new Vector2(x, s.top), order);
            }
        }

        void AddSpot(Shelf s, Kind kind, int stock, Vector2 ground, int order)
        {
            var spot = new Spot { Kind = kind, Stock = kind == Kind.Weapon || kind == Kind.Ink ? stock : -1, Facility = s.key, Ground = ground };
            var root = new GameObject(kind + " · " + stock).transform; root.SetParent(s.root, false); root.position = ground;
            spot.plate = Renderer("Price plate", root, order - 5); spot.plate.sprite = Plate(spot.Reroll ? CratePlateWidth : PlateWidth);
            spot.icon = Renderer("Piece", root, order - 4); spot.icon.transform.localPosition = new Vector3(0, PieceBase);
            if (spot.Reroll) { spot.icon.sprite = Crate(); spot.shown = kind; }
            Measure(spot);
            s.spots.Add(spot);
        }

        SpriteRenderer Renderer(string name, Transform parent, int order)
        {
            var r = new GameObject(name).AddComponent<SpriteRenderer>(); r.transform.SetParent(parent, false);
            r.sortingLayerID = arena.Player.visual.sortingLayerID; r.sortingOrder = order; if (material) r.sharedMaterial = material;
            return r;
        }

        // Swaps a piece's drawing when a reroll replaces it, and empties its plate once it is sold.
        void Refresh(Shelf s)
        {
            float now = Time.unscaledTime;
            foreach (var spot in s.spots)
            {
                if (spot.Reroll) { spot.icon.enabled = spot.plate.enabled = RerollsUnlocked; continue; }
                // A reroll shows the old piece until its turn, then the new one pops up.
                if (spot.changeAt >= 0 && now < spot.changeAt + .16f) continue;
                var item = ChoiceAt(spot);
                bool show = item != null && !item.Sold && (item.Weapon != null || item.Ink != null);
                spot.icon.enabled = show;
                if (!show || ReferenceEquals(spot.shown, item)) continue;
                spot.shown = item; spot.icon.sprite = item.Weapon != null ? WeaponSprite(item.Weapon.Form) : InkSprite(item.Ink.id, item.Ink.color);
                Measure(spot);
            }
        }

        static void Measure(Spot spot)
        {
            float h = spot.icon && spot.icon.sprite ? spot.icon.sprite.bounds.size.y : 1;
            spot.Height = PieceBase + h; spot.Center = spot.Ground + Vector2.up * (PieceBase + h / 2);
        }

        // Reroll motion only (purchases have none, user 2026-11-14): the crate hops and rattles in whole pixels and
        // lands in a puff; each changing piece shrinks into a dust ring, then the new one pops with an overshoot.
        static readonly int[] RattleX = { 0, -2, 2, -2, 2, -1, 1, 0, 0, 0 }, RattleY = { 4, 6, 4, 2, 0, 0, 0, 0, 0, 0 };
        static readonly float[] Pop = { .4f, 1.18f, .95f, 1 };
        void Animate()
        {
            float now = Time.unscaledTime;
            foreach (var spot in Spots)
            {
                var t = spot.icon.transform; float px = 0, py = PieceBase, scale = 1;
                if (spot.Reroll && spot.rattleAt >= 0)
                {
                    int k = Mathf.FloorToInt((now - spot.rattleAt) / .04f);
                    if (k >= 0 && k < RattleX.Length) { px = RattleX[k] / 32f; py += RattleY[k] / 32f; }
                    if (k >= 4 && (spot.puffed & 1) == 0 && (spot.puffed |= 1) != 0)
                        foreach (float side in new[] { -1f, 1f }) AddPuff(spot.Ground + new Vector2(side * 22 / 32f, PieceBase + 3 / 32f), .16f, 0, 6 / 32f, 1, 3, 0, DustColor);
                    if (k >= RattleX.Length) spot.rattleAt = -1;
                }
                else if (!spot.Reroll && spot.changeAt >= 0)
                {
                    float u = now - spot.changeAt, h = spot.icon.sprite ? spot.icon.sprite.bounds.size.y : 1;
                    if (u < 0) { }
                    else if (u < .16f)
                    {
                        scale = 1 - u / .16f; py += h * (1 - scale) / 2;
                        if ((spot.puffed & 2) == 0) { spot.puffed |= 2; AddPuff(spot.Center, .16f, 8 / 32f, 18 / 32f, 8, 3, 0, DustColor); }
                    }
                    else if (u < .32f)
                    {
                        int k = Mathf.Min(3, Mathf.FloorToInt((u - .16f) / .04f)); scale = Pop[k];
                        if ((spot.puffed & 4) == 0) { spot.puffed |= 4; AddPuff(spot.Center, .16f, 18 / 32f, 24 / 32f, 8, 2, .4f, Color.white); }
                    }
                    else spot.changeAt = -1;
                }
                t.localPosition = new Vector3(px, Mathf.Round(py * 32) / 32); t.localScale = new Vector3(scale, scale, 1);
            }
            DrawPuffs(now);
        }

        Color DustColor => new Color32(204, 199, 190, 255);
        void AddPuff(Vector2 at, float duration, float from, float to, int count, float size, float turn, Color color) =>
            puffs.Add(new Puff { at = at, start = Time.unscaledTime, duration = duration, from = from, to = to, count = count, size = size, turn = turn, color = color });

        void DrawPuffs(float now)
        {
            puffs.RemoveAll(p => now - p.start > p.duration);
            int used = 0;
            foreach (var p in puffs)
            {
                float u = Mathf.Clamp01((now - p.start) / p.duration), r = Mathf.Lerp(p.from, p.to, u);
                for (int i = 0; i < p.count; i++)
                {
                    if (used == dust.Count) dust.Add(DustDot());
                    var d = dust[used++]; float a = p.turn + u + i * Mathf.PI * 2 / p.count;
                    var at = p.at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    d.transform.position = new Vector3(Mathf.Round(at.x * 32) / 32, Mathf.Round(at.y * 32) / 32);
                    d.transform.localScale = Vector3.one * p.size; d.color = new Color(p.color.r, p.color.g, p.color.b, 1 - u * .6f); d.enabled = true;
                }
            }
            for (int i = used; i < dust.Count; i++) dust[i].enabled = false;
        }

        SpriteRenderer DustDot()
        {
            if (!dustRoot) { dustRoot = new GameObject("Shelf dust").transform; dustRoot.SetParent(transform, false); }
            var r = Renderer("Dust dot", dustRoot, arena.Player.visual.sortingOrder + 2);
            r.sprite = Cached("dust", () => { var t = Texture(1, 1); t.SetPixel(0, 0, Color.white); t.Apply(); return t; }, 32, Vector2.one * .5f);
            return r;
        }

        // Plates in the shop art's own colours: chapter 1 cream with its grey outline, chapter 2 steel with the ink line.
        // One step shaved off each corner, a lit top row and a shaded bottom row.
        Sprite Plate(int width) => Cached("plate:" + width, () =>
        {
            Color32 face, top, shade, edge;
            if (chapter == 0) { face = new Color32(254, 247, 238, 255); top = new Color32(255, 252, 247, 255); shade = new Color32(204, 199, 190, 255); edge = new Color32(111, 110, 105, 255); }
            else { face = new Color32(74, 92, 90, 255); top = new Color32(143, 168, 154, 255); shade = new Color32(44, 58, 58, 255); edge = new Color32(6, 9, 11, 255); }
            int w = width, h = PlateHeight; var t = Texture(w, h); var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int row = h - 1 - y;                                   // rows counted from the top, as drawn in the mock-up
                    var c = x == 0 || x == w - 1 || row == 0 || row == h - 1 ? edge : row == 1 && x >= 2 && x <= w - 3 ? top : row == h - 2 && x >= 2 && x <= w - 3 ? shade : face;
                    bool cx = x == 0 || x == w - 1, cy = row == 0 || row == h - 1, nx = x == 1 || x == w - 2, ny = row == 1 || row == h - 2;
                    if ((cx && cy) || (cx && ny) || (nx && cy)) c = new Color32(0, 0, 0, 0);
                    else if (nx && ny) c = edge;
                    px[y * w + x] = c;
                }
            t.SetPixels32(px); t.Apply(); return t;
        }, 32, new Vector2(.5f, 0));

        Sprite Crate() => Cached(art + "-reroll-crate", () => Resources.Load<Texture2D>("MaintenanceShops/" + art + "-reroll-crate"), 32, new Vector2(.5f, 0));
        // The approved bag art: the ink's own bottle with its effect and condition marks, the weapon's ui-assets-v2 drawing.
        Sprite InkSprite(string id, string color) => Cached("ink:" + id, () => Fit(ApprovedTheme.Ink(id), InkPixels) ?? Resources.Load<Sprite>("LootPickups/ink-" + color)?.texture, 32, new Vector2(.5f, 0));
        Sprite WeaponSprite(string form) => Cached("weapon:" + form, () => Fit(ApprovedTheme.Texture("ui-assets-v2/" + form), WeaponPixels) ?? LegacyWeapon(form), 32, new Vector2(.5f, 0));
        Texture2D LegacyWeapon(string form)
        {
            if (weaponIcons == null)
            {
                weaponIcons = new Dictionary<string, string>();
                var json = Resources.Load<TextAsset>("ApprovedCombatHUD/bindings");
                if (json) foreach (var w in JsonUtility.FromJson<Bindings>(json.text).weapons ?? Array.Empty<BindingWeapon>()) weaponIcons[w.id] = w.icon;
            }
            weaponIcons.TryGetValue(form, out var icon);
            return Fit(Resources.Load<Sprite>("ApprovedCombatHUD/sprites/" + (icon ?? "weapon-sword"))?.texture, WeaponPixels);
        }
        [Serializable] sealed class BindingWeapon { public string id, icon; }
        [Serializable] sealed class Bindings { public BindingWeapon[] weapons; }

        // Box-filtered down to the given long side in whole pixels (the mock-up's resize), so the point-filtered world
        // drawing keeps clean dots instead of skipped ones.
        Texture2D Fit(Texture2D source, int size)
        {
            if (!source) return null;
            Color[] src;
            try { src = source.GetPixels(); } catch (UnityException) { return null; }
            float scale = (float)size / Mathf.Max(source.width, source.height);
            int w = Mathf.Max(1, Mathf.RoundToInt(source.width * scale)), h = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
            var t = Texture(w, h); var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int x0 = x * source.width / w, x1 = Mathf.Max(x0 + 1, (x + 1) * source.width / w), y0 = y * source.height / h, y1 = Mathf.Max(y0 + 1, (y + 1) * source.height / h);
                    float r = 0, g = 0, b = 0, a = 0; int n = 0;
                    for (int sy = y0; sy < y1; sy++)
                        for (int sx = x0; sx < x1; sx++) { var c = src[sy * source.width + sx]; r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a; n++; }
                    px[y * w + x] = a > 0 ? new Color(r / a, g / a, b / a, a / n) : Color.clear;
                }
            t.SetPixels(px); t.Apply(); return t;
        }

        Texture2D Texture(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            owned.Add(t); return t;
        }

        Sprite Cached(string key, Func<Texture2D> load, float pixelsPerUnit, Vector2 pivot)
        {
            if (sprites.TryGetValue(key, out var cached)) return cached;
            var texture = load(); Sprite sprite = null;
            if (texture)
            {
                texture.filterMode = FilterMode.Point;
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprite.name = key; owned.Add(sprite);
            }
            sprites[key] = sprite; return sprite;
        }

        // Forgets the laid-out rows so the next frame lays them out again (editor review after it unlocks rerolls).
        public void RebuildForReview()
        {
            foreach (var s in shelves) { if (s.root) Destroy(s.root.gameObject); s.spots.Clear(); s.built = false; }
            Nearest = null;
        }

        void OnDestroy() { foreach (var o in owned) if (o) Destroy(o); }
    }
}
