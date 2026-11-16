using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ForestCombatV2.Tests
{
    // The boss room altar: hidden until the boss falls, then F opens a colour choice and three candidates of that
    // colour; one goes to the owned colour inks. Leaving with candidates unpicked discards them.
    public sealed class Chapter1InkAltarTests
    {
        static string[] ColoursOf(Deviant.Run.WeaponGrowth growth, string[] ids) => ids.Select(id => growth.Data.parts.First(p => p.id == id).color).ToArray();
        static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

        static void Open()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
        }

        // EnterPlayMode must be yielded by the test itself; this waits for the run and picks the two blueprints.
        static IEnumerator Load()
        {
            ForestCombatWorld world = null;
            for (float waitFrom = Time.realtimeSinceStartup; Time.realtimeSinceStartup - waitFrom < 20 && !(world && world.Ready && world.GetComponent<Chapter1RunBinding>());) { yield return null; world = Object.FindAnyObjectByType<ForestCombatWorld>(); }
            Assert.That(world && world.Ready, Is.True);
            yield return null; yield return null;
            var offers = world.GetComponent<Chapter1StartingBlueprints>(); var player = world.arena.Player;
            Assert.That(offers, Is.Not.Null, "blueprint offers"); Assert.That(world.GetComponent<Chapter1BossEncounter>()?.Altar, Is.Not.Null, "altar built");
            player.Position = offers.OfferPosition(0); Assert.That(offers.TryChoose(0), Is.True);
            player.Position = offers.OfferPosition(1); Assert.That(offers.TryChoose(1), Is.True);
            foreach (var e in world.arena.enemies) if (e) { e.aggressive = false; e.CancelAttack(); }
            yield return null;
        }

        [UnityTest] public IEnumerator TheAltarGivesOneInkOfTheChosenColour()
        {
            Open(); yield return new EnterPlayMode();
            // No lambda captures here: the test enumerator survives the play mode domain reload, a closure would not.
            var load = Load(); while (load.MoveNext()) yield return load.Current;
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            var altar = world.GetComponent<Chapter1BossEncounter>().Altar;
            var growth = world.GetComponent<Chapter1RunBinding>().Run.Growth;
            var player = world.arena.Player;
            Assert.That(altar.Available, Is.False, "no altar before the boss falls");
            Assert.That(altar.TryOpen(), Is.False);

            // Defeat first: walking into the room with the boss alive would start its entrance scene.
            world.GetComponent<Chapter1RunBinding>().Run.MarkBossDefeated();
            player.Position = altar.Position; Physics2D.SyncTransforms();
            yield return null;
            Assert.That(altar.Available && altar.PlayerNear, Is.True);
            Assert.That(altar.Key.Visible, Is.True, "an F keycap marks the altar");
            Assert.That(Vector2.Distance(altar.Position, world.GetComponent<Chapter1BossEncounter>().ExitPosition), Is.GreaterThan(3f), "away from the exit's reach");

            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui" && b.GetType().GetField("host", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(b) != null);
            Assert.That(altar.TryOpen(), Is.True);
            gui.GetType().GetMethod("Refresh").Invoke(gui, null);
            yield return null;
            Assert.That(world.arena.Controls.Current.SimulationPaused, Is.True, "the altar pauses the room");
            Assert.That(Field<GameObject>(gui, "altarPanel").activeInHierarchy, Is.True);
            Capture(world, "altar-colours");
            int before = growth.Inks.Count, rolled = growth.ColorInksRolled;
            Field<Button[]>(gui, "altarColors")[3].onClick.Invoke(); // blue
            Assert.That(altar.Color, Is.EqualTo("blue"));
            Assert.That(altar.Candidates.Length, Is.InRange(1, 3));
            Assert.That(ColoursOf(growth, altar.Candidates).All(c => c == "blue"), Is.True);
            Assert.That(Field<Button[]>(gui, "altarColors")[0].gameObject.activeSelf, Is.False, "the colour cannot be changed");
            yield return null;
            Capture(world, "altar-candidates");
            string chosen = altar.Candidates[0];
            // A card only marks the choice; the claim button takes it.
            Field<Button[]>(gui, "altarCards")[0].onClick.Invoke();
            Assert.That(growth.Inks.Count, Is.EqualTo(before), "marking a candidate takes nothing yet");
            yield return null;
            Capture(world, "altar-picked");
            Field<Button>(gui, "altarClaim").onClick.Invoke();
            Assert.That(growth.Inks.Count, Is.EqualTo(before + 1));
            Assert.That(growth.Inks.Last().Part.id, Is.EqualTo(chosen));
            Assert.That(growth.ColorInksRolled, Is.EqualTo(rolled), "altar inks are outside the early colour count");
            Assert.That((int)gui.GetType().GetProperty("ActiveUiEffects").GetValue(gui), Is.GreaterThan(0), "the acquisition effect plays on the taken card");
            Assert.That(PresentationSfx.LastPlayed, Is.EqualTo(PresentationSfx.AltarAcquire));
            Assert.That(altar.TryClaim(1), Is.False, "only one candidate");
            Assert.That(altar.Finished, Is.True);
            Assert.That(altar.IsOpen, Is.True, "the screen stays up while the acquisition effect plays");
            for (float waitFrom = Time.realtimeSinceStartup; altar.IsOpen && Time.realtimeSinceStartup - waitFrom < 5;) yield return null;
            // (The clock boss's own entrance scene may hold the room here; only the altar's lease is checked.)
            Assert.That(altar.IsOpen, Is.False, "taking the ink closes the altar by itself and returns its pause lease");
            Assert.That(Field<GameObject>(gui, "altarPanel").activeInHierarchy, Is.False, "the 잉크 선택 screen is gone");
            Assert.That(altar.TryOpen(), Is.False, "a used altar does not open again");
            Assert.That(altar.Key.Visible, Is.False);
            // Review capture of the used altar in the room (not a pixel test).
            player.Position = altar.Position + Vector2.left * 2; world.RefreshCamera(0, true); yield return null;
            Capture(world, "altar-world");
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator LeavingWithoutPickingDiscardsTheCandidates()
        {
            Open(); yield return new EnterPlayMode();
            // No lambda captures here: the test enumerator survives the play mode domain reload, a closure would not.
            var load = Load(); while (load.MoveNext()) yield return load.Current;
            var world = Object.FindAnyObjectByType<ForestCombatWorld>();
            var encounter = world.GetComponent<Chapter1BossEncounter>(); var altar = encounter.Altar;
            var binding = world.GetComponent<Chapter1RunBinding>(); var growth = binding.Run.Growth; var player = world.arena.Player;
            binding.Run.MarkBossDefeated();
            player.Position = altar.Position; Physics2D.SyncTransforms();
            yield return null;
            Assert.That(altar.TryOpen(), Is.True);
            Assert.That(altar.TryChooseColor("red"), Is.True);
            altar.Close();
            int before = growth.Inks.Count;
            player.Position = encounter.ExitPosition; Physics2D.SyncTransforms();
            Assert.That(encounter.TryUseExit(), Is.True, "the exit still clears the chapter");
            Assert.That(altar.Discarded, Is.True, "unpicked candidates are thrown away");
            yield return null;
            Assert.That(altar.DiscardEffects, Is.EqualTo(altar.Candidates.Length), "each discarded candidate sinks above the altar");
            Assert.That(PresentationSfx.LastPlayed, Is.EqualTo(PresentationSfx.AltarDiscard));
            Assert.That(growth.Inks.Count, Is.EqualTo(before), "and none is granted");
            yield return new ExitPlayMode();
        }

        static void Capture(ForestCombatWorld world, string name)
        {
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui" && b.GetComponent<Canvas>());
            var canvas = gui.GetComponent<Canvas>(); var camera = world.View;
            var mode = canvas.renderMode; var target = camera.targetTexture; var rect = camera.rect;
            var rt = RenderTexture.GetTemporary(960, 540, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(960, 540, TextureFormat.RGBA32, false);
            var fit = gui.GetType().GetMethod("FitToWorld");
            try
            {
                camera.targetTexture = rt; camera.rect = new Rect(0, 0, 1, 1);
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + 1;
                // Dynamic-font labels rasterise at the canvas scale, so rebuild them for the capture size.
                fit.Invoke(gui, null); foreach (var label in gui.GetComponentsInChildren<Text>()) label.SetAllDirty();
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
                Directory.CreateDirectory("../outputs/hub/capture");
                File.WriteAllBytes($"../outputs/hub/capture/{name}.png", image.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = mode; canvas.worldCamera = null; camera.targetTexture = target; camera.rect = rect; RenderTexture.active = null;
                fit.Invoke(gui, null); RenderTexture.ReleaseTemporary(rt); Object.Destroy(image);
            }
        }
    }
}
