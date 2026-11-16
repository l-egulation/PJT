using System;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Definitions;
using Deviant.Game.Diagnostics;
using Deviant.Integration;
using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

namespace ForestCombatV2
{
    [Serializable] public class Point { public float x,y; }
    [Serializable] public class GroundRect { public int x,y,w,h; }
    [Serializable] public class FoliagePlatform { public int x,y,w; public bool earthArtwork,blockDropThrough,vanishing; }
    [Serializable] public class VisualCell { public int x,y,id; public float offsetY; }
    [Serializable] public class ForestLayer { public string id; public float parallax,vertical,phase,baseCrop,minVertical,maxVertical,drift,opacity,contentBottom,bottomMargin; public int width,height,order; public bool single,coverBottom,repeatUnmirrored; }
    [Serializable] public class ForestView { public string name; public float x,y,a,b; }
    [Serializable] public class MonsterKind { public string id,name; public int width,height,baseline,bodyWidth,bodyHeight; public float bodyOffset; public bool ranged; }
    [Serializable] public class MonsterPlacement { public int kind,zone,roamSeed; public float x,y; public bool roaming; }
    [Serializable] public class ForestLandmark { public string kind,name; public float x,y; public int zone; }
    [Serializable] public class ForestEncounterTuning { public int minimumPerCombatZone; public float roamRadius,roamSpeedScale; public float[] waitSeconds,walkSeconds; }
    [Serializable] public class RouteNode { public int id; public float a,b,y; }
    [Serializable] public class RouteEdge { public int from,to; public float start,speed,second; }
    [Serializable] public class ForestRoom { public int id,sourceZone,x,y,w,h; public string kind,name,entrySide,exitSide; public int entryY,exitY; public bool mainPath,mirrored; public Chapter1RoomDecoration[] authoredDecorations; }
    [Serializable] public class ForestPassage { public int from,to,x,y,w,h; public bool fromEntry; public GroundRect[] spaces; }
    [Serializable] public class ForestData
    {
        public int width,height,cols,rows,atlasColumns,uniqueTiles;
        public Point spawn; public int[] ground; public GroundRect[] rects;
        public FoliagePlatform[] platforms; public VisualCell[] cells;
        public ForestLayer[] layers; public ForestView[] views;
        public MonsterKind[] monsters; public MonsterPlacement[] actors;
        public RouteNode[] nodes; public RouteEdge[] route;
        public ForestLandmark[] landmarks; public ForestEncounterTuning encounters;
        public ForestRoom[] rooms; public ForestPassage[] passages;
        public GroundRect[] passageSpaces;
        public VisualCell[] renderGround,renderPlatforms;
        public int generationSeed;
    }

    // Scene composition owns authored map content, camera and the explicit 120Hz scene policy.
    [DefaultExecutionOrder(500)]
    public sealed class ForestCombatWorld : MonoBehaviour
    {
        // Chapter 1 sketch backgrounds share the player's cream/ink palette; the outline keeps the player readable.
        public const string PlayerOutlineMaterial = "Chapter1PlayerOutline/PlayerOutline";
        public CombatArena arena;
        public TextAsset worldJson;
        public Texture2D terrainAtlas;
        public TextAsset roundedTerrainJson;
        public Texture2D roundedTerrainAtlas;
        public Sprite[] backdropSprites,monsterSprites;
        public Chapter1MonsterCatalog monsterAnimations;
        public Chapter1AttackEffectCatalog monsterAttackEffects;
        public Camera sceneCamera;
        [Tooltip("Raise the follow camera in authored pixels (32 pixels = 1 world unit).")]
        [UnityEngine.Serialization.FormerlySerializedAs("cameraVerticalOffset")]
        public float cameraVerticalOffsetPixels;
        // Vertical follow stiffness (critically damped omega, formerly 22). 10 lets stairs and new landings ease the view over
        // about half a second instead of snapping onto every height change; boss-room fixed views keep 22.
        public float cameraVerticalOmega = 10f;
        // Keep an ordinary jump inside the last grounded framing; follow real height transitions.
        public float cameraDeadzoneUpPixels = 112, cameraDeadzoneDownPixels = 48;
        [NonSerialized] public Func<Vector2> CameraPresentationPosition;
        public Vector2? FixedCameraCenter { get; set; }
        // A close-up (the opening's well): the view shrinks by CameraZoom about CameraZoomCenter, never leaving the
        // usual framing (as the HTML prototype crops its screen). 1 is the usual view.
        public float CameraZoom { get; set; } = 1;
        public Vector2 CameraZoomCenter { get; set; }
        // Horizontal follow stiffness of the camera (the village eases it down for its scenes).
        public float CameraFollowOmega { get; set; } = 12.5f;
        // Short decaying camera shake for heavy impacts; it offsets the view only, never the follow state.
        float shakeAmplitude,shakeLeft,shakeDuration;
        public void ShakeCamera(float amplitude,float duration)
        {if(amplitude*Mathf.Max(0,duration)<shakeAmplitude*shakeLeft)return;shakeAmplitude=amplitude;shakeLeft=shakeDuration=Mathf.Max(.01f,duration);}
        public CombatDiagnostics diagnostics;
        public Material spriteMaterial;
        public Sprite playerSprite;
        public int fixedStepsPerSecond = 120;
        public ForestData Data { get; private set; }
        public Camera View { get; private set; }
        public Tilemap Visuals { get; private set; }
        // Map, arena, player and camera exist; the chapter devices of Start are still being added (Composed).
        public bool Prepared { get; private set; }
        // Every device of Start is in place; a staged presentation on this object may still be building (Ready).
        public bool Composed { get; private set; }
        // The world is complete and the run may start. In a random chapter this waits for Composed and every
        // IStagedWorldPresentation; elsewhere it comes with Prepared, as before chapters were composed over frames.
        public bool Ready { get; private set; }
        // Random chapter stages compose under the loading screen over several frames (FrameSlice).
        public bool ComposesInFrames => proceduralRooms && !dummyTrainingOnly && !villageHub;
        public bool enablePlaytestWorkbench;
        public bool enablePlaytestBasicSpeeds;
        public bool proceduralRooms;
        public bool clockBossTestOnly;
        public bool dummyTrainingOnly;
        // The starting village: a safe flat lobby with no map, portals, resets or enemies.
        public bool villageHub;
        public Sprite clockBossSprite;
        public bool useTeamSketchTerrain = true;
        [Tooltip("Negative: fresh random run. Non-negative: reproducible test seed.")]
        public int proceduralSeed = -1;
        // Sketch-weapon runs (docs/design/equipment/sketch-weapons.md). Tests of the legacy floor-loot, bag-book and
        // ink-stall path turn it off before entering play mode.
        public bool sketchWeapons = true;
        public PlaytestEnvironment TestEnvironment { get; private set; }
        public Chapter1FullMap FullMap { get; private set; }
        public float CameraX { get; private set; }
        public float CameraY { get; private set; }
        public int SurfaceCount { get; private set; }
        readonly List<ArenaSurface> terrainSurfaces = new List<ArenaSurface>();
        // Platform index and collider of every vanishing deck, filled by BuildGeometry.
        public readonly List<(int index,BoxCollider2D collider)> VanishingSurfaces=new List<(int,BoxCollider2D)>();
        StaticArenaSurfaceIndex surfaceIndex;
        public void RefreshSurfaceIndex() => surfaceIndex?.Rebuild(terrainSurfaces);
        readonly List<SpriteRenderer[]> backdropRows=new List<SpriteRenderer[]>();
        readonly List<Sprite> madeSprites=new List<Sprite>();
        readonly List<Tile> madeTiles=new List<Tile>();
        float cameraVX,cameraVY,previousFixedDelta,cameraAnchorY=float.NaN;
        CameraOutputBinding cameraOutput;
        CameraOutputBinding.BaseSource cameraFollow;
        IDisposable engineTime;
        int nextEnemy;
        IChapter1MonsterContent monsterContent;
        bool destinations;

#if UNITY_EDITOR
        public static event Action<ForestCombatWorld> PreparingEditorPlay;
        void Awake() => PreparingEditorPlay?.Invoke(this);
#endif

