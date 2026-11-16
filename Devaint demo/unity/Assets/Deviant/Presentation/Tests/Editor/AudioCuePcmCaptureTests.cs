using System;
using System.Collections;
using System.IO;
using Deviant.Definitions;
using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Deviant.Presentation.Tests
{
    public sealed class AudioCuePcmCaptureTests
    {
        sealed class Clock : ICueClock { public float Time; public float ReadElapsed() => Time; }
        sealed class State { public bool Held = true; public bool IsHeld() => Held; public float Scale() => 1; }
        [UnityTearDown] public IEnumerator TearDown() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator CaptureApprovedWhipThroughSampledAndContinuousOutputs()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); yield return new EnterPlayMode();
            var listener = new GameObject("PCM capture listener", typeof(AudioListener));
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Deviant/Audio/SFX/Weapons/sfx_weapon_whip_basic_0_01_v01.wav");
            Assert.That(clip, Is.Not.Null);
            foreach (bool continuous in new[] { false, true })
            {
                using (var factory = new AudioCueFactory()) using (var cues = new CuePlayer(factory)) using (var owner = cues.OpenScope("PCM capture"))
                {
                    var definition = new AudioCueDefinition("PcmCapture", "whip", clip.samples, clip.frequency);
                    var state = new State(); var clock = new Clock();
                    if (continuous) factory.PublishContinuous(definition, clip, 1.4f, 1, state.IsHeld, state.Scale);
                    else factory.Publish(definition, clip, 1.4f, 1);
                    var handle = owner.Play(definition, new CueOccurrence(Guid.NewGuid(), definition.Id), new CueAnchor(0, 0, 1), clock);
                    var output = GameObject.Find(definition.Id).GetComponent<AudioCueOutputBinding>();
                    var capture = output.gameObject.AddComponent<AudioCuePcmCapture>();
                    yield return new WaitForSecondsRealtime(.1f);
                    state.Held = false; double start = AudioSettings.dspTime; handle.Poll();
                    while (AudioSettings.dspTime - start < .8)
                    {
                        float next = Mathf.Min(.48f, Mathf.Floor((float)(AudioSettings.dspTime - start) / .02f) * .02f * 1.4f);
                        if (next != clock.Time) { clock.Time = next; handle.Poll(); }
                        yield return null;
                    }
                    var pcm = capture.Snapshot(); Assert.That(pcm.Length, Is.GreaterThan(10000), "The native audio callback must actually run.");
                    string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../outputs/audio-runtime-diagnosis"));
                    Directory.CreateDirectory(dir); string name = continuous ? "unity-continuous" : "unity-sampled";
                    using (var writer = new BinaryWriter(File.Create(Path.Combine(dir, name + ".f32")))) foreach (float sample in pcm) writer.Write(sample);
                    File.WriteAllText(Path.Combine(dir, name + ".json"), "{\"rate\":" + AudioSettings.outputSampleRate + ",\"frames\":" + pcm.Length + ",\"seeks\":" + output.SeekCount + "}");
                    if (continuous) Assert.That(output.SeekCount, Is.EqualTo(1));
                    else Assert.That(output.SeekCount, Is.GreaterThan(3), "The former path repositions PCM on successive simulation steps.");
                    handle.Cancel();
                }
                yield return null;
            }
            Object.Destroy(listener); yield return new ExitPlayMode();
        }
    }
}
