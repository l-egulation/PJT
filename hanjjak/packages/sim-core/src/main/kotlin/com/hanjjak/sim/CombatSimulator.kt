package com.hanjjak.sim

import kotlin.math.exp
import kotlin.math.floor

data class FighterStats(val attack: Int, val maxHp: Int, val penetration: Int)
data class EnemyStats(val hp: Int, val attack: Int, val defense: Int)
data class SkillProfile(
    val heavyBasisPoints: Int = 0,
    val dotTotalBasisPoints: Int = 0,
    val hasteBasisPoints: Int = 0,
    val basicAmplificationBasisPoints: Int = 0,
    val criticalChanceBasisPoints: Int = 0,
    val allDamageBasisPoints: Int = 0,
    val permanentHasteBasisPoints: Int = 0,
    val permanentBasicAmplificationBasisPoints: Int = 0,
    val buffDurationBonusTicks: Int = 0,
    val activeOrder: List<String> = listOf("active_haste", "active_basic_amp", "active_heavy", "active_dot"),
)
data class CycleInput(
    val contentVersion: String,
    val seed: Long,
    val player: FighterStats,
    val normal: EnemyStats,
    val boss: EnemyStats,
    val skills: SkillProfile = SkillProfile(),
    val normalCount: Int = 20,
    val bossTimeLimitTicks: Int? = null,
    val scheduledStrikes: Map<Int, Int> = emptyMap(),
)
data class CycleResult(
    val success: Boolean,
    val failureCode: String?,
    val remainingHp: Int,
    val defeatedNormals: Int,
    val elapsedTicks: Int,
)

data class CombatRenderingEvent(
    val eventId: String,
    val logicalTick: Int,
    val type: String,
    val actor: String? = null,
    val target: String? = null,
    val enemyIndex: Int? = null,
    val boss: Boolean = false,
    val skillId: String? = null,
    val damage: Int? = null,
    val critical: Boolean = false,
    val hpBefore: Int? = null,
    val hpAfter: Int? = null,
    val defeated: Boolean = false,
)

data class CycleTrace(val result: CycleResult, val renderingTimeline: List<CombatRenderingEvent>)

object CombatSimulator {
    fun damage(attack: Int, penetration: Int, defense: Int, skillBasisPoints: Int = 10_000): Int {
        val multiplier = 0.5 + 1.0 / (1.0 + exp(-0.015 * (penetration - defense)))
        return roundHalfUp(attack * (skillBasisPoints / 10_000.0) * multiplier).coerceAtLeast(1)
    }

    fun simulate(input: CycleInput): CycleResult = run(input, collectEvents = false).result

    fun simulateWithEvents(input: CycleInput): CycleTrace = run(input, collectEvents = true)

    private fun run(input: CycleInput, collectEvents: Boolean): CycleTrace {
        val runtime = Runtime(input, collectEvents)
        repeat(input.normalCount) { index ->
            val outcome = runtime.fight(input.normal, null, emptyMap(), index + 1, false)
            if (!outcome.won) {
                val result = CycleResult(false, outcome.failureCode, 0, index, runtime.tick)
                runtime.battleFailed(outcome.failureCode)
                return CycleTrace(result, runtime.renderingTimeline())
            }
            if (index == input.normalCount - 1) {
                runtime.tick += 5
                runtime.discardDotTicksWithoutTarget()
            }
        }
        val outcome = runtime.fight(input.boss, input.bossTimeLimitTicks, input.scheduledStrikes, input.normalCount + 1, true)
        if (!outcome.won) {
            val result = CycleResult(false, outcome.failureCode, runtime.hp.coerceAtLeast(0), input.normalCount, runtime.tick)
            runtime.battleFailed(outcome.failureCode)
            return CycleTrace(result, runtime.renderingTimeline())
        }
        runtime.emit(type = "BATTLE_CYCLE_COMPLETED")
        return CycleTrace(CycleResult(true, null, input.player.maxHp, input.normalCount, runtime.tick), runtime.renderingTimeline())
    }

    private class Runtime(private val input: CycleInput, private val collectEvents: Boolean) {
        var hp = input.player.maxHp
        var tick = 0
        private val events = if (collectEvents) mutableListOf<CombatRenderingEvent>() else null
        private var nextPlayerAction = 0.0
        private var nextEnemyAction = 0
        private val cooldownUntil = IntArray(4)
        private var hasteUntil = 0
        private var amplificationUntil = 0
        private val dotTicks = mutableListOf<Int>()
        private var random = input.seed
        private var eventSequence = 0

