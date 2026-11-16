using System;
using System.Collections;
using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Combat.Tests
{
    public class CameraRequestIntegrationTests
    {
        sealed class Source : ICameraPositionSource
        {
            public CameraPosition Position;
            public CameraPosition Read() => Position;
        }
        [Test] public void OutputHasOneBaselineOwnerAndDoesNotRestoreOverAnotherRequest()
        {
            var root = new GameObject("CameraBindingFixture"); var camera = root.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 6;
            try
            {
                var binding = CameraOutputBinding.GetOrCreate(camera); var source = binding.BindBaseSource("first");
                Assert.That(CameraOutputBinding.GetOrCreate(camera), Is.SameAs(binding)); Assert.Throws<InvalidOperationException>(() => binding.BindBaseSource("second"));
                source.Submit(new CameraPosition(3, 4, -10)); binding.ApplyNow(); Assert.That(root.transform.position, Is.EqualTo(new Vector3(3, 4, -10)));
                using (var owner = binding.Requests.OpenScope("override"))
                {
                    var request = owner.Request(10, new FixedCameraPosition(new CameraPosition(20, 30, -10)));
                    source.Submit(new CameraPosition(40, 50, -10)); source.Dispose(); binding.ApplyNow();
                    Assert.That(root.transform.position, Is.EqualTo(new Vector3(20, 30, -10))); Assert.That(request.Status, Is.EqualTo(CameraRequestStatus.Active));
                    request.Complete(); binding.ApplyNow(); Assert.That(root.transform.position, Is.EqualTo(new Vector3(40, 50, -10)));
                    var next = binding.BindBaseSource("new"); next.Submit(new CameraPosition(60, 70, -10)); source.Dispose(); binding.ApplyNow();
                    Assert.That(root.transform.position, Is.EqualTo(new Vector3(60, 70, -10)), "A stale base lease cannot release the new owner.");
                    Assert.That(camera.orthographicSize, Is.EqualTo(6)); Assert.That(camera.orthographic, Is.True);
                    next.Dispose();
                }
            }
            finally { Object.DestroyImmediate(root); }
        }
        [UnityTest] public IEnumerator GameUsesCurrentFollowAndOwnerCameraHandles() => Exercise("Assets/Deviant/Game/Scenes/deviant.unity", true);
        [UnityTest] public IEnumerator LabUsesCurrentFollowAndOwnerCameraHandles() => Exercise("Assets/Deviant/Combat/Scenes/CombatV2Arena.unity", false);
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        static IEnumerator Exercise(string scene, bool forest)
        {
            LegacySceneFixture.Open(scene); Object.FindAnyObjectByType<CombatArena>().loadCharacterArt = false;
            yield return new EnterPlayMode(); yield return null;
            var arena = Object.FindAnyObjectByType<CombatArena>(); var player = arena.Player;
            var camera = Camera.main; var output = camera.GetComponent<CameraOutputBinding>();
            Assert.That(output, Is.Not.Null); Assert.That(arena.CameraRequests, Is.SameAs(output.Requests));
            float size = camera.orthographicSize; bool orthographic = camera.orthographic;
            Action follow;
            if (forest)
            {
                var composition = arena.GetComponent("GameComposition"); var rig = (MonoBehaviour)composition.GetType().GetField("cameraRig").GetValue(composition);
                Assert.That(rig.GetType().GetProperty("Requests").GetValue(rig), Is.SameAs(output.Requests));
                follow = (Action)Delegate.CreateDelegate(typeof(Action), rig, rig.GetType().GetMethod("SnapToTarget"));
            }
            else follow = () => arena.GetComponent("LabComposition").SendMessage("Follow");
            using (var driver = arena.AcquireManualSimulation())
            {
                driver.Reset(); player.Position = new Vector2(0, 100); follow();
                Assert.That(camera.transform.position, Is.EqualTo(forest ? new Vector3(10, 99.0625f, -10) : new Vector3(2, 102, -10)));
                using (var a = output.Requests.OpenScope("dialogue fixture")) using (var b = output.Requests.OpenScope("sequence fixture"))
                {
                    var live = new Source { Position = new CameraPosition(30, 70, -10) }; var first = a.Request(10, live);
                    output.ApplyNow(); Assert.That(camera.transform.position, Is.EqualTo(new Vector3(30, 70, -10)));
                    player.Position = new Vector2(20, 110); follow();
                    Assert.That(camera.transform.position, Is.EqualTo(new Vector3(30, 70, -10)), "Follow cannot overwrite an active framing request.");
                    var second = b.Request(20, new FixedCameraPosition(new CameraPosition(40, 80, -10))); output.ApplyNow();
                    live.Position = new CameraPosition(35, 75, -10); second.Complete(); output.ApplyNow();
                    Assert.That(camera.transform.position, Is.EqualTo(new Vector3(35, 75, -10)), "Release selects the remaining source's current position.");
                    first.Cancel(); output.ApplyNow();
                    Assert.That(camera.transform.position, Is.EqualTo(forest ? new Vector3(18.75f, 109.0625f, -10) : new Vector3(22, 112, -10)), "The follow baseline advanced while hidden.");
                    var reset = a.Request(10, new FixedCameraPosition(new CameraPosition(12, 13, -10)));
                    arena.ResetTest(); follow(); output.ApplyNow();
                    Assert.That(reset.Status, Is.EqualTo(CameraRequestStatus.Active), "Combat reset cannot release another owner's camera scope.");
                    Assert.That(camera.transform.position, Is.EqualTo(new Vector3(12, 13, -10)));
                    float clock = arena.Now; bool blocked = player.InputBlocked; float timeScale = Time.timeScale;
                    using (var control = arena.Controls.OpenScope("pause fixture"))
                    {
                        var pause = control.PauseSimulation(); var independent = b.Request(20, new FixedCameraPosition(new CameraPosition(14, 15, -10)));
                        output.ApplyNow(); independent.Dispose(); output.ApplyNow();
                        Assert.That(pause.IsActive, Is.True); Assert.That(arena.Controls.Current.SimulationPaused, Is.True);
                        Assert.That(camera.transform.position, Is.EqualTo(new Vector3(12, 13, -10)));
                    }
                    Assert.That(arena.Now, Is.EqualTo(clock)); Assert.That(player.InputBlocked, Is.EqualTo(blocked)); Assert.That(Time.timeScale, Is.EqualTo(timeScale));
                    Assert.That(camera.orthographicSize, Is.EqualTo(size)); Assert.That(camera.orthographic, Is.EqualTo(orthographic));
                    Assert.That(output.LastApplyError, Is.Null);
                }
                // Keep a request alive until the actual scene/camera destruction.
                var exitOwner = output.Requests.OpenScope("scene exit"); var onExit = exitOwner.Request(100, new FixedCameraPosition(new CameraPosition(1, 2, -10)));
                output.ApplyNow(); yield return new ExitPlayMode();
                Assert.That(onExit.Status, Is.EqualTo(CameraRequestStatus.Cancelled)); Assert.That(exitOwner.IsActive, Is.False); exitOwner.Dispose();
            }
        }
    }
}
