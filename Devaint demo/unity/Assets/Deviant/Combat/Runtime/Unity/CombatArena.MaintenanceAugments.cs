using System;
using System.Collections.Generic;
using Deviant.Combat.Contracts;
using Deviant.Run;
using UnityEngine;

namespace Deviant.Combat
{
    // Maintenance augments in combat (docs/design/progression/maintenance-augments.md). The run layer
    // (WeaponGrowth.Augments) owns the cards and their additive totals; this layer applies them to hits, healing,
    // movement, attack speed, cooldowns and maximum health, and keeps the per-attack and per-enemy counters.
    // Every value is neutral without owned cards, so arenas without a run growth behave exactly as before.
    public partial class CombatArena
    {
        public const string UnarmedFormId = "unarmed";
        static AugmentCatalog augmentCatalog;

        public static AugmentCatalog LoadMaintenanceAugmentCatalog()
        {
            if (augmentCatalog != null) return augmentCatalog;
            var text = Resources.Load<TextAsset>("MaintenanceAugments");
            if (!text) return null;
            var catalog = JsonUtility.FromJson<AugmentCatalog>(text.text);
            catalog.Validate();
            return augmentCatalog = catalog;
        }

        // The run's augments; a growth created without them (chapter 2 without a carried run, training) gets them here.
        public MaintenanceAugments Augments
        {
            get
            {
                var growth = MvpGrowth;
                if (growth == null) return null;
                if (growth.Augments == null)
                {
                    var catalog = LoadMaintenanceAugmentCatalog();
                    if (catalog != null) growth.AttachAugments(new MaintenanceAugments(catalog, Environment.TickCount));
                }
                return growth.Augments;
            }
        }
        MaintenanceAugments OwnedAugments { get { var a = Augments; return a != null && a.Owned.Count > 0 ? a : null; } }

        // Attack-scoped bonus of 이번엔 너다 / 다섯 번째는 진심, keyed by the cast execution that owns every hit of one attack.
        sealed class AugmentAttack { public float WeaponBonus; public bool Counted, Repeat; }
        readonly Dictionary<Guid, AugmentAttack> augmentAttacks = new Dictionary<Guid, AugmentAttack>();
        readonly Dictionary<int, (Guid execution, int count)> augmentEnemyHits = new Dictionary<int, (Guid, int)>();
        int augmentAttackCount, augmentHitAttackCount, augmentSignTarget = -1, augmentSignCount;
        Guid augmentLastWeapon, augmentSignExecution;
        float augmentLeechReadyAt = float.NegativeInfinity, augmentUnhurtFrom, augmentHurtUntil = float.NegativeInfinity;
        float augmentMaxHealthDelta;
        bool augmentBookmarked, augmentUnhurtStarted; Vector2 augmentBookmark; float augmentReturnAt;

        public float AugmentWalkBonus => OwnedAugments?.WalkSpeedBonus ?? 0;
        public float AugmentBasicSpeedScale => OwnedAugments?.BasicSpeedScale ?? 1;
        public float AugmentSkillCooldownReduction => OwnedAugments?.SkillCooldownReduction ?? 0;
        public float AugmentHealingScale => OwnedAugments?.HealingScale ?? 1;
        public bool AugmentBookmarkActive => augmentBookmarked;
        public Vector2 AugmentBookmarkPosition => augmentBookmark;

        void ResetMaintenanceAugmentState()
        {
            augmentAttacks.Clear(); augmentEnemyHits.Clear();
            augmentAttackCount = augmentHitAttackCount = augmentSignCount = 0; augmentSignTarget = -1;
            augmentLastWeapon = augmentSignExecution = Guid.Empty;
            augmentLeechReadyAt = augmentHurtUntil = float.NegativeInfinity; augmentUnhurtFrom = Now;
            augmentBookmarked = augmentUnhurtStarted = false;
        }

