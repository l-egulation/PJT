using System.Collections.Generic;
using UnityEngine;

namespace Deviant.Combat
{
    // Contact shadows (2026-11-12, user choice A "옅게"): a flat pixel shadow on the surface under the player and every
    // enemy. It multiplies the ground by 0.8, so inside it each terrain colour steps one shade down (cream to light
    // grey, light grey to mid grey) and the grass and soil marks show through. It lies on the ground or platform
    // straight below the feet: while an actor jumps the shadow stays there, narrower and fainter with height, and it
    // goes away above MaxHeight or with nothing below. Rows of whole 2px dots, the middle rows widest.
    public sealed class ContactShadowPresentation
    {
        public const float Multiplier = .8f, MaxHeight = 8, WidthPerBody = 1.2f;
        const int RowPixels = 2, AbovePixels = 2;
        static readonly int[] RowInsets = { 4, 0, 0, 4, 10 };   // pixels in from each end, top row first

        readonly CombatArena arena;
        readonly ArenaSurfaceQueryBuffer queries = new ArenaSurfaceQueryBuffer();
        readonly Dictionary<CombatActor, SpriteRenderer> shadows = new Dictionary<CombatActor, SpriteRenderer>();
        readonly Dictionary<int, Sprite> shapes = new Dictionary<int, Sprite>();
        readonly List<CombatActor> stale = new List<CombatActor>();
        Material material;
        Transform root;
        public bool Enabled = true;

        public ContactShadowPresentation(CombatArena arena) { this.arena = arena; }

        public void Tick()
        {
            if (!EnsureMaterial()) return;
            stale.Clear(); stale.AddRange(shadows.Keys);
            if (arena.Player) Place(arena.Player);
            foreach (var enemy in arena.enemies) if (enemy) Place(enemy);
            foreach (var gone in stale)
            {
                if (shadows.TryGetValue(gone, out var r) && r) r.enabled = false;
                if (!gone) { if (r) Object.Destroy(r.gameObject); shadows.Remove(gone); }
            }
        }

        bool EnsureMaterial()
        {
            if (material) return true;
            var shader = Resources.Load<Shader>("ContactShadowMultiply");
            if (!shader) return false;
            material = new Material(shader) { name = "Contact shadow multiply" };
            root = new GameObject("Contact shadows").transform; root.SetParent(arena.transform, false);
            return true;
        }

        void Place(CombatActor actor)
        {
            stale.Remove(actor);
            var r = Renderer(actor);
            bool visible = Enabled && actor.visual && actor.visual.enabled && actor.gameObject.activeInHierarchy && !actor.Dead && !actor.Hidden;
            if (!visible || !Ground(actor.Position, out float surface)) { r.enabled = false; return; }
            float height = actor.Position.y - surface;
            if (height > MaxHeight || height < -.5f) { r.enabled = false; return; }
            float lift = Mathf.Clamp01(height / MaxHeight);
            int width = Mathf.Max(8, Mathf.RoundToInt(actor.size.x * WidthPerBody * (1 - .5f * lift) * 32 / 4) * 4);
            r.sprite = Shape(width);
            r.color = new Color(Multiplier, Multiplier, Multiplier, 1 - .6f * lift);
            r.sortingLayerID = actor.visual.sortingLayerID; r.sortingOrder = actor.visual.sortingOrder - 1;
            r.transform.position = new Vector3(Mathf.Round(actor.Position.x * 16) / 16, Mathf.Round(surface * 16) / 16, 0);
            r.enabled = true;
        }

        SpriteRenderer Renderer(CombatActor actor)
        {
            if (shadows.TryGetValue(actor, out var r) && r) return r;
            var go = new GameObject("Contact shadow"); go.transform.SetParent(root, false);
            r = go.AddComponent<SpriteRenderer>(); r.sharedMaterial = material; r.enabled = false;
            return shadows[actor] = r;
        }

        // The first walkable surface straight below the feet (ground or a platform top), within MaxHeight.
        bool Ground(Vector2 feet, out float surface)
        {
            surface = 0;
            queries.Raycast(feet + Vector2.up * .2f, Vector2.down, MaxHeight + .2f);
            float best = float.PositiveInfinity;
            foreach (var hit in queries.Hits)
            {
                if (!hit.collider || hit.collider.isTrigger || !hit.collider.GetComponent<ArenaSurface>()) continue;
                if (hit.distance < best) { best = hit.distance; surface = hit.point.y; }
            }
            return !float.IsPositiveInfinity(best);
        }

        // A shadow shape `width` pixels wide: five rows of whole 2px dots, its top row 2px above the surface.
        Sprite Shape(int width)
        {
            if (shapes.TryGetValue(width, out var sprite)) return sprite;
            int height = RowInsets.Length * RowPixels;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Contact shadow " + width };
            var px = new Color32[width * height];
            for (int row = 0; row < RowInsets.Length; row++)
                for (int k = 0; k < RowPixels; k++)
                {
                    int y = height - 1 - (row * RowPixels + k);     // texture rows count up from the bottom
                    for (int x = RowInsets[row]; x < width - RowInsets[row]; x++) px[y * width + x] = new Color32(255, 255, 255, 255);
                }
            texture.SetPixels32(px); texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, (height - AbovePixels) / (float)height), 32, 0, SpriteMeshType.FullRect);
            sprite.name = texture.name;
            return shapes[width] = sprite;
        }

        public void Dispose()
        {
            if (root) Object.Destroy(root.gameObject);
            if (material) Object.Destroy(material);
            foreach (var s in shapes.Values) if (s) { Object.Destroy(s.texture); Object.Destroy(s); }
            shapes.Clear(); shadows.Clear();
        }
    }
}
