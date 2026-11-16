using System;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Run;
using UnityEngine;

namespace ForestCombatV2
{
    // 하늘길: the vertical parkour above the well village (docs/design/world/village/starting-village.md 「하늘길」, data/hub/sky_parkour.json).
    // F at the record farmhouse door puts the hero on the start stone just above the village screen (no sky stone shows over
    // the village), where the run's clock starts; zig-zag steps above it lead to the clouds (30 units). Six zones climb to the bell
    // tower at goalY: floating stones, crumbling stones, moving boards, climbable floating walls (wall kick, dash) and
    // launchers at the village's edges that shoot arrows along the course. An arrow only pushes the hero aside (the hub's
    // player hit reaction: 0.9 units over 0.18 s); it never hurts. Reaching the top ends the run and writes its record
    // (profile skyParkourRecords) on the board beside the keeper; standing on the village ground again ends an unfinished
    // run. The course art is the 2x2-dot pixel set (resources environments.stage1-pixel-sky-parkour-20261112: stones,
    // crumbling stones, rope boards, launchers, arrows, the bell tower and the summit record board) and the combat lawn
    // tiles for the walls; the keeper is the village NPC set's bell keeper. The sky is StartingVillageWorld.SkyBackdrop.
    public sealed partial class StartingVillageWorld
    {
        [Serializable] public sealed class SkyData
        {
            public int schemaVersion; public float worldWidth, goalY, doorX, summitBoardX, fallCountHeight;
            public SkyZone[] zones; public SkyPlatform[] platforms; public SkyMover[] movers; public SkyWall[] walls; public SkyLauncher[] launchers;
            public SkyArrowSpec arrow; public SkyCrumbleSpec crumble; public SkyKeeper keeper;
        }
        [Serializable] public sealed class SkyZone { public string name; public float y, spawnX, spawnY; }
        [Serializable] public sealed class SkyPlatform { public float x, y, w; public string kind; }
        [Serializable] public sealed class SkyMover { public float x0, y0, x1, y1, w, speed; }
        [Serializable] public sealed class SkyWall { public float x, y, w, h; }
        [Serializable] public sealed class SkyLauncher { public float x, y, period, offset; public int dir; public bool edge; }
        [Serializable] public sealed class SkyArrowSpec { public float speed, warnSeconds, length, knockDistance, knockSeconds, invulnerableSeconds; }
        [Serializable] public sealed class SkyCrumbleSpec { public float shakeSeconds, goneSeconds, returnSeconds; }
        [Serializable] public sealed class SkyKeeper { public float x; public string name, welcome; public string[] lines; }

        public enum SkyTarget { None, Door, SummitBoard, Keeper }

        sealed class SkyPad { public SkyPlatform data; public BoxCollider2D collider; public readonly List<SpriteRenderer> art = new List<SpriteRenderer>(); public readonly List<Sprite[]> frames = new List<Sprite[]>(); public readonly List<float> artX = new List<float>(); public int state; public float t; }
        sealed class SkyLift { public SkyMover data; public Transform root; public SpriteRenderer art; public BoxCollider2D collider; public float u; public Vector2 at, delta; }
        sealed class SkyShooter { public SkyLauncher data; public SpriteRenderer art; public Sprite[] frames; public int cycle = -1; public float warn; }
        sealed class SkyArrow { public Vector2 at; public float vx, life; public SpriteRenderer art; }

        const string SkyDataPath = "HubSkyParkour";
        const int SkyArtOrder = -6, SkyLauncherOrder = -5, SkyArrowOrder = 6, SummitBoardOrder = -12;
        static readonly Color32 SkyInk = new Color32(43, 37, 32, 255), SkyHot = new Color32(212, 80, 42, 255);

        public SkyData Sky { get; private set; }
        public bool SkyBuilt { get; private set; }
        // The run: from the start stone to the top. Cleared stays true on the summit until the hero touches the ground.
        public bool SkyRunActive { get; private set; }
        public bool SkyCleared { get; private set; }
        public bool SkyAssisted { get; set; }
        public float SkyClock { get; private set; }
        public int SkyFalls { get; private set; }
        public int SkyHits { get; private set; }
        public float SkyBest { get; private set; }
        public float SkyHeight => Ready ? World.arena.Player.Position.y - GroundTop : 0;
        public SkyParkourRecord SkyLastRecord { get; private set; }
        public bool SkyNewBest { get; private set; }
        public SkyTarget SkyNear { get; private set; }

        // The village screens play the door fade and the keeper's lines; without them (tests) the door moves at once.
        public event Action SkyDoorRequested;
        public event Action<SkyTarget> SkyTalkRequested;
        public event Action<string> SkyZoneEntered;
        public event Action<SkyParkourRecord> SkyFinished;

        readonly List<GameObject> skyObjects = new List<GameObject>();
        readonly List<SpriteRenderer> skyArt = new List<SpriteRenderer>();
        readonly List<SkyPad> skyPads = new List<SkyPad>();
        readonly List<SkyLift> skyLifts = new List<SkyLift>();
        readonly List<SkyShooter> skyShooters = new List<SkyShooter>();
        readonly List<SkyArrow> skyArrows = new List<SkyArrow>();
        readonly List<Rect> skyBlocks = new List<Rect>();
        readonly HashSet<int> skySeenZones = new HashSet<int>();
        Sprite skyLauncherSprite, skyArrowSprite;
        readonly Dictionary<string, Sprite> skyProps = new Dictionary<string, Sprite>();
        readonly List<UnityEngine.Object> skyPropMade = new List<UnityEngine.Object>();
        SkyPad skyStart, skyGoal;
        SpriteRenderer skyKeeperArt; Sprite[] skyKeeperFrames; float skyKeeperHead = 2.45f;
        SpriteRenderer recordHouse;
        float skyInvulnerable, skyPeak;
        bool skyWasGrounded = true;
        CombatArena skyArena;

