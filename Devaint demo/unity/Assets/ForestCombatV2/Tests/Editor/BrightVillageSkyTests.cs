using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class BrightVillageSkyTests
    {
        static Color Colour(float fromTop) => (Color)typeof(StartingVillageWorld)
            .GetMethod("DaylightSkyColour", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { fromTop });

        [Test]
        public void DaylightMatchesTheApprovedHtmlPalette()
        {
            Assert.That((Color32)Colour(0), Is.EqualTo(new Color32(207, 237, 248, 255)));
            Assert.That((Color32)Colour(.58f * 730 / 900), Is.EqualTo(new Color32(227, 244, 244, 255)));
            Assert.That((Color32)Colour(1), Is.EqualTo(new Color32(247, 247, 224, 255)));
            Assert.That(Colour(1), Is.EqualTo(Colour(730 / 900f)), "the warm horizon continues below the gradient");
        }

        static void Capture(Camera camera, string name)
        {
            var target = camera.targetTexture; var rect = camera.rect; var active = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(960, 540, 24);
            var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt; camera.rect = new Rect(0, 0, 1, 1); camera.Render();
                RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
                Directory.CreateDirectory("../outputs/bright-village-sky");
                File.WriteAllBytes("../outputs/bright-village-sky/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = target; camera.rect = rect; RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(rt); Object.Destroy(image);
            }
        }

        static void TickClouds(StartingVillageWorld village, float dt)
        {
            typeof(StartingVillageWorld).GetMethod("TickLivingClouds", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(village, new object[] { village.World.View.transform.position,
                    village.World.View.orthographicSize * village.World.View.aspect, dt });
        }

        [UnityTest]
        public IEnumerator VillageAndTutorialUseDaylightAndStillSwitchToSketch()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(StartingVillageTests.ScenePath);
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new Deviant.Run.MemoryProfileStore());
            StartingVillageWorld village = null;
            for (int frame = 0; frame < 600 && !(village && village.Ready); frame++)
            { yield return null; village = Object.FindAnyObjectByType<StartingVillageWorld>(); }
            Assert.That(village && village.Ready, Is.True);
            var gui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .FirstOrDefault(b => b.GetType().Name == "TemporaryGameGui");
            if (gui)
            {
                const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
                gui.GetType().GetField("storyChecked", Private).SetValue(gui, false);
            }
            yield return null; yield return null;
            Assert.That(village.CourseBuilt, Is.True, "capture the actual tutorial road before it is removed");
            var sky = village.GetComponentsInChildren<SpriteRenderer>().Single(r => r.name == "Hub sky");
            village.SetSketch(false);
            var bright = sky.sprite;
            Assert.That(bright.name, Is.EqualTo("Village daylight sky"));
            Assert.That(bright.texture.GetPixel(0, bright.texture.height - 1).g, Is.GreaterThan(.90f));
            var bounds = bright.bounds.size;
            var before = sky.transform.position;
            village.SetSketch(true);
            Assert.That(sky.sprite, Is.Not.SameAs(bright));
            Assert.That(sky.sprite.bounds.size, Is.EqualTo(bounds));
            Assert.That(sky.transform.position, Is.EqualTo(before), "switching the sky must not reposition it like a floor tile");
            village.SetSketch(false);
            Assert.That(sky.sprite, Is.SameAs(bright));
            var clouds = village.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name.StartsWith("Village living cloud ")).ToArray();
            Assert.That(clouds.Length, Is.EqualTo(6), "the title's six pixel cloud silhouettes replace the static strip");
            var cloud = clouds.Single(r => r.name == "Village living cloud 2");
            var texture = cloud.sprite.texture;
            var still = texture.GetPixels32();
            TickClouds(village, 4);
            var moving = texture.GetPixels32();
            Assert.That(moving.SequenceEqual(still), Is.False, "the shape evolves, rather than only translating");
            Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(cloud.sprite.pixelsPerUnit, Is.EqualTo(16), "each native cloud texel is a two-pixel game dot");
            Assert.That(moving.All(p => p.a == 0 || (p.a == 255 && p.r >= 240 && p.g >= 242 && p.b >= 230)), Is.True,
                "binary pixels and pale internal shading have no dark contour");
            int middle = texture.width / 2;
            int bottom = 0;
            while (bottom < texture.height && moving[bottom*texture.width+middle].a != 255) bottom++;
            Assert.That(bottom, Is.LessThan(texture.height));
            Assert.That(moving[bottom*texture.width+middle].r, Is.LessThan(255),
                "the original white stroke must not return below the cloud's bottom shadow");
            var model = ((IEnumerable)typeof(StartingVillageWorld).GetField("livingClouds", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(village)).Cast<object>().ElementAt(1);
            var distance = (float[])model.GetType().GetField("distance").GetValue(model);
            for (int y = 0; y < texture.height; y++) for (int x = 0; x < texture.width; x++)
            {
                if (distance[y*texture.width+x] >= 2)
                    Assert.That(moving[(texture.height-1-y)*texture.width+x].a, Is.EqualTo(255), "the fixed cloud body cannot wobble away");
            }
            village.SetSketch(true);
            var frozen = cloud.sprite.texture.GetPixels32(); var frozenPosition = cloud.transform.position;
            TickClouds(village, 3);
            Assert.That(cloud.sprite.texture.GetPixels32().SequenceEqual(frozen), Is.True, "sketch preserves the current silhouette");
            Assert.That(cloud.transform.position, Is.EqualTo(frozenPosition), "sketch freezes drift too");
            village.SetSketch(false);
            foreach (float x in new[] { 14f, -192.5f })
            {
                village.World.FixedCameraCenter = new Vector2(x, village.WalkingLine + 5.3f);
                village.World.RefreshCamera(0, true); village.SendMessage("LateUpdate");
                Assert.That(village.World.View.transform.position.x, Is.EqualTo(x).Within(.1f), "camera reaches each area");
                Assert.That(sky.sprite, Is.SameAs(bright));
                Capture(village.World.View, x > 0 ? "village-bright" : "tutorial-bright");
            }
            village.SetSketch(true); village.SendMessage("LateUpdate"); Capture(village.World.View, "tutorial-sketch");
            village.World.FixedCameraCenter = null;
            yield return new ExitPlayMode();
            Chapter1ProfileHost.UseStore(null);
        }
    }
}