        // Attack and skill hits of a weapon action. Ink effects, MVP effects, reflected shots and run extras are not.
        static bool IsAugmentAttack(CombatHitOrigin origin) =>
            origin.IsSpecified && origin.Stamp.WeaponId != Guid.Empty && origin.Kind != CombatHitKind.InkPart && origin.Reflector.ActorId <= 0 &&
            !(origin.ActionId != null && origin.ActionId.StartsWith("mvp_", StringComparison.Ordinal));

        // Called once when a player cast is created (an attack starts, whether or not it later hits).
        internal void BeginAugmentAttack(CombatAttackStamp stamp, Guid execution)
        {
            var a = OwnedAugments;
            if (a == null) return;
            if (augmentAttacks.Count > 256) augmentAttacks.Clear();
            var attack = new AugmentAttack();
            if (stamp.WeaponId != Guid.Empty && stamp.FormId != UnarmedFormId)
            {
                augmentAttackCount++;
                foreach (var card in a.With(e => e.everyAttack))
                    attack.WeaponBonus += augmentAttackCount % Mathf.Max(1, (int)card.effect.everyAttack) == 0 ? card.effect.everyAttackDamage : card.effect.otherAttackDamage;
                if (augmentLastWeapon != Guid.Empty)
                    attack.WeaponBonus += augmentLastWeapon != stamp.WeaponId ? a.Sum(e => e.switchFirstDamage) : a.Sum(e => e.repeatAttackDamage);
                augmentLastWeapon = stamp.WeaponId;
            }
            augmentAttacks[execution] = attack;
            float bookmark = a.Sum(e => e.bookmarkSeconds);
            if (!stamp.Basic && bookmark > 0 && !augmentBookmarked && Player && !Player.Dead)
            { augmentBookmarked = true; augmentBookmark = Player.Position; augmentReturnAt = Now + bookmark; }
        }

        // The augment step of one player hit, multiplied after the ink step (critical chance included).
        internal float ScalePlayerDamage(float damage, CombatHitOrigin origin, CombatActor target)
        {
            var a = OwnedAugments;
            if (Sketch != null)
            {
                float sketchChance = a == null ? PlayerCritChance : Mathf.Min(a.Catalog.maximumCritChance, PlayerCritChance + a.CritChance);
                LastHitWasCritical = sketchChance > 0 && UnityEngine.Random.value < sketchChance;
                float bonus = PlayerDamageScale - 1 + (MvpGrowth?.AttackMultiplier ?? 1) - 1 +
                    (a == null ? 0 : AugmentDamageScale(a, origin, target) - 1) + SketchDamageBonus(origin, target);
                return damage * Mathf.Max(0, 1 + bonus) * (LastHitWasCritical ? 2 : 1);
            }
            if (a == null) return ScalePlayerDamage(damage);
            float chance = Mathf.Min(a.Catalog.maximumCritChance, PlayerCritChance + a.CritChance);
            LastHitWasCritical = chance > 0 && UnityEngine.Random.value < chance;
            return damage * PlayerDamageScale * AugmentDamageScale(a, origin, target) * (LastHitWasCritical ? 2 : 1);
        }

        public float AugmentDamageScale(CombatHitOrigin origin, CombatActor target)
        { var a = OwnedAugments; return a == null ? 1 : AugmentDamageScale(a, origin, target); }

