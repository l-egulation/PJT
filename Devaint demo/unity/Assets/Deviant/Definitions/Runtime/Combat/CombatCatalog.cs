using System;

// Compatibility DTOs for the existing JSON catalog. Keep field names and the
// namespace stable while runtime consumers migrate to validated snapshots.
namespace Deviant.Combat
{
    [Serializable] public class BoxData
    {
        public float width, height, x, y;
    }
    [Serializable] public class HitData
    {
        public float time, damage, active, root, stun, push, pushSeconds;
        public bool pushWithCaster;
        public string anchor, targets;
        public BoxData box;
    }
    [Serializable] public class MoveData
    {
        public string type, wallPolicy;
        public float start, duration, distance, speed, wallRecovery, gap,recastWindow,targetRadius;
        public BoxData targetBox;
        public bool primaryPush;
        public float endDamage;
    }
    [Serializable] public class EffectData
    {
        public string type;
        public float duration, hp, reduction, speed, distance, radius;
        public float x, y;
        public float parryWindow, parryDamage;
        public BoxData box;
    }
    [Serializable] public class SkillData
    {
        public string kind;
        public BombData bomb;
        public bool unavailable;
        public MissileData missile;
        public RangedData ranged;
        public string id, name, slot, vertical, facing, description;
        public string summary; // The bag's player-facing line from data/combat/skill_descriptions.json.
        public float cooldown, activation, end, inputSpeed;
        public Deviant.Definitions.ActionTrackDocument actionTracks;
        public HitData[] hits;
        public MoveData movement;
        public EffectData effect;
        public float[] invulStart, invulEnd;
        public float[] verticalFreeze;
        public float chargeMin, chargeMax, chargeMinDamage, chargeMaxDamage;
        public int chargeSteps; // 0 = damage and size grow continuously; n >= 2 = n equal hold bands
        public bool chargeHoldUntilRelease;
        public float chargeReleaseWindup;
        public float basicLungeDistance, basicLungeStart, basicLungeDuration;
        public int capacity = 1;
    }
    [Serializable] public class MissileData
    {
        public float speed, distance, blastRadius, recovery, manualDelay, recoilHeight, recoilDistance;
        // 비행 거리 비례 폭발 피해: 발진부터 distanceBonusFullDistance 만큼 날면 피해가 distanceBonusMaxDamage(총합)에 이른다. 0 = 끔.
        public float distanceBonusMaxDamage, distanceBonusFullDistance;
    }
    [Serializable] public class BasicAdvanceData
    {
        public float distance, start, duration;
    }
    [Serializable] public class BasicData
    {
        public string kind;
        public BombData bomb;
        public RangedData ranged;
        public string name;
        public float damage, duration, impact, active;
        public BasicAdvanceData advance; // Optional authored ground step; omitted weapons retain their existing timeline.
        public BoxData box;
    }
    [Serializable] public class BombData
    {
        public float angle, speed, gravity, radius, blastRadius, lifetime, spawnHeight, spawnGap, fuseSeconds;
        public bool explodeOnExpiry;
        public float fragmentDamage, fragmentSpeed, fragmentGravity, fragmentRadius, fragmentBlastRadius, fragmentLifetime, fragmentSpawnOffset;
        public float[] fragmentAngles;
    }
    [Serializable] public class RangedData
    {
        public string type;
        public bool pierce, noBasicStagger, ignoreTerrain;
        public float speed, distance, radius, spawnHeight, spawnX, spawnGap;
        public float thickness, recoilDistance, recoilDuration,recoilHopHeight,blastRadius,returnDelay,behindGap,hitInterval;
        // Charged shots only: collision radius at full charge. The ordinary radius is the minimum-charge size.
        public float chargeRadius;
        public bool unlimitedRange;
        public float impactBoxSize;
        // Basic shuriken weak homing: turn rate (degrees/s), acquisition half-cone and deflection cap (degrees, from the throw direction). 0 = straight.
        public float homingTurnRate, homingCone, homingMaxAngle;
        public float[] angles;
    }
    [Serializable] public class FormData
    {
        public string id, name, category;
        public float inputSpeed;
        public BasicData[] basics;
        public SkillData[] specials;
    }
    [Serializable] public class EnemyHitReactionData { public float basicPushDistance, basicPushSeconds, basicStaggerSeconds, basicPushOverlap; }
    [Serializable] public class WeaponSwitchData { public float afterHitWindupReduction; }
    [Serializable] public class StealthAmbushData
    {
        public float firstHitBonusDamage;
        public bool useSourceBonuses;
        public float stealthFirstHitBonusDamage, smokeFirstHitBonusDamage;
        // 숨죽이기 은신이 풀린 뒤 기본공격 공속 보너스(0.5 = +50%)와 지속 시간(초). 없으면 0(효과 없음).
        public float stealthExitBasicSpeedBonus, stealthExitSeconds;
    }
    [Serializable] public class Catalog { public FormData[] forms; public EnemyHitReactionData enemyHitReaction; public WeaponSwitchData weaponSwitch; public StealthAmbushData stealthAmbush; }
}

