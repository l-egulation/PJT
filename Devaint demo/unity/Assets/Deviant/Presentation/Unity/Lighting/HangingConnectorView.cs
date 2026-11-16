using System;
using Deviant.Definitions;
using UnityEngine;
using UnityEngine.Rendering;

namespace Deviant.Presentation.Unity
{
    // Socket-to-socket presentation, independent of the lamp's authoritative physics.
    // Meshes and renderers are allocated once; style changes only select/reposition them.
    [DefaultExecutionOrder(950)]
    public sealed class HangingConnectorView : MonoBehaviour
    {
        sealed class Piece
        {
            internal Transform Root;
            internal MeshFilter Filter;
            internal MeshRenderer Mesh;
            internal SpriteRenderer Sprite;
        }
        [SerializeField] Transform startSocket, endSocket;
        [SerializeField] HangingConnectorStyle style;
        HangingConnectorDefinition definition;
        HangingConnectorCurve curve;
        HangingConnectorArt art;
        Piece[] pieces;
        Vector3[] points, ropeVertices;
        float[] distances;
        Mesh faceMesh, edgeMesh, ropeMesh;
        MeshRenderer ropeRenderer;
        float arcLength;
        bool bound;
        public HangingConnectorStyle Style => style;
        public Transform StartSocket => startSocket;
        public Transform EndSocket => endSocket;
        public float Bend => curve?.Bend ?? 0;
        public int CurvePointCount => points?.Length ?? 0;
        public int AllocatedPieceCount => pieces?.Length ?? 0;
        public bool LinkCountClamped { get; private set; }
        public void Bind(Transform start, Transform end, HangingConnectorDefinition settings, Material diagnosticMaterial,
            int sortingLayerId, int sortingOrder, HangingConnectorArt spriteArt = null)
        {
            if (bound) throw new InvalidOperationException("The connector already has an owner.");
            if (!start || !end || settings == null || !diagnosticMaterial) throw new ArgumentException("Connector sockets, settings and a diagnostic material are required.");
            if (spriteArt) spriteArt.Validate();
            startSocket = start; endSocket = end; definition = settings; art = spriteArt; style = settings.Style;
            curve = new HangingConnectorCurve(settings);
            points = new Vector3[settings.Segments + 1]; distances = new float[points.Length]; ropeVertices = new Vector3[points.Length * 2];
            double requested = Math.Ceiling(Vector3.Distance(start.position, end.position) / settings.LinkPitch);
            LinkCountClamped = requested > 128;
            int count = (int)Math.Max(1, Math.Min(128, requested));
            faceMesh = Ring(false); edgeMesh = Ring(true);
            ropeMesh = new Mesh { name = "Diagnostic continuous rope" }; ropeMesh.MarkDynamic();
            var colors = new Color[ropeVertices.Length]; var triangles = new int[settings.Segments * 6];
            for (int i = 0; i < points.Length; i++)
            {
                colors[i * 2] = i % 3 == 0 ? new Color(.53f, .38f, .20f) : new Color(.70f, .53f, .30f);
                colors[i * 2 + 1] = new Color(.39f, .27f, .13f);
                if (i == settings.Segments) continue;
                int vertex = i * 2, index = i * 6;
                triangles[index] = vertex; triangles[index + 1] = vertex + 2; triangles[index + 2] = vertex + 1;
                triangles[index + 3] = vertex + 1; triangles[index + 4] = vertex + 2; triangles[index + 5] = vertex + 3;
            }
            ropeMesh.vertices = ropeVertices; ropeMesh.colors = colors; ropeMesh.triangles = triangles;
            var rope = new GameObject("Continuous rope"); rope.transform.SetParent(transform, false);
            rope.AddComponent<MeshFilter>().sharedMesh = ropeMesh; ropeRenderer = rope.AddComponent<MeshRenderer>();
            Setup(ropeRenderer, diagnosticMaterial, sortingLayerId, sortingOrder);
            pieces = new Piece[count];
            for (int i = 0; i < count; i++)
            {
                var root = new GameObject("Connector piece " + i).transform; root.SetParent(transform, false);
                var mesh = new GameObject("Diagnostic link"); mesh.transform.SetParent(root, false);
                var sprite = new GameObject("Art piece"); sprite.transform.SetParent(root, false);
                var piece = new Piece { Root = root, Filter = mesh.AddComponent<MeshFilter>(), Mesh = mesh.AddComponent<MeshRenderer>(), Sprite = sprite.AddComponent<SpriteRenderer>() };
                Setup(piece.Mesh, diagnosticMaterial, sortingLayerId, sortingOrder);
                piece.Sprite.sortingLayerID = sortingLayerId; piece.Sprite.sortingOrder = sortingOrder;
                pieces[i] = piece;
            }
            bound = true; SampleNow();
        }
        static void Setup(MeshRenderer renderer, Material material, int layer, int order)
        {
            renderer.sharedMaterial = material; renderer.sortingLayerID = layer; renderer.sortingOrder = order;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }
        public void SetStyle(HangingConnectorStyle value)
        {
            if (value != HangingConnectorStyle.Chain && value != HangingConnectorStyle.Rope) throw new ArgumentOutOfRangeException(nameof(value));
            style = value; SampleNow();
        }
        public void SetArt(HangingConnectorArt value)
        { if (value) value.Validate(); art = value; SampleNow(); }
        public void Advance(float delta, float angularSpeed) { if (!bound) return; curve.Advance(delta, angularSpeed); SampleNow(); }
        public void ResetVisual() { curve?.Reset(); SampleNow(); }
        public Vector3 GetCurveWorldPoint(int index)
        {
            if (points == null || index < 0 || index >= points.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return transform.TransformPoint(points[index]);
        }
        public SpriteRenderer GetPieceSprite(int index) => pieces[index].Sprite;
        public void SampleNow()
        {
            if (!bound || !isActiveAndEnabled || !startSocket || !endSocket) return;
            Vector3 start = transform.InverseTransformPoint(startSocket.position), end = transform.InverseTransformPoint(endSocket.position);
            var first = new ConnectorPoint(start.x, start.y); var last = new ConnectorPoint(end.x, end.y);
            for (int i = 0; i < points.Length; i++)
            {
                float t = (float)i / definition.Segments; var p = curve.Sample(first, last, t);
                points[i] = new Vector3(p.X, p.Y, Mathf.Lerp(start.z, end.z, t));
                distances[i] = i == 0 ? 0 : distances[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            }
            arcLength = distances[distances.Length - 1];
            if (arcLength < 1e-6f) { Hide(); return; }
            bool chain = style == HangingConnectorStyle.Chain;
            bool sprites = art && (chain ? art.HasChain : art.HasRope);
            ropeRenderer.enabled = !chain && !sprites;
            if (ropeRenderer.enabled) UpdateRope();
            for (int i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i]; piece.Mesh.enabled = chain && !sprites; piece.Sprite.enabled = sprites;
                if (!chain && !sprites) continue;
                float spacing = arcLength / pieces.Length, middle = (i + .5f) * spacing;
                float half = spacing * (chain ? definition.LinkOverlap : 1.04f) / 2;
                Vector3 top = AtDistance(Mathf.Max(0, middle - half)), bottom = AtDistance(Mathf.Min(arcLength, middle + half));
                if (sprites)
                {
                    var input = chain ? (i % 2 == 0 ? art.chainFace : art.chainEdge) : art.rope;
                    piece.Root.localPosition = Vector3.zero; piece.Root.localRotation = Quaternion.identity; piece.Root.localScale = Vector3.one;
                    var axis = input.bottomSocket - input.topSocket; var delta = bottom - top;
                    var rotation = Quaternion.FromToRotation(new Vector3(axis.x, axis.y), delta);
                    float scale = delta.magnitude / axis.magnitude;
                    var output = piece.Sprite.transform;
                    output.localRotation = rotation; output.localScale = Vector3.one * scale;
                    output.localPosition = top - rotation * ((Vector3)input.topSocket * scale);
                    piece.Sprite.sprite = input.sprite; piece.Sprite.sharedMaterial = art.spriteMaterial;
                }
                else
                {
                    piece.Root.localPosition = (top + bottom) / 2;
                    piece.Root.localRotation = Quaternion.FromToRotation(Vector3.up, top - bottom);
                    piece.Root.localScale = new Vector3(definition.ChainWidth * (i % 2 == 0 ? 1 : .42f), Vector3.Distance(top, bottom), 1);
                    piece.Filter.sharedMesh = i % 2 == 0 ? faceMesh : edgeMesh;
                }
            }
        }
        Vector3 AtDistance(float distance)
        {
            int index = 1; while (index < distances.Length - 1 && distances[index] < distance) index++;
            float span = distances[index] - distances[index - 1];
            return Vector3.Lerp(points[index - 1], points[index], span > 1e-7f ? (distance - distances[index - 1]) / span : 0);
        }
        void UpdateRope()
        {
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 delta = points[Mathf.Min(points.Length - 1, i + 1)] - points[Mathf.Max(0, i - 1)];
                var normal = new Vector3(-delta.y, delta.x, 0).normalized * (definition.RopeWidth / 2);
                ropeVertices[i * 2] = points[i] + normal; ropeVertices[i * 2 + 1] = points[i] - normal;
            }
            ropeMesh.SetVertices(ropeVertices); ropeMesh.RecalculateBounds();
        }
        static Mesh Ring(bool edge)
        {
            const int sides = 12; var vertices = new Vector3[sides * 2]; var colors = new Color[vertices.Length]; var triangles = new int[sides * 6];
            for (int i = 0; i < sides; i++)
            {
                float angle = (float)i / sides * Mathf.PI * 2;
                vertices[i * 2] = new Vector3(Mathf.Cos(angle) * .5f, Mathf.Sin(angle) * .5f);
                vertices[i * 2 + 1] = new Vector3(Mathf.Cos(angle) * .24f, Mathf.Sin(angle) * .37f);
                colors[i * 2] = edge ? new Color(.45f, .43f, .37f) : new Color(.66f, .64f, .56f);
                colors[i * 2 + 1] = new Color(.27f, .26f, .23f);
                int next = (i + 1) % sides, t = i * 6;
                triangles[t] = i * 2; triangles[t + 1] = next * 2; triangles[t + 2] = i * 2 + 1;
                triangles[t + 3] = i * 2 + 1; triangles[t + 4] = next * 2; triangles[t + 5] = next * 2 + 1;
            }
            return new Mesh { name = edge ? "Diagnostic edge link" : "Diagnostic face link", vertices = vertices, colors = colors, triangles = triangles };
        }
        void Hide() { if (ropeRenderer) ropeRenderer.enabled = false; if (pieces != null) foreach (var piece in pieces) { piece.Mesh.enabled = false; piece.Sprite.enabled = false; } }
        void LateUpdate() => SampleNow();
        void OnEnable() => SampleNow();
        void OnDisable() => Hide();
        void OnDestroy()
        {
            if (pieces != null) foreach (var piece in pieces) if (piece.Root) Destroy(piece.Root.gameObject);
            if (ropeRenderer) Destroy(ropeRenderer.gameObject);
            if (faceMesh) Destroy(faceMesh); if (edgeMesh) Destroy(edgeMesh); if (ropeMesh) Destroy(ropeMesh);
        }
    }
}
