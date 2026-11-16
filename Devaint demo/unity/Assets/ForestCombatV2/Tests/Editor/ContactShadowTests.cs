using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    // Contact shadows (Deviant.Combat.ContactShadowPresentation): on the surface under the player's feet, staying on
    // the ground while the player is in the air (narrower), gone above its height limit.
    public sealed class ContactShadowTests
    {
        static Texture2D Render(Camera camera)
        {
            var rt = RenderTexture.GetTemporary(960, 540, 24, RenderTextureFormat.ARGB32);
            var previous = camera.targetTexture; var rect = camera.rect;
            camera.targetTexture = rt; camera.rect = new Rect(0, 0, 1, 1); camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(960, 540, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
            RenderTexture.active = null; camera.targetTexture = previous; camera.rect = rect; RenderTexture.ReleaseTemporary(rt);
            return image;
        }

        [UnityTest] public IEnumerator ThePlayersShadowLiesOnTheGroundBelowAndGoesAwayHighUp()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            StartingVillageWorld village = null;
            for (int frame = 0; frame < 600 && !(village && village.Ready); frame++) { yield return null; village = Object.FindAnyObjectByType<StartingVillageWorld>(); }
            Assert.That(village && village.Ready, Is.True);
            var arena = village.World.arena; var player = arena.Player;
            SpriteRenderer Shadow() => arena.transform.Find("Contact shadows")?.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault();

            player.Position = new Vector2(30, village.WalkingLine); Physics2D.SyncTransforms();
            yield return null; yield return null;
            var shadow = Shadow();
            Assert.That(shadow, Is.Not.Null, "the arena draws contact shadows");
            Assert.That(shadow.enabled, Is.True, "standing on the ground");
            Assert.That(shadow.transform.position.y, Is.EqualTo(village.WalkingLine).Within(1 / 16f), "on the walking line");
            Assert.That(shadow.sortingOrder, Is.EqualTo(player.visual.sortingOrder - 1), "just under the player");
            Assert.That(shadow.sharedMaterial.shader.name, Is.EqualTo("Deviant/ContactShadowMultiply"));
            float groundWidth = shadow.sprite.rect.width;

            // Drawn: the ground under the feet is darker with the shadow than without it.
            village.World.FixedCameraCenter = new Vector2(30, village.WalkingLine + 4);   // the opening may hold the camera elsewhere
            village.World.RefreshCamera(0, true);
            var with = Render(village.World.View);
            // where the shadow was drawn (the opening may walk the player on before the next tick)
            var low = Vector3.Scale(village.World.View.WorldToViewportPoint(shadow.bounds.min), new Vector3(960, 540, 1)); var high = Vector3.Scale(village.World.View.WorldToViewportPoint(shadow.bounds.max), new Vector3(960, 540, 1));
            arena.ContactShadows.Enabled = false; arena.ContactShadows.Tick();
            var without = Render(village.World.View); arena.ContactShadows.Enabled = true; arena.ContactShadows.Tick();
            int darker = 0;
            for (int y = Mathf.Max(0, (int)low.y); y <= Mathf.Min(539, (int)high.y); y++)
                for (int x = Mathf.Max(0, (int)low.x); x <= Mathf.Min(959, (int)high.x); x++)
                    if (with.GetPixel(x, y).grayscale < without.GetPixel(x, y).grayscale - .02f) darker++;
            System.IO.Directory.CreateDirectory("../outputs/contact-shadow");
            System.IO.File.WriteAllBytes("../outputs/contact-shadow/with.png", with.EncodeToPNG());
            System.IO.File.WriteAllBytes("../outputs/contact-shadow/without.png", without.EncodeToPNG());
            Object.Destroy(with); Object.Destroy(without);
            Assert.That(darker, Is.GreaterThan(20), "the shadow darkens the ground under the feet");
            village.World.FixedCameraCenter = null;

            player.Position = new Vector2(30, village.WalkingLine + 3); Physics2D.SyncTransforms();
            yield return null;
            Assert.That(shadow.enabled, Is.True, "in the air the shadow stays");
            Assert.That(shadow.transform.position.y, Is.EqualTo(village.WalkingLine).Within(1 / 16f), "on the ground below");
            Assert.That(shadow.sprite.rect.width, Is.LessThan(groundWidth), "narrower with height");

            player.Position = new Vector2(30, village.WalkingLine + 10); Physics2D.SyncTransforms();
            yield return null;
            Assert.That(shadow.enabled, Is.False, "too high above the ground");
            yield return new ExitPlayMode();
        }
    }
}
