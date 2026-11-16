using System;
using System.Collections;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using Deviant.Integration;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    // 실제 게임 카탈로그의 모든 구현 스킬은 적이 전혀 없는 전투장에서도 시작하고 자원을 소비한다.
    public class TargetlessSpecialWhiffTests
    {

        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest] public IEnumerator AllFortySkillsActivateSpendAndFinishWithoutAnyEnemy()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            var arena = new GameObject("WhiffArena").AddComponent<CombatArena>(); var camera = new GameObject("WhiffCamera").AddComponent<Camera>();
            var floor = new GameObject("WhiffFloor"); floor.transform.position = new Vector3(0, -.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(200, 1);
            var configuration = CombatContentLoader.Load(null, false);
            configuration.Profile = CombatCompositionProfile.Game; configuration.Camera = camera; configuration.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
            configuration.PlayerSpawn = Vector2.zero; configuration.PlayerSize = new Vector2(.9375f, 2); configuration.PlayerOffset = new Vector2(.1875f, 1); configuration.SecondaryFormIndex = 0;
            configuration.Enemies = Array.Empty<CombatEnemySpawn>();
            try
            {
                Assert.That(arena.Initialize(configuration), Is.True);
                Assert.That(arena.enemies, Is.Empty);
                Assert.That(arena.catalog.forms.SelectMany(f => f.specials).Count(s => !s.unavailable), Is.EqualTo(40));
                using (var driver = arena.AcquireManualSimulation())
                foreach (string id in arena.catalog.forms.SelectMany(f => f.specials).Where(s => !s.unavailable).Select(s => s.id).ToArray())
                {
                    driver.Reset(); var p = arena.Player; p.selected = 0; p.InputBlocked = false;
                    foreach (var weapon in p.weapons) weapon.StatSlots.Clear();
                    int form = Array.FindIndex(arena.catalog.forms, f => f.specials != null && Array.Exists(f.specials, s => s != null && s.id == id));
                    Assert.That(form, Is.GreaterThanOrEqualTo(0), id);
                    int special = Array.FindIndex(arena.catalog.forms[form].specials, s => s != null && s.id == id);
                    Assert.That(p.TryReconfigure(0, form, special, out var error), Is.True, id + ": " + error);
                    p.Position = Vector2.zero; p.velocity = Vector2.zero; p.grounded = true; p.SyncFacing(1);
                    Physics2D.SyncTransforms();
                    int before = p.Weapon.Cooldown.Available; Assert.That(before, Is.GreaterThan(0), id);

                    driver.Step(.02f, new CombatInput(0, CombatButtons.Weapon1Special));
                    var cast = p.currentCast;
                    Assert.That(cast, Is.Not.Null, id + " starts without a target in reach");
                    Assert.That(cast.isBasic, Is.False, id); Assert.That(cast.Definition.Id, Is.EqualTo(id));
                    bool spent = false;
                    var source = arena.catalog.forms[form].specials[special];
                    int maxTicks = Mathf.CeilToInt((source.end + source.chargeMax + 2) / .02f);
                    for (int tick = 0; tick < maxTicks && ReferenceEquals(p.currentCast, cast); tick++)
                    { driver.Step(.02f); spent |= p.Weapon.Cooldown.Available < before; }
                    Assert.That(arena.SimulationFaulted, Is.False, id);
                    Assert.That(p.currentCast, Is.Null, id + " finishes its whiff");
                    Assert.That(cast.ResourceResult?.Status, Is.EqualTo(ActionResourceStatus.Succeeded), id + " activates instead of failing");
                    Assert.That(spent, Is.True, id + " spends its charge");
                    if (id == "rapier_movement" || id == "rapier_follow_up")
                        Assert.That(Mathf.Abs(p.Position.x), Is.LessThan(1.5f), id + " does not teleport without a target");
                }
            }
            finally { Object.Destroy(floor); }
        }
    }
}
