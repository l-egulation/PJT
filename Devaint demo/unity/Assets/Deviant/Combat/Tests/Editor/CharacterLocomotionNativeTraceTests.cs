using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Deviant.Combat.Contracts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    // Frozen before the owner cutover. Only the existing Game/Lab, player,
    // presenter and SpriteRenderer APIs participate; no distributed art is used.
    public class CharacterLocomotionNativeTraceTests
    {
        [Serializable] sealed class Row
        {
            public string label, form, clip, sprite, action, attackVersion;
            public int frame, facing, basicIndex, hits;
            public bool grounded, dashing, approaching, dead, hidden, current, wall, faulted;
            public int[] bits;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        static readonly string[] States = { "idle", "run", "jump_start", "jump_rise", "jump_apex", "fall_start", "fall_loop", "land", "dash", "death", "attack1", "attack2" };
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        [UnityTest] public IEnumerator GameCharacterLocomotionNativeTrace() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game");
        [UnityTest] public IEnumerator LabCharacterLocomotionNativeTrace() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab");
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        static CharacterAnimationCatalog Catalog(Texture2D texture, List<Sprite> sprites)
        {
            var sets = new List<CharacterAnimationSet>();
            foreach (string form in new[] { "base_sword", "rapier" })
            {
                var clips = new List<CharacterClip>();
                foreach (string state in States)
                {
                    var frames = new string[3]; var body = new Sprite[3];
                    for (int frame = 0; frame < 3; frame++)
                    {
                        frames[frame] = form + "/" + state + "/" + frame;
                        body[frame] = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero, 32);
                        body[frame].name = frames[frame]; sprites.Add(body[frame]);
                    }
                    clips.Add(new CharacterClip { name = state, frames = frames, sprites = body,
                        seconds = state.StartsWith("attack", StringComparison.Ordinal) ? new[] { .125f, .125f, .25f } : new[] { .0625f, .0625f, .125f },
                        contact = 1, sourceImpact = .125f, fxStart = -1 });
                }
                sets.Add(new CharacterAnimationSet { form = form, clips = clips.ToArray() });
            }
            return new CharacterAnimationCatalog { sets = sets.ToArray() };
        }
        static void Prepare(CombatArena arena, CombatArena.ManualSimulation driver, string form = "base_sword", int special = 0)
        {
            driver.Reset(); var p = arena.Player; p.selected = 0; p.InputBlocked = false;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40, 1000); enemy.velocity = Vector2.zero; }
            foreach (var weapon in p.weapons) weapon.StatSlots.Clear();
            Assert.That(p.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == form), special, out var error), Is.True, error);
            p.Position = new Vector2(0, 1000); p.velocity = Vector2.zero; p.grounded = true; p.SyncFacing(1); Physics2D.SyncTransforms();
        }
        static void Observe(Trace trace, string label, CombatArena arena, float dt, CharacterAnimationCatalog media, CombatCast observed = null)
        {
            var p = arena.Player; var cast = p.currentCast; var set = Array.Find(media.sets, s => s.form == arena.Presentation.CurrentForm);
            var clip = set?.Find(arena.Presentation.CurrentClip); var color = p.visual.color;
            trace.rows.Add(new Row { label = label, form = arena.Presentation.CurrentForm, clip = arena.Presentation.CurrentClip,
                sprite = p.visual.sprite ? p.visual.sprite.name : "", frame = clip?.sprites == null ? -1 : Array.IndexOf(clip.sprites, p.visual.sprite),
                action = cast ? cast.Definition.Id : "", attackVersion = arena.Presentation.CurrentAttackMotionVersion,
                facing = p.facing, basicIndex = p.BasicIndex, hits = arena.enemies[0].ReceivedHitCount,
                grounded = p.grounded, dashing = p.Dashing, approaching = cast && cast.Approaching, dead = p.Dead, hidden = p.Hidden,
                current = cast, wall = p.wallHit, faulted = arena.SimulationFaulted,
                bits = new[] { Bits(arena.Now), Bits(dt), Bits(p.Position.x), Bits(p.Position.y), Bits(p.velocity.x), Bits(p.velocity.y),
                    Bits(color.r), Bits(color.g), Bits(color.b), Bits(color.a), Bits(cast ? cast.Age : 0), Bits(cast ? cast.ImpactTime : 0),
                    Bits(cast ? cast.MotionDuration : 0), Bits(cast ? cast.EndTime : 0), Bits(observed ? observed.EndTime : 0),
                    Bits(cast ? cast.DashVisualDuration : 0), Bits(p.health), Bits(arena.enemies[0].health) } });
        }
        static void Sample(Trace trace, string label, CombatArena arena, CharacterAnimationCatalog media, float dt,
            bool grounded, float vy, int axis, string state, int frame, string form = "base_sword", CombatCast observed = null)
        {
            var p = arena.Player; p.grounded = grounded; p.velocity = new Vector2(p.velocity.x, vy); p.axis = axis;
            arena.Presentation.Draw(dt);
            Assert.That(arena.Presentation.CurrentClip, Is.EqualTo(state), label);
            Assert.That(arena.Presentation.CurrentForm, Is.EqualTo(form), label);
            Assert.That(p.visual.sprite, Is.SameAs(Array.Find(media.sets, s => s.form == form).Find(state).sprites[frame]), label);
            Observe(trace, label, arena, dt, media, observed);
        }
        static void GroundAndLifetime(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver, CharacterAnimationCatalog media)
        {
            Prepare(arena, driver); var p = arena.Player;
            Sample(trace, "idle-entry", arena, media, .125f, true, 0, 0, "idle", 0);
            Sample(trace, "idle-frame", arena, media, .0625f, true, 0, 0, "idle", 1);
            Sample(trace, "idle-loop", arena, media, .1875f, true, 0, 0, "idle", 0);
            Sample(trace, "walk-entry", arena, media, .125f, true, 0, 1, "run", 0);
            Sample(trace, "walk-frame", arena, media, .0625f, true, 0, 1, "run", 1);
            Sample(trace, "walk-loop", arena, media, .1875f, true, 0, 1, "run", 0);
            Sample(trace, "walk-stop", arena, media, .125f, true, 0, 0, "idle", 0);
            p.SyncFacing(-1); p.stealthUntil = arena.Now + 1;
            Sample(trace, "facing-hidden", arena, media, .0625f, true, 0, 0, "idle", 1);
            Assert.That(p.visual.flipX, Is.True); Assert.That(p.visual.color, Is.EqualTo(new Color(1, 1, 1, .35f))); p.stealthUntil = 0;
            Assert.That(p.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "rapier"), 0, out var error), Is.True, error);
            Sample(trace, "form-change-same-state", arena, media, .1875f, true, 0, 0, "idle", 0, "rapier");
            Sample(trace, "new-form-frame", arena, media, .0625f, true, 0, 0, "idle", 1, "rapier");
            arena.Presentation.Dispose();
            Sample(trace, "dispose-keeps-locomotion", arena, media, .0625f, true, 0, 0, "idle", 2, "rapier");
            arena.Presentation.Reset();
            Assert.That(arena.Presentation.CurrentClip, Is.EqualTo("idle")); Assert.That(arena.Presentation.CurrentForm, Is.EqualTo("rapier"));
            Sample(trace, "reset-restarts-frame", arena, media, .1875f, true, 0, 0, "idle", 0, "rapier");
            Assert.That(p.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "hammer"), 0, out error), Is.True, error);
            Sample(trace, "missing-form-body-fallback", arena, media, .125f, true, 0, 0, "idle", 0);
        }
        static void AirAndLanding(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver, CharacterAnimationCatalog media)
        {
            Prepare(arena, driver);
            Sample(trace, "jump-ground", arena, media, 0, true, 0, 0, "idle", 0);
            driver.Step(.125f); Sample(trace, "jump-start", arena, media, .125f, false, 8, 0, "jump_start", 0);
            driver.Step(.125f); Sample(trace, "jump-start-frame", arena, media, .0625f, false, 6, 0, "jump_start", 1);
            driver.Step(.125f); Sample(trace, "jump-rise-boundary", arena, media, .125f, false, 3, 0, "jump_rise", 0);
            Sample(trace, "jump-rise-frame", arena, media, .0625f, false, 3, 0, "jump_rise", 1);
            Sample(trace, "jump-apex-up", arena, media, .0625f, false, 2, 0, "jump_apex", 0);
            Sample(trace, "jump-apex-zero", arena, media, .0625f, false, 0, 0, "jump_apex", 1);
            Sample(trace, "jump-apex-down", arena, media, .0625f, false, -2, 0, "jump_apex", 2);
            Sample(trace, "fall-entry", arena, media, .25f, false, -3, 0, "fall_start", 0);
            Sample(trace, "fall-duration-reached-after-selection", arena, media, .25f, false, -4, 0, "fall_start", 2);
            Sample(trace, "fall-loop-next-draw", arena, media, 0, false, -5, 0, "fall_loop", 0);
            Sample(trace, "fall-loop-frame", arena, media, .0625f, false, -5, 0, "fall_loop", 1);
            Sample(trace, "fall-loop-wrap", arena, media, .1875f, false, -5, 0, "fall_loop", 0);
            Sample(trace, "double-jump-restart", arena, media, .1875f, false, 8, 0, "jump_start", 0);
            driver.Step(.125f); Sample(trace, "double-jump-frame", arena, media, .0625f, false, 6, 0, "jump_start", 1);
            Sample(trace, "velocity-small-increase", arena, media, .0625f, false, 6.5f, 0, "jump_start", 2);
            Sample(trace, "velocity-exact-one-not-jump", arena, media, .0625f, false, 7.5f, 0, "jump_start", 2);
            Sample(trace, "velocity-above-one-is-jump", arena, media, .1875f, false, 8.5625f, 0, "jump_start", 0);
            Sample(trace, "landing-entry", arena, media, .125f, true, 0, 0, "land", 0);
            Sample(trace, "run-precedes-land", arena, media, .0625f, true, 0, 1, "run", 0);
            Sample(trace, "stop-retains-land-window", arena, media, .0625f, true, 0, 0, "land", 0);
            driver.Step(.25f); Sample(trace, "land-exclusive-deadline", arena, media, .125f, true, 0, 0, "idle", 0);
            Sample(trace, "reset-air-entry", arena, media, 0, false, -5, 0, "fall_start", 0);
            Sample(trace, "reset-land-entry", arena, media, 0, true, 0, 0, "land", 0);
            arena.Presentation.Reset(); Sample(trace, "reset-clears-land-window", arena, media, .125f, true, 0, 0, "idle", 0);

            Prepare(arena, driver); var jump = media.sets[0].Find("jump_start"); var original = jump.seconds;
            Sample(trace, "live-duration-jump", arena, media, 0, false, 8, 0, "jump_start", 0);
            driver.Step(.25f); jump.seconds = new[] { .25f, .25f, .5f };
            Sample(trace, "live-duration-extends-start", arena, media, .0625f, false, 3, 0, "jump_start", 0);
            jump.seconds = original;
            Sample(trace, "live-duration-restored-boundary", arena, media, 0, false, 3, 0, "jump_rise", 0);
        }
        static void DashAndDeath(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver, CharacterAnimationCatalog media)
        {
            Prepare(arena, driver); var p = arena.Player;
            driver.Step(.03125f, new CombatInput(1, CombatButtons.Dash)); Assert.That(p.Dashing, Is.True);
            Sample(trace, "dash-entry", arena, media, 0, false, -5, 1, "dash", 0);
            Sample(trace, "dash-scaled-frame", arena, media, arena.Movement.DashDuration * .3f, false, -5, 1, "dash", 1);
            Sample(trace, "dash-clamps", arena, media, arena.Movement.DashDuration, false, -5, 1, "dash", 2);
            p.health = 0;
            Sample(trace, "death-precedes-live-dash", arena, media, .25f, false, -5, 1, "death", 0);
            Assert.That(p.Dashing, Is.True); Assert.That(p.visual.color, Is.EqualTo(Color.gray));
            p.Die(); Sample(trace, "death-cleanup", arena, media, 0, false, -5, 0, "death", 0);
            driver.Reset(); Sample(trace, "death-reset", arena, media, .125f, true, 0, 0, "idle", 0);
        }
        static CombatCast Start(CombatArena arena, SkillData source)
        {
            var p = arena.Player; var cast = CombatCast.Create(arena, p, source, p.Weapon, false); p.currentCast = cast; return cast;
        }
        static void AttackAndLanding(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver, CharacterAnimationCatalog media)
        {
            Prepare(arena, driver); var p = arena.Player;
            driver.Step(.015625f, new CombatInput(1, CombatButtons.Basic)); var first = p.currentCast;
            Assert.That(first && first.isBasic, Is.True); Assert.That(p.BasicIndex, Is.Zero);
            Sample(trace, "basic-precedes-air-run", arena, media, .5f, false, -5, 1, "attack1", 0);
            driver.Step(.015625f, new CombatInput(0, CombatButtons.Basic));
            for (int tick = 0; tick < 60 && p.BasicIndex != 1; tick++) driver.Step(.015625f);
            Assert.That(p.currentCast && p.currentCast.isBasic, Is.True); Assert.That(p.BasicIndex, Is.EqualTo(1));
            Sample(trace, "basic-next-index", arena, media, .5f, false, -5, 1, "attack2", 0);
            driver.Step(.015625f, new CombatInput(1, CombatButtons.Dash));
            Assert.That(p.currentCast, Is.Null); Assert.That(p.Dashing, Is.True);
            Sample(trace, "dash-cancels-body", arena, media, .25f, false, -5, 1, "dash", 0);

            Prepare(arena, driver); var source = JsonUtility.FromJson<SkillData>(JsonUtility.ToJson(p.Special));
            source.id = "presentation_landing_fixture"; source.activation = .125f; source.end = 1; source.hits = new HitData[0]; source.movement = null; source.effect = null;
            source.actionTracks = null; // This technical no-hit presentation identity is not the copied product action.
            var cast = Start(arena, source);
            Sample(trace, "special-precedes-air", arena, media, .5f, false, -5, 1, "attack1", 0);
            var land = media.sets[0].Find("land"); var original = land.seconds; land.seconds = new[] { 1f, 1f, 1f };
            Sample(trace, "captured-landing-duration", arena, media, .5f, true, 0, 0, "attack1", 0);
            land.seconds = original; p.CancelAction();
            Sample(trace, "cancel-keeps-captured-land-window", arena, media, 0, true, 0, 0, "land", 0, observed: cast);
            driver.Step(.25f); Sample(trace, "captured-land-original-deadline", arena, media, 0, true, 0, 0, "idle", 0, observed: cast);
        }
        static void Recast(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver, CharacterAnimationCatalog media)
        {
            Prepare(arena, driver, "whip", 2); var p = arena.Player; var target = arena.enemies[0];
            target.Position = new Vector2(3.5f, 1000); target.health = 1000; Physics2D.SyncTransforms();
            var cast = Start(arena, JsonUtility.FromJson<SkillData>(JsonUtility.ToJson(p.Special)));
            Sample(trace, "recast-primary-body", arena, media, 0, true, 0, 0, "attack1", 0);
            for (int tick = 0; tick < 60 && p.currentCast; tick++) driver.Step(.015625f);
            Assert.That(p.currentCast, Is.Null); Assert.That(target.ReceivedHitCount, Is.EqualTo(1)); Assert.That(cast.RecastRemaining, Is.GreaterThan(0));
            Sample(trace, "recast-primary-finished", arena, media, 0, true, 0, 0, "idle", 0);
            Assert.That(cast.TryApproachRecast(p.Weapon), Is.True); Assert.That(cast.Approaching, Is.True);
            Sample(trace, "recast-dash-precedes-attack", arena, media, 0, false, -5, 1, "dash", 0);
            Sample(trace, "recast-scaled-frame", arena, media, cast.DashVisualDuration * .3f, false, -5, 1, "dash", 1);
            p.health = 0; Sample(trace, "death-precedes-recast", arena, media, .25f, false, -5, 1, "death", 0);
            Assert.That(cast.Approaching, Is.True); p.Die();
        }
        static void FixedAndWallRecovery(Trace trace, CombatArena arena, CombatArena.ManualSimulation driver, CharacterAnimationCatalog media, BoxCollider2D wall)
        {
            foreach (bool blocked in new[] { false, true })
            {
                wall.enabled = false; Prepare(arena, driver, "base_sword", 2); var p = arena.Player;
                var source = JsonUtility.FromJson<SkillData>(JsonUtility.ToJson(p.Special));
                source.id = "presentation_wall_fixture"; source.activation = .125f; source.end = 1; source.hits = new HitData[0]; source.effect = null;
                source.actionTracks = null; // Custom no-hit media/wall fixture, not base_sword_movement's authored gameplay tracks.
                source.movement.start = .125f; source.movement.duration = .5f; source.movement.speed = 4; source.movement.distance = 2; source.movement.wallRecovery = .25f;
                wall.transform.position = new Vector3(p.offset.x + p.size.x / 2 + .1875f, 1005, 0); wall.enabled = blocked; Physics2D.SyncTransforms();
                var cast = Start(arena, source); string label = blocked ? "wall-recovery" : "fixed-recovery";
                Sample(trace, label + "/start", arena, media, .5f, true, 0, 0, "attack1", 0);
                driver.Step(.125f); driver.Step(.0625f);
                Assert.That(p.wallHit, Is.EqualTo(blocked)); Assert.That(cast.MotionDuration, Is.EqualTo(1));
                Assert.That(cast.EndTime, Is.EqualTo(blocked ? .4375f : 1));
                Sample(trace, label + "/contact", arena, media, .5f, true, 0, 0, "attack1", 1);
                driver.Step(.125f);
                Sample(trace, label + "/original-duration-body", arena, media, .5f, true, 0, 0, "attack1", 1);
                driver.Step(.125f);
                if (blocked) Sample(trace, label + "/current-end", arena, media, 0, true, 0, 0, "idle", 0, observed: cast);
                else Sample(trace, label + "/still-current", arena, media, 0, true, 0, 0, "attack1", 2);
                Assert.That(arena.SimulationFaulted, Is.False); wall.enabled = false;
            }
        }
        static IEnumerator Exercise(string scene, string label)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var original = arena.characterAnimations; var sprites = new List<Sprite>();
            var texture = new Texture2D(1, 1); var media = Catalog(texture, sprites); var trace = new Trace(); arena.characterAnimations = media;
            var floor = new GameObject("CharacterLocomotionFixtureFloor"); floor.transform.position = new Vector3(0, 999.5f, 0);
            floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(200, 1);
            var wallObject = new GameObject("CharacterLocomotionFixtureWall"); wallObject.AddComponent<ArenaSurface>();
            var wall = wallObject.AddComponent<BoxCollider2D>(); wall.size = new Vector2(.25f, 16); wall.enabled = false; Physics2D.SyncTransforms();
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                {
                    Assert.That(arena.TryRestoreSwordFixedTiming(false, out var timingError), Is.True, timingError);
                    GroundAndLifetime(trace, arena, driver, media); AirAndLanding(trace, arena, driver, media); DashAndDeath(trace, arena, driver, media);
                    AttackAndLanding(trace, arena, driver, media); Recast(trace, arena, driver, media); FixedAndWallRecovery(trace, arena, driver, media, wall);
                    Assert.That(arena.SimulationFaulted, Is.False);
                }
                var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-locomotionPresentationTraceRoot");
                if (at >= 0 && at + 1 < args.Length) { Directory.CreateDirectory(args[at + 1]); File.WriteAllText(Path.Combine(args[at + 1], label + ".json"), JsonUtility.ToJson(trace)); }
            }
            finally
            {
                arena.Presentation.Reset(); arena.characterAnimations = original; Object.Destroy(wallObject); Object.Destroy(floor);
                foreach (var sprite in sprites) Object.Destroy(sprite); Object.Destroy(texture);
            }
        }
    }
}
