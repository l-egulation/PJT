using System;
using System.Collections.Generic;
using Deviant.Definitions;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Deviant.Presentation.Unity
{
    // Finite, collimated window illumination. Native Light2D is only the emitter's
    // settings/footprint handle: its radial contribution and shadows stay disabled.
    [DefaultExecutionOrder(1100)]
    public sealed class WindowLightView : MonoBehaviour
    {
        [SerializeField, Range(-180, 180)] float angleDegrees;
        WindowLightDefinition definition;
        WindowRect aperture, room;
        WindowAperture openings;
        WindowPoint[] footprint;
        LightEmitterView emitter;
        Material beamMaterial;
        Mesh beamMesh;
        MeshRenderer beamRenderer;
        readonly Vector4[] planes = new Vector4[16];
        const int MaxShadowEdges = 128;
        readonly Vector4[] edges = new Vector4[MaxShadowEdges], edgeOwners = new Vector4[MaxShadowEdges];
        readonly List<SpriteShadowSource> casters = new List<SpriteShadowSource>();
        readonly Dictionary<UnityEngine.Object, int> ownerIds = new Dictionary<UnityEngine.Object, int>();
        readonly List<WindowLightReceiver> receivers = new List<WindowLightReceiver>();
        readonly WindowLightBuffer shaftData = new WindowLightBuffer();
        readonly Vector4[] openingPlanes = new Vector4[WindowAperture.MaxVertices], openingPieces = new Vector4[WindowAperture.MaxPieces];
        MaterialPropertyBlock shaftBlock;
        int edgeCount, nextOwnerId;
        bool warnedOverflow;
        bool bound, emissionEnabled = true, showShaft = true;
        float builtAngle;
        public float AngleDegrees => angleDegrees;
        public int FootprintVertexCount => footprint?.Length ?? 0;
        public int RebuildCount { get; private set; }
        public LightEmitterView Emitter => emitter;
        public WindowRect Aperture => aperture;
        public bool ShadowEdgeOverflow { get; private set; }
        public int ShadowEdgeCount => edgeCount;
        public int ReceiverCount => receivers.Count;
        public void Bind(Rect opening, Rect receivingRoom, WindowLightDefinition settings, LightEmitterView light, Shader shaftShader, int sortingLayerId, int sortingOrder)
            => Bind(WindowAperture.Rectangle(RectOf(opening)), receivingRoom, settings, light, shaftShader, sortingLayerId, sortingOrder);
        public void Bind(WindowApertureArt art, Rect receivingRoom, WindowLightDefinition settings, LightEmitterView light, Shader shaftShader, int sortingLayerId, int sortingOrder)
            => Bind(art ? art.Capture() : throw new ArgumentNullException(nameof(art)), receivingRoom, settings, light, shaftShader, sortingLayerId, sortingOrder);
        public void Bind(WindowAperture opening, Rect receivingRoom, WindowLightDefinition settings, LightEmitterView light, Shader shaftShader, int sortingLayerId, int sortingOrder)
        {
            if (bound) throw new InvalidOperationException("The window already has an owner.");
            if (settings == null || !light || light.Definition == null || !shaftShader) throw new ArgumentException("Window settings, a bound emitter and shaft shader are required.");
            openings = opening ?? throw new ArgumentNullException(nameof(opening));
            aperture = openings.Bounds; room = RectOf(receivingRoom);
            if (settings.EdgeFeather * 2 >= receivingRoom.width || settings.EdgeFeather * 2 >= receivingRoom.height)
                throw new ArgumentException("The feather must fit inside the receiving room.");
            definition = settings; emitter = light; angleDegrees = settings.AngleDegrees;
            CaptureOpeningPlanes();
            shaftBlock = new MaterialPropertyBlock();
            beamMaterial = new Material(shaftShader) { name = "Window shaft / " + name };
            beamMesh = new Mesh { name = "Finite window beam" };
            var node = new GameObject("Window light shaft"); node.transform.SetParent(transform, false);
            node.AddComponent<MeshFilter>().sharedMesh = beamMesh; beamRenderer = node.AddComponent<MeshRenderer>();
            beamRenderer.sharedMaterial = beamMaterial; beamRenderer.sortingLayerID = sortingLayerId; beamRenderer.sortingOrder = sortingOrder;
            beamRenderer.shadowCastingMode = ShadowCastingMode.Off; beamRenderer.receiveShadows = false;
            bound = true; Rebuild();
        }
        public void SetAperture(WindowAperture opening)
        {
            if (!bound) throw new InvalidOperationException("Bind the window before changing its aperture.");
            openings = opening ?? throw new ArgumentNullException(nameof(opening)); aperture = openings.Bounds;
            CaptureOpeningPlanes(); Rebuild();
        }
        void CaptureOpeningPlanes()
        {
            Array.Clear(openingPlanes, 0, openingPlanes.Length); Array.Clear(openingPieces, 0, openingPieces.Length);
            int planeIndex = 0;
            for (int p = 0; p < openings.PieceCount; p++)
            {
                int count = openings.GetVertexCount(p); openingPieces[p] = new Vector4(planeIndex, count, 0, 0);
                for (int i = 0; i < count; i++)
                {
                    var a = openings.GetVertex(p, i); var b = openings.GetVertex(p, (i + 1) % count);
                    var normal = new Vector2(a.Y - b.Y, b.X - a.X).normalized;
                    openingPlanes[planeIndex++] = new Vector4(normal.x, normal.y, -normal.x * a.X - normal.y * a.Y, 0);
                }
            }
        }
        static WindowRect RectOf(Rect rect) => new WindowRect(rect.xMin, rect.yMin, rect.xMax, rect.yMax);
        int OwnerId(UnityEngine.Object owner)
        {
            if (!owner) return 0;
            if (!ownerIds.TryGetValue(owner, out int id)) { id = ++nextOwnerId; ownerIds.Add(owner, id); }
            return id;
        }
        public void SetShadowCasters(IEnumerable<SpriteShadowSource> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            casters.Clear();
            foreach (var source in sources) if (source && !casters.Contains(source))
            { casters.Add(source); OwnerId(source.target ? (UnityEngine.Object)source.target : source); }
            SampleNow();
        }
        public void AddReceiver(SpriteRenderer sprite)
        {
            if (!bound) throw new InvalidOperationException("Bind the window before registering receivers.");
            if (!sprite || !sprite.sharedMaterial || !sprite.sharedMaterial.HasProperty("_WindowCount"))
                throw new ArgumentException("A directional-window-compatible receiver material is required.");
            var receiver = sprite.GetComponent<WindowLightReceiver>();
            if (!receiver) receiver = sprite.gameObject.AddComponent<WindowLightReceiver>();
            receiver.Register(this);
            if (!receivers.Contains(receiver)) receivers.Add(receiver);
            OwnerId(sprite); SampleNow();
        }
        public void RemoveReceiver(SpriteRenderer sprite)
        {
            if (!sprite) return;
            var receiver = sprite.GetComponent<WindowLightReceiver>();
            if (receiver && receivers.Remove(receiver)) receiver.Unregister(this);
        }
        public void SetAngle(float degrees)
        {
            WindowLightDefinition.ValidateAngle(degrees);
            if (angleDegrees == degrees) return;
            angleDegrees = degrees; if (bound) Rebuild();
        }
        public void SetEnabled(bool enabled)
        { emissionEnabled = enabled; if (emitter) emitter.Output.enabled = enabled && FootprintVertexCount >= 3 && isActiveAndEnabled; SampleNow(); }
        public void SetShaftVisible(bool visible) { showShaft = visible; SampleNow(); }
        public bool ContainsWorldPoint(Vector2 point)
        {
            var local = transform.InverseTransformPoint(point);
            var sample = new WindowPoint(local.x, local.y);
            return bound && WindowLightGeometry.Contains(footprint, sample) && openings.TrySourceDistance(sample, WindowLightGeometry.Direction(angleDegrees), definition.Length, out _);
        }
        public Vector3 GetFootprintWorldPoint(int index)
        { var p = footprint[index]; return transform.TransformPoint(new Vector3(p.X, p.Y)); }
        void Rebuild()
        {
            var coreRoom = new WindowRect(room.Left + definition.EdgeFeather, room.Bottom + definition.EdgeFeather,
                room.Right - definition.EdgeFeather, room.Top - definition.EdgeFeather);
            var next = WindowLightGeometry.Build(aperture, coreRoom, angleDegrees, definition.Length);
            if (next.Length > planes.Length) throw new InvalidOperationException("Window geometry exceeds the convex shader budget.");
            footprint = next; builtAngle = angleDegrees; RebuildCount++;
            beamMesh.Clear();
            if (footprint.Length < 3) { emitter.Output.enabled = false; beamRenderer.enabled = false; ClearReceivers(); return; }
            var origin = aperture.Center;
            emitter.transform.SetParent(transform, false); emitter.transform.localPosition = new Vector3(origin.X, origin.Y);
            emitter.transform.localRotation = Quaternion.identity; emitter.transform.localScale = Vector3.one;
            // Do not move a point far away to approximate a sun: this removes the
            // point-light evaluation entirely for this window, including its normals.
            emitter.SetNativeContribution(false);
            emitter.Output.lightType = Light2D.LightType.Freeform; emitter.Output.shapeLightFalloffSize = definition.EdgeFeather;
            var path = new Vector3[footprint.Length]; var vertices = new Vector3[footprint.Length]; var triangles = new int[(footprint.Length - 2) * 3];
            for (int i = 0; i < footprint.Length; i++)
            {
                var p = footprint[i]; vertices[i] = new Vector3(p.X, p.Y); path[i] = vertices[i] - emitter.transform.localPosition;
                var end = footprint[(i + 1) % footprint.Length]; var direction = new Vector2(end.X - p.X, end.Y - p.Y);
                var normal = new Vector2(-direction.y, direction.x).normalized;
                planes[i] = new Vector4(normal.x, normal.y, -Vector2.Dot(normal, new Vector2(p.X, p.Y)), 0);
                if (i < footprint.Length - 2) { triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = i + 2; }
            }
            emitter.Output.SetShapePath(path); emitter.Output.enabled = emissionEnabled && isActiveAndEnabled;
            beamMesh.vertices = vertices; beamMesh.triangles = triangles; beamMesh.RecalculateBounds();
            SampleNow();
        }
        void CollectEdges()
        {
            edgeCount = 0; ShadowEdgeOverflow = false;
            foreach (var source in casters)
            {
                if (!source) continue;
                int count = source.DirectionalEdgeCount;
                if (count > MaxShadowEdges - edgeCount) { ShadowEdgeOverflow = true; break; }
                int owner = OwnerId(source.target ? (UnityEngine.Object)source.target : source);
                for (int i = 0; i < count; i++) if (source.TryGetDirectionalEdge(i, out var a, out var b))
                {
                    a = transform.InverseTransformPoint(a); b = transform.InverseTransformPoint(b);
                    edges[edgeCount] = new Vector4(a.x, a.y, b.x, b.y); edgeOwners[edgeCount] = new Vector4(owner, 0, 0, 0); edgeCount++;
                }
            }
            if (ShadowEdgeOverflow && !warnedOverflow) Debug.LogWarning("Directional window edge budget exceeded; disabling this window instead of dropping shadow casters.", this);
            warnedOverflow = ShadowEdgeOverflow;
        }
        internal void Write(WindowLightBuffer buffer, int slot, SpriteRenderer receiver)
        {
            if (!bound) return;
            bool active = emissionEnabled && emitter && emitter.Output.enabled && isActiveAndEnabled && FootprintVertexCount >= 3 && !ShadowEdgeOverflow;
            var d = WindowLightGeometry.Direction(angleDegrees);
            Vector3 worldDirection = transform.TransformVector(new Vector3(d.X, d.Y)).normalized;
            var toLight = new Vector3(-worldDirection.x, -worldDirection.y, -emitter.Definition.NormalDistance).normalized;
            var color = emitter.Output.color; color.r *= emitter.ComputedIntensity; color.g *= emitter.ComputedIntensity; color.b *= emitter.ComputedIntensity;
            buffer.WorldToLocal[slot] = transform.worldToLocalMatrix;
            Array.Copy(planes, 0, buffer.Planes, slot * 16, planes.Length);
            Array.Copy(edges, 0, buffer.Edges, slot * MaxShadowEdges, edges.Length);
            Array.Copy(edgeOwners, 0, buffer.EdgeOwners, slot * MaxShadowEdges, edgeOwners.Length);
            Array.Copy(openingPlanes, 0, buffer.OpeningPlanes, slot * WindowAperture.MaxVertices, openingPlanes.Length);
            Array.Copy(openingPieces, 0, buffer.Pieces, slot * WindowAperture.MaxPieces, openingPieces.Length);
            buffer.Direction[slot] = new Vector4(d.X, d.Y, 0, 0); buffer.ToLight[slot] = toLight;
            color.a = definition.BeamOpacity; buffer.Color[slot] = color;
            buffer.Shadow[slot] = new Vector4(emitter.Definition.ShadowStrength, emitter.Definition.ShadowSoftness * definition.EdgeFeather, receiver ? OwnerId(receiver) : 0, 0);
            buffer.Data[slot] = new Vector4(active ? 1 : 0, definition.EdgeFeather, definition.Length, definition.EndFade);
            buffer.Counts[slot] = new Vector4(FootprintVertexCount, edgeCount, openings.PieceCount, 0);
        }
        public void SampleNow()
        {
            if (!bound) return;
            if (angleDegrees != builtAngle) { WindowLightDefinition.ValidateAngle(angleDegrees); Rebuild(); return; }
            CollectEdges();
            bool active = emissionEnabled && emitter && emitter.Output.enabled && isActiveAndEnabled && FootprintVertexCount >= 3 && !ShadowEdgeOverflow;
            RefreshReceivers();
            if (beamRenderer)
            {
                beamRenderer.enabled = showShaft && active;
                beamRenderer.GetPropertyBlock(shaftBlock); shaftData.Clear(); Write(shaftData, 0, null); shaftData.Apply(shaftBlock, 1); beamRenderer.SetPropertyBlock(shaftBlock);
            }
        }
        void LateUpdate() => SampleNow();
        void OnEnable() { if (bound) SetEnabled(emissionEnabled); }
        void ClearReceivers()
            => RefreshReceivers();
        void RefreshReceivers()
        {
            foreach (var receiver in receivers) if (receiver) receiver.SampleNow();
        }
        void OnDisable() { if (emitter) emitter.Output.enabled = false; if (beamRenderer) beamRenderer.enabled = false; ClearReceivers(); }
        void OnDestroy() { foreach (var receiver in receivers) if (receiver) receiver.Unregister(this); receivers.Clear(); if (beamRenderer) Destroy(beamRenderer.gameObject); if (beamMesh) Destroy(beamMesh); if (beamMaterial) Destroy(beamMaterial); }
    }
}