        void BuildSky()
        {
            var text = Resources.Load<TextAsset>(SkyDataPath);
            if (!text) { Debug.LogWarning(SkyDataPath + " is missing; run tools/assets/build_hub_bindings.py."); return; }
            Sky = JsonUtility.FromJson<SkyData>(text.text);
            if (Sky == null || Sky.schemaVersion != 1 || Sky.platforms == null || Sky.platforms.Length == 0) { Debug.LogWarning("Sky path data is invalid."); Sky = null; return; }
            foreach (var p in Sky.platforms) BuildSkyPad(p);
            foreach (var m in Sky.movers ?? Array.Empty<SkyMover>()) BuildSkyLift(m);
            foreach (var w in Sky.walls ?? Array.Empty<SkyWall>()) BuildSkyWall(w);
            skyLauncherSprite = SkyLauncherSprite(); skyArrowSprite = SkyArrowSprite();
            foreach (var l in Sky.launchers ?? Array.Empty<SkyLauncher>()) BuildSkyShooter(l);
            BuildSkySummit();
            // The village's own side limits stop just above its ground; the sky path gets walls up to the top.
            float bottom = GroundTop + 20, top = GroundTop + Sky.goalY + 40;
            SkySolid("Sky left limit", new Vector2(-.5f, (bottom + top) / 2), new Vector2(1, top - bottom), false);
            SkySolid("Sky right limit", new Vector2(Sky.worldWidth + .5f, (bottom + top) / 2), new Vector2(1, top - bottom), false);
            // The camera may now rise to the bell tower (map pixels, y down).
            World.CameraTopPixels = -(GroundTop + Sky.goalY + 12) * 32;
            skyArena = World.arena;
            skyArena.BeforePlayerStep += SkyBeforeStep;
            skyArena.PlayerStepCompleted += SkyAfterStep;
            BuildSkyBackdrop();
            RefreshSkyColourPairs();
            Physics2D.SyncTransforms();
            SkyBuilt = true;
        }

        void DisposeSky()
        {
            if (skyArena) { skyArena.BeforePlayerStep -= SkyBeforeStep; skyArena.PlayerStepCompleted -= SkyAfterStep; }
            skyArena = null;
            if (skyLauncherSprite) Destroy(skyLauncherSprite.texture);
            if (skyArrowSprite) Destroy(skyArrowSprite.texture);
            foreach (var made in skyPropMade) if (made) Destroy(made);
            skyPropMade.Clear();
            DisposeSkyBackdrop();
        }

        // ---- Building ------------------------------------------------------------------------------------------------

        GameObject SkySolid(string name, Vector2 centre, Vector2 size, bool oneWay, Transform parent = null)
        {
            var go = new GameObject(name); go.transform.SetParent(parent ? parent : World.transform, false);
            go.transform.position = centre;
            var surface = go.AddComponent<ArenaSurface>(); surface.oneWay = oneWay; surface.footstepMaterial = FootstepMaterial.Stone;
            go.AddComponent<BoxCollider2D>().size = size;
            skyObjects.Add(go);
            return go;
        }

        // Stone platform tiles (left end, middles, right end) as the opening's platforms; top-left pivot on the walking line.
        void SkyTiles(List<SpriteRenderer> into, Transform parent, float x, float top, int width)
        {
            for (int i = 0; i < width; i++)
            {
                int tile = width == 1 ? 3 : i == 0 ? 1 : i == width - 1 ? 5 : 2 + (i - 1) % 3;
                var r = Textured("Sky stone", PlatformPath + tile + "_32x32", PlatformSketchPath + tile + "_32x32", new Vector2(0, 1), SkyArtOrder);
                if (parent) r.transform.SetParent(parent, false);
                r.transform.position = new Vector3(x + i, GroundTop + top, 0);
                into.Add(r); skyArt.Add(r);
            }
        }

        // ---- Course pictures (resources environments.village.sky-parkour-props-20261105, Unity Hub/sky/props) -----------
        // 2x2-dot pixel pictures (stage1-pixel-sky-parkour-20261112) drawn 1:1 at 32 PPU with each picture's walking row on
        // the platform's top: every stone and the crumbling stone on texel row 8 (the lawn tiles' contact line), the rope
        // board 78, the launcher's middle row 16 at the arrow's height. Moving pictures stay on the 2-texel dot grid.
        const string SkyPropPath = "Hub/sky/props/";
        const int SkyStoneTop = 8, SkyCrumbleTop = 8, SkyBoardTop = 78, SkyLauncherMiddle = 16;
        static float SkyDot(float v) => Mathf.Round(v * 16) / 16;

        Sprite SkyProp(string name)
        {
            if (!skyProps.TryGetValue(name, out var sprite)) skyProps[name] = sprite = Resources.Load<Sprite>(SkyPropPath + name);
            return sprite;
        }

        // A wide stone in 1~3 tile pictures (8 -> 3, 2, 3; 9 -> 3, 3, 3), the smaller piece in the middle.
        static List<int> SkySplitTiles(int n)
        {
            var parts = new List<int>();
            if (n <= 3) { parts.Add(n); return parts; }
            for (int left = n; left > 0;) { int k = left == 4 ? 2 : Mathf.Min(3, left); parts.Add(k); left -= k; }
            if (parts.Count > 2) { int small = parts[1]; parts.RemoveAt(1); parts.Add(small); }
            return parts;
        }

        // The prototype's seeded choice of grassy or bare pictures per stone (its x is the hub x + 8).
        static Func<float> SkySeed(float n)
        {
            long s = ((long)Math.Floor(n * 9301 + 49297) % 233280 + 233280) % 233280; if (s == 0) s = 1;
            return () => (s = s * 16807 % 2147483647) / 2147483647f;
        }

