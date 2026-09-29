package com.hanjjak.sim

import kotlin.math.floor
import kotlin.math.pow

enum class RaidCombatEventType {
    BATTLE_STARTED,
    ESCALATION,
    PLAYER_SKILL_CAST,
    PLAYER_HIT,
    DOT_HIT,
    BOSS_HIT,
    PLAYER_DIED,
    TIME_LIMIT_REACHED,
}

data class RaidCombatInput(
    val contentVersion: String,
    val seed: Long,
    val player: FighterStats,
    val skills: SkillProfile,
    val bossInitialAttack: Int,
    val bossInitialDefense: Int,
    val maxTicks: Int = 3_000,
) {
    init {
        require(contentVersion.isNotBlank())
        require(bossInitialAttack >= 0)
        require(bossInitialDefense >= 0)
        require(maxTicks > 0)
    }
}

data class RaidCombatEvent(
    val sequence: Int,
    val logicalTick: Int,
    val type: RaidCombatEventType,
    val skillId: String? = null,
    val damage: Int? = null,
    val critical: Boolean = false,
    val playerHpBefore: Int? = null,
    val playerHpAfter: Int? = null,
    val escalationStage: Int? = null,
    val bossAttack: Int? = null,
    val bossDefense: Int? = null,
)

data class RaidCombatResult(
    val elapsedTicks: Int,
    val totalDamage: Long,
    val remainingPlayerHp: Int,
    val playerDied: Boolean,
    val events: List<RaidCombatEvent>,
)

object RaidSimulator {
    private const val ESCALATION_INTERVAL_TICKS = 50
    private const val ACTIVE_COOLDOWN_TICKS = 100
    private const val BASE_BUFF_DURATION_TICKS = 50
    private const val BOSS_ACTION_INTERVAL_TICKS = 10

    fun simulate(input: RaidCombatInput): RaidCombatResult = Runtime(input).run()

    private class Runtime(private val input: RaidCombatInput) {
        private var playerHp = input.player.maxHp
        private var totalDamage = 0L
        private var tick = 0
        private var nextPlayerAction = 0.0
        private var nextBossAction = 0
        private val cooldownUntil = IntArray(4)
        private var hasteUntil = 0
        private var amplificationUntil = 0
        private val dotTicks = mutableListOf<Int>()
        private var random = input.seed
        private val events = mutableListOf<RaidCombatEvent>()
        private var currentEscalationStage = 0
        private var bossAttack = input.bossInitialAttack
        private var bossDefense = input.bossInitialDefense

        fun run(): RaidCombatResult {
            emit(RaidCombatEventType.BATTLE_STARTED)
            while (tick < input.maxTicks && playerHp > 0) {
                applyEscalation()
                applyDots()
                if (tick + 1e-9 >= nextPlayerAction) playerAction()
                if (playerHp > 0 && tick >= nextBossAction) bossAction()
                if (playerHp <= 0) {
                    emit(RaidCombatEventType.PLAYER_DIED, playerHpBefore = 0, playerHpAfter = 0)
                    return result(playerDied = true)
                }
                tick++
            }
            emit(RaidCombatEventType.TIME_LIMIT_REACHED)
            return result(playerDied = false)
        }

        private fun applyEscalation() {
            val stage = tick / ESCALATION_INTERVAL_TICKS
            if (stage == currentEscalationStage) return
            currentEscalationStage = stage
            bossAttack = escalated(input.bossInitialAttack, stage)
            bossDefense = escalated(input.bossInitialDefense, stage)
            emit(
                RaidCombatEventType.ESCALATION,
                escalationStage = stage,
                bossAttack = bossAttack,
                bossDefense = bossDefense,
            )
        }

