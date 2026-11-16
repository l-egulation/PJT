using System.Collections;
using System.IO;
using System.Linq;
using Deviant.Chapter2.Editor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deviant.Chapter2.Tests
{
    // v26 지하수로 화풍 타일 적용: 시드가 달라도 문·등·웅덩이 규칙이 지켜지는지 실제 씬에서 본다.
    // 캡처는 outputs/sewer-tiles/seed-<n>/ 에 방마다·웅덩이마다 남긴다.
    public sealed class Chapter2SewerModernTests
    {
        static string Out(int seed) => Path.GetFullPath(Application.dataPath + "/../../outputs/sewer-tiles/seed-" + seed);

        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest] public IEnumerator ModernSewerFitsAnySeed([Values(17, 4242, 90210)] int seed)
        {
            EditorSceneManager.playModeStartScene = null;
            Chapter2Tools.Create(); var setup = Object.FindAnyObjectByType<Chapter2World>(); Assert.That(setup, Is.Not.Null); setup.seed = seed;
            yield return new EnterPlayMode();
            Chapter2World world = null; float deadline = Time.realtimeSinceStartup + 90;
            do { yield return null; world = Object.FindAnyObjectByType<Chapter2World>(); } while ((!world || !world.Ready) && Time.realtimeSinceStartup < deadline);
            Assert.That(world && world.Ready, Is.True, "Chapter2 ready");
            var plan = world.Sewer; Assert.That(plan, Is.Not.Null, "v26 parts deployed");
            Assert.That(world.GetComponent<Chapter2WaterwayArt>().Modern, Is.True);
            var dressing = world.GetComponent<Chapter2SewerDressing>(); Assert.That(dressing, Is.Not.Null);
            Assert.That(plan.Doors.Count, Is.GreaterThan(3)); Assert.That(plan.Basins.Count, Is.GreaterThan(3)); Assert.That(plan.Lamps.Count, Is.GreaterThan(10));
            Assert.That(dressing.DoorCount, Is.EqualTo(plan.Doors.Count)); Assert.That(dressing.BasinCount, Is.EqualTo(plan.Basins.Count));
            Assert.That(plan.Pipes.Count, Is.GreaterThan(3)); Assert.That(plan.Waters.Count, Is.GreaterThan(3));
            // 정비소 방에는 물웅덩이·물줄기가 없다(사용자 2026-11-13)
            var shop = world.Layout.rooms.First(r => r.Bounds.Contains(world.Layout.maintenance.Vector + Vector2.up * .1f)).Bounds;
            Assert.That(plan.Basins.Where(b => new Rect(b.X0, b.Floor - 1, b.Width, 1.2f).Overlaps(shop)), Is.Empty, "no basin in the maintenance room");
            Assert.That(plan.Waters.Where(w => shop.Contains(new Vector2(w.X, w.Floor + .1f))), Is.Empty, "no waterfall in the maintenance room");
            foreach (var w in plan.Waters)
            {
                // 물줄기는 받는 웅덩이가 있고, 배수구까지 벽이 비어 있으며 문과 겹치지 않는다.
                Assert.That(plan.Basins.Any(b => b.Fall && b.Floor == w.Floor && b.X0 < w.X && b.X1 > w.X), Is.True, $"water at {w.X},{w.Floor} falls into a basin");
                Assert.That(plan.Doors.Any(d => d.Frame.Overlaps(new Rect(w.X - .6f, w.Floor, 1.2f, w.Outlet - w.Floor + .7f))), Is.False, "water keeps off doors");
            }
            foreach (var l in plan.Lamps) foreach (var p in plan.Pipes)
                Assert.That(l.At.y + 26 / 32f >= p.Top - 28 / 32f && l.At.y - 8 / 32f <= p.Top + 6 / 32f && l.At.x + 8 / 32f >= p.X0 && l.At.x - 8 / 32f <= p.X1, Is.False, "lamps keep off pipes (v26: lamp 26px above to 8px below, pipe 28px below to 6px above its top)");
            foreach (var d in plan.Doors)
            {
                // 문은 바닥에서 벗어나지 않는다: 테 폭 전체가 같은 높이 바닥 위, 테 안은 땅이 없다.
                for (float x = d.Frame.xMin + .05f; x < d.Frame.xMax; x += .25f)
                {
                    Assert.That(plan.Solid(Mathf.FloorToInt(x), Mathf.RoundToInt(d.Floor) - 1), Is.True, $"door at {d.X},{d.Floor} stands on floor");
                    Assert.That(plan.Solid(Mathf.FloorToInt(x), Mathf.RoundToInt(d.Floor)), Is.False, $"door at {d.X},{d.Floor} is open above the floor");
                }
                foreach (var l in plan.Lamps) Assert.That(new Rect(l.At.x - .25f, l.At.y - .25f, .5f, .5f).Overlaps(d.Frame), Is.False, "lamps keep off doors");
            }
            var ground = world.GetComponentsInChildren<Deviant.Combat.ArenaSurface>().Where(s => !s.oneWay).Select(s => s.GetComponent<BoxCollider2D>().bounds).ToArray();
            foreach (var b in plan.Basins)
            {
                var hole = new Bounds(new Vector3((b.X0 + b.X1) / 2f, b.Floor - .5f), new Vector3(b.Width - .02f, .98f, 1));
                Assert.That(ground.Any(g => g.Intersects(hole)), Is.False, "collision is dug 1u too");
                for (int x = b.X0; x < b.X1; x++)
                {
                    Assert.That(plan.Solid(x, b.Floor - 1), Is.False, "basin cell is dug out");
                    Assert.That(plan.Solid(x, b.Floor - 2), Is.True, "basin has a floor");
                    Assert.That(ground.Any(g => g.Contains(new Vector3(x + .5f, b.Floor - 1.5f, g.center.z))), Is.True, "the basin floor stays solid");
                }
            }
            // 캐릭터 색은 그 자리 빛을 한 번만 곱한다(프레임마다 거듭 어두워지지 않는다).
            var actorLight = world.GetComponent<Chapter2SewerActorLight>(); Assert.That(actorLight, Is.Not.Null);
            var hero = world.arena.Player; yield return null; yield return null; var first = hero.visual.color;
            for (int i = 0; i < 6; i++) yield return null;
            Assert.That(Vector4.Distance(first, hero.visual.color), Is.LessThan(.02f), "actor tint stays stable");
            Assert.That(hero.visual.color.b, Is.LessThan(1f), "the hero is tinted by the sewer light");
            Assert.That(actorLight.Aura.enabled, Is.True, "the hero glows"); Assert.That(Vector2.Distance(actorLight.Aura.transform.position, hero.Center), Is.LessThan(.05f));
            Assert.That(actorLight.Aura.sortingOrder, Is.LessThan(hero.visual.sortingOrder), "the glow sits behind the body");
            var dir = Out(seed); Directory.CreateDirectory(dir); var player = world.arena.Player; world.ExternalPresentation = true;
            File.WriteAllText(Path.Combine(dir, "plan.txt"), $"doors {plan.Doors.Count} lamps {plan.Lamps.Count} shafts {plan.Shafts.Count} basins {plan.Basins.Count} pipes {plan.Pipes.Count} waters {plan.Waters.Count}\n");
            int n = 0;
            foreach (var b in plan.Basins.OrderByDescending(b => b.Fall).Take(4))
            {
                player.Position = new Vector2((b.X0 + b.X1) / 2f, b.Floor - 1); player.velocity = Vector2.zero; player.health = player.maxHealth; Physics2D.SyncTransforms();
                for (int i = 0; i < 3; i++) yield return null;
                world.CameraStep(0, true); Capture(world.sceneCamera, Path.Combine(dir, "basin-" + (n++).ToString("00") + ".png"));
            }
            // 보스방에 들어가면 보스전이 시작돼 카메라가 고정되므로 보스방은 찍지 않는다.
            foreach (var room in world.Layout.rooms.Where(r => r.id != "B"))
            {
                world.WarpToRoom(room.id); player.health = player.maxHealth;
                for (int i = 0; i < 3; i++) yield return null;
                world.CameraStep(0, true); Capture(world.sceneCamera, Path.Combine(dir, "room-" + room.id + ".png"));
            }
            yield return new ExitPlayMode();
        }

        static void Capture(Camera camera, string path)
        {
            var previous = RenderTexture.active; var target = RenderTexture.GetTemporary(960, 540, 24); var texture = new Texture2D(960, 540, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(texture); }
        }
    }
}
