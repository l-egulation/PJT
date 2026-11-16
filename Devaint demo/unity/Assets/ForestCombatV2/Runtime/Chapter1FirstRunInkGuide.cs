using System;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Run;
using UnityEngine;

namespace ForestCombatV2
{
    // First-run ink tutorial (docs/design/story/tutorials/first-run-guide.md, guide-ink · guide-bag). Only on the profile's first
    // run (RunsStarted 1 at departure) and only until guide-bag is recorded: a few seconds after the departure palettes
    // are bound, one weak normal monster drops in front of the player and comes at it. Killing it always throws exactly
    // one lowest-grade colour ink that fits a slot of the equipped palettes; the ink blinks with an outline until it is
    // picked up. The play GUI reads this component for the hero's line, the bag prompt and the slot highlight. Normal
    // rewards (Chapter1Rewards, WeaponGrowth) are untouched: the guide monster pays gold through the normal table but no
    // ink roll, and the guide ink stays outside the early colour count.
    public sealed class Chapter1FirstRunInkGuide : MonoBehaviour
    {
        public const string GuideInkStory = "guide-ink", GuideBagStory = "guide-bag";
        public const string ReactionLine = "어? 이건 뭐지?";
        // Arena seconds after both palettes are bound: the 3.5 s departure palette card has gone by then.
        public const float SpawnDelaySeconds = 5f;
        // The monster appears this far ahead of the player (inside its sight), never closer than MinimumApproach.
        public const float ApproachDistance = 7f, MinimumApproach = 3f;
        // The bag prompt waits until no living monster is this close (the bag does not pause the world).
        public const float BagPromptClearance = 12f;
        public const float ReactionSeconds = 2.4f, OutlineBlinkSeconds = .8f;
        public enum Stage { Inactive, Waiting, Approaching, Dropped, InBag, Done }

        ForestCombatWorld world;
        Chapter1RunBinding binding;
        WeaponGrowth growth;
        System.Random random;
        bool decided;
        float spawnAt = -1, reactionAt = -1;
        readonly HashSet<Guid> mountedBefore = new HashSet<Guid>();
        readonly List<SpriteRenderer> outline = new List<SpriteRenderer>();
        SpriteRenderer pickupBody;
        Material outlineMaterial;

        public Stage Current { get; private set; } = Stage.Inactive;
        public FieldMouseEnemy Monster { get; private set; }
        public Chapter1RewardPickup Pickup { get; private set; }
        public GrowthPart GuidePart { get; private set; }
        public Guid GuideInk { get; private set; }
        public bool Completed { get; private set; }
        // Why the guide stopped without completing (empty when it completed or never started).
        public string EndReason { get; private set; } = "";
        public bool Active => Current != Stage.Inactive && Current != Stage.Done;

        // First-run notebooks (user decision 2026-11-09). A GUI that can show them sets NotebookWanted. The combat
        // notebook (attack and skill keys) comes as soon as both palettes are bound, before the monster, which then
        // spawns AfterNotebookSeconds after it closes. Picking up the guide ink starts the hero's line; once it has run the
        // palette notebook comes, and the bag prompt waits for it. Without a GUI (headless checks) neither shows and the
        // old timer, landing reaction and prompt apply.
        public enum Notebook { None, Combat, Palette }
        // Short, so the guide monster comes in the start room right after the notebook (user 2026-11-09: it came so late
        // the player met room 1's monsters first).
        public const float AfterNotebookSeconds = .4f;
        public bool NotebookWanted { get; set; }
        public Notebook OpenNotebook { get; private set; }
        public bool NotebookOpen => OpenNotebook != Notebook.None;
        public bool CombatNotebookFinished { get; private set; }
        public bool PaletteNotebookFinished { get; private set; }
        public Notebook NotebookDue
        {
            get
            {
                if (!NotebookWanted || NotebookOpen) return Notebook.None;
                if (Current == Stage.Waiting && waitingReady && !CombatNotebookFinished) return Notebook.Combat;
                if (Current == Stage.InBag && !PaletteNotebookFinished && world && world.arena.Player && !world.arena.Player.Dead) return Notebook.Palette;
                return Notebook.None;
            }
        }
        public void MarkNotebookOpen(Notebook kind) { if (kind != Notebook.None && NotebookDue == kind) OpenNotebook = kind; }
        // Finishes the open notebook (or, with none open, the one that is due).
        public void FinishNotebook()
        {
            var kind = NotebookOpen ? OpenNotebook : NotebookDue;
            OpenNotebook = Notebook.None;
            if (kind == Notebook.Combat)
            {
                CombatNotebookFinished = true;
                if (world && world.arena) spawnAt = world.arena.Now + AfterNotebookSeconds;
            }
            else if (kind == Notebook.Palette) PaletteNotebookFinished = true;
        }
        bool waitingReady;
        public int OutlinePieces => outline.Count;
        public bool OutlineVisible => outline.Count > 0 && outline[0] && outline[0].enabled;

