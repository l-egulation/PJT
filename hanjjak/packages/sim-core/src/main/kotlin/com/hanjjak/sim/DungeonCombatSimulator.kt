package com.hanjjak.sim

import kotlin.math.exp
import kotlin.math.floor

enum class DungeonMode { SURVIVE, DEFEAT_BOSS }
enum class DungeonEventType { BATTLE_START, SKILL_CAST, PLAYER_HIT, DOT_HIT, BOSS_HIT, SURVIVAL_STRIKE, VICTORY, DEFEAT }

data class DungeonBossStats(
    val hp: Int,
    val attack: Int,
    val defense: Int,
    val durationTicks: Int,
    val strikeDamage: Int = 0,
    val strikeIntervalTicks: Int = 20,
)

data class DungeonCombatInput(
    val contentVersion: String,
    val seed: Long,
    val mode: DungeonMode,
    val player: FighterStats,
    val skills: SkillProfile,
    val boss: DungeonBossStats,
)

data class DungeonCombatEvent(
    val sequence: Int,
    val tick: Int,
    val type: DungeonEventType,
    val skillId: String? = null,
    val amount: Int = 0,
    val playerHp: Int,
    val bossHp: Int,
    val critical: Boolean = false,
)

data class DungeonCombatResult(
    val success: Boolean,
    val failureCode: String?,
    val elapsedTicks: Int,
    val remainingPlayerHp: Int,
    val remainingBossHp: Int,
    val events: List<DungeonCombatEvent>,
)

object DungeonCombatSimulator {
    fun simulate(input: DungeonCombatInput): DungeonCombatResult = Runtime(input).run()

    private class Runtime(private val input: DungeonCombatInput) {
        private var playerHp = input.player.maxHp
        private var bossHp = input.boss.hp
        private var tick = 0
        private var nextPlayerAction = 0.0
        private var nextBossAction = 0
        private val cooldownUntil = IntArray(4)
        private var hasteUntil = 0
        private var amplificationUntil = 0
        private val dotTicks = mutableListOf<Int>()
        private var random = input.seed
        private val events = mutableListOf<DungeonCombatEvent>()

        fun run(): DungeonCombatResult {
            emit(DungeonEventType.BATTLE_START)
            while (tick < input.boss.durationTicks && playerHp > 0 && (input.mode == DungeonMode.SURVIVE || bossHp > 0)) {
                applyDots()
                if (bossHp <= 0 && input.mode == DungeonMode.DEFEAT_BOSS) break
                if (input.mode == DungeonMode.DEFEAT_BOSS && tick >= nextPlayerAction) playerAction()
                if (bossHp <= 0 && input.mode == DungeonMode.DEFEAT_BOSS) break
                if (input.boss.attack > 0 && tick >= nextBossAction) {
                    playerHp -= input.boss.attack
                    nextBossAction += 10
                    emit(DungeonEventType.BOSS_HIT, amount = input.boss.attack)
                }
                if (input.mode == DungeonMode.SURVIVE && tick > 0 && tick % input.boss.strikeIntervalTicks == 0) {
                    playerHp -= input.boss.strikeDamage
                    emit(DungeonEventType.SURVIVAL_STRIKE, amount = input.boss.strikeDamage)
                }
                tick++
            }
            val success = when (input.mode) {
                DungeonMode.SURVIVE -> playerHp > 0 && tick >= input.boss.durationTicks
                DungeonMode.DEFEAT_BOSS -> bossHp <= 0
            }
            val failure = if (success) null else if (playerHp <= 0) "PLAYER_DIED" else "TIME_LIMIT"
            emit(if (success) DungeonEventType.VICTORY else DungeonEventType.DEFEAT)
            return DungeonCombatResult(success, failure, tick, playerHp.coerceAtLeast(0), bossHp.coerceAtLeast(0), events)
        }

