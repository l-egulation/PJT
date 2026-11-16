using System.Collections;
using Deviant.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1PlayerOutlineShaderTests
    {
        static readonly Color Ink = new Color32(34, 28, 24, 255);

        [Test] public void OutlineMaterialLoadsWithInkColorAndCompiles()
        {
            var material = Resources.Load<Material>(ForestCombatWorld.PlayerOutlineMaterial);
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("ForestCombatV2/PlayerOutline"));
            Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
            Assert.That(material.shader.isSupported, Is.True);
            Assert.That(material.GetFloat("_OutlineStrength"), Is.EqualTo(1));
            var color = material.GetColor("_OutlineColor");
            Assert.That(Vector4.Distance(color, Ink), Is.LessThan(.002f));
        }

        // One render pixel per sprite texel: a 4x4 opaque block in an 8x8 transparent frame.
        [TestCase(1, FilterMode.Point)]
        [TestCase(4, FilterMode.Point)]
        [TestCase(4, FilterMode.Bilinear)]
        public void OutlineFillsOnlyTransparentTexelsTouchingTheSpriteEdge(int resolutionScale, FilterMode filterMode)
        {
            int size = 8 * resolutionScale;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = filterMode, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) pixels[y * size + x] = x >= 2 * resolutionScale && x < 6 * resolutionScale && y >= 2 * resolutionScale && y < 6 * resolutionScale ? Color.white : Color.clear;
            texture.SetPixels(pixels); texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, 32 * resolutionScale, 0, SpriteMeshType.FullRect);
            var target = new RenderTexture(8, 8, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            var root = new GameObject("player outline render test");
            try
            {
                var renderer = new GameObject("sprite").AddComponent<SpriteRenderer>();
                renderer.transform.SetParent(root.transform, false);
                renderer.sprite = sprite;
                renderer.sharedMaterial = Resources.Load<Material>(ForestCombatWorld.PlayerOutlineMaterial);
                var camera = new GameObject("camera").AddComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true; camera.orthographicSize = .125f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
                var request = new RenderPipeline.StandardRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(camera, request);

                var previous = RenderTexture.active; RenderTexture.active = target;
                var read = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                read.ReadPixels(new Rect(0, 0, 8, 8), 0, 0); read.Apply();
                RenderTexture.active = previous;
                Color At(int x, int y) => read.GetPixel(x, y);

                AssertColor(At(3, 3), Color.white, "sprite body keeps its own color");
                AssertColor(At(1, 3), Ink, "left edge");
                AssertColor(At(6, 4), Ink, "right edge");
                AssertColor(At(4, 1), Ink, "bottom edge");
                AssertColor(At(3, 6), Ink, "top edge");
                Assert.That(At(1, 1).a, Is.LessThan(.01f), "diagonal corner stays empty (4-neighbour outline)");
                Assert.That(At(0, 3).a, Is.LessThan(.01f), "outline is one texel wide");
                Object.DestroyImmediate(read);
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(target);
                Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture);
            }
        }

        static void AssertColor(Color actual, Color expected, string label)
        {
            Assert.That(actual.a, Is.GreaterThan(.99f), label);
            Assert.That(Mathf.Abs(actual.r - expected.r) + Mathf.Abs(actual.g - expected.g) + Mathf.Abs(actual.b - expected.b), Is.LessThan(.03f), label + $" was {actual}");
        }
    }

    public sealed class Chapter1PlayerOutlineIntegrationTests
    {
        [UnitySetUp] public IEnumerator Setup()
        {
            EditorSceneManager.OpenScene("Assets/MeadowDuskV3/MapVer2/Scenes/Chapter1Procedural.unity");
            yield return new EnterPlayMode(); yield return null; yield return null;
        }
        [UnityTearDown] public IEnumerator Teardown() { yield return new ExitPlayMode(); }

        [UnityTest] public IEnumerator OnlyThePlayerUsesTheOutlineMaterial()
        {
            var world = Object.FindFirstObjectByType<ForestCombatWorld>(); Assert.That(world.Ready, Is.True);
            var arena = Object.FindFirstObjectByType<CombatArena>();
            var outline = Resources.Load<Material>(ForestCombatWorld.PlayerOutlineMaterial);
            Assert.That(arena.Player.visual.sharedMaterial, Is.SameAs(outline));
            Assert.That(arena.spriteMaterial, Is.Not.SameAs(outline));
            Assert.That(arena.enemies, Is.Not.Empty);
            foreach (var enemy in arena.enemies) if (enemy.visual) Assert.That(enemy.visual.sharedMaterial, Is.Not.SameAs(outline), enemy.name);
            yield break;
        }
    }
}