        // The profile's first run only (user decision 2026-11-06): the departure counted this run, so RunsStarted is 1.
        // From the second run on it never appears, whether or not the first run finished it. A profile that already
        // recorded guide-bag (a reset record farm clears it) and runs not planned by this profile (training, direct
        // editor loads, test services) never see it.
        public static bool Eligible(bool profileRun, string planProfile, ProfileSnapshot profile) =>
            profileRun && profile != null && !string.IsNullOrEmpty(planProfile) && planProfile == profile.ProfileId &&
            profile.RunsStarted <= 1 && !profile.HasSeen(GuideBagStory);

        // The guide ink: the lowest grade on offer, in a colour of the equipped palettes (colourless wildcard slots
        // excluded; with no such colour any colour fits a colourless slot). Colour first with equal chance, then a part
        // of that colour's lowest offered grade. Null when nothing is on offer.
        public static GrowthPart ChooseGuidePart(GrowthData data, IList<string> paletteColors, Func<GrowthPart, bool> offered, System.Random random)
        {
            if (data?.parts == null || random == null) return null;
            bool Usable(GrowthPart p) => p != null && p.color != "colorless" && (offered == null || offered(p));
            var colors = new List<string>();
            foreach (var part in data.parts) if (Usable(part) && !colors.Contains(part.color)) colors.Add(part.color);
            if (paletteColors != null)
            {
                var fitting = colors.FindAll(c => paletteColors.Contains(c));
                if (fitting.Count > 0) colors = fitting;
            }
            if (colors.Count == 0) return null;
            string color = colors[random.Next(colors.Count)];
            int grade = int.MaxValue;
            foreach (var part in data.parts) if (Usable(part) && part.color == color) grade = Math.Min(grade, part.grade);
            var pool = new List<GrowthPart>();
            foreach (var part in data.parts) if (Usable(part) && part.color == color && part.grade == grade) pool.Add(part);
            return pool[random.Next(pool.Count)];
        }

        public void Initialize(ForestCombatWorld owner, Chapter1RunBinding runBinding)
        {
            world = owner; binding = runBinding;
            binding.EnemyDefeated += Defeated;
        }

        void OnDestroy()
        {
            if (binding) binding.EnemyDefeated -= Defeated;
            Unbind();
            if (outlineMaterial) Destroy(outlineMaterial);
        }

        void Unbind()
        {
            if (growth == null) return;
            growth.Acquired -= Acquired; growth.Changed -= GrowthChanged;
        }

        void Update()
        {
            if (world && world.arena && world.arena.Sketch != null) { if (Active) Stop("new run ink catalog"); return; }
            if (!world || !world.Ready || !binding || binding.Run == null) return;
            if (!decided) Decide();
            if (!Active) return;
            var run = binding.Run; var arena = world.arena; var player = arena.Player;
            // A finished run (death, clear) ends the guide here; nothing is recorded, and the next run is no longer the first.
            if (run.Ended) { Stop("run ended"); return; }
            if (!player) return;
            switch (Current)
            {
                case Stage.Waiting: TickWaiting(arena, player); break;
                case Stage.Approaching:
                    // Removed without a kill (a development reset or cleanup): nothing drops, the guide ends for this run.
                    if (!Monster) Stop("monster removed");
                    break;
                case Stage.Dropped:
                    if (!Pickup) { Stop("ink removed"); break; }
                    // With the notebooks the hero reacts when the ink is picked up (Acquired), not when it lands.
                    if (!NotebookWanted && reactionAt < 0 && Pickup.Ready && !player.Dead) reactionAt = arena.Now;
                    break;
            }
        }

        void LateUpdate() => RefreshOutline();

