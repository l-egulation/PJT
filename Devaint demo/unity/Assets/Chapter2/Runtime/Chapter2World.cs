using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Control.Contracts;
using Deviant.Definitions;
using Deviant.Equipment;
using Deviant.Game.Diagnostics;
using Deviant.Integration;
using Deviant.Inventory;
using Deviant.Inventory.Unity;
using Deviant.Presentation.Contracts;
using Deviant.Presentation.Unity;
using Deviant.Run;
using Deviant.Stage1Monsters.Game;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Deviant.Chapter2
{
    [DefaultExecutionOrder(500)]
    public sealed class Chapter2World : MonoBehaviour
    {
        public CombatArena arena; public Camera sceneCamera; public CombatDiagnostics diagnostics;
        public Material spriteMaterial; public Shader terrainShader,windowCityShader; public Sprite playerSprite,bossSprite; public Chapter1TeamTerrainPalette terrainPalette;
        public Stage1VillageMonsterContent monsterContent; public int seed=-1; public string family;
        public bool Ready {get;private set;} public C2Layout Layout {get;private set;} public Chapter2SewerPlan Sewer {get;private set;} public Chapter2RoomState Rooms {get;private set;}
        public string CurrentRoom {get;private set;} public RoseBlueEncounter Boss {get;private set;}
        public Chapter2EliteEnemy Elite {get;private set;}
        public bool ElitePursuing=>eliteEngaged&&!eliteStopped&&Elite&&!Elite.Dead&&!arena.Player.Dead;
        public bool InBossRoom=>CurrentRoom=="B";
        bool eliteEngaged,eliteStopped;
        public RunEconomy Economy {get;private set;} public RunStash Stash {get;private set;} public MaintenanceService Shop {get;private set;}
        public bool Complete {get;private set;} public IReadOnlyDictionary<string,TestEnemy> Actors=>actors;
        readonly Dictionary<string,TestEnemy> actors=new Dictionary<string,TestEnemy>();readonly Dictionary<TestEnemy,string> owners=new Dictionary<TestEnemy,string>();readonly HashSet<TestEnemy> counted=new HashSet<TestEnemy>();
        CameraOutputBinding output;CameraOutputBinding.BaseSource follow;IDisposable engineTime;IControlScope shopScope;
        StaticArenaSurfaceIndex surfaces;System.Random rewards;float previousStep,started,deathAt=-1;Vector3 cameraVelocity;string message;
        readonly Chapter2CameraFraming cameraFraming=new Chapter2CameraFraming();
        PlayerDeathPresentation deathPresentation;
        // The boss weapon choice waits for Blue's defeat pose (QA 2026-11-13) and a clear blacks out like a death before
        // its result; the player holds still through both waits.
        IControlScope bossOfferWait,clearWait;public bool BossOfferPending=>bossOfferWait!=null;
        Chapter2Carry carry;IControlScope resultScope;public RunSession SourceRun {get;private set;} public int Kills {get;private set;} public int PigmentEarned {get;private set;} public bool ShowingResult=>resultScope!=null;public double Elapsed=>arena?arena.Now-started:0;
        public bool ExternalPresentation {get;set;} public bool ExternalInput {get;set;} public bool ShopOpen=>shopScope!=null; public string Message=>message; public Chapter1FullMap FullMap {get;private set;}
        // 봉인 도전방과 잠긴 상자·열쇠 운반자(stage-gimmicks.md, 2챕터 적용 사용자 결정 2026-11-13)
        public Chapter2GimmickHost Gimmicks {get;private set;} public Chapter1SealChallenge Seal {get;private set;} public Chapter1KeyChest Keys {get;private set;}
        readonly HashSet<TestEnemy> gimmickSpawned=new HashSet<TestEnemy>();int gimmickCount;

        InventoryRuntimeHost Bag=>arena?arena.GetComponent<InventoryRuntimeHost>():null;
        // Composed under the loading screen over several frames, like chapter 1 (FrameSlice): the steps before Ready start
        // a new frame once a frame's share is spent, with input and simulation held by this scope until Start ends.
        System.Collections.IEnumerator Start()
        {
            if(!arena||!sceneCamera||!monsterContent||!terrainPalette||!spriteMaterial||!playerSprite)throw new InvalidOperationException("Chapter2 scene bindings missing; run Tools/deviant/Chapter2.");
            using(var preparing=arena.Controls.OpenScope("chapter composition"))
            {
            preparing.BlockInput(InputChannels.Combat|InputChannels.Interaction);preparing.PauseSimulation();
            var slice=new FrameSlice();
            int chosen=seed>=0?seed:Environment.TickCount&int.MaxValue;Layout=C2Bank.Load().Generate(chosen,family);
            yield return null;slice.Restart();
            // v26 지하수로 화풍: 문·등·물웅덩이 자리를 정하고, 웅덩이는 지형을 만들기 전에 바닥 충돌을 1u 판다. 부품이 배치돼 있지 않으면 예전 그림 그대로다.
            if(Resources.Load<Texture2D>(Chapter2WaterwayArt.ModernRoot+"terrain-atlas")){var keep=new List<Vector2>{Layout.spawn.Vector,Layout.maintenance.Vector,Layout.exit.Vector,Layout.boss.Vector};keep.AddRange(Layout.actors.Select(a=>new Vector2(a.x,a.y)));
                // 석판·봉인 보상 상자(석판 오른쪽 2.5)·잠긴 상자 발밑에는 물웅덩이를 파지 않는다.
                foreach(var g in new[]{Layout.seal,Layout.chest})if(g!=null){keep.Add(g.Marker);if(g==Layout.seal)keep.Add(g.Marker+Vector2.right*2.5f);}
                Sewer=Chapter2SewerPlan.Create(Layout,keep.ToArray());}EnemyRandom.SetSeed(arena,chosen);rewards=new System.Random(chosen^0x27182);
            if(slice.Due){yield return null;slice.Restart();}
            var economyAsset=Resources.Load<TextAsset>("Chapter1Economy");var old=Chapter1RunHost.Instance?Chapter1RunHost.Instance.Flow.Current:null;
            SourceRun=old;Economy=old?.Economy??new RunEconomy(JsonUtility.FromJson<EconomyData>(economyAsset.text));Stash=old?.Stash??new RunStash();Shop=new MaintenanceService(Economy,Stash);
            Rooms=new Chapter2RoomState();Rooms.Cleared+=OnRoomCleared;
            monsterContent.Prepare(new ForestData{monsters=Array.Empty<MonsterKind>(),actors=Array.Empty<MonsterPlacement>(),rooms=Array.Empty<ForestRoom>(),landmarks=Array.Empty<ForestLandmark>()});
            carry=Chapter2EntryBridge.Take();
            CombatRuntimeConfiguration content=null;
            yield return CombatContentLoader.LoadInFrames(arena.characterAnimations,arena.loadCharacterArt,loaded=>content=loaded);slice.Restart();
            content.Profile=CombatCompositionProfile.Game;content.Loadout=carry?.CreateLoadout()??WeaponResources.CreateLoadout();content.Camera=sceneCamera;content.SpriteMaterial=spriteMaterial;content.PlayerSprite=playerSprite;content.PlayerMaterial=Resources.Load<Material>(ForestCombatWorld.PlayerOutlineMaterial);content.PlayerSpawn=Layout.spawn.Vector;content.PlayerSize=new Vector2(.9375f,2);content.PlayerOffset=new Vector2(.1875f,1);content.PlayerSortingOrder=10;content.Enemies=Array.Empty<CombatEnemySpawn>();
            content.SecondaryFormIndex=carry?.Weapons.Weapons[1].Selection.FormIndex??content.Loadout.Read().Weapons[1].Selection.FormIndex;
            content.PrepareEnvironment=owner=>{var terrain=gameObject.AddComponent<Chapter2Terrain>();terrain.Build(Layout,owner,terrainPalette,terrainShader);surfaces=new StaticArenaSurfaceIndex(terrain.Surfaces);};
            content.BindPlayerInput=p=>{var input=p.gameObject.AddComponent<CombatKeyboardInput>();input.Player=p;input.ResetKeyEnabled=Debug.isDebugBuild||Application.isEditor;};
            yield return arena.InitializeInFrames(content);slice.Restart();
            if(arena.CompositionProfile==CombatCompositionProfile.Unconfigured)yield break;
            arena.Player.WeaponOrderSwapped += Stash.SwapWeaponSlots;
            PlaytestEnvironment.ApplyBasicSpeedTrials(arena);
            arena.Player.MaintenanceSite=()=>CurrentRoom=="M"||carry!=null&&carry.Restoring;arena.MvpGrowth=old?.Growth??new WeaponGrowth(JsonUtility.FromJson<GrowthData>(Resources.Load<TextAsset>("WeaponGrowthMvp").text),Economy);
            if(carry==null)for(int i=0;i<2;i++){var id=arena.Player.Loadout.Weapons[i].InstanceId;if(arena.MvpGrowth.Equipped(id)==null)arena.MvpGrowth.EquipStartPalette(id,3,false,rewards);}
            arena.EnableSketchEquipment(seed, 2);
            // Sketch weapons and the kill-milestone offers continue from chapter 1 as stage 2.
            if(SourceRun!=null){sketch=ForestCombatV2.SketchRunBinding.Attach(gameObject,arena,SourceRun,2);if(sketch)sketch.OpenShopFacility=()=>ShopOpen?"chapter2:M":null;}
            // A boss clear with a next chapter queues a free augment choice behind its weapon choice (user decision 2026-11-13).
            augmentChoice=ForestCombatV2.AugmentRewardChoice.Attach(gameObject,()=>arena,()=>arena?arena.MvpGrowth:null,()=>sketch&&sketch.RewardModalShown);
            // The permanent max-health upgrade of the village plan carries into chapter 2's fresh player.
            ForestCombatV2.Chapter1RunBinding.ApplyMovementUnlocks(arena,SourceRun?.Plan);
            int healthBonus=SourceRun?.Plan!=null&&!SourceRun.Plan.Training?SourceRun.Plan.MaxHealthBonus:0;if(healthBonus>0){arena.Player.maxHealth+=healthBonus;arena.Player.health=arena.Player.maxHealth;}
            // Maintenance augments re-apply their maximum health to the fresh player without healing it.
            arena.SyncAugmentMaxHealth(false);
            if(carry!=null){arena.Player.health=Mathf.Clamp(carry.Health,1,arena.Player.maxHealth);for(int i=0;i<2;i++)arena.Player.SetWeaponSlotReady(i,carry.Ready[i]);StartCoroutine(carry.RestoreInventory(arena));}
            foreach(var entry in Layout.actors){
                if(slice.Due){yield return null;slice.Restart();}
                // Preserve the existing E placement; the dedicated A3 owns its approved art.
                if(entry.id=="E:elite"&&entry.roomId=="E")
                {
                    Elite=new GameObject("Chapter2 A3 elite").AddComponent<Chapter2EliteEnemy>();Elite.transform.SetParent(transform,false);
                    Elite.Initialize(arena,new Vector2(entry.x,entry.y));Elite.ShowRanges=false;
                    Elite.SyncFacing(entry.flipX?-1:1);Elite.Presentation.Sample();
                    Chapter1EnemyRole.Mark(Elite,Chapter1EnemyRole.Elite);
                    actors.Add(entry.id,Elite);owners.Add(Elite,entry.roomId);Rooms.Register(entry.roomId,entry.id);continue;
                }
                var spec=monsterContent.Definition(entry.species);var request=new CombatEnemySpawn(entry.id,0,new Vector2(entry.x,entry.y),new Vector2(spec.bodySize[0],spec.bodySize[1]),new Vector2(spec.bodyOffset[0],spec.bodyOffset[1]),spec.maxHP,false,false,10);var enemy=Chapter2NormalEnemy.Create(this,arena,request,entry.species,spec,surfaces);enemy.SyncFacing(entry.flipX?-1:1);actors.Add(entry.id,enemy);owners.Add(enemy,entry.roomId);Rooms.Register(entry.roomId,entry.id);}
            arena.Player.DashAdmission=()=>(!Elite||!Elite.BlocksDash)&&(!Boss||!Boss.BlocksTraversal);
            foreach(var pair in actors)if(pair.Value is Chapter2NormalEnemy prototype){string owner=owners[pair.Value];prototype.BindEncounter(Layout.Room(owner).Bounds,_=>!InBossRoom,Chapter2BlockLayout.ApproachZones(Layout,owner),feet=>Layout.RoomAt(feet)?.id==owner);}
            if(slice.Due){yield return null;slice.Restart();}
            var node=new GameObject("Rose Blue boss encounter");node.transform.SetParent(transform,false);Boss=node.AddComponent<RoseBlueEncounter>();Boss.Initialize(arena,new Vector2(Layout.boss.x,Layout.boss.y-3),chosen);
            actors.Add("B:machine",Boss.Machine);owners.Add(Boss.Machine,"B");actors.Add("B:rose",Boss.Rose);owners.Add(Boss.Rose,"B");actors.Add("B:boss",Boss.Blue);owners.Add(Boss.Blue,"B");Rooms.Register("B","B:boss");
            Boss.Stage.OpenBottomEntries(Layout.ports.Where(p=>p.roomId=="B"),arena);
            if(slice.Due){yield return null;slice.Restart();}
            // Wall climb, ledge stand-up, wall kick and jumps use the same approved art as chapter 1.
            var traversal=gameObject.AddComponent<Chapter1TraversalPresentation>();traversal.Initialize(arena);
            if(slice.Due){yield return null;slice.Restart();}
            var checkpoint=gameObject.AddComponent<Chapter1ManualCheckpoint>();
            checkpoint.Initialize(arena,traversal.Art,()=>{UpdateRoom();CameraStep(0,true);},()=>Boss.BlocksTraversal||Complete);
            checkpoint.ExternalPresentation=true;
            Boss.Entered+=arena.Player.ClearCheckpoint;
            Boss.Entered+=()=>{if(Elite){Elite.StopEncounter();eliteStopped=true;counted.Add(Elite);actors.Remove("E:elite");owners.Remove(Elite);Rooms.RemoveWithoutReward("E","E:elite");Elite.gameObject.SetActive(false);}};
            Boss.Finished+=()=>{Rooms.Defeat("B","B:boss");Kills++;SourceRun?.RecordKill();GrantReward("boss",Boss.Blue.Position,"B:boss");};
            // The exit arch and the colour ink altar appear once the boss falls (playtest 2026-11-06).
            Exit=new GameObject("Chapter2 boss exit").AddComponent<Chapter2BossExit>();Exit.transform.SetParent(transform,false);Exit.Initialize(this,Layout.exit.Vector,SourceRun?.Seed??chosen,CompleteThroughExit);
            if(slice.Due){yield return null;slice.Restart();}
            // 1챕터와 같은 봉인 도전·잠긴 상자 규칙. 봉인 보상 상자는 1챕터처럼 증강 선택을 바로 연다.
            Gimmicks=new Chapter2GimmickHost(this);
            Keys=gameObject.AddComponent<Chapter1KeyChest>();Keys.Initialize(Gimmicks);
            Seal=gameObject.AddComponent<Chapter1SealChallenge>();Seal.Initialize(Gimmicks);Seal.RewardOpened+=_=>{if(augmentChoice)augmentChoice.OpenChoice();};
            foreach(var enemy in actors.Values){var source=enemy.GetComponent<InkDropSource>()??enemy.gameObject.AddComponent<InkDropSource>();source.tableKey="";}
            arena.BeforePlayerStep+=BeforeStep;arena.PlayerStepCompleted+=AfterPlayer;arena.SimulationStepCompleted+=AfterStep;arena.ResetCompleted+=ResetRun;
            previousStep=Time.fixedDeltaTime;Time.fixedDeltaTime=1f/120;engineTime=arena.Controls.BindEngineTime();output=CameraOutputBinding.GetOrCreate(sceneCamera);follow=output.BindBaseSource("chapter2 vertical follow");sceneCamera.orthographic=true;sceneCamera.orthographicSize=8.4375f;sceneCamera.backgroundColor=new Color32(21,32,28,255);if(!sceneCamera.GetComponent<AudioListener>())sceneCamera.gameObject.AddComponent<AudioListener>();
            // Same render-only smoothing as chapter 1: the 120 Hz body is drawn between its last two fixed positions.
            smoothing=arena.Player.GetComponent<ActorRenderInterpolation>();if(!smoothing)smoothing=arena.Player.gameObject.AddComponent<ActorRenderInterpolation>();smoothing.Bind(arena.Player);
            started=arena.Now;SourceRun?.StartClock(arena.Now);Ready=true;deathPresentation=PlayerDeathPresentation.Attach(arena);DebugGameMenu.InstallFor(arena,Economy,Stash);UpdateRoom();CameraStep(0,true);FullMap=Chapter2CommonComposition.Attach(this);
            // Room-clear and charging-station healing share chapter 1's green body sheet and screen edge (run-structure.md 전투방 섬멸 회복).
            var inkFx=gameObject.AddComponent<Chapter1InkPartFx>();inkFx.Initialize(arena);
            roomHealFx=gameObject.AddComponent<Chapter1RoomClearHealFx>();roomHealFx.Initialize(null,inkFx,arena);
            if (SourceRun != null) RunRevivalBridge.Attach(arena, SourceRun);
            gameObject.AddComponent<StageRecoveryStation>().Initialize(arena, Economy, chosen ^ 0x20000, RecoveryRooms,
                () => Ready, () => !Complete && !ShowingResult, stage: 2, extraUses: SourceRun?.Perks.Settings.ExtraRecoveryUses ?? 0);
            var background = gameObject.AddComponent<Chapter2WaterwayBackground>(); background.Build(this);
            // v26 지하수로 화풍은 창·시험 조명 소품(랜턴·횃불·창 빛)을 쓰지 않는다. 등빛은 뒷벽 셰이더가 칠한다(2026-11-12 사용자 결정).
            if (Sewer == null) gameObject.AddComponent<Chapter2LightingProps>().Build(this, background);
            }
        }
        IReadOnlyList<StageRecoveryRoom> RecoveryRooms()
        {
            // Route distance remains meaningful when the block layout folds vertically or leftward.
            var distance = new Dictionary<string, int> { ["S"] = 0 };
            var queue = new Queue<string>(); queue.Enqueue("S");
            while (queue.Count > 0)
            {
                string id = queue.Dequeue();
                foreach (var edge in Layout.connections)
                {
                    string next = edge.from == id ? edge.to : edge.to == id ? edge.from : null;
                    if (next == null || distance.ContainsKey(next)) continue;
                    distance[next] = distance[id] + 1; queue.Enqueue(next);
                }
            }
            return Layout.rooms.Where(r => r.kind == "combat" && r.main && distance.ContainsKey(r.id))
                .OrderBy(r => distance[r.id]).ThenBy(r => r.id, StringComparer.Ordinal)
                .Select(r => new StageRecoveryRoom(r.id, r.Bounds)).ToArray();
        }
        void BeforeStep(float dt){if(!Ready)return;UpdateRoom();if(Boss)Boss.BeforePlayer(dt);var p=arena.Player;if(p.Position.y<Layout.rooms.Min(r=>r.y)-8){p.Position=p.spawn;p.velocity=Vector2.zero;}}
        void AfterPlayer(float dt){if(Ready)UpdateRoom();}
        void UpdateRoom()
        {
            var next=Boss&&Boss.EnteredRoom&&!Boss.Dead?"B":Layout.RoomAt(arena.Player.Position)?.id;
            if(next!=CurrentRoom)
            {
                // 일반 적은 방·통로와 관계없이 교전한다. 보스방에 들어갈 때만 공격·투사체를 정리하고 멈춘다.
                if(next=="B")
                {
                    var normalOrdnance=arena.GetComponent<Chapter2NormalOrdnance>();if(normalOrdnance)normalOrdnance.Clear();
                    foreach(var e in actors.Values)if(e&&!(e==Elite&&ElitePursuing)){e.CancelAttack();e.aggressive=false;}
                    foreach(var projectile in arena.projectiles.ToArray())if(projectile&&projectile.source&&!projectile.source.isPlayer)projectile.Release();
                    foreach(var bomb in arena.bombs.ToArray())if(bomb&&bomb.owner&&!bomb.owner.isPlayer)bomb.Release();
                }
                else if(next!=null)foreach(var pair in actors)if(pair.Value is Chapter2NormalEnemy normal&&owners[normal]==next)normal.RollEntryDelay();
                CurrentRoom=next;
            }
            if(Elite)
            {
                if(!eliteStopped&&(arena.Player.Dead||Elite.Dead)){eliteStopped=true;Elite.StopEncounter();}
                if(!eliteEngaged&&!eliteStopped&&CurrentRoom=="E"){eliteEngaged=true;Elite.ActivateEncounter();}
            }
            if(ElitePursuing)Elite.NeedsPursuitTeleport=Layout.RoomAt(Elite.Position)?.id!=CurrentRoom ||
                Vector2.Distance(Elite.Position,arena.Player.Position)>Elite.Settings.comboWidth+Elite.size.x;
            arena.enemies.Clear();foreach(var pair in actors){var enemy=pair.Value;if(!enemy)continue;bool engaged=enemy==Elite?ElitePursuing:enemy is Chapter2NormalEnemy?!InBossRoom:Rooms.CanEngage(owners[enemy],CurrentRoom);if(enemy is Chapter2NormalEnemy normal)normal.SetEngaged(engaged&&!enemy.Dead);else enemy.aggressive=engaged&&!enemy.Dead;if(enemy.hurtCollider)enemy.hurtCollider.enabled=engaged&&!enemy.Dead;if(enemy.bodyCollider)enemy.bodyCollider.enabled=engaged&&!enemy.Dead&&!(enemy is RoseBlueActor);if(engaged)arena.enemies.Add(enemy);}
            if(CurrentRoom=="B"&&!Boss.EnteredRoom&&!Boss.Dead&&Boss.Stage.Area.Contains(arena.Player.Position)&&arena.Player.Position.x>Boss.Stage.Area.xMin+1&&arena.Player.Position.x<Boss.Stage.Area.xMax-1)Boss.BeginWalkIn();
            if(Boss)Boss.RegisterTargets(arena.enemies);
        }
        void AfterStep(float dt)
        {
            if(!Ready)return;if(Boss){Boss.WarmArtNear(arena.Player.Position);Boss.Step(dt);}
            foreach(var pair in actors)
            {
                var e=pair.Value;if(e is RoseBlueActor)continue;if(!e||!e.Dead||!counted.Add(e))continue;var room=Layout.Room(owners[e]);Rooms.Defeat(room.id,pair.Key);Kills++;SourceRun?.RecordKill();
                // 열쇠 운반자는 열쇠만 자동 지급한다. 봉인 도전방에서 떨어진 몹은 일반 몹처럼 세되 골드만 준다(stage-gimmicks.md).
                if(!e.GetComponent<Chapter1NoKillReward>())GrantReward(room.kind=="combat"||room.kind=="seal"?"normal":room.kind,e.Position,pair.Key,e.maxHealth,e.GetComponent<Chapter1NoInkReward>());
                Gimmicks?.RaiseDefeated(e);
            }
            if(arena.Player.Dead&&deathAt<0)deathAt=Time.realtimeSinceStartup;
        }
        List<string> BlueprintKinds()=>Enumerable.Range(0,2).Where(arena.Player.IsWeaponSlotReady).Select(i=>Chapter1Rewards.BlueprintKindOf(arena.catalog.forms[arena.Player.weapons[i].FormIndex].id)).Distinct().ToList();
        List<string> InkPool(bool specialOnly=false,bool maintenance=false)
        {
            if(!Bag||Bag.Inventory==null)return new List<string>();var kinds=BlueprintKinds();
            return Bag.Content.items.Where(e=>(!maintenance||Bag.Inventory.HasRoom)&&!(e.kind==InkKind.Form&&!Economy.AllowsFormInk(e.formId))&&(e.kind==InkKind.Special||!specialOnly&&(e.kind==InkKind.Stat&&arena.MvpGrowth==null||e.kind==InkKind.Form&&kinds.Contains(Chapter1Rewards.BlueprintKindOf(e.formId))))).Select(e=>e.id).ToList();
        }
        Chapter1RewardPickup SpawnReward(Chapter1RewardKind kind,Vector2 at,string blueprint=null,string ink=null,Vector2? source=null)
        {
            var go=new GameObject("Chapter2 reward · "+kind);go.transform.SetParent(transform,false);var pickup=go.AddComponent<Chapter1RewardPickup>();
            pickup.Initialize(arena,sceneCamera,arena.uiFont,Stash,kind,blueprint,arena.DropPoint(at,0),source);if(ink!=null)pickup.ConfigureGrowth(arena.MvpGrowth,ink);return pickup;
        }
        ForestCombatV2.SketchRunBinding sketch;
        ForestCombatV2.AugmentRewardChoice augmentChoice;
        public static bool BossAugmentFollows=>2<ProgressionService.LastChapter;
        void GrantReward(string role,Vector2 at,string actorId,float maxHealth=0,bool goldOnly=false)
        {
            if(sketch){if(role=="boss")WaitForBossOutro();else sketch.ReportKill(role,maxHealth);}
            if(SourceRun?.Plan!=null&&!SourceRun.Plan.Training)
            {
                int pigment = Chapter1ProfileHost.Service.AwardPigment(SourceRun.RunId,"chapter2:"+Layout.id+":"+Layout.variant+":"+actorId,role,false);
                PigmentEarned += pigment;SourceRun.RecordPigment(pigment);
                if (pigment > 0)
                {
                    var effect = arena.GetComponent<Chapter1PigmentPickupEffect>();
                    if (!effect) { effect = arena.gameObject.AddComponent<Chapter1PigmentPickupEffect>(); effect.Initialize(arena); }
                    effect.Show(at, pigment);
                }
            }
            var drop=RewardRoller.Roll(Economy.Chapter2Table(role),Economy.WeaponStampShare,rewards);
            // A first clear ends the run at the exit, so the boss pays no gold or stamp then (user decision 2026-11-09, like the
            // altar). Read before the exit settles the clear; pigment above stays.
            if(role=="boss"&&SourceRun?.Plan!=null&&!SourceRun.Plan.Training&&Chapter1ProfileHost.Service.ChapterClearEndsRun(2))drop=new RewardDrop(0,0,0,0);
            // Kill gold carries the run's kill-gold bonus once per drop (roguelite-progression.md).
            int gold=drop.Gold>0?(SourceRun?.Perks.AdjustKillGold(drop.Gold) ?? drop.Gold):0;
            // Sketch-weapon runs drop no equipment on the floor; weapons and inks come from the choices (rewards.md).
            if(sketch){if(gold>0)SpawnReward(Chapter1RewardKind.Gold,at).SetGold(Economy,gold);return;}
            if(gold>0)SpawnReward(Chapter1RewardKind.Gold,at).SetGold(Economy,gold);
            if (arena.Sketch != null || goldOnly) return;
            for(int i=0;i<drop.Parts;i++)SpawnReward(Chapter1RewardKind.InkPart,arena.DropPoint(at,i*.5f,1f),source:at);
            // Chapter 2 counts its own stage stamp limit; a weapon stamp without a carried blueprint drops as a skill stamp.
            var kinds=BlueprintKinds();
            for(int i=0;i<drop.WeaponStamps+drop.SkillStamps&&Economy.TryTakeStageStamp(StampStages.Chapter2);i++)
            {
                if(i<drop.WeaponStamps&&kinds.Count>0)SpawnReward(Chapter1RewardKind.WeaponStamp,arena.DropPoint(at,.7f+i*.5f,1f),kinds[rewards.Next(kinds.Count)],source:at);
                else SpawnReward(Chapter1RewardKind.SkillStamp,arena.DropPoint(at,-i*.5f,1f),source:at);
            }
            var pool=InkPool();var special=InkPool(true);if(Bag){for(int i=0;i<drop.Inks&&pool.Count>0;i++)Bag.SpawnDrop(pool[rewards.Next(pool.Count)],arena.DropPoint(at,.5f,1f),at);for(int i=0;i<drop.SpecialInks&&special.Count>0;i++)Bag.SpawnDrop(special[rewards.Next(special.Count)],arena.DropPoint(at,-.5f,1f),at);}
            if(arena.MvpGrowth!=null)foreach(var id in arena.MvpGrowth.PlanReward(role,rewards))SpawnReward(Chapter1RewardKind.ColoredInk,arena.DropPoint(at,.4f,1f),ink:id,source:at);
        }
        // The chapter 2 maintenance room keeps one ink stall for the run (user decision 2026-11-09), rolled on its first visit.
        public const string StallKey="chapter2:M";
        public IReadOnlyList<InkOffer> InkStock=>arena.MvpGrowth!=null?arena.MvpGrowth.StockOf(StallKey):Array.Empty<InkOffer>();
        public bool TryBuyInk(int index)
        {
            if(shopScope==null)return false;
            if(arena.MvpGrowth==null){message="잉크 판매대를 사용할 수 없습니다.";return false;}
            try{message=arena.MvpGrowth.BuyStockInk(StallKey,index).Part.name+" 구매 / 가방에 넣었습니다.";return true;}catch(InvalidOperationException e){message=e.Message;return false;}
        }
        Chapter1RoomClearHealFx roomHealFx;
        void OnRoomCleared(string id){var r=Layout.Room(id);if(!arena.Player.Dead){float restored=arena.Player.Heal(Economy.RoomClearHealth(r.kind));if(roomHealFx)roomHealFx.Play(restored);}message=r.kind=="boss"?"보스 처치 / 출구 사용 가능":id+" 섬멸";}
        public Chapter2BossExit Exit {get;private set;}
        public Chapter1InkAltar Altar=>Exit?Exit.Altar:null;
        // F at the arch opens the portcullis first; the chapter clears once it is open (CompleteThroughExit).
        public bool TryUseExit()=>Ready&&!Complete&&Exit&&Exit.TryUse();
        void CompleteThroughExit()
        {
            if(Complete)return;SourceRun?.MarkBossDefeated();Complete=true;message="2스테이지 클리어";
            CloseMaintenance();if(FullMap)FullMap.SetOpen(false);
            clearWait=arena.Controls.OpenScope("chapter2 clear blackout");clearWait.BlockInput(InputChannels.Combat|InputChannels.Interaction|InputChannels.Navigation);
            deathPresentation.BeginClearBlackout();UpdateResult();
        }
        void WaitForBossOutro()
        {
            if(bossOfferWait!=null)return;
            bossOfferWait=arena.Controls.OpenScope("chapter2 boss outro before reward");bossOfferWait.BlockInput(InputChannels.Combat|InputChannels.Interaction|InputChannels.Navigation);
            StepBossOffer();
        }
        void StepBossOffer()
        {
            if(bossOfferWait==null)return;
            if(arena.Player.Dead||!Boss){bossOfferWait.Dispose();bossOfferWait=null;return;}
            if(Boss.DefeatPresentationPlaying)return;
            bossOfferWait.Dispose();bossOfferWait=null;sketch.OpenBossOffer();
            // The augment follows only when a next chapter exists (user decision 2026-11-13); chapter 2 is the last one now.
            if(augmentChoice&&BossAugmentFollows)augmentChoice.QueueChoice();
        }
        public bool TryOpenMaintenance(){if(!Ready||CurrentRoom!="M"||shopScope!=null||arena.Player.Dead||arena.Controls.Current.SimulationPaused)return false;shopScope=arena.Controls.OpenScope("chapter2 maintenance");shopScope.BlockInput(InputChannels.Combat|InputChannels.Interaction);shopScope.PauseSimulation();arena.MvpGrowth?.InkStock(StallKey,rewards);return true;}
        public void CloseMaintenance(){shopScope?.Dispose();shopScope=null;}
        // The boss fight cannot rewind in place, so the development reset rebuilds the scene; the bridge hands the current
        // player, weapons and bag to the rebuilt scene so the same run continues instead of a fresh standalone chapter.
        void ResetRun(){if(Boss&&Boss.EnteredRoom){Chapter2EntryBridge.Reload(arena,gameObject.scene.path);return;}foreach(var spawned in gimmickSpawned.ToArray())RemoveGimmickMonster(spawned);CloseMaintenance();if(FullMap)FullMap.SetOpen(false);resultScope?.Dispose();resultScope=null;bossOfferWait?.Dispose();bossOfferWait=null;if(augmentChoice)augmentChoice.Cancel();clearWait?.Dispose();clearWait=null;Kills=0;counted.Clear();eliteEngaged=false;eliteStopped=false;Rooms=new Chapter2RoomState();Rooms.Cleared+=OnRoomCleared;foreach(var pair in actors){pair.Value.ResetEnemy();Rooms.Register(owners[pair.Value],pair.Key);}Complete=false;deathAt=-1;CurrentRoom=null;UpdateRoom();}
        // 봉인 도전방에서 떨어지는 몹: 그 판 일반 전투방 종류의 2챕터 몹을 공중에 만들고 방에 묶는다(Chapter2GimmickHost).
        public FieldMouseEnemy SpawnGimmickMonster(string species,Vector2 feet,int facing,string roomId)
        {
            if(!Ready)throw new InvalidOperationException("Wait for chapter 2 to initialize.");
            var spec=monsterContent.Definition(species);string id="SEAL:drop"+(++gimmickCount);
            var request=new CombatEnemySpawn(id,0,feet,new Vector2(spec.bodySize[0],spec.bodySize[1]),new Vector2(spec.bodyOffset[0],spec.bodyOffset[1]),spec.maxHP,false,true,10);
            var enemy=Chapter2NormalEnemy.Create(this,arena,request,species,spec,surfaces);enemy.SyncFacing(facing);
            // 돌아갈 자리와 이동 영역은 떨어져 닿을 바닥이다.
            var floor=arena.FindFloor(feet,24);if(floor.HasValue)enemy.spawn=new Vector2(feet.x,floor.Value);
            enemy.BindEncounter(Layout.Room(roomId).Bounds,_=>!InBossRoom,Chapter2BlockLayout.ApproachZones(Layout,roomId));
            actors.Add(id,enemy);owners.Add(enemy,roomId);gimmickSpawned.Add(enemy);
            enemy.SetEngaged(!InBossRoom);if(!InBossRoom)arena.enemies.Add(enemy);Physics2D.SyncTransforms();return enemy;
        }
        public void RemoveGimmickMonster(TestEnemy enemy)
        {
            if(!enemy||!gimmickSpawned.Remove(enemy))return;
            string key=actors.FirstOrDefault(p=>p.Value==enemy).Key;if(key!=null)actors.Remove(key);owners.Remove(enemy);counted.Remove(enemy);arena.enemies.Remove(enemy);
            enemy.CancelAttack();enemy.gameObject.SetActive(false);Destroy(enemy.gameObject);
        }
        public void WarpToRoom(string id, Vector2? verifiedLanding = null)
        {
            var r = Layout.Room(id); if (r == null) throw new ArgumentException(id);
            var at = new Vector2(r.x + r.w / 2, r.y + r.h - 2);
            float? floor = arena.FindFloor(at, r.h + 2);
            var destination = verifiedLanding ?? new Vector2(at.x, floor ?? r.y + 1);
            if (verifiedLanding.HasValue && !r.Bounds.Contains(destination + Vector2.up * .25f)) throw new ArgumentException("Landing must be inside the requested room.");
            arena.Player.Position = destination; arena.Player.velocity = Vector2.zero;
            Physics2D.SyncTransforms(); UpdateRoom(); CameraStep(0, true);
        }
        void Update()
        {
            if(!Ready||ExternalInput)return;
            var input=PlayerControls.For(arena.Player);if(!input)return;
            if(shopScope!=null)
            { if(input.TakeBack()||input.Take(Deviant.Input.Contracts.GameAction.Interact)){input.SuppressGameplay();CloseMaintenance();} }
            else PlayerControls.TryPerform(arena.Player,Deviant.Input.Contracts.GameAction.Interact,()=>TryUseExit()||TryOpenMaintenance());
        }
                void UpdateResult()
        {
            if(resultScope!=null||!Ready)return;
            deathPresentation.ObserveDeath();
            // A clear waits for the same blackout as a death (BeginClearBlackout), then the result rises out of the black.
            if(!Complete&&!arena.Player.Dead||!deathPresentation.ReadyForResult&&!(Complete&&!deathPresentation.isActiveAndEnabled))return;
            clearWait?.Dispose();clearWait=null;CloseMaintenance();if(FullMap)FullMap.SetOpen(false);resultScope=arena.Controls.OpenScope("chapter2 run result");resultScope.BlockInput(InputChannels.Combat|InputChannels.Interaction);resultScope.PauseSimulation();
            var host=Chapter1RunHost.Instance;
            if(SourceRun!=null&&!SourceRun.Ended&&host&&host.Flow.Current==SourceRun){if(Complete)host.Flow.TryClearChapter(arena.Now);else host.Flow.TryDie(arena.Now);}
            if(SourceRun?.Plan!=null&&!SourceRun.Plan.Training){Chapter1ProfileHost.Service.FlushPigment();if(SourceRun.Result.HasValue)Chapter1ProfileHost.Service.SettleRun(SourceRun.RunId,SourceRun.Result.Value.Outcome,false,2);}
        }
        ActorRenderInterpolation smoothing;
        const float BossCameraBlendSeconds=.65f;
        bool bossCameraHeld;
        Vector3 bossCameraFrom;
        float bossCameraSizeFrom,bossCameraBlend;
        void LateUpdate(){if(Ready){CameraStep(Time.deltaTime,false);StepBossOffer();UpdateResult();}}
        public void CameraStep(float dt,bool snap)
        {
            // 보스방은 1챕터처럼 화면 하나에 딱 맞춘다(사용자 2026-11-13 「양옆 벽이 안 보이게」): 화면 폭 = 전장 폭 30u, 양옆 벽은 화면 바로 밖.
            // 16:9 에서 높이 16.875u(바닥 아래 2.44u ~ 바닥 위 14.44u), 천장은 화면 바로 위. 전투감 카메라 확대(12%)는 끈다.
            var feel=arena.FeelFeedback?arena.FeelFeedback.CombatCamera:null;if(feel)feel.HoldBaseFraming=Boss&&Boss.EnteredRoom;
            if(Boss&&Boss.EnteredRoom)
            {
                if(!bossCameraHeld)
                {
                    bossCameraHeld=true;bossCameraBlend=0;bossCameraSizeFrom=sceneCamera.orthographicSize;
                    bossCameraFrom=follow!=null&&follow.IsActive?new Vector3(follow.Position.X,follow.Position.Y,follow.Position.Z):sceneCamera.transform.position;
                }
                bossCameraBlend=snap?BossCameraBlendSeconds:Mathf.Min(BossCameraBlendSeconds,bossCameraBlend+Mathf.Max(0,dt));
                float blend=Mathf.SmoothStep(0,1,bossCameraBlend/BossCameraBlendSeconds);
                var center=Boss.Stage.CameraCenter;var bossPosition=Vector3.Lerp(bossCameraFrom,new Vector3(center.x,center.y,-10),blend);
                sceneCamera.orthographicSize=Mathf.Lerp(bossCameraSizeFrom,RoseBlueStage.Width*.5f/sceneCamera.aspect,blend);
                sceneCamera.transform.position=bossPosition;cameraVelocity=Vector3.zero;
                follow.Submit(new CameraPosition(bossPosition.x,bossPosition.y,-10));output.ApplyNow();return;
            }
            bossCameraHeld=false;
            var player=arena.Player;
            // Follow reads its own baseline so a presentation-only kick cannot feed back into camera tracking.
            var current=follow!=null&&follow.IsActive?new Vector3(follow.Position.X,follow.Position.Y,follow.Position.Z):sceneCamera.transform.position;
            var seen=!snap&&smoothing?smoothing.RenderPosition():player.Position;
            float feetY=seen.y,goalY=cameraFraming.Step(feetY,player.velocity.y,player.grounded,current.y,snap);
            if(cameraFraming.FreezeVerticalVelocity)cameraVelocity.y=0;
            // Turning must not swing the camera; vertical speed only leads moves outside the jump band.
            var target=new Vector3(ClampBossApproachCameraX(seen.x+1.5f),goalY,-10);
            if(snap){sceneCamera.transform.position=target;cameraVelocity=Vector3.zero;}
            else if(dt>0)
            {
                float vx=cameraVelocity.x,vy=cameraVelocity.y,x=Mathf.SmoothDamp(current.x,target.x,ref vx,.16f,120,dt);
                float y=cameraFraming.Tracking?Mathf.SmoothDamp(current.y,target.y,ref vy,.1f,240,dt):Mathf.SmoothDamp(current.y,target.y,ref vy,.3f,60,dt);
                // Smoothing lag must never let the player outrun the frame vertically.
                float low=Chapter2CameraFraming.RisingLimit(feetY),high=Chapter2CameraFraming.FallingLimit(feetY);
                if(y<low||y>high){y=Mathf.Clamp(y,low,high);vy=player.velocity.y;}
                sceneCamera.transform.position=new Vector3(x,y,-10);cameraVelocity=new Vector3(vx,vy,0);
            }
            var p=sceneCamera.transform.position;p.x=ClampBossApproachCameraX(p.x);sceneCamera.transform.position=p;follow.Submit(new CameraPosition(p.x,p.y,-10));output.ApplyNow();
        }
        float ClampBossApproachCameraX(float x)
        {
            if(!Boss||Boss.EnteredRoom||Layout.bossHall==null)return x;
            var player=arena.Player;var area=Boss.Stage.Area;
            if(Mathf.Abs(player.Position.y-Layout.bossHall.y)>sceneCamera.orthographicSize+5)return x;
            float halfWidth=sceneCamera.orthographicSize*sceneCamera.aspect;
            return Layout.bossHallToLeft?Mathf.Max(x,area.xMax+halfWidth):Mathf.Min(x,area.xMin-halfWidth);
        }
        void OnGUI()
        {
            if(!Ready||ExternalPresentation)return;var previous=GUI.skin.font;if(arena.uiFont)GUI.skin.font=arena.uiFont;
            GUI.Box(new Rect(12,12,600,72),"2스테이지 / "+Layout.name+" / "+(CurrentRoom??"통로")+"\n체력 "+Mathf.CeilToInt(arena.Player.health)+" / "+arena.Player.maxHealth+" / 골드 "+Economy.Gold+" / "+message);
            GUI.Label(new Rect(12,90,900,30),PlayerControls.Prompt(arena.Player, "{Move} 이동  {Move.Up} 벽타기 / {Jump} 점프, {Move.Down}+{Jump} 통과 / {Dash} 대시 / {Weapon1Basic}/{Weapon2Basic} 기본, {Weapon1Special}/{Weapon2Special} 스킬 / {Interact} 정비소, 출구 / {DevelopmentReset} 초기화"));
            if(CurrentRoom=="B")GUI.Box(new Rect(Screen.width/2-200,120,400,32),RoseBlueEncounter.RoseDisplayName+" "+RoseBlueEncounter.BlueDisplayName+" / "+Boss.Model.Phase+" / "+Mathf.CeilToInt(Boss.health)+" / "+Boss.maxHealth);
            if(ShowingResult){deathPresentation.RevealResult();GUI.Box(new Rect(Screen.width/2-180,Screen.height/2-90,360,180),Complete?"2스테이지 클리어":"사망");if(GUI.Button(new Rect(Screen.width/2-150,Screen.height/2-30,140,50),"새 런 준비"))Chapter1RunHost.Ensure().GoToVillage();if(GUI.Button(new Rect(Screen.width/2+10,Screen.height/2-30,140,50),"타이틀"))Chapter1RunHost.Ensure().GoToVillage();}
            if(shopScope!=null){GUI.Box(new Rect(20,130,500,90),"상점 / 공통 게임 GUI를 사용합니다.");if(GUI.Button(new Rect(40,175,180,30),"닫기"))CloseMaintenance();}
            GUI.skin.font=previous;
        }
        void OnDestroy(){CloseMaintenance();resultScope?.Dispose();bossOfferWait?.Dispose();clearWait?.Dispose();var host=Chapter1RunHost.Instance;
            if(arena&&arena.Player&&Stash!=null)arena.Player.WeaponOrderSwapped-=Stash.SwapWeaponSlots;
            // A run left from the pause menu (or rebuilt by the development reset) has no result, as in chapter 1;
            // a death is settled when its result screen opens, never here.
            if(SourceRun!=null&&!SourceRun.Ended&&host&&host.Flow.Current==SourceRun&&Complete)host.Flow.TryClearChapter(arena.Now);
            if(SourceRun?.Plan!=null&&!SourceRun.Plan.Training){Chapter1ProfileHost.Service.FlushPigment();if(SourceRun.Result.HasValue)Chapter1ProfileHost.Service.SettleRun(SourceRun.RunId,SourceRun.Result.Value.Outcome,false,2);}if(arena){arena.BeforePlayerStep-=BeforeStep;arena.PlayerStepCompleted-=AfterPlayer;arena.SimulationStepCompleted-=AfterStep;arena.ResetCompleted-=ResetRun;}follow?.Dispose();engineTime?.Dispose();if(previousStep>0)Time.fixedDeltaTime=previousStep;}
    }
}