        bool SkyStoneArt(SkyPad pad, int width)
        {
            var p = pad.data; var rnd = SkySeed((p.x + 8) * 31 + p.y * 7); float x = p.x;
            var pieces = new List<(Sprite sprite, float x, int top)>();
            foreach (int k in SkySplitTiles(width))
            {
                string name = "floating-" + (p.kind == "goal" || rnd() < .45f ? "bare" : "green") + "-" + k + "tile";
                var sprite = SkyProp(name); if (!sprite) return false;
                pieces.Add((sprite, x, SkyStoneTop)); x += k;
            }
            foreach (var piece in pieces)
            {
                var r = Renderer("Sky stone", piece.sprite, SkyArtOrder);
                r.transform.position = new Vector3(piece.x, GroundTop + p.y + piece.top / 32f, 0);
                pad.art.Add(r); skyArt.Add(r); skyObjects.Add(r.gameObject);
            }
            return true;
        }

        // A crumbling stone: the 64 px picture per two tiles, the 32 px one for a single tile or an odd end.
        bool SkyCrumbleArt(SkyPad pad, int width)
        {
            var whole = new Sprite[6]; var single = new Sprite[6];
            for (int f = 0; f < whole.Length; f++)
                if (!(whole[f] = SkyProp("crumbling-0" + f)) || !(single[f] = SkyProp("crumbling-1tile-0" + f))) return false;
            for (int i = 0; i < width; i += 2)
            {
                var frames = i + 1 == width ? single : whole;
                var r = Renderer("Sky crumbling stone", frames[0], SkyArtOrder);
                pad.art.Add(r); pad.frames.Add(frames); pad.artX.Add(i); skyArt.Add(r); skyObjects.Add(r.gameObject);
            }
            return true;
        }

        void BuildSkyPad(SkyPlatform p)
        {
            var pad = new SkyPad { data = p };
            int width = Mathf.Max(1, Mathf.RoundToInt(p.w));
            bool drawn = p.kind == "crumble" ? SkyCrumbleArt(pad, width) : SkyStoneArt(pad, width);
            if (!drawn)
            {
                foreach (var r in pad.art) if (r) Destroy(r.gameObject);
                pad.art.Clear(); pad.frames.Clear(); pad.artX.Clear();
                SkyTiles(pad.art, null, p.x, p.y, width);
            }
            const float thickness = .125f;
            pad.collider = SkySolid("Sky stone " + p.kind, new Vector2(p.x + width / 2f, GroundTop + p.y - thickness / 2), new Vector2(width, thickness), true).GetComponent<BoxCollider2D>();
            skyPads.Add(pad);
            if (p.kind == "start") skyStart = pad; else if (p.kind == "goal") skyGoal = pad;
        }

        void BuildSkyLift(SkyMover m)
        {
            int width = Mathf.Max(1, Mathf.RoundToInt(m.w));
            var root = new GameObject("Sky moving board").transform; root.SetParent(World.transform, false);
            root.position = new Vector3(m.x0, GroundTop + m.y0, 0);
            skyObjects.Add(root.gameObject);
            var lift = new SkyLift { data = m, root = root, at = new Vector2(m.x0, m.y0) };
            var board = SkyProp(width >= 3 ? "moving-3tile" : "moving-2tile");
            if (board)
            {
                var r = Renderer("Sky rope board", board, SkyArtOrder);
                r.transform.SetParent(root, false);
                r.transform.position = new Vector3(m.x0, GroundTop + m.y0 + SkyBoardTop / 32f, 0);
                skyArt.Add(r); lift.art = r;
            }
            else SkyTiles(new List<SpriteRenderer>(), root, m.x0, m.y0, width);
            const float thickness = .125f;
            var go = SkySolid("Sky moving board top", new Vector2(m.x0 + width / 2f, GroundTop + m.y0 - thickness / 2), new Vector2(width, thickness), true, root);
            lift.collider = go.GetComponent<BoxCollider2D>();
            skyLifts.Add(lift);
        }

        // A floating wall of the stage-1 lawn and soil tiles (Stage1Combat/terrain-atlas, the combat stages' 47 connected
        // shapes; resources environments/stage1-pixel-lawn-wood-32): grass along its top, soil down its sides. The tiles
        // are lifted 8 px so the grass rises over the walking line as on the combat ground. The sprites are the sky path's
        // own (skyPropMade).
        const string SkyWallAtlasPath = "Stage1Combat/terrain-atlas";
        const float SkyWallLift = .25f;
        static readonly int[] SkyWallDx = { 0, 1, 0, -1, 1, 1, -1, -1 }, SkyWallDy = { -1, 0, 1, 0, -1, 1, 1, -1 };
        static int[] skyWallShapes;
        readonly Dictionary<int, Sprite> skyWallTiles = new Dictionary<int, Sprite>();

        // The 47 connected shapes: a corner counts only when both of its sides are solid (as Stage1CombatArt).
        static int SkyWallCanonical(int m)
        {
            int result = m & 15;
            for (int i = 0; i < 4; i++) if ((m & (1 << i)) != 0 && (m & (1 << ((i + 1) % 4))) != 0 && (m & (16 << i)) != 0) result |= 16 << i;
            return result;
        }

