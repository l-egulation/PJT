using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Music;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    // The pause menu sets 전체 and 음악 in 10% steps and keeps them across sessions (playtest 2026-11-04).
    public sealed class Chapter1PauseVolumeTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        float savedMaster, savedMusic; bool hadMaster, hadMusic;

        [UnitySetUp] public IEnumerator Remember()
        {
            hadMaster = PlayerPrefs.HasKey(GameAudioSettings.MasterKey); hadMusic = PlayerPrefs.HasKey(GameAudioSettings.MusicKey);
            savedMaster = PlayerPrefs.GetFloat(GameAudioSettings.MasterKey, 1); savedMusic = PlayerPrefs.GetFloat(GameAudioSettings.MusicKey, 1);
            PlayerPrefs.SetFloat(GameAudioSettings.MasterKey, 1); PlayerPrefs.SetFloat(GameAudioSettings.MusicKey, 1);
            yield break;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            Chapter1ProfileHost.UseStore(null);
            if (hadMaster) PlayerPrefs.SetFloat(GameAudioSettings.MasterKey, savedMaster); else PlayerPrefs.DeleteKey(GameAudioSettings.MasterKey);
            if (hadMusic) PlayerPrefs.SetFloat(GameAudioSettings.MusicKey, savedMusic); else PlayerPrefs.DeleteKey(GameAudioSettings.MusicKey);
            PlayerPrefs.Save(); AudioListener.volume = hadMaster ? savedMaster : 1;
        }

        static Button Find(RectTransform card, string name) => card.GetComponentsInChildren<Button>(true).Single(b => b.name == name);

        [UnityTest] public IEnumerator VillageConsoleResetRequiresConfirmationAndReturnsToTitle() => VerifyConsoleReset(StartingVillageTests.ScenePath);
        [UnityTest] public IEnumerator CombatConsoleResetRequiresConfirmationAndReturnsToTitle() => VerifyConsoleReset(Chapter1MapSelector.ProceduralScene);

        [UnityTest] public IEnumerator ChapterTwoConsoleResetRequiresConfirmationAndReturnsToTitle() => VerifyConsoleReset(Deviant.Chapter2.Chapter2EntryBridge.ScenePath);

        IEnumerator VerifyConsoleReset(string scenePath)
        {
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(scenePath);
            yield return new EnterPlayMode();
            // EnterPlayMode reloads the test iterator; resolve the context again from the loaded scene.
            bool inVillage = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == StartingVillageTests.ScenePath;
            var store = new Deviant.Run.MemoryProfileStore();
            Chapter1ProfileHost.UseStore(store);
            var profile = Chapter1ProfileHost.Service;
            profile.MarkStorySeen("opening");
            MonoBehaviour gui = null;
            for (int i = 0; i < 900 && !gui; i++)
            {
                yield return null;
                gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(b =>
                    b.GetType().Name == "TemporaryGameGui" && (bool)b.GetType().GetField("built", Hidden).GetValue(b));
            }
            Assert.That(gui, Is.Not.Null); yield return null; yield return null;
            var type = gui.GetType();
            if (inVillage && (bool)type.GetProperty("OpeningActive").GetValue(gui))
                type.GetMethod("SkipOpening", Hidden).Invoke(gui, null);
            yield return null;
            var primary = (Button)type.GetField("menuPrimary", Hidden).GetValue(gui);
            void Refresh() => type.GetMethod(inVillage ? "RefreshHub" : "Refresh", Hidden | BindingFlags.Public).Invoke(gui, null);
            void Pause()
            {
                if (inVillage) type.GetMethod("HubPause", Hidden).Invoke(gui, null);
                else
                {
                    var presenter = type.GetProperty("Presenter", Hidden | BindingFlags.Public).GetValue(gui);
                    presenter.GetType().GetMethod("TogglePause").Invoke(presenter, null);
                }
                Refresh();
            }
            Refresh(); Pause();
            Assert.That(gui.GetComponentsInChildren<Button>(true).Any(button =>
                button.GetComponentInChildren<Text>()?.text == "게임 초기화"), Is.False, "ESC has no reset button");
            primary.onClick.Invoke(); Refresh();
            var menu = Object.FindAnyObjectByType<DebugGameMenu>();
            Assert.That(menu.Open(), Is.True);
            menu.SelectCategory(1);
            void Choose(string label)
            {
                menu.ChangeSelectedValue(0); // Rebuild the rows after entering/leaving a confirmation.
                for (int i = 0; i < 100 && menu.SelectedLabel != label; i++) menu.MoveSelection(1);
                Assert.That(menu.SelectedLabel, Is.EqualTo(label));
                Assert.That(menu.ExecuteSelected(), Is.True);
            }
            string oldProfile = profile.Snapshot.ProfileId;
            Choose("게임 초기화");
            Assert.That(profile.Snapshot.HasSeen("opening"), Is.True, "confirmation preserves progress");
            menu.Back();
            Assert.That(profile.Snapshot.ProfileId, Is.EqualTo(oldProfile), "cancel preserves progress");
            Choose("게임 초기화");
            store.FailNextSave = true;
            Choose("확인하고 적용");
            Assert.That(menu.Status, Does.StartWith("게임 초기화 실패:"));
            Assert.That(DebugGameMenu.IsOpen, Is.True);
            Assert.That(profile.Snapshot.ProfileId, Is.EqualTo(oldProfile), "save failure preserves progress and scene");
            Choose("게임 초기화");
            Choose("확인하고 적용");
            Assert.That(profile.Snapshot.HasSeen("opening"), Is.False);
            Assert.That(profile.Snapshot.ProfileId, Is.Not.EqualTo(oldProfile));
            Assert.That(Chapter1RunHost.Instance.Flow.Current, Is.Null, "no old run can settle into the fresh profile");
            Assert.That(DebugGameMenu.IsOpen, Is.False);
            for (int i = 0; i < 300 && UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != Chapter1RunHost.TitleScene; i++) yield return null;
            Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, Is.EqualTo(Chapter1RunHost.TitleScene));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(Deviant.Music.GameAudioSettings.Master, Is.EqualTo(1f), "audio settings survive profile reset");
        }

        [UnityTest] public IEnumerator PauseMenuStepsMasterAndMusicVolumeAndKeepsThem()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            Object.FindAnyObjectByType<ForestCombatWorld>().proceduralSeed = 42;
            yield return new EnterPlayMode();
            ForestCombatWorld world = null;
            for (int i = 0; i < 900 && !(world && world.Ready); i++) { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.IsTrue(world && world.Ready); yield return null; yield return null;
            Assert.That(GameAudioSettings.Master, Is.EqualTo(1f)); Assert.That(AudioListener.volume, Is.EqualTo(1f));
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui" && b.GetType().GetField("host", Hidden).GetValue(b) != null);
            var card = (RectTransform)gui.GetType().GetField("volumeCard", Hidden).GetValue(gui);
            void Refresh() => gui.GetType().GetMethod("Refresh").Invoke(gui, null);

            Refresh(); Assert.That(card.gameObject.activeInHierarchy, Is.False, "no volume card while playing");
            var presenter = gui.GetType().GetProperty("Presenter", Hidden | BindingFlags.Public).GetValue(gui);
            presenter.GetType().GetMethod("TogglePause").Invoke(presenter, null); Refresh(); yield return null;
            Assert.That(card.gameObject.activeInHierarchy, Is.True, "the pause menu shows the volume card");

            for (int i = 0; i < 3; i++) Find(card, "전체 −").onClick.Invoke();
            Find(card, "음악 −").onClick.Invoke(); Find(card, "음악 −").onClick.Invoke();
            Assert.That(GameAudioSettings.Master, Is.EqualTo(.7f).Within(1e-5f)); Assert.That(AudioListener.volume, Is.EqualTo(.7f).Within(1e-5f));
            Assert.That(GameAudioSettings.Music, Is.EqualTo(.8f).Within(1e-5f));
            Assert.That(BackgroundMusicPlayer.Instance.volume, Is.EqualTo(.8f).Within(1e-5f), "music follows its own setting");
            Assert.That(PlayerPrefs.GetFloat(GameAudioSettings.MusicKey), Is.EqualTo(.8f).Within(1e-5f), "kept for the next session");
            var texts = card.GetComponentsInChildren<Text>().Select(t => t.text).ToArray();
            CollectionAssert.Contains(texts, "70%"); CollectionAssert.Contains(texts, "80%");
            for (int i = 0; i < 5; i++) Find(card, "음악 +").onClick.Invoke();
            Assert.That(GameAudioSettings.Music, Is.EqualTo(1f), "clamped at 100%");
            for (int i = 0; i < 12; i++) Find(card, "전체 −").onClick.Invoke();
            Assert.That(GameAudioSettings.Master, Is.EqualTo(0f), "clamped at 0%");
            Find(card, "전체 +").onClick.Invoke(); Find(card, "전체 +").onClick.Invoke(); Find(card, "전체 +").onClick.Invoke(); Find(card, "전체 +").onClick.Invoke(); Find(card, "전체 +").onClick.Invoke(); Find(card, "전체 +").onClick.Invoke(); Find(card, "전체 +").onClick.Invoke();
            Refresh(); yield return null;
            Chapter1MaintenanceGuiCaptureTests.Capture(world, gui, "pause-volume");
            GameAudioSettings.Load();
            Assert.That(GameAudioSettings.Master, Is.EqualTo(.7f).Within(1e-5f), "loads what was saved");
            presenter.GetType().GetMethod("TogglePause").Invoke(presenter, null); Refresh();
        }
    }
}
