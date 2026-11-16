using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Deviant.Replay.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Deviant.Replay.Tests
{
    public class ReplayUiIntegrationTests
    {
        const string RecordingPreference = "deviant.replay.local-consent.v1";
        const string LegacyUploadPreference = "deviant.replay.upload-consent.v1";
        const BindingFlags InstanceFields = BindingFlags.NonPublic | BindingFlags.Instance;

        sealed class RecordingFixture : IDisposable
        {
            readonly bool hadRecording = PlayerPrefs.HasKey(RecordingPreference), hadUpload = PlayerPrefs.HasKey(LegacyUploadPreference);
            readonly int savedRecording = PlayerPrefs.GetInt(RecordingPreference), savedUpload = PlayerPrefs.GetInt(LegacyUploadPreference);
            readonly GameObject holder = new GameObject("Automatic recording fixture");
            public readonly string DirectoryPath = Path.Combine(Path.GetTempPath(), "automatic-replay-" + Guid.NewGuid().ToString("N"));
            public readonly Type HostType = Type.GetType("Deviant.Game.ReplayRecordingHost, Assembly-CSharp", true);
            public readonly Component Host;
            public RecordingFixture(int? recordingPreference = null, int legacyUploadPreference = 0)
            {
                if (recordingPreference.HasValue) PlayerPrefs.SetInt(RecordingPreference, recordingPreference.Value);
                else PlayerPrefs.DeleteKey(RecordingPreference);
                PlayerPrefs.SetInt(LegacyUploadPreference, legacyUploadPreference);
                holder.SetActive(false); Host = holder.AddComponent(HostType);
                // Inject local-only settings so these tests never contact the production collector or its queue.
                Call("InitializeRecording", ""); holder.SetActive(true);
                Set("store", new ReplayStore(DirectoryPath)); Set("scenePath", "fixture");
            }
            public object Get(string name) => HostType.GetField(name, InstanceFields).GetValue(Host);
            public void Set(string name, object value) => HostType.GetField(name, InstanceFields).SetValue(Host, value);
            public void Call(string name, params object[] args) => HostType.GetMethod(name, InstanceFields | BindingFlags.Public).Invoke(Host, args);
            public ReplayFile Commit()
            {
                if (Get("segment") == null) Set("segment", ReplayUuid7.NewId());
                Call("BeginChunk"); Call("CloseChunk");
                var file = ((Task<ReplayFile>)Get("pending")).GetAwaiter().GetResult(); Call("CompletePending"); return file;
            }
            public void Dispose()
            {
                try
                {
                    (Get("pending") as Task<ReplayFile>)?.GetAwaiter().GetResult();
                    Set("recorder", null); Set("pending", null); Object.DestroyImmediate(holder);
                    if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, true);
                }
                finally
                {
                    if (hadRecording) PlayerPrefs.SetInt(RecordingPreference, savedRecording); else PlayerPrefs.DeleteKey(RecordingPreference);
                    if (hadUpload) PlayerPrefs.SetInt(LegacyUploadPreference, savedUpload); else PlayerPrefs.DeleteKey(LegacyUploadPreference);
                    PlayerPrefs.Save();
                }
            }
        }

        [TestCase(0)] [TestCase(1)]
        public void RecordingStartsEnabledAndNewFilesGetUploadBindingWithoutSeparateUploadChoice(int legacyUpload)
        {
            using (var fixture = new RecordingFixture(legacyUploadPreference: legacyUpload))
            {
                Assert.That(fixture.Get("recordingEnabled"), Is.True);
                var oldLocalFile = fixture.Commit();
                Assert.That(File.Exists(oldLocalFile.Path + ".collection.json"), Is.False);
                string player = Guid.NewGuid().ToString("N");
                fixture.Set("collectorEndpoint", "http://127.0.0.1:18099"); fixture.Set("playerId", player);
                var uploadedFile = fixture.Commit();
                var binding = JsonUtility.FromJson<ReplayUploadBinding>(File.ReadAllText(uploadedFile.Path + ".collection.json"));
                Assert.That(binding.endpoint, Is.EqualTo("http://127.0.0.1:18099"));
                Assert.That(binding.session.player_id, Is.EqualTo(player));
                Assert.That(binding.session.session_id, Is.EqualTo(uploadedFile.Chunk.Header.SessionId));
                Assert.That(File.Exists(oldLocalFile.Path + ".collection.json"), Is.False, "Older local-only recordings are not rebound.");
            }
        }

        [TestCase(0)] [TestCase(1)]
        public void ExplicitRecordingPreferenceIsPreservedRegardlessOfLegacyUploadChoice(int legacyUpload)
        {
            using (var fixture = new RecordingFixture(0, legacyUpload))
            {
                Assert.That(fixture.Get("recordingEnabled"), Is.False);
                Assert.That(fixture.Host.GetComponent<ReplayUploadClient>(), Is.Null);
            }
        }

        [Test] public void StoppingRecordingClearsPendingFilesAndPersistsTheStop()
        {
            using (var fixture = new RecordingFixture())
            {
                var file = fixture.Commit(); Assert.That(File.Exists(file.Path), Is.True);
                fixture.Call("SetRecordingEnabled", false);
                Assert.That(fixture.Get("recordingEnabled"), Is.False);
                Assert.That(File.Exists(file.Path), Is.False);
                Assert.That(PlayerPrefs.GetInt(RecordingPreference), Is.EqualTo(0));
                fixture.Call("SetRecordingEnabled", true);
                Assert.That(fixture.Get("recordingEnabled"), Is.True);
                Assert.That(PlayerPrefs.GetInt(RecordingPreference), Is.EqualTo(1));
            }
        }

        [Test] public void ActualRuntimeHostRecordsUiEventsAndNoDisplayOrTypedText()
        {
            string directory = Path.Combine(Path.GetTempPath(), "replay-ui-" + Guid.NewGuid().ToString("N"));
            var root = new GameObject("Replay integration", typeof(RectTransform), typeof(Canvas));
            var events = new GameObject("Events", typeof(EventSystem));
            var holder = new GameObject("Recorder");
            var type = Type.GetType("Deviant.Game.ReplayRecordingHost, Assembly-CSharp", true);
            var host = holder.AddComponent(type);
            void Set(string name, object value) => type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(host, value);
            object Get(string name) => type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(host);
            void Call(string name) => type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(host, null);
            try
            {
                type.GetMethod("InitializeRecording", InstanceFields).Invoke(host, new object[] { "" });
                Set("store", new ReplayStore(directory)); Set("segment", Guid.NewGuid().ToString("N"));
                Set("scenePath", "fixture"); Set("origin", Time.realtimeSinceStartupAsDouble); Set("focused", true);
                var button = new GameObject("PRIVATE DISPLAY TEXT", typeof(RectTransform), typeof(Image), typeof(Button)); button.transform.SetParent(root.transform);
                var marker = ReplayUiTarget.Attach(button, "inventory.slot_1");
                var field = new GameObject("Input", typeof(RectTransform), typeof(InputField)); field.transform.SetParent(root.transform);
                field.GetComponent<InputField>().text = "private@example.invalid";
                ReplayUiTarget.Attach(field, "inventory.input");
                Call("BeginChunk");
                ExecuteEvents.Execute(button, new PointerEventData(events.GetComponent<EventSystem>()), ExecuteEvents.pointerEnterHandler);
                events.GetComponent<EventSystem>().SetSelectedGameObject(button);
                marker.Activated();
                Call("CloseChunk"); var file = ((Task<ReplayFile>)Get("pending")).GetAwaiter().GetResult();
                Assert.That(file.Chunk.Records.Any(r => r.Kind == ReplayKind.UiActivate), Is.True);
                Assert.That(file.Chunk.Records.Any(r => r.Kind == ReplayKind.UiState && r.Values[0] == 0 && r.Values[1] > 0), Is.True);
                Assert.That(file.Chunk.Records.Any(r => r.Kind == ReplayKind.UiState && r.Values[0] == 1 && r.Values[1] > 0), Is.True);
                var text = string.Join("\n", file.Chunk.Records.Select(r => r.Text));
                Assert.That(text, Does.Not.Contain("PRIVATE DISPLAY TEXT")); Assert.That(text, Does.Not.Contain("private@example"));
                Assert.That(events.GetComponent<EventSystem>().currentSelectedGameObject, Is.EqualTo(button));
            }
            finally
            {
                Set("recorder", null); Set("pending", null); Object.DestroyImmediate(holder); Object.DestroyImmediate(root); Object.DestroyImmediate(events);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
        [Test] public void StructuralRegistrationDoesNotExposeObjectNamesAndActivationIsObservedOnce()
        {
            var root = new GameObject("Root"); var child = new GameObject("private name", typeof(RectTransform), typeof(Button)); child.transform.SetParent(root.transform);
            int activations = 0; Action<ReplayUiTarget, int> listener = (target, kind) => { if (kind == 6) activations++; };
            ReplayUiTarget.Observed += listener;
            try
            {
                ReplayUiTarget.RegisterTree(root.transform); ReplayUiTarget.RegisterTree(root.transform);
                Assert.That(child.GetComponent<ReplayUiTarget>().Id, Is.EqualTo("ui.structural.0"));
                child.GetComponent<Button>().onClick.Invoke(); Assert.That(activations, Is.EqualTo(1));
            }
            finally { ReplayUiTarget.Observed -= listener; Object.DestroyImmediate(root); }
        }
    }
}
