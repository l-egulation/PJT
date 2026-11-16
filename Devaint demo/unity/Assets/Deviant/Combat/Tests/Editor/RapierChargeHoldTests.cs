using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat.Contracts;
using Deviant.Integration;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class RapierChargeHoldTests
    {
        sealed class TestMedia : IDisposable
        {
            readonly Texture2D texture = new Texture2D(1, 1);
            readonly List<Sprite> created = new List<Sprite>();
            public Sprite Create(string name)
            {
                var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero, 32);
                sprite.name = name; created.Add(sprite); return sprite;
            }
            public void Dispose() { foreach (var sprite in created) Object.Destroy(sprite); Object.Destroy(texture); }
        }
        [Test] public void ChargePoseMediaLoadsAtTheExistingSpecialWorldScaleAndFootAnchor()
        {
            var catalog = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
            var set = catalog.sets.Single(value => value.form == "rapier");
            foreach (var clip in new[] { set.Find("charge_prepare"), set.Find("charge_hold") })
            foreach (string frame in clip.frames)
            {
                var sprite = Resources.Load<Sprite>(frame);
                Assert.That(sprite, Is.Not.Null, frame);
                Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(640, 360)), frame);
                Assert.That(sprite.pixelsPerUnit, Is.EqualTo(106.6667f).Within(.0001f), frame);
                Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point), frame);
                Assert.That(Vector2.Distance(sprite.pivot / sprite.pixelsPerUnit, new Vector2(2.0625f, .3125f)), Is.LessThan(.001f), frame);
            }
        }
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest] public IEnumerator HeldPoseLoopsUntilReleaseThenUsesTheWholeShiftedRecovery()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("RapierChargeArena").AddComponent<CombatArena>();
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            Assert.That(arena, Is.Not.Null);
            var content = CombatContentLoader.Load(null, false);
            content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
            content.Profile = CombatCompositionProfile.Game;
            content.Camera = new GameObject("RapierChargeCamera").AddComponent<Camera>();
            content.PlayerSpawn = new Vector2(0, 1000);
            content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = new Vector2(.1875f, 1);
            content.Enemies = new[] { new CombatEnemySpawn("RapierChargeTarget", 1, new Vector2(2, 1000), Vector2.one, new Vector2(0, 1), 100, true, false, 0) };
            content.PrepareEnvironment = _ => {
                var floor = new GameObject("RapierChargeFloor"); floor.transform.position = new Vector2(0, 999.5f);
                floor.AddComponent<ArenaSurface>(); floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
            };
            Assert.That(arena.Initialize(content), Is.True);
            Assert.That(arena.characterAnimations?.sets, Is.Not.Null);
            Assert.That(CombatDamageVariance.Settings, Is.Not.Null);
            float varianceMin = CombatDamageVariance.Settings.minimum, varianceMax = CombatDamageVariance.Settings.maximum;
            CombatDamageVariance.Settings.minimum = CombatDamageVariance.Settings.maximum = 1;
            // A local-function closure cannot survive EnterPlayMode's domain
            // reload in UnityTest iterators. Construct media after that boundary.
            var sprites = new TestMedia();
            // Keep the real generated action registry and body clips. Unique
            // stand-in sprites let the test observe exact presentation frames.
            foreach (var set in arena.characterAnimations.sets)
            {
                Assert.That(set.clips, Is.Not.Null, set.form);
                foreach (var clip in set.clips) {
                    Assert.That(clip.frames, Is.Not.Null, set.form + "/" + clip.name);
                    clip.sprites = clip.frames.Select(sprites.Create).ToArray();
                }
            }
            var media = arena.characterAnimations.sets.Single(set => set.form == "rapier");
            var prepare = media.Find("charge_prepare"); var hold = media.Find("charge_hold");
            var attack = media.Find("attack_charge");
            Assert.That(prepare.Duration, Is.LessThan(.2f));
            Assert.That(hold.Duration, Is.GreaterThan(0));
            try
            {
                using (var driver = arena.AcquireManualSimulation())
                foreach (var scenario in new[] { (release:0f, facing:1, cancel:false), (release:.5f, facing:-1, cancel:false),
                    (release:float.PositiveInfinity, facing:1, cancel:false), (release:float.PositiveInfinity, facing:-1, cancel:true) })
                {
                    driver.Reset(); var player = arena.Player;
                    player.SetWeaponSlotReady(0, true); player.SetWeaponSlotReady(1, true);
                    player.selected = 0; player.InputBlocked = false;
                    foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
                    foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
                    Assert.That(player.TryReconfigure(0, Array.FindIndex(arena.catalog.forms, f => f.id == "rapier"), 0, out var error), Is.True, error);
                    player.Position = new Vector2(0, 1000); player.velocity = Vector2.zero; player.grounded = true; player.SyncFacing(scenario.facing);
                    arena.enemies[0].Position = new Vector2(2 * scenario.facing, 1000); Physics2D.SyncTransforms();
                    CombatCast cast = null; bool sawHold = false, sawReleasedBody = false, sawLateRecovery = false;
                    var heldFrames = (Sprite[])hold.sprites.Clone();
                    for (int tick = 0; tick < 160; tick++)
                    {
                        float age = cast ? cast.Age : 0;
                        var buttons = tick == 0 ? CombatButtons.Weapon1Special : scenario.cancel && tick == 20 ? CombatButtons.Dash : CombatButtons.None;
                        driver.Step(.01f, new CombatInput(pressed: buttons,
                            heldSpecials: age < scenario.release ? CombatButtons.Weapon1Special : CombatButtons.None));
                        if (tick == 0) { cast = player.currentCast; Assert.That(cast, Is.Not.Null); }
                        arena.Presentation.Draw(0);
                        Assert.That(arena.Presentation.LastAttackMotionError, Is.Null);
                        if (!ReferenceEquals(player.currentCast, cast)) break;
                        bool held = !cast.ChargeRelease.HasValue || cast.Age < cast.ImpactTime;
                        Sprite expected;
                        if (held)
                        {
                            expected = cast.Age < prepare.Duration ? prepare.At(cast.Age, false)
                                : heldFrames[hold.FrameIndex(cast.Age - prepare.Duration, true)];
                            Assert.That(arena.enemies[0].ReceivedHitCount, Is.Zero, "No hit during charge preparation/hold");
                            sawHold |= cast.Age >= prepare.Duration;
                        }
                        else
                        {
                            expected = attack.Attack(cast.Age, cast.ImpactTime, cast.ChargeRelease.Value.MotionEnd);
                            sawReleasedBody = true; sawLateRecovery |= cast.Age > cast.MotionDuration;
                        }
                        Assert.That(player.visual.sprite, Is.SameAs(expected), "age=" + cast.Age + " held=" + held);
                        Assert.That(player.visual.flipX, Is.EqualTo(scenario.facing < 0));
                        // An execution pins its held artwork even after an edit.
                        if (tick == 15) {
                            var edited = new Sprite[hold.sprites.Length];
                            for (int i = 0; i < edited.Length; i++) edited[i] = sprites.Create("edited-hold");
                            hold.sprites = edited;
                        }
                    }
                    hold.sprites = heldFrames;
                    Assert.That(sawHold, Is.True);
                    if (scenario.cancel)
                    {
                        Assert.That(cast.ChargeRelease, Is.Null);
                        Assert.That(arena.enemies[0].ReceivedHitCount, Is.Zero);
                        Assert.That(player.Weapon.Cooldown.Available, Is.EqualTo(1));
                    }
                    else
                    {
                        Assert.That(sawReleasedBody, Is.True);
                        Assert.That(sawLateRecovery, Is.EqualTo(scenario.release > .2f));
                        Assert.That(arena.enemies[0].ReceivedHitCount, Is.EqualTo(1));
                        Assert.That(arena.enemies[0].health, Is.EqualTo(100 - cast.ChargeRelease.Value.Damage).Within(.0001f));
                        Assert.That(cast.ChargeRelease.Value.ReleaseTime, Is.InRange(.2f, .81f));
                    }
                    Assert.That(player.currentCast, Is.Null);
                    Assert.That(cast.ChargeActive || cast.ResourcesActive, Is.False);
                }
            }
            finally {
                CombatDamageVariance.Settings.minimum = varianceMin; CombatDamageVariance.Settings.maximum = varianceMax;
                sprites.Dispose();
            }
        }
    }
}
