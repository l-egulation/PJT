using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Equipment.Contracts;
using Deviant.Integration;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public partial class WeaponBalanceReleaseTests
    {
        CombatArena arena;
        CombatArena.ManualSimulation driver;
        float varianceMin, varianceMax;
        CombatPlayer Player => arena.Player;
        TestEnemy Target => arena.enemies[0];

        [UnitySetUp] public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            arena = new GameObject("Release balance arena").AddComponent<CombatArena>();
            arena.loadCharacterArt = false;
            var content = CombatContentLoader.Load(null, false);
            content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
            content.Profile = CombatCompositionProfile.Game;
            content.Camera = new GameObject("Release balance camera").AddComponent<Camera>();
            content.PlayerSpawn = new Vector2(0, 1000);
            content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = new Vector2(.1875f, 1);
            content.Enemies = new[] { new CombatEnemySpawn("Balance target", 1, new Vector2(1.3f, 1000), new Vector2(1, 2), Vector2.up, 10000, true, false, 0) };
            content.PrepareEnvironment = _ => {
                var floor = new GameObject("Release balance floor"); floor.transform.position = new Vector2(0, 999.5f);
                floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
            };
            Assert.That(arena.Initialize(content), Is.True);
            driver = arena.AcquireManualSimulation(); driver.Reset();
            PlaytestEnvironment.ApplyBasicSpeedTrials(arena);
            varianceMin = CombatDamageVariance.Settings.minimum; varianceMax = CombatDamageVariance.Settings.maximum;
            CombatDamageVariance.Settings.minimum = CombatDamageVariance.Settings.maximum = 1;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            driver?.Dispose(); driver = null;
            CombatDamageVariance.Settings.minimum = varianceMin; CombatDamageVariance.Settings.maximum = varianceMax;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
        void Ready(string form)
        {
            driver.Reset(); Player.selected = 0; Player.UseTestInput = true; Player.InputBlocked = false;
            Player.SetWeaponSlotReady(0, true); Player.SetWeaponSlotReady(1, true);
            foreach (var weapon in Player.weapons) weapon.StatSlots.Clear();
            Assert.That(Player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == form), 0, out var error), Is.True, error);
            Player.Position = new Vector2(0, 1000); Player.velocity = Vector2.zero; Player.SyncFacing(1); Player.grounded = true;
            foreach (var target in arena.enemies) {
                target.ResetActor(new Vector2(1.3f, 1000)); target.health = target.maxHealth = 10000;
                target.aggressive = false; target.isBoss = true; target.velocity = Vector2.zero;
            }
            Physics2D.SyncTransforms();
        }
        void Advance(float seconds)
        {
            float end = arena.Now + seconds;
            while (arena.Now < end - .000001f) driver.Step(Mathf.Min(.001f, end - arena.Now));
        }

        [UnityTest] public IEnumerator RealInputsHitOneAndThreeTargetsWithOriginalCadenceForAllTenForms()
        {
            var forms = new[] { "base_sword", "whip", "dagger", "rapier", "hammer", "greatsword", "shield", "launcher", "bomb", "shuriken" };
            var hits = new List<CombatAcceptedHit>();
            arena.AcceptedHit += hit => { if (hit.Origin.EffectiveSource.ActorId == Player.ActorId) hits.Add(hit); };
            foreach (int targets in new[] { 1, 3 }) {
                if (targets == 3) for (int i = 0; i < 2; i++) {
                    var extra = new GameObject("Overlapping balance target").AddComponent<TestEnemy>();
                    extra.Configure(arena, new Vector2(1.3f, 1000), new Vector2(1, 2), Vector2.up, false);
                    extra.health = extra.maxHealth = 10000; extra.aggressive = false; extra.isBoss = true; arena.enemies.Add(extra);
                }
                foreach (string form in forms) {
                    Ready(form); hits.Clear();
                    var authored = Player.Form.basics;
                    float speed = Player.BasicSpeed;
                    driver.Step(.001f, new CombatInput(pressed: CombatButtons.Basic));
                    float cycleStart = Player.currentCast.StartedAt;
                    for (int index = 0; index < authored.Length; index++) {
                        var cast = Player.currentCast;
                        Assert.That(cast, Is.Not.Null, form);
                        Assert.That(cast.MotionDuration, Is.EqualTo(authored[index].duration / speed).Within(.00001f), form + " duration");
                        Assert.That(cast.ImpactTime, Is.EqualTo(authored[index].impact / speed).Within(.00001f), form + " impact");
                        int ticks = 0;
                        while (Player.currentCast == cast && ticks++ < 2500) {
                            driver.Step(.001f, index < authored.Length - 1 ? new CombatInput(pressed: CombatButtons.Basic) : default);
                        }
                        Assert.That(ticks, Is.LessThan(2500), form);
                    }
                    // Let generated projectiles resolve. They spawn overlapping these targets.
                    Advance(.03f);
                    bool single = form == "launcher" || form == "shuriken";
                    int count = single ? 1 : targets;
                    Assert.That(hits.Count, Is.EqualTo(authored.Length * count), form + " hit multiplicity");
                    Assert.That(hits.Sum(h => h.Damage), Is.EqualTo(authored.Sum(b => b.damage) * count).Within(.01f), form + " damage");
                    Assert.That(arena.SimulationFaulted, Is.False);
                    Debug.Log($"BALANCE_COMBO {form} targets={targets} speed={speed:F3} damage={hits.Sum(h => h.Damage):F2} duration={authored.Sum(b => b.duration) / speed:F6} first={hits[0].Now - cycleStart:F6}");
                }
            }
            yield return null;
        }

    }
}