        // Atlas cell k (row-major, 16 per row) holds shape k/4, variant k%4 (runtime/tileset.json).
        Sprite SkyWallTile(Texture2D atlas, int mask, int variant)
        {
            if (skyWallShapes == null)
            {
                var set = new SortedSet<int>(); for (int m = 0; m < 256; m++) set.Add(SkyWallCanonical(m));
                skyWallShapes = new int[set.Count]; set.CopyTo(skyWallShapes);
            }
            int k = Array.IndexOf(skyWallShapes, mask) * 4 + variant;
            if (skyWallTiles.TryGetValue(k, out var sprite) && sprite) return sprite;
            sprite = Sprite.Create(atlas, new Rect(k % 16 * 32, atlas.height - (k / 16 + 1) * 32, 32, 32), Vector2.one * .5f, 32, 0, SpriteMeshType.FullRect);
            sprite.name = "Sky wall tile " + mask; skyPropMade.Add(sprite);
            return skyWallTiles[k] = sprite;
        }

        void BuildSkyWall(SkyWall w)
        {
            int cols = Mathf.Max(1, Mathf.RoundToInt(w.w)), rows = Mathf.Max(1, Mathf.RoundToInt(w.h));
            var atlas = Resources.Load<Texture2D>(SkyWallAtlasPath);
            if (atlas)
            {
                var root = new GameObject("Sky wall art").transform; root.SetParent(transform, false);
                root.position = new Vector3(w.x, GroundTop + w.y, 0);
                skyObjects.Add(root.gameObject);
                bool Solid(int cx, int cy) => cx >= 0 && cy >= 0 && cx < cols && cy < rows;   // cy counts down from the top
                for (int y = 0; y < rows; y++)
                    for (int x = 0; x < cols; x++)
                    {
                        int mask = 0;
                        for (int i = 0; i < 8; i++) if (Solid(x + SkyWallDx[i], y + SkyWallDy[i])) mask |= 1 << i;
                        uint mark = unchecked((uint)(Mathf.RoundToInt(w.x) + x) * 73856093u ^ (uint)(Mathf.RoundToInt(w.y) - y) * 19349663u); mark ^= mark >> 13;
                        var tile = SkyWallTile(atlas, SkyWallCanonical(mask), (int)(mark % 4));
                        var r = Renderer("Sky wall tile", tile, CourseBlockOrder);
                        r.transform.SetParent(root, false);
                        r.transform.localPosition = new Vector3(x + .5f, rows - y - .5f + SkyWallLift, 0);
                        skyArt.Add(r);
                        if (y < rows - 1) continue;
                        // The bottom row again, unlifted and behind, fills the 8 px the lift leaves above the wall's bottom.
                        var foot = Renderer("Sky wall foot", tile, CourseBlockOrder - 1);
                        foot.transform.SetParent(root, false);
                        foot.transform.localPosition = new Vector3(x + .5f, .5f, 0);
                        skyArt.Add(foot);
                    }
            }
            else Debug.LogWarning(SkyWallAtlasPath + " is missing; the sky walls stand without pictures.");
            SkySolid("Sky wall", new Vector2(w.x + w.w / 2f, GroundTop + w.y + w.h / 2f), new Vector2(w.w, w.h), false);
            skyBlocks.Add(new Rect(w.x, GroundTop + w.y, w.w, w.h));
        }

        void BuildSkyShooter(SkyLauncher l)
        {
            // The launcher pictures: 00 at rest, 01~03 brightening while it loads.
            string side = l.dir > 0 ? "right" : "left";
            var frames = new Sprite[4];
            for (int f = 0; f < frames.Length; f++) frames[f] = SkyProp("launcher-" + side + "-0" + f);
            var shooter = new SkyShooter { data = l };
            SpriteRenderer r;
            if (Array.TrueForAll(frames, f => f))
            {
                shooter.frames = frames;
                r = Renderer("Sky launcher", frames[0], SkyLauncherOrder);
                // Edge launchers have the muzzle at x; wall launchers sink 8 px into their wall.
                float w = frames[0].rect.width / 32f, left = l.edge ? (l.dir > 0 ? l.x - w : l.x) : (l.dir > 0 ? l.x - 8 / 32f : l.x - w + 8 / 32f);
                r.transform.position = new Vector3(left, GroundTop + l.y + SkyLauncherMiddle / 32f, 0);
            }
            else
            {
                r = Renderer("Sky launcher", skyLauncherSprite, SkyLauncherOrder);
                r.flipX = l.dir < 0;
                float body = 24 / 32f, cx = l.edge ? l.x - l.dir * body / 2 : l.x - l.dir * (body / 2 - 8 / 32f);
                r.transform.position = new Vector3(cx, GroundTop + l.y, 0);
            }
            shooter.art = r;
            skyShooters.Add(shooter);
            skyObjects.Add(r.gameObject);
        }

