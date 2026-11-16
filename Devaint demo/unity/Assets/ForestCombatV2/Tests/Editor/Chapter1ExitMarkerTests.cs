using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    // The chapter exit appears as its own arch (opening frames, then exit-open) once the boss falls, with an F keycap
    // bobbing above it.
    public sealed class Chapter1ExitMarkerTests
    {
        [UnityTest] public IEnumerator TheOpenExitIsAPortalWithABobbingKeycap()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            ForestCombatWorld world = null;
            for (float waitFrom = Time.realtimeSinceStartup; Time.realtimeSinceStartup - waitFrom < 20 && !(world && world.Ready && world.GetComponent<Chapter1RunBinding>());) { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.That(world && world.Ready, Is.True);
            yield return null; yield return null;
            var exit = world.GetComponent<Chapter1BossEncounter>();
            Assert.That(exit, Is.Not.Null);
            Assert.That(exit.ExitVisible, Is.False, "no portal before the boss falls");
            Assert.That(exit.ExitKey.Visible, Is.False);

            world.GetComponent<Chapter1RunBinding>().Run.MarkBossDefeated();
            yield return null;
            Assert.That(exit.ExitVisible, Is.True, "the portal opens");
            Assert.That(exit.ExitKey.Visible && exit.ExitKey.Key == 'F', Is.True, "an F keycap marks it");
            var portal = GameObject.Find("Chapter1 exit").GetComponent<SpriteRenderer>();
            Assert.That(portal.sprite.name, Does.StartWith("0").Or.EqualTo("exit-open"), "drawn with the exit art (opening frames, then exit-open)");
            Assert.That(exit.ExitKey.GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo("keycap-f"), "the approved keycap art");
            Assert.That(ForestCombatV2.PresentationSfx.CountOf(ForestCombatV2.PresentationSfx.ExitOpen), Is.EqualTo(1), "the exit sound plays once when it opens");
            Assert.That(exit.ExitKey.transform.position.y, Is.GreaterThan(exit.ExitPosition.y + 1.5f), "the keycap floats above the portal");
            Assert.That(exit.ExitKey.transform.position.y, Is.LessThanOrEqualTo(portal.bounds.max.y + .3f));
            Assert.That(Mathf.Abs(exit.ExitKey.transform.position.x - exit.ExitPosition.x), Is.LessThan(.01f));

            // It bobs by 4 world pixels, on whole pixels.
            float low = float.MaxValue, high = float.MinValue, started = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - started < 1.1f)
            {
                yield return null;
                float offset = exit.ExitKey.Offset; low = Mathf.Min(low, offset); high = Mathf.Max(high, offset);
                Assert.That(Mathf.Abs(offset * KeycapMarker.Ppu - Mathf.Round(offset * KeycapMarker.Ppu)), Is.LessThan(.001f), "snapped to pixels");
            }
            Assert.That(high - low, Is.EqualTo(KeycapMarker.BobPixels / KeycapMarker.Ppu).Within(.001f), "moves up and down by 4 px");

            // Review capture (not a pixel test): the player beside the exit at 960x540.
            var player = world.arena.Player;
            player.Position = exit.ExitPosition + Vector2.left * 3; Physics2D.SyncTransforms();
            world.RefreshCamera(0, true);
            yield return null; yield return null;
            var view = world.View; var rt = RenderTexture.GetTemporary(960, 540, 24); var target = view.targetTexture; var rect = view.rect;
            view.targetTexture = rt; view.rect = new Rect(0, 0, 1, 1); view.Render(); RenderTexture.active = rt;
            var shot = new Texture2D(960, 540, TextureFormat.RGB24, false); shot.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            RenderTexture.active = null; view.targetTexture = target; view.rect = rect; RenderTexture.ReleaseTemporary(rt);
            Directory.CreateDirectory("../outputs/hub/capture");
            File.WriteAllBytes("../outputs/hub/capture/chapter-exit-portal.png", shot.EncodeToPNG()); Object.Destroy(shot);
            yield return new ExitPlayMode();
        }
    }
}