namespace Deviant.Definitions
{
    public sealed class EnemyHitReactionDefinition
    {
        public float BasicStaggerSeconds { get; }
        public float BasicPushDistance { get; }
        public float BasicPushSeconds { get; }
        public float BasicPushOverlap { get; }

        EnemyHitReactionDefinition(Deviant.Combat.EnemyHitReactionData source)
        {
            BasicStaggerSeconds = source.basicStaggerSeconds;
            BasicPushDistance = source.basicPushDistance;
            BasicPushSeconds = source.basicPushSeconds;
            BasicPushOverlap = source.basicPushOverlap;
        }

        public static bool TryCreate(Deviant.Combat.EnemyHitReactionData source, out EnemyHitReactionDefinition definition, out string error)
        {
            definition = null; error = null;
            if (source == null || !Finite(source.basicStaggerSeconds) || source.basicStaggerSeconds <= 0 ||
                !Finite(source.basicPushDistance) || source.basicPushDistance < 0 ||
                !Finite(source.basicPushSeconds) || source.basicPushSeconds <= 0 ||
                !Finite(source.basicPushOverlap) || source.basicPushOverlap < 0 ||
                !Finite(source.basicPushDistance / source.basicPushSeconds))
                error = "Enemy hit reaction requires positive finite stagger/push durations and nonnegative finite push distance, speed and melee overlap.";
            if (error != null) return false;
            definition = new EnemyHitReactionDefinition(source);
            return true;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    // Switching to the other weapon after the running action's first hit/effect
    // shortens the new attack's windup (start to first hit) by this fraction.
    // 기습(data/combat/stealth_ambush.json): 은신 중 시작한 플레이어 직접 타격 공격의 첫 적중 대상 하나에 더하는 기준 피해.
    // 값이 없는 이전 카탈로그는 기습 없음(0)으로 읽는다. 음수·비유한 값은 거부한다.
    public sealed class StealthAmbushDefinition
    {
        public static readonly StealthAmbushDefinition None = new StealthAmbushDefinition(0, 0, 0, 0, 0);
        public float FirstHitBonusDamage { get; }
        public float StealthFirstHitBonusDamage { get; }
        public float SmokeFirstHitBonusDamage { get; }
        // 숨죽이기 은신 해제 후 ExitSeconds 동안 기본공격 공속 ×(1 + ExitBasicSpeedBonus).
        public float ExitBasicSpeedBonus { get; }
        public float ExitSeconds { get; }

        StealthAmbushDefinition(float bonus, float stealth, float smoke, float exitBonus, float exitSeconds)
        { FirstHitBonusDamage = bonus; StealthFirstHitBonusDamage = stealth; SmokeFirstHitBonusDamage = smoke; ExitBasicSpeedBonus = exitBonus; ExitSeconds = exitSeconds; }
        public float BonusFor(bool stealth, bool smoke) => stealth && smoke ? Math.Max(StealthFirstHitBonusDamage, SmokeFirstHitBonusDamage) :
            stealth ? StealthFirstHitBonusDamage : smoke ? SmokeFirstHitBonusDamage : 0;

        public static bool TryCreate(Deviant.Combat.StealthAmbushData source, out StealthAmbushDefinition definition, out string error)
        {
            definition = null; error = null;
            if (source == null) { definition = None; return true; }
            float bonus = source.firstHitBonusDamage;
            float stealth = source.useSourceBonuses ? source.stealthFirstHitBonusDamage : bonus;
            float smoke = source.useSourceBonuses ? source.smokeFirstHitBonusDamage : bonus;
            foreach (float value in new[] { bonus, stealth, smoke })
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0) { error = "Stealth ambush bonus damage must be finite and non-negative."; return false; }
            foreach (float value in new[] { source.stealthExitBasicSpeedBonus, source.stealthExitSeconds })
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0) { error = "Stealth exit haste must be finite and non-negative."; return false; }
            definition = new StealthAmbushDefinition(bonus, stealth, smoke, source.stealthExitBasicSpeedBonus, source.stealthExitSeconds);
            return true;
        }
    }
    public sealed class WeaponSwitchDefinition
    {
        public float AfterHitWindupReduction { get; }

        WeaponSwitchDefinition(Deviant.Combat.WeaponSwitchData source) { AfterHitWindupReduction = source.afterHitWindupReduction; }

        public static bool TryCreate(Deviant.Combat.WeaponSwitchData source, out WeaponSwitchDefinition definition, out string error)
        {
            definition = null; error = null;
            if (source == null || float.IsNaN(source.afterHitWindupReduction) || source.afterHitWindupReduction < 0 || source.afterHitWindupReduction >= 1)
                error = "Weapon switch windup reduction must be a finite fraction in [0, 1).";
            if (error != null) return false;
            definition = new WeaponSwitchDefinition(source);
            return true;
        }
    }
}