        fun fight(enemy: EnemyStats, limitTicks: Int?, strikes: Map<Int, Int>, enemyIndex: Int, boss: Boolean): FightResult {
            var enemyHp = enemy.hp
            val startedAt = tick
            nextEnemyAction = tick
            if (nextPlayerAction < tick) nextPlayerAction = tick.toDouble()
            emit(if (boss) "BOSS_SPAWNED" else "ENEMY_SPAWNED", actor = "ENEMY", enemyIndex = enemyIndex, boss = boss, hpBefore = enemyHp, hpAfter = enemyHp)
            while (enemyHp > 0 && hp > 0) {
                val relativeTick = tick - startedAt
                if (limitTicks != null && relativeTick >= limitTicks) return FightResult(false, "TIME_LIMIT")
                val dotHit = dotDamageAt(enemy, tick)
                if (dotHit != null) {
                    val before = enemyHp
                    enemyHp -= dotHit.damage
                    emit("DOT_TICK", "PLAYER", "ENEMY", enemyIndex, boss, "active_dot", dotHit.damage, false, before, enemyHp.coerceAtLeast(0), enemyHp <= 0)
                }
                if (tick + 1e-9 >= nextPlayerAction) {
                    val action = playerAction(enemy)
                    emit(action.startedType, "PLAYER", "ENEMY", enemyIndex, boss, action.skillId)
                    action.buffType?.let { emit(it, "PLAYER", "PLAYER", enemyIndex, boss, action.skillId) }
                    action.hit?.let { hit ->
                        val before = enemyHp
                        enemyHp -= hit.damage
                        emit(action.impactType, "PLAYER", "ENEMY", enemyIndex, boss, action.skillId, hit.damage, hit.critical, before, enemyHp.coerceAtLeast(0), enemyHp <= 0)
                    }
                    scheduleNextPlayerAction(action.recoveryMultiplier)
                }
                if (enemyHp <= 0) {
                    emit("ENEMY_DEFEATED", "PLAYER", "ENEMY", enemyIndex, boss, hpBefore = 0, hpAfter = 0, defeated = true)
                    break
                }
                if (tick >= nextEnemyAction) {
                    emit("ENEMY_ATTACK_STARTED", "ENEMY", "PLAYER", enemyIndex, boss)
                    val before = hp
                    hp -= enemy.attack
                    emit("PLAYER_HIT", "ENEMY", "PLAYER", enemyIndex, boss, damage = enemy.attack, hpBefore = before, hpAfter = hp.coerceAtLeast(0), defeated = hp <= 0)
                    nextEnemyAction += 10
                }
                val strikeDamage = strikes[relativeTick] ?: 0
                if (strikeDamage > 0) {
                    val before = hp
                    hp -= strikeDamage
                    emit("PLAYER_HIT", "ENEMY", "PLAYER", enemyIndex, boss, "boss_scheduled_strike", strikeDamage, hpBefore = before, hpAfter = hp.coerceAtLeast(0), defeated = hp <= 0)
                }
                if (hp <= 0) {
                    emit("PLAYER_DEFEATED", "ENEMY", "PLAYER", enemyIndex, boss, hpBefore = 0, hpAfter = 0, defeated = true)
                    return FightResult(false, "PLAYER_DIED")
                }
                tick++
            }
            return FightResult(true, null)
        }

        fun battleFailed(failureCode: String?) = emit("BATTLE_FAILED", skillId = failureCode)

        fun discardDotTicksWithoutTarget() = dotTicks.removeAll { it <= tick }
        fun renderingTimeline(): List<CombatRenderingEvent> = events?.toList() ?: emptyList()

        fun emit(
            type: String,
            actor: String? = null,
            target: String? = null,
            enemyIndex: Int? = null,
            boss: Boolean = false,
            skillId: String? = null,
            damage: Int? = null,
            critical: Boolean = false,
            hpBefore: Int? = null,
            hpAfter: Int? = null,
            defeated: Boolean = false,
        ) {
            val sink = events ?: return
            eventSequence++
            sink += CombatRenderingEvent("event-%05d".format(eventSequence), tick, type, actor, target, enemyIndex, boss, skillId, damage, critical, hpBefore, hpAfter, defeated)
        }

