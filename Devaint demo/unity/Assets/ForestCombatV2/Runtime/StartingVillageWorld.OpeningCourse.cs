using System.Collections.Generic;
using Deviant.Combat;
using UnityEngine;

namespace ForestCombatV2
{
    // The road outside the village where the playable opening teaches moving (docs/design/story/opening/opening-playable.md part
    // 1). It exists only during the opening, left of the map (negative x): the same ground continues, low walls and the
    // handcart stand on it, stone platforms float above it, ground walls (climbable, drawn with the ground's own body and
    // cap stones) rise from it and pits cut it, and two bystanders stand by it. Falling into a pit of the learning road
    // puts the hero back just before that pit; once the erasure chases, a fall counts as caught. The village's own left
    // limit is lifted meanwhile and restored by RemoveOpeningCourse, so later visits (and everything after the opening)
    // end at the village's left edge.
    public sealed partial class StartingVillageWorld
    {
        public struct CoursePiece { public string kind, id, art, name; public float x, top, width, bottom; public int face, phase; }

        const string PlatformPath = "Village/color/platforms/platform_c0", PlatformSketchPath = "Village/reference-sketch/color/platforms/platform_c0";
        readonly List<GameObject> courseObjects = new List<GameObject>();
        readonly List<SpriteRenderer> courseArt = new List<SpriteRenderer>();
        Collider2D villageLeftLimit;
        public bool CourseBuilt => courseObjects.Count > 0;

        readonly List<Vector2> coursePits = new List<Vector2>();   // (left x, right x)
        public const float PitRespawnBefore = 2, PitFallDepth = 3;
        float courseLeftX = float.NegativeInfinity;
        // Centres of the walls and platforms: where the erasure bites.
        readonly List<float> courseBlocks = new List<float>();
        // Village ground art over a pit right of x -12 (the village's own ground run), hidden while the road is built.
        readonly List<SpriteRenderer> hiddenGround = new List<SpriteRenderer>();

        // A bystander by the road: idle frames, a paper-tone sketch of each made at build time (there is no sketch art).
        Material residentPlain;
        // clock: the bystander's own idle clock. It runs only while the bystander is in colour, so one the erasure has
        // swallowed stands still in the pose it had, as the birds do (user 2026-11-13).
        sealed class Resident { public string id, name; public SpriteRenderer renderer; public Material outline; public Sprite[] color, sketch; public int phase; public float x, lag, head, clock; }
        readonly List<Resident> residents = new List<Resident>();
        readonly List<Object> residentMade = new List<Object>();
        // The village NPC set (96x96, 32 frames at 16 per second, feet 8 px up) is drawn at the hero's pixel density and faces
        // left, so a resident facing right (face 1) is the flipped one. Bubbles sit a little above the art's own top.
        const float ResidentScale = 1, ResidentHeadGap = .14f, ResidentFps = 16;
        const bool ResidentArtFacesLeft = true;

        // Height of the highest opaque pixel above the sprite's pivot, in world units at ResidentScale.
        static float ArtTop(Sprite sprite)
        {
            var rect = sprite.rect; var pixels = sprite.texture.GetPixels32();
            int stride = sprite.texture.width, ox = (int)rect.x, oy = (int)rect.y;
            for (int y = (int)rect.height - 1; y >= 0; y--)
                for (int x = 0; x < (int)rect.width; x++)
                    if (pixels[(oy + y) * stride + ox + x].a >= 128) return (y + 1 - sprite.pivot.y) / sprite.pixelsPerUnit * ResidentScale;
            return 0;
        }

        public void BuildOpeningCourse(float leftX, IEnumerable<CoursePiece> pieces) => BuildOpeningCourse(leftX, float.NaN, pieces);