        System.Collections.IEnumerator Start()
        {
            if (!arena || !sceneCamera || !diagnostics || !worldJson || !terrainAtlas)
                throw new InvalidOperationException("Forest scene requires explicit combat, camera, diagnostics and supplied forest data.");
            Data = JsonUtility.FromJson<ForestData>(worldJson.text);
            if(proceduralRooms)
            {
                if(!roundedTerrainJson || !roundedTerrainAtlas)
                    throw new InvalidOperationException("Procedural Chapter1 requires the approved rounded terrain assets.");
                var layout=JsonUtility.FromJson<RoundedTerrainLayout>(roundedTerrainJson.text);
                Data=Chapter1ProceduralMap.Generate(Data,layout,proceduralSeed>=0?proceduralSeed:Chapter1ProceduralMap.NextSeed());
            }
            if(arena)EnemyRandom.SetSeed(arena,Data.generationSeed);
            bool staged = ComposesInFrames;
            using (var preparing = staged ? arena.Controls.OpenScope("chapter composition") : null)
            {
                if (preparing != null)
                {
                    preparing.BlockInput(Deviant.Control.Contracts.InputChannels.Combat | Deviant.Control.Contracts.InputChannels.Interaction);
                    preparing.PauseSimulation();
                }
                if (staged) yield return null;
                var monsterSettings=GetComponent<Chapter1MonsterTestSettings>();
                if(monsterSettings)Data.actors=monsterSettings.BuildPlacements(Data);
                monsterContent=dummyTrainingOnly?null:GetComponent<IChapter1MonsterContent>();
                monsterContent?.Prepare(Data);
                if(clockBossTestOnly && proceduralRooms)
                {
                    var room=Array.Find(Data.rooms,r=>r.kind=="boss");
                    Data.actors=Array.Empty<MonsterPlacement>();
                    // Start in the corridor outside the room so the player walks in and triggers the entry.
                    Data.spawn=new Point{x=(room.x-4)*32,y=(room.y+15)*32};
                }
                if (fixedStepsPerSecond <= 0) throw new InvalidOperationException("Forest scene requires an explicit positive fixed-step rate.");
                CombatRuntimeConfiguration content = null;
                if (staged) yield return CombatContentLoader.LoadInFrames(arena.characterAnimations, arena.loadCharacterArt, loaded => content = loaded);
                else content = CombatContentLoader.Load(arena.characterAnimations, arena.loadCharacterArt);
                content.Profile = CombatCompositionProfile.Game;
                content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
                content.Camera = sceneCamera; content.SpriteMaterial = spriteMaterial; content.PlayerSprite = playerSprite;
                content.PlayerMaterial = Resources.Load<Material>(PlayerOutlineMaterial);
                content.PlayerSpawn = new Vector2(Data.spawn.x / 32, -Data.spawn.y / 32);
                content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = new Vector2(.1875f, 1); content.PlayerSortingOrder = 10;
                int bomb = Array.FindIndex(content.Catalog.forms, form => form.id == "bomb");
                content.SecondaryFormIndex = bomb >= 0 ? bomb : 1;
                content.Enemies = EnemyRequests(); content.EnemyFactory = CreateEnemy;
                content.PrepareEnvironment = owner => { BuildGeometry(); BuildVisuals(); BuildBackdrops(); };
                content.BindPlayerInput = player => {
                    var input = player.gameObject.AddComponent<CombatKeyboardInput>();
                    // F5 is the team's test reset: Editor and Development Builds only (user decision 2026-10-30).
                    input.Player = player; input.ResetKeyEnabled = !dummyTrainingOnly && !villageHub && DevFeatures.Enabled;
                };
                if (staged)
                {
                    yield return arena.InitializeInFrames(content);
                    if (arena.CompositionProfile == CombatCompositionProfile.Unconfigured) { enabled = false; yield break; }
                }
                else if (!arena.Initialize(content)) { enabled = false; yield break; }
                arena.Player.gameObject.AddComponent<Chapter1PlayerHitFeedback>().player = arena.Player;
                diagnostics.arena = arena;
                View = sceneCamera;
                if (!FindAnyObjectByType<AudioListener>()) View.gameObject.AddComponent<AudioListener>();
                cameraOutput = CameraOutputBinding.GetOrCreate(View);
                View.orthographic = true; View.orthographicSize = 540f / 64;
                View.backgroundColor = new Color32(85, 75, 58, 255);
                // Physics runs at a fixed 120 Hz; without this the body steps unevenly on other display rates, most visibly
                // in a jump while the camera holds its height. The village binds its own and owns its camera source.
                if (proceduralRooms && !villageHub && !dummyTrainingOnly)
                {
                    var smoothing = arena.Player.GetComponent<ActorRenderInterpolation>();
                    if (!smoothing) smoothing = arena.Player.gameObject.AddComponent<ActorRenderInterpolation>();
                    smoothing.Bind(arena.Player); CameraPresentationPosition = smoothing.RenderPosition;
                }
                Prepared = true; if (!staged) Ready = true;
                OpenScenePolicy(); RefreshCamera(0, true);
                // A random chapter adds its devices over frames; each one still initializes whole.
                var slice = staged ? new FrameSlice() : null;
                if(!dummyTrainingOnly&&!villageHub){FullMap=gameObject.AddComponent<Chapter1FullMap>();FullMap.Initialize(this);}
                if (slice != null && slice.Due) { yield return null; slice.Restart(); }
                if(enablePlaytestBasicSpeeds&&!enablePlaytestWorkbench)PlaytestEnvironment.ApplyBasicSpeedTrials(arena);
                if (enablePlaytestWorkbench)
                {
                    arena.uiFont = diagnostics.uiFont;
                    diagnostics.showDebugUI = false;
                    diagnostics.EquipmentTuning = arena.DrawWeaponTuning;
                    arena.TestEquipmentPanel = rect => { diagnostics.showPanel = true; diagnostics.DrawEquipmentPanel(rect); };
                    TestEnvironment = gameObject.AddComponent<PlaytestEnvironment>();
                    TestEnvironment.standaloneArena = arena; TestEnvironment.GroupSpawner = BuildTestGroups;
                    TestEnvironment.Initialize();
                }
                if(proceduralRooms&&!clockBossTestOnly)gameObject.AddComponent<Chapter1StartingBlueprints>().Initialize(this);
                if(proceduralRooms&&!clockBossTestOnly)gameObject.AddComponent<Chapter1MaintenanceFacility>().Initialize(this);
                if(VanishingSurfaces.Count>0)gameObject.AddComponent<Chapter1VanishingPlatforms>().Initialize(this);
                if (slice != null && slice.Due) { yield return null; slice.Restart(); }
                // The clock executioner is the boss of every random stage; the chapter encounter adopts it for the exit.
                if(proceduralRooms&&!dummyTrainingOnly)gameObject.AddComponent<ClockExecutionerEncounter>().Initialize(this,clockBossSprite);
                if (slice != null && slice.Due) { yield return null; slice.Restart(); }
                if(proceduralRooms&&!clockBossTestOnly)gameObject.AddComponent<Chapter1BossEncounter>().Initialize(this);
                if (slice != null && slice.Due) { yield return null; slice.Restart(); }
                if(!dummyTrainingOnly)
                {
                    gameObject.AddComponent<Chapter1TraversalPresentation>().Initialize(this);
                    if (slice != null && slice.Due) { yield return null; slice.Restart(); }
                    if(!villageHub)gameObject.AddComponent<Chapter1ManualCheckpoint>().Initialize(this);
                }
                if (slice != null && slice.Due) { yield return null; slice.Restart(); }
                if(proceduralRooms&&!dummyTrainingOnly)gameObject.AddComponent<Chapter1BossRoomEnclosure>().Initialize(this);
                if(dummyTrainingOnly)gameObject.AddComponent<DummyTrainingRoom>().Initialize(this);
                arena.Record("Chapter1 forest: arrow keys, Space, Shift, A/S basic 1/2, Q/W special 1/2, F interact, C/V checkpoint, F5 reset. F3: area list.");
                Composed = true;
                if (staged)
                {
                    // The chapter art builds over the next frames (VillagePresentation); the run starts on the finished
                    // picture, still paused under this scope and the loading screen.
                    while (PresentationPending()) yield return null;
                    Ready = true;
                }
            }
        }
        bool PresentationPending()
        {
            foreach (var presentation in GetComponents<IStagedWorldPresentation>())
                if (presentation is Behaviour behaviour && behaviour.isActiveAndEnabled && !presentation.Settled) return true;
            return false;
        }
        BoxCollider2D Surface(string label,Vector2 center,Vector2 size,bool oneWay)
        {
            var go=new GameObject(label); go.transform.SetParent(transform,false); go.transform.position=center;
            var surface=go.AddComponent<ArenaSurface>();surface.oneWay=oneWay;terrainSurfaces.Add(surface);
            surface.footstepMaterial = label == "Wood platform" ? FootstepMaterial.Wood :
                villageHub ? FootstepMaterial.Stone : proceduralRooms ? FootstepMaterial.Dirt : FootstepMaterial.Grass;
            var collider=go.AddComponent<BoxCollider2D>(); collider.size=size;arena.RegisterFeedbackSurface(collider); SurfaceCount++; return collider;
        }
        void BuildGeometry()
        {
            foreach(var r in Data.rects) Surface("Ground",new Vector2(r.x+r.w/2f,-r.y-r.h/2f),new Vector2(r.w,r.h),false);
            VanishingSurfaces.Clear();
            for(int i=0;i<Data.platforms.Length;i++)
            {
                var p=Data.platforms[i];
                const float thickness=.125f;
                var platform=Surface(p.earthArtwork?"Earth platform":"Wood platform",new Vector2((p.x+p.w/2f)/32,-p.y/32f-thickness/2),new Vector2(p.w/32f,thickness),true);
                platform.GetComponent<ArenaSurface>().blockDropThrough=p.blockDropThrough;
                if(p.vanishing){platform.gameObject.name="Vanishing platform "+i;VanishingSurfaces.Add((i,platform));}
            }
            float limitY=Data.height>2048?-Data.height/64f:-32,limitHeight=Data.height>2048?Data.height/32f+32:160;
            Surface("Left world limit",new Vector2(-.5f,limitY),new Vector2(1,limitHeight),false);
            Surface("Right world limit",new Vector2(Data.width/32f+.5f,limitY),new Vector2(1,limitHeight),false);
            RecoveryVolume("Forest recovery volume",new Vector2(Data.width/64f,-Data.height/32f-4),new Vector2(Data.width/32f+8,4));
            Physics2D.SyncTransforms();
            surfaceIndex=new StaticArenaSurfaceIndex(terrainSurfaces);
        }
        void RecoveryVolume(string label,Vector2 center,Vector2 size)
        {
            var go=new GameObject(label);go.transform.SetParent(transform,false);go.transform.position=center;
            var pit=go.AddComponent<BoxCollider2D>();pit.isTrigger=true;pit.size=size;go.AddComponent<ArenaPit>();arena.pits.Add(pit);
        }
        void BuildVisuals()
        {
            if(proceduralRooms && useTeamSketchTerrain)
            {
                Visuals=Chapter1TeamTerrain.Build(Data,Resources.Load<Chapter1TeamTerrainPalette>("Chapter1TeamSketch"),transform,arena.spriteMaterial,madeTiles);
                return;
            }
            if(roundedTerrainJson || roundedTerrainAtlas)
            {
                Visuals=Chapter1RoundedTerrain.Build(Data,roundedTerrainJson,roundedTerrainAtlas,transform,arena.spriteMaterial,madeSprites,madeTiles);
                return;
            }
            var grid=new GameObject("Native32 forest terrain",typeof(Grid)); grid.transform.SetParent(transform,false);
            var tiles=new GameObject("Approved terrain pixels",typeof(Tilemap),typeof(TilemapRenderer)); tiles.transform.SetParent(grid.transform,false);
            Visuals=tiles.GetComponent<Tilemap>(); Visuals.tileAnchor=Vector3.zero;
            var renderer=tiles.GetComponent<TilemapRenderer>(); renderer.sortingOrder=0; renderer.sharedMaterial=arena.spriteMaterial;
            var library=new Tile[Data.uniqueTiles];
            for(int i=0;i<library.Length;i++)
            {
                var sprite=Sprite.Create(terrainAtlas,new Rect(i%Data.atlasColumns*32,terrainAtlas.height-(i/Data.atlasColumns+1)*32,32,32),Vector2.zero,32,0,SpriteMeshType.FullRect);
                madeSprites.Add(sprite); var tile=ScriptableObject.CreateInstance<Tile>(); tile.sprite=sprite; tile.colliderType=Tile.ColliderType.None; library[i]=tile; madeTiles.Add(tile);
            }
            var positions=new Vector3Int[Data.cells.Length]; var values=new TileBase[Data.cells.Length];
            for(int i=0;i<positions.Length;i++){var c=Data.cells[i];positions[i]=new Vector3Int(c.x,-c.y-1,0);values[i]=library[c.id];}
            Visuals.SetTiles(positions,values); Visuals.CompressBounds();
        }
        void BuildBackdrops()
        {
            for(int j=0;j<Data.layers.Length;j++)
            {
                var layer=Data.layers[j];
                var list=new SpriteRenderer[layer.single?1:3];
                for(int i=0;i<list.Length;i++)
                {
                    var go=new GameObject(layer.id); go.transform.SetParent(transform,false);
                    var r=go.AddComponent<SpriteRenderer>(); r.sprite=backdropSprites[j];
                    r.sortingOrder=layer.order; r.sharedMaterial=arena.spriteMaterial;
                    r.transform.localScale=new Vector3(layer.width/r.sprite.rect.width,layer.height/r.sprite.rect.height,1);
                    r.color=new Color(1,1,1,layer.opacity>0?layer.opacity:1); list[i]=r;
                }
                backdropRows.Add(list);
            }
        }
        CombatEnemySpawn[] EnemyRequests()
        {
            var requests = new CombatEnemySpawn[Data.actors.Length];
            for (int i = 0; i < requests.Length; i++)
            {
                var placement = Data.actors[i]; var kind = Data.monsters[placement.kind];
                var definition = monsterContent!=null ? monsterContent.Definition(kind.id) : Chapter1EnemyDefinitions.Load(kind.id);
                requests[i] = new CombatEnemySpawn(kind.name, 0,
                    new Vector2(placement.x / 32, -placement.y / 32),
                    new Vector2(definition.bodySize[0], definition.bodySize[1]),
                    new Vector2(definition.bodyOffset[0], definition.bodyOffset[1]), definition.maxHP, false, true, 10);
            }
            return requests;
        }
        Chapter1RoomActivity roomActivity;
        TestEnemy CreateEnemy(CombatArena owner, CombatEnemySpawn request)
        {
            var placement = Data.actors[nextEnemy++]; var kind = Data.monsters[placement.kind];
            var enemy=CreateSpeciesEnemy(owner,request,placement.kind);
            // Scene policy also applies to enemies created through team content.
            if(proceduralRooms)
            {
                enemy.distantIdleRange=new Vector2(64,24);
                // Rooms awake: the player's room and the rooms its passages join (distance rule outside rooms).
                if(Data.rooms!=null&&Data.rooms.Length>0)
                {
                    if(roomActivity==null||!roomActivity.Uses(Data,owner))roomActivity=new Chapter1RoomActivity(Data,owner);
                    enemy.OutsideActiveArea=roomActivity.Outside;
                }
            }
            if(placement.roaming && Data.encounters!=null){
                var t=Data.encounters;enemy.ConfigureIdleRoaming(placement.roamSeed,t.roamRadius,t.roamSpeedScale,t.waitSeconds[0],t.waitSeconds[1],t.walkSeconds[0],t.walkSeconds[1]);
            }
            return enemy;
        }
        FieldMouseEnemy CreateSpeciesEnemy(CombatArena owner,CombatEnemySpawn request,int kindIndex)
        {
            var kind=Data.monsters[kindIndex];
            if(monsterContent!=null)return monsterContent.CreateEnemy(owner,request,kind,surfaceIndex);
            if (monsterAnimations && monsterAnimations.Find(kind.id, "move") == null)
                throw new InvalidOperationException("Monster is outside the approved animation catalog: " + kind.id);
            var go = new GameObject(request.Name); go.transform.SetParent(transform, false);
            var enemy = go.AddComponent<FieldMouseEnemy>();
            enemy.SurfaceIndex=surfaceIndex;
            enemy.InitializeSpecies(owner, request.Position, Chapter1EnemyDefinitions.Load(kind.id));
            enemy.profile = request.Profile; enemy.spawn = request.Position; enemy.maxHealth = request.MaxHealth; enemy.aggressive = request.Aggressive;
            var visual = new GameObject("Approved native sprite"); visual.transform.SetParent(go.transform, false);
            enemy.visual = visual.AddComponent<SpriteRenderer>(); enemy.visual.sprite = monsterSprites[kindIndex];
            enemy.visual.sharedMaterial = spriteMaterial; enemy.visual.sortingOrder = request.SortingOrder;
            enemy.ResetEnemy();
            enemy.gameObject.AddComponent<Chapter1AttackWarning>().actor = enemy;
            if (monsterAnimations)
            {
                var animation = go.AddComponent<Chapter1MonsterPresentation>();
                animation.actor = enemy; animation.catalog = monsterAnimations; animation.species = kind.id;
                if(monsterAttackEffects && monsterAttackEffects.Find(kind.id)!=null)
                {
                    animation.attackEffects=go.AddComponent<Chapter1AttackEffects>();
                    animation.attackEffects.catalog=monsterAttackEffects;
                }
            }
            return enemy;
        }
        public FieldMouseEnemy SpawnMonster(string species,Vector2 feet,bool aggressive=false,int facing=1)
        {
            // Prepared, not Ready: the boss and room devices spawn their monsters while the chapter is composed.
            if(!Prepared)throw new InvalidOperationException("Wait for the test world to initialize.");
            int index=Array.FindIndex(Data.monsters,m=>m.id==species);
            if(index<0 || monsterContent==null && (!monsterAnimations || monsterAnimations.Find(species,"move")==null))
                throw new ArgumentException("Monster is not in the scene's approved catalog.",nameof(species));
            var definition=monsterContent!=null ? monsterContent.Definition(species) : Chapter1EnemyDefinitions.Load(species);
            var request=new CombatEnemySpawn(Data.monsters[index].name,0,feet,
                new Vector2(definition.bodySize[0],definition.bodySize[1]),
                new Vector2(definition.bodyOffset[0],definition.bodyOffset[1]),definition.maxHP,false,aggressive,10);
            var enemy=CreateSpeciesEnemy(arena,request,index);enemy.SyncFacing(facing);
            arena.enemies.Add(enemy);Physics2D.SyncTransforms();return enemy;
        }
        public void RemoveMonster(TestEnemy enemy)
        {
            if(!enemy || !arena.enemies.Remove(enemy))return;
            enemy.CancelAttack();enemy.gameObject.SetActive(false);Destroy(enemy.gameObject);
        }
        void OpenScenePolicy()
        {
            if (!Prepared || cameraFollow != null) return;
            previousFixedDelta = Time.fixedDeltaTime;
            Time.fixedDeltaTime = 1f / fixedStepsPerSecond;
            try
            {
                engineTime = arena.Controls.BindEngineTime();
                cameraFollow = cameraOutput.BindBaseSource("chapter1 map follow");
            }
            catch { CloseScenePolicy(); throw; }
        }
        void CloseScenePolicy()
        {
            cameraFollow?.Dispose(); cameraFollow = null;
            engineTime?.Dispose(); engineTime = null;
            if (previousFixedDelta > 0) { Time.fixedDeltaTime = previousFixedDelta; previousFixedDelta = 0; }
        }
        void OnEnable() { if (Prepared) OpenScenePolicy(); }
        void OnDisable() { if(FullMap)FullMap.SetOpen(false);CloseScenePolicy(); }
        TestEnemy SpawnMonster(int kindIndex,Vector2 at)
        {
                var kind=Data.monsters[kindIndex];
                if(monsterContent!=null)return SpawnMonster(kind.id,at,true,1);
                if(monsterAnimations && monsterAnimations.Find(kind.id,"move")==null)throw new InvalidOperationException("Monster is outside the approved animation catalog: "+kind.id);
                var go=new GameObject(kind.name); go.transform.SetParent(transform,false);
                var e=go.AddComponent<TestEnemy>(); e.Configure(arena,at,new Vector2(kind.bodyWidth/32f,kind.bodyHeight/32f),new Vector2(kind.bodyOffset,kind.bodyHeight/64f),false);
                e.profile=kind.ranged?1:0; e.spawn=e.Position; e.maxHealth=e.profile==1?60:100; e.aggressive=true;
                var visual=new GameObject("Approved native sprite"); visual.transform.SetParent(go.transform,false);
                e.visual=visual.AddComponent<SpriteRenderer>(); e.visual.sprite=monsterSprites[kindIndex]; e.visual.sharedMaterial=arena.spriteMaterial; e.visual.sortingOrder=10;
                e.ResetEnemy(); arena.enemies.Add(e);
                if(monsterAnimations && monsterAnimations.Find(kind.id,"move")!=null)
                {
                    var animation=go.AddComponent<Chapter1MonsterPresentation>();
                    animation.actor=e;animation.catalog=monsterAnimations;animation.species=kind.id;
                }
                return e;
        }
        int BuildTestGroups(Deviant.Combat.PlaytestEnvironment.Settings settings)
        {
            arena.ClearTestEnemies();
            for(int g=0;g<settings.groupCount;g++)for(int m=0;m<settings.monstersPerGroup;m++)
            {
                int kindIndex=(g+m)%Data.monsters.Length;
                var kind=Data.monsters[kindIndex];
                float x=arena.Player.spawn.x+settings.firstGroupOffset+g*settings.groupSpacing+(m-(settings.monstersPerGroup-1)*.5f)*settings.memberSpacing;
                if(!TryTestSpawn(x,kind,settings.memberSpacing,out var at))continue;
                var enemy=SpawnMonster(kindIndex,at);
                enemy.name="무리 "+(g+1)+" · "+(m+1)+" / "+kind.name;
            }
            Physics2D.SyncTransforms();return arena.enemies.Count;
        }
        bool TryTestSpawn(float requestedX,MonsterKind kind,float searchRadius,out Vector2 at)
        {
            // Stay near this member's slot; never spawn inside a step above a lower support.
            for(int attempt=0;attempt<=2*Mathf.FloorToInt(searchRadius/.25f);attempt++)
            {
                float shift=attempt==0?0:((attempt+1)/2)*.25f*(attempt%2==1?1:-1);
                float x=requestedX+shift,left=x+kind.bodyOffset-kind.bodyWidth/64f,right=left+kind.bodyWidth/32f;
                float top=float.NegativeInfinity;
                foreach(var rect in Data.rects)if(rect.x<=left && rect.x+rect.w>=right)top=Mathf.Max(top,-rect.y);
                if(float.IsNegativeInfinity(top))continue;
                float bottom=top+.03f,bodyTop=bottom+kind.bodyHeight/32f;
                bool blocked=false;
                foreach(var rect in Data.rects)
                    if(rect.x<right-.001f && rect.x+rect.w>left+.001f && -rect.y>bottom+.001f && -rect.y-rect.h<bodyTop-.001f){blocked=true;break;}
                if(blocked)continue;
                at=new Vector2(x,bottom);return true;
            }
            at=default;return false;
        }
        public bool ExternalAreaNavigation { get; set; }
        void Update() { if(ExternalAreaNavigation||!DevFeatures.Enabled||FullMap&&FullMap.IsOpen)return;if(Keyboard.current!=null && Keyboard.current.f3Key.wasPressedThisFrame) destinations=!destinations; }
        void LateUpdate()
        {
            if(!Ready)return; RefreshCamera(Time.deltaTime);
            foreach(var e in arena.enemies) if(e && e.visual && !(e is TrainingDummy) && !e.GetComponent<Chapter1MonsterPresentation>()) { e.visual.transform.localPosition=Vector3.zero; e.visual.transform.localScale=Vector3.one; e.visual.color=e.Dead?new Color(.5f,.5f,.5f,.3f):Color.white; }
        }
        static void Damp(ref float position,ref float velocity,float target,float omega,float dt)
        { float offset=position-target,j=velocity+omega*offset,e=Mathf.Exp(-omega*dt); position=target+(offset+j*dt)*e; velocity=(velocity-omega*j*dt)*e; }
        // Left edge of the camera in map pixels. The village opening lowers it for its path outside the map (negative x).
        public float CameraMinPixels { get; set; }
        // The village ground and its wider sky course have independent horizontal extents.
        public float? CameraRightPixels { get; set; }
        Chapter1BossRoomEnclosure bossCameraEnclosure;
        const float BossCameraBlendSeconds=.65f;
        Vector2? cameraFixedTarget;
        Vector2 cameraFixedFrom;
        float cameraFixedBlend=BossCameraBlendSeconds;
        // Top edge the camera may rise to (map pixels, y down). The village's sky path (StartingVillageWorld.SkyParkour) raises it.
        public float CameraTopPixels { get; set; } = -384;
        public void RefreshCamera(float dt,bool snap=false)
        {
            if (!View || !arena.Player || cameraFollow == null) return;
            var player=arena.Player;var position=!snap&&CameraPresentationPosition!=null?CameraPresentationPosition():player.Position;
            float x=position.x*32,y=-position.y*32;
            float fallPixels=Mathf.Max(0,-player.velocity.y*32);
            float right=CameraRightPixels??Data.width;
            if(!bossCameraEnclosure)bossCameraEnclosure=GetComponent<Chapter1BossRoomEnclosure>();
            if(!FixedCameraCenter.HasValue && bossCameraEnclosure && bossCameraEnclosure.ApproachCameraRightPixels is float bossEdge)right=Mathf.Min(right,bossEdge);
            float maxX=Mathf.Max(CameraMinPixels,right-960);
            float targetX=Mathf.Clamp(x-432,CameraMinPixels,maxX);
            if(snap||float.IsNaN(cameraAnchorY)||player.grounded)cameraAnchorY=y;
            else cameraAnchorY=Mathf.Clamp(cameraAnchorY,y-cameraDeadzoneDownPixels,y+cameraDeadzoneUpPixels);
            // Returning from a normal jump must not activate falling look-ahead.
            if(y<=cameraAnchorY)fallPixels=0;
            float targetY=cameraAnchorY-270-cameraVerticalOffsetPixels+Mathf.Min(220,fallPixels*.09f);
            targetY=Mathf.Clamp(targetY,CameraTopPixels,Data.height-540);
            bool blendBossView=FixedCameraCenter.HasValue&&bossCameraEnclosure&&bossCameraEnclosure.Holding;
            if(FixedCameraCenter.HasValue)
            {
                var center=FixedCameraCenter.Value;
                if(blendBossView&&(!cameraFixedTarget.HasValue||cameraFixedTarget.Value!=center))
                {
                    cameraFixedTarget=center;cameraFixedFrom=new Vector2(CameraX,CameraY);
                    cameraFixedBlend=snap?BossCameraBlendSeconds:0;
                }
                targetX=Mathf.Clamp(center.x*32-480,CameraMinPixels,maxX);
                targetY=Mathf.Clamp(-center.y*32-270,CameraTopPixels,Data.height-540);
                // Glide onto a fixed view (entering a boss room) instead of jumping; callers may still snap.
            }
            if(!blendBossView){cameraFixedTarget=null;cameraFixedBlend=BossCameraBlendSeconds;}
            float px=CameraX,py=CameraY;
            if(snap){px=targetX;py=targetY;cameraVX=cameraVY=0;cameraFixedBlend=BossCameraBlendSeconds;}
            else
            {
                Damp(ref px,ref cameraVX,targetX,CameraFollowOmega,dt);
                // Long, fast falls tighten back to the old 22 so the player cannot sink off the bottom of the view.
                float omegaY=FixedCameraCenter.HasValue?22:Mathf.Lerp(cameraVerticalOmega,22,Mathf.InverseLerp(480,800,fallPixels));
                Damp(ref py,ref cameraVY,targetY,omegaY,dt);
            }
            if(!snap&&blendBossView&&cameraFixedBlend<BossCameraBlendSeconds)
            {
                cameraFixedBlend=Mathf.Min(BossCameraBlendSeconds,cameraFixedBlend+Mathf.Max(0,dt));
                float blend=Mathf.SmoothStep(0,1,cameraFixedBlend/BossCameraBlendSeconds);
                px=Mathf.Lerp(cameraFixedFrom.x,targetX,blend);py=Mathf.Lerp(cameraFixedFrom.y,targetY,blend);cameraVX=cameraVY=0;
            }
            CameraX=Mathf.Clamp(px,CameraMinPixels,maxX); CameraY=Mathf.Clamp(py,CameraTopPixels,Data.height-540);
            float ratio=Screen.width/(float)Mathf.Max(1,Screen.height),aspect=960f/540;
            View.rect=ratio>aspect?new Rect((1-aspect/ratio)/2,0,aspect/ratio,1):new Rect(0,(1-ratio/aspect)/2,1,ratio/aspect);
            float zoom=Mathf.Max(1,CameraZoom);
            View.orthographicSize=540f/64/zoom;
            Vector2 shake=Vector2.zero;
            if(shakeLeft>0){shakeLeft=Mathf.Max(0,shakeLeft-Mathf.Max(dt,0));shake=UnityEngine.Random.insideUnitCircle*shakeAmplitude*(shakeLeft/shakeDuration);}
            float viewX=(CameraX+480)/32,viewY=-(CameraY+270)/32;
            if(zoom>1.001f)
            {
                float spareX=15*(1-1/zoom),spareY=270f/32*(1-1/zoom);
                viewX=Mathf.Clamp(CameraZoomCenter.x,viewX-spareX,viewX+spareX);viewY=Mathf.Clamp(CameraZoomCenter.y,viewY-spareY,viewY+spareY);
            }
            cameraFollow.Submit(new CameraPosition(viewX+shake.x,viewY+shake.y,-10));
            cameraOutput.ApplyNow();
            for(int j=0;j<Data.layers.Length;j++)
            {
                var l=Data.layers[j]; float offset=CameraX*l.parallax+l.phase+Time.time*l.drift,top=-Mathf.Clamp(l.baseCrop+CameraY*l.vertical,l.minVertical,l.maxVertical);
                // Use the last painted row, not the texture's transparent padding.
                if(l.coverBottom)top=Mathf.Max(top,Mathf.Ceil(540+l.bottomMargin-l.height*l.contentBottom));
                int first=Mathf.FloorToInt(offset/l.width)-1;
                for(int i=0;i<backdropRows[j].Length;i++)
                {
                    int n=first+i; var r=backdropRows[j][i]; r.flipX=!l.single&&!l.repeatUnmirrored&&(n%2+2)%2==1;
                    float screenX=l.single?l.phase-CameraX*l.parallax:n*l.width-offset;
                    r.transform.position=new Vector3((CameraX+screenX+l.width/2f)/32,-(CameraY+top+l.height/2f)/32,0);
                }
            }
        }
        public bool BackgroundCoversBottom(out string evidence)
        {
            evidence="";bool pass=true;
            for(int j=0;j<Data.layers.Length;j++)
            {
                var l=Data.layers[j];if(!l.coverBottom)continue;
                var r=backdropRows[j][1];
                float contentEdge=-r.transform.position.y*32-CameraY+(l.contentBottom-.5f)*l.height;
                evidence+=l.id+" painted bottom="+contentEdge.ToString("F1")+"px; ";
                pass&=contentEdge>=540+l.bottomMargin-.1f;
            }
            return pass;
        }
        public void Warp(int index)
        {
            if(!Ready)return;
            var view=Data.views[index];
            var position=new Vector2(view.x/32,-view.y/32);
            if(Data.rooms!=null&&index<Data.rooms.Length&&Data.rooms[index].kind=="combat")
            {
                var room=Data.rooms[index];
                float center=room.x+room.w/2f,foot=room.y+21,best=float.PositiveInfinity;
                void Consider(float left,float right,float top)
                {
                    if(right-left<2||top<room.y||top>=room.y+room.h)return;
                    float x=Mathf.Clamp(center,left+1,right-1);
                    float score=Mathf.Abs(x-center)*100+Mathf.Abs(top-foot);
                    if(score>=best)return;
                    best=score;position=new Vector2(x,-top);
                }
                // Merged collision rectangles also have internal top edges. Only
                // exposed terrain tops are valid destinations in a rooted hill.
                for(int y=room.y;y<room.y+room.h;y++)
                {
                    bool Exposed(int x)=>Data.ground[y*Data.cols+x]!=0&&
                        (y==0||Data.ground[(y-1)*Data.cols+x]==0);
                    for(int x=room.x;x<room.x+room.w;)
                    {
                        if(!Exposed(x)){x++;continue;}
                        int left=x;
                        while(x<room.x+room.w&&Exposed(x))x++;
                        Consider(left,x,y);
                    }
                }
                foreach(var platform in Data.platforms)
                    if(platform.x>=room.x*32&&platform.x+platform.w<=(room.x+room.w)*32)
                        Consider(platform.x/32f,(platform.x+platform.w)/32f,platform.y/32f);
                if(float.IsPositiveInfinity(best))throw new InvalidOperationException("Combat room has no safe warp surface: "+room.name);
            }
            arena.Player.CancelAction();arena.Player.Position=position;arena.Player.velocity=Vector2.zero;
            arena.Player.jumps.Reset();Physics2D.SyncTransforms();arena.Player.grounded=arena.Player.ProbeGround();
            RefreshCamera(0,true);destinations=false;
        }
        public bool ExternalPresentation { get; set; }

