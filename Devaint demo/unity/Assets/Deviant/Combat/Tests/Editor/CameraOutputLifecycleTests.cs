using System;
using System.Collections;
using Deviant.Control.Contracts;
using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class CameraOutputLifecycleTests
    {
        sealed class Source : ICameraPositionSource
        {
            public Action OnRead;
            public CameraPosition Read() { OnRead?.Invoke(); return P(90); }
        }
        static CameraPosition P(float x) => new CameraPosition(x, x + 1, -10);
        static Vector3 V(float x) => new Vector3(x, x + 1, -10);
        [UnityTest] public IEnumerator DisabledOutputClosesCachedRequestsAndResumesTheCurrentBaseline()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene); yield return new EnterPlayMode(); yield return null;
            Assert.That(Application.isPlaying, Is.True);
            var root = new GameObject("CameraOutputLifetime"); var camera = root.AddComponent<Camera>();
            try
            {
                var output = CameraOutputBinding.GetOrCreate(camera); var baseline = output.BindBaseSource("follow"); baseline.Submit(P(1)); output.ApplyNow();
                var cached = output.Requests; var owner = cached.OpenScope("old"); var handle = owner.Request(1, new FixedCameraPosition(P(10))); output.ApplyNow();
                output.enabled = false;
                Assert.That(handle.Status, Is.EqualTo(CameraRequestStatus.Cancelled)); Assert.That(owner.IsActive, Is.False);
                Assert.Throws<ObjectDisposedException>(() => cached.OpenScope("stale")); Assert.Throws<InvalidOperationException>(() => output.Requests.OpenScope("disabled"));
                baseline.Submit(P(20)); output.ApplyNow(); Assert.That(root.transform.position, Is.EqualTo(V(10))); Assert.That(baseline.IsActive, Is.True);
                output.enabled = true; var current = output.Requests; Assert.That(current.SessionId, Is.Not.EqualTo(cached.SessionId)); output.ApplyNow(); Assert.That(root.transform.position, Is.EqualTo(V(20)));
                using (var next = current.OpenScope("new"))
                {
                    var active = next.Request(1, new FixedCameraPosition(P(30))); owner.Dispose(); handle.Complete(); output.ApplyNow();
                    Assert.That(active.Status, Is.EqualTo(CameraRequestStatus.Active)); Assert.That(root.transform.position, Is.EqualTo(V(30)));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }
        [UnityTest] public IEnumerator CameraAndHierarchyAvailabilityCannotRevivePreviousRequests()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene); yield return new EnterPlayMode(); yield return null;
            Assert.That(Application.isPlaying, Is.True);
            var root = new GameObject("CameraAvailability"); var camera = root.AddComponent<Camera>();
            try
            {
                var output = CameraOutputBinding.GetOrCreate(camera); var baseline = output.BindBaseSource("follow"); baseline.Submit(P(1));
                var old = output.Requests; var scope = old.OpenScope("old"); var handle = scope.Request(1, new FixedCameraPosition(P(10))); output.ApplyNow();
                camera.enabled = false; output.ApplyNow(); Assert.That(handle.Status, Is.EqualTo(CameraRequestStatus.Cancelled)); Assert.That(root.transform.position, Is.EqualTo(V(10)));
                Assert.Throws<InvalidOperationException>(() => output.Requests.OpenScope("unavailable")); camera.enabled = true;
                var current = output.Requests; Assert.That(current.SessionId, Is.Not.EqualTo(old.SessionId)); var next = current.OpenScope("next"); var active = next.Request(1, new FixedCameraPosition(P(20))); output.ApplyNow();
                root.SetActive(false); Assert.That(active.Status, Is.EqualTo(CameraRequestStatus.Cancelled)); root.SetActive(true);
                Assert.That(output.Requests.SessionId, Is.Not.EqualTo(current.SessionId)); baseline.Submit(P(3)); output.ApplyNow(); Assert.That(root.transform.position, Is.EqualTo(V(3)));
                scope.Dispose(); next.Dispose(); Assert.That(output.Requests.ActiveCount, Is.Zero);
            }
            finally { Object.DestroyImmediate(root); }
        }
        [UnityTest] public IEnumerator SourceDisableReenableCannotWriteItsOldFrame() => SourceEndingOutput(false);
        [UnityTest] public IEnumerator SourceDestructionCannotWriteItsOldFrame() => SourceEndingOutput(true);
        static IEnumerator SourceEndingOutput(bool destroy)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene); yield return new EnterPlayMode(); yield return null;
            Assert.That(Application.isPlaying, Is.True);
            var root = new GameObject("CameraReadReentry"); root.AddComponent<Camera>();
            try
            {
                var output = root.AddComponent<CameraOutputBinding>(); int observed = 0;
                var baseline = output.BindBaseSource("follow", () => observed++); baseline.Submit(P(1)); output.ApplyNow();
                var cached = output.Requests; var owner = cached.OpenScope("source");
                var handle = owner.Request(10, new Source { OnRead = () => { if (destroy) Object.DestroyImmediate(root); else { output.enabled = false; output.enabled = true; } } }); output.ApplyNow();
                Assert.That(observed, Is.EqualTo(1)); Assert.That(handle.Status, Is.EqualTo(CameraRequestStatus.Cancelled)); Assert.That(output.LastApplyError, Is.Null);
                if (destroy) { Assert.That(baseline.IsActive, Is.False); Assert.Throws<ObjectDisposedException>(() => cached.OpenScope("destroyed")); }
                else
                {
                    Assert.That(root.transform.position, Is.EqualTo(V(1))); Assert.That(output.Requests.SessionId, Is.Not.EqualTo(cached.SessionId));
                    baseline.Submit(P(2)); output.ApplyNow(); Assert.That(root.transform.position, Is.EqualTo(V(2))); Assert.That(observed, Is.EqualTo(2));
                }
                owner.Dispose();
            }
            finally { if (root) Object.DestroyImmediate(root); }
        }
        [UnityTest] public IEnumerator ObserverDisableReenableAndDestructionReturnRequestsAfterResolution()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene); yield return new EnterPlayMode(); yield return null;
            Assert.That(Application.isPlaying, Is.True);
            var root = new GameObject("CameraObserverReentry"); root.AddComponent<Camera>();
            try
            {
                var output = root.AddComponent<CameraOutputBinding>(); Action observer = null;
                var baseline = output.BindBaseSource("follow", () => observer?.Invoke()); baseline.Submit(P(1));
                var cached = output.Requests; var owner = cached.OpenScope("first"); var handle = owner.Request(1, new FixedCameraPosition(P(10)));
                observer = () => { output.enabled = false; output.enabled = true; }; output.ApplyNow();
                Assert.That(handle.Status, Is.EqualTo(CameraRequestStatus.Cancelled)); Assert.That(output.LastApplyError, Is.Null); Assert.That(root.transform.position, Is.EqualTo(V(10)));
                var next = output.Requests.OpenScope("destroy"); var active = next.Request(1, new FixedCameraPosition(P(20))); observer = () => Object.DestroyImmediate(root); output.ApplyNow();
                Assert.That(active.Status, Is.EqualTo(CameraRequestStatus.Cancelled)); Assert.That(next.IsActive, Is.False); Assert.That(baseline.IsActive, Is.False); Assert.That(output.LastApplyError, Is.Null);
                Assert.Throws<ObjectDisposedException>(() => cached.OpenScope("stale")); owner.Dispose(); next.Dispose();
            }
            finally { if (root) Object.DestroyImmediate(root); }
        }
        [UnityTest] public IEnumerator GameResumesItsLiveFollowWithoutReleasingOtherControl() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", true);
        [UnityTest] public IEnumerator LabResumesItsLiveFollowWithoutReleasingOtherControl() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", false);
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static IEnumerator Exercise(string scene, bool forest)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var camera = Camera.main; var output = camera.GetComponent<CameraOutputBinding>();
            Action follow;
            if (forest)
            {
                var rig = camera.GetComponent("InkForestCamera"); follow = (Action)Delegate.CreateDelegate(typeof(Action), rig, rig.GetType().GetMethod("SnapToTarget"));
            }
            else follow = () => arena.GetComponent("LabComposition").SendMessage("Follow");
            using (var driver = arena.AcquireManualSimulation()) using (var modal = arena.Controls.OpenScope("independent modal"))
            {
                var block = modal.BlockInput(InputChannels.Combat); var pause = modal.PauseSimulation();
                float now = arena.Now, health = arena.Player.health, size = camera.orthographicSize; bool orthographic = camera.orthographic;
                var cached = arena.CameraRequests; var scope = cached.OpenScope("sequence camera"); var handle = scope.Request(10, new FixedCameraPosition(P(30))); output.ApplyNow();
                output.enabled = false; arena.Player.Position = new Vector2(20, 110); follow();
                Assert.That(handle.Status, Is.EqualTo(CameraRequestStatus.Cancelled)); Assert.That(camera.transform.position, Is.EqualTo(V(30))); Assert.That(scope.IsActive, Is.False);
                output.enabled = true; output.ApplyNow(); Assert.That(camera.transform.position, Is.EqualTo(forest ? new Vector3(18.75f, 109.0625f, -10) : new Vector3(22, 112, -10)));
                using (var next = arena.CameraRequests.OpenScope("current camera"))
                {
                    var current = next.Request(20, new FixedCameraPosition(P(40))); scope.Dispose(); handle.Complete(); output.ApplyNow();
                    Assert.That(current.Status, Is.EqualTo(CameraRequestStatus.Active)); Assert.That(camera.transform.position, Is.EqualTo(V(40)));
                    camera.enabled = false; output.ApplyNow(); Assert.That(current.Status, Is.EqualTo(CameraRequestStatus.Cancelled)); camera.enabled = true; output.ApplyNow();
                }
                Assert.That(block.IsActive && pause.IsActive, Is.True); Assert.That(arena.Player.InputBlocked, Is.True); Assert.That(arena.Controls.Current.SimulationPaused, Is.True);
                Assert.That(arena.Now, Is.EqualTo(now)); Assert.That(arena.Player.health, Is.EqualTo(health)); Assert.That(arena.Player.Position, Is.EqualTo(new Vector2(20, 110)));
                Assert.That(camera.orthographicSize, Is.EqualTo(size)); Assert.That(camera.orthographic, Is.EqualTo(orthographic)); Assert.That(output.LastApplyError, Is.Null);
            }
        }
    }
}
