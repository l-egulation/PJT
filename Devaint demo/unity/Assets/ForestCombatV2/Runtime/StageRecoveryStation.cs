using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using Deviant.Control.Contracts;
using Deviant.Input.Contracts;
using Deviant.Run;
using UnityEngine;

namespace ForestCombatV2
{
    public readonly struct StageRecoveryRoom
    {
        public readonly string Id;
        public readonly Rect Bounds;
        public StageRecoveryRoom(string id, Rect bounds) { Id = id; Bounds = bounds; }
    }

    // Shared by both chapters; only the room list and the admission to a live run differ.
    public sealed class StageRecoveryStation : MonoBehaviour
    {
        int chapter = 1;
        public int Chapter => chapter;
        // Chapter 1 uses the 96x112 pixel pump (PPU32, 3u x 3.5u); chapter 2 keeps its authored tank.
        public float ArtHeight => chapter == 2 ? 3.3f : 3.5f;
        public float ArtWidth => chapter == 2 ? 3.8f : 3f;
        CombatArena arena;
        RunEconomy economy;
        Func<bool> ready, available;
        Func<IReadOnlyList<StageRecoveryRoom>> rooms;
        int seed;
        bool placementAttempted;
        StageRecovery reservoir;
        StageRecoveryHold hold;
        Transform pump;
        SpriteRenderer artwork;
        public StageRecoveryPrompt Prompt { get; private set; }
        Chapter1RoomClearHealFx healFx;

        public bool Placed { get; private set; }
        public Vector2 Position { get; private set; }
        public string RoomId { get; private set; }
        public int Remaining => reservoir?.Remaining ?? 0;
        public int Capacity => reservoir?.Capacity ?? 0;
        public int Price => reservoir?.Price ?? 0;
        bool CanAfford => economy != null && economy.Gold >= Price;
        public bool Holding => hold?.Holding ?? false;
        public float HoldProgress => arena && hold != null ? hold.Progress(arena.Now) : 0;
        public int HealPercent => Mathf.RoundToInt(reservoir.HealthFraction * 100);
        public bool ShowPrompt => isActiveAndEnabled && Placed && PlayerNear && ready() && available() &&
            arena.Player && !arena.Player.Dead && !arena.Controls.Current.SimulationPaused &&
            !arena.Controls.Current.Blocks(InputChannels.Interaction) && Time.timeScale > 0;
        public string PromptStatus => Remaining == 0 ? "충전 소진" :
            arena.Player.health >= arena.Player.maxHealth ? "체력이 가득 찼습니다" :
            !CanAfford ? "골드 부족" : "";
        public bool PlayerNear => Placed && arena && arena.Player &&
            Vector2.Distance(arena.Player.Position, new Vector2(
                Mathf.Clamp(arena.Player.Position.x, Position.x - ArtWidth / 2, Position.x + ArtWidth / 2),
                Position.y)) <= reservoir.Reach &&
            !Physics2D.LinecastAll(arena.Player.Position + Vector2.up, Position + Vector2.up)
                .Any(h => h.collider.GetComponent<ArenaSurface>() is ArenaSurface s && !s.oneWay);

        public void Initialize(ForestCombatWorld world, Chapter1RunBinding binding)
        {
            Initialize(world.arena, binding.Run.Economy, binding.Run.Seed,
                () => world.Data.rooms.Where(r => r.kind == "combat" && r.mainPath).OrderBy(r => r.x)
                    .Select(r => new StageRecoveryRoom(r.id.ToString(), new Rect(r.x, -(r.y + r.h), r.w, r.h))).ToArray(),
                () => world && world.Ready && !world.dummyTrainingOnly,
                () => binding && !binding.Run.Ended && !binding.ShowingResult,
                extraUses: binding.Run.Perks.Settings.ExtraRecoveryUses);
        }

