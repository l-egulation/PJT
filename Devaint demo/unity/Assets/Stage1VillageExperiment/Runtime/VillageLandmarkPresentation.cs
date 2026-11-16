using System;
using System.Collections.Generic;
using ForestCombatV2;
using UnityEngine;

namespace Stage1VillageExperiment
{
    // Navigation art for the experimental layout. These signs do not start an
    // encounter, award treasure, lock a room, or perform a chapter transition.
    [DefaultExecutionOrder(650)]
    public sealed class VillageLandmarkPresentation : MonoBehaviour
    {
        public Stage1VillageWorld world;
        public bool showAreaNames = false;
        public bool Ready { get; private set; }
        public int MarkerCount { get; private set; }

        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly List<Label> labels = new List<Label>();
        Material flatMaterial,inkMaterial;
        Font font;

        sealed class Label
        {
            public TextMesh text;
            public Transform backing;
            public int attempts;
            public bool fitted;
        }

        static readonly Color Wood = new Color32(91, 76, 58, 255);
        static readonly Color WoodLight = new Color32(155, 133, 93, 255);
        static readonly Color Cream = new Color32(239, 228, 201, 255);
        static readonly Color RedCloth = new Color32(135, 68, 55, 255);
        static readonly Color Gold = new Color32(180, 148, 72, 255);

        void Start()
        {
            if (!world) world = GetComponent<Stage1VillageWorld>();
            TryBuild();
        }

        void LateUpdate()
        {
            if (!Ready) TryBuild();
            foreach (var label in labels)
            {
                if (label.fitted || !label.text) continue;
                var renderer = label.text.GetComponent<MeshRenderer>();
                var size = renderer.bounds.size;
                if (size.y <= .0001f || size.x <= .0001f)
                {
                    if (++label.attempts > 12) label.fitted = true;
                    continue;
                }
                float factor = Mathf.Min((14f / 32) / size.y, (214f / 32) / size.x);
                label.text.transform.localScale *= factor;
                label.backing.localScale = new Vector3(Mathf.Max(1.6f, size.x * factor + .5f), .78f, 1);
                label.fitted = true;
            }
        }

        void TryBuild()
        {
            if (Ready || !world || !world.Ready) return;
            var presentation=world.GetComponent<VillagePresentation>();
            if(!presentation||!presentation.Ready)return;
            font = world.diagnostics ? world.diagnostics.uiFont : null;
            if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // Original vertex colors remain intact in normal areas; sketch uses paper/ink.
            flatMaterial=presentation.MakeLandmarkMaterial(false);
            inkMaterial=presentation.MakeLandmarkMaterial(true);

            if (world.Data.landmarks != null)
                foreach (var landmark in world.Data.landmarks)
                    if (landmark != null && IsVisibleKind(landmark.kind))
                        BuildMarker(landmark.kind, landmark.name, landmark.x, landmark.y, landmark.zone);
            // This is the reference-map exit sign only. No transition is implied.
            BuildMarker("exit_marker", "수로 출구", 475 * 32, 108 * 32, 0);
            Ready = true;
            Debug.Log($"VILLAGE_LANDMARKS_READY markers={MarkerCount}; encounter/reward/transition behavior remains outside this presentation.");
        }

        static bool IsVisibleKind(string kind) =>
            kind == "start" || kind == "treasure" || kind == "midboss_marker" || kind == "boss_marker";

