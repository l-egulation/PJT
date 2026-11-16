using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Deviant.Chapter2;
using Deviant.Combat;
using Deviant.Control.Contracts;
using Deviant.Run;
using Deviant.Tutorial.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    // Real title/start, village/sign, both boss exits, loading, final-result button and home scenes.
    // Fixture shortcuts are limited to opening/tutorial dismissal, development travel and accelerated boss combat.
    public sealed class ActualSceneFlowTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const string Output = "../outputs/actual-scene-flow";
        static MonoBehaviour Gui() => Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .FirstOrDefault(b => b.GetType().Name == "TemporaryGameGui");
        static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
        static void Call(object owner, string name) => owner.GetType().GetMethod(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(owner, null);
        static StartingVillageWorld Village()
        { var v = Object.FindAnyObjectByType<StartingVillageWorld>(); return v && v.Ready ? v : null; }
        static ForestCombatWorld ChapterOne()
        { var w = Object.FindAnyObjectByType<ForestCombatWorld>(); return w && w.Ready && w.GetComponent<Chapter1RunBinding>() ? w : null; }
        static Chapter2World ChapterTwo()
        { var w = Object.FindAnyObjectByType<Chapter2World>(); return w && w.Ready ? w : null; }
        static bool LoadingLabelIs(string expected)
        {
            var fade = Object.FindAnyObjectByType<ChapterSceneFade>();
            return fade && fade.GetComponentsInChildren<Text>(true).Any(t => t.name == "Place" && t.text == expected);
        }
        static bool Free(CombatArena arena) => arena && !arena.Controls.Current.SimulationPaused &&
            !arena.Controls.Current.Blocks(InputChannels.Combat | InputChannels.Interaction | InputChannels.Navigation);
        static void Stand(CombatPlayer player, Vector2 at)
        { player.Position = at; player.velocity = Vector2.zero; Physics2D.SyncTransforms(); }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            Chapter1ProfileHost.UseStore(null);
        }

        static IEnumerator Until(Func<bool> done, string step, float seconds = 45)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(done(), Is.True, step + "; scene=" + SceneManager.GetActiveScene().path + "; fade=" + ChapterSceneFade.Busy);
            Debug.Log("ACTUAL FLOW: " + step);
        }

        [UnityTest]
        public IEnumerator TitleVillageTwoChaptersResultAndVillageUseActualSceneLoads()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1RunHost.TitleScene);
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new MemoryProfileStore());
            yield return Walkthrough();
        }

        static IEnumerator Walkthrough()
        {
            Directory.CreateDirectory(Output);
            var profile = Chapter1ProfileHost.Service;
            // Tutorial-specific behavior is covered separately; retain an otherwise fresh first-run profile.
            profile.MarkStorySeen(Chapter1FirstRunInkGuide.GuideBagStory);
            profile.MarkStorySeen(SketchRunBinding.AttackTutorialRecord);
            profile.MarkStorySeen(SketchRunBinding.RewardTutorialRecord);
            yield return Until(() => Gui() && Field<MonoBehaviour>(Gui(), "wellTitle"), "title GUI ready");
            var title = Field<MonoBehaviour>(Gui(), "wellTitle");
            var buttons = (Button[])title.GetType().GetProperty("MenuButtons").GetValue(title);
            Capture(null, "01-title");
            buttons[0].onClick.Invoke();
            Assert.That((bool)title.GetType().GetProperty("Transitioning").GetValue(title), Is.True);
            buttons[0].onClick.Invoke(); // the title's own duplicate-start guard must hold.
            yield return Until(() => Village() && Gui(), "real title start reaches the village");
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(Chapter1RunHost.VillageScene));
            yield return null; yield return null;
            var village = Village();
            if (Field<bool>(Gui(), "openingActive")) Call(Gui(), "SkipOpening");
            yield return Until(() => Free(village.World.arena), "opening dismissal restores village controls");
            Capture(village.World.View, "02-village");

            Stand(village.World.arena.Player, village.Find(StartingVillageWorld.RecordDesk).Position);
            Assert.That(village.TryInteract(), Is.True, "the real village record station opens");
            yield return Until(() => Gui() && Field<GameObject>(Gui(), "hubOpen"), "record station GUI visible");
            Assert.That(village.World.arena.Controls.Current.SimulationPaused, Is.True);
            Capture(village.World.View, "03-village-records");
            Call(Gui(), "Back");
            // Back returns its pause scope synchronously, but the closing input frame remains consumed.
            // A player's next interaction occurs on a later frame (OpenStation checks hubClosedFrame).
            yield return null;
            yield return Until(() => Free(village.World.arena), "closing the service restores village controls");

            Stand(village.World.arena.Player, village.Find(StartingVillageWorld.DepartureSign).Position);
            Assert.That(village.NearestStation?.Id, Is.EqualTo(StartingVillageWorld.DepartureSign));
            Assert.That(village.TryInteract(), Is.True, "the real departure sign prepares a run");
            Assert.That(village.Departing, Is.True);
            var host = Chapter1RunHost.Instance;
            var run = host.Flow.Current;
            Assert.That(run.Plan.Unarmed && run.Plan.Blueprints.Count == 0, Is.True);
            Assert.That(village.TryInteract(), Is.False, "repeat interaction cannot prepare another run");
            Assert.That(host.Flow.Current, Is.SameAs(run));
            yield return Until(() => ChapterSceneFade.Busy, "departure walk opens the real loading screen", 25);
            Assert.That(ChapterSceneFade.ShowingPlace, Is.EqualTo(LoadingPlace.Chapter1));
            Assert.That(ChapterSceneFade.ShowingLeaving, Is.EqualTo(LoadingPlace.Village));
            yield return Until(() => LoadingLabelIs("1챕터"), "chapter-one loading label finishes writing", 10);
            Capture(village ? village.World.View : null, "04-village-to-chapter1");
            yield return Until(() => ChapterOne() && !ChapterSceneFade.Busy && ChapterOne().ExternalPresentation,
                "departure loads and reveals chapter one");
            var first = ChapterOne();
            var binding = first.GetComponent<Chapter1RunBinding>();
            Assert.That(binding.Run, Is.SameAs(run));
            yield return Until(() => Free(first.arena), "chapter one releases loading input restrictions");
            Assert.That(first.GetComponent<Chapter1StartingBlueprints>().Complete, Is.True);
            Assert.That(first.GetComponent<Chapter1StartingBlueprints>().SelectedCount, Is.Zero);
            Capture(first.View, "05-chapter1");

            var player = first.arena.Player;
            float maximum = player.maxHealth;
            player.maxHealth = player.health = 100000;
            Assert.That(Chapter1DebugTravel.TryToBoss(first, out var travelError), Is.True, travelError);
            var clock = first.GetComponent<ClockExecutionerEncounter>();
            yield return Until(() => clock.SkipAvailable || clock.Boss.Started, "chapter-one boss entry begins");
            if (clock.SkipAvailable) Assert.That(clock.TrySkip(), Is.True);
            yield return Until(() => clock.Boss.Started && Free(first.arena), "chapter-one boss fight starts");
            Assert.That(clock.Boss.Receive(1000000, player, 1, false, null, bypass: true), Is.True,
                "fixture lethal hit follows the actual boss damage/defeat path");
            yield return Until(() => clock.Defeated && !clock.DeathCutscene && !binding.BossOfferPending,
                "chapter-one boss outro finishes before reward");
            yield return ResolveRewards(first.gameObject);
            var carriedWeapon = run.Sketch.Arsenal.Weapons.First();
            first.arena.Sketch.Confirm(carriedWeapon.Id);
            first.arena.Sketch.Equip(carriedWeapon.Id, 0);
            first.arena.SyncSketchLoadout();
            var exit = first.GetComponent<Chapter1BossEncounter>();
            yield return Collect(exit.VillageInkPickup, player);
            player.maxHealth = maximum; player.health = 57;
            var growth = run.Growth;
            var sketchState = run.Sketch;
            var weaponIds = player.Loadout.Weapons.Select(w => w.InstanceId).ToArray();
            int gold = run.Economy.Gold;
            Stand(player, exit.ExitPosition);
            first.RefreshCamera(0, true);
            Capture(first.View, "06-chapter1-exit-ready");
            Assert.That(exit.TryUseExit(), Is.True, "the actual chapter-one portcullis accepts F");
            Assert.That(exit.TryUseExit(), Is.False, "the exit accepts only one transition");
            Assert.That(first.arena.Controls.Current.SimulationPaused, Is.True);
            yield return Until(() => ChapterSceneFade.Busy, "chapter-one exit opens chapter-two loading");
            Assert.That(run.Ended, Is.False);
            Assert.That(ChapterSceneFade.ShowingPlace, Is.EqualTo(LoadingPlace.Chapter2));
            Assert.That(ChapterSceneFade.ShowingLeaving, Is.EqualTo(LoadingPlace.Chapter1));
            yield return Until(() => LoadingLabelIs("2챕터"), "chapter-two loading label finishes writing", 10);
            Capture(first ? first.View : null, "07-chapter1-to-chapter2");
            yield return Until(() => ChapterTwo() && ChapterTwo().ExternalPresentation && !ChapterSceneFade.Busy,
                "actual chapter-one exit reveals chapter two");
            var second = ChapterTwo();
            yield return Until(() => Free(second.arena), "chapter two releases transition input restrictions");
            Assert.That(second.SourceRun, Is.SameAs(run));
            Assert.That(run.Chapter, Is.EqualTo(2));
            Assert.That(run.Ended, Is.False);
            Assert.That(run.Growth, Is.SameAs(growth));
            Assert.That(run.Sketch, Is.SameAs(sketchState));
            Assert.That(second.arena.Sketch.Equipped(0).Id, Is.EqualTo(carriedWeapon.Id));
            Assert.That(second.arena.Player.health, Is.EqualTo(57));
            Assert.That(second.Economy.Gold, Is.EqualTo(gold));
            CollectionAssert.AreEqual(weaponIds, second.arena.Player.Loadout.Weapons.Select(w => w.InstanceId).ToArray());
            Capture(second.arena.ViewCamera, "08-chapter2-carried-run");

            second.arena.Player.maxHealth = second.arena.Player.health = 100000;
            second.WarpToRoom("B");
            yield return DefeatChapterTwo(second);
            yield return Until(() => second.Boss.Dead && !second.Boss.DefeatPresentationPlaying && !second.BossOfferPending,
                "chapter-two final defeat pose finishes before reward");
            yield return ResolveRewards(second.gameObject);
            yield return Collect(second.Exit.VillageInkPickup, second.arena.Player);
            Stand(second.arena.Player, second.Exit.Position);
            Capture(second.arena.ViewCamera, "09-chapter2-exit-ready");
            Assert.That(second.TryUseExit(), Is.True, "the actual last-chapter exit opens");
            Assert.That(second.TryUseExit(), Is.False);
            Assert.That(second.ShowingResult, Is.False, "the result waits for the door and blackout");
            yield return Until(() => second.Complete, "chapter-two door finishes and clear blackout starts");
            Assert.That(second.ShowingResult, Is.False);
            Assert.That(second.arena.Controls.Current.Blocks(InputChannels.Combat), Is.True);
            yield return Until(() => second.ShowingResult, "full blackout settles the final run", 12);
            Assert.That(run.Result.HasValue, Is.True);
            Assert.That(run.Result.Value.Outcome, Is.EqualTo(RunOutcome.ChapterCleared));
            Assert.That(run.Result.Value.Kills, Is.GreaterThanOrEqualTo(2));
            Assert.That(second.arena.Controls.Current.SimulationPaused, Is.True);
            yield return null; yield return null;
            var completion = Object.FindAnyObjectByType<DemoCompletionPanel>();
            Assert.That(completion && completion.isActiveAndEnabled, Is.True);
            AssertFinalMenuFitsViewport(completion, 1280, 720);
            string summary = completion.GetComponentsInChildren<Text>().Single(t => t.text.StartsWith("플레이 시간 ")).text;
            StringAssert.Contains(" / 처치 " + run.Result.Value.Kills + " / 마법안료 +" + run.Pigment, summary);
            yield return new WaitForSecondsRealtime(DemoCompletionPanel.InputGraceSeconds + .05f);
            Capture(second.arena.ViewCamera, "10-final-result");
            completion.VillageButton.onClick.Invoke();
            yield return Until(() => ChapterSceneFade.Busy, "final-result village button opens loading");
            Assert.That(ChapterSceneFade.ShowingLeaving, Is.EqualTo(LoadingPlace.Chapter2));
            Assert.That(ChapterSceneFade.ShowingPlace, Is.EqualTo(LoadingPlace.Village));
            yield return Until(() => LoadingLabelIs("우물 마을"), "village loading label finishes writing", 10);
            Capture(second ? second.arena.ViewCamera : null, "11-chapter2-to-village");
            yield return Until(() => Village() && !ChapterSceneFade.Busy && Gui(), "final-result button returns to the real village");
            village = Village();
            // Returning-home stories have their own tests; dismiss their real directors to check scope cleanup.
            float releaseDeadline = Time.realtimeSinceStartup + 12;
            while (!Free(village.World.arena) && Time.realtimeSinceStartup < releaseDeadline)
            {
                if (Gui() && Field<bool>(Gui(), "openingActive")) Call(Gui(), "SkipOpening");
                yield return null;
            }
            Assert.That(Free(village.World.arena), Is.True, "returning-home stories release their controls");
            Assert.That(profile.PalettePieces, Is.EqualTo(2));
            Capture(village.World.View, "12-village-returned");
            File.WriteAllText(Path.Combine(Output, "completed.txt"), "Title -> Village -> Chapter1 -> Chapter2 -> Demo completion -> Village\n" + summary);
        }

        static IEnumerator ResolveRewards(GameObject owner)
        {
            var sketch = owner.GetComponent<SketchRunBinding>();
            var augments = owner.GetComponent<AugmentRewardChoice>();
            float deadline = Time.realtimeSinceStartup + 15;
            do
            {
                if (sketch)
                {
                    sketch.ContinueReward();
                    if (sketch.State.PendingOffer != null) Assert.That(sketch.TryChooseReward(0), Is.True);
                    else if (sketch.State.MustOpenBeforeExit) sketch.TryOpenBankedReward();
                }
                if (augments && augments.IsOpen) Assert.That(augments.TryTake(0), Is.True);
                yield return null;
            } while ((sketch && (sketch.State.MustOpenBeforeExit || sketch.RewardModalShown) ||
                      augments && (augments.Queued > 0 || augments.IsOpen)) && Time.realtimeSinceStartup < deadline);
            Assert.That(!sketch || !sketch.State.MustOpenBeforeExit && !sketch.RewardModalShown, Is.True, "all actual reward choices finish");
            Assert.That(!augments || augments.Queued == 0 && !augments.IsOpen, Is.True);
            yield return null;
        }

        static IEnumerator Collect(VillageInkPickup pickup, CombatPlayer player)
        {
            if (!pickup.Required) yield break;
            yield return Until(() => pickup.Landed, "boss village ink finishes landing", 8);
            Stand(player, pickup.Position);
            Assert.That(pickup.TryCollect(), Is.True, "required village ink is collected through its real interaction");
            yield return null;
        }

        static string PhaseName(RoseBlueEncounter boss)
        {
            object model = boss.GetType().GetProperty("Model").GetValue(boss);
            return model.GetType().GetProperty("Phase").GetValue(model).ToString();
        }

        // Exercise the real fitting method at a non-integer design scale without changing the Editor Game View.
        // If the final menu is still under the pixel/HUD stage, it measures 960x540 instead of filling 1280x720.
        static void AssertFinalMenuFitsViewport(DemoCompletionPanel completion, int width, int height)
        {
            var gui = Gui();
            var canvas = gui.GetComponent<Canvas>();
            var scaler = gui.GetComponent<CanvasScaler>();
            var menuStage = gui.transform.Find("Approved menu viewport");
            Assert.That(menuStage, Is.Not.Null);
            float previousScaler = scaler.scaleFactor, previousCanvas = canvas.scaleFactor;
            var previousMenuScale = menuStage.localScale;
            try
            {
                scaler.scaleFactor = canvas.scaleFactor = 1;
                gui.GetType().GetMethod("FitApprovedMenus", Private).Invoke(gui, new object[] { width, height });
                Canvas.ForceUpdateCanvases();
                var corners = new Vector3[4];
                ((RectTransform)completion.transform).GetWorldCorners(corners);
                var lower = canvas.transform.InverseTransformPoint(corners[0]);
                var upper = canvas.transform.InverseTransformPoint(corners[2]);
                Assert.That(upper.x - lower.x, Is.EqualTo(1280).Within(.1f),
                    "the final menu follows the continuous menu viewport instead of the integer-scaled HUD stage");
                Assert.That(upper.y - lower.y, Is.EqualTo(720).Within(.1f));
            }
            finally
            {
                scaler.scaleFactor = previousScaler; canvas.scaleFactor = previousCanvas;
                menuStage.localScale = previousMenuScale; Canvas.ForceUpdateCanvases();
            }
        }

        static IEnumerator DefeatChapterTwo(Chapter2World world)
        {
            var boss = world.Boss;
            using (var simulation = world.arena.AcquireManualSimulation())
            {
                int steps = 0;
                float deadline = Time.realtimeSinceStartup + 60;
                while (!boss.Dead && steps < 24000 && Time.realtimeSinceStartup < deadline)
                {
                    if (world.arena.Controls.Current.SimulationPaused) { yield return null; continue; }
                    string phase = PhaseName(boss);
                    if (phase == "Machine") boss.Machine.health = 0;
                    else if (phase == "Duo") boss.Rose.health = 0;
                    else if (phase == "FusedBlue") boss.Blue.health = 0;
                    Physics2D.SyncTransforms(); simulation.Step(1f / 120); steps++;
                    if (steps % 120 == 0) yield return null;
                }
                Assert.That(boss.Dead, Is.True, "accelerated native simulation crosses every chapter-two boss phase; phase=" + PhaseName(boss));
            }
            yield return null;
        }

        // World camera plus overlay canvases only; IMGUI world notices require a full Game View capture elsewhere.
        static void Capture(Camera camera, string name)
        {
            bool own = !camera;
            if (own) { camera = new GameObject("Flow capture camera").AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; }
            var overlays = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(c => c.isRootCanvas && c.isActiveAndEnabled && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            var cameras = overlays.Select(c => c.worldCamera).ToArray();
            var distances = overlays.Select(c => c.planeDistance).ToArray();
            var target = camera.targetTexture; var rect = camera.rect; var previous = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = rt; camera.rect = new Rect(0, 0, 1, 1);
                foreach (var canvas in overlays) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + 1; }
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(Output, name + ".png"), image.EncodeToPNG());
            }
            finally
            {
                for (int i = 0; i < overlays.Length; i++) { overlays[i].renderMode = RenderMode.ScreenSpaceOverlay; overlays[i].worldCamera = cameras[i]; overlays[i].planeDistance = distances[i]; }
                camera.targetTexture = target; camera.rect = rect; RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt); Object.Destroy(image); if (own) Object.Destroy(camera.gameObject);
            }
        }
    }
}
