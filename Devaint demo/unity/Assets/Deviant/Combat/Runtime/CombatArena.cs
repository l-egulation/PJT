using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
using Deviant.Definitions;
using System.Collections.Generic;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatArena : MonoBehaviour
    {
        readonly ArenaSurfaceQueryBuffer terrainQueries = new ArenaSurfaceQueryBuffer();
        public event System.Action Initialized;
        public event System.Action ResetCompleted;
        public ApprovedCombatAudio Audio { get; private set; }
        public CombatFeelFeedback FeelFeedback { get; private set; }
        // Per-action enemy push and player basic step (data/combat/attack_feel_motion.json).
        public CombatFeelMotion FeelMotion { get; private set; }
        public bool loadCharacterArt = true;
        public bool showPanel, TestConfigurationOpen, ExternalTestPanel, showDebugUI = true;
        public Font uiFont;
        public readonly HashSet<string> UnavailableTestForms = new HashSet<string>();
        public System.Action<Rect> TestEquipmentPanel;
        public System.Action DrawFeedbackOptions;
        public void DrawEquipmentPanel(Rect rect) => TestEquipmentPanel?.Invoke(rect);
        public CombatCompositionProfile CompositionProfile { get; private set; }
        public Material spriteMaterial { get; private set; }
        public CharacterAnimationCatalog characterAnimations;
        public RangedWeaponPresentation RangedPresentation { get; private set; }
        // Flat pixel shadows on the surface under the player and the enemies (Unity/ContactShadowPresentation).
        public ContactShadowPresentation ContactShadows { get; private set; }
        public ShieldSkillPresentation ShieldPresentation { get; private set; }
        public Catalog catalog;
        public LocomotionProfile Movement { get; private set; }
        public EnemyHitReactionDefinition EnemyHitReaction { get; private set; }
        public WeaponSwitchDefinition WeaponSwitch { get; private set; }
        public StealthAmbushDefinition StealthAmbush { get; private set; } = StealthAmbushDefinition.None;
        BasicMeleeDefinition basicSwordFirst;
        BasicMeleeCatalog basicMeleeCatalog;
        public BasicMeleeDefinition BasicSwordFirst => frameSwordTiming?.Action ?? basicSwordFirst;
        public BasicMeleeDefinition BasicSwordSecond => basicMeleeCatalog?.Find("base_sword_basic_1");
        public int BasicMeleeCount => basicMeleeCatalog?.Actions.Count ?? 0;
        public BasicMeleeDefinition GetBasicMelee(string formId, int index) => formId == "base_sword"
            ? index == 0 ? BasicSwordFirst : index == 1 ? BasicSwordSecond : null
            : basicMeleeCatalog?.Find(formId, index);
        public CombatPlayer Player { get; private set; }
        public float Now => simulation?.Clock.Now ?? 0;
        public long SimulationTickCount => simulation?.Clock.TickCount ?? 0;
        public bool SimulationFaulted => simulation != null && simulation.IsFaulted;
        public bool showBoxes;
        public readonly List<TestEnemy> enemies = new List<TestEnemy>();
        public readonly List<CombatActor> decoys = new List<CombatActor>();
        public readonly List<CombatCast> casts = new List<CombatCast>();
        public readonly List<ArenaProjectile> projectiles = new List<ArenaProjectile>();
        public readonly List<BombProjectile> bombs=new List<BombProjectile>();
        public readonly List<BombBurst> bombBursts=new List<BombBurst>();
        public readonly List<PlayerProjectile> playerShots=new List<PlayerProjectile>();
        public readonly List<LaserVisual> lasers=new List<LaserVisual>();
        public int PlayerShotCount;
        public int BombExplosionCount { get; private set; }
        public Vector2 LastBombExplosion { get; private set; }
        public readonly List<BoxCollider2D> pits = new List<BoxCollider2D>();
        readonly Queue<string> log = new Queue<string>();
        readonly List<CombatDebugFlash> flashes = new List<CombatDebugFlash>();
        public Queue<string>.Enumerator GetRecentEvents() => log.GetEnumerator();
        public int DebugFlashCount => flashes.Count;
        public CombatDebugFlash GetDebugFlash(int index) => flashes[index];
        Sprite square;
        public Sprite TechnicalSprite => square;
        public Sprite CircleSprite { get; private set; }
        Camera view;
        public Camera ViewCamera => view;
        public bool CombatActive { get { foreach (var e in enemies) if (e && !e.Dead && e.aggressive) return true; return false; } }

        bool initializing;
        public bool Initialize(CombatRuntimeConfiguration configuration)
        {
            foreach (bool valid in InitializeSteps(configuration)) if (!valid) return false;
            return true;
        }
        // The chapter keeps its loading screen up while heavy composition steps
        // yield. Tests and small arenas can still use the synchronous entry point.
        public System.Collections.IEnumerator InitializeInFrames(CombatRuntimeConfiguration configuration)
        {
            using (var scope = Controls.OpenScope("combat initialization"))
            {
                scope.BlockInput(Deviant.Control.Contracts.InputChannels.Combat | Deviant.Control.Contracts.InputChannels.Interaction);
                scope.PauseSimulation();
                foreach (bool valid in InitializeSteps(configuration))
                {
                    if (!valid) yield break;
                    if (CompositionProfile == CombatCompositionProfile.Unconfigured) yield return null;
                }
            }
        }
        IEnumerable<bool> InitializeSteps(CombatRuntimeConfiguration configuration)
        {
            if (initializing) throw new System.InvalidOperationException("Combat initialization is already running.");
            initializing = true;
            try
            {
                if (configuration == null) throw new System.ArgumentNullException(nameof(configuration));
                if (CompositionProfile != CombatCompositionProfile.Unconfigured) throw new System.InvalidOperationException("Combat runtime already has a composition owner.");
                if (configuration.Profile == CombatCompositionProfile.Unconfigured || !configuration.Camera || configuration.Enemies == null || configuration.Loadout == null)
                    throw new System.ArgumentException("A composition profile, scene camera, explicit enemy list and equipment loadout owner are required.", nameof(configuration));
                simulation = new CombatSimulation(new ArenaTickWorld(this));
                catalog = configuration.Catalog;
                if (!LocomotionProfile.TryCreate(configuration.Locomotion, out var movement, out var movementError))
                { Debug.LogError(movementError); enabled = false; yield return false; yield break; }
                Movement = movement;
                characterAnimations = configuration.Animations;
                spriteMaterial = configuration.SpriteMaterial;
                if (catalog == null || catalog.forms == null || catalog.forms.Length < 7) { Debug.LogError("Invalid combat catalog"); enabled = false; yield return false; yield break; }
                if (!EnemyHitReactionDefinition.TryCreate(catalog.enemyHitReaction, out var enemyHitReaction, out var reactionError))
                { Debug.LogError(reactionError); enabled = false; yield return false; yield break; }
                EnemyHitReaction = enemyHitReaction;
                if (!WeaponSwitchDefinition.TryCreate(catalog.weaponSwitch, out var weaponSwitch, out var switchError))
                { Debug.LogError(switchError); enabled = false; yield return false; yield break; }
                WeaponSwitch = weaponSwitch;
                if (!StealthAmbushDefinition.TryCreate(catalog.stealthAmbush, out var stealthAmbush, out var ambushError))
                { Debug.LogError(ambushError); enabled = false; yield return false; yield break; }
                StealthAmbush = stealthAmbush;
                if (!BasicMeleeCatalog.TryCreate(catalog, out var meleeBasics, out var basicError))
                { Debug.LogError(basicError); enabled = false; yield return false; yield break; }
                var first = meleeBasics.Find("base_sword", 0); var second = meleeBasics.Find("base_sword", 1);
                if (first == null || second == null) { Debug.LogError("Both sword basic hits are required."); enabled = false; yield return false; yield break; }
                basicMeleeCatalog = meleeBasics; basicSwordFirst = first;
                yield return true;
                if (!InitializeSwordFrameSource(out var frameSourceError)) { Debug.LogError(frameSourceError); enabled = false; yield return false; yield break; }
                if (!InitializeDirectMeleeSpecials() || !InitializeChargedMelee()) { enabled = false; yield return false; yield break; }
                yield return true;
                if (!InitializeRangedActions()) { enabled = false; yield return false; yield break; }
                if (!InitializeActionTracks(out var actionTrackError)) { Debug.LogError(actionTrackError); enabled = false; yield return false; yield break; }
                yield return true;
                if (!InitializeAuthoredSkills(out var authoredError)) { Debug.LogError(authoredError); enabled = false; yield return false; yield break; }
                yield return true;
                if (!InitializeMotionRegistry(out var motionRegistryError)) { Debug.LogError(motionRegistryError); enabled = false; yield return false; yield break; }
                yield return true;
                if (!InitializeMeleeFrames(out var meleeFrameError)) { Debug.LogError(meleeFrameError); enabled = false; yield return false; yield break; }
                yield return true;
                if (!InitializeSpecialFrames(characterAnimations?.specialFrames, out var specialFrameError)) { Debug.LogError(specialFrameError); enabled = false; yield return false; yield break; }
                if(loadCharacterArt && characterAnimations?.sets!=null && System.Array.Exists(characterAnimations.sets,set=>set.gameplayForm=="launcher"))RangedPresentation=new RangedWeaponPresentation(this);
                yield return true;
                square = Sprite.Create(Texture2D.whiteTexture, new Rect(0,0,Texture2D.whiteTexture.width,Texture2D.whiteTexture.height), Vector2.one * .5f, Texture2D.whiteTexture.width);
                var circleTexture=new Texture2D(32,32,TextureFormat.RGBA32,false); circleTexture.filterMode=FilterMode.Point;
                var pixels=new Color[32*32];
                for(int y=0;y<32;y++) for(int x=0;x<32;x++) pixels[y*32+x]=Vector2.Distance(new Vector2(x,y),new Vector2(15.5f,15.5f))<=15.5f ? Color.white : Color.clear;
                circleTexture.SetPixels(pixels); circleTexture.Apply();
                CircleSprite=Sprite.Create(circleTexture,new Rect(0,0,32,32),Vector2.one*.5f,32);
                view = configuration.Camera;
                InitializeCameraOutput();
                configuration.PrepareEnvironment?.Invoke(this);
                yield return true;
                Player = new GameObject("CombatV2Player").AddComponent<CombatPlayer>();
                Player.BindLoadout(configuration.Loadout);
                Player.Configure(this, configuration.PlayerSpawn, configuration.PlayerSize, configuration.PlayerOffset, true); Player.spawn = Player.Position;
                simulation.BindInputIngress(Player.InputIngress);
                configuration.BindPlayerInput?.Invoke(Player);
                var playerSprite = configuration.PlayerSprite;
                Player.visual = Visual(Player.transform, playerSprite ? playerSprite : square, Color.white,
                    playerSprite ? Vector2.one : configuration.PlayerSize, playerSprite ? Vector2.zero : Vector2.up);
                Player.visual.sortingOrder = configuration.PlayerSortingOrder;
                if (configuration.PlayerMaterial) Player.visual.sharedMaterial = configuration.PlayerMaterial;
                Player.weapons[1].FormIndex = configuration.SecondaryFormIndex;
                int spawned = 0;
                foreach (var enemy in configuration.Enemies)
                {
                    SpawnEnemy(enemy, configuration.EnemyFactory);
                    if (++spawned % 8 == 0) yield return true;
                }
                Player.ResetPlayer(); Physics2D.SyncTransforms();
                Audio = new ApprovedCombatAudio(this);
                if (!configuration.Camera.GetComponent<AudioListener>()) configuration.Camera.gameObject.AddComponent<AudioListener>();
                if (commands == null) commands = new ArenaCombatCommands(this);
                FeelFeedback = gameObject.AddComponent<CombatFeelFeedback>();
                PlayerHitReaction=PlayerHitReactionSettings.Load();
                FeelMotion=CombatFeelMotion.Load();
                FeelFeedback.Bind(this, CombatFeelSettings.Load());
                ShieldPresentation = new ShieldSkillPresentation(this);
                try { configuration.BindFeedback?.Invoke(this); }
                catch (System.Exception error) { Debug.LogWarning("Hit feedback composition failed: " + error.Message); }
                CompositionProfile = configuration.Profile;
                initializing = false;
                Initialized?.Invoke();
                yield return true;
            }
            finally { initializing = false; }
        }
        SpriteRenderer Visual(Transform parent, Sprite sprite, Color color, Vector2 scale, Vector2 position)
        {
            var child = new GameObject("Visual"); child.transform.SetParent(parent,false); child.transform.localPosition = position; child.transform.localScale = scale;
            var r = child.AddComponent<SpriteRenderer>(); r.sprite = sprite; r.color = color; r.sortingOrder = 10;
            if (spriteMaterial) r.sharedMaterial = spriteMaterial; return r;
        }
        void SpawnEnemy(CombatEnemySpawn request, System.Func<CombatArena, CombatEnemySpawn, TestEnemy> factory)
        {
            if (factory != null)
            {
                var supplied = factory(this, request);
                if (!supplied) throw new System.InvalidOperationException("The configured enemy factory must return an initialized actor.");
                enemies.Add(supplied);
                return;
            }
            var enemy = new GameObject(request.Name).AddComponent<TestEnemy>();
            enemy.Configure(this, request.Position, request.Size, request.Offset, false);
            enemy.profile = request.Profile; enemy.isBoss = request.IsBoss; enemy.aggressive = request.Aggressive; enemy.maxHealth = request.MaxHealth;
            enemy.spawn = request.Position;
            enemy.visual = Visual(enemy.transform, square, Color.white, request.Size, request.Offset);
            enemy.visual.sortingOrder = request.SortingOrder;
            enemy.ResetEnemy(); enemies.Add(enemy);
        }
        public TestEnemy SpawnEnemy(int profile, Vector2 at)
        {
            var e = new GameObject(profile == 2 ? "BossDummy" : profile == 1 ? "RangedTester" : "MeleeTester").AddComponent<TestEnemy>();
            Vector2 size = profile == 2 ? new Vector2(5.3f,3.8f) : profile == 1 ? new Vector2(1.3f,1.2f) : new Vector2(2.65f,1.9f);
            Vector2 offset = profile == 2 ? new Vector2(.36f,1.9f) : profile == 1 ? new Vector2(.45f,1.25f) : new Vector2(.18f,.95f);
            e.Configure(this,at,size,offset,false); e.profile = profile; e.isBoss = profile == 2; e.maxHealth = profile == 2 ? 1000 : profile == 1 ? 60 : 100;
            e.spawn = at; e.visual = Visual(e.transform,square,Color.white,size,new Vector2(offset.x,offset.y)); e.ResetEnemy(); enemies.Add(e);
            return e;
        }
        public void ClearTestEnemies()
        {
            foreach(var enemy in enemies)
            {
                if(!enemy)continue;
                enemy.CancelAttack(); enemy.gameObject.SetActive(false);
                Destroy(enemy.gameObject);
            }
            enemies.Clear();
        }
        float localFeedbackBudget;
        void FixedUpdate()
        {
            if (initializing || CompositionProfile == CombatCompositionProfile.Unconfigured || !Player || ManualSimulationActive || SimulationFaulted || Controls.Current.SimulationPaused ||
                FeelFeedback && FeelFeedback.HitStopped) return;
            localFeedbackBudget+=Time.fixedDeltaTime*(Controls.EngineTimeBound?1:Controls.Current.SimulationScale);
            for(int steps=0;steps<32 && localFeedbackBudget>=Time.fixedDeltaTime;steps++)
            {localFeedbackBudget-=Time.fixedDeltaTime;BeginFeedbackStep();simulation.Step(Time.fixedDeltaTime);NotifyCompletedStep(Time.fixedDeltaTime);}
        }
        void Update()
        {
            if (!initializing && CompositionProfile != CombatCompositionProfile.Unconfigured && Player && !ManualSimulationActive) AdvanceFeedbackAccent(Time.unscaledDeltaTime);
            if (CompositionProfile != CombatCompositionProfile.Unconfigured) WarmEquippedMedia();
        }
        void LateUpdate()
        {
            if (initializing || CompositionProfile == CombatCompositionProfile.Unconfigured || !Player || !view) return;
            Presentation.Draw(Time.deltaTime);
            (ContactShadows ??= new ContactShadowPresentation(this)).Tick();
            ShieldPresentation?.Tick();
            RangedPresentation?.Tick();
            Audio?.Tick();
            RefreshInkStatusVisuals();
            RefreshWhipSkillVisuals();
        }
        public List<CombatActor> TargetsInBox(Vector2 center, Vector2 size)
        {
            var result = new List<CombatActor>();
            foreach (var c in Physics2D.OverlapBoxAll(center,size,0))
            { var h = c.GetComponent<HurtRegion>(); if (h && h.actor && !result.Contains(h.actor)) result.Add(h.actor); }
            return result;
        }
        public CombatActor NearestInBox(CombatActor owner, BoxData box)
            => box == null ? null : NearestInBox(owner, new Vector2(box.x * owner.facing,box.y), new Vector2(box.width,box.height));
        public CombatActor NearestInBox(CombatActor owner, SkillBoxDefinition box)
            => box == null ? null : NearestInBox(owner, new Vector2(box.X * owner.facing,box.Y), new Vector2(box.Width,box.Height));
        CombatActor NearestInBox(CombatActor owner, Vector2 offset, Vector2 size)
        {
            var host = new NearestWorldTargetHost(this, owner); float distance = float.PositiveInfinity;
            Vector2 center = owner.Position + offset;
            foreach (var target in TargetsInBox(center,size))
            {
                host.Target = target;
                if (WorldHitQueryRules.TrySelectBox(ref host, ref distance)) host.Best = target;
            }
            return host.Best;
        }
        public CombatActor NearestInCircle(CombatActor owner,float radius, bool ignoreOcclusion = false)
        {
            var host = new NearestWorldTargetHost(this, owner); float distance = float.PositiveInfinity;
            foreach(var target in enemies)
            {
                host.Target = target;
                if (WorldHitQueryRules.TrySelectCircle(radius, ref host, ref distance, ignoreOcclusion)) host.Best = target;
            }
            return host.Best;
        }
        public bool Occluded(Vector2 from, Vector2 to, bool includeOneWay = false)
        {
            terrainQueries.Linecast(from,to);
            for(int i=0;i<terrainQueries.Hits.Count;i++)
                if (terrainQueries.Hits[i].collider.TryGetComponent<ArenaSurface>(out var s) && (includeOneWay || !s.oneWay)) return true;
            return false;
        }
        public bool Exposed(Vector2 origin, CombatActor target, Vector2 center, Vector2 size)
            => Exposed(origin, target, center, size, out _, 0);
        public bool Exposed(Vector2 origin, CombatActor target, Vector2 center, Vector2 size, out CombatHitContact contact, int incoming)
        {
            Bounds bounds = target.hurtCollider.bounds;
            contact = default;
            var host=new WorldHitVisibilityHost(this);
            bool exposed=WorldHitQueryRules.Exposed(new WorldHitPoint(origin.x,origin.y),new WorldHitBounds(bounds.min.x,bounds.min.y,bounds.max.x,bounds.max.y),
                new WorldHitBox(center.x,center.y,size.x,size.y),ref host,out var point);
            if(exposed)contact=new CombatHitContact(point.X,point.Y,incoming,0,HitAnchorKind.OverlapRepresentative);
            return exposed;
        }
        public bool SolidOverlap(Vector2 at, Vector2 size)
        {
            terrainQueries.OverlapBox(at,size);
            for(int i=0;i<terrainQueries.Overlaps.Count;i++)
                if (terrainQueries.Overlaps[i].TryGetComponent<ArenaSurface>(out var s) && !s.oneWay) return true;
            return false;
        }
        public float SolidTravel(Vector2 at, Vector2 size, Vector2 direction, float distance)
        {
            float allowed = distance;
            terrainQueries.BoxCast(at,size,direction,distance);
            for(int i=0;i<terrainQueries.Hits.Count;i++)
            {
                var h=terrainQueries.Hits[i];
                if (h.collider.TryGetComponent<ArenaSurface>(out var s) && !s.oneWay) allowed = Mathf.Min(allowed,Mathf.Max(0,h.distance - .005f));
            }
            return allowed;
        }
        public bool SkillProjectileBlocked(Vector2 at,Vector2 size)
        {
            Vector2 inner=size-Vector2.one*.02f;
            var bounds=new Bounds(at,new Vector3(inner.x,inner.y,1));
            terrainQueries.OverlapBox(at,inner);
            for(int i=0;i<terrainQueries.Overlaps.Count;i++)
            {
                var c=terrainQueries.Overlaps[i];
                if(c.TryGetComponent<ArenaSurface>(out var s) && !s.oneWay && bounds.Intersects(c.bounds)) return true;
            }
            return false;
        }
        public float SkillProjectileTravel(Vector2 at,Vector2 size,Vector2 direction,float distance)
        {
            float allowed=distance;
            terrainQueries.BoxCast(at,size-Vector2.one*.02f,direction,distance);
            for(int i=0;i<terrainQueries.Hits.Count;i++)
            {
                var h=terrainQueries.Hits[i]; var c=h.collider;
                if(!(c.GetComponent<ArenaSurface>() is ArenaSurface s) || s.oneWay) continue;
                if(c.bounds.max.y<=at.y-size.y/2+.01f || c.bounds.min.y>=at.y+size.y/2-.01f) continue;
                allowed=Mathf.Min(allowed,Mathf.Max(0,h.distance-.005f));
            }
            return allowed;
        }
        // Loot spreads sideways only as far as open space allows, so items beside walls or stair risers
        // stay on reachable floor instead of landing inside terrain.
        public Vector2 DropPoint(Vector2 origin, float offsetX, float footprint = .5f)
        {
            if (Mathf.Abs(offsetX) < .001f) return origin;
            float direction = Mathf.Sign(offsetX);
            float clear = SkillProjectileTravel(origin + Vector2.up * .6f, new Vector2(footprint, footprint), Vector2.right * direction, Mathf.Abs(offsetX));
            return origin + Vector2.right * (direction * clear);
        }
        public float? FindFloor(Vector2 at, float distance)
        {
            terrainQueries.Raycast(at + Vector2.up * .03f,Vector2.down,distance + .03f);
            for(int i=0;i<terrainQueries.Hits.Count;i++)
            {
                var h=terrainQueries.Hits[i];
                if (h.collider.TryGetComponent<ArenaSurface>(out _) && h.normal.y > .8f) return h.point.y;
            }
            return null;
        }
        public CombatActor EnemyTarget(TestEnemy enemy)
        {
            if (!enemy.isBoss)
            {
                CombatActor best = null; float distance = float.PositiveInfinity;
                foreach (var d in decoys)
                {
                    if (!d || d.Dead) continue;
                    float current = Vector2.Distance(d.Center,enemy.Center);
                    if (current < (d.DecoyDefinition?.Radius ?? 5) && current < distance) { best = d; distance = current; }
                }
                if (best) return best;
            }
            return Player && !Player.Dead && !Player.Hidden ? Player : null;
        }
        public void FireEnemyProjectile(CombatActor owner, Vector2 at, Vector2 velocity, float damage, CombatHitOrigin hitOrigin=default)
        {
            var p = new GameObject("TestProjectile").AddComponent<ArenaProjectile>(); p.Initialize(this,owner,at,velocity,damage,Now+3,hitOrigin);
            Visual(p.transform,square,Color.red,Vector2.one * .25f,Vector2.zero); projectiles.Add(p);
        }
        public void FirePlayerRanged(CombatActor owner,RangedData data,float damage,int direction,WeaponState sourceWeapon=null,CombatHitOrigin hitOrigin=default)
            => FirePlayerRanged(owner,new SkillRangedLaunchDefinition(data),damage,direction,sourceWeapon,hitOrigin);
        public void FirePlayerRanged(CombatActor owner,SkillRangedLaunchDefinition data,float damage,int direction,WeaponState sourceWeapon=null,CombatHitOrigin hitOrigin=default)
        {
            if(data.Projectile.Type=="ball" || data.Projectile.Type=="charge_ball" || data.Projectile.Type=="shotgun")RangedPresentation?.Muzzle(owner,data.Projectile.Type=="shotgun"?"launcher_shotgun":"launcher");
            if(data.Projectile.Type=="ball" || data.Projectile.Type=="charge_ball" || data.Projectile.Type=="shotgun")RangedPresentation?.FirePop(owner,data);
            var host=new RangedLaunchHost(this,owner,data,damage,direction,sourceWeapon,hitOrigin.IsSpecified ? hitOrigin : DirectHitOrigin(owner));
            RangedLaunchExecution.Fire(ref host);
        }
        void FireLaser(CombatActor owner,SkillRangedLaunchDefinition data,float damage,int direction,Vector2 origin,Vector2 muzzle,CombatHitOrigin hitOrigin)
        {
            var host = new LaserHitHost(this, owner, data, damage, direction, origin, muzzle, hitOrigin.WithEffect(System.Guid.NewGuid(), CombatHitKind.Laser));
            LaserHitExecution.Fire(ref host);
        }
        public BombProjectile SpawnBomb(CombatActor owner,BombData data,float damage,int direction,float fragmentMultiplier=1,CombatHitOrigin hitOrigin=default)
            => SpawnBomb(owner,new SkillBombLaunchDefinition(data),damage,direction,fragmentMultiplier,hitOrigin);
        public BombProjectile SpawnBomb(CombatActor owner,SkillBombLaunchDefinition data,float damage,int direction,float fragmentMultiplier=1,CombatHitOrigin hitOrigin=default)
        {
            var host=new BombLaunchHost(this,owner,data,damage,direction,hitOrigin.IsSpecified ? hitOrigin : DirectHitOrigin(owner));
            RangedLaunchExecution.SpawnBomb(fragmentMultiplier,ref host);
            if(host.Bomb && host.Bomb.approvedGrenade){RangedPresentation?.Muzzle(owner,"launcher_grenade");RangedPresentation?.GrenadePop(owner);}
            return host.Bomb;
        }
        public static bool IsBombBasicAttack(CombatHitOrigin? origin)
        {
            string id = origin.HasValue ? origin.Value.ActionId : null;
            return id != null && id.StartsWith("bomb_basic_", System.StringComparison.Ordinal) && !id.EndsWith("_special", System.StringComparison.Ordinal);
        }
        public void BombExploded(Vector2 at,float radius,bool approvedGrenade=false,Vector2? visualContact=null,CombatHitOrigin? fxOrigin=null,int facing=1)
        {
            BombExplosionCount++; LastBombExplosion=at; Record("bomb impact explosion at="+at+" radius="+radius);
            if (radius < 1) Audio?.Emit("explosion_fragment", at);
            else if (!(fxOrigin.HasValue && Audio != null && Audio.EmitSkillEvent(fxOrigin.Value.ActionId, "explosion", at)))
                // A bomb basic attack sounds like one bomb of the triple throw (explosion_bomb), whatever its radius
                // (its blast radius 3 used to select the large explosion; user decision 2026-11-16).
                Audio?.Emit(radius < 1 ? "explosion_fragment" : approvedGrenade ? "explosion_grenade"
                    : IsBombBasicAttack(fxOrigin) ? "explosion_bomb" : radius >= 2.8f ? "explosion_large" : "explosion_bomb", at);
            if(approvedGrenade && RangedPresentation!=null && RangedPresentation.Blast(visualContact??at,radius)) return;
            // Approved bomb skill frames replace the placeholder's picture. The burst object itself stays:
            // it is the explosion record (radius, position) the arena and its tests read.
            bool approvedFx=!approvedGrenade && fxOrigin.HasValue && BombSkillFx.TryBlast(this,at,fxOrigin.Value,facing,radius);
            ShowExplosion(at,radius,false);
            if(approvedFx && bombBursts.Count>0 && bombBursts[bombBursts.Count-1] && bombBursts[bombBursts.Count-1].visual) bombBursts[bombBursts.Count-1].visual.enabled=false;
        }
        // Explosive shuriken: the approved blast frames replace the placeholder's picture; the burst stays as the record.
        public void ShowShurikenExplosion(Vector2 at,float radius,int facing,bool approvedArt)
        {
            bool approvedFx=approvedArt && ShurikenSkillFx.TryBlast(this,at,facing);
            bool selected = Audio != null && Audio.EmitSkillEvent("shuriken_range_special", "explosion", at);
            ShowExplosion(at,radius,!selected);
            if(approvedFx && bombBursts.Count>0 && bombBursts[bombBursts.Count-1] && bombBursts[bombBursts.Count-1].visual) bombBursts[bombBursts.Count-1].visual.enabled=false;
        }
        public void ShowExplosion(Vector2 at,float radius,bool playAudio=true,float boxSize=0)
        {
            if(playAudio)Audio?.Emit("explosion_bomb",at);
            var b=new GameObject("BombExplosion").AddComponent<BombBurst>(); b.arena=this; b.radius=radius; b.started=Now; b.transform.position=at;
            b.visual=b.gameObject.AddComponent<SpriteRenderer>(); b.visual.sprite=boxSize > 0 ? square : CircleSprite; b.boxSize=boxSize; b.visual.sortingOrder=30;
            if(spriteMaterial) b.visual.sharedMaterial=spriteMaterial;
            bombBursts.Add(b); b.Step();
        }
        public void SpawnDecoy(CombatActor owner, Vector2 at, EffectData effect)
            => SpawnDecoy(owner,at,new SkillEffectDefinition(effect));
        public void SpawnDecoy(CombatActor owner, Vector2 at, SkillEffectDefinition effect, string definitionVersion = null, bool authoredGeometry = false)
        {
            foreach (var old in decoys) if (old) { old.GetComponent<ShieldDecoyRuntime>()?.Discard(); Destroy(old.gameObject); } decoys.Clear();
            var box = authoredGeometry ? effect.Box : null;
            var size = box == null ? new Vector2(.8f, 1.5f) : new Vector2(box.Width, box.Height);
            var offset = box == null ? new Vector2(0, .75f) : new Vector2(box.X, box.Y);
            var d = new GameObject("Decoy").AddComponent<CombatActor>(); d.isDecoy = true; d.DecoyDefinition = authoredGeometry ? effect : null; d.DecoyDefinitionVersion = definitionVersion; d.Configure(this, at, size, offset, false);
            d.maxHealth = d.health = effect.Hp; d.expiredAt = Now + effect.Duration; d.visual = Visual(d.transform,square,Color.cyan,size,offset); decoys.Add(d);
            d.gameObject.AddComponent<ShieldDecoyRuntime>().Bind(d, owner);
            if (owner == Player) Audio?.EmitSkillEvent("shield_follow_up", "placed", at);
        }
        public void BreakStealth(CombatActor owner)
        {
            if (owner.stealthUntil > Now) owner.stealthExitHasteUntil = Now + StealthAmbush.ExitSeconds;
            owner.ClearConcealment();
            foreach (var c in casts) if (c && c.owner == owner && c.Definition.Effect != null && c.Definition.Effect.Type == "smoke") c.MarkSmokeSpent();
        }
        public void ReplaceSmoke(CombatCast cast)
        {
            foreach (var old in casts.ToArray()) if (old && old != cast && old.owner == cast.owner && old.Definition.Effect != null && old.Definition.Effect.Type == "smoke") { old.ReleaseResources(); old.EndCommand(CombatCommandState.Cancelled, CombatCommandReason.Replaced); casts.Remove(old); Destroy(old.gameObject); }
        }
        public void RemoveOwnedEffects(CombatActor actor)
        {
            Presentation.Reset();
            RangedPresentation?.Reset();
            foreach(var p in playerShots.ToArray()) if(p && p.owner==actor) p.Release();
            foreach(var l in lasers) if(l) Destroy(l.gameObject); lasers.Clear();
            foreach (var c in casts.ToArray()) if (c && c.owner == actor) { c.ReleaseResources(); c.EndCommand(CombatCommandState.Cancelled, actor.Dead ? CombatCommandReason.Dead : CombatCommandReason.Interrupted); casts.Remove(c); Destroy(c.gameObject); }
            foreach (var p in projectiles.ToArray()) if (p && p.source == actor) p.Release();
            foreach(var b in bombs.ToArray()) if(b && b.owner==actor) b.Release();
            foreach(var b in bombBursts) if(b) Destroy(b.gameObject); bombBursts.Clear(); BombSkillFx.Clear(this);
            foreach (var d in decoys) if (d) Destroy(d.gameObject); decoys.Clear();
        }
        // The first sword hit is the initial versioned action. Existing executions
        // retain their own start definition and version; only a new execution reads this value.
        public bool TryApplyBasicSwordFirst(BasicData candidate, bool resetScenario, out string error)
        {
            if (!Player) { error = "Combat runtime is not initialized."; return false; }
            if (frameSwordTiming != null) { error = "Restore fixed sword timing before applying a separate numeric definition."; return false; }
            if (resetScenario)
                try { simulation.BeginResetPreparation(); } catch (System.InvalidOperationException exception) { error = exception.Message; return false; }
            try
            {
                if (!BasicMeleeDefinition.TryCreate("base_sword_basic_0", candidate, out var next, out error)) return false;
                if (!TryRecompileMeleeFrameNumerics(null, next, null, out var frames, out error)) return false;
                if (resetScenario) simulation.ValidateResetAdmission();
                basicSwordFirst = next;
                PublishMeleeFrameNumerics(frames);
            }
            catch (System.InvalidOperationException exception) { error = exception.Message; return false; }
            finally { if (resetScenario) simulation.EndResetPreparation(); }
            if (resetScenario) ResetTest();
            return true;
        }
        public bool TryApplyBasicMelee(IReadOnlyList<BasicMeleeDefinition> candidates, bool resetScenario, out string error)
        {
            if (!Player || basicMeleeCatalog == null) { error = "Combat runtime is not initialized."; return false; }
            if (resetScenario)
                try { simulation.BeginResetPreparation(); } catch (System.InvalidOperationException exception) { error = exception.Message; return false; }
            try
            {
                if (candidates != null)
                    for (int i = 0; i < candidates.Count; i++)
                        if (candidates[i]?.Id == "base_sword_basic_0")
                        { error = "Use Basic Sword First Hit and its existing frame timing guards for the first sword hit."; return false; }
                if (candidates != null)
                    foreach (var candidate in candidates)
                    {
                        var frame = candidate == null ? null : GetMeleeFrame(candidate.Id, true);
                        if (frame?.Mode == MeleeTimingMode.FrameTime && (candidate.Impact != frame.NumericAction.Impact || candidate.Active != frame.NumericAction.Active || candidate.Duration != frame.NumericAction.Duration))
                        { error = "Active FrameTime hit/end values are owned by action markers. Numeric timing edits are read-only until explicit fixed mode return."; return false; }
                    }
                if (!basicMeleeCatalog.TryReplace(candidates, out var next, out error)) return false;
                if (!TryRecompileMeleeFrameNumerics(next, null, null, out var frames, out error)) return false;
                if (resetScenario) simulation.ValidateResetAdmission();
                basicMeleeCatalog = next;
                PublishMeleeFrameNumerics(frames);
            }
            catch (System.InvalidOperationException exception) { error = exception.Message; return false; }
            finally { if (resetScenario) simulation.EndResetPreparation(); }
            if (resetScenario) ResetTest();
            return true;
        }
        // Reset-only application is the initial safe editing boundary. No live action
        // or independent effect is allowed to retain a half-edited movement profile.
        public bool TryApplyTestRunSpeed(string text, out string error)
        {
            if (Movement == null || !float.TryParse(text, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var speed) ||
                float.IsNaN(speed) || float.IsInfinity(speed) || speed < 0)
            { error = "이동속도는 0 이상의 유한한 숫자로 입력하세요. (유닛/초)"; return false; }
            var candidate = Movement.ToData();
            candidate.runSpeed = speed;
            return TryApplyMovementAndReset(candidate, out error);
        }
        public bool TryApplyMovementAndReset(LocomotionData candidate, out string error)
        {
            if (!Player) { error = "Combat runtime is not initialized."; return false; }
            if (!LocomotionProfile.TryCreate(candidate, out var next, out error)) return false;
            try { simulation.BeginResetPreparation(); }
            catch (System.InvalidOperationException exception) { error = exception.Message; return false; }
            ICombatMovementValidationTransaction validation = null;
            try
            {
                var binding = movementValidation;
                if (binding != null && !binding.Validation.TryPrepare(next, out validation, out error)) return false;
                if (binding != null && validation == null)
                { error = "The movement validation owner accepted without a prepared transaction."; return false; }
                if (!isActiveAndEnabled || !ReferenceEquals(binding, movementValidation))
                { error = "The runtime movement validation owner changed during preflight."; return false; }
                simulation.ValidateResetAdmission();
                if (validation != null && !validation.TryCommit(out error)) return false;
                Movement = next;
            }
            catch (System.InvalidOperationException exception) { error = exception.Message; return false; }
            finally { try { validation?.Dispose(); } finally { simulation.EndResetPreparation(); } }
            ResetTest();
            return true;
        }
        public void ResetTest() => simulation.Reset(Deviant.Combat.Contracts.SimulationResetMode.KeepTime);
        void ResetActorsAndEffects(SimulationResetMode mode)
        {
            BeginEnvironmentStep();
            localFeedbackBudget=0;
            ResetInkParts();
            ResetMaintenanceAugmentState();
            Player.ResetInputSession(mode == SimulationResetMode.NewEpoch);
            Presentation.Reset();
            RangedPresentation?.Reset();
            for(int i=playerShots.Count-1;i>=0;i--) if(playerShots[i]) playerShots[i].Release(); playerShots.Clear(); PlayerShotCount=0;
            foreach(var l in lasers) if(l) Destroy(l.gameObject); lasers.Clear();
            foreach (var c in casts) if (c) { c.ReleaseResources(); c.EndCommand(CombatCommandState.Cancelled, CombatCommandReason.Reset); Destroy(c.gameObject); } casts.Clear();
            for(int i=projectiles.Count-1;i>=0;i--) if(projectiles[i]) projectiles[i].Release(); projectiles.Clear();
            for(int i=bombs.Count-1;i>=0;i--) if(bombs[i]) bombs[i].Release(); bombs.Clear();
            foreach(var b in bombBursts) if(b) Destroy(b.gameObject); bombBursts.Clear(); BombSkillFx.Clear(this); BombExplosionCount=0;
            foreach (var d in decoys) if (d) Destroy(d.gameObject); decoys.Clear();
            Player.ResetPlayerInInputSession(); foreach (var e in enemies) e.ResetEnemy(); flashes.Clear();
            ResetCompleted?.Invoke();
            Audio?.Reset();
            Physics2D.SyncTransforms(); Record("TEST RESET: gear preserved; no saves/currency/rewards written");
        }
        public void Record(string text)
        {
            string line = Now.ToString("0.000") + " " + text; log.Enqueue(line); while (log.Count > 10) log.Dequeue();
            // Same console line (tests and the combat lab read it through logMessageReceived) without a managed stack
            // trace: Record runs on every attack, weapon switch and wall-jump check, and the trace capture plus the
            // Editor console made each one a visible stall in play (QA 2026-11-16).
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", "[CombatV2] " + line);
        }
        public void AddFlash(Vector2 position, Vector2 size, float duration, Color color)
        { flashes.Add(new CombatDebugFlash(new Bounds(position,size), Now + duration, color)); }
    }
}