        // cameraLeftX (NaN: leftX): where the camera stops, so the hero can walk in from off screen left of it.
        public void BuildOpeningCourse(float leftX, float cameraLeftX, IEnumerable<CoursePiece> pieces)
        {
            RemoveOpeningCourse();
            OpeningMusicCue = "opening_road";
            int left = Mathf.FloorToInt(leftX); courseLeftX = leftX;
            var list = new List<CoursePiece>(pieces);
            foreach (var p in list) if (p.kind == "pit") coursePits.Add(new Vector2(p.x, p.x + Mathf.Max(1, p.width)));
            coursePits.Sort((a, b) => a.x.CompareTo(b.x));
            // Ground and floor in runs between the pits; the village ground starts at x -12 (its own run already covers it).
            int from = left - 12; bool openLeft = false;
            foreach (var pit in coursePits)
            {
                Floor(from, Mathf.RoundToInt(pit.x), left, openLeft, true);
                from = Mathf.RoundToInt(pit.y); openLeft = true;
            }
            Floor(from, 0, left, openLeft, false);
            // The village ground run starts at x -12: a pit there takes its art away too.
            foreach (var piece in paired)
            {
                if (!piece.renderer || !piece.renderer.enabled || (piece.renderer.name != "Ground" && piece.renderer.name != "Ground body")) continue;
                float x = piece.colorSprite ? piece.column + .5f : piece.renderer.bounds.center.x;
                foreach (var pit in coursePits) if (pit.y > -12 && x > pit.x && x < pit.y) { piece.renderer.enabled = false; hiddenGround.Add(piece.renderer); break; }
            }
            // After the hiding above, so the pit's own soil stays drawn.
            foreach (var pit in coursePits) PitShade(pit.x, pit.y);
            Solid("Opening course left limit", new Vector2(left - .5f, GroundTop + 40), new Vector2(1, 100), false);
            var limit = World.transform.Find("Left world limit");
            villageLeftLimit = limit ? limit.GetComponent<Collider2D>() : null;
            if (villageLeftLimit) villageLeftLimit.enabled = false;
            foreach (var piece in list)
            {
                if (piece.kind == "pit") continue;
                if (piece.kind == "dodgeGate") { BuildDodgeGate(piece.x, piece.width, piece.top); continue; }
                if (piece.kind == "platform") { int width = Mathf.Max(2, Mathf.RoundToInt(piece.width)); Platform(piece.x, piece.top, width); courseBlocks.Add(piece.x + width / 2f); }
                else if (piece.kind == "wall")
                {
                    float width = Mathf.Max(1, piece.width);
                    Wall(piece.x, width, piece.bottom, piece.top);
                    courseBlocks.Add(piece.x + width / 2f);
                }
                else if (piece.kind == "fence" || piece.kind == "crate")
                {
                    WoodProp(piece.kind, piece.x, piece.width, piece.top);
                    courseBlocks.Add(piece.x + piece.width / 2f);
                }
                else if (piece.kind == "resident") AddResident(piece);
                else if (piece.kind == "decoration" && piece.id == "bridge-pier")
                {
                    var pier = PlaceCourseProp("bridge-pier", new Vector2(piece.x, GroundTop), CourseBlockOrder - 1);
                    if (pier) pier.flipX = piece.face < 0;
                }
                else
                {
                    var art = Place(piece.id, "props", piece.x, -14, null);
                    if (!art) continue;
                    // Decoration only: the hero walks past the low wall and the handcart (playtest 2026-11-04: they blocked
                    // the road and could be stood on). The climbable walls and the shelf are the course's own pieces.
                    courseArt.Add(art);
                }
            }
            Physics2D.SyncTransforms();
            World.CameraMinPixels = (float.IsNaN(cameraLeftX) ? left : cameraLeftX) * 32f;
            Apply();
        }

        // Ground art from `from` to `to` (the part right of x -12 is the village's own run) and a solid floor from the
        // course's left end; nothing under a pit.
        void Floor(int from, int to, int left, bool openLeft, bool openRight)
        {
            if (to > from) courseArt.AddRange(GroundRunUpTo(from, Mathf.Min(to, -12), openLeft, openRight && to <= -12));
            float a = Mathf.Max(from, left - 1), b = to;
            if (b > a) Solid("Opening course floor", new Vector2((a + b) / 2, GroundTop - 2), new Vector2(b - a, 4), false);
        }
        List<SpriteRenderer> GroundRunUpTo(int from, int to, bool openLeft, bool openRight) => to > from ? GroundRun(from, to, openLeft, openRight) : new List<SpriteRenderer>();