        void BuildSkySummit()
        {
            if (skyGoal == null) return;
            float top = GroundTop + skyGoal.data.y, centre = skyGoal.data.x + skyGoal.data.w / 2;
            // The bell tower: the 2x2-dot pixel picture (Hub/sky/props/bell-tower, 112x368, the tower alone, about 1.3 times
            // the old colour picture's tower). Its centre column 56 is centred on the stone and its foot row 367 sinks one dot
            // under the walking line, whatever pivot the sprite was imported with; only the bell and the finial keep their gold.
            // Without it the old colour picture (280x311, tower and houses, columns 29~259, foot row 299) stands there.
            var towerArt = Resources.Load<Sprite>(SkyPropPath + "bell-tower");
            bool pixelTower = towerArt;
            if (!pixelTower) towerArt = Resources.Load<Sprite>("Hub/approved-background/color/tower");
            if (towerArt)
            {
                float DrawnCentre = pixelTower ? 56 : (29 + 260) / 2f, FootRow = pixelTower ? 367 : 299, Sink = pixelTower ? 2 : 3;
                float TowerBellX = pixelTower ? PixelTowerBellX : ColourTowerBellX, TowerBellRow = pixelTower ? PixelTowerBellRow : ColourTowerBellRow;
                // Behind the summit board (order -12), which stands at the tower's left foot.
                var tower = Renderer("Sky bell tower", towerArt, SummitBoardOrder - 1);
                float height = towerArt.rect.height, left = centre - DrawnCentre / 32, bottom = top - Sink / 32f - (height - 1 - FootRow) / 32;
                tower.transform.position = new Vector3(left + towerArt.pivot.x / 32, bottom + towerArt.pivot.y / 32, 0);
                skyArt.Add(tower); skyObjects.Add(tower.gameObject);
                skyBellAt = new Vector2(left + TowerBellX / 32, bottom + (height - TowerBellRow) / 32);
            }
            // The summit record board: its own pixel picture (Hub/sky/props/summit-record-board, 80x84), a stone lectern with
            // an open record book, centre column 40, foot row 83 one dot under the walking line. Without it the village's
            // departure sign stands there as before.
            var boardArt = Resources.Load<Sprite>(SkyPropPath + "summit-record-board");
            if (boardArt)
            {
                const float BoardCentre = 40, BoardFoot = 83, BoardSink = 2;
                var board = Renderer("Sky summit record board", boardArt, SummitBoardOrder);
                float left = Sky.summitBoardX - BoardCentre / 32, bottom = top - BoardSink / 32f - (boardArt.rect.height - 1 - BoardFoot) / 32;
                board.transform.position = new Vector3(left + boardArt.pivot.x / 32, bottom + boardArt.pivot.y / 32, 0);
                skyArt.Add(board); skyObjects.Add(board.gameObject);
            }
            else
            {
                var board = Place("departure_sign", "props", Sky.summitBoardX, SummitBoardOrder, null, skyGoal.data.y);
                if (board) skyArt.Add(board);
            }
            // The bell keeper, from the village NPC set (faces left, toward the summit board and the way up).
            var frames = new List<Sprite>();
            for (int i = 1; ; i++) { var f = Resources.Load<Sprite>($"Hub/sky/keeper/F{i:00}"); if (!f) break; frames.Add(f); }
            if (frames.Count > 0 && Sky.keeper != null)
            {
                skyKeeperFrames = frames.ToArray();
                // In front of the tower picture, behind the stones whose top covers the feet a little (as the opening residents).
                skyKeeperArt = Renderer("Sky bell keeper", frames[0], SkyArtOrder - 1);
                skyKeeperArt.transform.position = new Vector3(Sky.keeper.x, top, 0);
                skyKeeperArt.transform.localScale = new Vector3(ResidentScale, ResidentScale, 1);
                skyKeeperArt.flipX = !ResidentArtFacesLeft;
                skyKeeperHead = ArtTop(frames[0]) + ResidentHeadGap;
                skyObjects.Add(skyKeeperArt.gameObject);
            }
        }

        Sprite SkyLauncherSprite()
        {
            const int w = 24, h = 20; var px = new Color32[w * h];
            Color32 stone = new Color32(232, 220, 198, 255), shade = new Color32(196, 182, 160, 255);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                {
                    bool edge = x == 0 || y == 0 || y == h - 1 || x == 15 && y > 3 && y < h - 4;
                    bool barrel = x >= 15 && y >= 6 && y <= 13;
                    if (x >= 16 && !barrel) { px[y * w + x] = default; continue; }
                    px[y * w + x] = edge || barrel && (y == 6 || y == 13 || x == w - 1) ? SkyInk : barrel ? shade : (x + y) % 5 == 0 ? shade : stone;
                }
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point }; t.SetPixels32(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 32);
        }

