using System;
using System.Collections;
using System.Linq;
using Deviant.Definitions;
using Deviant.Integration;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public sealed class SpecialProjectileTerrainTests
    {
        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest] public IEnumerator StraightSpecialsPassMuzzleAndFlightWallsButBasicsStillStop()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Special projectile test arena").AddComponent<CombatArena>();
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var content = CombatContentLoader.Load(null, false);
            content.Profile = CombatCompositionProfile.Game; content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
            content.Camera = new GameObject("Special projectile test camera").AddComponent<Camera>();
            content.Camera.tag = "MainCamera"; content.Camera.orthographic = true; content.Camera.orthographicSize = 10;
            content.PlayerSpawn = new Vector2(0, 1000); content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = new Vector2(.1875f, 1);
            content.Enemies = new[] { new CombatEnemySpawn("Target behind wall", 0, new Vector2(5, 1000), Vector2.one, Vector2.up, 10000, false, false, 0) };
            Assert.That(arena.Initialize(content), Is.True);
            var muzzleWall = Wall("Muzzle wall"); var flightWall = Wall("Flight wall");
            var target = arena.enemies[0]; var player = arena.Player;
            var specials = arena.catalog.forms.SelectMany(f => f.specials).Where(s => s.ranged != null && s.ranged.ignoreTerrain).ToArray();
            Assert.That(specials.Length, Is.EqualTo(7));
            using (var sim = arena.AcquireManualSimulation())
            {
                foreach (int direction in new[] { -1, 1 })
                foreach (var special in specials)
                {
                    Prepare(direction, false);
                    var launch = new SkillRangedLaunchDefinition(special.ranged).WithChargeAmount(1);
                    arena.FirePlayerRanged(player, launch, 10, direction);
                    if (launch.Projectile.Type == "laser")
                    {
                        Assert.That(arena.lasers.Count, Is.EqualTo(1));
                    }
                    else
                    {
                        var shots = arena.playerShots.ToArray();
                        Assert.That(shots.Length, Is.EqualTo(launch.Projectile.Type == "shotgun" ? 5 : 1), special.id + " muzzle wall");
                        foreach (var shot in shots)
                        {
                            Vector2 start = shot.Position; float speed = shot.velocity.magnitude, distance = shot.Definition.Distance;
                            shot.Step((distance - .1f) / speed);
                            Assert.That(arena.playerShots.Contains(shot), Is.True, special.id + " flight wall");
                            Assert.That(shot.traveled, Is.EqualTo(distance - .1f).Within(.001f));
                            shot.Step(1);
                            Assert.That(arena.playerShots.Contains(shot), Is.False, special.id + " expires at its own range");
                            Assert.That(shot.traveled, Is.EqualTo(distance).Within(.001f));
                            Assert.That(Vector2.Distance(start, shot.Position), Is.EqualTo(distance).Within(.002f));
                        }
                    }
                    Prepare(direction, true);
                    arena.FirePlayerRanged(player, launch, 10, direction);
                    foreach (var shot in arena.playerShots.ToArray()) shot.Step(launch.Projectile.Distance / launch.Speed + .1f);
                    Assert.That(target.ReceivedHitCount, Is.GreaterThan(0), special.id + " still hits an enemy behind the wall");
                }
                foreach (int direction in new[] { -1, 1 })
                foreach (string form in new[] { "launcher", "shuriken" })
                {
                    Prepare(direction, false);
                    var basic = arena.catalog.forms.Single(f => f.id == form).basics[0].ranged;
                    arena.FirePlayerRanged(player, basic, 10, direction);
                    Assert.That(arena.playerShots, Is.Empty, form + " basic keeps muzzle obstruction");
                }
                Assert.That(arena.SimulationFaulted, Is.False);

                void Prepare(int direction, bool enemy)
                {
                    target.gameObject.SetActive(true); sim.Reset(); player.ResetPlayer(); player.SyncFacing(direction);
                    target.ResetEnemy(); target.aggressive = false; target.Position = new Vector2(direction * 5, 1000); target.gameObject.SetActive(enemy);
                    muzzleWall.transform.position = new Vector2(direction * .5f, 1001);
                    flightWall.transform.position = new Vector2(direction * 3, 1001);
                    Physics2D.SyncTransforms();
                }
            }
            static BoxCollider2D Wall(string name)
            {
                var root = new GameObject(name); root.AddComponent<ArenaSurface>();
                var box = root.AddComponent<BoxCollider2D>(); box.size = new Vector2(.1f, 10); return box;
            }
        }
    }
}