        public void Initialize(CombatArena owner, RunEconomy wallet, int stageSeed, Func<IReadOnlyList<StageRecoveryRoom>> route,
            Func<bool> isReady, Func<bool> isAvailable, int stage = 1, int extraUses = 0)
        {
            chapter = stage;
            arena = owner; economy = wallet; seed = stageSeed; rooms = route; ready = isReady; available = isAvailable;
            var asset = Resources.Load<TextAsset>("StageRecovery");
            if (!asset) throw new InvalidOperationException("StageRecovery tuning is missing; run export_stage_recovery.py.");
            reservoir = new StageRecovery(StageRecovery.WithExtraUses(JsonUtility.FromJson<StageRecoveryData>(asset.text), extraUses));
            hold = new StageRecoveryHold(reservoir.HoldSeconds);
            arena.ResetCompleted += ResetReservoir;
        }

        public bool Prepare()
        {
            if (Placed) return true;
            if (ready == null || !ready()) return false;
            if (placementAttempted) return false;
            placementAttempted = true;
            var route = rooms();
            if (route.Count == 0) return false;
            var random = new System.Random(unchecked(seed * 7919 + 0x524543));
            // Prefer the middle/later route. An early start-room heal is usually wasted.
            int first = route.Count / 3, last = Mathf.Max(first + 1, Mathf.CeilToInt(route.Count * .8f));
            var preferred = Enumerable.Range(first, last - first).ToList();
            Shuffle(preferred, random);
            var fallback = Enumerable.Range(0, route.Count).Where(i => i < first || i >= last).ToList();
            Shuffle(fallback, random); preferred.AddRange(fallback);
            Physics2D.SyncTransforms();
            foreach (int index in preferred)
            {
                var room = route[index];
                var positions = FloorPositions(arena, room.Bounds, ArtWidth, ArtHeight);
                if (positions.Count == 0) continue;
                Position = positions[random.Next(positions.Count)]; RoomId = room.Id;
                BuildPump(); Placed = true;
                return true;
            }
            Debug.LogWarning("No safe solid floor for the stage recovery station.", this);
            // No unsafe fallback in midair or inside a wall.
            return false;
        }

        static void Shuffle(List<int> values, System.Random random)
        {
            for (int i = values.Count - 1; i > 0; i--)
            { int j = random.Next(i + 1); (values[i], values[j]) = (values[j], values[i]); }
        }

        public static List<Vector2> FloorPositions(CombatArena owner, Rect room, float width = 2.1f, float height = 2.5f)
        {
            var found = new List<Vector2>();
            // Keep clear of doorways. Use solid, level floor with space for the pump and a standing player.
            for (float x = Mathf.Ceil(room.xMin + 3) + .5f; x <= room.xMax - 3; x += 1)
            {
                var hits = Physics2D.RaycastAll(new Vector2(x, room.yMax - .125f), Vector2.down, room.height + .125f);
                foreach (var hit in hits)
                {
                    var surface = hit.collider.GetComponent<ArenaSurface>();
                    if (!surface || surface.oneWay || hit.normal.y < .9f || hit.point.y < room.yMin - .01f) continue;
                    var at = new Vector2(x, hit.point.y);
                    if (!room.Contains(at + Vector2.up * (height + .1f))) continue;
                    if (!SolidSupport(at + Vector2.left * (width / 2)) || !SolidSupport(at + Vector2.right * (width / 2))) continue;
                    bool blocked = Physics2D.OverlapBoxAll(at + Vector2.up * (height / 2 + .08f), new Vector2(width + .1f, height), 0)
                        .Any(c => c.GetComponent<ArenaSurface>() || c.GetComponent<ArenaPit>());
                    if (!blocked && !found.Any(p => (p - at).sqrMagnitude < .01f)) found.Add(at);
                }
            }
            // Physics enumeration is not an ordering contract; a seed always indexes the same sorted candidates.
            found.Sort((a, b) => { int x = a.x.CompareTo(b.x); return x != 0 ? x : a.y.CompareTo(b.y); });
            return found;
            bool SolidSupport(Vector2 at) => Physics2D.RaycastAll(at + Vector2.up * .15f, Vector2.down, .3f)
                .Any(h => h.collider.GetComponent<ArenaSurface>() is ArenaSurface s && !s.oneWay &&
                    h.normal.y > .9f && Mathf.Abs(h.point.y - at.y) <= .05f);
        }