        private fun playerAction() {
            for (skillId in input.skills.activeOrder) {
                when (skillId) {
                    "active_haste" -> if (input.skills.hasteBasisPoints > 0 && tick >= cooldownUntil[0]) {
                        cooldownUntil[0] = tick + ACTIVE_COOLDOWN_TICKS
                        hasteUntil = tick + BASE_BUFF_DURATION_TICKS + input.skills.buffDurationBonusTicks
                        emit(RaidCombatEventType.PLAYER_SKILL_CAST, skillId = skillId)
                        scheduleNextPlayerAction()
                        return
                    }
                    "active_basic_amp" -> if (input.skills.basicAmplificationBasisPoints > 0 && tick >= cooldownUntil[1]) {
                        cooldownUntil[1] = tick + ACTIVE_COOLDOWN_TICKS
                        amplificationUntil = tick + BASE_BUFF_DURATION_TICKS + input.skills.buffDurationBonusTicks
                        emit(RaidCombatEventType.PLAYER_SKILL_CAST, skillId = skillId)
                        scheduleNextPlayerAction()
                        return
                    }
                    "active_heavy" -> if (input.skills.heavyBasisPoints > 0 && tick >= cooldownUntil[2]) {
                        cooldownUntil[2] = tick + ACTIVE_COOLDOWN_TICKS
                        hit(input.skills.heavyBasisPoints, RaidCombatEventType.PLAYER_HIT, skillId, canCritical = true)
                        scheduleNextPlayerAction()
                        return
                    }
                    "active_dot" -> if (input.skills.dotTotalBasisPoints > 0 && tick >= cooldownUntil[3]) {
                        cooldownUntil[3] = tick + ACTIVE_COOLDOWN_TICKS
                        repeat(5) { dotTicks += tick + (it + 1) * 10 }
                        emit(RaidCombatEventType.PLAYER_SKILL_CAST, skillId = skillId)
                        scheduleNextPlayerAction(1.0 / 3.0)
                        return
                    }
                }
            }
            val amplification = 10_000 + input.skills.permanentBasicAmplificationBasisPoints +
                if (tick < amplificationUntil) input.skills.basicAmplificationBasisPoints else 0
            hit(amplification, RaidCombatEventType.PLAYER_HIT, null, canCritical = true)
            scheduleNextPlayerAction()
        }

        private fun applyDots() {
            val count = dotTicks.count { it == tick }
            if (count == 0) return
            dotTicks.removeAll { it == tick }
            repeat(count) {
                hit(input.skills.dotTotalBasisPoints / 5, RaidCombatEventType.DOT_HIT, "active_dot", canCritical = false)
            }
        }

        private fun hit(basisPoints: Int, type: RaidCombatEventType, skillId: String?, canCritical: Boolean) {
            val allDamage = 10_000 + input.skills.allDamageBasisPoints
            var totalBasisPoints = positiveRoundHalfUp(basisPoints * allDamage / 10_000.0)
            val critical = canCritical && nextBasisPoint() < input.skills.criticalChanceBasisPoints
            if (critical) totalBasisPoints = positiveRoundHalfUp(totalBasisPoints * 1.5)
            val damage = CombatSimulator.damage(input.player.attack, input.player.penetration, bossDefense, totalBasisPoints)
            totalDamage = Math.addExact(totalDamage, damage.toLong())
            emit(type, skillId = skillId, damage = damage, critical = critical)
        }

        private fun bossAction() {
            val before = playerHp
            playerHp = (playerHp - bossAttack).coerceAtLeast(0)
            nextBossAction += BOSS_ACTION_INTERVAL_TICKS
            emit(
                RaidCombatEventType.BOSS_HIT,
                damage = bossAttack,
                playerHpBefore = before,
                playerHpAfter = playerHp,
                bossAttack = bossAttack,
                bossDefense = bossDefense,
            )
        }

        private fun scheduleNextPlayerAction(recoveryMultiplier: Double = 1.0) {
            val haste = (input.skills.permanentHasteBasisPoints +
                if (tick < hasteUntil) input.skills.hasteBasisPoints else 0).coerceAtMost(10_000)
            val interval = (4.0 / (1.0 + haste / 10_000.0)).coerceAtLeast(2.0) * recoveryMultiplier
            nextPlayerAction += interval
        }

        private fun nextBasisPoint(): Int {
            random += -7046029254386353131L
            var value = random
            value = (value xor (value ushr 30)) * -4658895280553007687L
            value = (value xor (value ushr 27)) * -7723592293110705685L
            return ((value xor (value ushr 31)).ushr(1) % 10_000).toInt()
        }

        private fun emit(
            type: RaidCombatEventType,
            skillId: String? = null,
            damage: Int? = null,
            critical: Boolean = false,
            playerHpBefore: Int? = null,
            playerHpAfter: Int? = null,
            escalationStage: Int? = null,
            bossAttack: Int? = null,
            bossDefense: Int? = null,
        ) {
            events += RaidCombatEvent(
                sequence = events.size + 1,
                logicalTick = tick,
                type = type,
                skillId = skillId,
                damage = damage,
                critical = critical,
                playerHpBefore = playerHpBefore,
                playerHpAfter = playerHpAfter,
                escalationStage = escalationStage,
                bossAttack = bossAttack,
                bossDefense = bossDefense,
            )
        }

        private fun result(playerDied: Boolean) = RaidCombatResult(
            elapsedTicks = tick,
            totalDamage = totalDamage,
            remainingPlayerHp = playerHp,
            playerDied = playerDied,
            events = events.toList(),
        )
    }

    private fun escalated(initial: Int, stage: Int): Int = positiveRoundHalfUp(initial * 1.05.pow(stage))
    private fun positiveRoundHalfUp(value: Double): Int = floor(value + 0.5).toInt()
}
