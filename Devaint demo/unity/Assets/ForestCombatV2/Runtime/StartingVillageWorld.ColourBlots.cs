using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ForestCombatV2
{
    // The colour that comes back with the village palette pieces (docs/design/progression/village-palette-pieces.md): blots fixed
    // in the world (data/assets/starting_hub.json layoutProposal.colourBlots, the HTML prototype's) where every sketched
    // piece of the village shows its colour twin, with a soft 40 px paper-grain edge (Resources/VillageBlotColour.shader).
    // Piece 1 colours around the well and a few spots, 2-4 more and wider, 5 the whole village. The newest piece's blots
    // spread over 2.4 s when a visit blooms them; earlier ones are there from the start. The erasure, the previews and the
    // opening's all-colour/all-sketch states hide the blots.
    public sealed partial class StartingVillageWorld
    {
        [System.Serializable] public sealed class Blot { public int piece; public float x, y, r; }

        const float BloomSeconds = 2.4f;
        // The shipped five-ink layout has 65 blots. Match the shader buffer so the final full-village blot is retained.
        const int MaxBlots = 128;
        Blot[] blots = new Blot[0];
        Material blotMaterial;
        readonly Vector4[] blotData = new Vector4[MaxBlots];
        int colourPieces;
        int bloomPiece; float bloomStarted = -1;
        static readonly int BlotsId = Shader.PropertyToID("_VillageBlots"), BlotCountId = Shader.PropertyToID("_VillageBlotCount"), GrainId = Shader.PropertyToID("_VillageBlotGrain");
        static Texture2D grain;

        // Pieces whose colour is shown in the village (the profile's pieces, or one fewer while a visit waits to bloom it).
        public int ColourPieces
        {
            get => colourPieces;
            // The same level again leaves a bloom in progress alone (the GUI sets the profile's level every frame).
            set { if (value == colourPieces) return; colourPieces = Mathf.Max(0, value); bloomStarted = -1; Apply(); SendBlots(); }
        }
        public bool Blooming => bloomStarted >= 0 && SceneTime - bloomStarted < BloomSeconds;

        // The piece's blots spread from nothing; the pieces before it stay as they are.
        public void BloomPiece(int piece)
        {
            colourPieces = Mathf.Max(colourPieces, piece); bloomPiece = piece; bloomStarted = SceneTime;
            Apply(); SendBlots();
        }

        bool BlotsShown => colourPieces > 0 && !Preview.HasValue && float.IsNaN(erasureLeft);

        void BuildBlots(Blot[] layoutBlots)
        {
            blots = layoutBlots ?? new Blot[0];
            var shader = Resources.Load<Shader>("VillageBlotColour");
            if (!shader) { Debug.LogWarning("VillageBlotColour shader missing: the colour blots are not drawn."); return; }
            blotMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            Shader.SetGlobalTexture(GrainId, Grain());
            // A colour twin over every piece drawn so far, shown where the piece is sketched and a blot covers it. A sorting
            // group keeps the pair at the piece's own place among the others, with the twin drawn over it. The rows of a
            // ground column overlap, so a column is one group: all its sketch rows first, then all its colour rows.
            var columns = new Dictionary<int, Transform>();
            foreach (var piece in paired)
            {
                if (!piece.renderer) continue;
                int inside = piece.renderer.sortingOrder;
                if (piece.colorSprite)
                {
                    if (!columns.TryGetValue(piece.column, out var column))
                    {
                        column = new GameObject("Ground column " + piece.column).transform; column.SetParent(transform, false);
                        var columnGroup = column.gameObject.AddComponent<SortingGroup>();
                        columnGroup.sortingLayerID = piece.renderer.sortingLayerID; columnGroup.sortingOrder = piece.renderer.sortingOrder;
                        columns[piece.column] = column;
                    }
                    piece.renderer.transform.SetParent(column, true);
                    piece.renderer.sortingOrder = inside = 0;
                }
                else
                {
                    var group = piece.renderer.gameObject.AddComponent<SortingGroup>();
                    group.sortingLayerID = piece.renderer.sortingLayerID; group.sortingOrder = piece.renderer.sortingOrder;
                }
                var twin = new GameObject("Colour blot").AddComponent<SpriteRenderer>();
                twin.transform.SetParent(piece.renderer.transform, false);
                twin.transform.localPosition = new Vector3(0, 0, -.01f);
                twin.sharedMaterial = blotMaterial; twin.sortingLayerID = piece.renderer.sortingLayerID; twin.sortingOrder = inside + 1;
                twin.enabled = false; piece.blotTwin = twin;
            }
            SendBlots();
        }

        // Called from ApplyPiece: the twin shows the piece's colour picture where the piece itself is sketched.
        void ApplyBlotTwin(Piece piece, bool sketch)
        {
            // While the opening's front is out the twin draws the erasure instead (StartingVillageWorld.EraseColour).
            if (sketch && Erasing && ShowEraseTwin(piece)) return;
            RestorePieceMaterial(piece);
            var twin = piece.blotTwin;
            if (!twin) return;
            if (blotMaterial && twin.sharedMaterial != blotMaterial) twin.sharedMaterial = blotMaterial;
            bool show = sketch && BlotsShown && piece.renderer.enabled;
            twin.enabled = show;
            if (!show) return;
            if (piece.colorSprite)
            {
                // Body columns stack their colour pattern with its own row height: the twin sits where that row would be.
                twin.sprite = piece.colorSprite;
                float colourY = piece.keepTransform ? piece.renderer.transform.position.y : GroundTop + piece.top - piece.row * piece.rowHeights.y / 32f;
                twin.transform.localPosition = new Vector3(0, colourY - piece.renderer.transform.position.y, -.01f);
            }
            else
            {
                twin.sprite = Load(piece.color, piece.pivot);
                twin.transform.localPosition = new Vector3(0, 0, -.01f);
            }
            twin.flipX = piece.renderer.flipX; twin.color = piece.renderer.color;
        }

        void SendBlots()
        {
            int count = 0;
            float grow = 1;
            if (bloomStarted >= 0) { float t = Mathf.Clamp01((SceneTime - bloomStarted) / BloomSeconds); grow = 1 - (1 - t) * (1 - t); if (t >= 1) bloomStarted = -1; }
            if (BlotsShown)
                foreach (var blot in blots)
                {
                    if (blot.piece > colourPieces || count >= MaxBlots) continue;
                    float r = blot.r * (blot.piece == bloomPiece && bloomStarted >= 0 ? grow : 1);
                    blotData[count++] = new Vector4(blot.x, GroundTop + blot.y, r, 0);
                }
            Shader.SetGlobalVectorArray(BlotsId, blotData);
            Shader.SetGlobalFloat(BlotCountId, count);
        }

        void TickBlots() { if (bloomStarted >= 0) SendBlots(); }

        // Whether a blot colours the village at x (3 units above the ground, as the prototype checks for bubbles).
        bool BlotColoured(float x)
        {
            if (!BlotsShown) return false;
            foreach (var blot in blots)
                if (blot.piece <= colourPieces && Vector2.Distance(new Vector2(x, 3), new Vector2(blot.x, blot.y)) < blot.r) return true;
            return false;
        }

        // The prototype's paper grain: seeded noise blurred once into soft lumps, scaled to the 0.55 it adds to an edge.
        static Texture2D Grain()
        {
            if (grain) return grain;
            long seed = 5; double Next() { seed = seed * 16807 % 2147483647; return seed / 2147483647.0; }
            var raw = new float[32 * 32];
            for (int i = 0; i < raw.Length; i++) raw[i] = (float)(Next() - .5);
            var pixels = new Color32[32 * 32];
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float sum = 0;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) sum += raw[((y + dy) & 31) * 32 + ((x + dx) & 31)];
                    float value = sum / 9 * 2.2f * .55f;
                    byte v = (byte)Mathf.Clamp(Mathf.RoundToInt((value + .5f) * 255), 0, 255);
                    pixels[y * 32 + x] = new Color32(v, v, v, 255);
                }
            grain = new Texture2D(32, 32, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.DontSave };
            grain.SetPixels32(pixels); grain.Apply(false, true);
            return grain;
        }
    }
}