        private fun playerAction(enemy: EnemyStats): PlayerAction {
            val skills = input.skills
            for (skillId in skills.activeOrder) {
                when (skillId) {
                    "active_haste" -> if (skills.hasteBasisPoints > 0 && tick >= cooldownUntil[0]) {
                        cooldownUntil[0] = tick + 100
                        hasteUntil = tick + 50 + skills.buffDurationBonusTicks
                        return PlayerAction("PLAYER_SKILL_CAST_STARTED", "PLAYER_SKILL_IMPACT", skillId, buffType = "BUFF_STARTED")
                    }
                    "active_basic_amp" -> if (skills.basicAmplificationBasisPoints > 0 && tick >= cooldownUntil[1]) {
                        cooldownUntil[1] = tick + 100
                        amplificationUntil = tick + 50 + skills.buffDurationBonusTicks
                        return PlayerAction("PLAYER_SKILL_CAST_STARTED", "PLAYER_SKILL_IMPACT", skillId, buffType = "BUFF_STARTED")
                    }
                    "active_heavy" -> if (skills.heavyBasisPoints > 0 && tick >= cooldownUntil[2]) {
                        cooldownUntil[2] = tick + 100
                        return PlayerAction("PLAYER_SKILL_CAST_STARTED", "PLAYER_SKILL_IMPACT", skillId, hit(enemy, skills.heavyBasisPoints, true))
                    }
                    "active_dot" -> if (skills.dotTotalBasisPoints > 0 && tick >= cooldownUntil[3]) {
                        cooldownUntil[3] = tick + 100
                        repeat(5) { dotTicks.add(tick + (it + 1) * 10) }
                        return PlayerAction("PLAYER_SKILL_CAST_STARTED", "PLAYER_SKILL_IMPACT", skillId, recoveryMultiplier = 1.0 / 3.0)
                    }
                }
            }
            val amplification = 10_000 + skills.permanentBasicAmplificationBasisPoints + if (tick < amplificationUntil) skills.basicAmplificationBasisPoints else 0
            return PlayerAction("PLAYER_BASIC_ATTACK_STARTED", "PLAYER_ATTACK_IMPACT", null, hit(enemy, amplification, true))
        }

        private fun dotDamageAt(enemy: EnemyStats, currentTick: Int): Hit? {
            val count = dotTicks.count { it == currentTick }
            if (count == 0) return null
            dotTicks.removeAll { it == currentTick }
            return Hit(count * hit(enemy, input.skills.dotTotalBasisPoints / 5, false).damage, false)
        }

        private fun hit(enemy: EnemyStats, basisPoints: Int, canCritical: Boolean): Hit {
            val allDamage = 10_000 + input.skills.allDamageBasisPoints
            var totalBasisPoints = roundHalfUp(basisPoints * allDamage / 10_000.0)
            val critical = canCritical && nextBasisPoint() < input.skills.criticalChanceBasisPoints
            if (critical) totalBasisPoints = roundHalfUp(totalBasisPoints * 1.5)
            return Hit(damage(input.player.attack, input.player.penetration, enemy.defense, totalBasisPoints), critical)
        }

        private fun scheduleNextPlayerAction(recoveryMultiplier: Double = 1.0) {
            val haste = (input.skills.permanentHasteBasisPoints + if (tick < hasteUntil) input.skills.hasteBasisPoints else 0).coerceAtMost(10_000)
            val interval = (4.0 / (1.0 + haste / 10_000.0)).coerceAtLeast(2.0) * recoveryMultiplier
            // Keep sub-tick time so small haste bonuses accumulate instead of rounding away each action.
            nextPlayerAction += interval
        }

        private fun nextBasisPoint(): Int {
            random += -7046029254386353131L
            var value = random
            value = (value xor (value ushr 30)) * -4658895280553007687L
            value = (value xor (value ushr 27)) * -7723592293110705685L
            return ((value xor (value ushr 31)).ushr(1) % 10_000).toInt()
        }
    }

    private data class Hit(val damage: Int, val critical: Boolean)
    private data class PlayerAction(
        val startedType: String,
        val impactType: String,
        val skillId: String?,
        val hit: Hit? = null,
        val buffType: String? = null,
        val recoveryMultiplier: Double = 1.0,
    )
    private data class FightResult(val won: Boolean, val failureCode: String?)
    private fun roundHalfUp(value: Double): Int = floor(value + 0.5).toInt()
}