        bool InteractionAllowed
        {
            get
            {
                var p = arena ? arena.Player : null;
                return isActiveAndEnabled && Placed && ready() && available() && p && !p.Dead && !p.InputBlocked &&
                    !arena.Controls.Current.SimulationPaused && !arena.Controls.Current.Blocks(InputChannels.Interaction) &&
                    Time.timeScale > 0 && p.grounded && !p.currentCast && !p.Dashing && !p.CheckpointBusy &&
                    arena.Now >= p.knockUntil && arena.Now >= p.moveLockUntil && arena.Now >= p.stunUntil &&
                    arena.Now >= p.rootUntil && arena.Now >= p.staggerUntil;
            }
        }

        public bool TryBeginHold()
        {
            if (!InteractionAllowed || !PlayerNear || !CanAfford || !reservoir.Ready(arena.Now) || arena.Player.health >= arena.Player.maxHealth) return false;
            return hold.Begin(arena.Now, arena.Player.ReceivedHitCount);
        }

        public bool AdvanceHold(bool held)
        {
            if (!Holding) return false;
            bool allowed = InteractionAllowed && PlayerNear && CanAfford && reservoir.Ready(arena.Now) && arena.Player.health < arena.Player.maxHealth;
            if (!hold.Advance(arena.Now, held, allowed, arena.Player.ReceivedHitCount)) return false;
            return CompleteRecovery();
        }

        bool CompleteRecovery()
        {
            if (!InteractionAllowed || !PlayerNear || !CanAfford || !reservoir.TryUse(arena.Player.health, arena.Player.maxHealth, arena.Now, out float restored, economy.TrySpend)) return false;
            arena.Player.Heal(restored);
            if (Prompt) Prompt.ShowHeal(restored);
            if (!healFx) healFx = GetComponent<Chapter1RoomClearHealFx>();
            if (!healFx)
            {
                healFx = gameObject.AddComponent<Chapter1RoomClearHealFx>();
                healFx.Initialize(null, GetComponent<Chapter1InkPartFx>(), arena);
            }
            healFx.Play(restored);
            RefreshPump();
            return true;
        }

        void Update()
        {
            if (!Prepare()) return;
            bool offer = PlayerNear && InteractionAllowed && CanAfford && reservoir.Ready(arena.Now) && arena.Player.health < arena.Player.maxHealth;
            if (!arena.ManualSimulationActive && !arena.Player.UseTestInput)
            {
                if (Holding) AdvanceHold(PlayerControls.Held(arena.Player, GameAction.Interact));
                else if (offer) PlayerControls.TryPerform(arena.Player, GameAction.Interact, TryBeginHold);
            }
        }

        void ResetReservoir()
        {
            reservoir.Reset(); hold.Cancel(); if (Prompt) Prompt.ClearHeal();
            if (Placed) RefreshPump();
        }

        void BuildPump()
        {
            var go = new GameObject("Ink recovery pump"); go.transform.SetParent(transform, false);
            pump = go.transform; pump.position = Position;
            artwork = go.AddComponent<SpriteRenderer>();
            artwork.sprite = Resources.Load<Sprite>("StageRecoveryArt/stage" + chapter);
            if (!artwork.sprite) throw new InvalidOperationException("Missing authored stage recovery sprite: " + chapter);
            artwork.sharedMaterial = arena.spriteMaterial; artwork.sortingOrder = 9;
            var ui = new GameObject("Ink recovery interaction", typeof(RectTransform));
            ui.transform.SetParent(transform, false);
            Prompt = ui.AddComponent<StageRecoveryPrompt>(); Prompt.Initialize(this, arena);
            RefreshPump();
        }

        void RefreshPump()
        {
            if (artwork) artwork.color = Remaining > 0 ? Color.white : new Color(.65f, .65f, .65f, 1);
            if (Prompt) Prompt.Refresh();
        }

        void OnDisable() { hold?.Cancel(); if (Prompt) Prompt.Hide(); }
        void OnDestroy()
        {
            if (arena) arena.ResetCompleted -= ResetReservoir;
            if (pump) Destroy(pump.gameObject);
            if (Prompt) Destroy(Prompt.gameObject);
        }
    }
}
