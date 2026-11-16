using System.Collections.Generic;
using System.Reflection;
using Deviant.Combat;
using Deviant.Run;
using Deviant.Tutorial.Unity;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class FirstRunTutorialTriggerTests
    {
        GameObject host;
        MemoryProfileStore store;
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        [SetUp] public void Setup()
        {
            store = new MemoryProfileStore();
            Chapter1ProfileHost.UseStore(store);
        }
        [TearDown] public void Cleanup()
        {
            if (host) Object.DestroyImmediate(host);
            Chapter1ProfileHost.UseStore(null);
        }
        SketchRunBinding Binding(RunStartPlan plan)
        {
            host = new GameObject("first-run trigger fixture");
            host.SetActive(false);
            var arena = host.AddComponent<CombatArena>();
            var run = new RunFlow(() => 42).StartNewRun(0, plan: plan);
            run.Sketch = new SketchRunState(new Dictionary<string, string[]> {
                ["base_sword"] = new[] { "base_sword_default" } }, new[] { "red-test", "green-test" },
                id => id.StartsWith("red") ? "red" : "green", 42);
            var binding = SketchRunBinding.Attach(host, arena, run, 1);
            var player = new GameObject("fixture player").AddComponent<CombatPlayer>();
            player.transform.SetParent(host.transform, false);
            player.maxHealth = player.health = 100;
            typeof(CombatArena).GetProperty("Player").GetSetMethod(true).Invoke(arena, new object[] { player });
            return binding;
        }
        static void Attack(SketchRunBinding binding) =>
            typeof(SketchRunBinding).GetMethod("InitializeFirstRunTutorials", Hidden).Invoke(binding, new object[] { 1 });
        static FirstRunTutorialWindow Window(SketchRunBinding binding) =>
            binding.GetComponentInChildren<FirstRunTutorialWindow>(true);

        [Test] public void SavedPrefabAndRuntimeWindowsReuseOnePixelFrameAcrossPages()
        {
            var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Deviant/Tutorial/Prefabs/FirstRunTutorialWindow.prefab");
            Assert.That(prefab,Is.Not.Null);
            host=Object.Instantiate(prefab);
            var view=host.GetComponent<FirstRunTutorialWindow>();
            for(int i=0;i<3;i++) { view.ShowAttack();view.ShowRewards();view.Dismiss(); }
            view.ShowRewards();
            var frames=System.Array.FindAll(host.GetComponentsInChildren<UnityEngine.UI.MaskableGraphic>(true),g=>g.GetType().FullName=="Deviant.UI.Theme.PixelFramedPanel");
            Assert.That(frames.Length,Is.EqualTo(1),"An existing prefab must replace its old frame without adding duplicates on reopen.");
            Assert.That(frames[0].GetType().GetProperty("SourceTexture").GetValue(frames[0]),Is.Not.Null);
            Assert.That(frames[0].GetComponentsInChildren<UnityEngine.UI.MaskableGraphic>(true).Length,Is.EqualTo(2),"One outline and one original-texture layer.");
            foreach(var image in host.GetComponentsInChildren<UnityEngine.UI.Image>())
                Assert.That(image.name,Is.Not.EqualTo("Native pixel panel").And.Not.EqualTo("Approved frame"),"The previous prefab frame must remain hidden.");
        }

        [Test] public void AttackOpensBeforeCombatAndPersistsAcrossProfileReload()
        {
            var profile = Chapter1ProfileHost.Service;
            var plan = profile.PlanUnarmedRun();
            var binding = Binding(plan);
            Attack(binding);
            Assert.That(binding.FirstRunTutorialShowing, Is.True);
            Assert.That(Window(binding).PageCount, Is.EqualTo(1));
            Assert.That(binding.Arena.Controls.Current.SimulationPaused, Is.True);
            Assert.That(profile.Snapshot.HasSeen(SketchRunBinding.AttackTutorialRecord), Is.True);
            Window(binding).Dismiss();
            Assert.That(binding.Arena.Controls.Current.SimulationPaused, Is.False);
            Attack(binding);
            Assert.That(binding.FirstRunTutorialShowing, Is.False);
            Chapter1ProfileHost.UseStore(store);
            Assert.That(SketchRunBinding.TutorialEligible(plan, Chapter1ProfileHost.Service.Snapshot,
                SketchRunBinding.AttackTutorialRecord), Is.False);
        }

        [Test] public void FirstRewardWaitsForGuideWithoutChangingOfferOrItsPause()
        {
            var profile = Chapter1ProfileHost.Service;
            var plan = profile.PlanUnarmedRun();
            var binding = Binding(plan);
            binding.State.RecordKill("elite", 200);
            Assert.That(binding.State.OpenBanked(), Is.True);
            var offer = binding.State.PendingOffer;
            Assert.That(binding.TryChooseReward(0), Is.False);
            Assert.That(binding.FirstRunTutorialShowing, Is.True);
            Assert.That(Window(binding).PageCount, Is.EqualTo(5));
            Assert.That(binding.State.PendingOffer, Is.SameAs(offer));
            Assert.That(profile.Snapshot.HasSeen(SketchRunBinding.RewardTutorialRecord), Is.True);
            Assert.That(profile.Snapshot.HasSeen(SketchRunBinding.AttackTutorialRecord), Is.False);
            Window(binding).Dismiss();
            Assert.That(binding.Arena.Controls.Current.SimulationPaused, Is.True);
            Assert.That(binding.TryChooseReward(0), Is.True);
            Assert.That(binding.State.PendingOffer, Is.Not.Null, "the first reward still owes its blueprint");
            Assert.That(binding.Arena.Controls.Current.SimulationPaused, Is.True);
            Assert.That(binding.TryChooseReward(0), Is.True);
            binding.ContinueReward();
            Assert.That(binding.Arena.Controls.Current.SimulationPaused, Is.False);
            binding.State.RecordKill("elite", 200);
            binding.State.OpenBanked();
            binding.TryChooseReward(0);
            Assert.That(binding.FirstRunTutorialShowing, Is.False);
            Chapter1ProfileHost.UseStore(store);
            Assert.That(SketchRunBinding.TutorialEligible(plan, Chapter1ProfileHost.Service.Snapshot,
                SketchRunBinding.RewardTutorialRecord), Is.False);
        }

        [Test] public void SecondRunAndTrainingNeverOpenUnseenGuides()
        {
            var profile = Chapter1ProfileHost.Service;
            var first = profile.PlanUnarmedRun();
            profile.SettleRun(first.RunId, RunOutcome.Died, false);
            var next = profile.PlanUnarmedRun();
            foreach (var record in new[] { SketchRunBinding.AttackTutorialRecord, SketchRunBinding.RewardTutorialRecord })
            {
                Assert.That(SketchRunBinding.TutorialEligible(next, profile.Snapshot, record), Is.False);
                Assert.That(SketchRunBinding.TutorialEligible(profile.PlanUnarmedRun(true), profile.Snapshot, record), Is.False);
                Assert.That(SketchRunBinding.TutorialEligible(null, profile.Snapshot, record), Is.False);
            }
        }
    }
}
