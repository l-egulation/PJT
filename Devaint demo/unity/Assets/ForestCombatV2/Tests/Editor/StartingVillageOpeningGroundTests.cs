using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class StartingVillageOpeningGroundTests
    {
        static Color32 PixelAt(SpriteRenderer renderer, Vector2 world)
        {
            var sprite = renderer.sprite; var local = renderer.transform.InverseTransformPoint(world);
            int x = Mathf.FloorToInt(local.x * sprite.pixelsPerUnit + sprite.pivot.x);
            int y = Mathf.FloorToInt(local.y * sprite.pixelsPerUnit + sprite.pivot.y);
            if (x < 0 || y < 0 || x >= sprite.rect.width || y >= sprite.rect.height) return new Color32();
            return sprite.texture.GetPixel(x + (int)sprite.rect.x, y + (int)sprite.rect.y);
        }

        static void Capture(Camera camera, string name)
        {
            var target = camera.targetTexture; var rect = camera.rect; var active = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(960, 540, 24); var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt; camera.rect = new Rect(0, 0, 1, 1); camera.Render(); RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
                Directory.CreateDirectory("../outputs/tutorial-bright-review/fixed");
                File.WriteAllBytes("../outputs/tutorial-bright-review/fixed/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = target; camera.rect = rect; RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(rt); Object.Destroy(image);
            }
        }

        static void AssertGroundAndFoot(StartingVillageWorld village, SpriteRenderer[] art, SpriteRenderer large, SpriteRenderer floating)
        {
            foreach (float x in new[] { -205.3f, -201.4f, -196.6f })
            {
                var at = new Vector2(x, village.WalkingLine - .3f);
                var floor = art.First(r => r.name == "Ground body" && r.bounds.Contains((Vector3)at));
                // The foot is the inside of the lawn (no grass, no side faces) drawn over the floor's grass under it.
                Assert.That(PixelAt(large, at).a, Is.EqualTo(255), "the wall foot is solid below the walking line");
                Assert.That(large.sortingOrder, Is.GreaterThan(floor.sortingOrder), "the wall buries the floor's grass under it");
            }
            float bottom = village.WalkingLine + 3.2f;
            Assert.That(PixelAt(floating, new Vector2(-148.25f, bottom + 4 / 32f)).a, Is.EqualTo(255), "solid body closes the foot");
            Assert.That(PixelAt(floating, new Vector2(-148.25f, bottom - 4 / 32f)).a, Is.Zero, "the space under it stays open");
            Assert.That(PixelAt(floating, new Vector2(-149 + .5f / 32, bottom + .5f / 32)).a, Is.Zero, "left bottom corner is cut like the lawn's own corners");
            Assert.That(PixelAt(floating, new Vector2(-147.5f - .5f / 32, bottom + .5f / 32)).a, Is.Zero, "right bottom corner is cut like the lawn's own corners");
        }

        [UnityTest] public IEnumerator WallsContinueTheFloorAndHangingSoilHasAClosedRoundedFoot()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new MemoryProfileStore());
            StartingVillageWorld village = null;
            for (int frame = 0; frame < 600 && !(village && village.Ready); frame++)
            { yield return null; village = Object.FindAnyObjectByType<StartingVillageWorld>(); }
            Assert.That(village && village.Ready, Is.True);
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "TemporaryGameGui");
            gui.GetType().GetField("storyChecked", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gui, false);
            yield return null; yield return null;
            Assert.That(village.CourseBuilt, Is.True);
            village.SetSketch(false);
            var art = village.GetComponentsInChildren<SpriteRenderer>();
            Assert.That(art.Any(r => r.name == "Opening course art overhang"), Is.False, "non-solid wide dirt caps are removed");
            var fence = art.First(r => r.name == "Opening course art fence");
            // 2x2-dot pixel art: native size on the dot grid, its own ink outline (no stretch, no contrast material).
            Assert.That(fence.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(fence.sharedMaterial.name, Does.Not.StartWith("Course obstacle"));
            Assert.That(fence.transform.position.x * 16f, Is.EqualTo(Mathf.Round(fence.transform.position.x * 16f)).Within(1e-3f));
            Assert.That(fence.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            var walls = art.Where(r => r.name == "Opening course wall").ToArray();
            Assert.That(walls.Length, Is.EqualTo(15), "one picture per wall of HubOpening.json");
            var large = walls.Single(r => Mathf.Abs(r.bounds.center.x + 201) < .01f);
            var floating = walls.Single(r => Mathf.Abs(r.bounds.center.x + 148.25f) < .01f);
            AssertGroundAndFoot(village, art, large, floating);

            // The real tutorial geometry and camera, at native resolution. Include both a wide grounded wall and the
            // hanging wall/high wall pair; the notebook UI is intentionally outside these terrain review captures.
            village.World.FixedCameraCenter = new Vector2(-235, village.WalkingLine + 5);
            village.World.RefreshCamera(0, true); village.SendMessage("LateUpdate");
            Capture(village.World.View, "fence");
            village.World.FixedCameraCenter = new Vector2(-192.5f, village.WalkingLine + 5.5f);
            village.World.RefreshCamera(0, true); village.SendMessage("LateUpdate");
            Capture(village.World.View, "grounded");
            village.World.FixedCameraCenter = new Vector2(-147, village.WalkingLine + 7);
            village.World.RefreshCamera(0, true); village.SendMessage("LateUpdate");
            Capture(village.World.View, "hanging");
            var colour = large.sprite;
            village.SetSketch(true);
            Assert.That(large.sprite, Is.Not.SameAs(colour), "the sketch switches to its own drawing");
            Assert.That(large.sprite.rect.size, Is.EqualTo(colour.rect.size), "both states share the same contact anchors");
            AssertGroundAndFoot(village, art, large, floating);
            village.SendMessage("LateUpdate");
            Capture(village.World.View, "hanging-sketch");
            village.World.FixedCameraCenter = new Vector2(-192.5f, village.WalkingLine + 5.5f);
            village.World.RefreshCamera(0, true); village.SendMessage("LateUpdate");
            Capture(village.World.View, "grounded-sketch");
            village.SetSketch(false);
            Assert.That(large.sprite, Is.SameAs(colour));
            Assert.That(PixelAt(large, new Vector2(-201.4f, village.WalkingLine - .3f)).a, Is.EqualTo(255), "colour returns after the sketch preview");
            village.World.FixedCameraCenter = null;
            yield return new ExitPlayMode();
        }
    }
}