        void BuildMarker(string kind, string title, float pixelX, float pixelY, int zone)
        {
            bool boss = kind == "midboss_marker" || kind == "boss_marker";
            var root = new GameObject($"Village landmark {zone:00} {kind}").transform;
            root.SetParent(transform, false);
            // A sign beside the authored standing point leaves the arrival actor clear.
            float side = kind == "exit_marker" ? -1.1f : 1.6f;
            root.position = Stage1VillageWorld.PixelFeet(pixelX, pixelY) + new Vector2(side, 0);
            float poleHeight = boss ? 3.35f : 2.15f;
            Quad(root, "Wood post", new Vector2(0, poleHeight * .5f), new Vector2(3f / 32, poleHeight), Wood, -8);
            Quad(root, "Post lit edge", new Vector2(1f / 32, poleHeight * .5f), new Vector2(1f / 32, poleHeight), WoodLight, -7,true);

            if (boss)
            {
                // A restrained cloth silhouette gives the two halls an identity
                // without depicting an invented boss or a new combat mechanic.
                float top = 3.1f, bottom = .95f;
                Polygon(root, "Notched red cloth", new[]
                {
                    new Vector2(-.68f, top), new Vector2(.68f, top),
                    new Vector2(.68f, bottom), new Vector2(0, bottom + .34f),
                    new Vector2(-.68f, bottom)
                }, new[] { 0, 1, 3, 1, 2, 3, 0, 3, 4 }, RedCloth, -7);
                Quad(root, "Cloth upper seam", new Vector2(0, top - .09f), new Vector2(1.36f, 2f / 32), Gold, -6,true);
                Quad(root, "Cloth vertical stitch", new Vector2(-.51f, 2.02f), new Vector2(1f / 32, 1.7f), WoodLight, -6,true);
                // Two plain bars are woven trim, not a boss-state status icon.
                Quad(root, "Cloth center trim", new Vector2(0, 2.18f), new Vector2(.64f, 2f / 32), Cream, -6,true);
                Quad(root, "Cloth lower trim", new Vector2(0, 1.94f), new Vector2(.4f, 2f / 32), Cream, -6,true);
            }
            else
            {
                Color accent = kind == "treasure" ? Gold : new Color32(122, 151, 143, 255);
                Quad(root, "Small direction sign", new Vector2(.15f, 1.68f), new Vector2(1.5f, .55f), Wood, -7);
                Quad(root, "Sign top edge", new Vector2(.15f, 1.92f), new Vector2(1.5f, 1f / 32), WoodLight, -6,true);
                Quad(root, "Painted sign inset", new Vector2(.15f, 1.68f), new Vector2(.72f, 2f / 32), accent, -6,true);
                Polygon(root, "Small direction arrow", new[]
                {
                    new Vector2(.49f, 1.82f), new Vector2(.69f, 1.68f), new Vector2(.49f, 1.54f)
                }, new[] { 0, 1, 2 }, accent, -6,true);
            }

            if (showAreaNames)
            {
                string name = kind == "midboss_marker" ? "중간 보스 홀" : kind == "boss_marker" ? "보스 저택" : title;
                BuildLabel(root, name, boss ? 3.78f : 2.57f);
            }
            MarkerCount++;
        }

        void BuildLabel(Transform root, string text, float y)
        {
            var backing = Quad(root, "Wood nameplate", new Vector2(0, y), new Vector2(4, .78f),
                new Color32(66, 59, 51, 240), -5);
            var go = new GameObject("Area name");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0, y, -.01f);
            // Bound-based fitting follows once Unity has populated the glyph mesh;
            // start small so a first-frame capture never sees enormous text.
            go.transform.localScale = Vector3.one * .08f;
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = font;
            mesh.fontSize = 64;
            mesh.characterSize = 1;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Cream;
            font.RequestCharactersInTexture(text, 64, FontStyle.Normal);
            mesh.text = text;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = -4;
            labels.Add(new Label { text = mesh, backing = backing.transform });
        }

        GameObject Quad(Transform root, string name, Vector2 center, Vector2 size, Color color, int order,bool ink=false)
        {
            var go = Polygon(root, name, new[]
            {
                new Vector2(-.5f, -.5f), new Vector2(.5f, -.5f),
                new Vector2(.5f, .5f), new Vector2(-.5f, .5f)
            }, new[] { 0, 1, 2, 0, 2, 3 }, color, order,ink);
            go.transform.localPosition = center;
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            return go;
        }

        GameObject Polygon(Transform root, string name, Vector2[] points, int[] triangles, Color color, int order,bool ink=false)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false);
            var vertices = new Vector3[points.Length];
            var uv = new Vector2[points.Length];
            var colors = new Color[points.Length];
            for (int i = 0; i < points.Length; i++) { vertices[i] = points[i]; uv[i] = Vector2.one * .5f; colors[i] = QualitySettings.activeColorSpace==ColorSpace.Linear?color.linear:color; }
            var mesh = new Mesh { name = "Village sign geometry" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = ink?inkMaterial:flatMaterial;
            renderer.sortingOrder = order;
            owned.Add(mesh);
            return go;
        }

        void OnDestroy()
        {
            foreach (var item in owned) if (item) Destroy(item);
            owned.Clear();
        }
    }
}
