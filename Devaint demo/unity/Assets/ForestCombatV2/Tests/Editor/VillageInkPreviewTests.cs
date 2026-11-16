using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class VillageInkPreviewTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        const string Pending = "Deviant.VillageInkPreview.Pending", Restart = "Deviant.VillageInkPreview.Restart";
        static Type PreviewType => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("Deviant.Game.Editor.VillageInkPreviewEditor")).First(t => t != null);
        static object Read(object owner, string name) => owner.GetType().GetProperty(name).GetValue(owner);
        static MonoBehaviour Gui() => Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .FirstOrDefault(b => b.GetType().Name == "TemporaryGameGui");
        static ProgressionCatalog Catalog() => new ProgressionCatalog(JsonUtility.FromJson<ProgressionData>(Resources.Load<TextAsset>("RogueliteProgression").text));
        static IEnumerator Until(Func<bool> done, float seconds = 30)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(done(), Is.True, "preview did not reach its requested screen");
        }

        [Test] public void FullPlayPreviewStartsFreshWithoutChangingTheNormalProfile()
        {
            var data = (ProfileData)PreviewType.GetMethod("CreateProfile").Invoke(null, new object[] { 17 });
            var service = new ProgressionService(Catalog(), new MemoryProfileStore(data));
            Assert.That(service.LoadWarning, Is.Null);
            Assert.That(service.Snapshot.RunsStarted, Is.Zero);
            Assert.That(service.Snapshot.SeenStories, Is.Empty);
            Assert.That(service.Snapshot.FirstRunSettled, Is.False);
            Assert.That(service.PalettePieces, Is.Zero);
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(11)]
        [TestCase(12)] [TestCase(13)] [TestCase(14)] [TestCase(15)] [TestCase(16)]
        [TestCase(20)] [TestCase(21)] [TestCase(22)] [TestCase(23)] [TestCase(24)] [TestCase(25)]
        public void EveryPreviewProfileLoadsWithoutLosingItsStoryFlags(int mode)
        {
            var data = (ProfileData)PreviewType.GetMethod("CreateProfile").Invoke(null, new object[] { mode });
            var service = new ProgressionService(Catalog(), new MemoryProfileStore(data));
            Assert.That(service.LoadWarning, Is.Null);
            Assert.That(service.Snapshot.ProfileId, Is.EqualTo(data.profileId));
            Assert.That(service.Snapshot.HasSeen("opening"), Is.True, "the prologue must stay seen");
            Assert.That(service.Snapshot.HasSeen("first-return"), Is.True);
            Assert.That(service.PalettePieces, Is.EqualTo(mode == 12 || mode == 13 ? 0 : mode == 14 || mode == 15 ? 2 : mode >= 20 ? mode-20 : mode >= 8 ? 1 : mode == 6 || mode == 7 ? 0 : mode));
            if (mode == 6)
            {
                var plan = service.PlanRun(new[] { "sword", "blunt" });
                Assert.That(Chapter1FirstRunInkGuide.Eligible(true, plan.ProfileId, service.Snapshot), Is.False,
                    "a boss preview must not start the ordinary ink tutorial reaction");
            }
        }

        [Test] public void ADecoratedProfileIdReproducesTheDiscardedFlags()
        {
            var broken = new ProfileData { profileId = "red-ink-preview-" + Guid.NewGuid(), seenStories = new[] { "opening", "guide-bag" } };
            var service = new ProgressionService(Catalog(), new MemoryProfileStore(broken));
            Assert.That(service.LoadWarning, Does.Contain("profile id must be a GUID"));
            Assert.That(service.Snapshot.HasSeen("opening"), Is.False);
            Assert.That(service.Snapshot.HasSeen("guide-bag"), Is.False);
        }

        [UnityTest] public IEnumerator RealPreviewBossDropStartsWithoutTheOrdinaryInkTutorial() => CheckEntry(6);
        [UnityTest] public IEnumerator RealPreviewFirstRunStartsWithoutThePrologue() => CheckEntry(7);
        [UnityTest] public IEnumerator RealPreviewVillageVisitStartsWithoutThePrologue() => CheckEntry(1);
        [UnityTest] public IEnumerator RealPreviewPourStartsWithoutThePrologue() => CheckEntry(8);
        [UnityTest] public IEnumerator RealPreviewRewardStartsWithoutThePrologue() => CheckEntry(9);
        [UnityTest] public IEnumerator RealPreviewStorybookStartsWithoutThePrologue() => CheckEntry(11);
        [UnityTest] public IEnumerator RealPreviewFullColourVillageStartsWithoutThePrologue() => CheckEntry(25);

        static IEnumerator CheckEntry(int launchMode)
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
                EditorSceneManager.playModeStartScene = null;
                SessionState.EraseInt(Restart);
                SessionState.SetInt(Pending, launchMode);
                SessionState.SetInt("Deviant.VillageInkPreview.TestMode", launchMode);
                EditorSceneManager.OpenScene(launchMode == 6 ? Chapter1RunHost.TitleScene : StartingVillageTests.ScenePath);
                yield return new EnterPlayMode();
                // Allocate captured state after the domain reload, just as the interactive editor callback does.
                {
                int mode = SessionState.GetInt("Deviant.VillageInkPreview.TestMode", 0);
                Assert.That(Chapter1ProfileHost.Service.LoadWarning, Is.Null);
                Assert.That(Chapter1ProfileHost.Service.Snapshot.HasSeen("opening"), Is.True);
                MonoBehaviour gui = null;
                if (mode == 6)
                {
                    Chapter1VillageInkPickup pickup = null;
                    yield return Until(() => { pickup = Object.FindAnyObjectByType<Chapter1VillageInkPickup>(); return pickup && pickup.Landed && pickup.PlayerNear; });
                    gui = Gui();
                    Assert.That(Object.FindAnyObjectByType<Chapter1FirstRunInkGuide>().Active, Is.False);
                    Assert.That(pickup.TryCollect(), Is.True);
                    yield return null;
                    Assert.That(gui.GetComponentsInChildren<Text>(true).Any(t=>t.name=="붉은 잉크 발견 물음표"), Is.False);
                    Assert.That(Object.FindAnyObjectByType<ForestCombatWorld>().arena.Controls.Current.SimulationPaused, Is.False);
                    var reaction = gui.GetType().GetField("inkGuideBubble", Hidden).GetValue(gui) as Component;
                    Assert.That(reaction && reaction.gameObject.activeInHierarchy, Is.False);
                }
                else
                {
                    yield return Until(() => { gui = Gui(); return gui && Object.FindAnyObjectByType<StartingVillageWorld>()?.Ready == true; });
                    if (mode == 7 || mode == 1) yield return Until(() => (int)Read(gui,"VisitPlaying") == (mode == 7 ? 100 : 1));
                    if (mode == 8) yield return Until(() => (bool)Read(gui,"VillageInkShowing"));
                    if (mode == 9) yield return Until(() => (bool)Read(gui,"VillageInkRewardShowing"));
                    if (mode == 25) yield return Until(() => Object.FindAnyObjectByType<StartingVillageWorld>().ColourPieces == 5);
                    if (mode == 11)
                    {
                        yield return Until(() => gui.GetType().GetField("hubOpen", Hidden).GetValue(gui) is GameObject panel && panel.activeInHierarchy);
                        Assert.That((int)Read(gui, "RecordBookSpread"), Is.EqualTo(1));
                        Assert.That((bool)Read(gui, "RecordDetailShowing"), Is.False);
                        Assert.That(gui.GetComponentsInChildren<Text>().Any(t => t.text == "스토리 1"), Is.True);
                        var preview = (RawImage)gui.GetType().GetField("recordPreviewArt", Hidden).GetValue(gui);
                        Assert.That(preview.texture, Is.SameAs(Resources.Load<Texture2D>("RecordFarmStories/previews/story1")));
                    }
                    Assert.That((bool)Read(gui,"NotebookShowing"), Is.False, "the prologue notebook never replaces a requested preview");
                }
                Debug.Log("SCENE_PICKER_CHECK_PASSED: mode=" + mode);
                }
                yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator PickingUpThenSwitchingScenesKeepsTheSamePlaySession()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            SessionState.SetInt(Pending, 6);
            EditorSceneManager.OpenScene(Chapter1RunHost.TitleScene);
            yield return new EnterPlayMode();
            {
                Chapter1VillageInkPickup pickup = null;
                yield return Until(() => { pickup=Object.FindAnyObjectByType<Chapter1VillageInkPickup>(); return pickup && pickup.Landed && pickup.PlayerNear; });
                Assert.That(pickup.TryCollect(), Is.True);
                Assert.That(Object.FindAnyObjectByType<ForestCombatWorld>().arena.Controls.Current.SimulationPaused, Is.False);
                int domain=AppDomain.CurrentDomain.Id, exits=0;
                Action<PlayModeStateChange> watch=state=> { if(state==PlayModeStateChange.ExitingPlayMode) exits++; };
                EditorApplication.playModeStateChanged += watch;
                try
                {
                    foreach(int mode in new[]{1,8,9,7,25,6,1})
                    {
                        PreviewType.GetMethod("Review").Invoke(null,new object[]{mode});
                        yield return Until(() => (int)PreviewType.GetProperty("ActiveMode").GetValue(null)==mode);
                        MonoBehaviour gui=null;
                        if(mode==6)
                        {
                            yield return Until(() => { pickup=Object.FindAnyObjectByType<Chapter1VillageInkPickup>(); return pickup && pickup.Landed && pickup.PlayerNear; });
                            Assert.That(pickup.TryCollect(),Is.True);
                        }
                        else
                        {
                            yield return Until(() => { gui=Gui(); return gui && Object.FindAnyObjectByType<StartingVillageWorld>()?.Ready==true; });
                            if(mode==1 || mode==7) yield return Until(()=>(int)Read(gui,"VisitPlaying")== (mode==1?1:100));
                            if(mode==8) yield return Until(()=>(bool)Read(gui,"VillageInkShowing"));
                            if(mode==9) yield return Until(()=>(bool)Read(gui,"VillageInkRewardShowing"));
                            if(mode==25) yield return Until(()=>Object.FindAnyObjectByType<StartingVillageWorld>().ColourPieces==5);
                            Assert.That((bool)Read(gui,"NotebookShowing"),Is.False);
                        }
                        Assert.That(Chapter1ProfileHost.Service.LoadWarning,Is.Null);
                        Assert.That(EditorApplication.isPlaying,Is.True);
                        Assert.That(AppDomain.CurrentDomain.Id,Is.EqualTo(domain),"scene selection must not unload the scripting domain");
                        Assert.That(exits,Is.Zero,"scene selection must not stop/restart Play mode");
                        Debug.Log("LIVE_SCENE_SWITCH_CHECK_PASSED: mode="+mode+" domain="+domain);
                    }
                }
                finally { EditorApplication.playModeStateChanged -= watch; }
            }
            yield return new ExitPlayMode();
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            SessionState.EraseInt(Pending); SessionState.EraseInt(Restart);
            SessionState.EraseInt("Deviant.VillageInkPreview.TestMode");
            if (Application.isPlaying) yield return new ExitPlayMode();
            Chapter1ProfileHost.UseStore(null);
        }
    }
}
