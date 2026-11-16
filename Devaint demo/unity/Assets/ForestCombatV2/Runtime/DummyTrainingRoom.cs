using System;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Control.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ForestCombatV2
{
    public sealed partial class DummyTrainingRoom : MonoBehaviour
    {
        public const float TargetHealth = 1000, PlatformHeight = 4, PlatformWidth = 5;
        readonly List<TrainingDummy> targets = new List<TrainingDummy>();
        public IReadOnlyList<TrainingDummy> Targets => targets;
        public readonly List<BoxCollider2D> Platforms = new List<BoxCollider2D>();
        ForestCombatWorld world;
        CombatOutcomeCursor cursor;
        readonly CombatAcceptedHit[] hits = new CombatAcceptedHit[128];
        bool bound;
        IControlScope scope;
        public bool IsOpen => scope != null;
        string message = "F1 장비 커스텀 · F5 가까운 허수아비 회복";
        public void Initialize(ForestCombatWorld owner)
        {
            world = owner;
            var room = Array.Find(world.Data.rooms, r => r.kind == "boss");
            if (room == null) throw new InvalidOperationException("Training requires a boss test room.");
            float floor = -(room.y + 15);
            world.arena.showDebugUI = false; world.arena.showPanel = false;
            world.diagnostics.showDebugUI = true; world.diagnostics.showHud = false; world.diagnostics.showPanel = false;
            world.arena.ExternalTestPanel = true;
            world.FixedCameraCenter = new Vector2(room.x + 12, floor + 5);
            world.RefreshCamera(0, true);
            Platform(room.x + 3, floor); Platform(room.x + 21, floor);
            AddTarget("발판 왼쪽", new Vector2(room.x + 3, floor + PlatformHeight));
            AddTarget("발판 오른쪽", new Vector2(room.x + 21, floor + PlatformHeight));
            AddTarget("바닥 단일", new Vector2(room.x + 4, floor));
            for (int i = 0; i < 3; i++) AddTarget("바닥 " + (i + 1), new Vector2(room.x + 16 + i, floor));
            Physics2D.SyncTransforms();
            bound = world.arena.HitEvents.TryGetCursor(out cursor);
            world.arena.SimulationStepCompleted += Observe;
            world.arena.ResetCompleted += ResetMeters;
        }
        SpriteRenderer Shape(Transform parent, string label, Vector2 size, Vector2 at, Color color, bool circle = false)
        {
            var go = new GameObject(label); go.transform.SetParent(parent, false); go.transform.localPosition = at;
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = circle ? world.arena.CircleSprite : world.arena.TechnicalSprite;
            if(world.arena.spriteMaterial)renderer.sharedMaterial = world.arena.spriteMaterial;
            renderer.color = color; renderer.sortingOrder = 18;
            go.transform.localScale = new Vector3(size.x / renderer.sprite.bounds.size.x, size.y / renderer.sprite.bounds.size.y, 1);
            return renderer;
        }
        void Platform(float x, float floor)
        {
            var go = new GameObject("Training platform +4u"); go.transform.SetParent(transform, false);
            go.transform.position = new Vector2(x, floor + PlatformHeight - .12f);
            go.AddComponent<ArenaSurface>().oneWay = true;
            var c = go.AddComponent<BoxCollider2D>(); c.size = new Vector2(PlatformWidth, .24f); Platforms.Add(c);
            Shape(go.transform, "Wood ledge", c.size, Vector2.zero, new Color32(104, 83, 58, 255));
        }
        void AddTarget(string label, Vector2 at)
        {
            var go = new GameObject(label); go.transform.SetParent(transform, false);
            var d = go.AddComponent<TrainingDummy>();
            d.Configure(world.arena, at, new Vector2(.9f, 2), new Vector2(0, 1), false);
            d.bodyCollider.enabled = false; d.ConfigureHurtbox(new Vector2(.9f, 2), new Vector2(0, 1));
            d.spawn = at; d.maxHealth = TargetHealth; d.aggressive = false; d.isBoss = true;
            d.visual = Shape(go.transform, "Straw torso", new Vector2(.7f, .8f), new Vector2(0, 1), new Color32(181, 142, 70, 255));
            Shape(go.transform, "Head", new Vector2(.6f, .6f), new Vector2(0, 1.7f), new Color32(211, 180, 112, 255), true);
            Shape(go.transform, "Stake", new Vector2(.12f, 1.8f), new Vector2(0, .9f), new Color32(97, 72, 45, 255));
            Shape(go.transform, "Arms", new Vector2(1.5f, .18f), new Vector2(0, 1.2f), new Color32(136, 99, 55, 255));
            d.ResetEnemy(); d.visual.color = new Color32(181, 142, 70, 255);
            targets.Add(d); world.arena.enemies.Add(d);
        }
        void Observe(float dt)
        {
            var arena = world.arena;
            if (!bound) { bound = arena.HitEvents.TryGetCursor(out cursor); return; }
            while (arena.HitEvents.TryRead(cursor, hits, out var page))
            {
                cursor = page.Next;
                if (page.DroppedBefore > 0) { foreach (var target in targets) target.Meter.Clear(); message = "피해 기록 용량 초과 · 측정을 다시 시작합니다."; }
                for (int i = 0; i < page.Count; i++)
                    foreach (var d in targets)
                        if (d && d.ActorId == hits[i].Target.ActorId)
                            d.Meter.Add(Mathf.Max(0, hits[i].Before.Health - hits[i].After.Health), hits[i].Now);
                if (cursor.AfterEventSequence >= page.LatestEventSequence) break;
            }
        }
        void ResetMeters() { foreach(var d in targets)d.Meter.Clear(); bound = world.arena.HitEvents.TryGetCursor(out cursor); }
        public TrainingDummy Nearest()
        {
            TrainingDummy nearest = null; float distance = float.PositiveInfinity;
            foreach (var d in targets)
            {
                float next = (d.Position - world.arena.Player.Position).sqrMagnitude;
                if (next < distance) { nearest = d; distance = next; }
            }
            return nearest;
        }
        public void RestoreNearest()
        {
            if (IsOpen || !world || world.arena.Controls.Current.SimulationPaused || world.arena.ManualSimulationActive) return;
            Observe(0); // Drain before resetting only one meter; never lose another target's hits.
            var target = Nearest(); if (!target) return;
            target.Restore(); message = target.name + " · 체력 회복 / 피해·DPS 초기화";
        }
        public void SetOpen(bool open)
        {
            if (open == IsOpen) return;
            if (open)
            {
                var next = world.arena.Controls.OpenScope("dummy training equipment");
                try { next.BlockInput(InputChannels.Combat | InputChannels.Interaction | InputChannels.Navigation); next.PauseSimulation(); scope = next; world.arena.TestConfigurationOpen = true; }
                catch { next.Dispose(); throw; }
            }
            else { scope?.Dispose(); scope = null; if(world)world.arena.TestConfigurationOpen = false; }
        }
        void Update()
        {
            if (!world || !world.Ready || Keyboard.current == null) return;
            var k = Keyboard.current;
            if (k.f1Key.wasPressedThisFrame) SetOpen(!IsOpen);
            else if (k.f2Key.wasPressedThisFrame) ToggleHitboxes();
            else if (IsOpen && k.escapeKey.wasPressedThisFrame) SetOpen(false);
            else if (CombatKeyboardBindings.WasPressed(k, CombatKeyboardBindings.For(world.arena.Player).DevelopmentReset) && !world.arena.Player.InputBlocked) RestoreNearest();
        }
        public void ToggleHitboxes()
        {
            if(IsOpen || !world || world.arena.Controls.Current.SimulationPaused || world.arena.ManualSimulationActive)return;
            world.arena.showBoxes=!world.arena.showBoxes;
            message=world.arena.showBoxes?"히트박스 표시 켬 [F2]":"히트박스 표시 끔 [F2]";
        }
        void OnDisable() => SetOpen(false);
        void OnDestroy()
        {
            SetOpen(false);
            ReleaseUiTextures();
            if(world && world.arena) { world.arena.SimulationStepCompleted -= Observe; world.arena.ResetCompleted -= ResetMeters; world.arena.ExternalTestPanel = false; }
        }
    }
}