        Sprite SkyArrowSprite()
        {
            const int w = 35, h = 9; var px = new Color32[w * h];
            Color32 wood = new Color32(201, 176, 140, 255);
            for (int x = 5; x < w - 6; x++) { px[4 * w + x] = SkyInk; px[5 * w + x] = wood; }
            for (int i = 0; i < 5; i++) for (int y = 4 - i; y <= 4 + i; y++) px[y * w + (w - 1 - i)] = SkyInk;
            for (int i = 0; i < 4; i++) { px[(4 + i) * w + (1 + i)] = SkyHot; px[(4 - i) * w + (1 + i)] = SkyHot; px[(4 + i) * w + (4 + i)] = SkyHot; px[(4 - i) * w + (4 + i)] = SkyHot; }
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point }; t.SetPixels32(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 32);
        }

        // ---- Simulation (the arena's fixed 120 Hz steps) ---------------------------------------------------------------

        // Moving boards move before the hero, carrying a hero who stands on them.
        void SkyBeforeStep(float dt)
        {
            if (Sky == null) return;
            var player = World.arena.Player;
            foreach (var lift in skyLifts)
            {
                var m = lift.data;
                float length = Mathf.Max(.01f, Vector2.Distance(new Vector2(m.x0, m.y0), new Vector2(m.x1, m.y1)));
                lift.u = (lift.u + dt / (2 * length / m.speed)) % 1;
                float k = .5f - .5f * Mathf.Cos(lift.u * Mathf.PI * 2);   // a short pause at either end
                var next = Vector2.Lerp(new Vector2(m.x0, m.y0), new Vector2(m.x1, m.y1), k);
                lift.delta = next - lift.at;
                bool riding = player && player.grounded && OnTop(player.Position, lift.at.x, lift.at.y, Mathf.Round(m.w));
                lift.at = next;
                // The children were placed in world space with the root at (x0, y0); moving the root moves them along.
                lift.root.position = new Vector3(next.x, GroundTop + next.y, 0);
                if (riding) player.Position += lift.delta;
            }
            if (skyLifts.Count > 0) Physics2D.SyncTransforms();
        }

        bool OnTop(Vector2 feet, float x, float top, float width) =>
            Mathf.Abs(feet.y - (GroundTop + top)) < .08f && feet.x + .47f > x && feet.x - .47f < x + width;

        void SkyAfterStep(float dt)
        {
            if (Sky == null) return;
            var player = World.arena.Player; if (!player) return;
            var feet = player.Position; float height = feet.y - GroundTop;
            bool grounded = player.grounded;
            SkyPad under = null;
            if (grounded) foreach (var pad in skyPads) if (pad.state != 2 && OnTop(feet, pad.data.x, pad.data.y, Mathf.Round(pad.data.w))) { under = pad; break; }

            // Crumbling stones: stepped on → shake → gone (collider off) → back.
            var c = Sky.crumble;
            foreach (var pad in skyPads)
            {
                if (pad.data.kind != "crumble") continue;
                pad.t += dt;
                if (pad.state == 0 && pad == under) { pad.state = 1; pad.t = 0; }
                else if (pad.state == 1 && pad.t >= c.shakeSeconds) { pad.state = 2; pad.t = 0; pad.collider.enabled = false; Physics2D.SyncTransforms(); }
                else if (pad.state == 2 && pad.t >= c.goneSeconds) { pad.state = 3; pad.t = 0; pad.collider.enabled = true; Physics2D.SyncTransforms(); }
                else if (pad.state == 3 && pad.t >= c.returnSeconds) { pad.state = 0; pad.t = 0; }
            }

            // Run: starts on the start stone, ends on the village ground (unfinished runs keep nothing).
            if (!SkyRunActive && !SkyCleared && under == skyStart && skyStart != null) StartSkyRun(height);
            if (grounded && height < .05f && (SkyRunActive || SkyCleared))
            {
                SkyRunActive = false; SkyCleared = false; SkyLastRecord = null;
                if (feet.x > GroundWidth - 1)
                {
                    player.Position = Find(ReturnWell).Position + Vector2.right * 2;
                    player.velocity = Vector2.zero; Physics2D.SyncTransforms();
                    World.CameraRightPixels = GroundWidth * 32;
                    World.RefreshCamera(0, true);
                }
            }
            if (SkyRunActive)
            {
                SkyClock += dt;
                if (!grounded) skyPeak = Mathf.Max(skyPeak, height);
                if (grounded && !skyWasGrounded) { if (skyPeak - height > Sky.fallCountHeight) SkyFalls++; skyPeak = height; }
                if (grounded) skyPeak = height;
                SkyBest = Mathf.Max(SkyBest, height);
                if (grounded)
                    for (int i = Sky.zones.Length - 1; i > 0; i--)
                        if (height >= Sky.zones[i].y - .5f) { if (skySeenZones.Add(i)) SkyZoneEntered?.Invoke(Sky.zones[i].name); break; }
                if (under == skyGoal && skyGoal != null) FinishSkyRun();
            }
            skyWasGrounded = grounded;

            TickSkyArrows(dt, player);
        }

        void StartSkyRun(float height)
        {
            SkyRunActive = true; SkyCleared = false; SkyAssisted = false; SkyClock = 0; SkyFalls = 0; SkyHits = 0; SkyBest = height; skyPeak = height;
            SkyLastRecord = null; SkyNewBest = false; skySeenZones.Clear(); skySeenZones.Add(0);
        }

        void FinishSkyRun()
        {
            SkyRunActive = false; SkyCleared = true;
            var service = Chapter1ProfileHost.Service;
            var before = service.Snapshot.BestSkyParkour;
            var record = new SkyParkourRecord { at = DateTime.Now.ToBinary(), seconds = Mathf.Round(SkyClock * 100) / 100, falls = SkyFalls, hits = SkyHits, assisted = SkyAssisted };
            service.RecordSkyParkour(record);
            SkyNewBest = !record.assisted && (before == null || record.seconds < before.seconds);
            SkyLastRecord = record;
            StartSkyCelebration();
            SkyFinished?.Invoke(record);
        }

        void TickSkyArrows(float dt, CombatPlayer player)
        {
            var a = Sky.arrow; float now = World.arena.Now;
            foreach (var s in skyShooters)
            {
                var l = s.data;
                float phase = Mathf.Repeat(now + l.offset, l.period); int cycle = Mathf.FloorToInt((now + l.offset) / l.period);
                s.warn = Mathf.Clamp01((phase - (l.period - a.warnSeconds)) / a.warnSeconds);
                if (s.frames != null) s.art.sprite = s.frames[s.warn <= 0 ? 0 : Mathf.Min(3, 1 + Mathf.FloorToInt(s.warn * 3))];
                else s.art.color = Color.Lerp(Color.white, (Color)SkyHot, s.warn * .85f);
                if (cycle != s.cycle)
                {
                    if (s.cycle >= 0)
                    {
                        var picture = SkyProp(l.dir > 0 ? "arrow-right" : "arrow-left");
                        var r = Renderer("Sky arrow", picture ? picture : skyArrowSprite, SkyArrowOrder); r.flipX = !picture && l.dir < 0;
                        float range = l.edge ? Sky.worldWidth : 40;
                        skyArrows.Add(new SkyArrow { at = new Vector2(l.x + l.dir * .3f, GroundTop + l.y), vx = l.dir * a.speed, life = range / a.speed, art = r });
                    }
                    s.cycle = cycle;
                }
            }
            skyInvulnerable -= dt;
            var hurt = player.hurtCollider ? player.hurtCollider.bounds : new Bounds(player.Center, new Vector3(.9375f, 2, 1));
            for (int i = skyArrows.Count - 1; i >= 0; i--)
            {
                var arrow = skyArrows[i];
                arrow.at.x += arrow.vx * dt; arrow.life -= dt;
                float tip = arrow.at.x + Mathf.Sign(arrow.vx) * a.length / 2, tail = arrow.at.x - Mathf.Sign(arrow.vx) * a.length / 2;
                bool gone = arrow.life <= 0 || arrow.at.x < -2 || arrow.at.x > Sky.worldWidth + 2;
                foreach (var block in skyBlocks) if (block.Contains(new Vector2(tip, arrow.at.y))) { gone = true; break; }
                if (!gone && skyInvulnerable <= 0 && Mathf.Max(tip, tail) > hurt.min.x && Mathf.Min(tip, tail) < hurt.max.x && arrow.at.y > hurt.min.y && arrow.at.y < hurt.max.y)
                {
                    // Pushed the arrow's way like a monster's hit (no damage): 0.9 units over 0.18 s, no upward pop.
                    float dir = Mathf.Sign(arrow.vx);
                    player.SetKnockMotion(World.arena.Now + a.knockSeconds, dir * a.knockDistance / a.knockSeconds);
                    if (player.velocity.y > 0) player.velocity = new Vector2(player.velocity.x, 0);
                    skyInvulnerable = a.invulnerableSeconds;
                    if (SkyRunActive) SkyHits++;
                    gone = true;
                }
                if (gone) { if (arrow.art) Destroy(arrow.art.gameObject); skyArrows.RemoveAt(i); continue; }
                arrow.art.transform.position = new Vector3(SkyDot(arrow.at.x), SkyDot(arrow.at.y), 0);
            }
        }

        // ---- Presentation (every frame) -----------------------------------------------------------------------------

        void TickSkyArt()
        {
            if (Sky == null) return;
            foreach (var pad in skyPads)
            {
                if (pad.data.kind != "crumble") continue;
                if (pad.frames.Count == 0)
                {
                    // Placeholder tiles: shake and fade.
                    float wobble = pad.state == 1 ? Mathf.Round(Mathf.Sin(pad.t * 90) * 2) / 32f : 0;
                    float fade = pad.state == 2 ? 0 : pad.state == 3 ? Mathf.Clamp01(pad.t / Sky.crumble.returnSeconds) : 1;
                    for (int i = 0; i < pad.art.Count; i++)
                    {
                        var r = pad.art[i];
                        r.transform.position = new Vector3(pad.data.x + i + wobble, GroundTop + pad.data.y, 0);
                        var col = r.color; col.a = fade; r.color = col;
                    }
                    continue;
                }
                // Before a step 00; shaking 01 and 02 by turns; giving way 03, 04, 05; coming back 00 fading in.
                int frame = pad.state == 1 ? 1 + Mathf.FloorToInt(pad.t / .09f) % 2
                    : pad.state == 2 ? (pad.t < .18f ? 3 : pad.t < .36f ? 4 : pad.t < .54f ? 5 : -1) : 0;
                float shake = pad.state == 1 ? Mathf.Round(Mathf.Sin(pad.t * 90)) / 16f : 0;
                float alpha = pad.state == 3 ? Mathf.Clamp01(pad.t / Sky.crumble.returnSeconds) : 1;
                for (int i = 0; i < pad.art.Count; i++)
                {
                    var r = pad.art[i];
                    r.enabled = frame >= 0;
                    if (frame < 0) continue;
                    r.sprite = pad.frames[i][frame];
                    r.transform.position = new Vector3(pad.data.x + pad.artX[i] + shake, GroundTop + pad.data.y + SkyCrumbleTop / 32f, 0);
                    r.color = new Color(1, 1, 1, alpha);
                }
            }
            // Rope boards move smoothly; their pictures keep to the dot grid.
            foreach (var lift in skyLifts)
                if (lift.art) lift.art.transform.position = new Vector3(SkyDot(lift.root.position.x), SkyDot(lift.root.position.y + SkyBoardTop / 32f), 0);
            if (skyKeeperArt && skyKeeperFrames != null) skyKeeperArt.sprite = skyKeeperFrames[Mathf.FloorToInt(Time.time * ResidentFps) % skyKeeperFrames.Length];
            RefreshSkyColourPairs();
            TickSkyCelebration();
        }


        readonly Dictionary<Sprite, (Sprite colour, Sprite sketch)> skyColourSprites = new Dictionary<Sprite, (Sprite, Sprite)>();
        readonly Dictionary<SpriteRenderer, Piece> skyColourPieces = new Dictionary<SpriteRenderer, Piece>();
        (Sprite colour, Sprite sketch) SkyColourPair(Sprite source)
        {
            if (skyColourSprites.TryGetValue(source, out var pair)) return pair;
            // Terrain tiles have an exact registered colour atlas with the same layout.
            Texture2D texture; Sprite colour;
            if (source.texture == Resources.Load<Texture2D>(SkyWallAtlasPath))
            {
                texture = Resources.Load<Texture2D>(GroundColourAtlas);
                colour = Sprite.Create(texture, source.rect, source.pivot / source.rect.size, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            }
            else
            {
                int w = (int)source.rect.width, h = (int)source.rect.height;
                var src = source.texture.GetPixels32(); var pixels = new Color32[w * h];
                bool wood = source.name.Contains("moving") || source.name.Contains("launcher") || source.name.Contains("arrow");
                bool green = source.name.Contains("green");
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                {
                    var c = src[((int)source.rect.y + y) * source.texture.width + (int)source.rect.x + x];
                    if (c.a == 0) { pixels[y * w + x] = c; continue; }
                    float l = (c.r * .3f + c.g * .59f + c.b * .11f) / 255f;
                    bool coloured = Mathf.Max(c.r, c.g, c.b) - Mathf.Min(c.r, c.g, c.b) > 65;
                    if (l < .29f || coloured) { pixels[y * w + x] = c; continue; }
                    Color dark = wood ? new Color32(102, 67, 41, 255) : green && y >= h - 20 ? new Color32(77, 96, 55, 255) : new Color32(134, 112, 81, 255);
                    Color light = wood ? new Color32(214, 170, 107, 255) : green && y >= h - 20 ? new Color32(158, 174, 103, 255) : new Color32(240, 218, 174, 255);
                    var tone = (Color32)Color.Lerp(dark, light, Mathf.Clamp01((l - .29f) / .65f)); tone.a = c.a; pixels[y * w + x] = tone;
                }
                texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
                texture.SetPixels32(pixels); texture.Apply(false, true); skyPropMade.Add(texture);
                colour = Sprite.Create(texture, new Rect(0, 0, w, h), source.pivot / source.rect.size, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            }
            colour.name = source.name + " colour"; skyPropMade.Add(colour);
            pair = (colour, source); skyColourSprites[source] = skyColourSprites[colour] = pair; return pair;
        }

        void RefreshSkyColourPairs()
        {
            foreach (var art in skyArt)
            {
                if (!art || !art.sprite || !art.sprite.texture.isReadable && !skyColourSprites.ContainsKey(art.sprite)) continue;
                var pair = SkyColourPair(art.sprite);
                if (!skyColourPieces.TryGetValue(art, out var piece))
                {
                    piece = new Piece { renderer = art, keepTransform = true }; skyColourPieces[art] = piece; paired.Add(piece);
                }
                piece.colorSprite = pair.colour; piece.sketchSprite = pair.sketch;
                bool sketch = ErasedOverride(piece) ?? Preview ?? !IsColoured(piece.region);
                ApplyPiece(piece, sketch);
            }
        }

        // ---- Interaction --------------------------------------------------------------------------------------------

        // The way up opens after the first run (user decision 2026-11-06): before any departure the farmhouse door shows
        // nothing and F does nothing there.
        public static bool SkyOpen => Chapter1ProfileHost.Service.Snapshot.RunsStarted > 0;

        SkyTarget FindSkyTarget(out Vector2 keyAt)
        {
            keyAt = default;
            if (Sky == null || !Ready || CourseBuilt || StationsSuppressed || !SkyOpen) return SkyTarget.None;
            var player = World.arena.Player; if (!player.grounded) return SkyTarget.None;
            var feet = player.Position; float height = feet.y - GroundTop;
            if (height < .5f && Mathf.Abs(feet.x - Sky.doorX) < 1.1f) { keyAt = new Vector2(Sky.doorX, GroundTop + 3.4f); return SkyTarget.Door; }
            if (skyGoal != null && Mathf.Abs(height - skyGoal.data.y) < .2f)
            {
                if (Mathf.Abs(feet.x - Sky.summitBoardX) < 1.4f) { keyAt = new Vector2(Sky.summitBoardX, GroundTop + skyGoal.data.y + 3.2f); return SkyTarget.SummitBoard; }
                if (Sky.keeper != null && Mathf.Abs(feet.x - Sky.keeper.x) < 2.2f) { keyAt = new Vector2(Sky.keeper.x, GroundTop + skyGoal.data.y + 3.1f); return SkyTarget.Keeper; }
            }
            return SkyTarget.None;
        }

        bool TrySkyInteract()
        {
            if (!InteractionAllowed) return false;
            var target = FindSkyTarget(out _);
            if (target == SkyTarget.None) return false;
            if (target == SkyTarget.Door) { if (SkyDoorRequested != null) SkyDoorRequested(); else EnterSkyStart(); return true; }
            SkyTalkRequested?.Invoke(target);
            return true;
        }

        // The door's destination: the start stone just above the village screen. Standing there starts
        // the run (SkyAfterStep), so the hero is born on it and the clock runs from that step.
        public void EnterSkyStart()
        {
            if (skyStart == null) return;
            var player = World.arena.Player;
            player.Position = new Vector2(skyStart.data.x + skyStart.data.w / 2, GroundTop + skyStart.data.y);
            player.velocity = Vector2.zero; Physics2D.SyncTransforms(); player.grounded = player.ProbeGround();
            skyWasGrounded = true;
            World.RefreshCamera(0, true);
        }

        // Developer-console travel: drops the hero just above the bell tower stone so the arrival
        // (the clear text, the record on the summit board) can be seen at once. The run counts as practice (연습).
        public bool WarpToSkySummit()
        {
            if (Sky == null || skyGoal == null || !InteractionAllowed) return false;
            if (!SkyRunActive) StartSkyRun(SkyHeight);
            SkyAssisted = true;
            var player = World.arena.Player;
            player.Position = new Vector2(skyGoal.data.x + 1.5f, GroundTop + skyGoal.data.y + 1.2f);
            player.velocity = Vector2.zero; Physics2D.SyncTransforms(); player.grounded = false;
            skyWasGrounded = false; skyPeak = skyGoal.data.y + 1.2f;
            World.RefreshCamera(0, true);
            return true;
        }

        // Test and review hook: stands the hero on a zone's spawn (0: the village ground).
        public void PlaceInSky(int zone)
        {
            if (Sky == null) return;
            var z = Sky.zones[Mathf.Clamp(zone, 0, Sky.zones.Length - 1)];
            var player = World.arena.Player;
            player.Position = new Vector2(z.spawnX, GroundTop + z.spawnY);
            player.velocity = Vector2.zero; Physics2D.SyncTransforms(); player.grounded = player.ProbeGround();
            World.RefreshCamera(0, true);
        }

        public Vector2 SkyKeeperTop => Sky?.keeper == null || skyGoal == null ? Vector2.zero : new Vector2(Sky.keeper.x, GroundTop + skyGoal.data.y + skyKeeperHead);
    }
}
