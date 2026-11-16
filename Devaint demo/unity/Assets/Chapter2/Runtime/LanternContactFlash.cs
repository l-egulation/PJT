using System;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using Deviant.Integration;
using UnityEngine;

namespace Deviant.Chapter2
{
    [Serializable] public sealed class ContactBurstData
    {
        public int sparks;
        public float duration, speed, length, width, spreadDegrees, gravity, flashDuration, flashRadius;
        public void Validate()
        {
            if (sparks < 1 || sparks > 12) throw new ArgumentException("Use one to twelve contact sparks.");
            LightingValidation.Positive(duration, "contact lifetime"); LightingValidation.Positive(speed, "spark speed");
            LightingValidation.Positive(length, "spark length"); LightingValidation.Positive(width, "spark width");
            LightingValidation.Positive(spreadDegrees, "spark fan"); LightingValidation.Nonnegative(gravity, "spark gravity");
            LightingValidation.Nonnegative(flashDuration, "impact flash duration"); LightingValidation.Nonnegative(flashRadius, "impact flash radius");
            if (flashDuration > duration || spreadDegrees >= 90) throw new ArgumentException("The flash must fit the lifetime and sparks must leave the contact face.");
        }
    }
    [Serializable] public sealed class ContactFlashData { public ContactBurstData body, melee; }

    // Accepted environment contacts only. World-space presentation; no damage, hitstop or reward.
    public sealed class LanternContactFlash : MonoBehaviour
    {
        CombatArena arena;
        ReactiveLanternBridge bridge;
        ContactFlashData settings;
        ContactBurstData burst;
        Mesh mesh;
        MeshRenderer output;
        Material material;
        readonly List<Vector3> vertices = new List<Vector3>(64);
        readonly List<Color> colors = new List<Color>(64);
        readonly List<int> triangles = new List<int>(192);
        Vector2 origin, normal;
        int playerId;
        float age;
        public int EmittedCount { get; private set; }
        public bool Visible => output && output.enabled;
        public bool LastWasMelee { get; private set; }
        public Vector2 LastContactPoint { get; private set; }

