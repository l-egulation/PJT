using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using Deviant.Input.Contracts;
using Deviant.Run;
using Deviant.Control.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ForestCombatV2
{
    // The starting village lobby on top of a ForestCombatWorld in villageHub mode: one flat lane with the
    // record farmhouse and well at the centre, the three unlock buildings and the departure lane. No enemies, no
    // attacks, no rewards. Art comes from the pinned resource deployment; main owns hub layout/bindings.
    // The village starts erased (all sketch, docs/design/world/village/starting-village.md): an unlock building, its workbench and the
    // ground under their span turn colour once any unlock of that building is owned in the profile.
    [DefaultExecutionOrder(600)]
    public sealed partial class StartingVillageWorld : MonoBehaviour
    {
        // label: the station's name on its paper card (docs/design/progression/village-palette-pieces.md).
        [Serializable] public sealed class Placement { public string id, label; public float x, y; }
        [Serializable] public sealed class Layout { public float worldWidthUnits, groundY, spawnX; public Placement[] stations, buildings, decorations; public Blot[] colourBlots; }

        public sealed class Station
        {
            public string Id; public string Name; public Vector2 Position; public float Reach;
            // Short name on the paper card above the station (Name is the longer building and workbench name).
            public string Label;
            // Top of the workbench art, where its keycap floats.
            public float Top;
            public SpriteRenderer Art;
            public Station(string id, string name, Vector2 position, float reach) { Id = id; Name = name; Position = position; Reach = reach; }
        }

        // Station ids follow data/combat/roguelite_progression.json (buildings[].station) plus the return well.
        public const string RecordDesk = "record_desk", ReturnWell = "return_well", PaletteBench = "palette_bench",
            SupplyCounter = "supply_counter", ReclaimBench = "reclaim_bench", DepartureTable = "departure_table", DepartureSign = "departure_sign", ServiceHub = "artisan_hub";
        const string DepartureLane = "departure_lane";
        static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            { ServiceHub, "장인의 공방" }, { DepartureSign, "출발 이정표" }, { RecordDesk, "기록" }, { ReturnWell, "우물 광장" }, { PaletteBench, "염색공방 · 팔레트 작업대" },
            { SupplyCounter, "보급소 · 보급 접수대" }, { ReclaimBench, "회수공방 · 회수 작업대" }, { DepartureTable, "출발 농로 · 출발 준비대" },
        };
        const float GroundTop = -14f;
        const float StationReach = 1.75f;

        public ForestCombatWorld World { get; private set; }
        public bool Ready { get; private set; }
        // Editor/capture preview overriding the profile state: null follows the unlocks, true all sketch, false all colour.
        public bool? Preview { get; private set; }
        public IReadOnlyCollection<string> ColouredRegions => coloured;
        readonly HashSet<string> coloured = new HashSet<string>();
        ProgressionService boundService; ProfileSnapshot boundSnapshot;
        public IReadOnlyList<Station> Stations => stations;
        public bool ExternalPresentation { get; set; }
        // Raised when the player confirms a station with F; the village screens open the matching workbench.
        public event Action<Station> StationRequested;
        // F keycap bobbing over the station F would open, like the chapter exit's.
        public KeycapMarker StationKey { get; private set; }
        readonly List<Station> stations = new List<Station>();
        // Every drawn piece knows its colour and sketch source and the unlock building (region) whose ownership colours
        // it; pieces without a region stay sketch outside previews.
        sealed class Piece
        {
            public SpriteRenderer renderer; public string color, sketch, region; public Vector2 pivot; public Sprite colorSprite, sketchSprite; public Vector2 rowHeights; public int row, column; public float top; public bool keepTransform;
            // Erasure bookkeeping: the state last drawn (-1 unknown, 0 colour, 1 sketch) and the piece's own front offset.
            public int erased = -1; public float erasureOffset = float.NaN;
            // The colour twin the palette-piece blots reveal over the sketched piece (StartingVillageWorld.ColourBlots).
            public SpriteRenderer blotTwin;
            // The piece's own material while the erasure draws it through the erase shader (StartingVillageWorld.EraseColour).
            public Material plainMaterial;
        }
        readonly List<Piece> paired = new List<Piece>();
        readonly Dictionary<string, Vector2> regionSpans = new Dictionary<string, Vector2>();
        readonly List<Sprite> made = new List<Sprite>();
        SpriteRenderer sky;
        readonly List<SpriteRenderer> village = new List<SpriteRenderer>();
        float villageWidth;
        public float GroundWidth { get; private set; }
        ActorRenderInterpolation interpolation;

        void Start() { World = GetComponent<ForestCombatWorld>(); }

        // The village's scene clock: real time that stands still while the lobby's simulation is paused (the pause menu, a
        // screen, a guide card), so the well's pour, the colour bloom, the artisan's leaving and the auto bubbles wait too
        // (playtest 2026-11-13: they ran on behind the pause menu).
        float scenePausedFor;
        public bool ScenePaused => Ready && World.arena.Controls.Current.SimulationPaused;
        public float SceneTime => Time.unscaledTime - scenePausedFor;
        public float SceneDelta => ScenePaused ? 0 : Time.unscaledDeltaTime;

        void Update()
        {
            if (!Ready) { TryBuild(); return; }
            if (ScenePaused) scenePausedFor += Time.unscaledDeltaTime;
            var player = World.arena.Player;
            World.CameraRightPixels = CameraRightUnits(Sky, GroundWidth, SkyRunActive, SkyCleared, player.Position.y - GroundTop) * 32;
            // The lobby has no combat: weapons stay unready even after a reset re-arms them.
            for (int slot = 0; slot < 2; slot++) player.SetWeaponSlotReady(slot, false);
            if (Application.isEditor && Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
            { if (Preview == null) SetSketch(false); else if (Preview == false) SetSketch(true); else ClearPreview(); }
            RefreshFromProfile();
            TickSpreads();
            TickOpeningScene();
            TickVillageScenes(); TickBlots(); TickSkyArt();
            if (InteractionAllowed) PlayerControls.TryPerform(player, GameAction.Interact, TryInteract);
            var near = InteractionAllowed ? NearestStation : null;
            // A station the opening's guide points at keeps its keycap from afar (the outline still needs the hero near).
            var keyed = near ?? (!string.IsNullOrEmpty(MarkedStation) && !StationsSuppressed ? Find(MarkedStation) : null);
            // The sky path when no station is near (StartingVillageWorld.SkyParkour): the farmhouse door, the summit board and
            // the keeper show the F keycap like a station (playtest 2026-11-06: the door's faint white band went unnoticed).
            Vector2 skyKeyAt = default;
            SkyNear = near == null && InteractionAllowed ? FindSkyTarget(out skyKeyAt) : SkyTarget.None;
            bool skyKeyed = keyed == null && SkyNear != SkyTarget.None;
            StationKey.SetVisible(keyed != null || skyKeyed);
            RefreshInteractionOutline(near);
            // Above the station's name card (the GUI draws it CardLift above the art).
            if (keyed != null) StationKey.MoveTo(new Vector2(keyed.Position.x, keyed.Top + CardLift));
            else if (skyKeyed) StationKey.MoveTo(skyKeyAt);
        }

        // The camera's right edge in units: the village ground's own width, or the sky path's wider one while the hero is on
        // the sky path (a run under way, a cleared summit not yet left, or standing as high as the sky's lowest stone, as the
        // farmhouse door's start stone before the run's first step). An ordinary jump in the village (the double jump climbs
        // about 6 units) never reaches the sky's lowest stone (17 units), so it keeps the village's edge: right of the artisan
        // hub the view stays clamped to the village's right edge instead of sliding toward the sky's 84 units in mid-air
        // (playtest 2026-11-15, a jump by the departure sign pushed the screen sideways).
        public static float CameraRightUnits(SkyData sky, float groundWidth, bool skyRunActive, bool skyCleared, float heroHeight)
        {
            if (sky == null || sky.platforms == null || sky.platforms.Length == 0) return groundWidth;
            if (skyRunActive || skyCleared) return sky.worldWidth;
            float lowestStone = float.PositiveInfinity;
            foreach (var platform in sky.platforms) lowestStone = Mathf.Min(lowestStone, platform.y);
            return heroHeight >= lowestStone - SkyCameraReach ? sky.worldWidth : groundWidth;
        }
        // How far below the sky's lowest stone the sky camera already applies (the hero stands about .05 above a top).
        const float SkyCameraReach = 1;

        void LateUpdate()
        {
            if (!Ready || !World.View) return;
            TickApprovedBackground();
            TickSkyBackdrop();
        }

        public bool InteractionAllowed
        {
            get
            {
                if (!Ready || Departing || ChapterSceneFade.Busy) return false;
                var player = World.arena.Player; var controls = World.arena.Controls.Current;
                return player && !player.Dead && !player.InputBlocked && !controls.SimulationPaused && !controls.Blocks(InputChannels.Interaction) && Time.timeScale > 0;
            }
        }

        // Height above a station's art where the F keycap floats, clear of its name card.
        public const float CardLift = 34 / 32f;
        // Where the station name cards and the F keycap draw: over every piece of scenery that can stand behind them
        // (stations -12 and back), under every character (opening residents -10, the artisan, the player). The ground
        // (-5, -4) sits higher but never reaches up to a card.
        public const int MarkerOrder = -11;

        // The well is not used before the first departure (as the HTML prototype): while the opening walks the player through
        // the desk, the table and the sign, its departure shortcut stays out of reach (no F keycap, outline or bubble).
        public bool WellResting { get; set; }
        // The well takes the player's F only to pour a carried village ink; otherwise it has no keycap, outline or bubble
        // (playtest 2026-11-13). The village GUI says when an ink waits to be poured.
        public bool WellHasInk { get; set; }

        // A workbench whose building waits for more village palette pieces has no interaction at all.
        // The shared counter stays accessible; individual services retain their progression gates.
        public bool StationOpen(Station station) => station != null && station.Id != HeldClosed
            && (station.Id != ReturnWell || (boundService != null && boundService.PalettePieces > 0 && boundSnapshot.HasSeen("artisan-visit-1")))
            && (station.Id != ServiceHub || boundSnapshot?.ArtisanWorkshopOpen == true)
            && (station.Id == ServiceHub || boundService == null || boundService.StationOpen(station.Id));

        // Nearest open station whose reach covers the player's feet; null when none is close.
        public Station NearestStation
        {
            get
            {
                if (!Ready || StationsSuppressed) return null;
                var feet = World.arena.Player.Position; Station best = null; float distance = float.PositiveInfinity;
                foreach (var station in stations)
                {
                    if (!StationOpen(station) || station.Id == ReturnWell && (WellResting || !WellHasInk)) continue;
                    float dx = Mathf.Abs(feet.x - station.Position.x);
                    if (dx > station.Reach || Mathf.Abs(feet.y - station.Position.y) > 2.5f || dx >= distance) continue;
                    best = station; distance = dx;
                }
                return best;
            }
        }

        // Re-checks the target at confirmation time, then hands it to whoever owns the village screens.
        public bool TryInteract()
        {
            if (!InteractionAllowed) return false;
            var station = NearestStation;
            if (station == null) return TrySkyInteract();
            if (StationRequested == null) return false;
            StationRequested(station);
            return true;
        }

        // Save/progression ids remain stable; old scripted service targets share the single physical counter.
        public static string PhysicalStationId(string id) => id == PaletteBench || id == SupplyCounter || id == ReclaimBench ? ServiceHub
            : id == DepartureTable ? DepartureSign : id;
        public Station Find(string id)
        {
            var physical = stations.Find(s => s.Id == PhysicalStationId(id));
            if (physical == null || physical.Id == id) return physical;
            return new Station(id, physical.Name, physical.Position, physical.Reach)
                { Label = physical.Label, Top = physical.Top, Art = physical.Art };
        }

        void TryBuild()
        {
            if (!World || !World.Ready) return;
            var layoutText = Resources.Load<TextAsset>("HubLayout");
            if (!layoutText) throw new InvalidOperationException("HubLayout resource is missing; run tools/assets/build_hub_bindings.py.");
            var layout = JsonUtility.FromJson<Layout>(layoutText.text);
            GroundWidth = layout.worldWidthUnits;
            var arena = World.arena;
            World.ExternalPresentation = true;
            interpolation = arena.Player.GetComponent<ActorRenderInterpolation>();
            if (!interpolation) interpolation = arena.Player.gameObject.AddComponent<ActorRenderInterpolation>();
            interpolation.Bind(arena.Player);
            World.CameraPresentationPosition = interpolation.RenderPosition;

            BuildApprovedBackground();
            var catalog = Chapter1ProfileHost.Service.Catalog;
            var unlockBuildings = new HashSet<string>(catalog.Unlocks.Select(u => u.building));
            // Unlock buildings and buildings with a colorWhen rule each own a colour region (building, workbench, ground).
            var regionBuildings = new HashSet<string>(catalog.Buildings.Where(b => unlockBuildings.Contains(b.id) || !string.IsNullOrEmpty(b.colorWhen)).Select(b => b.id));
            var stationRegion = catalog.Buildings.Where(b => regionBuildings.Contains(b.id)).ToDictionary(b => b.station, b => b.id);
            foreach (var b in layout.buildings)
            {
                if (b.id == "record_farmhouse")
                {
                    // The record farmhouse has its own pixel art in the village (house_balcony stays for the combat
                    // backgrounds and the loading view); its canvas bottom is its contact line.
                    var house = Textured("Record farmhouse", "Hub/buildings/color/record-farmhouse", "Hub/buildings/sketch/record-farmhouse", new Vector2(.5f, 0), -30, regionBuildings.Contains(b.id) ? b.id : null);
                    house.transform.position = new Vector3(b.x, GroundTop+b.y, 0);
                    recordHouse = house;
                    continue;
                }
                string folder = b.id == "departure_gate" ? "props" : "buildings";
                Place(b.id, folder, b.x, b.id == "departure_gate" ? -15 : -30, regionBuildings.Contains(b.id) ? b.id : null, b.y);
            }
            foreach (var s in layout.stations)
            {
                // The departure sign stands on the lane; it has no building entry of its own.
                string region = stationRegion.TryGetValue(s.id, out var owner) ? owner : s.id == DepartureSign && regionBuildings.Contains(DepartureLane) ? DepartureLane : null;
                if (s.id == ServiceHub) region = "palette_atelier";
                var art = Place(s.id == ServiceHub ? PaletteBench : s.id, "props", s.x, -12, region, s.y);
                stations.Add(new Station(s.id, Names.TryGetValue(s.id, out var name) ? name : s.id, new Vector2(s.x, GroundTop), StationReach)
                { Art = art, Top = art ? KeycapMarker.OpaqueTop(art) : GroundTop + 1.5f, Label = string.IsNullOrEmpty(s.label) ? s.id : s.label });
            }
            // The departure lane runs from its preparation table past the sign: the walls and handcart along it belong to it.
            float laneFrom = layout.stations.Where(s => stationRegion.TryGetValue(s.id, out var r) && r == DepartureLane).Select(s => s.x).DefaultIfEmpty(float.PositiveInfinity).Min();
            foreach(var d in layout.decorations ?? Array.Empty<Placement>()) Place(d.id,"props",d.x,-14,regionBuildings.Contains(DepartureLane) && d.x >= laneFrom ? DepartureLane : null,d.y);
            // A region spans its building and workbench; the ground under that span belongs to it.
            foreach (var piece in paired)
                if (piece.region != null && piece.renderer.sprite)
                {
                    var bounds = piece.renderer.bounds;
                    regionSpans[piece.region] = regionSpans.TryGetValue(piece.region, out var span)
                        ? new Vector2(Mathf.Min(span.x, bounds.min.x), Mathf.Max(span.y, bounds.max.x)) : new Vector2(bounds.min.x, bounds.max.x);
                }
            // The lane continues out of the village to the right edge of the ground.
            if (regionSpans.TryGetValue(DepartureLane, out var lane)) regionSpans[DepartureLane] = new Vector2(lane.x, Mathf.CeilToInt(layout.worldWidthUnits) + 12);
            // Keep the original ground below the wider sky course for safe landings. The low boundary and camera
            // confine ordinary village travel to the compact four-station layout.
            BuildGround(Mathf.Max(layout.worldWidthUnits, World.Data.width / 32f));
            var rightLimit = World.transform.Find("Right world limit");
            if (rightLimit)
            {
                rightLimit.position = new Vector3(GroundWidth + .5f, GroundTop - 2, 0);
                var box = rightLimit.GetComponent<BoxCollider2D>();
                if (box) box.size = new Vector2(1, 28);
                Physics2D.SyncTransforms();
                World.RefreshSurfaceIndex();
            }
            World.CameraRightPixels = GroundWidth * 32;
            RefreshFromProfile(true);
            StationKey = KeycapMarker.Create(transform, 'F', new Vector2(0, GroundTop), arena.spriteMaterial, MarkerOrder, PlayerControls.For(arena.Player));
            StationKey.SetVisible(false);
            arena.Player.Position = new Vector2(layout.spawnX, GroundTop);
            arena.Player.velocity = Vector2.zero; Physics2D.SyncTransforms(); arena.Player.grounded = arena.Player.ProbeGround();
            World.RefreshCamera(0, true);
            BuildBlots(layout.colourBlots);
            BuildHeroBodies();
            BuildSky();
            Ready = true;
        }

        // The chapter 1 ground: the pixel lawn tiles (StartingVillageWorld.OpeningGround), one paired piece per cell so
        // each building region can switch on its own, from 12 units left of the village to 12 right of its end.
        void BuildGround(float width) => GroundRun(-12, Mathf.CeilToInt(width) + 12);

        // Ground cells and their decor from `left` to `right` (whole units); returns what it drew.
        List<SpriteRenderer> GroundRun(int left, int right, bool openLeft = false, bool openRight = false)
        {
            var drawn = new List<SpriteRenderer>();
            int before = paired.Count;
            GroundColumns(left, right, openLeft, openRight, -5);
            GroundDecor(left, right);
            for (int i = before; i < paired.Count; i++) drawn.Add(paired[i].renderer);
            return drawn;
        }

        // Every piece under a building or workbench belongs to that region (the larger overlap wins), so nothing
        // coloured stands on sketch ground.
        string RegionOver(float from, float to)
        {
            string region = null; float best = 0;
            foreach (var pair in regionSpans)
            {
                float overlap = Mathf.Min(to, pair.Value.y) - Mathf.Max(from, pair.Value.x);
                if (overlap > best) { best = overlap; region = pair.Key; }
            }
            return region;
        }

        SpriteRenderer Place(string id, string folder, float x, int order, string region, float y = 0)
        {
            string file = id.Replace('_', '-');
            string color = "Hub/" + folder + "/color/" + file, sketch = "Hub/" + folder + "/sketch/" + file;
            var sprite = Resources.Load<Sprite>(color);
            if (!sprite) { Debug.LogWarning("Starting village art is missing: " + color + " (run tools/assets/update_assets.py for the pinned selection)."); return null; }
            var r = Renderer(id, sprite, order);
            r.transform.position = new Vector3(x, GroundTop+y, 0);
            paired.Add(new Piece { renderer = r, color = color, sketch = sketch, pivot = sprite.pivot / sprite.rect.size, region = region });
            return r;
        }

        // Preview the whole village in one state (Editor K, review captures); ClearPreview returns to the unlocks.
        public void SetSketch(bool sketch) { Preview = sketch; Apply(); }
        public void ClearPreview() { Preview = null; Apply(); }

        // A region whose unlock was just bought keeps its sketch until its colour-spread overlay has finished.
        public bool IsColoured(string region) => region != null && coloured.Contains(region) && !spreading.ContainsKey(region);
        public bool Spreading(string region) => spreading.ContainsKey(region);
        readonly Dictionary<string, (SpriteRenderer overlay, SpriteFlipbook clip)> spreading = new Dictionary<string, (SpriteRenderer, SpriteFlipbook)>();

        // Newly coloured building (purchase during play): the 8-frame colour-spread mask (Hub/effects/color-spread/<id>,
        // clipped to the colour building's own pixels, last frame equals the colour art) plays over the sketch once the
        // lobby is running again (screens closed), then the region switches to colour. Rules and saving never wait on it.
        void BeginSpread(string region)
        {
            if (spreading.ContainsKey(region)) return;
            var building = paired.Find(p => p.region == region && p.renderer && p.renderer.name == region)?.renderer;
            var clip = new SpriteFlipbook("Hub/effects/color-spread/" + region.Replace('_', '-'));
            if (!building || !clip.Ready) return;
            var overlay = Renderer(region + " colour spread", null, building.sortingOrder + 1);
            overlay.transform.position = building.transform.position;
            spreading[region] = (overlay, clip);
        }

        void TickSpreads()
        {
            if (spreading.Count == 0) return;
            bool paused = World.arena.Controls.Current.SimulationPaused;
            foreach (var region in new List<string>(spreading.Keys))
            {
                var (overlay, clip) = spreading[region];
                if (!clip.Started) { if (paused) continue; clip.Play(); }
                if (clip.Playing) { overlay.sprite = clip.Current; continue; }
                if (overlay) Destroy(overlay.gameObject);
                spreading.Remove(region); Apply();
            }
        }

        // Rebinds to the profile when it changes (purchase, departure, record reset) or a different profile service is
        // installed. Regions are never coloured from it now; the region colouring and its spread stay for previews.
        void RefreshFromProfile(bool force = false)
        {
            var service = Chapter1ProfileHost.Service;
            if (!force && service == boundService && service.Snapshot == boundSnapshot) return;
            bool live = !force && service == boundService;
            var before = new HashSet<string>(coloured);
            boundService = service; boundSnapshot = service.Snapshot;
            // The lobby uses the live permanent unlocks; chapters keep their
            // departure snapshot. Setting capacity does not refill spent charges.
            World.arena.Player.DashCharges = Math.Max(ProgressionIds.BaseDashCharges,
                service.Catalog.Highest(ProgressionIds.DashCharges, boundSnapshot.Has, effect => effect.value));
            coloured.Clear();
            // No region colours from the profile any more (user decision 2026-11-04): the village stays sketch and only
            // the village palette pieces' blots bring colour back (ColourBlots). Unlocks, the first boss clear and
            // departures colour no building.
            foreach (var region in new List<string>(spreading.Keys))
                if (!coloured.Contains(region)) { if (spreading[region].overlay) Destroy(spreading[region].overlay.gameObject); spreading.Remove(region); }
            if (live) foreach (var region in coloured) if (!before.Contains(region)) BeginSpread(region);
            Apply();
        }

        void Apply()
        {
            foreach (var piece in paired)
            {
                if (!piece.renderer) continue;
                bool sketch = ErasedOverride(piece) ?? Preview ?? !IsColoured(piece.region);
                piece.erased = sketch ? 1 : 0;
                ApplyPiece(piece, sketch);
            }
        }

        void ApplyPiece(Piece piece, bool sketch)
        {
            if (piece.colorSprite)
            {
                // Ground cells: both states stand at the cell's row below the walking line.
                piece.renderer.sprite = sketch ? piece.sketchSprite : piece.colorSprite;
                if (!piece.keepTransform)
                {
                    float height = (sketch ? piece.rowHeights.x : piece.rowHeights.y) / 32f;
                    piece.renderer.transform.position = new Vector3(piece.column, GroundTop + piece.top - piece.row * height, 0);
                }
            }
            else piece.renderer.sprite = (sketch ? piece.sketchSprite ?? Load(piece.sketch, piece.pivot) :
                Load(piece.color, piece.pivot)) ?? piece.renderer.sprite;
            ApplyBlotTwin(piece, sketch);
        }

        // Imported hub sprites keep their own pivot; plain village textures are wrapped at 32 PPU with the given pivot.
        readonly Dictionary<string, Sprite> wrapped = new Dictionary<string, Sprite>();
        Sprite Load(string path, Vector2 pivot)
        {
            string key = path + "@" + pivot;
            if (wrapped.TryGetValue(key, out var sprite)) return sprite;
            sprite = path.StartsWith("Hub/", StringComparison.Ordinal) ? Resources.Load<Sprite>(path) : null;
            if (sprite) return sprite;
            var texture = Resources.Load<Texture2D>(path);
            return texture ? wrapped[key] = Make(texture, pivot) : null;
        }

        SpriteRenderer Textured(string name, string color, string sketch, Vector2 pivot, int order, string region = null)
        {
            var r = Renderer(name, Load(color, pivot), order);
            paired.Add(new Piece { renderer = r, color = color, sketch = sketch, pivot = pivot, region = region });
            return r;
        }

        SpriteRenderer Renderer(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>(); r.sprite = sprite; r.sortingOrder = order;
            r.sharedMaterial = World.arena.spriteMaterial;
            return r;
        }

        Sprite Make(Texture2D texture, Vector2 pivot)
        {
            texture.filterMode = FilterMode.Point;
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, 32, 0, SpriteMeshType.FullRect);
            made.Add(sprite); return sprite;
        }

        void OnDestroy()
        {
            DisposeDeparture(); StopScriptedWalk(); DisposeSky();
            if (interactionOutlineMaterial) Destroy(interactionOutlineMaterial);
            if (World && interpolation && World.CameraPresentationPosition == (Func<Vector2>)interpolation.RenderPosition) World.CameraPresentationPosition = null;
            foreach (var sprite in made) if (sprite) Destroy(sprite);
        }
    }
}