        void OnGUI()
        {
            if (ExternalPresentation || dummyTrainingOnly) return;
            if (!Ready || FullMap&&FullMap.IsOpen) return; GUI.depth = -20; if (diagnostics.uiFont) GUI.skin.font = diagnostics.uiFont;
            if(!diagnostics.CompactHud) {
            var old=GUI.color;GUI.color=new Color(.06f,.07f,.10f,.94f);GUI.DrawTexture(new Rect(8,6,Screen.width-172,62),Texture2D.whiteTexture);GUI.color=old;
            var style=new GUIStyle(GUI.skin.label){fontSize=13};style.normal.textColor=new Color(.97f,.92f,.83f);
            GUI.Label(new Rect(16,10,Screen.width-188,24),PlayerControls.Prompt(arena.Player, "{Move} 이동   {Jump} 2단 점프   {Move.Down}+{Jump} 내려가기   {Dash} 대시   {Weapon1Basic}/{Weapon2Basic} 기본1/2   {Weapon1Special}/{Weapon2Special} 스킬1/2"),style);
            var playerPoint=new Vector2(arena.Player.Position.x*32,-arena.Player.Position.y*32);
            string roomName="길목";
            if(Data.rooms!=null&&Data.rooms.Length>0)
            {
                foreach(var room in Data.rooms)
                    if(playerPoint.x>=room.x*32&&playerPoint.x<(room.x+room.w)*32&&playerPoint.y>=room.y*32&&playerPoint.y<(room.y+room.h)*32)
                    {roomName=room.name;break;}
            }
            else
            {
                var nearest=float.PositiveInfinity;
                foreach(var view in Data.views){float distance=(new Vector2(view.x,view.y)-playerPoint).sqrMagnitude;if(distance<nearest){nearest=distance;roomName=view.name;}}
            }
            GUI.Label(new Rect(16,36,Screen.width-340,24),"1챕터 / "+roomName+"   M 전체 지도   R 다시 시작   F1 장비   F2 판정   F4 배치",style);
            if(DevFeatures.Enabled&&GUI.Button(new Rect(Screen.width-314,36,134,26),destinations?"구간 닫기 [F3]":"구간 보기 [F3]"))destinations=!destinations;
            }
            if(Data.landmarks!=null)foreach(var landmark in Data.landmarks){
                if(landmark.kind=="boss")continue; // no "최종보스" badge over the boss room
                var point=Deviant.Presentation.Unity.CameraDisplayProjection.WorldToScreenPoint(View, new Vector3(landmark.x/32,-landmark.y/32+2.5f,0));
                if(point.z<=0 || point.x<-100 || point.x>Screen.width+100 || point.y<0 || point.y>Screen.height)continue;
                var badge=new GUIStyle(GUI.skin.box){fontSize=15,alignment=TextAnchor.MiddleCenter};
                badge.normal.textColor=Chapter1MaintenanceFacility.IsFacility(landmark.kind)?new Color(.4f,1,.8f):new Color(1,.5f,.45f);
                GUI.Box(new Rect(point.x-110,Screen.height-point.y-34,220,34),landmark.name,badge);
            }
            if(destinations&&DevFeatures.Enabled)for(int i=0;i<Data.views.Length;i++)
                if(FullMap.IsRoomDiscovered(i)&&GUI.Button(new Rect(14+(i%4)*180,242+(i/4)*34,172,30),(i+1)+". "+Data.views[i].name))Warp(i);
        }
        void OnDestroy() { CloseScenePolicy(); foreach (var t in madeTiles) if (t) Destroy(t); foreach (var s in madeSprites) if (s) Destroy(s); }
    }
}
