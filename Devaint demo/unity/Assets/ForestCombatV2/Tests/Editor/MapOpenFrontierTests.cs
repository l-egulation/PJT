using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Deviant.Chapter2;
using Deviant.Combat;
using Deviant.Integration;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class MapOpenFrontierTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        static Type GeometryType => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Deviant.Game.ApprovedMapGeometry")).First(t => t != null);
        GameObject root;
        [TearDown] public void Cleanup() { if (root) Object.DestroyImmediate(root); }
        [UnityTearDown] public IEnumerator LeavePlay() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [TestCase(595, 340, 1.2f)]
        [TestCase(350, 209, 2.6f)]
        public void FullMapAndMinimapLeaveFourOpenEndsWithoutRevealingTheHiddenRooms(float width, float height, float stroke)
        {
            var data = Crossroads(); var map = Source(data, new[] { true, false, false, false, false });
            var geometry = Geometry(new Vector2(width, height), stroke);
            Present(geometry, map);
            using var vertices = Mesh(geometry);
            foreach (var end in new[] { new Vector2(3, 8), new Vector2(13, 8), new Vector2(8, 3), new Vector2(8, 13) })
                Assert.That(Covers(vertices, geometry, data, end * 32, true), Is.False, "open frontier " + end);
            foreach (var wall in new[] { new Vector2(11, 7), new Vector2(11, 9), new Vector2(7, 11), new Vector2(9, 11), new Vector2(6.5f, 6) })
                Assert.That(Covers(vertices, geometry, data, wall * 32, true), Is.True, "solid jamb/floor " + wall);
            Assert.That(Covers(vertices, geometry, data, new Vector2(8, 8) * 32, false), Is.True);
            Assert.That(Covers(vertices, geometry, data, new Vector2(14, 8) * 32, false), Is.False, "unexplored interior remains hidden");
            typeof(Chapter1FullMap).GetField("discoveredRooms", Hidden).SetValue(map, new[] { true, true, false, false, false });
            Present(geometry, map);
            using var revealed = Mesh(geometry);
            Assert.That(Covers(revealed, geometry, data, new Vector2(14, 8) * 32, false), Is.True, "discovery rebuilds the cached geometry");
            Assert.That(Covers(revealed, geometry, data, new Vector2(13, 8) * 32, true), Is.False, "discovery does not add a seam across the connection");
            Assert.That(Covers(revealed, geometry, data, new Vector2(16, 8) * 32, true), Is.True, "real far wall remains visible");
        }

        [UnityTest] public IEnumerator GeneratedChaptersDrawOnlyRealSolidBoundaries()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var arena = new GameObject("Map test arena").AddComponent<CombatArena>();
            yield return new EnterPlayMode(); yield return null;
            arena = Object.FindAnyObjectByType<CombatArena>();
            var content = CombatContentLoader.Load(null, false);
            content.Profile = CombatCompositionProfile.Game;
            content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout(); content.Enemies = Array.Empty<CombatEnemySpawn>();
            content.Camera = new GameObject("Map test camera").AddComponent<Camera>();
            content.Camera.tag = "MainCamera";
            content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = new Vector2(.1875f, 1);
            Assert.That(arena.Initialize(content), Is.True);
            using var simulation = arena.AcquireManualSimulation();
            var source = JsonUtility.FromJson<ForestData>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/MeadowDuskV3/MapVer2/Data/ForestWorld.json").text);
            var art = JsonUtility.FromJson<RoundedTerrainLayout>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/MeadowDuskV3/MapVer2/Data/RoundedTerrain.json").text);
            foreach (int seed in new[] { 42, 777 })
            {
                Check(Chapter1ProceduralMap.Generate(source, art, seed), "chapter1 seed " + seed);
                // Use chapter 2's actual map adapter, including its padded connection spaces.
                var holder = new GameObject("Chapter2 map adapter fixture"); holder.SetActive(false);
                var world = holder.AddComponent<Chapter2World>(); world.enabled = false; world.arena = arena; world.sceneCamera = content.Camera;
                typeof(Chapter2World).GetProperty("Layout").SetValue(world, Chapter2BlockLayout.Generate(seed));
                var map = Chapter2CommonComposition.Attach(world);
                Check(map.MapData, "chapter2 seed " + seed);
                Object.Destroy(holder);
            }
            void Check(ForestData data, string label)
            {
                var map = Source(data, new bool[data.rooms.Length]);
                Component geometry;
                var flags = (bool[])typeof(Chapter1FullMap).GetField("discoveredRooms", Hidden).GetValue(map);
                int boundaries = 0;
                foreach (var room in data.rooms)
                {
                    Array.Clear(flags, 0, flags.Length); flags[room.id] = true;
                    // A different source instance forces a rebuild even at the same discovery count.
                    geometry = Geometry(new Vector2(350, 209), 2.6f); Present(geometry, map);
                    foreach (var patch in (IEnumerable)GeometryType.GetField("patches", Hidden).GetValue(geometry))
                    {
                        var type = patch.GetType(); var color = (Color)type.GetField("Tint").GetValue(patch);
                        if (!(bool)type.GetField("Stroke").GetValue(patch) || !((Color32)color).Equals(new Color32(214, 215, 209, 220))) continue;
                        var box = (Rect)type.GetField("Box").GetValue(patch);
                        bool horizontal = box.height == 0; int count = Mathf.RoundToInt((horizontal ? box.width : box.height) / 32);
                        for (int i = 0; i < count; i++)
                        {
                            int x = Mathf.RoundToInt(box.x / 32) + (horizontal ? i : 0), y = Mathf.RoundToInt(box.y / 32) + (horizontal ? 0 : i);
                            bool Solid(int xx, int yy) => xx < 0 || yy < 0 || xx >= data.cols || yy >= data.rows || data.ground[yy * data.cols + xx] != 0;
                            Assert.That(Solid(x, y) || Solid(horizontal ? x : x - 1, horizontal ? y - 1 : y), Is.True, label + " false wall " + box);
                            boundaries++;
                        }
                    }
                }
                Assert.That(boundaries, Is.GreaterThan(0), label);
                Object.Destroy(root); root = null;
            }
        }

        Chapter1FullMap Source(ForestData data, bool[] found)
        {
            if (!root) root = new GameObject("Map frontier fixture");
            var map = root.AddComponent<Chapter1FullMap>(); map.enabled = false;
            typeof(Chapter1FullMap).GetField("source", Hidden).SetValue(map, new Chapter1MapSource { Data = data });
            typeof(Chapter1FullMap).GetField("discoveredRooms", Hidden).SetValue(map, found);
            return map;
        }
        Component Geometry(Vector2 size, float stroke)
        {
            var go = new GameObject("Actual map geometry", typeof(RectTransform)); go.transform.SetParent(root.transform);
            ((RectTransform)go.transform).sizeDelta = size;
            var geometry = go.AddComponent(GeometryType); GeometryType.GetField("StrokeWidth").SetValue(geometry, stroke); return geometry;
        }
        static void Present(Component geometry, Chapter1FullMap map) => GeometryType.GetMethod("Present").Invoke(geometry, new object[] { map, new Chapter1MapViewport(new Vector2(map.MapData.width, map.MapData.height)) });
        static VertexHelper Mesh(Component geometry) { var vertices = new VertexHelper(); GeometryType.GetMethod("OnPopulateMesh", Hidden, null, new[] { typeof(VertexHelper) }, null).Invoke(geometry, new object[] { vertices }); return vertices; }
        static bool Covers(VertexHelper mesh, Component geometry, ForestData data, Vector2 point, bool stroke)
        {
            var size = ((RectTransform)geometry.transform).sizeDelta;
            var at = new Chapter1MapViewport(new Vector2(data.width, data.height)).ToScreen(point, size);
            at = new Vector2(at.x - size.x / 2, size.y / 2 - at.y);
            for (int i = 0; i < mesh.currentVertCount; i += 4)
            {
                UIVertex a = default, b = default; mesh.PopulateUIVertex(ref a, i); mesh.PopulateUIVertex(ref b, i + 2);
                if (stroke && a.color.a < 200) continue;
                if (at.x >= a.position.x - .001f && at.x <= b.position.x + .001f && at.y <= a.position.y + .001f && at.y >= b.position.y - .001f) return true;
            }
            return false;
        }
        static ForestData Crossroads()
        {
            var data = new ForestData { cols = 17, rows = 17, width = 544, height = 544, ground = Enumerable.Repeat(1, 289).ToArray(), platforms = Array.Empty<FoliagePlatform>() };
            data.rooms = new[] { Room(0, 6, 6, 4, 4), Room(1, 13, 6, 3, 4), Room(2, 0, 6, 3, 4), Room(3, 6, 0, 4, 3), Room(4, 6, 13, 4, 3) };
            data.passages = new[] { Passage(1, 10, 7, 3, 2), Passage(2, 3, 7, 3, 2), Passage(3, 7, 3, 2, 3), Passage(4, 7, 10, 2, 3) };
            foreach (var room in data.rooms) Carve(room.x, room.y, room.w, room.h);
            foreach (var passage in data.passages) Carve(passage.x, passage.y, passage.w, passage.h);
            return data;
            void Carve(int x, int y, int w, int h) { for (int yy = y; yy < y + h; yy++) for (int xx = x; xx < x + w; xx++) data.ground[yy * 17 + xx] = 0; }
            ForestRoom Room(int id, int x, int y, int w, int h) => new ForestRoom { id = id, x = x, y = y, w = w, h = h };
            ForestPassage Passage(int to, int x, int y, int w, int h) => new ForestPassage { from = 0, to = to, x = x, y = y, w = w, h = h, spaces = new[] { new GroundRect { x = x, y = y, w = w, h = h } } };
        }
    }
}