        void Decide()
        {
            decided = true;
            var run = binding.Run;
            if (world.arena.SketchEquipment || run.Growth == null || world.dummyTrainingOnly || !world.proceduralRooms) return;
            if (!Eligible(binding.ProfileRun, run.Plan?.ProfileId, Chapter1ProfileHost.Service.Snapshot)) return;
            growth = run.Growth;
            growth.Acquired += Acquired; growth.Changed += GrowthChanged;
            random = new System.Random(unchecked(run.Seed * 7177 + 59));
            Current = Stage.Waiting;
        }

        void Stop(string reason)
        {
            if (!Active) return;
            Current = Stage.Done; EndReason = reason; OpenNotebook = Notebook.None;
            Unbind();
            ClearOutline();
        }

        bool PalettesBound(CombatPlayer player)
        {
            for (int slot = 0; slot < 2; slot++)
                if (!player.IsWeaponSlotReady(slot) || growth.Equipped(player.weapons[slot].InstanceId) == null) return false;
            return true;
        }

        void TickWaiting(CombatArena arena, CombatPlayer player)
        {
            waitingReady = !player.Dead && PalettesBound(player);
            if (!waitingReady) return;
            // The combat notebook replaces the fixed delay: the monster waits for it to close, then AfterNotebookSeconds.
            if (NotebookWanted && !CombatNotebookFinished) return;
            if (spawnAt < 0) { spawnAt = arena.Now + SpawnDelaySeconds; return; }
            if (arena.Now < spawnAt || arena.Controls.Current.SimulationPaused) return;
            TrySpawn(arena, player);
        }

        // The weakest plain walking normal monster of this stage (the key carrier's rule: no fixed or self-moving species).
        FieldMouseEnemy WeakestNormal(CombatArena arena)
        {
            var keys = GetComponent<Chapter1KeyChest>(); var carrier = keys ? keys.Carrier : null;
            FieldMouseEnemy best = null;
            foreach (var enemy in arena.enemies)
                if (enemy is FieldMouseEnemy mob && mob != carrier && mob != Monster && Chapter1KeyChest.CanCarry(mob) && !mob.GetComponent<Chapter1NoKillReward>() &&
                    (!best || mob.maxHealth < best.maxHealth) && SpeciesOf(mob) != null) best = mob;
            return best;
        }

        bool Solid(int x, int y)
        {
            var d = world.Data;
            return x < 0 || y < 0 || x >= d.cols || y >= d.rows || d.ground[y * d.cols + x] != 0;
        }

        // How far the floor row runs open from x in one direction, up to want (1 tile = 1 unit; feet on row r stand at
        // y = -r and row r is the ground under them). Stops at a wall, a low ceiling or a gap.
        float OpenFloor(float x, int foot, int direction, float want)
        {
            float good = 0;
            for (float d = .5f; d <= want + .01f; d += .5f)
            {
                int column = Mathf.FloorToInt(x + direction * d);
                if (Solid(column, foot - 1) || Solid(column, foot - 2) || Solid(column, foot - 3) || !Solid(column, foot)) break;
                good = d;
            }
            return good;
        }

        void TrySpawn(CombatArena arena, CombatPlayer player)
        {
            var template = WeakestNormal(arena);
            if (!template) { Stop("no walking normal monster in this stage"); return; }
            var floor = arena.FindFloor(player.Position + Vector2.up * .5f, 16);
            if (!floor.HasValue) return; // In the air: try again next frame.
            int foot = Mathf.RoundToInt(-floor.Value);
            var definition = template.Definition;
            float want = Mathf.Clamp(definition != null ? definition.senseX * .7f : ApproachDistance, MinimumApproach, ApproachDistance);
            float x = float.NaN;
            // Ahead first (the route runs right), then behind.
            foreach (int direction in new[] { 1, -1 })
            {
                float open = OpenFloor(player.Position.x, foot, direction, want);
                if (open >= MinimumApproach) { x = player.Position.x + direction * open; break; }
            }
            if (float.IsNaN(x)) return; // No room on this floor: wait until the player stands somewhere open.
            string species = SpeciesOf(template);
            if (species == null) { Stop("monster species unknown"); return; }
            try { Monster = world.SpawnMonster(species, new Vector2(x, -foot + .003f), true, x < player.Position.x ? 1 : -1); }
            catch (Exception exception) { Debug.LogWarning("First-run ink guide could not spawn its monster: " + exception.Message, this); Stop("spawn failed"); return; }
            Monster.name = "첫 런 잉크 안내 · " + Monster.name;
            // Comes at the player wherever it stands, like the seal pages.
            Monster.distantIdleRange = Vector2.zero;
            // No fixed ink table and no random ink roll: the guide ink below is its only ink (gold and pigment stay normal).
            if (!Monster.GetComponent<Deviant.Inventory.Unity.InkDropSource>()) Monster.gameObject.AddComponent<Deviant.Inventory.Unity.InkDropSource>().tableKey = "";
            Monster.gameObject.AddComponent<Chapter1NoInkReward>();
            Current = Stage.Approaching;
        }

