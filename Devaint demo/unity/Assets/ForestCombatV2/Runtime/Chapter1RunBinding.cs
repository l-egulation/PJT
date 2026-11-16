using System;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Control.Contracts;
using Deviant.Integration;
using Deviant.Run;
using UnityEngine;

namespace ForestCombatV2
{
    // Connects one chapter scene to the run: kills, death, chapter clear and the result screen.
    public sealed class Chapter1RunBinding : MonoBehaviour
    {
        public ForestCombatWorld World { get; private set; }
        public Chapter1RunHost Host { get; private set; }
        public RunSession Run { get; private set; }
        public bool ShowingResult => resultScope != null;
        public event Action<TestEnemy> EnemyDefeated;
        readonly HashSet<TestEnemy> counted = new HashSet<TestEnemy>();
        IControlScope resultScope;
        CombatArena arena;
        PlayerDeathPresentation deathPresentation;
        bool clockStarted;
        // Legacy minimum retained for existing checks. The actual result waits for the death presentation
        // to finish its animation, gradual blackout and black hold.
        public const float GameOverDelaySeconds = 2.5f;
        float diedAtRealtime = -1, diedAtNow;
        public bool AwaitingGameOver => diedAtRealtime >= 0 && !Run.Ended;
        Chapter1RoomClearHealing roomHealing;
        Chapter1StartingBlueprints blueprints;
        SketchRunBinding sketch;
        AugmentRewardChoice augmentChoice;
        public SketchRunBinding Sketch => sketch;
        // The boss weapon choice waits for the clock executioner's defeat outro (bars, collapse, village ink drop),
        // which runs in real time and would otherwise finish behind the paused choice (QA 2026-11-13). The player
        // holds still until the choice opens.
        IControlScope bossOfferWait;
        public bool BossOfferPending => bossOfferWait != null;
        bool vitalsApplied;
        // Village runs pay pigment into the profile and settle there; runs without a plan (direct editor loads) and
        // training plans never touch the profile.
        public bool ProfileRun => Run.Plan != null && !Run.Plan.Training;
        public int PigmentEarned { get; private set; }
        public bool Settled { get; private set; }
        public event Action<Vector2, int> PigmentAwarded;

        public void Initialize(ForestCombatWorld world, Chapter1RunHost host)
        {
            World = world; Host = host; Run = host.Flow.Current;
            host.Flow.Ended += RunEnded;
            if(Run.Economy!=null&&Run.Growth==null){var growth=Resources.Load<TextAsset>("WeaponGrowthMvp");if(growth)Run.Growth=new WeaponGrowth(JsonUtility.FromJson<GrowthData>(growth.text),Run.Economy);}
            // Maintenance augment offers follow the run seed (docs/design/progression/maintenance-augments.md).
            if(Run.Growth!=null&&Run.Growth.Augments==null){var augmentCatalog=Deviant.Combat.CombatArena.LoadMaintenanceAugmentCatalog();if(augmentCatalog!=null)Run.Growth.AttachAugments(new MaintenanceAugments(augmentCatalog,unchecked(Run.Seed*4421+17)));}
            if (Run.Growth != null)
            {
                Run.Growth.Perks = Run.Perks;
                Run.Growth.Augments?.LockCards(ProfileRun ? Run.Plan.LockedAugments : null);
            }
            gameObject.AddComponent<Chapter1Rewards>().Initialize(world, this);
            RunRevivalBridge.Attach(world.arena, Run);
            roomHealing = gameObject.AddComponent<Chapter1RoomClearHealing>();
            roomHealing.Initialize(world, this);
            if (!world.dummyTrainingOnly) gameObject.AddComponent<StageRecoveryStation>().Initialize(world, this);
            // The starting room's signpost: F raises the opening road's strip of moves (for players who skipped the road).
            if (!world.dummyTrainingOnly) gameObject.AddComponent<Chapter1ControlsSign>().Initialize(world);
            // Key carrier and locked chest of the random stage (docs/design/world/stage-gimmicks.md).
            gameObject.AddComponent<Chapter1KeyChest>().Initialize(world, this);
            // First-run ink tutorial: only on the profile's first run (docs/design/story/tutorials/first-run-guide.md).
            gameObject.AddComponent<Chapter1FirstRunInkGuide>().Initialize(world, this);
            // Free augment choices (user decision 2026-11-13): the seal reward chest opens one at once, the boss clear queues
            // one behind its weapon choice.
            var augments = augmentChoice = AugmentRewardChoice.Attach(gameObject, () => World ? World.arena : null, () => Run.Growth, () => sketch && sketch.RewardModalShown);
            var seal = gameObject.AddComponent<Chapter1SealChallenge>();
            seal.Initialize(world, this);
            seal.RewardOpened += _ => augments.OpenChoice();
            if (DevFeatures.Enabled) gameObject.AddComponent<Chapter1GimmickDevKeys>().Initialize(world);
            if (Run.Economy != null) gameObject.AddComponent<Chapter1MaintenanceShop>().Initialize(world, this);
            gameObject.AddComponent<Chapter1PigmentPickupEffect>().Initialize(this);
            // 잉크 파츠 이펙트 시험판: 파츠 발동 신호에 pixelfx 시트를 튼다. 시트가 없으면 아무것도 하지 않는다
            var inkFx = gameObject.AddComponent<Chapter1InkPartFx>();
            inkFx.Initialize(world.arena);
            // 방 섬멸 회복: 몸에 초록 회복 시트, 화면 외곽선에 초록 테두리
            gameObject.AddComponent<Chapter1RoomClearHealFx>().Initialize(roomHealing, inkFx, world.arena);
            if (DevFeatures.Enabled) gameObject.AddComponent<Chapter1InkFxTrialKeys>().Initialize(this);
            // Debug HUD, F1 panel and boxes remain development tools.
            if (!DevFeatures.Enabled && world.diagnostics) world.diagnostics.showDebugUI = false;
        }

