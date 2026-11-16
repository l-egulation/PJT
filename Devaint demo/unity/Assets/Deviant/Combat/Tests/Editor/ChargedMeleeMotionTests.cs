using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class ChargedMeleeMotionTests
    {
        const float Step = .02f;
        [Serializable] sealed class Row
        {
            public string scenario, form, clip, action;
            public int tick, frame, now, age, impact, duration, end, started, x, y, vx, vy, hp, hits, available, ready;
            public bool released, chargeActive, resourcesActive, spent, flip;
        }
        [Serializable] sealed class Trace { public List<Row> rows = new List<Row>(); }
        sealed class Media : IDisposable
        {
            readonly Texture2D texture = new Texture2D(1, 1);
            readonly List<Sprite> created = new List<Sprite>();
            public Sprite[] Sprites(params string[] names)
            {
                var sprites = new Sprite[names.Length];
                for (int i = 0; i < sprites.Length; i++)
                {
                    sprites[i] = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero, 32);
                    sprites[i].name = names[i]; created.Add(sprites[i]);
                }
                return sprites;
            }
            public void Dispose() { foreach (var sprite in created) Object.Destroy(sprite); Object.Destroy(texture); }
        }
        [UnityTest] public IEnumerator GameUneditedChargedBodiesKeepHoldReleaseAndFallbackFrames() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game", false);
        [UnityTest] public IEnumerator LabUneditedChargedBodiesKeepHoldReleaseAndFallbackFrames() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab", false);
        [UnityTest] public IEnumerator GameChargedBodiesPinStartBeforeFirstDrawAndKeepCombat() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", "game", true);
        [UnityTest] public IEnumerator LabChargedBodiesPinStartBeforeFirstDrawAndKeepCombat() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", "lab", true);
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        static CharacterClip Body(Sprite[] sprites, string name = "attack1") => new CharacterClip {
            name = name, frames = new[] { "windup", "contact", "recovery", "finish" }, seconds = new[] { .2f, .2f, .2f, .4f },
            contact = 1, sourceImpact = -1, fxStart = 0, sprites = new[] { sprites[0], sprites[1], sprites[2], sprites[3] }
        };
        static CharacterAnimationCatalog Catalog(string form, Sprite[] sprites, bool idleOnly = false)
        {
            var body = Body(sprites, idleOnly ? "idle" : "attack1");
            var clips = idleOnly ? new[] { body } : new[] { body,
                new CharacterClip { name = "idle", frames = new[] { "idle" }, seconds = new[] { 1f }, sprites = new[] { sprites[0] } },
                new CharacterClip { name = "dash", frames = new[] { "dash" }, seconds = new[] { .2f }, sprites = new[] { sprites[4] } } };
            var effect = new CharacterEffect { attack = "attack1", frames = new[] { "basic-fx" }, seconds = new[] { .1f }, x = new[] { 0f }, y = new[] { 1f }, sprites = new[] { sprites[1] } };
            return new CharacterAnimationCatalog { sets = new[] { new CharacterAnimationSet { form = form, clips = clips, effects = new[] { effect } } } };
        }
        static CharacterClip Copy(CharacterClip clip) => new CharacterClip {
            name = clip.name, frames = (string[])clip.frames.Clone(), seconds = (float[])clip.seconds.Clone(), contact = clip.contact,
            sourceImpact = clip.sourceImpact, fxStart = clip.fxStart, sprites = (Sprite[])clip.sprites.Clone()
        };
        static void Edit(CharacterAnimationCatalog catalog, Sprite edited)
        {
            var body = catalog.sets[0].clips[0];
            body.frames[0] = "edited-windup"; body.seconds[0] = 2; body.contact = 0; body.sourceImpact = 1.4f;
            for (int i = 0; i < body.sprites.Length; i++) body.sprites[i] = edited;
            catalog.sets[0].effects[0].sprites[0] = edited;
        }
        static void Setup(CombatArena arena, CombatArena.ManualSimulation driver, string form)
        {
            driver.Reset(); var player = arena.Player; player.selected = 0; player.InputBlocked = false;
            foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
            foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
            Assert.That(player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == form), 0, out var error), Is.True, error);
            player.Position = Vector2.zero; player.velocity = Vector2.zero; player.grounded = true; player.SyncFacing(1);
            arena.enemies[0].Position = new Vector2(2, 0); Physics2D.SyncTransforms();
        }
        static CombatCast Start(CombatArena arena, CombatArena.ManualSimulation driver)
        {
            driver.Step(Step, new CombatInput(0, CombatButtons.Special, true));
            var cast = arena.Player.currentCast;
            Assert.That(cast, Is.Not.Null); Assert.That(cast.ChargedMelee, Is.Not.Null);
            return cast;
        }
        static void NoCue(CombatArena arena)
        {
            Assert.That(arena.Presentation.LastEffectHandle, Is.Null); Assert.That(arena.Presentation.ActiveEffects, Is.Zero);
        }
        static void DrawBody(CombatArena arena, CombatCast cast, CharacterClip reference, string form)
        {
            arena.Presentation.Draw(0);
            Sprite expected;
            if (reference?.name == "attack_charge_slam")
            {
                var timing = new AttackMotionTiming(form + "/" + reference.name, reference.frames, reference.seconds,
                    reference.contact, reference.sourceImpact, -1, cast.ChargePolicy.Minimum, cast.ChargePolicy.BaseEnd);
                expected = reference.sprites[timing.ChargeSlamFrameAt(cast.Age, cast.ImpactTime, cast.ChargePolicy, !cast.ChargeRelease.HasValue)];
            }
            else expected = reference?.Attack(cast.Age, cast.ImpactTime, cast.MotionDuration);
            Assert.That(arena.Player.visual.sprite, Is.SameAs(expected));
            Assert.That(arena.Presentation.CurrentClip, Is.EqualTo(reference?.name ?? "attack1")); Assert.That(arena.Presentation.CurrentForm, Is.EqualTo(form));
            NoCue(arena);
        }
        static void Completed(CombatArena arena, CombatCast cast)
        {
            Assert.That(arena.Player.currentCast, Is.Null); Assert.That(cast.ChargeRelease.HasValue, Is.True);
            Assert.That(cast.MotionDuration, Is.EqualTo(cast.ChargePolicy.BaseEnd));
            Assert.That(cast.EndTime - cast.StartedAt, Is.EqualTo(cast.ChargeRelease.Value.MotionEnd).Within(.000001f));
            Assert.That(arena.enemies[0].health, Is.EqualTo(100 - cast.ChargeRelease.Value.Damage).Within(.00001f));
            Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(1)); Assert.That(cast.ResourceResult.Status, Is.EqualTo(ActionResourceStatus.Succeeded));
            Assert.That(cast.ResourceResult.Snapshot.Key.IsBasic, Is.False); Assert.That(arena.Player.Weapon.Cooldown.Available, Is.Zero);
            Assert.That(cast.ChargeActive || cast.ResourcesActive || arena.SimulationFaulted, Is.False); NoCue(arena);
        }
        static void Finish(CombatArena arena, CombatArena.ManualSimulation driver, CombatCast cast, CharacterClip reference, string form)
        {
            float releaseAt = (cast.ChargePolicy.Minimum + cast.ChargePolicy.BaseEnd) * .5f;
            for (int tick = 0; tick < 100 && ReferenceEquals(arena.Player.currentCast, cast); tick++)
            {
                driver.Step(Step, new CombatInput(0, CombatButtons.None, cast.Age < releaseAt));
                if (ReferenceEquals(arena.Player.currentCast, cast)) DrawBody(arena, cast, reference, form);
            }
            Completed(arena, cast);
        }
        static void Unedited(CombatArena arena, CombatArena.ManualSimulation driver, Media media, Sprite[] sprites, Trace trace)
        {
            foreach (string form in new[] { "rapier", "greatsword" })
            foreach (string binding in new[] { "catalog", "dedicated", "base-fallback", "idle-fallback" })
            foreach (string release in new[] { "early", "middle", "maximum" })
            {
                Setup(arena, driver, form);
                CharacterAnimationCatalog catalog; CharacterClip reference; string displayedForm;
                if (binding == "catalog")
                {
                    catalog = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
                    var set = Array.Find(catalog.sets, s => s.form == form) ?? Array.Find(catalog.sets, s => s.form == "base_sword");
                    // This fixture deliberately exercises legacy attack1 fallback.
                    // The generated rapier charge loop has its own phase test.
                    set.clips = Array.FindAll(set.clips, c => c.name != "charge_prepare" && c.name != "charge_hold");
                    displayedForm = set.form; reference = set.Find("attack_charge_slam") ?? set.Find("attack1") ?? set.Find("idle"); reference.sprites = media.Sprites(reference.frames);
                }
                else
                {
                    displayedForm = binding == "base-fallback" ? "base_sword" : form;
                    catalog = Catalog(displayedForm, sprites, binding == "idle-fallback"); reference = Copy(catalog.sets[0].clips[0]);
                }
                arena.characterAnimations = catalog;
                var policy = arena.GetChargedMelee(form + "_default").Policy;
                float releaseAt = release == "early" ? 0 : release == "middle" ? (policy.Minimum + policy.BaseEnd) * .5f : policy.HoldUntilRelease ? policy.Maximum + 2 * Step : float.PositiveInfinity;
                CombatCast cast = null; bool wasReleased = false, sawClampedHold = false, sawReleaseRewind = false; Sprite previous = null;
                for (int tick = 0; tick < 100; tick++)
                {
                    driver.Step(Step, new CombatInput(0, tick == 0 ? CombatButtons.Special : CombatButtons.None, arena.Now < releaseAt));
                    if (tick == 0) { cast = arena.Player.currentCast; Assert.That(cast, Is.Not.Null); }
                    bool active = ReferenceEquals(arena.Player.currentCast, cast);
                    if (active)
                    {
                        DrawBody(arena, cast, reference, displayedForm);
                        if (binding != "catalog")
                        {
                            if (tick == 0) Assert.That(arena.Player.visual.sprite, Is.SameAs(sprites[0]));
                            if (!cast.ChargeRelease.HasValue && cast.Age > policy.BaseEnd + Step)
                            { Assert.That(arena.Player.visual.sprite, Is.SameAs(sprites[3]), "The existing held body clamps; it does not invent a charge loop."); sawClampedHold = true; }
                            if (!wasReleased && cast.ChargeRelease.HasValue && release == "middle")
                            {
                                Assert.That(previous, Is.SameAs(sprites[3])); Assert.That(arena.Player.visual.sprite, Is.SameAs(sprites[policy.ReleaseWindup > 0 ? 0 : 1]), "Release remaps the same body back toward contact."); sawReleaseRewind = true;
                            }
                            if (cast.ChargeRelease.HasValue && release == "maximum")
                            { Assert.That(cast.ImpactTime, Is.GreaterThan(cast.MotionDuration)); Assert.That(arena.Player.visual.sprite, Is.SameAs(sprites[3])); }
                        }
                    }
                    else arena.Presentation.Draw(0);
                    NoCue(arena); previous = arena.Player.visual.sprite; wasReleased = cast.ChargeRelease.HasValue;
                    trace.rows.Add(new Row {
                        scenario = form + "/" + binding + "/" + release, tick = tick, frame = Array.IndexOf(reference.sprites, arena.Player.visual.sprite),
                        form = arena.Presentation.CurrentForm, clip = arena.Presentation.CurrentClip, action = active ? cast.Definition.Id : "",
                        now = Bits(arena.Now), age = Bits(cast.Age), impact = Bits(cast.ImpactTime), duration = Bits(cast.MotionDuration), end = Bits(cast.EndTime), started = Bits(cast.StartedAt),
                        x = Bits(arena.Player.Position.x), y = Bits(arena.Player.Position.y), vx = Bits(arena.Player.velocity.x), vy = Bits(arena.Player.velocity.y),
                        hp = Bits(arena.enemies[0].health), hits = arena.enemies[0].ReceivedHitCount, available = arena.Player.Weapon.Cooldown.Available, ready = Bits(arena.Player.Weapon.Cooldown.NextReady),
                        released = cast.ChargeRelease.HasValue, chargeActive = cast.ChargeActive, resourcesActive = cast.ResourcesActive, spent = cast.ResourceResult != null, flip = arena.Player.visual.flipX
                    });
                }
                Completed(arena, cast);
                if (release == "early") { Assert.That(cast.ImpactTime, Is.EqualTo(policy.Minimum + policy.ReleaseWindup)); Assert.That(cast.ChargeRelease.Value.Damage, Is.EqualTo(policy.MinimumDamage)); }
                if (release == "maximum") { Assert.That(cast.ImpactTime, Is.EqualTo(cast.ChargeRelease.Value.ReleaseTime + policy.ReleaseWindup).Within(.000001f)); Assert.That(cast.ChargeRelease.Value.Damage, Is.EqualTo(policy.MaximumDamage)); }
                if (binding != "catalog" && release == "maximum") Assert.That(sawClampedHold, Is.True);
                if (binding != "catalog" && release == "middle") Assert.That(sawReleaseRewind, Is.True);
            }
        }
        static void Mutations(CombatArena arena, CombatArena.ManualSimulation driver, Sprite[] sprites)
        {
            foreach (string form in new[] { "rapier", "greatsword" })
            {
                foreach (string mutation in new[] { "edit", "remove-lists", "remove-catalog", "replace-fallback" })
                {
                    Setup(arena, driver, form); bool fallback = mutation == "replace-fallback";
                    string displayedForm = fallback ? "base_sword" : form;
                    var catalog = Catalog(displayedForm, sprites); var reference = Copy(catalog.sets[0].clips[0]); arena.characterAnimations = catalog;
                    var cast = Start(arena, driver); string version = arena.Presentation.CurrentAttackMotionVersion;
                    var next = Catalog(form, sprites); Edit(next, sprites[4]);
                    if (mutation == "edit") { Edit(catalog, sprites[4]); catalog.sets[0].form = "base_sword"; next = catalog; }
                    if (mutation == "remove-lists") { catalog.sets[0].clips = null; catalog.sets[0].effects = null; }
                    if (mutation == "remove-catalog") arena.characterAnimations = null;
                    if (fallback) arena.characterAnimations = next;
                    arena.CaptureCharacterAttack(cast); DrawBody(arena, cast, reference, displayedForm);
                    Assert.That(arena.Player.visual.sprite, Is.SameAs(sprites[0]), "Editing or removing media before the first Draw cannot retarget this body.");
                    Assert.That(version, Is.Not.Null.And.Not.Empty); Assert.That(arena.Presentation.LastAttackMotionError, Is.Null);
                    Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.EqualTo(version));
                    Finish(arena, driver, cast, reference, displayedForm);
                    Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.EqualTo(version));
                    for (int tick = 0; tick < 400 && arena.Player.Weapon.Cooldown.Available == 0; tick++) driver.Step(Step);
                    Assert.That(arena.Player.Weapon.Cooldown.Available, Is.EqualTo(1)); arena.characterAnimations = next;
                    var following = Start(arena, driver); arena.Presentation.Draw(0);
                    Assert.That(following.ExecutionId, Is.Not.EqualTo(cast.ExecutionId)); Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.Not.EqualTo(version));
                    Assert.That(arena.Player.visual.sprite, Is.SameAs(sprites[4])); Assert.That(arena.Presentation.CurrentForm, Is.EqualTo(next.sets[0].form)); NoCue(arena);
                }
                foreach (bool missingCatalog in new[] { true, false })
                {
                    Setup(arena, driver, form); var repaired = Catalog(form, sprites);
                    if (!missingCatalog) repaired.sets[0].clips[0].sprites = new[] { sprites[0] };
                    arena.characterAnimations = missingCatalog ? null : repaired;
                    var cast = Start(arena, driver); string error = arena.Presentation.LastAttackMotionError;
                    Assert.That(error, Is.Not.Null.And.Not.Empty); Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.Null);
                    repaired.sets[0].clips[0].sprites = Body(sprites).sprites; arena.characterAnimations = repaired;
                    arena.CaptureCharacterAttack(cast); DrawBody(arena, cast, null, form);
                    Assert.That(arena.Presentation.LastAttackMotionError, Is.EqualTo(error));
                    Finish(arena, driver, cast, null, form);
                    Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.Null); Assert.That(arena.Presentation.LastAttackMotionError, Is.EqualTo(error));
                }
                Setup(arena, driver, form); arena.characterAnimations = null; var cancelled = Start(arena, driver);
                arena.characterAnimations = Catalog(form, sprites); driver.Step(Step, new CombatInput(0, CombatButtons.Dash, true)); arena.Presentation.Draw(0);
                Assert.That(arena.Player.Dashing, Is.True); Assert.That(arena.Player.currentCast, Is.Null); Assert.That(arena.Presentation.CurrentClip, Is.EqualTo("dash"));
                Assert.That(arena.Player.visual.sprite, Is.SameAs(sprites[4])); Assert.That(cancelled.ChargeRelease, Is.Null); Assert.That(cancelled.ResourceResult, Is.Null);
                Assert.That(cancelled.ChargeActive || cancelled.ResourcesActive, Is.False); Assert.That(arena.Player.Weapon.Cooldown.Available, Is.EqualTo(1)); Assert.That(arena.enemies[0].ReceivedHitCount, Is.Zero); NoCue(arena);

                Setup(arena, driver, form); arena.characterAnimations = null; var reset = Start(arena, driver); driver.Reset();
                Assert.That(reset.ChargeActive || reset.ResourcesActive, Is.False); Assert.That(reset.ChargeRelease, Is.Null); Assert.That(reset.ResourceResult, Is.Null);
                Assert.That(arena.Player.currentCast, Is.Null); Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.Null); Assert.That(arena.Presentation.LastAttackMotionError, Is.Null);
                Assert.That(arena.Player.Weapon.Cooldown.Available, Is.EqualTo(1)); Assert.That(arena.enemies[0].health, Is.EqualTo(100)); NoCue(arena);
                Setup(arena, driver, form); arena.characterAnimations = Catalog(form, sprites); var restarted = Start(arena, driver);
                DrawBody(arena, restarted, Body(sprites), form); Assert.That(arena.Presentation.LastAttackMotionError, Is.Null); Assert.That(arena.Presentation.CurrentAttackMotionVersion, Is.Not.Null.And.Not.Empty);
            }
        }
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static IEnumerator Exercise(string scene, string label, bool mutations)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); Assert.That(Application.isPlaying, Is.True); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var original = arena.characterAnimations;
            var floor = new GameObject("ChargedMotionFloor"); floor.transform.position = new Vector3(0, -.5f, 0); floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1); Physics2D.SyncTransforms();
            try
            {
                using (var media = new Media())
                using (var driver = arena.AcquireManualSimulation())
                {
                    var sprites = media.Sprites("windup", "contact", "recovery", "finish", "edited");
                    if (mutations) Mutations(arena, driver, sprites);
                    else
                    {
                        var trace = new Trace(); Unedited(arena, driver, media, sprites, trace);
                        var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-chargedMotionTraceRoot");
                        if (at >= 0 && at + 1 < args.Length)
                        { Directory.CreateDirectory(args[at + 1]); File.WriteAllText(Path.Combine(args[at + 1], label + ".json"), JsonUtility.ToJson(trace)); }
                    }
                }
            }
            finally
            {
                if (arena) { arena.Presentation.Reset(); arena.characterAnimations = original; }
                Object.Destroy(floor);
            }
        }
    }
}