        private fun playerAction() {
            for (skillId in input.skills.activeOrder) {
                when (skillId) {
                    "active_haste" -> if (input.skills.hasteBasisPoints > 0 && tick >= cooldownUntil[0]) {
                        cooldownUntil[0] = tick + 100
                        hasteUntil = tick + 50 + input.skills.buffDurationBonusTicks
                        emit(DungeonEventType.SKILL_CAST, skillId)
                        scheduleNextAction()
                        return
                    }
                    "active_basic_amp" -> if (input.skills.basicAmplificationBasisPoints > 0 && tick >= cooldownUntil[1]) {
                        cooldownUntil[1] = tick + 100
                        amplificationUntil = tick + 50 + input.skills.buffDurationBonusTicks
                        emit(DungeonEventType.SKILL_CAST, skillId)
                        scheduleNextAction()
                        return
                    }
                    "active_heavy" -> if (input.skills.heavyBasisPoints > 0 && tick >= cooldownUntil[2]) {
                        cooldownUntil[2] = tick + 100
                        hit(input.skills.heavyBasisPoints, DungeonEventType.PLAYER_HIT, skillId, true)
                        scheduleNextAction()
                        return
                    }
                    "active_dot" -> if (input.skills.dotTotalBasisPoints > 0 && tick >= cooldownUntil[3]) {
                        cooldownUntil[3] = tick + 100
                        repeat(5) { dotTicks += tick + (it + 1) * 10 }
                        emit(DungeonEventType.SKILL_CAST, skillId)
                        scheduleNextAction(1.0 / 3.0)
                        return
                    }
                }
            }
            val amplification = 10_000 + input.skills.permanentBasicAmplificationBasisPoints +
                if (tick < amplificationUntil) input.skills.basicAmplificationBasisPoints else 0
            hit(amplification, DungeonEventType.PLAYER_HIT, null, true)
            scheduleNextAction()
        }

        private fun applyDots() {
            val count = dotTicks.count { it == tick }
            if (count == 0) return
            dotTicks.removeAll { it == tick }
            repeat(count) { hit(input.skills.dotTotalBasisPoints / 5, DungeonEventType.DOT_HIT, "active_dot", false) }
        }

        private fun hit(basisPoints: Int, type: DungeonEventType, skillId: String?, canCritical: Boolean) {
            val allDamage = 10_000 + input.skills.allDamageBasisPoints
            var totalBasisPoints = roundHalfUp(basisPoints * allDamage / 10_000.0)
            val critical = canCritical && nextBasisPoint() < input.skills.criticalChanceBasisPoints
            if (critical) totalBasisPoints = roundHalfUp(totalBasisPoints * 1.5)
            val multiplier = 0.5 + 1.0 / (1.0 + exp(-0.015 * (input.player.penetration - input.boss.defense)))
            val damage = roundHalfUp(input.player.attack * (totalBasisPoints / 10_000.0) * multiplier).coerceAtLeast(1)
            bossHp -= damage
            emit(type, skillId, damage, critical)
        }

        private fun scheduleNextAction(recoveryMultiplier: Double = 1.0) {
            val haste = (input.skills.permanentHasteBasisPoints +
                if (tick < hasteUntil) input.skills.hasteBasisPoints else 0).coerceAtMost(10_000)
            val interval = (4.0 / (1.0 + haste / 10_000.0)).coerceAtLeast(2.0) * recoveryMultiplier
            nextPlayerAction += interval
        }

        private fun emit(type: DungeonEventType, skillId: String? = null, amount: Int = 0, critical: Boolean = false) {
            events += DungeonCombatEvent(events.size + 1, tick, type, skillId, amount, playerHp.coerceAtLeast(0), bossHp.coerceAtLeast(0), critical)
        }

        private fun nextBasisPoint(): Int {
            random += -7046029254386353131L
            var value = random
            value = (value xor (value ushr 30)) * -4658895280553007687L
            value = (value xor (value ushr 27)) * -7723592293110705685L
            return ((value xor (value ushr 31)).ushr(1) % 10_000).toInt()
        }
    }

    private fun roundHalfUp(value: Double): Int = floor(value + 0.5).toInt()
}