        float AugmentDamageScale(MaintenanceAugments a, CombatHitOrigin origin, CombatActor target)
        {
            bool attack = IsAugmentAttack(origin), weapon = attack && origin.Stamp.FormId != UnarmedFormId, basic = attack && origin.IsBasicAction;
            float bonus = a.Sum(e => e.damage);
            if (weapon)
            {
                bonus += a.Sum(e => e.weaponDamage);
                if (augmentAttacks.TryGetValue(origin.ExecutionId, out var state)) bonus += state.WeaponBonus;
                if (target && (target.isElite || target.isBoss)) bonus += a.Sum(e => e.eliteBossWeaponDamage);
                if (target && Player)
                {
                    int facing = target.facing < 0 ? -1 : 1;
                    if (facing * (Player.Position.x - target.Position.x) >= 0) bonus += a.Sum(e => e.frontWeaponDamage);
                }
            }
            if (attack) bonus += basic ? a.Sum(e => e.basicDamage) : a.Sum(e => e.skillDamage);
            foreach (var card in a.With(e => e.executeThreshold))
                if (target && target.maxHealth > 0 && target.health <= card.effect.executeThreshold * target.maxHealth) bonus += card.effect.executeDamage;
            if (Player && Player.maxHealth > 0)
                foreach (var card in a.With(e => e.lowHealthThreshold))
                    bonus += Player.health <= card.effect.lowHealthThreshold * Player.maxHealth ? card.effect.lowHealthDamage : card.effect.highHealthDamage;
            foreach (var card in a.With(e => e.unhurtMaxDamage))
            {
                var fx = card.effect;
                bonus += Now < augmentHurtUntil ? fx.hurtDamage : Mathf.Min(fx.unhurtMaxDamage, Mathf.Floor(Mathf.Max(0, Now - Mathf.Max(augmentUnhurtFrom, augmentHurtUntil))) * fx.unhurtGainPerSecond);
            }
            foreach (var card in a.With(e => e.nearbyRadius))
            {
                int near = NearbyLivingEnemies(card.effect.nearbyRadius);
                if (near == 1) bonus += card.effect.aloneDamage; else if (near >= 2) bonus += card.effect.crowdDamage;
            }
            return a.DamageScale(bonus);
        }

        int NearbyLivingEnemies(float radius)
        {
            if (!Player) return 0;
            int count = 0; Vector2 at = Player.Position;
            foreach (var e in enemies) if (e && !e.Dead && !e.isDecoy && e.isActiveAndEnabled && Vector2.Distance(at, e.Position) <= radius) count++;
            return count;
        }

        // Damage the player takes, before invulnerability, parry and shields: all augment terms added once.
        internal float AugmentIncomingScale(CombatActor source, CombatHitOrigin origin, bool projectileHit)
        {
            var a = OwnedAugments;
            if (a == null || !Player) return 1;
            float bonus = a.Sum(e => e.incoming);
            int facing = Player.facing < 0 ? -1 : 1;
            if (source && facing * (source.Position.x - Player.Position.x) < 0) bonus += a.Sum(e => e.backIncoming);
            if (projectileHit || origin.Kind == CombatHitKind.Projectile) bonus += a.Sum(e => e.projectileIncoming);
            return a.IncomingScale(bonus);
        }

        internal void AugmentPlayerHurt()
        {
            var a = OwnedAugments;
            if (a == null) return;
            float penalty = a.Sum(e => e.hurtSeconds);
            if (penalty > 0) { augmentHurtUntil = Now + penalty; augmentUnhurtFrom = augmentHurtUntil; }
            else augmentUnhurtFrom = Now;
        }