        // The pit's inside: the far wall of the cut, the same soil (sketch or colour like the ground) from the walking line
        // down, dimmed, under a warm shade with a dark band under the rim that deepens downwards and towards both edges
        // (playtest 2026-11-04: the flat black fill read as a hole in the picture, and a gap above the soil showed the
        // backdrop as a pale band).
        const int PitSoilOrder = -7;
        static readonly Color PitSoilTint = new Color(.55f, .48f, .45f);
        void PitShade(float x0, float x1)
        {
            int before = paired.Count;
            GroundColumns(Mathf.RoundToInt(x0), Mathf.RoundToInt(x1), false, false, PitSoilOrder, interior: true);
            for (int i = before; i < paired.Count; i++)
            {
                var r = paired[i].renderer;
                r.sortingOrder = PitSoilOrder; r.color = PitSoilTint;
                courseArt.Add(r);
            }
            var shade = new GameObject("Opening course pit").AddComponent<SpriteRenderer>();
            shade.transform.SetParent(World.transform, false);
            shade.sprite = PitSprite(); shade.sortingOrder = PitSoilOrder + 1;
            shade.transform.position = new Vector3(x0, GroundTop, 0);
            shade.transform.localScale = new Vector3((x1 - x0) / 16f, GroundDepth / 8f, 1);   // the 16x8 px shade spans the pit
            courseObjects.Add(shade.gameObject);
        }
        static Sprite pitSprite;
        static Sprite PitSprite()
        {
            if (pitSprite) return pitSprite;
            var t = new Texture2D(16, 8) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var ink = new Color32(24, 18, 14, 255);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 16; x++)
                {
                    float depth = 1 - y / 7f, edge = 1 - Mathf.Min(x, 15 - x) / 4f, rim = y == 7 ? .4f : 0;
                    t.SetPixel(x, y, new Color(ink.r / 255f, ink.g / 255f, ink.b / 255f, Mathf.Clamp01(Mathf.Lerp(.2f, .8f, depth) + rim + .3f * Mathf.Max(0, edge))));
                }
            t.Apply();
            return pitSprite = Sprite.Create(t, new Rect(0, 0, 16, 8), new Vector2(0, 1), 1);
        }

        const int CourseBlockOrder = -2;
        // Both states are cut from the floor's own lawn tiles (PixelCourseWall); the buried foot covers the floor's grass,
        // so a wall grows out of the ground. Colour and sketch share the silhouette and the exact collision dimensions.
        void Wall(float x, float width, float bottom, float top)
        {
            int w = Mathf.RoundToInt(width * 32), h = Mathf.RoundToInt((top - bottom) * 32);
            PixelCourseWall(x, bottom, w, h);
            if (bottom > 0) Solid("Opening course wall", new Vector2(x + width / 2f, GroundTop + (bottom + top) / 2f), new Vector2(width, top - bottom), false);
            else Solid("Opening course wall", new Vector2(x + width / 2f, GroundTop + top / 2f - 2), new Vector2(width, top + 4), false);
        }

        SpriteRenderer PlaceCourseProp(string kind, Vector2 at, int order)
        {
            const string path = "Hub/opening/course-props/";
            string colour = path + kind, sketch = colour + "-sketch";
            var pivot = new Vector2(.5f, 0);
            if (!Load(colour, pivot) || !Load(sketch, pivot)) return null;
            var art = Textured("Opening course art " + kind, colour, sketch, pivot, order);
            art.transform.position = at;
            courseArt.Add(art);
            return art;
        }