        // Each equipped starting weapon gets its departure palette: the planned slot count (3/4/5, the same for both)
        // and the first-slot wildcard of that weapon slot only. Runs without a village plan use the base 3 slots.
        void BindStartingBlueprints()
        {
            if (Run.Growth == null) return;
            if (!blueprints)
            {
                blueprints = World.GetComponent<Chapter1StartingBlueprints>();
                if (!blueprints) return;
                Run.Growth.DestroyedInkGoldBonus = Run.Plan == null || Run.Plan.Training ? 0 : Run.Plan.DestroyedInkGoldBonus;
                Run.Growth.PermanentAttackBonus = Run.Plan == null || Run.Plan.Training ? 0 : Run.Plan.AttackBonusPercent / 100f;
                // Village ink unlocks: parts not yet unlocked stay out of this run's drops, draws and altar (chapter 2 too).
                Run.Growth.LockParts(Run.Plan == null || Run.Plan.Training ? null : Run.Plan.LockedInkParts);
                // The bag unlock sets the run's shared bag once; the growth carries it into chapter 2.
                if (ProfileRun && Run.Plan.BagSlots > Run.Growth.InkCapacity) Run.Growth.InkCapacity = Run.Plan.BagSlots;
                blueprints.WeaponEquipped += GivePalette;
            }
            // The offer initialises with the world; keep trying until the planned pair is equipped once.
            if (Run.Plan != null && !blueprints.Preselected && blueprints.SelectedCount == 0) { if (Run.Plan.Unarmed) blueprints.ApplyUnarmedDeparture(); else blueprints.ApplyPreselected(Run.Plan.Blueprints); }
        }

        // The permanent max-health upgrade raises the chapter player's maximum once and starts it full. A combat reset
        // restores health to that maximum, so it is never applied twice. The move-speed and extra-dash unlocks are
        // settings of the arena and player that a reset keeps.
        void ApplyStartingVitals()
        {
            if (vitalsApplied || !arena.Player) return;
            vitalsApplied = true;
            ApplyMovementUnlocks(arena, ProfileRun ? Run.Plan : null);
            int bonus = ProfileRun ? Run.Plan.MaxHealthBonus : 0;
            if (bonus <= 0) return;
            arena.Player.maxHealth += bonus; arena.Player.health = arena.Player.maxHealth;
        }