        // After a player hit was accepted: defeats for 성장통, lifesteal and the counted extra hits.
        internal void AfterAugmentHit(CombatActor target, CombatHitOrigin origin, float healthBefore, bool counted)
        {
            var a = OwnedAugments;
            if (a == null || !target || target.isPlayer) return;
            if (healthBefore > 0 && target.Dead && !target.isDecoy) a.RecordKill();
            if (!counted || !IsAugmentAttack(origin)) return;
            float loss = Mathf.Max(0, healthBefore - Mathf.Max(0, target.health));
            bool weapon = origin.Stamp.FormId != UnarmedFormId;
            augmentAttacks.TryGetValue(origin.ExecutionId, out var attack);
            float extra = 0;
            if (weapon && loss > 0 && Now >= augmentLeechReadyAt && Player && !Player.Dead)
                foreach (var card in a.With(e => e.leechRatio))
                {
                    Player.Heal(Mathf.Min(card.effect.leechMaxHeal, loss * card.effect.leechRatio));
                    augmentLeechReadyAt = Now + card.effect.leechCooldown;
                }
            // 착실한 마침표: per enemy, one count per attack.
            augmentEnemyHits.TryGetValue(target.ActorId, out var seen);
            if (seen.execution != origin.ExecutionId)
            {
                int count = seen.count + 1;
                augmentEnemyHits[target.ActorId] = (origin.ExecutionId, count);
                foreach (var card in a.With(e => e.sameEnemyHits))
                    if (count % Mathf.Max(1, (int)card.effect.sameEnemyHits) == 0) extra += loss * card.effect.sameEnemyExtra;
            }
            // 제자리 인쇄소: weapon attacks that hit anything, counted once per attack; every hit of the third repeats.
            if (weapon && attack != null)
            {
                if (!attack.Counted)
                {
                    attack.Counted = true; augmentHitAttackCount++;
                    foreach (var card in a.With(e => e.attackHits))
                        if (augmentHitAttackCount % Mathf.Max(1, (int)card.effect.attackHits) == 0) attack.Repeat = true;
                }
                if (attack.Repeat) extra += loss * a.Sum(e => e.attackExtra);
            }
            // 사인해 주세요: three different attacks on one enemy; hitting another enemy moves the mark there.
            if (origin.ExecutionId != augmentSignExecution)
                foreach (var card in a.With(e => e.signHits))
                {
                    augmentSignExecution = origin.ExecutionId;
                    if (target.ActorId != augmentSignTarget) { augmentSignTarget = target.ActorId; augmentSignCount = 0; }
                    if (++augmentSignCount >= Mathf.Max(1, (int)card.effect.signHits)) { extra += loss * card.effect.signExtra; augmentSignCount = 0; }
                }
            if (extra > 0 && !target.Dead && Player) target.ReceiveAugmentExtra(extra, Player, Player.facing < 0 ? -1 : 1);
        }

        // 반송 봉투: a player hit box returns enemy shots it touches (once each), doubling their damage.
        internal void AugmentReflectShots(CombatCast cast, Vector2 center, Vector2 size)
        {
            var a = OwnedAugments;
            if (a == null || !cast || !Player || cast.owner != Player) return;
            float multiplier = a.Sum(e => e.reflectDamageMultiplier);
            if (!(multiplier > 0)) return;
            var box = new Rect(center - size / 2, size);
            foreach (var shot in projectiles.ToArray())
            {
                if (!shot || !shot.source || shot.source.isPlayer) continue;
                var state = shot.Execution;
                if (state.Reflected || state.Removed) continue;
                var bounds = new Rect((Vector2)shot.transform.position - shot.CollisionSize / 2, shot.CollisionSize);
                if (box.Overlaps(bounds)) shot.ReflectByAugment(Player, cast.HitOrigin(-1, CombatHitKind.Parry), multiplier);
            }
        }

        // Each simulation step: maximum health and the bookmark return.
        void StepMaintenanceAugments()
        {
            SyncAugmentMaxHealth(true);
            // 무사고 장인 counts unhurt time from when it is first in play (taking it, or a reset).
            if (!augmentUnhurtStarted && OwnedAugments != null && OwnedAugments.Sum(e => e.unhurtMaxDamage) > 0) { augmentUnhurtStarted = true; augmentUnhurtFrom = Now; }
            if (augmentBookmarked && Now >= augmentReturnAt)
            {
                augmentBookmarked = false;
                if (Player && !Player.Dead) Player.TeleportToward(augmentBookmark, true);
            }
        }

        // Applies the augments' maximum health over the augment-free base. A rise adds the same health (heal=false
        // keeps health, for a fresh chapter player); a fall cuts health to the new maximum.
        public void SyncAugmentMaxHealth(bool heal)
        {
            var a = Augments;
            if (a == null || !Player) return;
            float baseMax = Player.maxHealth - augmentMaxHealthDelta;
            float delta = Mathf.Round(baseMax * a.MaxHealthRatio) - baseMax;
            if (Mathf.Approximately(delta, augmentMaxHealthDelta)) return;
            float change = delta - augmentMaxHealthDelta;
            augmentMaxHealthDelta = delta;
            Player.maxHealth = baseMax + delta;
            if (change > 0 && heal && !Player.Dead) Player.health = Mathf.Min(Player.maxHealth, Player.health + change);
            else Player.health = Mathf.Min(Player.health, Player.maxHealth);
        }
    }
}
