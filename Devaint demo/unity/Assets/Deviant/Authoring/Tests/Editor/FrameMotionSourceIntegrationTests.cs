using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using Deviant.Presentation.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Deviant.Authoring.Tests
{
    public class FrameMotionSourceIntegrationTests
    {
        struct Metrics
        {
            public int firstHit, firstCue, completed, hits, bodyAt100ms;
            public float health, impact, active, end;
            public string frameVersion, motionVersion;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            CloseWindows();
        }
        static void CloseWindows()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>().Where(window => window.name == "PermanentFrameTestWindow")) window.Close();
        }
        static FrameTimingWindow Window(string repository)
        {
            var window = ScriptableObject.CreateInstance<FrameTimingWindow>(); window.name = "PermanentFrameTestWindow";
            window.Show(); window.CreateGUI(); window.LoadRepository(repository); return window;
        }
        static void Click(EditorWindow window, string name)
        {
            var button = window.rootVisualElement.Q<Button>(name); Assert.That(button.enabledSelf, Is.True, name);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
        }
        static void BindSprites(CombatArena arena, Sprite[] sprites)
        {
            foreach (var set in arena.characterAnimations.sets)
            {
                foreach (var clip in set.clips) clip.sprites = clip.frames.Select((_, index) => sprites[index % sprites.Length]).ToArray();
                if (set.effects != null) foreach (var effect in set.effects) effect.sprites = effect.frames.Select((_, index) => sprites[index % sprites.Length]).ToArray();
            }
        }
        static Metrics Run(CombatArena arena, Sprite[] sprites)
        {
            using (var driver = arena.AcquireManualSimulation())
            {
                driver.Reset(); var player = arena.Player; player.selected = 0;
                foreach (var weapon in player.weapons) weapon.StatSlots.Clear();
                foreach (var enemy in arena.enemies) { enemy.aggressive = false; enemy.Position = new Vector2(35, 0); }
                player.Reconfigure(0, Array.FindIndex(arena.catalog.forms, form => form.id == "base_sword"), 0);
                player.Position = Vector2.zero; player.SyncFacing(1); arena.enemies[0].Position = new Vector2(3, 0); Physics2D.SyncTransforms();
                var result = new Metrics { firstHit = -1, firstCue = -1, completed = -1, bodyAt100ms = -1 };
                CombatCast cast = null;
                for (int tick = 0; tick < 40; tick++)
                {
                    driver.Step(.02f, tick == 0 ? new CombatInput(0, CombatButtons.Basic) : default); arena.Presentation.Draw(0);
                    if (tick == 0)
                    {
                        cast = player.currentCast; Assert.That(cast, Is.Not.Null);
                        result.impact = cast.ImpactTime; result.active = cast.FrameTiming.Action.Active; result.end = cast.MotionDuration;
                        result.frameVersion = cast.FrameTimingVersion; result.motionVersion = arena.Presentation.CurrentAttackMotionVersion;
                    }
                    if (tick == 4) result.bodyAt100ms = Array.IndexOf(sprites, player.visual.sprite);
                    if (result.firstCue < 0 && arena.Presentation.LastEffectHandle != null)
                    {
                        result.firstCue = tick;
                        Assert.That(arena.Presentation.LastEffectHandle.Occurrence.ExecutionId, Is.EqualTo(cast.ExecutionId));
                        Assert.That(arena.Presentation.LastEffectHandle.Status, Is.EqualTo(CueStatus.Playing));
                    }
                    if (result.firstHit < 0 && arena.enemies[0].ReceivedHitCount > 0) result.firstHit = tick;
                    if (result.completed < 0 && player.currentCast == null) result.completed = tick;
                }
                result.health = arena.enemies[0].health; result.hits = arena.enemies[0].ReceivedHitCount;
                Assert.That(arena.Presentation.LastAttackMotionError, Is.Null); Assert.That(arena.Presentation.LastEffectError, Is.Null);
                return result;
            }
        }
        [UnityTest] public IEnumerator GameActivatesGeneratedPermanentSourceInTwoFreshSessions() => FreshSessions("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabActivatesGeneratedPermanentSourceInTwoFreshSessions() => FreshSessions("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        [UnityTest] public IEnumerator GameLoadsGeneratedMetadataFromResourcesWithoutAnOverride() => ResourceStartup("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabLoadsGeneratedMetadataFromResourcesWithoutAnOverride() => ResourceStartup("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        static IEnumerator ResourceStartup(string scene)
        {
            var generated = JsonUtility.FromJson<CharacterAnimationCatalog>(Resources.Load<TextAsset>("CharacterV2/AnimationCatalog").text);
            var combat = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("MeleeCatalog").text);
            var expected = FrameMotionBuildValidation.Validate(generated, combat);
            var expectedClip = generated.sets.Single(set => set.form == "base_sword").Find("attack1");
            LegacySceneFixture.Open(scene);
            Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            var actual = arena.characterAnimations.sets.Single(set => set.form == "base_sword").Find("attack1");
            Assert.That(actual.frames, Is.EqualTo(expectedClip.frames));
            Assert.That(actual.seconds, Is.EqualTo(expectedClip.seconds));
            Assert.That(arena.FrameSwordTiming?.ContentHash, Is.EqualTo(expected?.ContentHash));
            if (expected != null) Assert.That(arena.BasicSwordFirst.ContentHash, Is.EqualTo(expected.Action.ContentHash));
            yield return new ExitPlayMode();
        }
        static IEnumerator FreshSessions(string scene)
        {
            string repository = FrameMotionSourceTests.CopyRepository();
            try
            {
                var candidate = FrameMotionSourceTests.Candidate(repository); FrameMotionSource.Load(repository, out var hash);
                FrameMotionSource.Save(repository, hash, candidate); AttackMotionSource.Regenerate(repository);
                byte[] sourceBytes = File.ReadAllBytes(Path.Combine(repository, AttackMotionSource.RelativePath));
                byte[] generatedBytes = File.ReadAllBytes(Path.Combine(repository, AttackMotionSource.CatalogPath));
                Metrics first = default;
                for (int session = 0; session < 2; session++)
                {
                    // Inject the actual generated transport before Start. No authoring
                    // candidate, temporary apply API or post-start initializer is used.
                    LegacySceneFixture.Open(scene); var configured = Object.FindAnyObjectByType<CombatArena>();
                    configured.loadCharacterArt = false; configured.characterAnimations = FrameMotionSourceTests.Generated(repository);
                    yield return new EnterPlayMode(); yield return null;
                    Assert.That(Application.isPlaying, Is.True); var arena = Object.FindAnyObjectByType<CombatArena>();
                    Assert.That(arena.Player, Is.Not.Null); Assert.That(arena.FrameSwordTiming.ContentHash, Is.EqualTo(candidate.ContentHash));
                    Assert.That(arena.BasicSwordFirst.Impact, Is.EqualTo(.23f).Within(.000001f)); Assert.That(arena.BasicSwordFirst.Duration, Is.EqualTo(.43f).Within(.000001f));
                    Assert.That(arena.BasicSwordFirst.Active, Is.EqualTo(.08f).Within(.000001f)); Assert.That(arena.BasicSwordFirst.Damage, Is.EqualTo(10));
                    var floor = new GameObject("PermanentFrameFixtureFloor"); floor.transform.position = new Vector3(0, -.5f, 0);
                    floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1); floor.AddComponent<ArenaSurface>();
                    var texture = new Texture2D(6, 1); var sprites = Enumerable.Range(0, 6).Select(index => Sprite.Create(texture, new Rect(index, 0, 1, 1), Vector2.zero, 32)).ToArray();
                    try
                    {
                        BindSprites(arena, sprites); var result = Run(arena, sprites);
                        Assert.That(result.frameVersion, Is.EqualTo(candidate.ContentHash)); Assert.That(result.motionVersion, Is.EqualTo(candidate.CompileMotion(1).ContentHash));
                        Assert.That(result.impact, Is.EqualTo(.23f).Within(.000001f)); Assert.That(result.active, Is.EqualTo(.08f).Within(.000001f)); Assert.That(result.end, Is.EqualTo(.43f).Within(.000001f));
                        Assert.That(result.health, Is.EqualTo(90)); Assert.That(result.hits, Is.EqualTo(1)); Assert.That(result.firstHit, Is.EqualTo(11));
                        Assert.That(result.firstCue, Is.EqualTo(9), "The preserved 0.16129s Cue origin moves by the edited 0.03s ready frame.");
                        Assert.That(result.bodyAt100ms, Is.Zero, "The extended first frame is still visible at action time 0.10s."); Assert.That(result.completed, Is.EqualTo(21));
                        if (session == 0) first = result; else Assert.That(result, Is.EqualTo(first), "Both fresh Game/Lab sessions consume the same saved/generated action, body and Cue timeline.");
                    }
                    finally { arena.Presentation.Reset(); Object.Destroy(floor); foreach (var sprite in sprites) Object.Destroy(sprite); Object.Destroy(texture); }
                    yield return new ExitPlayMode();
                }
                Assert.That(File.ReadAllBytes(Path.Combine(repository, AttackMotionSource.RelativePath)), Is.EqualTo(sourceBytes));
                Assert.That(File.ReadAllBytes(Path.Combine(repository, AttackMotionSource.CatalogPath)), Is.EqualTo(generatedBytes));
            }
            finally { if (Directory.Exists(repository)) Directory.Delete(repository, true); }
        }
        [UnityTest] public IEnumerator InvalidGeneratedGraphFailsActualStartupBeforeAPlayerOrFixedFallbackExists()
        {
            string repository = FrameMotionSourceTests.CopyRepository();
            try
            {
                var candidate = FrameMotionSourceTests.Candidate(repository); FrameMotionSource.Load(repository, out var hash); FrameMotionSource.Save(repository, hash, candidate);
                AttackMotionSource.Regenerate(repository); var generated = FrameMotionSourceTests.Generated(repository);
                var contact = generated.motionSources[0].timeline.markers.Single(marker => marker.id == FrameTimingProjection.Contact);
                contact.anchor = TimelineAnchor.Marker; contact.reference = contact.id;
                LegacySceneFixture.Open("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity"); var configured = Object.FindAnyObjectByType<CombatArena>();
                configured.loadCharacterArt = false; configured.characterAnimations = generated;
                LogAssert.Expect(LogType.Error, new Regex("Invalid generated sword motion source: .*Marker cycle"));
                yield return new EnterPlayMode(); yield return null;
                var arena = Object.FindAnyObjectByType<CombatArena>(); Assert.That(arena.enabled, Is.False); Assert.That(arena.Player, Is.Null); Assert.That(arena.FrameSwordTiming, Is.Null);
                yield return new ExitPlayMode();
            }
            finally { if (Directory.Exists(repository)) Directory.Delete(repository, true); }
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator PermanentWindowSavesReloadsShowsDiffAndPreservesDraftOnInvalidConflictAndGenerationFailure()
        {
            string repository = FrameMotionSourceTests.CopyRepository();
            try
            {
                var window = Window(repository); yield return null; var root = window.rootVisualElement;
                string path = Path.Combine(repository, AttackMotionSource.RelativePath); byte[] original = File.ReadAllBytes(path);
                Assert.That(root.Q<Label>("result").text, Does.Contain("200.00 → 200.00").And.Contain("400.00 → 400.00"));
                Assert.That(root.Q<Label>("source").text, Does.Contain(FixedAttackMotion.Mode));
                root.Q<FloatField>("frame-sword-first-ready").value += .03f; yield return null;
                Assert.That(root.Q<Label>("result").text, Does.Contain("200.00 → 230.00").And.Contain("400.00 → 430.00")); Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
                Click(window, "saveSource"); yield return null;
                var saved = FrameMotionSource.ReadCandidate(repository, out _); Assert.That(saved.Action.Impact, Is.EqualTo(.23f).Within(.000001f));
                Assert.That(root.Q<Label>("source").text, Does.Contain(FrameAttackMotionSource.FrameMode));
                Assert.That(new FrameAttackMotionSource(FrameMotionSourceTests.Generated(repository).motionSources.Single()).Compile(saved.SourceAction, 2.5f).ContentHash, Is.EqualTo(saved.ContentHash));
                byte[] savedBytes = File.ReadAllBytes(path);
                root.Q<FloatField>("frame-sword-first-ready").value += .03f; yield return null;
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); yield return null;
                Assert.That(root.Q<FloatField>("frame-sword-first-ready").value, Is.EqualTo(saved.Timeline.Frames[0].Seconds)); Undo.PerformRedo(); yield return null;
                Assert.That(root.Q<FloatField>("frame-sword-first-ready").value, Is.EqualTo(saved.Timeline.Frames[0].Seconds + .03f)); Assert.That(File.ReadAllBytes(path), Is.EqualTo(savedBytes));
                root.Q<FloatField>("frame-sword-first-ready").value = 0; Assert.That(root.Q<Button>("saveSource").enabledSelf, Is.False);
                Assert.Catch<ArgumentException>(() => window.SavePermanentSource()); Assert.That(File.ReadAllBytes(path), Is.EqualTo(savedBytes));
                Click(window, "reload"); Assert.That(root.Q<Label>("result").text, Does.Contain("230.00 → 230.00").And.Contain("430.00 → 430.00"));
                File.WriteAllText(path, "{}"); Click(window, "reload");
                Assert.That(root.Q<FloatField>("frame-sword-first-ready").value, Is.EqualTo(saved.Timeline.Frames[0].Seconds));
                Assert.That(root.Q<Label>("result").text, Does.Contain("230.00 → 230.00"), "Failed reload preserves the last complete draft.");
                File.WriteAllBytes(path, savedBytes); Click(window, "reload"); root.Q<FloatField>("frame-sword-first-ready").value += .03f;
                File.AppendAllText(path, "\n"); byte[] external = File.ReadAllBytes(path); Click(window, "saveSource");
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(external));
                Assert.That(root.Q<Label>("result").text, Does.Contain("230.00 → 260.00"));
                Click(window, "reload"); root.Q<FloatField>("frame-sword-first-ready").value += .03f;
                string catalogPath = Path.Combine(repository, AttackMotionSource.CatalogPath); byte[] catalog = File.ReadAllBytes(catalogPath); File.Delete(catalogPath);
                Click(window, "saveSource"); Assert.That(File.Exists(catalogPath), Is.False);
                Assert.That(FrameMotionSource.ReadCandidate(repository, out _).Action.Impact, Is.EqualTo(.26f).Within(.000001f));
                File.WriteAllBytes(catalogPath, catalog); Click(window, "saveSource");
                Assert.That(new FrameAttackMotionSource(FrameMotionSourceTests.Generated(repository).motionSources.Single()).Compile(saved.SourceAction, 2.5f).Action.Impact, Is.EqualTo(.26f).Within(.000001f));
                window.Close(); window = Window(repository); yield return null;
                Assert.That(window.rootVisualElement.Q<Label>("result").text, Does.Contain("260.00 → 260.00").And.Contain("460.00 → 460.00"));
                CaptureRequestedWindow(window);
            }
            finally { CloseWindows(); if (Directory.Exists(repository)) Directory.Delete(repository, true); }
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator FixedEditorsKeepActiveFrameTimingReadOnlyWhileNumericDamageAndGeometryRemainEditable()
        {
            string repository = FrameMotionSourceTests.CopyRepository();
            try
            {
                var candidate = FrameMotionSourceTests.Candidate(repository); FrameMotionSource.Load(repository, out var hash); FrameMotionSource.Save(repository, hash, candidate);
                string path = Path.Combine(repository, AttackMotionSource.RelativePath); byte[] sourceBytes = File.ReadAllBytes(path);
                var fixedWindow = ScriptableObject.CreateInstance<AttackMotionWindow>(); fixedWindow.name = "PermanentFrameTestWindow";
                fixedWindow.Show(); fixedWindow.CreateGUI(); fixedWindow.LoadRepository(repository); yield return null;
                Assert.That(fixedWindow.rootVisualElement.Q<Button>("save").enabledSelf, Is.False);
                Assert.That(fixedWindow.rootVisualElement.Q<FloatField>("sourceImpact").enabledSelf, Is.False);
                fixedWindow.Close();
                var numeric = ScriptableObject.CreateInstance<BasicSwordWindow>(); numeric.name = "PermanentFrameTestWindow";
                numeric.Show(); numeric.CreateGUI(); numeric.LoadRepository(repository); yield return null; var root = numeric.rootVisualElement;
                foreach (string field in new[] { "duration", "impact", "active" }) Assert.That(root.Q<FloatField>(field).enabledSelf, Is.False, field);
                root.Q<FloatField>("damage").value = 17; root.Q<FloatField>("width").value += 1; Click(numeric, "save");
                var action = FrameMotionSource.ReadAction(repository); Assert.That(action.Damage, Is.EqualTo(17)); Assert.That(action.Width, Is.EqualTo(candidate.SourceAction.Width + 1));
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(sourceBytes)); var compiled = FrameMotionSource.ReadCandidate(repository, out _);
                Assert.That(compiled.Action.Impact, Is.EqualTo(.23f).Within(.000001f)); Assert.That(compiled.Action.Duration, Is.EqualTo(.43f).Within(.000001f)); Assert.That(compiled.Action.Active, Is.EqualTo(.08f).Within(.000001f));
                Assert.That(compiled.Action.Damage, Is.EqualTo(17)); Assert.That(compiled.Action.Width, Is.EqualTo(action.Width));
                var generated = JsonUtility.FromJson<Catalog>(File.ReadAllText(Path.Combine(repository, "unity/Assets/Deviant/Combat/Resources/MeleeCatalog.json")));
                Assert.That(generated.forms.Single(form => form.id == "base_sword").basics[0].damage, Is.EqualTo(17));
            }
            finally { CloseWindows(); if (Directory.Exists(repository)) Directory.Delete(repository, true); }
        }
        static void CaptureRequestedWindow(EditorWindow window)
        {
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-frameSourceScreenshot");
            if (index < 0 || index + 1 >= args.Length) return;
            string path = Path.GetFullPath(args[index + 1]); var rect = window.position;
            try
            {
                window.Focus(); window.Repaint(); int width = Mathf.RoundToInt(rect.width), height = Mathf.RoundToInt(rect.height);
                var pixels = UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(rect.position, width, height);
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                try { image.SetPixels(pixels); image.Apply(); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, image.EncodeToPNG()); }
                finally { Object.DestroyImmediate(image); }
            }
            catch (Exception exception) { TestContext.Progress.WriteLine("Editor window screenshot unavailable in this environment: " + exception.Message); }
        }
    }
}