        // Village move-speed and extra-dash unlocks of a profile run's departure snapshot; null (training, direct editor
        // loads) keeps the base walk speed and one dash.
        public static void ApplyMovementUnlocks(CombatArena arena, RunStartPlan plan)
        {
            if (!arena || !arena.Player) return;
            bool profile = plan != null && !plan.Training;
            arena.PermanentMoveSpeedBonus = profile ? plan.MoveSpeedBonusPercent / 100f : 0;
            arena.Player.DashCharges = profile ? plan.DashCharges : ProgressionIds.BaseDashCharges;
        }

        void GivePalette(int slot, Guid weapon)
        {
            if (Run.Growth == null || Run.Growth.Equipped(weapon) != null) return;
            int slots = Run.Plan?.PaletteSlots ?? 3;
            bool wildcard = Run.Plan != null && Run.Plan.Wildcard(slot + 1);
            Run.Growth.EquipStartPalette(weapon, slots, wildcard, new System.Random(unchecked(Run.Seed * 397 + slot)));
        }

        void OnDestroy()
        {
            if (World && World.arena && World.arena.Player && Run != null) World.arena.Player.WeaponOrderSwapped -= Run.Stash.SwapWeaponSlots;
            if (blueprints) blueprints.WeaponEquipped -= GivePalette;
            if (Host != null) Host.Flow.Ended -= RunEnded;
            resultScope?.Dispose(); resultScope = null;
            EndBossOfferWait();
        }

        void Update()
        {
            if (!World || !World.Ready) return;
            if (!arena) World.arena.Player.WeaponOrderSwapped += Run.Stash.SwapWeaponSlots;
            arena = World.arena;
            if (!sketch && World.sketchWeapons)
            {
                sketch = SketchRunBinding.Attach(gameObject, arena, Run, 1);
                if (sketch) sketch.OpenShopFacility = () => { var shop = GetComponent<Chapter1MaintenanceShop>(); return shop && shop.IsOpen ? "chapter1:" + shop.Facility : null; };
            }
            if (!deathPresentation) deathPresentation = PlayerDeathPresentation.Attach(arena);
            arena.MvpGrowth=Run.Growth;
            BindStartingBlueprints();
            if (!World.dummyTrainingOnly)
            {
                if (blueprints && !blueprints.Complete) blueprints.ApplyUnarmedDeparture();
                arena.EnableSketchEquipment(Run.Seed);
            }
            ApplyStartingVitals();
            if (!clockStarted) { clockStarted = true; Run.StartClock(arena.Now); }
            if (Run.Ended || !arena.Player) return;
            foreach (var enemy in arena.enemies)
            {
                if (!enemy) continue;
                // The development reset revives enemies; they may be counted again after that.
                if (!enemy.Dead) { counted.Remove(enemy); continue; }
                if (!counted.Add(enemy)) continue;
                Run.RecordKill();
                string role = Chapter1EnemyRole.Of(enemy);
                if (role == Chapter1EnemyRole.Boss) Run.MarkBossDefeated();
                EnemyDefeated?.Invoke(enemy);
                // Kill-milestone sketch offers; the boss opens its own weapon choice (docs/design/run/rewards.md).
                if (sketch && !enemy.GetComponent<Chapter1NoKillReward>()) { if (role == Chapter1EnemyRole.Boss) WaitForBossOutro(); else sketch.ReportKill(role, enemy.maxHealth); }
                // Pigment is acquired the moment the kill is counted; the service's per-run ledger ignores a revived
                // enemy killed again after a development reset.
                if (ProfileRun && !enemy.GetComponent<Chapter1NoKillReward>())
                {
                    int pigment = Chapter1ProfileHost.Service.AwardPigment(Run.RunId, "enemy:" + enemy.GetEntityId(), role, false);
                    if (pigment > 0) { PigmentEarned += pigment; Run.RecordPigment(pigment); PigmentAwarded?.Invoke(enemy.Position, pigment); }
                }
            }
            StepBossOffer();
            roomHealing.Tick();
            if (!arena.Player.Dead) { diedAtRealtime = -1; return; } // A development reset revives the player.
            if (diedAtRealtime < 0) { diedAtRealtime = Time.realtimeSinceStartup; diedAtNow = arena.Now; }
            deathPresentation.ObserveDeath();
            if (deathPresentation.ReadyForResult) Host.Flow.TryDie(diedAtNow);
        }

