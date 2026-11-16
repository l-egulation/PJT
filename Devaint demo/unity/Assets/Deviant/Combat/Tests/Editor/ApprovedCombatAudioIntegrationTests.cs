using System;
using System.Collections;
using System.IO;
using Deviant.Combat.Contracts;
using Deviant.Integration;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public sealed class ApprovedCombatAudioIntegrationTests
    {
        [UnityTest] public IEnumerator GameUsesSelectedAttacksAndActualExplosionEvents() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabUsesSelectedAttacksAndActualExplosionEvents() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTest] public IEnumerator AcceptedDashStartsSelectedClipOnce()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var arena = AirborneSelectedAudioArena("Selected dash");
            using (var driver = arena.AcquireManualSimulation())
                AssertPressPlaysSelectedClipOnce(arena, driver, CombatButtons.Dash, "sfx_player_dash");
        }
        [UnityTest] public IEnumerator AcceptedFirstAndSecondJumpsEachStartTheSharedJumpClipOnce()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            var arena = AirborneSelectedAudioArena("Selected jump");
            // Both jumps keep their own ids (and the air jump its puff) but share one cloth flick.
            Assert.That(SelectedAudioClips.Find("sfx_player_double_jump"), Is.EqualTo(SelectedAudioClips.Find("sfx_player_jump")));
            using (var driver = arena.AcquireManualSimulation())
            {
                AssertPressPlaysSelectedClipOnce(arena, driver, CombatButtons.Jump, "sfx_player_jump");
                AssertPressPlaysSelectedClipOnce(arena, driver, CombatButtons.Jump, "sfx_player_double_jump");
            }
        }
        [UnityTest] public IEnumerator HealPlaysTheHealClipOnceAndSharesItAcrossBackToBackHeals()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            // The body lives in a static helper: a lambda capture in this iterator would allocate its closure before
            // the play-mode domain reload and come back null.
            AssertHealSoundPlaysOnceAndThrottles(AirborneSelectedAudioArena("Selected heal"));
        }
        static void AssertHealSoundPlaysOnceAndThrottles(CombatArena arena)
        {
            var player = arena.Player;
            var clip = SelectedAudioClips.Find("sfx_player_heal");
            Assert.That(clip, Is.Not.Null);
            using (var driver = arena.AcquireManualSimulation())
            {
                driver.Step(.02f, default);
                player.health = player.maxHealth;
                int before = arena.Audio.SelectedPlayCount;
                Assert.That(player.Heal(5), Is.Zero, "A full bar restores nothing");
                Assert.That(arena.Audio.SelectedPlayCount, Is.EqualTo(before), "No heal sound when health does not rise");

                player.health = player.maxHealth - 10;
                Assert.That(player.Heal(4), Is.EqualTo(4).Within(1e-4f));
                driver.Step(.02f, default);
                arena.Audio.Tick();
                Assert.That(arena.Audio.SelectedPlayCount, Is.EqualTo(before + 1));
                Assert.That(arena.Audio.LastSelectedId, Is.EqualTo("sfx_player_heal"));
                var output = Array.Find(arena.GetComponentsInChildren<AudioCueOutputBinding>(true), item => item.Source && item.Source.clip == clip);
                Assert.That(output, Is.Not.Null);
                Assert.That(output.Source.isPlaying, Is.True);

                Assert.That(player.Heal(100), Is.EqualTo(6).Within(1e-4f), "Heal clamps to the maximum");
                Assert.That(arena.Audio.SelectedPlayCount, Is.EqualTo(before + 1), "A second heal inside the interval shares the sound");

                player.health = 1;
                for (float t = 0; t <= CombatPlayer.HealSoundInterval; t += .02f) driver.Step(.02f, default);
                player.Heal(1);
                Assert.That(arena.Audio.SelectedPlayCount, Is.EqualTo(before + 2), "After the interval the next heal sounds again");
            }
        }
        static CombatArena AirborneSelectedAudioArena(string name)
        {
            var arena = new GameObject(name + " test").AddComponent<CombatArena>();
            var view = new GameObject(name + " view");
            view.AddComponent<AudioListener>();
            var content = CombatContentLoader.Load(null, false);
            content.Profile = CombatCompositionProfile.Game;
            content.Camera = view.AddComponent<Camera>();
            content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
            content.Enemies = Array.Empty<CombatEnemySpawn>();
            content.PlayerSize = new Vector2(.9375f, 2);
            content.PlayerOffset = new Vector2(.1875f, 1);
            content.PlayerSpawn = new Vector2(0, 100);
            Assert.That(arena.Initialize(content), Is.True);
            return arena;
        }
        static void AssertPressPlaysSelectedClipOnce(CombatArena arena, CombatArena.ManualSimulation driver, CombatButtons button, string id)
        {
            var clip = SelectedAudioClips.Find(id);
            Assert.That(clip, Is.Not.Null, id);
            int before = arena.Audio.SelectedPlayCount;
            driver.Step(.02f, new CombatInput(0, button));
            driver.Step(.02f, default);
            arena.Audio.Tick();
            var output = Array.Find(arena.GetComponentsInChildren<AudioCueOutputBinding>(true),
                item => item.Source && item.Source.clip == clip);
            Assert.That(arena.Audio.SelectedPlayCount, Is.EqualTo(before + 1), id);
            Assert.That(arena.Audio.LastSelectedId, Is.EqualTo(id));
            Assert.That(output, Is.Not.Null, id);
            Assert.That(output.Source.isPlaying, Is.True, id);
        }
        [UnityTest] public IEnumerator ChargedGreatswordStaysHeldAndPlaysWhenReleased()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            using(var driver = arena.AcquireManualSimulation())
            {
                driver.Reset();foreach(var e in arena.enemies){e.aggressive=false;e.Position=new Vector2(40,0);}
                arena.Player.selected=0;
                Assert.That(arena.Player.TryReconfigure(0,Array.FindIndex(arena.catalog.forms,f=>f.id=="greatsword"),0,out var error),Is.True,error);
                arena.Player.Position=Vector2.zero;arena.Player.SyncFacing(1);arena.enemies[0].Position=new Vector2(3,0);Physics2D.SyncTransforms();
                driver.Step(.02f,new CombatInput(0,CombatButtons.Special,true));
                for(int i=0;i<18;i++){driver.Step(.02f,new CombatInput(0,CombatButtons.None,true));arena.Audio.Tick();}
                Assert.That(arena.Audio.LastEventId,Is.EqualTo("greatsword_default"));
                var output=Array.Find(arena.GetComponentsInChildren<AudioCueOutputBinding>(true),item=>item.IsAvailable);
                Assert.That(output,Is.Not.Null);Assert.That(output.Source.isPlaying,Is.False,"Held charge has no premature release sound");
                driver.Step(.02f,default);arena.Audio.Tick();
                Assert.That(output.Source.isPlaying,Is.True,"The prepared output must advance as soon as charge releases");
                Assert.That(arena.Audio.LastError,Is.Null);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator MissedWhipMarkDoesNotPlayDelayedTightening()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            using(var driver = arena.AcquireManualSimulation())
            {
                driver.Reset(); foreach(var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(40,0); }
                arena.Player.selected = 0;
                Assert.That(arena.Player.TryReconfigure(0,Array.FindIndex(arena.catalog.forms,f=>f.id=="whip"),3,out var error),Is.True,error);
                arena.Player.Position=Vector2.zero;arena.Player.SyncFacing(1);
                arena.enemies[0].Position=new Vector2(3,0);Physics2D.SyncTransforms();
                driver.Step(.02f,new CombatInput(0,CombatButtons.Special));
                Assert.That(arena.Audio.LastEventId,Is.EqualTo("whip_follow_up"));
                arena.enemies[0].Position=new Vector2(40,0);Physics2D.SyncTransforms();
                for(int i=0;i<35;i++) { driver.Step(.02f,default); arena.Audio.Tick(); }
                Assert.That(arena.Audio.ActiveCount,Is.Zero,"Missed mark has no delayed effect tail");
                Assert.That(arena.Audio.LastError,Is.Null);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator OrdinaryRenderGapsKeepDspRunningBetweenFixedSteps()
        {
            LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            foreach (var enemy in arena.enemies) enemy.aggressive = false;
            float started = arena.Now;
            Assert.That(arena.Audio.Emit("explosion_bomb", Vector2.zero), Is.True);
            for (int i = 0; i < 10 && arena.Now <= started; i++) yield return new WaitForFixedUpdate();
            Assert.That(arena.Now, Is.GreaterThan(started), "The automatic arena must advance its fixed clock");
            arena.Audio.Tick();
            var output = Array.Find(arena.GetComponentsInChildren<AudioCueOutputBinding>(true), item => item.IsAvailable);
            Assert.That(output, Is.Not.Null);
            Assert.That(output.Source.isPlaying, Is.True, "now="+arena.Now+" paused="+arena.Controls.Current.SimulationPaused+" manual="+arena.ManualSimulationActive);
            arena.Audio.Tick(); arena.Audio.Tick();
            Assert.That(output.Source.isPlaying, Is.True, "Normal render frames with unchanged fixed time must not stutter audio");
            arena.Audio.Reset();
        }
        [UnityTest] public IEnumerator ActualApprovedBodiesRenderWithTheirAttackAudio()
        {
            LegacySceneFixture.Open("Assets/Deviant/Game/Scenes/deviant.unity");
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = true;
            yield return new EnterPlayMode(); yield return null; yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../outputs/runtime-sfx/captures")); Directory.CreateDirectory(output);
            using (var driver = arena.AcquireManualSimulation())
                foreach (string form in new[] { "base_sword", "whip", "rapier", "dagger", "greatsword", "shuriken" })
                {
                    driver.Reset(); foreach (var e in arena.enemies) { e.aggressive = false; e.Position = new Vector2(40, 0); }
                    int index = Array.FindIndex(arena.catalog.forms, f => f.id == form);
                    arena.Player.selected = 0; arena.Player.Reconfigure(0, index, 0); arena.Player.Position = Vector2.zero;
                    driver.Step(.01f, new CombatInput(0, CombatButtons.Basic));
                    for (int i = 0; i < Mathf.CeilToInt(arena.catalog.forms[index].basics[0].impact / .01f); i++) driver.Step(.01f, default);
                    arena.Presentation.Draw(.01f); arena.Audio.Tick();
                    Assert.That(arena.Player.visual.sprite, Is.Not.Null, form+" actual approved sprite");
                    Assert.That(arena.Audio.LastEventId, Is.EqualTo(form+"_basic_0"));
                    Assert.That(arena.Audio.LastError, Is.Null);
                    var camera = arena.ViewCamera; var previous = camera.targetTexture; var active = RenderTexture.active;
                    var target = RenderTexture.GetTemporary(960, 540, 24); var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
                    try
                    {
                        camera.orthographicSize = 4; camera.transform.position = new Vector3(arena.Player.Center.x, arena.Player.Center.y+1, -10);
                        camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                        image.ReadPixels(new Rect(0,0,960,540),0,0); image.Apply(); File.WriteAllBytes(Path.Combine(output,form+".png"),image.EncodeToPNG());
                    }
                    finally { camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); Object.Destroy(image); }
                }
            yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static IEnumerator Exercise(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null; yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            Assert.That(arena.Audio, Is.Not.Null);
            using (var driver = arena.AcquireManualSimulation())
            {
                // Every weapon's first basic attack plays its own selected swing "sfx_weapon_<form>_basic_0" (2026-11-13).
                foreach (string form in new[] { "base_sword", "whip", "rapier", "dagger", "greatsword", "shuriken", "hammer", "shield", "launcher", "bomb", "unarmed" })
                {
                    int formIndex = Array.FindIndex(arena.catalog.forms, f => f.id == form);
                    if (formIndex < 0) continue; // a scene whose catalog lacks the weapon
                    driver.Reset(); foreach (var e in arena.enemies) { e.aggressive = false; e.Position = new Vector2(40, 0); }
                    arena.Player.selected = 0; arena.Player.Reconfigure(0, formIndex, 0);
                    arena.Player.Position = Vector2.zero; arena.Player.SyncFacing(1); Physics2D.SyncTransforms();
                    int before = arena.Audio.AttackPlayCount;
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); arena.Audio.Tick();
                    Assert.That(arena.Audio.AttackPlayCount, Is.EqualTo(before + 1), form);
                    Assert.That(arena.Audio.LastEventId, Is.EqualTo(form + "_basic_0"));
                    Assert.That(arena.Audio.LiveOutputCount, Is.EqualTo(1));
                    var output = Array.Find(arena.GetComponentsInChildren<AudioCueOutputBinding>(true), item => item.IsAvailable);
                    Assert.That(output, Is.Not.Null, form);
                    var selected = SelectedAudioClips.Find("sfx_weapon_" + form + "_basic_0");
                    Assert.That(selected, Is.Not.Null, form + " has its selected swing");
                    Assert.That(output.Source.clip, Is.SameAs(selected), form);
                    Assert.That(output.Source.clip.frequency, Is.EqualTo(48000));
                    int position = output.Source.timeSamples;
                    arena.Audio.Tick(); Assert.That(output.Source.timeSamples, Is.EqualTo(position));
                    Assert.That(output.Source.isPlaying, Is.False, "Equal simulation time pauses output");
                    arena.Player.currentCast.Cancel(); arena.Audio.Tick();
                    Assert.That(arena.Audio.ActiveCount, Is.Zero); Assert.That(arena.Audio.LiveOutputCount, Is.Zero);
                    for (int i = 0; i < 45; i++) { driver.Step(.02f, default); arena.Audio.Tick(); }
                    Assert.That(arena.Audio.AttackPlayCount, Is.EqualTo(before + 1), "Audio must not queue following combo attacks");
                    Assert.That(arena.Audio.LastError, Is.Null);
                }
                foreach (var test in new[] { (radius: .6f, grenade: true, id: "explosion_fragment"), (radius: 1.2f, grenade: true, id: "explosion_grenade"), (radius: 1.5f, grenade: false, id: "explosion_bomb"), (radius: 3f, grenade: false, id: "explosion_large") })
                {
                    driver.Reset(); int before = arena.Audio.EventPlayCount;
                    arena.BombExploded(Vector2.zero, test.radius, test.grenade); arena.Audio.Tick();
                    if (test.id == "explosion_fragment")
                    {
                        Assert.That(arena.Audio.EventPlayCount, Is.EqualTo(before), "Approved grenade cue already contains the successive bursts");
                        continue;
                    }
                    Assert.That(arena.Audio.EventPlayCount, Is.EqualTo(before + 1), "One sound per real explosion, without visual fallback duplication");
                    Assert.That(arena.Audio.LastEventId, Is.EqualTo(test.id));
                }
                if (scene.Contains("/Game/"))
                {
                    driver.Reset(); TestEnemy boar = null; MonoBehaviour skin = null;
                    foreach (var enemy in arena.enemies)
                        foreach (var component in enemy.GetComponents<MonoBehaviour>())
                            if (component.GetType().Name == "ForestEnemyPresentation" &&
                                component.GetType().GetField("data").GetValue(component) is object art &&
                                (string)art.GetType().GetField("species").GetValue(art) == "boar")
                            { boar = enemy; skin = component; }
                    Assert.That(skin, Is.Not.Null, "Common forest boar binding");
                    boar.aggressive = false;
                    // Monsters make no hurt or death sound of their own; the player's weapon owns the hit sound.
                    int count = arena.Audio.EventPlayCount;
                    Assert.That(boar.Receive(1, arena.Player, 1, true, null), Is.True);
                    skin.SendMessage("LateUpdate");
                    Assert.That(boar.Receive(10000, arena.Player, 1, true, null), Is.True);
                    skin.SendMessage("LateUpdate");
                    Assert.That(arena.Audio.EventPlayCount, Is.EqualTo(count), "No boar sound");
                }
                // An accepted player hit plays the weapon's hit sound, whatever monster was hit.
                foreach (var (form, hitId) in new[] { ("base_sword", "sfx_hit_sword"), ("hammer", "sfx_hit_hammer") })
                {
                    int formIndex = Array.FindIndex(arena.catalog.forms, f => f.id == form);
                    if (formIndex < 0 || arena.enemies.Count == 0) continue;
                    driver.Reset(); foreach (var e in arena.enemies) { e.aggressive = false; e.Position = new Vector2(40, 0); }
                    var target = arena.enemies[0]; target.Position = new Vector2(1.2f, 0); target.health = 100000;
                    arena.Player.selected = 0; arena.Player.Reconfigure(0, formIndex, 0);
                    arena.Player.Position = Vector2.zero; arena.Player.SyncFacing(1); Physics2D.SyncTransforms();
                    arena.Audio.Tick(); arena.Audio.Tick(); int hitsBefore = arena.Audio.HitPlayCount;
                    driver.Step(.02f, new CombatInput(0, CombatButtons.Basic)); arena.Audio.Tick();
                    for (int i = 0; i < 60 && arena.Audio.HitPlayCount == hitsBefore; i++) { driver.Step(.02f, default); arena.Audio.Tick(); }
                    Assert.That(arena.Audio.HitPlayCount, Is.GreaterThan(hitsBefore), form + " accepted hit sound");
                    Assert.That(arena.Audio.LastHitId, Is.EqualTo(hitId));
                }
                driver.Reset(); Assert.That(arena.Audio.ActiveCount, Is.Zero); Assert.That(arena.Audio.LiveOutputCount, Is.Zero);
                arena.gameObject.SetActive(false); Assert.That(arena.Audio.LiveOutputCount, Is.Zero);
            }
            yield return null;
        }
    }
}
