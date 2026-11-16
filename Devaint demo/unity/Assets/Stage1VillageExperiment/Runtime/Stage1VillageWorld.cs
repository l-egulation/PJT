using System;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Game.Diagnostics;
using Deviant.Integration;
using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Stage1VillageExperiment
{
    // Scene composition only: movement, weapons, damage and input use the common combat runtime.
    // Authored map coordinates are top-left pixels; GroundRect coordinates are native 32px cells.
    [DefaultExecutionOrder(500)]
    public sealed class Stage1VillageWorld : MonoBehaviour
    {
        public CombatArena arena;
        public TextAsset worldJson;
        public Material spriteMaterial;
        public Camera sceneCamera;
        public Sprite playerSprite;
        public Chapter1MonsterCatalog monsterAnimations;
        public Chapter1AttackEffectCatalog monsterAttackEffects;
        public Sprite[] monsterSprites;
        public CombatDiagnostics diagnostics;
        public int fixedStepsPerSecond = 120;
        public bool enemiesAggressive = true;
        public bool showHud = true;
        public bool enableAreaShortcuts = true;

        public ForestData Data { get; private set; }
        public Camera View => sceneCamera;
        public bool Ready { get; private set; }
        public float CameraX { get; private set; }
        public float CameraY { get; private set; }
        public int SurfaceCount { get; private set; }
        public Chapter1FullMap FullMap { get; private set; }
        public IReadOnlyList<ArenaSurface> TerrainSurfaces => surfaces;
        public event Action Initialized;

        readonly List<ArenaSurface> surfaces = new List<ArenaSurface>();
        StaticArenaSurfaceIndex surfaceIndex;
        CameraOutputBinding cameraOutput;
        CameraOutputBinding.BaseSource cameraFollow;
        IDisposable engineTime;
        float previousFixedDelta, cameraVX, cameraVY;
        int nextEnemy;
        bool areaShortcutsOpen;
        GUIStyle textStyle, titleStyle;

        void Start()
        {
            if (!arena || !worldJson || !sceneCamera || !spriteMaterial || !playerSprite)
                throw new InvalidOperationException("Village scene requires its combat, authored layout, camera, approved material and player sprite.");
            if (fixedStepsPerSecond <= 0)
                throw new InvalidOperationException("Village scene requires a positive explicit simulation rate.");
            Data = JsonUtility.FromJson<ForestData>(worldJson.text);
            ValidateData();
            var content = CombatContentLoader.Load(arena.characterAnimations, arena.loadCharacterArt);
            content.Profile = CombatCompositionProfile.Game;
            content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
            content.Camera = sceneCamera;
            content.SpriteMaterial = spriteMaterial;
            content.PlayerSprite = playerSprite;
            content.PlayerSpawn = PixelFeet(Data.spawn.x, Data.spawn.y);
            content.PlayerSize = new Vector2(.9375f, 2);
            content.PlayerOffset = new Vector2(.1875f, 1);
            content.PlayerSortingOrder = 10;
            int bomb = Array.FindIndex(content.Catalog.forms, form => form.id == "bomb");
            content.SecondaryFormIndex = bomb >= 0 ? bomb : 1;
            content.PrepareEnvironment = _ => BuildGeometry();
            content.BindPlayerInput = player => player.gameObject.AddComponent<CombatKeyboardInput>().Player = player;
            content.Enemies = EnemyRequests();
            content.EnemyFactory = CreateAuthoredEnemy;
            if (!arena.Initialize(content)) { enabled = false; return; }
            arena.Player.gameObject.AddComponent<Chapter1PlayerHitFeedback>().player = arena.Player;
            arena.showDebugUI = false;
            arena.showPanel = false;
            if (diagnostics)
            {
                diagnostics.arena = arena;
                diagnostics.showDebugUI = false;
                diagnostics.showPanel = false;
                arena.uiFont = diagnostics.uiFont;
            }
            sceneCamera.orthographic = true;
            sceneCamera.orthographicSize = 540f / 64;
            cameraOutput = CameraOutputBinding.GetOrCreate(sceneCamera);
            Ready = true;
            FullMap = gameObject.AddComponent<Chapter1FullMap>();
            FullMap.Initialize(new Chapter1MapSource
            {
                Data = Data, Arena = arena, Diagnostics = diagnostics,
                Ready = () => this && Ready,
                Visible = () => showHud,
                CameraPosition = () => new Vector2(CameraX, CameraY),
                Title = "잊힌 마을 / 전체 지도", MinimapTop = 96, PlatformLabel = "돌 발판"
            });
            OpenScenePolicy();
            RefreshCamera(0, true);
            Initialized?.Invoke();
        }

        void ValidateData()
        {
            if (Data == null || Data.width < 960 || Data.height < 540 || Data.cols <= 0 || Data.rows <= 0 ||
                Data.spawn == null || Data.rects == null || Data.views == null || Data.views.Length == 0 ||
                Data.ground == null || Data.ground.Length != Data.cols * Data.rows)
                throw new InvalidOperationException("Village layout requires valid dimensions, grid occupancy, spawn, terrain and camera areas.");
            if (Data.width != Data.cols * 32 || Data.height != Data.rows * 32)
                throw new InvalidOperationException("Village dimensions must match its native 32px occupancy grid.");
            Data.platforms = Data.platforms ?? Array.Empty<FoliagePlatform>();
            Data.actors = Data.actors ?? Array.Empty<MonsterPlacement>();
            Data.monsters = Data.monsters ?? Array.Empty<MonsterKind>();
            Data.layers = Data.layers ?? Array.Empty<ForestLayer>();
            Data.landmarks = Data.landmarks ?? Array.Empty<ForestLandmark>();
            foreach (var rect in Data.rects)
                if (rect == null || rect.w <= 0 || rect.h <= 0)
                    throw new InvalidOperationException("Authored village solids must have positive dimensions.");
            foreach (var placement in Data.actors)
                if (placement.kind < 0 || placement.kind >= Data.monsters.Length)
                    throw new InvalidOperationException("Village enemy placement references an unknown approved species.");
        }

        public static Vector2 PixelFeet(float x, float y) => new Vector2(x / 32, -y / 32);

        BoxCollider2D AddSurface(Transform parent, string label, Vector2 center, Vector2 size, bool oneWay)
        {
            var node = new GameObject(label);
            node.transform.SetParent(parent, false);
            node.transform.position = center;
            var surface = node.AddComponent<ArenaSurface>();
            surface.oneWay = oneWay;
            var collider = node.AddComponent<BoxCollider2D>();
            collider.size = size;
            surfaces.Add(surface);
            SurfaceCount++;
            return collider;
        }

        void BuildGeometry()
        {
            var root = new GameObject("Authored village collision").transform;
            root.SetParent(transform, false);
            foreach (var rect in Data.rects)
                AddSurface(root, "Solid", new Vector2(rect.x + rect.w / 2f, -rect.y - rect.h / 2f), new Vector2(rect.w, rect.h), false);
            foreach (var platform in Data.platforms)
            {
                if (platform.w <= 0) throw new InvalidOperationException("Village platform width must be positive.");
                const float thickness = .125f;
                var collider = AddSurface(root, "One-way stone ledge", new Vector2((platform.x + platform.w / 2f) / 32, -platform.y / 32f - thickness / 2),
                    new Vector2(platform.w / 32f, thickness), true);
                collider.GetComponent<ArenaSurface>().blockDropThrough = platform.blockDropThrough;
            }
            float height = Data.height / 32f + 32;
            AddSurface(root, "Left map boundary", new Vector2(-.5f, -Data.height / 64f), new Vector2(1, height), false);
            AddSurface(root, "Right map boundary", new Vector2(Data.width / 32f + .5f, -Data.height / 64f), new Vector2(1, height), false);
            var recovery = new GameObject("Fall recovery");
            recovery.transform.SetParent(root, false);
            recovery.transform.position = new Vector2(Data.width / 64f, -Data.height / 32f - 3);
            var pit = recovery.AddComponent<BoxCollider2D>();
            pit.isTrigger = true;
            pit.size = new Vector2(Data.width / 32f + 8, 4);
            recovery.AddComponent<ArenaPit>();
            arena.pits.Add(pit);
            Physics2D.SyncTransforms();
            surfaceIndex = new StaticArenaSurfaceIndex(surfaces);
        }

        CombatEnemySpawn[] EnemyRequests()
        {
            var requests = new CombatEnemySpawn[Data.actors.Length];
            for (int index = 0; index < requests.Length; index++)
            {
                var placement = Data.actors[index];
                var species = Data.monsters[placement.kind];
                var definition = Chapter1EnemyDefinitions.Load(species.id);
                requests[index] = new CombatEnemySpawn(species.name, 0, PixelFeet(placement.x, placement.y),
                    new Vector2(definition.bodySize[0], definition.bodySize[1]), new Vector2(definition.bodyOffset[0], definition.bodyOffset[1]),
                    definition.maxHP, false, enemiesAggressive, 10);
            }
            return requests;
        }

        TestEnemy CreateAuthoredEnemy(CombatArena owner, CombatEnemySpawn request)
        {
            var placement = Data.actors[nextEnemy++];
            var actor = CreateSpeciesEnemy(owner, request, placement.kind);
            var tuning = Data.encounters;
            if (placement.roaming && tuning != null && tuning.waitSeconds?.Length >= 2 && tuning.walkSeconds?.Length >= 2)
                actor.ConfigureIdleRoaming(placement.roamSeed, tuning.roamRadius, tuning.roamSpeedScale,
                    tuning.waitSeconds[0], tuning.waitSeconds[1], tuning.walkSeconds[0], tuning.walkSeconds[1]);
            return actor;
        }

        FieldMouseEnemy CreateSpeciesEnemy(CombatArena owner, CombatEnemySpawn request, int kindIndex)
        {
            var species = Data.monsters[kindIndex];
            var clip = monsterAnimations ? monsterAnimations.Find(species.id, "move") : null;
            if (clip == null || clip.frames == null || clip.frames.Length == 0)
                throw new InvalidOperationException("Village requires approved animation media for " + species.id);
            var node = new GameObject(request.Name);
            node.transform.SetParent(transform, false);
            var actor = node.AddComponent<FieldMouseEnemy>();
            actor.SurfaceIndex = surfaceIndex;
            actor.InitializeSpecies(owner, request.Position, Chapter1EnemyDefinitions.Load(species.id));
            actor.profile = request.Profile;
            actor.spawn = request.Position;
            actor.maxHealth = request.MaxHealth;
            actor.aggressive = request.Aggressive;
            actor.isBoss = request.IsBoss;
            var visual = new GameObject("Approved enemy art");
            visual.transform.SetParent(node.transform, false);
            actor.visual = visual.AddComponent<SpriteRenderer>();
            actor.visual.sprite = monsterSprites != null && kindIndex < monsterSprites.Length && monsterSprites[kindIndex]
                ? monsterSprites[kindIndex] : clip.frames[0];
            actor.visual.sharedMaterial = spriteMaterial;
            actor.visual.sortingOrder = request.SortingOrder;
            actor.ResetEnemy();
            node.AddComponent<Chapter1AttackWarning>().actor = actor;
            var presentation = node.AddComponent<Chapter1MonsterPresentation>();
            presentation.actor = actor;
            presentation.catalog = monsterAnimations;
            presentation.species = species.id;
            if (monsterAttackEffects && monsterAttackEffects.Find(species.id) != null)
            {
                presentation.attackEffects = node.AddComponent<Chapter1AttackEffects>();
                presentation.attackEffects.catalog = monsterAttackEffects;
            }
            return actor;
        }

        public FieldMouseEnemy SpawnMonster(string speciesId, Vector2 feet, bool aggressive = true, int facing = -1)
        {
            if (!Ready) throw new InvalidOperationException("Wait for village initialization before spawning actors.");
            int index = Array.FindIndex(Data.monsters, species => species.id == speciesId);
            if (index < 0) throw new ArgumentException("Unknown village species: " + speciesId, nameof(speciesId));
            var definition = Chapter1EnemyDefinitions.Load(speciesId);
            var request = new CombatEnemySpawn(Data.monsters[index].name, 0, feet, new Vector2(definition.bodySize[0], definition.bodySize[1]),
                new Vector2(definition.bodyOffset[0], definition.bodyOffset[1]), definition.maxHP, false, aggressive, 10);
            var actor = CreateSpeciesEnemy(arena, request, index);
            actor.SyncFacing(facing);
            arena.enemies.Add(actor);
            Physics2D.SyncTransforms();
            return actor;
        }

        public Vector2 GetLandmarkSafeFeet(int zoneIndex)
        {
            if (Data == null || zoneIndex < 0 || zoneIndex >= Data.views.Length) throw new ArgumentOutOfRangeException(nameof(zoneIndex));
            var point = Data.views[zoneIndex];
            return PixelFeet(point.x, point.y);
        }

        // Inspection shortcut, not a traversal proof or a gameplay teleport ability.
        public void Warp(int zoneIndex)
        {
            if (!Ready) return;
            var feet = GetLandmarkSafeFeet(zoneIndex);
            var player = arena.Player;
            player.CancelAction();
            player.ClearInputState();
            player.Position = feet;
            player.velocity = Vector2.zero;
            player.jumps.Reset();
            Physics2D.SyncTransforms();
            player.grounded = player.ProbeGround();
            RefreshCamera(0, true);
            areaShortcutsOpen = false;
        }

        void OpenScenePolicy()
        {
            if (!Ready || cameraFollow != null) return;
            previousFixedDelta = Time.fixedDeltaTime;
            Time.fixedDeltaTime = 1f / fixedStepsPerSecond;
            try
            {
                engineTime = arena.Controls.BindEngineTime();
                cameraFollow = cameraOutput.BindBaseSource("stage1 authored village follow");
            }
            catch { CloseScenePolicy(); throw; }
        }

        void CloseScenePolicy()
        {
            cameraFollow?.Dispose(); cameraFollow = null;
            engineTime?.Dispose(); engineTime = null;
            if (previousFixedDelta > 0) { Time.fixedDeltaTime = previousFixedDelta; previousFixedDelta = 0; }
        }

        public void RefreshCamera(float dt, bool snap = false)
        {
            if (!Ready || !arena.Player || cameraFollow == null) return;
            float x = arena.Player.Position.x * 32, y = -arena.Player.Position.y * 32;
            float targetX = Mathf.Clamp(x - 432, 0, Data.width - 960);
            float targetY = y - 384;
            if (!arena.Player.grounded && !snap)
            {
                float screenY = y - CameraY;
                targetY = screenY < 306 ? y - 306 : screenY > 442 ? y - 442 : CameraY;
            }
            targetY = Mathf.Clamp(targetY, -384, Data.height - 540);
            float px = CameraX, py = CameraY;
            if (snap) { px = targetX; py = targetY; cameraVX = cameraVY = 0; }
            else
            {
                Damp(ref px, ref cameraVX, targetX, 12.5f, dt);
                Damp(ref py, ref cameraVY, targetY, Mathf.Lerp(7, 12, Mathf.Clamp01((-arena.Player.velocity.y * 32 - 200) / 300)), dt);
            }
            CameraX = Mathf.Clamp(px, 0, Data.width - 960);
            CameraY = Mathf.Clamp(py, -384, Data.height - 540);
            float ratio = Screen.width / (float)Mathf.Max(1, Screen.height), aspect = 960f / 540;
            View.rect = ratio > aspect ? new Rect((1 - aspect / ratio) / 2, 0, aspect / ratio, 1) : new Rect(0, (1 - ratio / aspect) / 2, 1, ratio / aspect);
            View.orthographicSize = 540f / 64;
            cameraFollow.Submit(new CameraPosition((Mathf.Round(CameraX) + 480) / 32, -(Mathf.Round(CameraY) + 270) / 32, -10));
            cameraOutput.ApplyNow();
        }

        static void Damp(ref float position, ref float velocity, float target, float omega, float dt)
        {
            float offset = position - target, j = velocity + omega * offset, e = Mathf.Exp(-omega * Mathf.Max(0, dt));
            position = target + (offset + j * dt) * e;
            velocity = (velocity - omega * j * dt) * e;
        }

        void Update()
        {
            if (FullMap && FullMap.IsOpen) { areaShortcutsOpen = false; return; }
            if (Ready && enableAreaShortcuts && Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
                areaShortcutsOpen = !areaShortcutsOpen;
        }

        void LateUpdate() { if (Ready) RefreshCamera(Time.deltaTime); }
        void OnEnable() { if (Ready) OpenScenePolicy(); if (FullMap) FullMap.enabled = true; }
        void OnDisable() { areaShortcutsOpen = false; if (FullMap) FullMap.enabled = false; CloseScenePolicy(); }
        void OnDestroy() { CloseScenePolicy(); Ready = false; }

        void OnGUI()
        {
            if (!Ready || !showHud || (FullMap && FullMap.IsOpen)) return;
            if (diagnostics && diagnostics.uiFont) GUI.skin.font = diagnostics.uiFont;
            if (textStyle == null)
            {
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
                textStyle.normal.textColor = new Color32(243, 236, 218, 255);
                titleStyle = new GUIStyle(textStyle) { fontSize = 16, fontStyle = FontStyle.Bold };
            }
            var previous = GUI.color;
            GUI.color = new Color(.08f, .09f, .10f, .84f);
            GUI.DrawTexture(new Rect(20, 18, 224, 65), Texture2D.whiteTexture);
            GUI.color = new Color(.22f, .22f, .24f, 1);
            GUI.DrawTexture(new Rect(31, 57, 202, 12), Texture2D.whiteTexture);
            GUI.color = new Color32(220, 110, 94, 255);
            GUI.DrawTexture(new Rect(31, 57, 202 * Mathf.Clamp01(arena.Player.health / Mathf.Max(1, arena.Player.maxHealth)), 12), Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(new Rect(30, 26, 210, 25), "잊힌 마을 / 1 STAGE", titleStyle);
            var help = PlayerControls.Prompt(arena.Player,
                "{Move} 이동  /  {Jump} 점프  /  {Move.Down}+{Jump} 내려가기  /  {Dash} 대시  /  {Weapon1Basic}/{Weapon2Basic} 기본  /  {Weapon1Special}/{Weapon2Special} 스킬  /  {Map} 지도");
            GUI.Label(new Rect(20, Screen.height - 31, Screen.width - 40, 25),
                help + (enableAreaShortcuts ? "  /  F3 구간 보기" : ""), textStyle);
            if (areaShortcutsOpen)
                for (int index = 0; index < Data.views.Length; index++)
                    if (GUI.Button(new Rect(20 + index % 4 * 200, 266 + index / 4 * 36, 192, 30), Data.views[index].name)) Warp(index);
        }
    }
}
