using System;
using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Integration;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    public sealed class RoguelitePerkIntegrationTests
    {
        float minimum, maximum;
        bool varianceSaved;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (varianceSaved) { CombatDamageVariance.Settings.minimum = minimum; CombatDamageVariance.Settings.maximum = maximum; }
            if (Application.isPlaying) yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
        static RunSession Run()
        {
            var catalog = new ProgressionCatalog(JsonUtility.FromJson<ProgressionData>(File.ReadAllText("../data/combat/roguelite_progression.json")));
            var p = ProfileData.CreateNew(Guid.NewGuid().ToString()); p.runsDied = 1; p.chapter1BossFirstClear = true; p.chaptersCleared = 2; p.pigment = 100000;
            var service = new ProgressionService(catalog, new MemoryProfileStore(p));
            foreach (var u in catalog.Unlocks) Assert.That(service.TryPurchase(u.id, out _), Is.True, u.id);
            return new RunFlow(() => 42, JsonUtility.FromJson<EconomyData>(Resources.Load<TextAsset>("Chapter1Economy").text))
                .StartNewRun(0, plan: service.PlanUnarmedRun());
        }
        CombatArena Create(RunSession run)
        {
            minimum = CombatDamageVariance.Settings.minimum; maximum = CombatDamageVariance.Settings.maximum; varianceSaved = true;
            CombatDamageVariance.Settings.minimum = CombatDamageVariance.Settings.maximum = 1;
            var a = new GameObject("Roguelite perk arena").AddComponent<CombatArena>(); a.loadCharacterArt = false;
            var content = CombatContentLoader.Load(null, false); content.Profile = CombatCompositionProfile.Game;
            content.Camera = new GameObject("Camera").AddComponent<Camera>(); content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
            content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = new Vector2(.1875f, 1); content.PlayerSpawn = new Vector2(0, 100);
            content.Enemies = new[] { new CombatEnemySpawn("Target", 0, new Vector2(1, 100), Vector2.one, Vector2.zero, 100000, false, false, 1) };
            Assert.That(a.Initialize(content), Is.True); a.Player.UseTestInput = true;
            run.Growth = new WeaponGrowth(JsonUtility.FromJson<GrowthData>(Resources.Load<TextAsset>("WeaponGrowthMvp").text), run.Economy) { Perks = run.Perks };
            a.MvpGrowth = run.Growth;
            StartingVillageWorld.DressUnarmed(a);
            RunRevivalBridge.Attach(a, run); return a;
        }
        [UnityTest] public IEnumerator NativeRevivalPreservesTheRunAndTheNextLethalHitDies()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            var run = Run(); var a = Create(run); VerifyRevival(a, run);
        }
        // Allocate the event closure after EnterPlayMode's domain reload, outside the serialized iterator.
        static void VerifyRevival(CombatArena a, RunSession run)
        {
            a.Player.maxHealth = 140; a.Player.health = 140;
            int deaths = 0; a.Player.Died += () => deaths++;
            Assert.That(a.Player.Receive(999, a.enemies[0], 1, false, null, bypass: true), Is.True);
            Assert.That(a.Player.health, Is.EqualTo(70)); Assert.That(a.Player.Dead, Is.False); Assert.That(deaths, Is.Zero);
            Assert.That(run.Ended, Is.False); Assert.That(run.Perks.RevivesRemaining, Is.Zero);
            Assert.That(a.Player.Receive(999, a.enemies[0], 1, false, null, bypass: true), Is.True);
            Assert.That(a.Player.Dead, Is.True); Assert.That(deaths, Is.EqualTo(1));
        }
    }
}