        public void Bind(CombatArena owner, ReactiveLanternBridge source, SpriteRenderer housing, Shader shader, ContactFlashData data)
        {
            if (arena || !owner || !source || !housing || !shader || data?.body == null || data.melee == null)
                throw new ArgumentException("A complete, single-owner contact burst is required.");
            data.body.Validate(); data.melee.Validate();
            arena = owner; bridge = source; settings = data; playerId = owner.Player.ActorId;
            var node = new GameObject("Contact sparks"); node.transform.SetParent(transform, false);
            mesh = new Mesh { name = "Lantern contact sparks" }; mesh.MarkDynamic();
            node.AddComponent<MeshFilter>().sharedMesh = mesh;
            material = new Material(shader) { name = "Lantern ivory impact" };
            output = node.AddComponent<MeshRenderer>(); output.sharedMaterial = material;
            output.sortingLayerID = housing.sortingLayerID; output.sortingOrder = housing.sortingOrder + 7;
            output.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; output.receiveShadows = false;
            source.BodyImpacted += BodyImpact; source.MeleeImpacted += MeleeImpact;
            arena.SimulationCompleted += Step; arena.ResetCompleted += ResetFlash; arena.Deactivated += ResetFlash;
            ResetFlash();
        }
        void BodyImpact(LanternImpact impact) => Emit(impact, false);
        void MeleeImpact(LanternImpact impact) => Emit(impact, true);
        void Emit(LanternImpact impact, bool melee)
        {
            if (!isActiveAndEnabled || impact.ActorId != playerId) return;
            burst = melee ? settings.melee : settings.body;
            origin = impact.Point; normal = impact.Normal.normalized;
            LastContactPoint = origin; LastWasMelee = melee; EmittedCount++; age = 0; Draw();
        }
        void Step(CombatCompletedStep step)
        {
            if (!isActiveAndEnabled || burst == null || age >= burst.duration) return;
            age = Mathf.Min(burst.duration, age + step.Delta); Draw();
        }
        void Vertex(Vector2 world, Color color)
        {
            // At 32 pixels/unit a one-pixel stroke needs half-pixel edges.
            // Rounding both edges to whole pixels can collapse the complete triangle.
            world = new Vector2(Mathf.Round(world.x * 64) / 64, Mathf.Round(world.y * 64) / 64);
            vertices.Add(transform.InverseTransformPoint(world)); colors.Add(color);
        }
        void Streak(Vector2 tail, Vector2 head, float width, Color color)
        {
            var tangent = new Vector2(-(head - tail).y, (head - tail).x).normalized * width * .5f;
            int first = vertices.Count;
            Vertex(tail + tangent, color); Vertex(tail - tangent, color);
            Vertex(head - tangent * .35f, color); Vertex(head + tangent * .35f, color);
            triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
            triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
        }
        void Core(float scale)
        {
            int first = vertices.Count; var ivory = new Color(1, .97f, .84f, 1);
            Vertex(origin, ivory);
            // Four unequal points: an impact glint, rather than a persistent symmetric marker.
            var across = new Vector2(-normal.y, normal.x);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                float radius = i % 2 == 0 ? (i == 2 ? 1.15f : i == 6 ? .8f : .65f) : .22f;
                Vertex(origin + (normal * Mathf.Cos(angle) + across * Mathf.Sin(angle)) * (radius * scale), ivory);
            }
            for (int i = 0; i < 8; i++) { triangles.Add(first); triangles.Add(first + i + 1); triangles.Add(first + (i + 1) % 8 + 1); }
        }
        void Draw()
        {
            bool active = burst != null && age < burst.duration;
            output.enabled = active; mesh.Clear(); if (!active) return;
            vertices.Clear(); colors.Clear(); triangles.Clear();
            float progress = age / burst.duration;
            float grow = Mathf.Clamp01(age / .025f);
            float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.48f, 1, progress));
            var across = new Vector2(-normal.y, normal.x);
            for (int i = 0; i < burst.sparks; i++)
            {
                float fan = burst.sparks == 1 ? 0 : (i / (float)(burst.sparks - 1) * 2 - 1);
                float angle = (fan * burst.spreadDegrees + (i % 2 == 0 ? 5 : -4)) * Mathf.Deg2Rad;
                var direction = normal * Mathf.Cos(angle) + across * Mathf.Sin(angle);
                float speed = burst.speed * (.8f + .13f * (i % 3));
                float distance = speed * age / (1 + age * 2);
                var head = origin + direction * (.045f + distance) + Vector2.down * (.5f * burst.gravity * age * age);
                float length = burst.length * grow * Mathf.Lerp(1, .25f, progress);
                var color = Color.Lerp(new Color(1, .97f, .84f, 1), new Color(.86f, .64f, .3f, 1), progress);
                color.a = fade;
                Streak(head - direction * length, head, Mathf.Max(1f / 32, burst.width * fade), color);
            }
            if (burst.flashDuration > 0 && age < burst.flashDuration)
                Core(burst.flashRadius * Mathf.Sin(Mathf.PI * age / burst.flashDuration));
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
        }
        void ResetFlash()
        {
            EmittedCount = 0; LastWasMelee = false; burst = null;
            if (output) output.enabled = false; if (mesh) mesh.Clear();
        }
        void OnDisable() => ResetFlash();
        void OnDestroy()
        {
            if (bridge) { bridge.BodyImpacted -= BodyImpact; bridge.MeleeImpacted -= MeleeImpact; }
            if (arena) { arena.SimulationCompleted -= Step; arena.ResetCompleted -= ResetFlash; arena.Deactivated -= ResetFlash; }
            if (mesh) Destroy(mesh); if (material) Destroy(material);
        }
    }
}
