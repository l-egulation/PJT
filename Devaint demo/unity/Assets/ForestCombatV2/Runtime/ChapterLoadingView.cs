using System;
using System.Collections.Generic;
using Deviant.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace ForestCombatV2
{
    // Where a scene change goes; picks the loading screen's scenery and the name written under it.
    public enum LoadingPlace { Village, Chapter1, Chapter2 }

    // The run the loading hero plays: the run clip of the animation set the player is drawn with right now
    // (CharacterPresenter.CurrentForm: the weapon form in a run, the bare-handed body in the village), so every weapon and
    // body shows as it is without a case of its own (user decision 2026-11-07).
    public sealed class LoadingHeroRun
    {
        public readonly string Form;
        public readonly Sprite[] Frames;
        public readonly float[] Seconds;
        public readonly float Duration;
        LoadingHeroRun(string form, Sprite[] frames, float[] seconds)
        {
            Form = form; Frames = frames; Seconds = seconds;
            foreach (float s in seconds) Duration += s;
        }

        // The frame shown `time` seconds into the looping run.
        public Sprite At(float time)
        {
            if (Duration <= 0) return Frames[0];
            float t = Mathf.Repeat(time, Duration);
            for (int i = 0; i < Frames.Length; i++) { if (t < Seconds[i]) return Frames[i]; t -= Seconds[i]; }
            return Frames[Frames.Length - 1];
        }

        // The run clip of `form` in the catalog, or null when the form, its run clip or one of its frames is missing.
        public static LoadingHeroRun From(CharacterAnimationCatalog catalog, string form)
        {
            if (catalog?.sets == null || string.IsNullOrEmpty(form)) return null;
            CharacterAnimationSet set = null;
            foreach (var candidate in catalog.sets) if (candidate != null && candidate.form == form) set = candidate;
            var clip = set?.Find("run");
            if (clip?.frames == null || clip.seconds == null || clip.frames.Length == 0 || clip.seconds.Length != clip.frames.Length) return null;
            var frames = new Sprite[clip.frames.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = clip.sprites != null && i < clip.sprites.Length && clip.sprites[i] ? clip.sprites[i] : Resources.Load<Sprite>(clip.frames[i]);
                if (!frames[i]) return null;
            }
            return new LoadingHeroRun(form, frames, (float[])clip.seconds.Clone());
        }

        // The player of the scene being left, as it is drawn now; null when there is no player.
        public static LoadingHeroRun Capture()
        {
            foreach (var arena in UnityEngine.Object.FindObjectsByType<CombatArena>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!arena || !arena.Player) continue;
                var run = From(arena.characterAnimations, arena.Presentation.CurrentForm ?? arena.Player.AppearanceOverride);
                if (run != null) return run;
            }
            return null;
        }
    }

    // The loading screen's picture (user decision 2026-11-07, replacing the notebook): the hero runs on the spot in the
    // middle of a dark screen and the place it is going to slides past, seen only through a round window around the hero.
    // The window is built from the place's own art at the game's scale (1 art pixel = 1 window pixel, 32 per unit) and
    // shown at twice that; every layer keeps its in-game parallax speed, so the hero reads as running through the place.
    // Leaving one place for another, it opens on the place being left and the place entered sweeps in (SeamStart).
    public sealed class ChapterLoadingView
    {
        public const int Window = 192, Radius = 72, Ground = 128, HeroX = 86, Scale = 2;
        public const float RunSpeed = 200, OpenSeconds = .35f;
        // The chapter-1 player's ink outline (art-and-presentation.md), drawn in the window by a UI version of the shader.
        public const string OutlineShader = "ChapterLoading/LoadingHeroOutline";
        static readonly Color32 Paper = new Color32(245, 245, 243, 255);

        sealed class Piece
        {
            public string path; public Texture2D texture;
            public Rect crop;          // art pixels, top-left origin; zero size = the whole texture
            public Vector2 size;       // drawn size in window pixels (zero = the crop's size)
            public Vector2 at;         // top-left inside its repeat (x) and in the window (y, downwards)
        }
        sealed class Strip
        {
            public float speed, period, phase; public bool mirror, front;
            public readonly List<Piece> pieces = new List<Piece>();
            public readonly List<(Piece piece, RawImage image)> drawn = new List<(Piece, RawImage)>();
        }

        // One place's picture: its layers, the colour behind them and whether the hero wears chapter 1's ink outline there.
        sealed class Scenery
        {
            public readonly LoadingPlace place;
            public readonly List<Strip> strips = new List<Strip>();
            public Color fill = Color.clear;
            public bool outline;
            public RectMask2D back, front;
            public Scenery(LoadingPlace place) { this.place = place; }
        }

        // Leaving one place for another (user decision 2026-11-13, 「A 이어달리기」): the window opens on the place being
        // left, then the place being entered comes in from the right behind a 2 px paper seam that sweeps left faster than
        // the ground runs, so the hero runs out of one into the other. The seam starts at SeamStart and has crossed the
        // window by SeamEnd, inside the 1.15 s that always plays before the load.
        public const float SeamStart = .40f, SeamSpeed = 260;
        public static float SeamEnd => SeamStart + Window / SeamSpeed;

        readonly List<string> missing = new List<string>();
        Scenery to, from, building;

        RectTransform world, seamLine;
        Image heroImage;
        Texture2D maskTexture, rimTexture;
        Sprite maskSprite;
        Material outlineMaterial;
        GameObject maskObject;
        int drawnRadius = -1, drawnSeam = -1;
        LoadingHeroRun hero;

        // Art that failed to load for these places (empty when everything is there).
        public IReadOnlyList<string> Missing => missing;
        // The place being left, shown first; null when the window shows the place being entered alone.
        public LoadingPlace? Leaving => from?.place;

        // villageInColour: null reads the profile (checks pass it to stay off the save file).
        public static ChapterLoadingView For(LoadingPlace place, bool? villageInColour = null) => For(place, null, villageInColour);

        // leaving: the place the scene change starts from; the same place or null shows `place` alone.
        public static ChapterLoadingView For(LoadingPlace place, LoadingPlace? leaving, bool? villageInColour = null)
        {
            var view = new ChapterLoadingView();
            bool colour = place == LoadingPlace.Village || leaving == LoadingPlace.Village ? villageInColour ?? VillageInColour() : false;
            view.to = view.Make(place, colour);
            if (leaving.HasValue && leaving.Value != place) view.from = view.Make(leaving.Value, colour);
            return view;
        }

        Scenery Make(LoadingPlace place, bool villageInColour)
        {
            building = new Scenery(place);
            switch (place)
            {
                case LoadingPlace.Chapter1: Chapter1(); break;
                case LoadingPlace.Chapter2: Chapter2(); break;
                default: Village(villageInColour); break;
            }
            var made = building; building = null;
            return made;
        }

        public static string Label(LoadingPlace place) => place == LoadingPlace.Chapter1 ? "1챕터" : place == LoadingPlace.Chapter2 ? "2챕터" : "우물 마을";

        // The village comes back in colour only with every palette piece (starting-village.md); until then it is a sketch.
        static bool VillageInColour()
        {
            try { var profile = Chapter1ProfileHost.Service; return profile.PalettePieces >= profile.Catalog.PalettePieces; }
            catch (Exception e) { Debug.LogWarning("Loading: village colour unknown, drawing the sketch: " + e.Message); return false; }
        }

        // ---- places ---------------------------------------------------------------------------------------------------

        // The starting village: fixed sky, mountains .08, far village .18, near village .45 and clouds .05,
        // over the same 32px terrain atlas used by the village's colour/sketch pair.
        void Village(bool colour)
        {
            string root = "Hub/pixel-village/", state = colour ? "color/" : "sketch/";
            Static(root + "sky/" + state + "sky", new Rect(380, 100, Window, Window));
            Layer(root + "background/" + state + "mountains", .08f, 0, 0, Ground + 64, true, true);
            Layer(root + "background/" + state + "far-village", .18f, 0, -100, Ground + 64, true, true);
            Layer(root + "background/" + state + "near-village", .45f, 0, -300, Ground + 64, true, true);
            Layer(root + "sky/" + state + "clouds", .05f, 1600, 150, Ground - 40, true, false);
            PixelGround(root + "ground/" + state + "terrain-atlas");
        }

        // Chapter 1, the erased village (VillagePresentation in sketch): sky held by the camera, the far skyline whose top
        // stands 22 px below the screen's top scrolling .18, the middle row bottom-aligned with it scrolling .45, sketch
        // houses on the walking line and the sketch terrain tiles. The window is cut with the feet at screen y 380.
        void Chapter1()
        {
            building.outline = true;
            building.fill = new Color32(252, 247, 238, 255);
            Layer("Stage1Combat/background/mountains", .08f, 0, 0, Ground + 64, true, true);
            Layer("Stage1Combat/background/far-village", .18f, 0, -100, Ground + 64, true, true);
            Layer("Stage1Combat/background/near-village", .45f, 0, -300, Ground + 64, true, true);
            Layer("Stage1Combat/sky/clouds/clouds-0", .05f, 520, 100, 30, false, false);
            PixelGround("Stage1Combat/terrain-atlas");
        }

        // Chapter 2, the waterway (Chapter2WaterwayBackground/Art): the brick wall, windows onto the city with their frame
        // and grille, pipes, the mossy stone ledge from the waterway atlas, and a buttress passing in front (1.12).
        void Chapter2()
        {
            const string root = "Chapter2Waterway/";
            building.fill = new Color32(21, 32, 28, 255);
            Layer(root + "wall", 1, 0, 0, Ground - 400, anchorBottom: false, mirror: false);
            var rooms = new Strip { speed = 1, period = 420 };
            var city = MakePiece(root + "city", new Rect(200, 140, 80, 100)); if (city != null) { city.at = new Vector2(158, Ground - 114); rooms.pieces.Add(city); }
            var grille = MakePiece(root + "grille", default, new Vector2(64, 80)); if (grille != null) { grille.at = new Vector2(166, Ground - 104); rooms.pieces.Add(grille); }
            var frame = MakePiece(root + "window-frame", default, new Vector2(96, 112)); if (frame != null) { frame.at = new Vector2(150, Ground - 120); rooms.pieces.Add(frame); }
            var pipe = MakePiece(root + "pipe", default, new Vector2(128, 384)); if (pipe != null) { pipe.at = new Vector2(292, Ground - 330); rooms.pieces.Add(pipe); }
            building.strips.Add(rooms);
            var ledge = new Strip { speed = 1, period = 224 };
            var top = MakePiece(root + "WaterwayRuntimeAtlas", new Rect(64, 64, 224, 32)); if (top != null) { top.at = new Vector2(0, Ground); ledge.pieces.Add(top); }
            building.strips.Add(ledge);
            var under = new Strip { speed = 1, period = 192 };
            for (int row = 1; row <= 2; row++)
            {
                var stone = MakePiece(root + "WaterwayRuntimeAtlas", new Rect(0, 96, 192, 32)); if (stone != null) { stone.at = new Vector2(0, Ground + row * 32); under.pieces.Add(stone); }
            }
            building.strips.Add(under);
            var front = new Strip { speed = 1.12f, period = 640, phase = 40, front = true };
            var buttress = MakePiece(root + "foreground-buttress", default, new Vector2(146, 768)); if (buttress != null) { buttress.at = new Vector2(0, Ground - 600); front.pieces.Add(buttress); }
            building.strips.Add(front);
        }

        // The village's walking line (StartingVillageWorld.GroundRun): body columns from the line down, cap stones in the
        // 1 · 1.2 · 1 · .8 · 1 rhythm of 32 px stones, each hung from its contact anchor (ReferenceGround/library.json).
        void PixelGround(string path)
        {
            var atlas = Load(path); if (!atlas) return;
            var shapes = new SortedSet<int>();
            for (int mask = 0; mask < 256; mask++)
            {
                int m = mask & 15;
                for (int i = 0; i < 4; i++) if ((mask & (1 << i)) != 0 && (mask & (1 << ((i + 1) % 4))) != 0 && (mask & (16 << i)) != 0) m |= 16 << i;
                shapes.Add(m);
            }
            var ordered = new List<int>(shapes);
            var strip = new Strip { speed = 1, period = 256 };
            for (int x = 0; x < 8; x++) for (int row = 0; row < 3; row++)
            {
                int index = ordered.IndexOf(row == 0 ? 110 : 255) * 4 + x % 4;
                strip.pieces.Add(new Piece { path = path, texture = atlas, crop = new Rect(index % (atlas.width / 32) * 32, index / (atlas.width / 32) * 32, 32, 32), size = new Vector2(32, 32), at = new Vector2(x * 32, Ground - 8 + row * 32) });
            }
            building.strips.Add(strip);
        }

        Texture2D Load(string path)
        {
            var texture = Resources.Load<Texture2D>(path);
            if (!texture && !missing.Contains(path)) missing.Add(path);
            return texture;
        }
        Piece MakePiece(string path, Rect crop = default, Vector2 size = default)
        {
            var texture = Load(path); if (!texture) return null;
            var shown = crop.width > 0 ? crop.size : new Vector2(texture.width, texture.height);
            return new Piece { path = path, texture = texture, crop = crop, size = size == default ? shown : size };
        }
        // Held by the camera: a crop of the picture filling the window.
        void Static(string path, Rect crop)
        {
            var piece = MakePiece(path, crop, new Vector2(Window, Window)); if (piece == null) return;
            var strip = new Strip { speed = 0, period = 100000 }; strip.pieces.Add(piece); building.strips.Add(strip);
        }
        // One picture repeated every `period` (0 = its own width) at `speed` of the hero's run, its bottom (or top) at y.
        Piece Layer(string path, float speed, float period, float phase, float y, bool anchorBottom, bool mirror)
        {
            var piece = MakePiece(path); if (piece == null) return null;
            piece.at = new Vector2(0, anchorBottom ? y - piece.size.y : y);
            var strip = new Strip { speed = speed, period = period > 0 ? period : piece.size.x, phase = phase, mirror = mirror };
            strip.pieces.Add(piece); building.strips.Add(strip);
            return piece;
        }

        // ---- drawing --------------------------------------------------------------------------------------------------

        // Builds the window under `parent` (its centre at `centre`, stage pixels from the top-left) for `run` (may be null).
        public void Build(RectTransform parent, Vector2 centre, LoadingHeroRun run)
        {
            hero = run;
            var frame = Node("Loading window", parent);
            frame.anchoredPosition = new Vector2(centre.x - Window * Scale / 2f, -(centre.y - Window * Scale / 2f));
            frame.sizeDelta = new Vector2(Window, Window); frame.localScale = new Vector3(Scale, Scale, 1);
            maskTexture = NewTexture("Loading window mask"); rimTexture = NewTexture("Loading window rim");
            maskSprite = Sprite.Create(maskTexture, new Rect(0, 0, Window, Window), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            var maskRect = Node("Round window", frame); maskRect.sizeDelta = new Vector2(Window, Window);
            maskObject = maskRect.gameObject;
            var maskImage = maskObject.AddComponent<Image>(); maskImage.sprite = maskSprite; maskImage.raycastTarget = false;
            maskObject.AddComponent<Mask>().showMaskGraphic = false;
            world = Node("Place", maskRect); world.sizeDelta = new Vector2(Window, Window);
            // Each place is cut to its side of the seam by a RectMask2D inside the round Mask: the place being left on the
            // left, the place being entered on the right. The hero runs between the back layers and the front ones.
            foreach (var scenery in Sceneries())
            {
                scenery.back = Clip(scenery.place + " back");
                if (scenery.fill.a > 0)
                {
                    var back = Node("Fill", scenery.back.transform); back.sizeDelta = new Vector2(Window, Window);
                    var image = back.gameObject.AddComponent<Image>(); image.color = scenery.fill; image.raycastTarget = false;
                }
                foreach (var strip in scenery.strips) if (!strip.front) Draw(strip, scenery.back.transform);
            }
            var heroRect = Node("Hero", world);
            heroImage = heroRect.gameObject.AddComponent<Image>(); heroImage.raycastTarget = false; heroImage.enabled = false;
            if (to.outline || from != null && from.outline)
            {
                var shader = Resources.Load<Shader>(OutlineShader);
                if (shader) outlineMaterial = new Material(shader) { name = "Loading hero outline", hideFlags = HideFlags.DontSave };
                else Debug.LogWarning("Loading: hero outline shader missing: " + OutlineShader);
            }
            foreach (var scenery in Sceneries())
            {
                scenery.front = Clip(scenery.place + " front");
                foreach (var strip in scenery.strips) if (strip.front) Draw(strip, scenery.front.transform);
            }
            seamLine = Node("Seam", world); seamLine.sizeDelta = new Vector2(2, Window);
            var seamImage = seamLine.gameObject.AddComponent<Image>(); seamImage.color = Paper; seamImage.raycastTarget = false;
            seamLine.gameObject.SetActive(false);
            var rimRect = Node("Window rim", frame); rimRect.sizeDelta = new Vector2(Window, Window);
            var rim = rimRect.gameObject.AddComponent<RawImage>(); rim.texture = rimTexture; rim.raycastTarget = false;
            Circle(0);
            Seam(SeamAt(0));
            if (missing.Count > 0) Debug.LogWarning("Loading: missing art " + string.Join(", ", missing));
        }

        IEnumerable<Scenery> Sceneries() { if (from != null) yield return from; yield return to; }

        RectMask2D Clip(string name)
        {
            var rect = Node(name, world); rect.sizeDelta = new Vector2(Window, Window);
            return rect.gameObject.AddComponent<RectMask2D>();
        }

        void Draw(Strip strip, Transform parent)
        {
            int repeats = Mathf.CeilToInt(Window / strip.period) + 2;
            foreach (var piece in strip.pieces)
                for (int i = 0; i < repeats; i++)
                {
                    var rect = Node(piece.path, parent); rect.sizeDelta = piece.size;
                    var image = rect.gameObject.AddComponent<RawImage>(); image.texture = piece.texture; image.raycastTarget = false;
                    strip.drawn.Add((piece, image));
                }
        }

        // `time` since the screen came up: the window opens over OpenSeconds, the place slides by, the hero runs; when
        // leaving another place, the seam sweeps across from SeamStart to SeamEnd.
        public void Tick(float time)
        {
            if (!world) return;
            float k = Mathf.Clamp01(time / OpenSeconds);
            Circle(Mathf.RoundToInt(Radius * (1 - (1 - k) * (1 - k) * (1 - k))));
            Seam(SeamAt(time));
            float travelled = time * RunSpeed;
            foreach (var scenery in Sceneries()) foreach (var strip in scenery.strips) Place(strip, travelled);
            if (hero != null)
            {
                var sprite = hero.At(time);
                heroImage.sprite = sprite; heroImage.enabled = sprite;
                if (sprite)
                {
                    var r = sprite.rect; var rect = heroImage.rectTransform;
                    rect.sizeDelta = r.size; rect.pivot = new Vector2(sprite.pivot.x / r.width, sprite.pivot.y / r.height);
                    rect.anchoredPosition = new Vector2(HeroX, -Ground);
                }
            }
        }

        // The seam's x in window pixels, in 2 px steps: Window before it starts, 0 once the place being entered fills the
        // window, and always 0 without a place being left.
        public int SeamAt(float time)
        {
            if (from == null) return 0;
            if (time <= SeamStart) return Window;
            return Mathf.Clamp(Mathf.RoundToInt((Window - (time - SeamStart) * SeamSpeed) / 2f) * 2, 0, Window);
        }

        void Seam(int seam)
        {
            if (seam == drawnSeam) return;
            drawnSeam = seam;
            // RectMask2D pads in the root canvas's units, and the window is drawn at Scale (or whatever it ends up at).
            var canvas = world.GetComponentInParent<Canvas>();
            float k = canvas ? world.lossyScale.x / canvas.rootCanvas.transform.lossyScale.x : Scale;
            if (from != null)
            {
                from.back.gameObject.SetActive(seam > 0); from.front.gameObject.SetActive(seam > 0);
                from.back.padding = from.front.padding = new Vector4(0, 0, (Window - seam) * k, 0);
            }
            to.back.gameObject.SetActive(seam < Window); to.front.gameObject.SetActive(seam < Window);
            to.back.padding = to.front.padding = new Vector4(seam * k, 0, 0, 0);
            seamLine.gameObject.SetActive(from != null && seam > 0 && seam < Window);
            seamLine.anchoredPosition = new Vector2(seam, 0);
            // The ink outline belongs to chapter 1: the hero wears it while standing on chapter 1's side of the seam.
            bool ink = from != null && HeroX < seam ? from.outline : to.outline;
            heroImage.material = ink ? outlineMaterial : null;
        }

        static void Place(Strip strip, float travelled)
        {
            float offset = strip.phase - travelled * strip.speed;
            int first = Mathf.FloorToInt(-offset / strip.period) - 1, slot = 0;
            int repeats = strip.pieces.Count == 0 ? 0 : strip.drawn.Count / strip.pieces.Count;
            foreach (var piece in strip.pieces)
                for (int i = 0; i < repeats; i++, slot++)
                {
                    int n = first + i; bool flip = strip.mirror && ((n % 2) + 2) % 2 == 1;
                    var image = strip.drawn[slot].image;
                    float x = offset + n * strip.period + (flip ? strip.period - piece.at.x - piece.size.x : piece.at.x);
                    image.rectTransform.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(piece.at.y));
                    var t = piece.texture; var c = piece.crop.width > 0 ? piece.crop : new Rect(0, 0, t.width, t.height);
                    var uv = new Rect(c.x / t.width, 1 - (c.y + c.height) / t.height, c.width / t.width, c.height / t.height);
                    image.uvRect = flip ? new Rect(uv.xMax, uv.y, -uv.width, uv.height) : uv;
                }
        }

        static readonly Color32 Solid = new Color32(255, 255, 255, 255), ShadeColour = new Color32(0, 0, 0, 115), DeepShadeColour = new Color32(0, 0, 0, 56);

        // Redraws the round window at `radius` (LoadingWindowCircle, 2 px blocks): the mask, a two-step shade just inside
        // its edge and a paper rim outside.
        void Circle(int radius)
        {
            if (radius == drawnRadius) return;
            drawnRadius = radius;
            maskObject.SetActive(radius > 0);
            var cells = LoadingWindowCircle.Draw(radius);
            var mask = new Color32[Window * Window]; var rim = new Color32[Window * Window];
            for (int y = 0; y < Window; y++)
                for (int x = 0; x < Window; x++)
                {
                    byte cell = cells[y / 2 * LoadingWindowCircle.Blocks + x / 2];
                    int i = y * Window + x;
                    if (cell == LoadingWindowCircle.Outside) continue;
                    if (cell == LoadingWindowCircle.Rim) { rim[i] = Paper; continue; }
                    mask[i] = Solid;
                    if (cell == LoadingWindowCircle.Shade) rim[i] = ShadeColour;
                    else if (cell == LoadingWindowCircle.DeepShade) rim[i] = DeepShadeColour;
                }
            maskTexture.SetPixels32(mask); maskTexture.Apply(false);
            rimTexture.SetPixels32(rim); rimTexture.Apply(false);
        }

        public void Dispose()
        {
            Release(maskSprite); Release(maskTexture); Release(rimTexture); Release(outlineMaterial);
        }
        static void Release(UnityEngine.Object made)
        {
            if (!made) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(made); else UnityEngine.Object.DestroyImmediate(made);
        }

        // For checks: the hero's drawn frame, whether it wears the ink outline, and the window's radius now.
        public Sprite HeroSprite => heroImage && heroImage.enabled ? heroImage.sprite : null;
        public bool HeroOutlined => heroImage && heroImage.material && heroImage.material.shader && heroImage.material.shader.name == "ForestCombatV2/LoadingHeroOutline";
        public int WindowRadius => drawnRadius;

        static Texture2D NewTexture(string name) => new Texture2D(Window, Window, TextureFormat.RGBA32, false)
            { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };

        static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            return rect;
        }
    }
}