        // Species id of a stage monster: the placement it was created from (its spawn is the placement's feet).
        string SpeciesOf(TestEnemy mob)
        {
            foreach (var actor in world.Data.actors ?? Array.Empty<MonsterPlacement>())
                if (actor.kind >= 0 && actor.kind < world.Data.monsters.Length && (new Vector2(actor.x / 32, -actor.y / 32) - mob.spawn).sqrMagnitude < .01f)
                    return world.Data.monsters[actor.kind].id;
            return null;
        }

        // Colours on the player's equipped palettes right now (colourless wildcard slots excluded).
        public List<string> PaletteColors()
        {
            var colors = new List<string>();
            var player = world ? world.arena.Player : null;
            for (int slot = 0; growth != null && player && slot < player.weapons.Count; slot++)
            {
                if (!player.IsWeaponSlotReady(slot)) continue;
                var palette = growth.Equipped(player.weapons[slot].InstanceId);
                if (palette == null) continue;
                foreach (var cell in palette.Slots)
                    if (!string.IsNullOrEmpty(cell.Color) && cell.Color != "colorless" && !colors.Contains(cell.Color)) colors.Add(cell.Color);
            }
            return colors;
        }

        void Defeated(TestEnemy enemy)
        {
            if (Current != Stage.Approaching || !Monster || enemy != Monster) return;
            var run = binding.Run;
            if (run == null || run.Ended) return;
            var part = ChooseGuidePart(growth.Data, PaletteColors(), growth.Offered, random);
            if (part == null) { Stop("no ink on offer"); return; }
            GuidePart = part;
            var at = enemy.Position;
            var go = new GameObject("Reward · Colored Ink (first-run guide)");
            go.transform.SetParent(transform, false);
            Pickup = go.AddComponent<Chapter1RewardPickup>();
            Pickup.Initialize(world, run.Stash, Chapter1RewardKind.ColoredInk, null, world.arena.DropPoint(at, -.4f, 1f), at);
            Pickup.ConfigureGrowth(growth, part.id);
            BuildOutline();
            Current = Stage.Dropped;
        }

        void Acquired(GrowthInk ink)
        {
            if (Current != Stage.Dropped || ink == null || ink.Part != GuidePart) return;
            GuideInk = ink.Id;
            // With the notebooks there is no line: the palette notebook opens on the pickup (user 2026-11-09). Without them,
            // picked up in the frame it became collectable, the line still plays as the ink comes in.
            if (!NotebookWanted && reactionAt < 0) reactionAt = world.arena.Now;
            mountedBefore.Clear();
            foreach (var slot in MountedSlots()) mountedBefore.Add(slot.InstanceId);
            Current = Stage.InBag;
            ClearOutline();
            if (binding.ProfileRun) Chapter1ProfileHost.Service.MarkStorySeen(GuideInkStory);
        }

        IEnumerable<GrowthSlot> MountedSlots()
        {
            var player = world ? world.arena.Player : null;
            for (int w = 0; growth != null && player && w < player.weapons.Count; w++)
            {
                var palette = growth.Equipped(player.weapons[w].InstanceId);
                if (palette == null) continue;
                foreach (var slot in palette.Slots) if (slot.Part != null) yield return slot;
            }
        }

        // Any ink newly mounted from the bag ends the guide (the guide ink or another one: the player has learned it).
        // The guide ink destroyed or sold before that ends it without a record.
        void GrowthChanged()
        {
            if (Current != Stage.InBag) return;
            foreach (var slot in MountedSlots())
                if (!mountedBefore.Contains(slot.InstanceId)) { Complete(); return; }
            bool owned = false;
            foreach (var ink in growth.Inks) if (ink.Id == GuideInk) { owned = true; break; }
            if (!owned) Stop("guide ink left the bag");
        }

        void Complete()
        {
            Completed = true;
            if (binding.ProfileRun) Chapter1ProfileHost.Service.MarkStorySeen(GuideBagStory);
            Stop("");
        }

