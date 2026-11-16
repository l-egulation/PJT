using System;
using System.Collections;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Game.Diagnostics;
using Deviant.Integration;
using Deviant.World;
using Deviant.UI;
using UnityEngine;

namespace Deviant.Game
{
    // Composition only: combat rules, terrain and HUD remain owned by their modules.
    [DefaultExecutionOrder(-50)]
    public partial class GameComposition : MonoBehaviour, IBattleHudDataSource
    {
        public CombatArena combat;
        public PlaytestEnvironment TestEnvironment { get; private set; }
        public InkForestWorld forest;
        public InkForestCamera cameraRig;
        public InkMonsterData groundMonster, flyingMonster;
        public GameObject monsterHealthBar;
        public BattleHudView hud;
        public CombatDiagnostics diagnostics;
        public Material spriteMaterial;
        public Sprite playerSprite;
        public Vector2 playerSpawn = new Vector2(4.6875f, -8.97f);
        readonly BattleHudState state = new BattleHudState();
        ICombatReadModel combatReads;
        CombatInputSession hudSession;
        System.IDisposable engineTime;
        Deviant.Control.Contracts.IControlRequests controls;
        void Awake()
        {
            if (!combat || !forest || !cameraRig || !hud || !diagnostics)
            {
                Debug.LogError("GameComposition requires its combat, forest, camera, HUD and technical diagnostics.");
                if (combat) combat.enabled = false;
                enabled = false; return;
            }
            controls = combat.Controls;
            combatReads = combat.ReadModel;
            try
            {
                OpenSession(); WireLifetime(); BindHudSession();
                engineTime = combat.Controls.BindEngineTime(); hud.Bind(this);
            }
            catch (InvalidOperationException error)
            {
                Debug.LogError(error.Message); CloseSession(GameSessionEndReason.InitializationFailed);
                UnwireLifetime(); combat.enabled = false; enabled = false;
            }
        }
        IEnumerator Start()
        {
            try
            {
                if (!combat || !forest || !cameraRig || !diagnostics) throw new InvalidOperationException("GameComposition requires its combat, forest, camera and technical diagnostics.");
                OpenSession(); WireLifetime();
                var content = CombatContentLoader.Load(combat.characterAnimations, combat.loadCharacterArt);
                content.Profile = CombatCompositionProfile.Game;
                content.Loadout = Session.Loadout;
                content.Camera = cameraRig.GetComponent<Camera>(); content.SpriteMaterial = spriteMaterial; content.PlayerSprite = playerSprite;
                content.PlayerSpawn = playerSpawn; content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = new Vector2(.1875f, 1);
                content.PlayerSortingOrder = 35;
                int bomb = content.Catalog?.forms == null ? -1 : Array.FindIndex(content.Catalog.forms, form => form.id == "bomb");
                content.SecondaryFormIndex = bomb >= 0 ? bomb : 1;
                content.Enemies = TemporaryForestEncounters();
                content.PrepareEnvironment = PrepareEnvironment;
                content.BindPlayerInput = player => player.gameObject.AddComponent<CombatKeyboardInput>().Player = player;
                if (!combat.Initialize(content)) enabled = false;
                diagnostics.arena = combat;
                if (enabled) { sessionStarted = true; ObservePlayer(); BindSessionHud(); }
            }
            catch (InvalidOperationException error)
            {
                Debug.LogError(error.Message);
                if (combat) combat.enabled = false;
                enabled = false;
            }
            if (!enabled) yield break;
            // Preserve the existing next-frame Game arming/presentation boundary.
            // Actors already exist at their requested forest positions; no temporary Lab placement is used.
            yield return null;
            if (!enabled || Session == null || !Session.IsActive) yield break;
            foreach (var enemy in combat.enemies)
            {
                enemy.aggressive = true;
                if (monsterHealthBar)
                {
                    var bar = Instantiate(monsterHealthBar, enemy.transform);
                    bar.transform.localPosition = Vector3.up * (enemy.size.y + .4f);
                }
                var art = enemy.profile == 1 ? flyingMonster : groundMonster;
                if (art && !enemy.isBoss)
                {
                    var skin = enemy.gameObject.AddComponent<ForestEnemyPresentation>();
                    skin.actor = enemy; skin.data = art;
                }
            }
            combat.uiFont = diagnostics.uiFont;
            diagnostics.showDebugUI = false;
            diagnostics.EquipmentTuning = combat.DrawWeaponTuning;
            combat.TestEquipmentPanel = rect => { diagnostics.showPanel = true; diagnostics.DrawEquipmentPanel(rect); };
            TestEnvironment = gameObject.AddComponent<PlaytestEnvironment>();
            TestEnvironment.standaloneArena = combat;
            TestEnvironment.GroupSpawner = s => RebuildTestGroups(s.monstersPerGroup, s.groupCount, s.groupSpacing, s.memberSpacing, s.firstGroupOffset);
            TestEnvironment.Initialize();
            BindFollowTargets();
        }
        CombatEnemySpawn[] TemporaryForestEncounters()
        {
            // Approved prototype encounters, not chapter/Run content or Lab defaults.
            Vector2 At(int index)
            {
                float x = playerSpawn.x + 6 + index * 8;
                return new Vector2(x, forest.data.FloorTop(x) + .03f);
            }
            return new[] {
                new CombatEnemySpawn("MeleeTester", 0, At(0), new Vector2(2.65f, 1.9f), new Vector2(.18f, .95f), 100, false, false, 34),
                new CombatEnemySpawn("RangedTester", 1, At(1), new Vector2(1.3f, 1.2f), new Vector2(.45f, 1.25f), 60, false, false, 34),
                new CombatEnemySpawn("BossDummy", 2, At(2), new Vector2(5.3f, 3.8f), new Vector2(.36f, 1.9f), 1000, true, false, 34)
            };
        }
        void PrepareEnvironment(CombatArena arena)
        {
            var pit = new GameObject("ForestFallRecovery").AddComponent<BoxCollider2D>();
            pit.isTrigger = true; pit.size = new Vector2(200000, 3);
            pit.transform.position = new Vector3(0, -30, 0);
            arena.pits.Add(pit);
        }
        public int RebuildTestGroups(int members,int groups,float spacing,float memberSpacing,float firstOffset)
        {
            combat.ClearTestEnemies();
            // All four preview groups fit inside the starting streaming window.
            forest.EnsureChunks(combat.Player.spawn.x);
            Physics2D.SyncTransforms();
            for(int group=0;group<groups;group++)for(int member=0;member<members;member++)
            {
                float x=combat.Player.spawn.x+firstOffset+group*spacing+(member-(members-1)*.5f)*memberSpacing;
                float y=forest.data.FloorTop(x);
                if(y < -50)continue; // A void is not a spawn surface.
                // Ordinary testers only; the old boss dummy is deliberately excluded.
                var enemy=combat.SpawnEnemy(member%3==2?1:0,new Vector2(x,y+.03f));
                enemy.name="무리 "+(group+1)+" · "+(member+1);
                enemy.visual.sortingOrder = 34;
                enemy.aggressive = true;
                if (monsterHealthBar)
                {
                    var bar = Instantiate(monsterHealthBar, enemy.transform);
                    bar.transform.localPosition = Vector3.up * (enemy.size.y + .4f);
                }
                var art = enemy.profile == 1 ? flyingMonster : groundMonster;
                if (art && !enemy.isBoss)
                {
                    var skin = enemy.gameObject.AddComponent<ForestEnemyPresentation>();
                    skin.actor = enemy; skin.data = art;
                }
            }
            Physics2D.SyncTransforms();
            return combat.enemies.Count;
        }
        void PrepareChunk(InkForestChunk chunk)
        {
            foreach (var collider in chunk.GetComponentsInChildren<Collider2D>(true))
            {
                if (collider.isTrigger) continue;
                var surface = collider.GetComponent<ArenaSurface>();
                if (!surface) surface = collider.gameObject.AddComponent<ArenaSurface>();
                surface.oneWay = collider.GetComponent<PlatformEffector2D>() != null;
                combat.RegisterFeedbackSurface(collider);
            }
        }
        void BindHudSession()
        {
            hudSession = default;
            combatReads?.TryGetSession(out hudSession);
        }
        public BattleHudState Read()
        {
            state.region = "숲";
            state.exp = state.level = 0;
            state.maxEXP = 0;
            if (combatReads != null && combatReads.TryReadPlayer(hudSession, out var player))
            {
                state.hp = Mathf.CeilToInt(player.Vitals.Health);
                state.maxHP = Mathf.CeilToInt(player.Vitals.Maximum);
                state.weaponName = player.ActiveWeaponName;
            }
            else
            {
                state.hp = state.maxHP = 0;
                state.weaponName = null;
            }
            return state;
        }
        void OnDestroy()
        {
            CloseSession(GameSessionEndReason.Destroyed); UnwireLifetime();
            engineTime?.Dispose();
        }
    }
}