        // The wooden obstacles retain the course's original collider and use the corresponding art pair.
        static readonly Color32[] WoodColour = { new Color32(0x8a, 0x5a, 0x33, 255), new Color32(0xb0, 0x7a, 0x47, 255), new Color32(0x3b, 0x26, 0x18, 255) };
        static readonly Color32[] WoodSketch = { new Color32(0xd8, 0xcc, 0xb6, 255), new Color32(0xef, 0xe5, 0xd2, 255), new Color32(0x6e, 0x64, 0x5a, 255) };
        void WoodProp(string kind, float x, float width, float top)
        {
            int w = Mathf.RoundToInt(width * 32), h = Mathf.RoundToInt(top * 32);
            bool fence = kind == "fence";
            Solid("Opening course " + kind, new Vector2(x + width / 2f, GroundTop + top / 2f - 1), new Vector2(width, top + 2), false);
            // The 2x2-dot pixel art (stage1-hub-pixel-props) carries its own one-dot ink outline and is drawn for these
            // obstacle sizes: it stands at native size, centred on the collider and snapped to the 2 px dot grid, with no
            // stretch and no contrast material (either would break or darken the dots).
            float centre = Mathf.Round((x + width / 2f) * 16f) / 16f;
            var art = PlaceCourseProp(kind, new Vector2(centre, GroundTop), CourseBlockOrder);
            if (art)
            {
                art.sprite.texture.filterMode = FilterMode.Point;
                return;
            }
            int tw = fence ? w + 28 : w, th = fence ? h + 6 : h;
            Color32[] Draw(Color32[] wood)
            {
                var px = new Color32[tw * th];
                void Fill(int rx, int ry, int rw, int rh, Color32 c)
                {
                    for (int yy = Mathf.Max(0, ry); yy < Mathf.Min(th, ry + rh); yy++)
                        for (int xx = Mathf.Max(0, rx); xx < Mathf.Min(tw, rx + rw); xx++) px[yy * tw + xx] = c;
                }
                if (fence)
                {
                    Fill(0, 14, w + 28, 8, wood[2]); Fill(0, 32, w + 28, 8, wood[2]);
                    Fill(2, 16, w + 24, 4, wood[1]); Fill(2, 34, w + 24, 4, wood[1]);
                    foreach (int dx in new[] { -14, w + 6 }) { Fill(14 + dx, 4, 8, h + 2, wood[2]); Fill(16 + dx, 6, 4, h, wood[0]); }
                    Fill(14, 0, w, h + 6, wood[2]); Fill(17, 3, w - 6, h + 3, wood[0]);
                }
                else
                {
                    Fill(0, 0, w, h, wood[2]); Fill(3, 3, w - 6, h - 6, wood[0]);
                    for (int k = 1; k < 4; k++) Fill(3, Mathf.RoundToInt(k * h / 4f), w - 6, 3, wood[1]);
                    // The diagonal brace, 3 px wide.
                    for (int s = 0; s <= 200; s++)
                    {
                        float f = s / 200f; int cx = Mathf.RoundToInt(Mathf.Lerp(4, w - 4, f)), cy = Mathf.RoundToInt(Mathf.Lerp(4, h - 4, f));
                        Fill(cx - 1, cy - 1, 3, 3, wood[2]);
                    }
                }
                return px;
            }
            var pivot = new Vector2(fence ? 14f / tw : 0, 0);
            AddCourseArt("Opening course " + kind, Draw(WoodColour), tw, th, pivot, Draw(WoodSketch), new Vector2(x, GroundTop), $"{kind} {w}x{h}");
        }

        // A course picture from top-down pixels, paired with its sketch (given, or the paper tone of the colour one) so the
        // village's sketch, preview and erasure treat it like any other piece. Same-size pictures share their sprites.
        void AddCourseArt(string name, Color32[] topDown, int w, int h, Vector2 pivot, Color32[] sketchTopDown, Vector2 at, string key)
        {
            string path = "Opening course/" + key;
            if (!wrapped.TryGetValue(path + "@" + pivot, out var colour))
                wrapped[path + "@" + pivot] = colour = CourseSprite(topDown, w, h, pivot);
            string sketchKey = path + " sketch@" + pivot;
            if (!wrapped.TryGetValue(sketchKey, out var sketch))
                wrapped[sketchKey] = sketch = sketchTopDown != null ? CourseSprite(sketchTopDown, w, h, pivot) : SketchOf(colour, residentMade);
            var r = Renderer(name, colour, CourseBlockOrder);
            r.transform.position = new Vector3(at.x, at.y, 0);
            paired.Add(new Piece { renderer = r, color = path, sketchSprite = sketch, pivot = pivot });
            courseArt.Add(r);
        }