        // The hero's line runs for ReactionSeconds once the guide ink has landed.
        public bool ReactionShowing => reactionAt >= 0 && world && !NotebookWanted && world.arena.Now - reactionAt < ReactionSeconds;
        public float ReactionAge => reactionAt < 0 || !world ? -1 : world.arena.Now - reactionAt;

        // The bag prompt shows once the ink is in the bag, the palette notebook (if any) has closed and no living monster is close.
        public bool BagPromptReady
        {
            get
            {
                if (Current != Stage.InBag || !world) return false;
                if (NotebookWanted && !PaletteNotebookFinished) return false;
                var player = world.arena.Player;
                if (!player || player.Dead) return false;
                foreach (var enemy in world.arena.enemies)
                    if (enemy && !enemy.Dead && Vector2.Distance(enemy.Position, player.Position) < BagPromptClearance) return false;
                return true;
            }
        }

        // The palette slot to point at in the bag: an empty slot of the ink's colour, then an empty colourless slot, then
        // a filled slot of that colour (a free replacement). Weapon 1 first.
        public bool TargetSlot(out int weapon, out int slot)
        {
            weapon = slot = -1;
            var player = world ? world.arena.Player : null;
            var part = GuidePart;
            if (part == null || growth == null || !player) return false;
            for (int pass = 0; pass < 3; pass++)
                for (int w = 0; w < Mathf.Min(2, player.weapons.Count); w++)
                {
                    if (!player.IsWeaponSlotReady(w)) continue;
                    var palette = growth.Equipped(player.weapons[w].InstanceId);
                    if (palette == null) continue;
                    for (int i = 0; i < palette.Slots.Count; i++)
                    {
                        var cell = palette.Slots[i];
                        bool fits = pass == 0 ? cell.Part == null && cell.Color == part.color
                            : pass == 1 ? cell.Part == null && cell.Color == "colorless"
                            : cell.Color == part.color;
                        if (fits) { weapon = w; slot = i; return true; }
                    }
                }
            return false;
        }

        // guide-ink: a blinking light outline around the ink on the floor until it is picked up (the key carrier's
        // solid silhouette shader, one sprite pixel out in eight directions).
        static readonly Color OutlineColor = new Color32(255, 244, 190, 255);
        void BuildOutline()
        {
            pickupBody = Pickup ? Pickup.GetComponent<SpriteRenderer>() : null;
            var shader = Resources.Load<Shader>("Chapter1GimmickShaders/KeyCarrierLight");
            if (!pickupBody || !shader) return;
            if (!outlineMaterial) { outlineMaterial = new Material(shader) { name = "First-run ink outline" }; outlineMaterial.SetFloat("_Solid", 1); }
            for (int i = 0; i < 8; i++)
            {
                var piece = new GameObject("First-run ink outline").AddComponent<SpriteRenderer>();
                piece.transform.SetParent(Pickup.transform, false);
                piece.sharedMaterial = outlineMaterial; piece.sortingLayerID = pickupBody.sortingLayerID;
                outline.Add(piece);
            }
            RefreshOutline();
        }

        void ClearOutline()
        {
            foreach (var piece in outline) if (piece) Destroy(piece.gameObject);
            outline.Clear();
        }

        void RefreshOutline()
        {
            if (outline.Count == 0) return;
            bool shown = Current == Stage.Dropped && Pickup && !Pickup.Collected && pickupBody && pickupBody.sprite;
            float pixel = shown ? 1f / Mathf.Max(1, pickupBody.sprite.pixelsPerUnit) : 0;
            float alpha = .35f + .65f * (.5f + .5f * Mathf.Cos(Time.time * Mathf.PI * 2 / OutlineBlinkSeconds));
            for (int i = 0; i < outline.Count; i++)
            {
                var piece = outline[i];
                if (!piece) continue;
                piece.enabled = shown;
                if (!shown) continue;
                float angle = i * Mathf.PI / 4;
                piece.sprite = pickupBody.sprite; piece.flipX = pickupBody.flipX; piece.flipY = pickupBody.flipY;
                piece.transform.localPosition = new Vector3(Mathf.Round(Mathf.Cos(angle)), Mathf.Round(Mathf.Sin(angle)), 0) * pixel;
                piece.sortingOrder = pickupBody.sortingOrder - 1;
                var tint = OutlineColor; tint.a = alpha; piece.color = tint;
            }
        }
    }
}
