using System;
using System.Collections.Generic;
using System.IO;
using ForestCombatV2;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Stage1VillageExperiment.Editor
{
    // Temporary visual fixture on a private layer; authored map and colliders stay untouched.
    public static class VillageTerrainArtReview
    {
        public static void Capture(string output)
        {
            var transition = Object.FindAnyObjectByType<VillageArtTransition>();
            if (!transition) throw new InvalidOperationException("Village art transition is required for tile review.");
            var mode = transition.Mode; float weight = transition.Weight, boundary = transition.BoundaryPixels;
            var owned = new List<Object>();
            GameObject root = null, cameraObject = null;
            try
            {
                root = new GameObject("Tile assembly visual review");
                root.layer = 30;
                var shader = Resources.Load<Shader>("Village/VillageRegion");
                var presentation=transition.GetComponent<VillagePresentation>();
                var textures=Resources.LoadAll<Texture2D>("Village/color/terrain");
                Array.Sort(textures,(a,b)=>string.CompareOrdinal(a.name,b.name));
                var source=new Sprite[16];
                for(int i=0;i<15;i++)
                {
                    source[i]=Sprite.Create(textures[i],new Rect(0,0,32,32),Vector2.one*.5f,32);owned.Add(source[i]);
                }
                var seed=Resources.Load<Texture2D>("Village/color_seed");
                source[15]=Sprite.Create(seed,new Rect(0,0,32,32),Vector2.one*.5f,32);owned.Add(source[15]);
                var cells = new bool[30, 16];
                for (int y = 11; y < 16; y++) for (int x = 0; x < 30; x++) cells[x, y] = true;
                // Long flat, both wall orientations, one- and two-cell stairs.
                for (int x = 1; x < 8; x++) for (int y = 10 - x / 2; y < 11; y++) cells[x, y] = true;
                for (int x = 10; x < 15; x++) for (int y = 9 - (x - 10) / 2 * 2; y < 11; y++) cells[x, y] = true;
                // Cave opening beneath an intact ceiling and two narrow open gaps.
                for (int x = 18; x < 27; x++) for (int y = 4; y < 11; y++) cells[x, y] = true;
                for (int x = 20; x < 25; x++) for (int y = 6; y < 11; y++) cells[x, y] = false;
                for (int y = 11; y < 16; y++) { cells[8, y] = false; cells[16, y] = false; cells[17, y] = false; }
                Stage1TerrainQuarterRenderer.Build(root.transform,source,cells,presentation.TerrainMaterial,owned);
                var platforms = new[] { new FoliagePlatform { x=64,y=64,w=160 }, new FoliagePlatform { x=352,y=64,w=384 } };
                VillageReferenceGround.Build(root.transform,cells,new ForestData { platforms=platforms },owned);
                foreach (var platform in platforms)
                {
                    int length=platform.w/32,x=platform.x/32;
                    for (int i = 0; i < length; i++)
                    {
                        int index = i == 0 ? 1 : i == length - 1 ? 5 : 3;
                        string path = "color/platforms/platform_c" + index.ToString("00") + "_32x32";
                        var c = Resources.Load<Texture2D>("Village/" + path);
                        var s = Resources.Load<Texture2D>("Village/reference-sketch/" + path);
                        var material = new Material(shader); owned.Add(material);
                        material.mainTexture = c; material.SetTexture("_SketchTex", s); material.SetFloat("_HasSketch", 1); material.SetFloat("_HideSketch",1);
                        var sprite = Sprite.Create(c, new Rect(0, 0, 32, 32), new Vector2(0, 1), 32); owned.Add(sprite);
                        var go = new GameObject("Deck " + length + ":" + i); go.transform.SetParent(root.transform);
                        go.transform.position = new Vector3(x + i, -2, 0);
                        var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sharedMaterial = material;
                    }
                }
                foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.layer = 30;
                cameraObject = new GameObject("Native tile review camera");
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
                camera.orthographic = true; camera.orthographicSize = 540f / 64; camera.aspect = 960f / 540;
                camera.transform.position = new Vector3(15, -8, -10); camera.cullingMask = 1 << 30;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(246, 236, 218, 255);
                Directory.CreateDirectory(output);
                transition.SetBoundary(480);
                foreach (var display in new[] { VillageArtMode.Color, VillageArtMode.Sketch, VillageArtMode.Region })
                {
                    transition.SetDisplay(display);
                    Save(camera, Path.Combine(output, "tile-assembly-" + display.ToString().ToLowerInvariant() + ".png"));
                }
                transition.SetDisplay(VillageArtMode.Sketch);
                // Read rendered alpha at every terrain pixel, especially exposed turns.
                // Inset cell quads used to leave holes even with intact source canvases.
                camera.backgroundColor = Color.clear;
                var coverage = Save(camera, Path.Combine(output, "tile-assembly-coverage.png"));
                int missing = 0, checkedPixels = 0;
                for (int y = 0; y < 16 * 32; y++) for (int x = 0; x < 30 * 32; x++)
                {
                    if (!cells[x / 32, y / 32]) continue;
                    int row = 539 - (y + 14);
                    checkedPixels++;
                    if (coverage[row * 960 + x].a < 250) missing++;
                }
                File.WriteAllText(Path.Combine(output, "terrain-join-coverage.json"),
                    "{\"checkedTerrainPixels\":" + checkedPixels + ",\"missingPixels\":" + missing + "}");
                if (missing != 0) throw new InvalidOperationException("Open sketch terrain pixels at joins: " + missing);
                camera.backgroundColor = new Color32(246, 236, 218, 255);
                camera.transform.position += new Vector3(1, -.5f, 0);
                Save(camera, Path.Combine(output, "tile-assembly-sketch-scrolled.png"));
            }
            finally
            {
                transition.SetBoundary(boundary); transition.SetDisplay(mode, weight);
                foreach (var item in owned) if (item) Object.DestroyImmediate(item);
                if (root) Object.DestroyImmediate(root);
                if (cameraObject) Object.DestroyImmediate(cameraObject);
            }
        }

        static Color32[] Save(Camera camera, string path)
        {
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(960, 540, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(960, 540, TextureFormat.RGBA32, false);
            try
            {
                target.filterMode = FilterMode.Point; camera.targetTexture = target; camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                return image.GetPixels32();
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(image);
            }
        }
    }
}