        Sprite CourseSprite(Color32[] topDown, int w, int h, Vector2 pivot)
        {
            var flipped = new Color32[w * h];
            for (int y = 0; y < h; y++) System.Array.Copy(topDown, y * w, flipped, (h - 1 - y) * w, w);
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            texture.SetPixels32(flipped); texture.Apply(false, false);
            var sprite = Sprite.Create(texture, new Rect(0, 0, w, h), pivot, 32, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.DontSave;
            residentMade.Add(texture); residentMade.Add(sprite);
            return sprite;
        }

        // Called every frame of the opening: a hero fallen into a pit stands again just before it (the road is for learning).
        // During the chase a fall is a catch instead, and the chase starts again from its checkpoint.
        public bool CatchPitFall()
        {
            var player = World.arena.Player;
            if (coursePits.Count == 0 || player.Position.y > GroundTop - PitFallDepth || caught != null) return false;
            if (ErasureChasing) { Catch("pit"); return false; }
            float x = player.Position.x; var pit = coursePits[0];
            foreach (var p in coursePits) if (Mathf.Abs((p.x + p.y) / 2 - x) < Mathf.Abs((pit.x + pit.y) / 2 - x)) pit = p;
            player.Position = new Vector2(pit.x - PitRespawnBefore, GroundTop); player.velocity = Vector2.zero;
            Physics2D.SyncTransforms(); player.grounded = player.ProbeGround();
            World.RefreshCamera(0, true);
            return true;
        }

        void AddResident(CoursePiece piece)
        {
            var frames = new List<Sprite>();
            for (int i = 1; ; i++) { var frame = Resources.Load<Sprite>($"{piece.art}/F{i:00}"); if (!frame) break; frames.Add(frame); }
            if (frames.Count == 0) { Debug.LogWarning("Opening resident art missing: " + piece.art); return; }
            // In front of the props, behind the ground, whose cap stones cover the feet a little as on the village's own props.
            var r = Renderer("Opening resident " + piece.id, frames[0], -10);
            r.transform.position = new Vector3(piece.x, GroundTop, 0); r.transform.localScale = new Vector3(ResidentScale, ResidentScale, 1);
            r.flipX = ResidentArtFacesLeft ? piece.face > 0 : piece.face < 0;
            // The hero's 1 px ink outline (playtest 2026-11-06: the residents had none beside the hero). Each resident gets
            // its own copy: sharing one let both draw in a single batch with one resident's picture (both looked alike).
            residentPlain ??= r.sharedMaterial;
            var outlineSource = Resources.Load<Material>(ForestCombatWorld.PlayerOutlineMaterial);
            Material outline = null;
            if (outlineSource) { outline = new Material(outlineSource) { name = "Resident outline " + piece.id }; residentMade.Add(outline); r.sharedMaterial = outline; }
            courseObjects.Add(r.gameObject);
            residents.Add(new Resident { id = piece.id, name = piece.name, renderer = r, outline = outline, color = frames.ToArray(), sketch = frames.ConvertAll(frame => SketchOf(frame)).ToArray(),
                phase = piece.phase, x = piece.x, lag = chase != null ? Lag(piece.id, piece.x) : 0, head = ArtTop(frames[0]) + ResidentHeadGap });
        }

        // Paper tone of a colour frame (as the HTML prototype's sketchOf): the shading moves to paper greys, dark ink to a
        // light grey-brown, and only the outer outline gets a 1 px pencil line.
        Sprite SketchOf(Sprite source, List<Object> storage = null, bool scenery = false)
        {
            var rect = source.rect; int w = (int)rect.width, h = (int)rect.height;
            var src = source.texture.GetPixels32();
            int stride = source.texture.width, ox = (int)rect.x, oy = (int)rect.y;
            var dst = new Color32[w * h];
            bool Opaque(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && src[(oy + y) * stride + ox + x].a >= 128;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = src[(oy + y) * stride + ox + x];
                    if (c.a < 128) { dst[y * w + x] = new Color32(0, 0, 0, 0); continue; }
                    float l = Mathf.Pow((c.r * .3f + c.g * .59f + c.b * .11f) / 255f, .7f);
                    if (scenery)
                        dst[y * w + x] = new Color32((byte)(205 + 42 * l), (byte)(198 + 43 * l), (byte)(184 + 43 * l), c.a);
                    else
                    {
                        bool rim = !Opaque(x + 1, y) || !Opaque(x - 1, y) || !Opaque(x, y + 1) || !Opaque(x, y - 1);
                        dst[y * w + x] = rim ? new Color32(122, 114, 104, 255) :
                            new Color32((byte)(150 + 95 * l), (byte)(142 + 96 * l), (byte)(130 + 96 * l), 255);
                    }
                }
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            texture.SetPixels32(dst); texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, w, h), source.pivot / rect.size, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            storage ??= residentMade;
            storage.Add(texture); storage.Add(sprite);
            return sprite;
        }

