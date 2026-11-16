using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Deviant.Replay.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Replay.Tests
{
    public class ReplayCollectorIntegrationTests
    {
        // Use a disposable collector: this creates one random synthetic player.
        [UnityTest] public IEnumerator RealCollectorCommitsBeforeClientRemovesRawFileAndSyncsSessionStatus()
        {
            string endpoint = Environment.GetEnvironmentVariable("DEVIANT_COLLECTOR_TEST_URL");
            if (string.IsNullOrEmpty(endpoint)) Assert.Ignore("Set DEVIANT_COLLECTOR_TEST_URL to a disposable Go collector.");
            endpoint = ReplayUploadBinding.NormalizeEndpoint(endpoint);
            string root = Path.Combine(Path.GetTempPath(), "collector-integration-" + Guid.NewGuid().ToString("N"));
            var go = new GameObject("Replay upload integration test"); var stack = new Stack<IEnumerator>();
            try
            {
                var header = new ReplayHeader { SessionId = ReplayUuid7.NewId(), SegmentId = ReplayUuid7.NewId(), BuildId = "integration", ContentHash = "integration" };
                var binding = new ReplayUploadBinding { endpoint = endpoint, session = new ReplayCollectionSession
                { player_id = Guid.NewGuid().ToString("N"), session_id = header.SessionId, build_id = header.BuildId, platform = "editor", started_at = DateTimeOffset.UtcNow.ToString("O") } };
                var recorder = new ReplayRecorder(0); int scene = recorder.Intern("Assets/Synthetic.unity", 0);
                recorder.Add(ReplayKind.Scene, 0, -1, scene, 0); recorder.Add(ReplayKind.SnapshotEnd, 0, -1);
                recorder.Add(ReplayKind.UiActivate, 100, -1, scene, 0);
                var store = new ReplayStore(root); var file = store.Commit(header, recorder.Finish(), binding);
                long last = file.Chunk.Records[file.Chunk.Records.Count - 1].Sequence;
                store.MarkSession(header.SessionId, 100, last, true, false);
                var client = go.AddComponent<ReplayUploadClient>();
                // EditMode does not run MonoBehaviour coroutines. Drive the same network loop with editor updates.
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(ReplayUploadClient).GetField("root", flags).SetValue(client, root);
                typeof(ReplayUploadClient).GetField("endpoint", flags).SetValue(client, endpoint);
                stack.Push((IEnumerator)typeof(ReplayUploadClient).GetMethod("Loop", flags).Invoke(client, null));
                double deadline = UnityEditor.EditorApplication.timeSinceStartup + 25;
                string acknowledgement = Path.Combine(root, header.SessionId, "session.json.uploaded");
                while (!File.Exists(acknowledgement) && UnityEditor.EditorApplication.timeSinceStartup < deadline)
                {
                    var top = stack.Peek();
                    if (!top.MoveNext()) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (top.Current is AsyncOperation operation) { while (!operation.isDone) yield return null; }
                    else if (top.Current is IEnumerator nested) stack.Push(nested);
                    else yield return null;
                }
                Assert.That(client.LastError, Is.Null); Assert.That(client.UploadedChunks, Is.EqualTo(1));
                Assert.That(File.Exists(file.Path), Is.False, "Deletion requires the real server's committed SHA receipt.");
                Assert.That(File.Exists(acknowledgement), Is.True);
            }
            finally
            {
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                UnityEngine.Object.DestroyImmediate(go);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
