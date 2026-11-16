using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ForestCombatV2
{
    // The opening's erasure drawn per pixel (HTML prototype 2026-11-04/05): while a front is out every piece draws its
    // sketch and its colour twin over it with Resources/VillageEraseColour.shader, which takes the colour away in small
    // world cells scattered around the front. Every piece (sky, far rows, each building and prop, the bystanders, the
    // ground) has its own lead that sways at its own pace, so the village does not turn as one screen. Dust leaves the
    // freshly erased cells and drifts on in the direction the front travels.
    public sealed partial class StartingVillageWorld
    {
        public const float EraseBand = 2.2f;
        // Dust: of the cells that turn (8 per unit across, a column of the screen high), this share lets go a speck
        // (HTML 2026-11-04 「파티클 너무 많다」: .3 → .06).
        const float DustChance = .06f;
        const int FrontFlakeLimit = 420;
        float lastLeft = float.NaN, lastRight = float.NaN;
        static readonly int EraseId = Shader.PropertyToID("_VillageErase"), EraseGroundId = Shader.PropertyToID("_VillageEraseGround"),
            EraseOffsetId = Shader.PropertyToID("_EraseOffset"), EraseInvertId = Shader.PropertyToID("_EraseInvert");
        Material eraseMaterial;
        MaterialPropertyBlock eraseBlock;
        float frontFlakeDebt;
        readonly List<Flake> frontFlakes = new List<Flake>();
        // Village tones the flakes take (the textures are not CPU-readable): roofs, foliage, walls up high; stone and soil low.
        static readonly Color[] DustTones = { new Color32(0xb9, 0xcf, 0xe0, 255), new Color32(0xc9, 0x7a, 0x5a, 255), new Color32(0xef, 0xe4, 0xcf, 255), new Color32(0x8f, 0xa3, 0x6a, 255), new Color32(0x6a, 0x4a, 0x34, 255) };

        sealed class Flake { public SpriteRenderer renderer; public Vector2 velocity; public float born, life, wobble; }
        static Sprite flakeSprite;
        // One art pixel at 32 PPU; scaled up per flake.
        static Sprite FlakeSprite()
        {
            if (flakeSprite) return flakeSprite;
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
            texture.SetPixel(0, 0, Color.white); texture.Apply(false, true);
            flakeSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 32, 0, SpriteMeshType.FullRect);
            flakeSprite.hideFlags = HideFlags.DontSave;
            return flakeSprite;
        }

        bool Erasing => !float.IsNaN(erasureLeft) && !ErasureMet;
        public int FrontFlakesAlive => frontFlakes.Count;

        Material EraseMaterial()
        {
            if (eraseMaterial) return eraseMaterial;
            var shader = Resources.Load<Shader>("VillageEraseColour");
            if (!shader) { Debug.LogWarning("VillageEraseColour shader missing: the erasure falls back to whole pieces."); return null; }
            return eraseMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
        }

        // The colour twin of a piece during the erasure; false when there is none to draw (the piece then stays sketch).
        bool ShowEraseTwin(Piece piece)
        {
            var material = EraseMaterial();
            if (!material || !piece.renderer || !piece.renderer.enabled) return false;
            var twin = piece.blotTwin ? piece.blotTwin : piece.blotTwin = MakeTwin(piece.renderer);
            twin.sharedMaterial = material;
            if (piece.colorSprite)
            {
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
            float lead = Lead(Seed(piece));
            SetEraseOffset(twin, lead);
            twin.enabled = true;
            // The sketch below shows only in the cells the colour has left, with the same lead: the two pictures trade
            // places cell by cell instead of the whole sketch sitting behind the colour from the first frame.
            if (!piece.plainMaterial) piece.plainMaterial = piece.renderer.sharedMaterial;
            piece.renderer.sharedMaterial = material;
            SetEraseOffset(piece.renderer, lead, true);
            return true;
        }

        // Puts a piece's own material back once its erasure twin is no longer drawn.
        void RestorePieceMaterial(Piece piece)
        {
            if (!piece.plainMaterial || !piece.renderer) return;
            piece.renderer.sharedMaterial = piece.plainMaterial; piece.renderer.SetPropertyBlock(null);
            piece.plainMaterial = null;
        }

        // Pieces added after the blots were built (the opening road) get their twin here, drawn over them in their place.
        SpriteRenderer MakeTwin(SpriteRenderer owner)
        {
            var twin = new GameObject("Colour blot").AddComponent<SpriteRenderer>();
            twin.transform.SetParent(owner.transform, false);
            twin.transform.localPosition = new Vector3(0, 0, -.01f);
            twin.sortingLayerID = owner.sortingLayerID; twin.sortingOrder = owner.sortingOrder;
            twin.enabled = false;
            return twin;
        }

        void SetEraseOffset(SpriteRenderer twin, float offset, bool invert = false)
        {
            eraseBlock ??= new MaterialPropertyBlock();
            twin.GetPropertyBlock(eraseBlock); eraseBlock.SetFloat(EraseOffsetId, offset); eraseBlock.SetFloat(EraseInvertId, invert ? 1 : 0); twin.SetPropertyBlock(eraseBlock);
        }

        // The bystanders: sketch frames below, the colour frame over them through the same erasure.
        readonly Dictionary<Resident, SpriteRenderer> residentTwins = new Dictionary<Resident, SpriteRenderer>();
        bool ResidentErase(Resident resident, int frame)
        {
            var material = EraseMaterial();
            if (!material || !Erasing) { if (residentTwins.TryGetValue(resident, out var off) && off) off.enabled = false; return false; }
            if (!residentTwins.TryGetValue(resident, out var twin) || !twin)
            {
                twin = MakeTwin(resident.renderer); twin.sharedMaterial = material; residentTwins[resident] = twin;
            }
            SetEraseOffset(twin, Lead(resident.id));
            twin.sprite = resident.color[frame]; twin.flipX = resident.renderer.flipX; twin.enabled = true;
            resident.renderer.sprite = resident.sketch[frame];
            return true;
        }

        void ClearResidentTwins()
        {
            foreach (var twin in residentTwins.Values) if (twin) Destroy(twin.gameObject);
            residentTwins.Clear();
        }

        // Each piece's own front (HTML pieceLead): units ahead of the front (positive: erased early) from -230 to +70 px,
        // swaying by 40 to 120 px at its own pace.
        static float Unit(string seed, string salt)
        {
            uint h = 2166136261;
            foreach (char c in seed + salt) h = (h ^ c) * 16777619;
            return (h % 100000) / 100000f;
        }
        float Lead(string seed)
        {
            float r1 = Unit(seed, "#a"), r2 = Unit(seed, "#b"), r3 = Unit(seed, "#c"), r4 = Unit(seed, "#d");
            return (-230 + 300 * r1 + (40 + 80 * r2) * Mathf.Sin(Time.time * (.25f + .9f * r3) + r4 * 6.283f)) / 32f;
        }
        // Fixed per piece for its life (the far layers move with the camera, so not by position). Pieces cut from one
        // picture share a lead (playtest 2026-11-06): the repeated copies of a backdrop row, which split into sketch and
        // colour at their seams, and the tiles of the ground, its cap stones and the platforms, which turned square by
        // square. The cells are fixed to the world, so with one lead the ground drops its cells as one piece.
        static string Seed(Piece piece) => !piece.renderer ? "?" : IsTile(piece) ? TileSeed
            : piece.renderer.name.StartsWith("Hub background ", System.StringComparison.Ordinal) || piece.renderer.name == "Hub sky" ? piece.renderer.name
            : piece.renderer.name + "#" + piece.renderer.GetEntityId();
        const string TileSeed = "village tiles";
        static bool IsTile(Piece piece) => piece.renderer && (piece.renderer.name == "Ground" || piece.renderer.name == "Ground body" || piece.renderer.name == "Opening course platform");

        // Every frame of the opening: the globals the shader reads, each twin's lead, then the dust at the fronts.
        void TickEraseColour()
        {
            float right = float.IsNaN(erasureRight) ? 1e6f : erasureRight;
            Shader.SetGlobalVector(EraseId, Erasing ? new Vector4(erasureLeft, right, EraseBand, Time.time) : new Vector4(-1e6f, 1e6f, EraseBand, Time.time));
            Shader.SetGlobalFloat(EraseGroundId, GroundTop);
            if (Erasing)
                foreach (var piece in paired)
                    if (piece.blotTwin && piece.blotTwin.enabled) SetEraseOffset(piece.blotTwin, Lead(Seed(piece)));
            float dt = Time.deltaTime;
            bool running = Erasing && !World.arena.Controls.Current.SimulationPaused;
            if (running && EraseMaterial() && !float.IsNaN(lastLeft))
            {
                var view = World.View;
                float bottom = view.transform.position.y - view.orthographicSize, top = view.transform.position.y + view.orthographicSize;
                float rows = (top - bottom) * 8;
                var random = new System.Random(Mathf.FloorToInt(Time.time * 977));
                void Spawn(float front, float moved, int dir)
                {
                    frontFlakeDebt += Mathf.Max(0, moved) * 8 * rows * DustChance;
                    while (frontFlakeDebt >= 1 && frontFlakes.Count < FrontFlakeLimit)
                    {
                        frontFlakeDebt -= 1;
                        float y = Mathf.Lerp(bottom, top, (float)random.NextDouble()), height = (top - y) / (top - bottom);
                        // Where cells turn: the front, each piece's lead and each cell's own scatter.
                        float x = front + dir * (-160 + (float)random.NextDouble() * 220) / 32f;
                        int tone = height < .35f ? (random.NextDouble() < .7 ? 0 : 1) : height < .8f ? 1 + random.Next(3) : 4;
                        var r = Renderer("Erase flake", FlakeSprite(), 4);
                        r.transform.position = new Vector3(Mathf.Round(x * 32) / 32, Mathf.Round(y * 32) / 32, -.02f);
                        float size = random.NextDouble() < .6 ? 2 : 3; r.transform.localScale = new Vector3(size, size, 1); r.color = DustTones[tone];
                        frontFlakes.Add(new Flake { renderer = r, born = Time.time, life = .6f + (float)random.NextDouble() * .9f, wobble = (float)random.NextDouble() * 6,
                            velocity = new Vector2(dir * (50 + (float)random.NextDouble() * 100) / 32f, (14 - (float)random.NextDouble() * 22) / 32f) });
                    }
                }
                Spawn(erasureLeft, erasureLeft - lastLeft, 1);
                if (!float.IsNaN(erasureRight) && !float.IsNaN(lastRight)) Spawn(erasureRight, lastRight - erasureRight, -1);
                if (frontFlakes.Count >= FrontFlakeLimit) frontFlakeDebt = 0;
            }
            lastLeft = Erasing ? erasureLeft : float.NaN; lastRight = Erasing ? erasureRight : float.NaN;
            float now = Time.time;
            for (int i = frontFlakes.Count - 1; i >= 0; i--)
            {
                var f = frontFlakes[i];
                float age = now - f.born;
                if (!f.renderer || age >= f.life) { if (f.renderer) Destroy(f.renderer.gameObject); frontFlakes.RemoveAt(i); continue; }
                // Drifts on and wobbles as it goes (the prototype: vy += sin(age 9 + wobble) 40 px/s^2, vx 0.5% slower a frame).
                f.velocity = new Vector2(f.velocity.x * Mathf.Pow(.995f, dt * 60), f.velocity.y - Mathf.Sin(age * 9 + f.wobble) * 40 / 32f * dt);
                var p = f.renderer.transform.position; f.renderer.transform.position = new Vector3(p.x + f.velocity.x * dt, p.y + f.velocity.y * dt, p.z);
                var col = f.renderer.color; col.a = Mathf.Max(0, 1 - age / f.life); f.renderer.color = col;
            }
        }

        void ClearFrontFlakes()
        {
            foreach (var f in frontFlakes) if (f.renderer) Destroy(f.renderer.gameObject);
            frontFlakes.Clear(); frontFlakeDebt = 0; lastLeft = lastRight = float.NaN;
        }
    }
}
