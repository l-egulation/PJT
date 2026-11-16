using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using ForestCombatV2;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Stage1Monsters.Game.Tests
{
    // Wind-up cue (user decision 2026-10-30): a gentle one-pixel shake through the wind-up, then a flat flash in the
    // last 0.1 s before the hit. Presentation only; the actor never moves for it.
    public sealed class Stage1VillageWindupCueTests
    {
        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest] public IEnumerator ThornAndBroomShakeOnePixelThenFlashJustBeforeTheirHit()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode(); yield return null; yield return null;
            var world = Object.FindAnyObjectByType<ForestCombatWorld>(); Assert.That(world.Ready, Is.True);
            var arena = world.arena; arena.Player.UseTestInput = true; arena.Player.maxHealth = arena.Player.health = 100000;
            var enemies = arena.enemies.OfType<FieldMouseEnemy>().ToArray();
            var floor = new GameObject("Temporary wind-up cue floor"); floor.transform.position = new Vector3(-500, -.5f, 0);
            floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1); Physics2D.SyncTransforms();
            foreach (var index in enemies.Select(e => e.SurfaceIndex).Distinct()) index.Rebuild(Object.FindObjectsByType<ArenaSurface>(FindObjectsSortMode.None));
            var presentations = world.GetComponentsInChildren<Stage1VillageMonsterPresentation>();
            using (var simulation = arena.AcquireManualSimulation())
            {
                foreach (var id in new[] { "01_thorn-root", "02_broom-stalker" })
                {
                    arena.ResetTest(); foreach (var e in enemies) { e.aggressive = false; e.CancelAttack(); }
                    var actor = enemies.First(e => e.Definition.id == id); actor.spawn = new Vector2(-500, .003f); actor.ResetEnemy(); actor.SyncFacing(1);
                    arena.Player.Position = actor.Position + new Vector2(1.2f, 0); arena.Player.velocity = Vector2.zero; Physics2D.SyncTransforms();
                    var cue = actor.GetComponent<Stage1VillageMonsterPresentation>().Windup;
                    Assert.That(cue.TryGetWindow(out float start, out float strike), Is.True, id);
                    actor.aggressive = true;
                    bool Step() { simulation.Step(.005f); foreach (var p in presentations) p.Draw(); return actor.SpeciesAttackVisible; }
                    for (int i = 0; i < 800 && !(Step() && actor.SpeciesAttackAge >= start); i++) { }
                    Assert.That(actor.SpeciesAttackVisible && actor.SpeciesAttackAge >= start, Is.True, id + " starts its attack");
                    var offsets = new HashSet<int>(); var held = actor.Position; bool flashed = false; float lastRed = 0, maxRed = 0;
                    float flashFrom = strike - actor.GetComponent<Stage1VillageMonsterAttack>().Spec.cue.flashSeconds - 1e-4f;
                    while (actor.SpeciesAttackAge < strike - .006f)
                    {
                        offsets.Add(cue.OffsetPixels);
                        Assert.That(actor.visual.transform.localPosition.x, Is.EqualTo(cue.OffsetPixels / 32f).Within(1e-6), "the sprite carries the cue");
                        if (actor.SpeciesAttackAge >= flashFrom) { flashed |= cue.Flashing; }
                        else
                        {
                            Assert.That(cue.Flashing, Is.False, id + " flashes only in the last 0.1 s");
                            Assert.That(cue.Redness, Is.GreaterThanOrEqualTo(lastRed - 1e-5f), id + " reddens steadily over the wind-up");
                            lastRed = cue.Redness; maxRed = Mathf.Max(maxRed, cue.Redness);
                        }
                        Step();
                    }
                    Assert.That(offsets.IsSubsetOf(new[] { -1, 0, 1 }) && offsets.Contains(1) && offsets.Contains(-1), Is.True,
                        id + " shakes by one pixel, both sides near the end: " + string.Join(",", offsets));
                    Assert.That(flashed, Is.True, id + " flashes just before the hit");
                    Assert.That(maxRed, Is.GreaterThan(.4f), id + " a melee wind-up turns red before the flash");
                    Assert.That(Vector2.Distance(actor.Position, held), Is.LessThan(1e-4f), "the actor itself does not move for the cue");
                    while (actor.SpeciesAttackAge < strike + .01f) Step();
                    Assert.That(cue.Flashing || cue.OffsetPixels != 0 || cue.Redness > 0, Is.False, id + " cue ends at the hit");
                }
            }
        }
    }
}
