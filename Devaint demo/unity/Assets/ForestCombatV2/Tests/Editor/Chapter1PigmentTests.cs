using System.Collections;
using System.Linq;
using Deviant.Combat;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    // Kills of a village run pay pigment into the profile once; the run's end settles the outcome once.
    public sealed class Chapter1PigmentTests
    {
        static ForestCombatWorld Bound(RunSession run)
        {
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            var binding = world ? world.GetComponent<Chapter1RunBinding>() : null;
            return world && world.Ready && binding && binding.Run == run ? world : null;
        }
        static TestEnemy Alive(ForestCombatWorld world, string role) => world.arena.enemies.FirstOrDefault(e => e && !e.Dead && Chapter1EnemyRole.Of(e) == role);

        [UnityTest] public IEnumerator KillsPayPigmentOnceAndTheClearOpensTheUnlocks()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            for (int frame = 0; frame < 600 && !(Object.FindAnyObjectByType<ForestCombatWorld>()?.Ready ?? false); frame++) yield return null;
            var store = new MemoryProfileStore();
            Chapter1ProfileHost.UseStore(store);
            var profile = Chapter1ProfileHost.Service;
            profile.MarkStorySeen(Chapter1FirstRunInkGuide.GuideBagStory); // the first-run ink guide has its own test
            var host = Chapter1RunHost.Ensure();

            host.StartNewRun(profile.PlanRun(new[] { "sword", "ranged" }));
            var run = host.Flow.Current; ForestCombatWorld world = null;
            for (float waitFrom = Time.realtimeSinceStartup; Time.realtimeSinceStartup - waitFrom < 20 && !(world = Bound(run));) yield return null;
            Assert.That(world, Is.Not.Null);
            var binding = world.GetComponent<Chapter1RunBinding>();
            foreach (var e in world.arena.enemies) if (e) { e.aggressive = false; e.CancelAttack(); }

            var normal = Alive(world, Chapter1EnemyRole.Normal);
            normal.health = 0;
            for (int frame = 0; frame < 60 && run.Kills < 1; frame++) yield return null;
            Assert.That(profile.Snapshot.Pigment, Is.EqualTo(1), "a normal kill pays 1 immediately");
            var effect = world.GetComponent<Chapter1PigmentPickupEffect>();
            Assert.That(effect, Is.Not.Null);
            Assert.That(effect.Shown, Is.EqualTo(1), "the pickup clip plays where the kill paid");
            // Review capture (not a pixel test): the clip over the defeated enemy at 960x540.
            normal.transform.position = world.arena.Player.Position + Vector2.right * 2; Physics2D.SyncTransforms();
            effect.transform.GetChild(effect.transform.childCount - 1).position = (Vector2)normal.transform.position + Vector2.up * .8f;
            world.RefreshCamera(0, true);
            var view = world.View;
            var clip = effect.transform.GetChild(effect.transform.childCount - 1).GetComponent<SpriteRenderer>();
            Assert.That(clip.sprite.name, Is.EqualTo("pigment-palette-flat"), "the pickup uses the simplified village palette symbol");
            Assert.That(clip.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(clip.sprite.pixelsPerUnit, Is.EqualTo(16));
            Assert.That(clip.transform.localScale, Is.EqualTo(Vector3.one));
            var rt = RenderTexture.GetTemporary(960, 540, 24); var target = view.targetTexture; var rect = view.rect;
            view.targetTexture = rt; view.rect = new Rect(0, 0, 1, 1); view.Render(); RenderTexture.active = rt;
            var shot = new Texture2D(960, 540, TextureFormat.RGB24, false); shot.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            RenderTexture.active = null; view.targetTexture = target; view.rect = rect; RenderTexture.ReleaseTemporary(rt);
            System.IO.Directory.CreateDirectory("../outputs/hub/capture");
            System.IO.File.WriteAllBytes("../outputs/hub/capture/pigment-pickup.png", shot.EncodeToPNG()); Object.Destroy(shot);
            float shownAt = Time.realtimeSinceStartup;
            while (effect.ActiveCount > 0 && Time.realtimeSinceStartup - shownAt < 3) yield return null;
            Assert.That(effect.ActiveCount, Is.Zero, "the floating palette and amount end by themselves");
            Alive(world, Chapter1EnemyRole.Elite).health = 0;
            for (int frame = 0; frame < 60 && run.Kills < 2; frame++) yield return null;
            Assert.That(profile.Snapshot.Pigment, Is.EqualTo(11), "an elite pays 10");
            Assert.That(binding.PigmentEarned, Is.EqualTo(11));

            // A development reset revives the same enemies; killing them again pays nothing more.
            world.arena.ResetTest();
            for (int frame = 0; frame < 5; frame++) yield return null;
            foreach (var e in world.arena.enemies) if (e) { e.aggressive = false; e.CancelAttack(); }
            normal.health = 0;
            for (int frame = 0; frame < 60 && run.Kills < 3; frame++) yield return null;
            Assert.That(run.Kills, Is.EqualTo(3), "the kill still counts for the run");
            Assert.That(profile.Snapshot.Pigment, Is.EqualTo(11), "but the revived enemy pays no pigment");
            Assert.That(profile.Snapshot.BossFirstClear, Is.False);

            run.MarkBossDefeated();
            Assert.That(binding.TryClearChapter(), Is.True);
            for (int frame = 0; frame < 10; frame++) yield return null;
            Assert.That(binding.Settled, Is.True);
            Assert.That(profile.Snapshot.BossFirstClear, Is.True, "the first clear opens the village unlocks");
            Assert.That(profile.Snapshot.RunsCleared, Is.EqualTo(1));
            Assert.That(profile.Snapshot.Chapter1Clears, Is.EqualTo(1), "a cleared run passed chapter 1");
            Assert.That(profile.HasUnsavedPigment, Is.False, "pigment is written at the end of the run");
            var saved = store.Load(_ => null, out _);
            Assert.That(saved.pigment, Is.EqualTo(11)); Assert.That(saved.chapter1BossFirstClear, Is.True);

            // A training plan never pays or settles.
            host.StartNewRun(profile.PlanRun(new[] { "sword", "blunt" }, training: true));
            run = host.Flow.Current; world = null;
            for (float waitFrom = Time.realtimeSinceStartup; Time.realtimeSinceStartup - waitFrom < 20 && !(world = Bound(run));) yield return null;
            foreach (var e in world.arena.enemies) if (e) { e.aggressive = false; e.CancelAttack(); }
            Alive(world, Chapter1EnemyRole.Elite).health = 0;
            for (int frame = 0; frame < 60 && run.Kills < 1; frame++) yield return null;
            Assert.That(profile.Snapshot.Pigment, Is.EqualTo(11));
            Assert.That(world.GetComponent<Chapter1PigmentPickupEffect>().Shown, Is.Zero, "training runs show no pigment pickup");
            world.arena.Player.health = 0;
            for (int frame = 0; frame < 600 && !run.Ended; frame++) yield return null;
            Assert.That(profile.Snapshot.RunsDied, Is.Zero);
            Chapter1ProfileHost.UseStore(null);
            yield return new ExitPlayMode();
        }
    }
}
