using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Deviant.Combat.Contracts;
using Deviant.Integration;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    // 런 잉크가 실제로 발동한 순간·유지 동안 사용자 선택 이펙트(resources effects/ink-buffs, 2026-11-12)가 뜨는지 검사한다.
    public sealed class RunInkEffectVisualTests
    {
        CombatArena arena;
        CombatArena.ManualSimulation driver;
        CombatPlayer Player => arena.Player;
        TestEnemy Target => arena.enemies[0];
        SketchEquipment Equipment => arena.Sketch;
        float minimum, maximum;
        [UnitySetUp] public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("RunInkFxArena").AddComponent<CombatArena>();
            yield return new EnterPlayMode(); yield return null;
            arena = Object.FindAnyObjectByType<CombatArena>();
            arena.loadCharacterArt = false;
            var content = CombatContentLoader.Load(null, false);
            content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout(); content.Profile = CombatCompositionProfile.Game;
            content.Camera = new GameObject("RunInkFxCamera").AddComponent<Camera>(); content.PlayerSpawn = new Vector2(0, 1000);
            content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = new Vector2(.1875f, 1);
            content.Enemies = new[] { new CombatEnemySpawn("InkFxTarget", 1, new Vector2(1, 1000), Vector2.one * 2, Vector2.up, 10000, true, false, 0) };
            content.PrepareEnvironment = _ => { var floor = new GameObject("Floor"); floor.transform.position = new Vector2(0, 999.5f); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1); };
            Assert.That(arena.Initialize(content), Is.True); driver = arena.AcquireManualSimulation(); driver.Reset();
            var growth = new WeaponGrowth(JsonUtility.FromJson<GrowthData>(Resources.Load<TextAsset>("WeaponGrowthMvp").text),
                new RunEconomy(JsonUtility.FromJson<EconomyData>(Resources.Load<TextAsset>("Chapter1Economy").text)));
            arena.MvpGrowth = growth;
            growth.Sketch = new SketchEquipment(CombatArena.LoadRunInkCatalog(), growth.Economy, 123, new Dictionary<string, int[]> { ["base_sword"] = new[] { 0 } });
            arena.EnableSketchEquipment(123);
            foreach (var enemy in arena.enemies) enemy.aggressive = false;
            minimum = CombatDamageVariance.Settings.minimum; maximum = CombatDamageVariance.Settings.maximum;
            CombatDamageVariance.Settings.minimum = CombatDamageVariance.Settings.maximum = 1;
            driver.Step(.01f);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            driver?.Dispose(); driver = null;
            CombatDamageVariance.Settings.minimum = minimum; CombatDamageVariance.Settings.maximum = maximum;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
        RunInkDefinition Ink(string trigger, string effect, string condition = null) => Equipment.Catalog.inks.Single(i => i.trigger == trigger && i.effect == effect && (condition == null || i.condition == condition));
        RunInkWeapon Equip(int slot, params RunInkDefinition[] inks)
        {
            // An ink-only choice (the ink half of a double reward) is taken until a weapon choice comes.
            while (Equipment.RewardChoices().All(c => c.Weapon == null)) { if (Equipment.PendingReward) Equipment.ChooseReward(0); else { Equipment.RecordKill(false); Equipment.OpenReward(); } }
            int index = Equipment.RewardChoices().ToList().FindIndex(c => c.Weapon != null);
            var weapon = Equipment.ChooseReward(index).Weapon;
            Equipment.DebugResizePalette(weapon.Id, Math.Max(3, inks.Length));
            for (int i = 0; i < weapon.Slots.Count; i++) Equipment.DebugSetSlotColor(weapon.Id, i, "neutral");
            for (int i = 0; i < inks.Length; i++) { Equipment.Register(inks[i].id); Equipment.Place(weapon.Id, i, inks[i].id); }
            Equipment.Confirm(weapon.Id); Equipment.Equip(weapon.Id, slot); arena.SyncSketchLoadout(); return weapon;
        }
        CombatHitOrigin Begin(int slot, bool basic = true)
        {
            var weapon = Player.weapons[slot]; Guid execution = Guid.NewGuid();
            var stamp = new CombatAttackStamp(weapon.InstanceId, "base_sword", "run_ink_fx_fixture", "1", basic, 0, weapon.InkPalette.Read());
            typeof(CombatArena).GetMethod("BeginInkAction", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(arena, new object[] { stamp, execution, Player });
            arena.TryBindActor(Player, out var source);
            return new CombatHitOrigin(source, default, execution, 0, "fixture", Guid.Empty, CombatHitKind.Melee, stamp: stamp);
        }
        void Hit(CombatHitOrigin origin, float damage = 1) => Assert.That(Target.Receive(damage, Player, 1, origin.Stamp.Basic, null, origin: origin), Is.True);
        void Advance(float seconds)
        { float until = arena.Now + seconds; while (arena.Now < until - .000001f) driver.Step(Mathf.Min(.01f, until - arena.Now)); }
        SpriteRenderer Renderer(CombatActor actor, string name)
        {
            var root = arena.transform.Find("[Ink status] " + actor.name); Assert.That(root, Is.Not.Null, actor.name);
            return root.Find(name).GetComponent<SpriteRenderer>();
        }
        static string SheetOf(SpriteRenderer renderer) => renderer.enabled && renderer.sprite ? renderer.sprite.name.Split(' ')[0] : null;
        static string VariantOf(SpriteRenderer renderer) => renderer.sprite.name.Split(' ')[1];

        [Serializable] sealed class SheetInfo { public string name; public int columns, rows, cellWidth, cellHeight; }
        [Serializable] sealed class SheetTable { public SheetInfo[] sheets; }
        [Test] public void EverySelectedSheetIsDeployedWithItsDeclaredLayout()
        {
            var table = Resources.Load<TextAsset>("InkBuffFxSheets"); Assert.That(table, Is.Not.Null, "run tools/assets/update_assets.py");
            var names = new List<string>();
            foreach (var sheet in JsonUtility.FromJson<SheetTable>(table.text).sheets)
            {
                var texture = Resources.Load<Texture2D>("InkBuffFx/" + sheet.name);
                Assert.That(texture, Is.Not.Null, sheet.name);
                Assert.That(texture.width, Is.EqualTo(sheet.columns * sheet.cellWidth), sheet.name);
                Assert.That(texture.height, Is.EqualTo(sheet.rows * sheet.cellHeight), sheet.name);
                Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Point), "pixel FX stay crisp");
                names.Add(sheet.name);
            }
            Assert.That(names, Is.EquivalentTo(new[] { "speed-wind", "shield-front", "shield-gain", "shield-hit", "dash-shield", "dash-shield-break", "heal-crosses",
                "red-flames-back", "red-flames-front", "blue-flames-back", "blue-flames-front", "cdr-clock", "chain-front", "chain-back", "chain-in", "chain-snap", "slow-snail" }));
        }
        [UnityTest] public IEnumerator SpeedHealAndShieldInksShowOnThePlayerWhileTheyLast()
        {
            Equip(0, Ink("basic_attack", "movement_speed_increase"), Ink("basic_attack", "heal"), Ink("basic_attack", "shield"));
            Player.health = Player.maxHealth - 10;
            Hit(Begin(0)); arena.RefreshInkStatusVisuals();
            Assert.That(arena.ReadInkStatusParticles(Player, "heal-crosses"), Is.EqualTo(1), "a real heal plays the crosses once");
            Assert.That(VariantOf(Live("heal-crosses")), Is.EqualTo("base"));
            if (Player.visual && Player.visual.enabled && Player.visual.sprite)
            { var flash = arena.ReadInkStatusBodyTint(Player); Assert.That(flash.g, Is.GreaterThan(flash.r)); Assert.That(flash.a, Is.GreaterThan(.4f), "the body flashes green"); }
            Assert.That(arena.ReadInkStatusParticles(Player, "shield-gain"), Is.EqualTo(1));
            var wind = Renderer(Player, "Speed wind"); Assert.That(SheetOf(wind), Is.EqualTo("speed-wind"));
            Assert.That(wind.sortingOrder, Is.LessThan(Player.visual ? Player.visual.sortingOrder : int.MaxValue), "the wind trails behind the body");
            Assert.That(SheetOf(Renderer(Player, "Ink shield")), Is.EqualTo("shield-front"));
            Assert.That(VariantOf(Renderer(Player, "Ink shield")), Is.EqualTo("steady"));
            Player.SyncFacing(-1); arena.RefreshInkStatusVisuals();
            Assert.That(wind.transform.localScale.x, Is.EqualTo(-1), "the wind mirrors with the body");
            Player.SyncFacing(1);
            // 피해를 막으면 막 표시가 번쩍이고, 1.4초 뒤 회복 십자는 끝난다.
            Player.Receive(2, Target, 1, false, null); arena.RefreshInkStatusVisuals();
            Assert.That(arena.ReadInkStatusParticles(Player, "shield-hit"), Is.EqualTo(1));
            Advance(1.5f); arena.RefreshInkStatusVisuals();
            Assert.That(arena.ReadInkStatusParticles(Player, "heal-crosses"), Is.Zero);
            Advance(1.6f); arena.RefreshInkStatusVisuals();
            Assert.That(Renderer(Player, "Speed wind").enabled, Is.False, "the 3 s speed buff has ended");
            yield return null;
        }
        SpriteRenderer Live(string sheet) => arena.transform.Find("[Ink status] " + Player.name).GetComponentsInChildren<SpriteRenderer>().First(r => r.enabled && r.gameObject.name == sheet);
        [UnityTest] public IEnumerator RedAndBlueBuffsShowTheirFlamesAndTheCooldownCutPlaysTheClock()
        {
            Equip(0, Ink("basic_attack", "stun"), Ink("basic_attack", "basic_attack_damage_increase", "stun"));
            Equip(1, Ink("skill_attack", "skill_attack_damage_increase", "stun"), Ink("skill_attack", "cooldown_reduction", "stun"));
            Hit(Begin(0)); arena.RefreshInkStatusVisuals(); float redStart = arena.Now;
            Assert.That(SheetOf(Renderer(Player, "Red flames back")), Is.EqualTo("red-flames-back"));
            Assert.That(SheetOf(Renderer(Player, "Red flames front")), Is.EqualTo("red-flames-front"));
            var pool = Player.weapons[1].Cooldown; float cooldown = arena.catalog.forms[Player.weapons[1].FormIndex].specials[Player.weapons[1].SpecialIndex].cooldown;
            pool.Reset(1, cooldown); pool.Spend(arena.Now); Advance(cooldown * .6f);
            Target.stunUntil = arena.Now + 2;
            Hit(Begin(1, false)); arena.RefreshInkStatusVisuals();
            Assert.That(SheetOf(Renderer(Player, "Blue flames back")), Is.EqualTo("blue-flames-back"));
            Assert.That(arena.ReadInkStatusParticles(Player, "cdr-clock"), Is.EqualTo(1));
            Advance(1.3f); arena.RefreshInkStatusVisuals();
            Assert.That(Renderer(Player, "Blue flames back").enabled, Is.False, "blue flames only cover the rest of that skill use");
            Advance(Mathf.Max(0, redStart + 4.05f - arena.Now)); arena.RefreshInkStatusVisuals();
            Assert.That(Renderer(Player, "Red flames back").enabled, Is.False, "red ended after 4 s");
            yield return null;
        }
        [UnityTest] public IEnumerator RootShowsCenteredRopesImmediatelyForBothBodySizesAndSlowKeepsItsSnail()
        {
            var weapon = Equip(0, Ink("basic_attack", "root"), Ink("basic_attack", "slow"));
            Hit(Begin(0)); arena.RefreshInkStatusVisuals();
            var front = Renderer(Target, "Bind front"); var back = Renderer(Target, "Bind back");
            Assert.That(SheetOf(front), Is.EqualTo("bind-front"), "root shows the ropes immediately");
            Assert.That(SheetOf(back), Is.EqualTo("bind-back"));
            Assert.That(VariantOf(front), Is.EqualTo("large"));
            Assert.That(VariantOf(back), Is.EqualTo("large"));
            Assert.That(front.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(back.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(front.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(back.sortingOrder, Is.LessThan(front.sortingOrder));
            Assert.That(arena.ReadInkStatusParticles(Target, "chain-in"), Is.Zero);
            var frame = front.sprite;
            arena.RefreshInkStatusVisuals();
            Assert.That(front.sprite, Is.SameAs(frame), "refreshing the paused combat clock keeps the rope frame");
            var snail = Renderer(Target, "Slow"); Assert.That(SheetOf(snail), Is.EqualTo("slow-snail"));
            Assert.That(snail.transform.localPosition.y, Is.GreaterThan(Target.size.y * .5f), "the snail sits over the head");
            if (Target.visual && Target.visual.enabled && Target.visual.sprite) Assert.That(arena.ReadInkStatusBodyTint(Target).a, Is.GreaterThan(0), "slowed bodies take a faint violet tint");
            Advance(.2f); arena.RefreshInkStatusVisuals();
            Assert.That(SheetOf(front), Is.EqualTo("bind-front"));
            Advance(1); arena.RefreshInkStatusVisuals();
            Assert.That(front.enabled, Is.False); Assert.That(back.enabled, Is.False);
            Assert.That(arena.ReadInkStatusParticles(Target, "chain-snap"), Is.Zero);
            Advance(2.5f); arena.RefreshInkStatusVisuals();
            Assert.That(arena.ReadRunInkSlow(Target), Is.Zero); Assert.That(Renderer(Target, "Slow").enabled, Is.False);
            Advance(Mathf.Max(weapon.Slots[0].Cooldown, Target.BossControl.RootRemaining(arena.Now)) + .01f);
            Target.size = Vector2.one; Target.bodyCollider.size = Target.size; Target.hurtCollider.size = Target.size;
            Physics2D.SyncTransforms();
            Hit(Begin(0)); arena.RefreshInkStatusVisuals();
            Assert.That(SheetOf(front), Is.EqualTo("bind-front"));
            Assert.That(SheetOf(back), Is.EqualTo("bind-back"));
            Assert.That(VariantOf(front), Is.EqualTo("small"));
            Assert.That(VariantOf(back), Is.EqualTo("small"));
            Target.rootUntil = arena.Now; arena.RefreshInkStatusVisuals();
            Assert.That(front.enabled, Is.False); Assert.That(back.enabled, Is.False);
            Assert.That(arena.ReadInkStatusParticles(Target, "chain-snap"), Is.Zero);
            yield return null;
        }
        [UnityTest] public IEnumerator DashShieldHoldsItsCrescentThenBreaksAway()
        {
            Equip(0, Ink("dash_use", "shield")); Player.selected = 0; Player.grounded = true;
            driver.Step(.01f, new CombatInput(pressed: CombatButtons.Dash)); arena.RefreshInkStatusVisuals();
            Assert.That(Player.Dashing, Is.True);
            Assert.That(SheetOf(Renderer(Player, "Dash shield")), Is.EqualTo("dash-shield"));
            Assert.That(Renderer(Player, "Ink shield").enabled, Is.False, "the dash shield is not drawn as the normal bubble");
            float grace = Equipment.Catalog.dashShieldGrace;
            Advance(.5f + grace); arena.RefreshInkStatusVisuals();
            if (arena.ReadRunInkBuffs().DashShield > 0) Assert.That(SheetOf(Renderer(Player, "Dash shield")), Is.EqualTo("dash-shield-break"));
            Advance(2); arena.RefreshInkStatusVisuals();
            Assert.That(arena.ReadRunInkBuffs().DashShield, Is.Zero); Assert.That(Renderer(Player, "Dash shield").enabled, Is.False);
            yield return null;
        }
    }
}