        // Idle frames at 16 per second (each bystander from its own phase), in sketch once the front has passed it (where it
        // swallows the bystander, StartingVillageWorld.OpeningChase). A sketch bystander keeps the frame it had.
        void TickResidents()
        {
            foreach (var resident in residents)
            {
                if (!resident.renderer) continue;
                bool sketch = ResidentSketch(resident);
                if (!sketch) resident.clock += Time.deltaTime;
                int frame = ResidentFrame(resident);
                // The sketch frames carry their own pencil line: the ink outline only on the colour bystander.
                if (resident.outline) resident.renderer.sharedMaterial = sketch ? residentPlain : resident.outline;
                // While the front is out the colour drains per pixel over the sketch (StartingVillageWorld.EraseColour).
                if (ResidentErase(resident, frame)) continue;
                resident.renderer.sprite = (sketch ? resident.sketch : resident.color)[frame];
            }
        }

        bool ResidentSketch(Resident resident) => !float.IsNaN(erasureLeft) && (ErasureMet || erasureLeft >= resident.x + resident.lag);
        static int ResidentFrame(Resident resident) => (Mathf.FloorToInt(resident.clock * ResidentFps) + resident.phase) % resident.color.Length;

        // The idle frame a resident shows and whether the erasure has made it a sketch, null for anyone else.
        public (int frame, bool sketch)? ResidentPose(string id)
        {
            foreach (var resident in residents) if (resident.id == id) return (ResidentFrame(resident), ResidentSketch(resident));
            return null;
        }

        // Where a resident's speech bubble points (above its head), null for anyone else.
        public Vector2? ResidentTop(string id)
        {
            foreach (var resident in residents) if (resident.id == id) return new Vector2(resident.x, GroundTop + resident.head);
            return null;
        }

        // The name tag of a resident's bubbles, null for anyone else.
        public string ResidentName(string id)
        {
            foreach (var resident in residents) if (resident.id == id) return resident.name;
            return null;
        }

        // The lawn's wood planks (left end, middles, right end) with a one-way top surface, like the chapter's platforms.
        void Platform(float x, float top, int width)
        {
            WoodPlatform("Opening course platform", x, top, width, -6, courseArt);
            const float thickness = .125f;
            Solid("Opening course platform", new Vector2(x + width / 2f, GroundTop + top - thickness / 2), new Vector2(width, thickness), true);
        }

        void Solid(string name, Vector2 centre, Vector2 size, bool oneWay)
        {
            var go = new GameObject(name); go.transform.SetParent(World.transform, false);
            go.transform.position = centre;
            var surface = go.AddComponent<ArenaSurface>();
            surface.oneWay = oneWay;
            surface.footstepMaterial = oneWay ? FootstepMaterial.Stone : FootstepMaterial.Grass;
            go.AddComponent<BoxCollider2D>().size = size;
            courseObjects.Add(go);
        }

        public void RemoveOpeningCourse()
        {
            dodgeGateParts.Clear(); dodgeGateBuilt = dodgeGateOpen = false;
            foreach (var go in courseObjects) if (go) Destroy(go);
            ClearResidentTwins(); ClearFrontFlakes();
            courseObjects.Clear(); coursePits.Clear(); courseBlocks.Clear(); residents.Clear();
            foreach (var made in residentMade) if (made) Destroy(made);
            residentMade.Clear();
            foreach (var key in new List<string>(wrapped.Keys)) if (key.StartsWith("Opening course/", System.StringComparison.Ordinal)) wrapped.Remove(key);
            foreach (var ground in hiddenGround) if (ground) ground.enabled = true;
            hiddenGround.Clear();
            courseLeftX = float.NegativeInfinity;
            paired.RemoveAll(piece => courseArt.Contains(piece.renderer));
            foreach (var art in courseArt) if (art) Destroy(art.gameObject);
            courseArt.Clear();
            if (villageLeftLimit) villageLeftLimit.enabled = true;
            villageLeftLimit = null;
            if (World) World.CameraMinPixels = 0;
            Physics2D.SyncTransforms();
        }
    }
}
