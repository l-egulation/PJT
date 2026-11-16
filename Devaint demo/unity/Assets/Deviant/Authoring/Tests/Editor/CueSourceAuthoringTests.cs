using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deviant.Combat;
using Deviant.Definitions;
using Deviant.Presentation;
using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Deviant.Authoring.Tests
{
    public class CueSourceAuthoringTests
    {
        const int Frequency = 48000;
        string directory, path, assetFolder;
        Sprite first, second;
        AudioClip original, replacement;
        CueSourceWindow window;
        CueSourceDocument Fixture() => new CueSourceDocument {
            schemaVersion = 1, id = "fixture.cue-source", cues = new[] {
                new CueSourceEntryDocument { id = "fixture.sprite", kind = "Sprite", frames = new[] {
                    new CueSourceFrameDocument { id = "fixture.frame.a", seconds = .25f, x = 1, y = 2, asset = CueSourceEditorAssets.Reference(first) },
                    new CueSourceFrameDocument { id = "fixture.frame.b", seconds = .25f, x = 3, y = 4, asset = CueSourceEditorAssets.Reference(second) }
                } },
                new CueSourceEntryDocument { id = "fixture.audio", kind = "Audio", clipId = "fixture.silent.pcm", sampleCount = Frequency, frequency = Frequency, audioAsset = CueSourceEditorAssets.Reference(original) }
            }
        };
        [SetUp] public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "deviant-cue-source-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            assetFolder = "Assets/__CueSourceFixture_" + Guid.NewGuid().ToString("N"); AssetDatabase.CreateFolder("Assets", Path.GetFileName(assetFolder));
            first = ImportSprite("first", Color.cyan); second = ImportSprite("second", Color.yellow);
            original = ImportAudio("original"); replacement = ImportAudio("replacement");
            path = Path.Combine(directory, "cues.json"); File.WriteAllText(path, CueSource.Serialize(Fixture()));
        }
        Sprite ImportSprite(string name, Color color)
        {
            string asset = assetFolder + "/" + name + ".png"; var texture = new Texture2D(2, 2);
            try { texture.SetPixels(Enumerable.Repeat(color, 4).ToArray()); texture.Apply(); File.WriteAllBytes(asset, texture.EncodeToPNG()); }
            finally { Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(asset); importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.spritePixelsPerUnit = 2; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(asset);
        }
        AudioClip ImportAudio(string name)
        {
            string asset = assetFolder + "/" + name + ".wav";
            // Generated silent technical PCM only; no authored product sound.
            using (var writer = new BinaryWriter(File.Create(asset)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + Frequency * 2); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(Frequency); writer.Write(Frequency * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(Frequency * 2); writer.Write(new byte[Frequency * 2]);
            }
            AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceSynchronousImport);
            var importer = (AudioImporter)AssetImporter.GetAtPath(asset); var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad; settings.compressionFormat = AudioCompressionFormat.PCM; settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings; importer.SaveAndReimport();
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(asset);
            Assert.That(clip, Is.Not.Null);
            bool loaded = clip.LoadAudioData();
            Assert.That(loaded && clip.loadState == AudioDataLoadState.Loaded && clip.samples == Frequency && clip.frequency == Frequency, Is.True,
                "Imported silent fixture must be ready before catalog composition: samples=" + clip.samples + " frequency=" + clip.frequency + " state=" + clip.loadState);
            return clip;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (window) window.Close(); window = null;
            if (Application.isPlaying) yield return new ExitPlayMode();
            if (!string.IsNullOrEmpty(assetFolder)) AssetDatabase.DeleteAsset(assetFolder);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        [Test] public void BothMediaPreserveExistingSemanticHashesDefensiveCopiesAndBindingProvenance()
        {
            var document = Fixture(); var source = CueSourceCompiler.Compile(document);
            var sprite = new PresentationCueDefinition("fixture.sprite", new[] { "fixture.frame.a", "fixture.frame.b" }, new[] { .25f, .25f }, new[] { 1f, 3f }, new[] { 2f, 4f });
            var audio = new AudioCueDefinition("fixture.audio", "fixture.silent.pcm", Frequency, Frequency);
            Assert.That(source.Read(sprite.Id).Definition.ContentHash, Is.EqualTo(sprite.ContentHash)); Assert.That(source.Read(audio.Id).Definition.ContentHash, Is.EqualTo(audio.ContentHash));
            document.cues[0].frames[0].seconds = 9; document.cues[0].frames[0].asset.guid = "";
            Assert.That(((PresentationCueDefinition)source.Read(sprite.Id).Definition).Seconds[0], Is.EqualTo(.25f)); Assert.That(source.Read(sprite.Id).SpriteAssets[0].IsAssigned, Is.True);
            var json = CueSource.Serialize(source.ToDocument()); var roundTrip = CueSourceJson.Compile(json);
            Assert.That(roundTrip.ContentHash, Is.EqualTo(source.ContentHash)); Assert.That(roundTrip.BindingHash, Is.EqualTo(source.BindingHash));
            var changed = source.ToDocument(); changed.cues[1].audioAsset = CueSourceEditorAssets.Reference(replacement); var next = CueSourceCompiler.Compile(changed);
            Assert.That(next.DefinitionHash, Is.EqualTo(source.DefinitionHash)); Assert.That(next.BindingHash, Is.Not.EqualTo(source.BindingHash)); Assert.That(next.ContentHash, Is.Not.EqualTo(source.ContentHash));
        }
        [Test] public void InvalidSchemaKindsIdsTimesReferencesAndCrossMediaFieldsRejectWholeCandidate()
        {
            foreach (Action<CueSourceDocument> mutate in new Action<CueSourceDocument>[] {
                d => d.schemaVersion = 99, d => d.id = "", d => d.cues = null, d => d.cues[1] = null, d => d.cues[1].id = d.cues[0].id,
                d => d.cues[0].kind = "sprite", d => d.cues[0].kind = "0", d => d.cues[0].frames = null, d => d.cues[0].frames[0] = null,
                d => d.cues[0].frames[0].seconds = 0, d => d.cues[0].frames[0].x = float.NaN, d => d.cues[0].frames[0].y = float.PositiveInfinity,
                d => d.cues[0].sampleCount = 1, d => d.cues[1].flipX = true, d => d.cues[1].frames = d.cues[0].frames,
                d => d.cues[1].sampleCount = 0, d => d.cues[1].frequency = 0, d => d.cues[1].clipId = "",
                d => d.cues[0].frames[0].asset.guid = "not-a-guid", d => d.cues[1].audioAsset.localFileId = 0
            }) { var document = Fixture(); mutate(document); Assert.Catch<ArgumentException>(() => CueSourceCompiler.Compile(document)); }
            var missing = Fixture(); missing.cues[1].audioAsset = null; var source = CueSourceCompiler.Compile(missing);
            Assert.That(source.Read("fixture.audio").AudioAsset.IsAssigned, Is.False, "Unassigned media is editable/savable, not publishable.");
            Assert.Throws<InvalidOperationException>(() => CueSourceBinding.Prepare(source, new CueSourceEditorAssets()));
            Assert.Throws<ArgumentException>(() => CueSourceJson.Compile(new string('x', CueSourceJson.MaximumFileBytes + 1)));
        }
        [Test] public void AtomicSaveGeneratedTextAssetAndExternalConflictPreserveCanonicalSource()
        {
            var loaded = CueSource.Load(path); var document = loaded.Compiled.ToDocument(); document.cues[0].frames[0].seconds = .5f;
            var saved = CueSource.Save(loaded, document); var fresh = CueSource.Load(path); Assert.That(fresh.FileHash, Is.EqualTo(saved.FileHash));
            string generated = Path.Combine(directory, "generated.json"); CueSource.Export(fresh, generated);
            Assert.That(File.ReadAllBytes(generated), Is.EqualTo(File.ReadAllBytes(path)));
            var asset = new TextAsset(File.ReadAllText(generated)); try { Assert.That(CueSourceJson.Compile(asset).ContentHash, Is.EqualTo(saved.Compiled.ContentHash)); } finally { Object.DestroyImmediate(asset); }
            string before = File.ReadAllText(path); document.cues[1].frequency = 0;
            Assert.Catch(() => CueSource.Save(fresh, document)); Assert.That(File.ReadAllText(path), Is.EqualTo(before));
            File.AppendAllText(path, "\n"); before = File.ReadAllText(path); Assert.Throws<IOException>(() => CueSource.Save(fresh, fresh.Compiled.ToDocument())); Assert.That(File.ReadAllText(path), Is.EqualTo(before));
            Assert.Throws<IOException>(() => CueSource.Export(fresh, generated)); Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
        }
        [Test] public void ExactTypedImportedReferencesNeverFallBackToOtherAssetsOrSubassets()
        {
            var assets = new CueSourceEditorAssets(); var source = CueSourceCompiler.Compile(Fixture());
            Assert.That(assets.ReadSprite(source.Cues[0].SpriteAssets[0]), Is.SameAs(first)); Assert.That(assets.ReadAudio(source.Cues[1].AudioAsset), Is.SameAs(original));
            Assert.Throws<InvalidOperationException>(() => assets.ReadAudio(source.Cues[0].SpriteAssets[0]));
            var reference = source.Cues[0].SpriteAssets[0]; Assert.Throws<InvalidOperationException>(() => assets.ReadSprite(new CueSourceAssetReference(reference.Guid, reference.LocalFileId + 1)));
            var transient = AudioClip.Create("TechnicalTransient", Frequency, 1, Frequency, false);
            try { Assert.Throws<ArgumentException>(() => CueSourceEditorAssets.Reference(transient)); } finally { Object.DestroyImmediate(transient); }
        }
        [Test] public void AtomicExplicitPublicationPinsOldClipAndRejectsMissingOrMismatchedCandidate()
        {
            var owner = new GameObject("CueSourcePublicationRoot");
            using (var factory = new AudioCueFactory(owner.transform))
            try
            {
                var document = Fixture(); document.cues = new[] { document.cues[1] }; var source = CueSourceCompiler.Compile(document);
                var catalog = new SequenceCueCatalog(audio: factory); var binding = new CueSourceBinding(catalog, new CueSourceEditorAssets());
                Assert.That(binding.TryPublish(source, out var error), Is.True, error); var captured = catalog.Capture(new[] { "fixture.audio" }); var version = catalog.Version;
                var changed = source.ToDocument(); changed.cues[0].audioAsset = CueSourceEditorAssets.Reference(replacement); var next = CueSourceCompiler.Compile(changed);
                Assert.That(binding.TryPublish(next, out error), Is.True, error); Assert.That(catalog.Version, Is.Not.EqualTo(version));
                AudioCueOutputBinding oldOutput = null, nextOutput = null;
                captured.Begin(source.Cues[0].Definition, new CueAnchor(0, 0, 1), result => oldOutput = (AudioCueOutputBinding)result.View);
                catalog.Capture(new[] { "fixture.audio" }).Begin(next.Cues[0].Definition, new CueAnchor(0, 0, 1), result => nextOutput = (AudioCueOutputBinding)result.View);
                Assert.That(oldOutput.Source.clip, Is.SameAs(original)); Assert.That(nextOutput.Source.clip, Is.SameAs(replacement)); oldOutput.Dispose(); nextOutput.Dispose();
                version = catalog.Version; var bad = next.ToDocument(); bad.cues[0].sampleCount--;
                Assert.That(binding.TryPublish(CueSourceCompiler.Compile(bad), out error), Is.False); Assert.That(error, Does.Contain("sample count")); Assert.That(catalog.Version, Is.EqualTo(version)); Assert.That(binding.Source, Is.SameAs(next));
                bad = next.ToDocument(); bad.cues[0].audioAsset = null; Assert.That(binding.TryPublish(CueSourceCompiler.Compile(bad), out error), Is.False); Assert.That(catalog.Version, Is.EqualTo(version));
                bad = next.ToDocument(); bad.cues[0].audioAsset = CueSourceEditorAssets.Reference(first);
                Assert.That(binding.TryPublish(CueSourceCompiler.Compile(bad), out error), Is.False); Assert.That(catalog.Version, Is.EqualTo(version));
                Assert.That(factory.LiveOutputCount, Is.Zero);
            }
            finally { Object.DestroyImmediate(owner); }
        }
        [Test] public void DestroyedPrivateAudioRootFailsClosedWithoutRedirectingCapturedOutput()
        {
            var scene = EditorSceneManager.NewPreviewScene(); var root = new GameObject("CueSourceExplicitAudioRoot"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            using (var factory = new AudioCueFactory(root.transform))
            try
            {
                var definition = new AudioCueDefinition("fixture.private-audio", "fixture.pcm", Frequency, Frequency);
                var catalog = new SequenceCueCatalog(audio: factory); catalog.Publish(SequenceCueMedia.Audio(definition, original)); var captured = catalog.Capture(new[] { definition.Id });
                AudioCueOutputBinding output = null; captured.Begin(definition, new CueAnchor(2, 3, 1), result => output = (AudioCueOutputBinding)result.View);
                Assert.That(output.gameObject.scene, Is.EqualTo(scene)); Assert.That(output.transform.parent, Is.EqualTo(root.transform)); Assert.That(root.transform.childCount, Is.EqualTo(1)); output.Dispose();
                Object.DestroyImmediate(root); Assert.Throws<InvalidOperationException>(() => catalog.Capture(new[] { definition.Id }));
                Assert.Throws<InvalidOperationException>(() => captured.Begin(definition, new CueAnchor(0, 0, 1), result => { })); Assert.That(factory.LiveOutputCount, Is.Zero);
            }
            finally { if (root) Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        }
        [Test] public void PrivateSpritePreviewScrubsExistingSamplesAndPinsRunningCandidateUntilRepeat()
        {
            var source = CueSourceCompiler.Compile(Fixture());
            using (var preview = new CueSourcePreview(source, new CueSourceEditorAssets()))
            {
                Assert.That(preview.TryStart("fixture.sprite", out var error), Is.True, error); var firstHandle = preview.Current;
                Assert.That(preview.SpriteOutput.gameObject.scene, Is.EqualTo(preview.PreviewScene)); Assert.That(preview.SpriteOutput.sprite, Is.SameAs(first));
                preview.Seek(.3f); Assert.That(preview.SpriteOutput.sprite, Is.SameAs(second)); preview.Seek(.1f); Assert.That(preview.SpriteOutput.sprite, Is.SameAs(first));
                var changed = source.ToDocument(); changed.cues[0].frames[0].seconds = .5f; var next = CueSourceCompiler.Compile(changed);
                Assert.That(preview.TryPublish(next, out error), Is.True, error); Assert.That(firstHandle.DefinitionVersion, Is.EqualTo(source.Cues[0].Definition.ContentHash)); Assert.That(preview.StartedSourceVersion, Is.EqualTo(source.ContentHash));
                preview.Cancel(); Assert.That(firstHandle.Status, Is.EqualTo(CueStatus.Cancelled)); Assert.That(preview.ActiveCount, Is.Zero); Assert.That(preview.LiveSpriteCount, Is.Zero);
                Assert.Throws<InvalidOperationException>(() => preview.Seek(0)); Assert.That(preview.TryStart("fixture.sprite", out error), Is.True, error);
                Assert.That(preview.StartedSourceVersion, Is.EqualTo(next.ContentHash)); preview.Seek(.3f); Assert.That(preview.SpriteOutput.sprite, Is.SameAs(first)); preview.Seek(.75f);
                Assert.That(preview.Current.Status, Is.EqualTo(CueStatus.Completed)); Assert.That(preview.LiveSpriteCount, Is.Zero);
            }
        }
        [UnityTest, Category("RequiresGraphics")] public IEnumerator WindowEditsUndoSaveReloadAndExplicitAssetPreview()
        {
            window = ScriptableObject.CreateInstance<CueSourceWindow>(); window.Show(); window.CreateGUI(); window.LoadFile(path); yield return null;
            var root = window.rootVisualElement; string before = File.ReadAllText(path);
            root.Q<FloatField>("cue-0-frame-0-seconds").value = .5f; Assert.That(root.Q<Label>("state").text, Does.Contain("Unsaved")); Assert.That(File.ReadAllText(path), Is.EqualTo(before));
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); yield return null; Assert.That(window.Document.cues[0].frames[0].seconds, Is.EqualTo(.25f));
            root.Q<FloatField>("cue-0-frame-0-seconds").value = .5f; Click("save"); Click("reload"); Assert.That(window.Saved.Compiled.ContentHash, Is.EqualTo(window.CompileCandidate().ContentHash));
            Click("startPreview"); Assert.That(window.Preview.Current.Status, Is.EqualTo(CueStatus.Playing)); root.Q<Slider>("scrub").value = .6f;
            Assert.That(window.Preview.SpriteOutput.sprite, Is.SameAs(second)); Click("cancelPreview"); Assert.That(window.Preview.ActiveCount, Is.Zero);
            root.Q<FloatField>("cue-0-frame-0-seconds").value = .75f; Click("publishPreview"); Click("repeatPreview"); Assert.That(window.Preview.StartedSourceVersion, Is.EqualTo(window.CompileCandidate().ContentHash));
            var assetField = root.Q<UnityEditor.UIElements.ObjectField>("cue-0-frame-0-asset"); assetField.value = second;
            Assert.That(window.Document.cues[0].frames[0].asset.guid, Is.EqualTo(CueSourceEditorAssets.Reference(second).guid));
            Assert.That(root.Q<Button>("publishRuntime").enabledSelf, Is.False, "Runtime catalogs are never discovered automatically.");
        }
        [UnityTest] public IEnumerator PrivateAudioPreviewUsesActualSourceHoldResumeBackwardCancelAndVersion()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            var source = CueSourceCompiler.Compile(Fixture());
            using (var preview = new CueSourcePreview(source, new CueSourceEditorAssets()))
            {
                Assert.That(preview.TryStart("fixture.audio", out var error), Is.True, error); var output = preview.AudioOutput; var audio = output.Source;
                Assert.That(audio.gameObject.scene, Is.EqualTo(preview.PreviewScene)); Assert.That(audio.clip, Is.SameAs(original)); Assert.That(audio.timeSamples, Is.Zero); Assert.That(audio.isPlaying, Is.False);
                preview.Seek(.25f); Assert.That(audio.isPlaying, Is.True); preview.Hold(); Assert.That(audio.timeSamples, Is.EqualTo(12000)); Assert.That(audio.isPlaying, Is.False);
                preview.Seek(.125f); Assert.That(audio.timeSamples, Is.EqualTo(6000)); Assert.That(audio.isPlaying, Is.False); preview.Seek(.5f); Assert.That(audio.isPlaying, Is.True); preview.Hold(); Assert.That(audio.timeSamples, Is.EqualTo(24000));
                var changed = source.ToDocument(); changed.cues[1].audioAsset = CueSourceEditorAssets.Reference(replacement); var next = CueSourceCompiler.Compile(changed);
                Assert.That(preview.TryPublish(next, out error), Is.True, error); Assert.That(audio.clip, Is.SameAs(original)); preview.Cancel();
                Assert.That(output.IsDisposed, Is.True); Assert.That(audio.clip, Is.Null); Assert.That(audio.enabled, Is.False); Assert.That(audio.gameObject.activeSelf, Is.False);
                Assert.That(preview.TryStart("fixture.audio", out error), Is.True, error); Assert.That(preview.AudioOutput.Source.clip, Is.SameAs(replacement)); Assert.That(preview.StartedSourceVersion, Is.EqualTo(next.ContentHash));
                preview.Seek(1); Assert.That(preview.Current.Status, Is.EqualTo(CueStatus.Completed)); Assert.That(preview.LiveAudioCount, Is.Zero);
            }
            var runtimeRoot = new GameObject("CueSourceExplicitRuntimeFixture");
            using (var factory = new AudioCueFactory(runtimeRoot.transform))
            try
            {
                var document = Fixture(); document.cues = new[] { document.cues[1] }; var candidate = CueSourceCompiler.Compile(document);
                var runtimeCatalog = new SequenceCueCatalog(audio: factory);
                var runtimeBinding = new CueSourceBinding(runtimeCatalog, new CueSourceEditorAssets());
                window = ScriptableObject.CreateInstance<CueSourceWindow>(); window.Show(); window.CreateGUI();
                File.WriteAllText(path, CueSource.Serialize(document)); window.LoadFile(path); window.BindRuntime(runtimeBinding);
                Click("publishRuntime"); Assert.That(runtimeBinding.Source.ContentHash, Is.EqualTo(candidate.ContentHash));
                var version = runtimeCatalog.Version; window.SaveSource(); Assert.That(runtimeCatalog.Version, Is.EqualTo(version), "Saving never publishes.");
            }
            finally { Object.DestroyImmediate(runtimeRoot); }
        }
        [UnityTest] public IEnumerator GamePrivateCueAuthoringDoesNotChangeSimulationOrControl() => SceneIsolation("Assets/Deviant/Game/Scenes/deviant.unity");
        [UnityTest] public IEnumerator LabPrivateCueAuthoringDoesNotChangeSimulationOrControl() => SceneIsolation("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity");
        IEnumerator SceneIsolation(string scene)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false; yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>();
            using (var driver = arena.AcquireManualSimulation())
            {
                float time = arena.Now, health = arena.Player.health; var position = arena.Player.Position; long revision = arena.Controls.Current.Revision;
                using (var preview = new CueSourcePreview(CueSourceCompiler.Compile(Fixture()), new CueSourceEditorAssets()))
                {
                    Assert.That(preview.TryStart("fixture.sprite", out var error), Is.True, error); preview.Seek(.5f);
                    Assert.That(preview.TryStart("fixture.audio", out error), Is.True, error); preview.Seek(.25f); preview.Hold(); preview.Cancel();
                    Assert.That(preview.ActiveCount, Is.Zero); Assert.That(preview.LiveAudioCount, Is.Zero); Assert.That(preview.LiveSpriteCount, Is.Zero);
                }
                Assert.That(arena.Now, Is.EqualTo(time)); Assert.That(arena.Player.health, Is.EqualTo(health)); Assert.That(arena.Player.Position, Is.EqualTo(position)); Assert.That(arena.Controls.Current.Revision, Is.EqualTo(revision));
            }
        }
        void Click(string name)
        {
            var button = window.rootVisualElement.Q<Button>(name); Assert.That(button, Is.Not.Null, name); Assert.That(button.enabledSelf, Is.True, name);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
        }
    }
}