        void WaitForBossOutro()
        {
            if (bossOfferWait != null) return;
            bossOfferWait = arena.Controls.OpenScope("chapter1 boss outro before reward");
            bossOfferWait.BlockInput(InputChannels.Combat | InputChannels.Interaction | InputChannels.Navigation);
            StepBossOffer();
        }
        void StepBossOffer()
        {
            if (bossOfferWait == null) return;
            // A death or a development reset during the outro drops the choice (a reset revives the boss to be won again).
            var boss = World.GetComponent<ClockExecutionerEncounter>();
            if (arena.Player.Dead || boss && boss.Boss && !boss.Boss.Dead) { EndBossOfferWait(); return; }
            if (boss && boss.Boss && (boss.DeathCutscene || !boss.Defeated)) return;
            EndBossOfferWait(); sketch.OpenBossOffer();
            // The augment follows only when the exit leads on to a next chapter (user decision 2026-11-13).
            var encounter = World.GetComponent<Chapter1BossEncounter>();
            if (augmentChoice && !(encounter && encounter.ClearEndsRun)) augmentChoice.QueueChoice();
        }
        void EndBossOfferWait() { bossOfferWait?.Dispose(); bossOfferWait = null; }

        public bool TryClearChapter() => arena && Host.Flow.TryClearChapter(arena.Now);

        void RunEnded(RunResult result)
        {
            // Death or clear: write the pigment and record the outcome once; a clear opens the village unlocks.
            if (ProfileRun && !Settled)
            {
                var profile = Chapter1ProfileHost.Service;
                profile.FlushPigment();
                Settled = profile.SettleRun(Run.RunId, result.Outcome, false);
            }
            if (resultScope != null || !arena) return;
            var scope = arena.Controls.OpenScope("chapter1 run result");
            try { scope.BlockInput(InputChannels.Combat | InputChannels.Interaction); scope.PauseSimulation(); resultScope = scope; }
            catch { scope.Dispose(); throw; }
            if (World.diagnostics) World.diagnostics.showDebugUI = false;
        }

        public bool ExternalPresentation { get; set; }

        void OnGUI()
        {
            if (ExternalPresentation) return;
            if (resultScope == null || !Run.Result.HasValue) return;
            deathPresentation.RevealResult();
            var result = Run.Result.Value;
            var oldFont = GUI.skin.font; int oldDepth = GUI.depth;
            try
            {
                if (World.diagnostics && World.diagnostics.uiFont) GUI.skin.font = World.diagnostics.uiFont;
                GUI.depth = -200;
                GUI.color = new Color(0, 0, 0, .72f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
                var area = new Rect((Screen.width - 420) / 2f, (Screen.height - 300) / 2f, 420, 300);
                GUI.Box(area, "");
                var title = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleCenter };
                var line = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
                bool cleared = result.Outcome == RunOutcome.ChapterCleared;
                GUI.Label(new Rect(area.x, area.y + 24, area.width, 44), cleared ? "1챕터 클리어" : "사망", title);
                GUI.Label(new Rect(area.x, area.y + 90, area.width, 28), "플레이 시간  " + FormatTime(result.ElapsedSeconds), line);
                GUI.Label(new Rect(area.x, area.y + 122, area.width, 28), "처치 수  " + result.Kills, line);
                if (cleared && ProfileRun) GUI.Label(new Rect(area.x, area.y + 154, area.width, 28), PaletteLine, line);
                // Both endings return to the starting village (user decision 2026-10-31).
                if (GUI.Button(new Rect(area.x + 110, area.y + 200, 200, 48), "마을로")) Host.GoToVillage();
            }
            finally { GUI.skin.font = oldFont; GUI.depth = oldDepth; GUI.color = Color.white; }
        }

        // The village palette after this clear, for the clear result.
        public static string PaletteLine => $"마을 잉크  {Chapter1ProfileHost.Service.PalettePieces} / {Chapter1ProfileHost.Service.Catalog.PalettePieces}";

        public static string FormatTime(double seconds)
        {
            int total = (int)Math.Floor(seconds);
            return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }
    }
}
