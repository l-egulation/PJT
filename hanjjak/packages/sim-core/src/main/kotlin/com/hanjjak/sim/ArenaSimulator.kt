package com.hanjjak.sim

import kotlin.math.floor

data class ArenaFighter(
    val id: String,
    val stats: FighterStats,
    val skills: SkillProfile = SkillProfile(),
    val defense: Int = 0,
)

data class ArenaInput(
    val contentVersion: String,
    val seed: Long,
    val attacker: ArenaFighter,
    val defender: ArenaFighter,
    val hpMultiplier: Int = 30,
    val maxDurationTicks: Int = 3_000,
)

data class ArenaResult(
    val winnerId: String?,
    val loserId: String?,
    val draw: Boolean,
    val elapsedTicks: Int,
    val attackerRemainingHp: Int,
    val defenderRemainingHp: Int,
)

data class ArenaEvent(
    val eventId: String,
    val logicalTick: Int,
    val type: String,
    val actorId: String? = null,
    val targetId: String? = null,
    val skillId: String? = null,
    val damage: Int? = null,
    val critical: Boolean = false,
    val hpBefore: Int? = null,
    val hpAfter: Int? = null,
)

data class ArenaTrace(val result: ArenaResult, val events: List<ArenaEvent>)

object ArenaSimulator {
    fun simulate(input: ArenaInput): ArenaResult = simulateWithEvents(input).result

    fun simulateWithEvents(input: ArenaInput): ArenaTrace {
        require(input.hpMultiplier > 0) { "INVALID_ARENA_HP_MULTIPLIER" }
        require(input.maxDurationTicks > 0) { "INVALID_ARENA_DURATION" }
        val attacker = Runtime(input.attacker, input.attacker.stats.maxHp * input.hpMultiplier, input.seed)
        val defender = Runtime(input.defender, input.defender.stats.maxHp * input.hpMultiplier, input.seed xor ARENA_SEED_MIX)
        val events = mutableListOf<ArenaEvent>()
        var sequence = 0
        fun emit(tick: Int, type: String, actor: String? = null, target: String? = null, skill: String? = null, damage: Int? = null, critical: Boolean = false, before: Int? = null, after: Int? = null) {
            sequence += 1
            events += ArenaEvent("arena-event-%05d".format(sequence), tick, type, actor, target, skill, damage, critical, before, after)
        }
        emit(0, "ARENA_STARTED")
        var tick = 0
        while (tick < input.maxDurationTicks && attacker.alive && defender.alive) {
            attacker.applyDots(tick, defender, emit = { type, skill, damage, critical, before, after -> emit(tick, type, attacker.fighter.id, defender.fighter.id, skill, damage, critical, before, after) })
            defender.applyDots(tick, attacker, emit = { type, skill, damage, critical, before, after -> emit(tick, type, defender.fighter.id, attacker.fighter.id, skill, damage, critical, before, after) })
            if (attacker.alive && defender.alive && tick.toDouble() >= attacker.nextAction) {
                attacker.act(tick, defender, emit = { type, skill, damage, critical, before, after -> emit(tick, type, attacker.fighter.id, defender.fighter.id, skill, damage, critical, before, after) })
            }
            if (attacker.alive && defender.alive && tick.toDouble() >= defender.nextAction) {
                defender.act(tick, attacker, emit = { type, skill, damage, critical, before, after -> emit(tick, type, defender.fighter.id, attacker.fighter.id, skill, damage, critical, before, after) })
            }
            if (!attacker.alive) emit(tick, "ARENA_FIGHTER_DEFEATED", attacker.fighter.id, defender.fighter.id, after = 0)
            if (!defender.alive) emit(tick, "ARENA_FIGHTER_DEFEATED", defender.fighter.id, attacker.fighter.id, after = 0)
            tick += 1
        }
        val attackerWins = attacker.alive && !defender.alive
        val defenderWins = defender.alive && !attacker.alive
        val draw = !attackerWins && !defenderWins
        val result = ArenaResult(
            winnerId = when { attackerWins -> attacker.fighter.id; defenderWins -> defender.fighter.id; else -> null },
            loserId = when { attackerWins -> defender.fighter.id; defenderWins -> attacker.fighter.id; else -> null },
            draw = draw,
            elapsedTicks = tick,
            attackerRemainingHp = attacker.hp.coerceAtLeast(0),
            defenderRemainingHp = defender.hp.coerceAtLeast(0),
        )
        emit(tick, "ARENA_COMPLETED", result.winnerId, result.loserId)
        return ArenaTrace(result, events)
    }

    private class Runtime(val fighter: ArenaFighter, initialHp: Int, seed: Long) {
        val defense: Int get() = fighter.defense
        var hp = initialHp
        var nextAction = 0.0
        private var random = seed
        private var hasteUntil = 0
        private var amplificationUntil = 0
        private val cooldownUntil = IntArray(4)
        private val dotTicks = mutableListOf<Int>()
        val alive: Boolean get() = hp > 0

        fun act(tick: Int, target: Runtime, emit: (String, String?, Int?, Boolean, Int?, Int?) -> Unit) {
            val skills = fighter.skills
            var selected: String? = null
            for (skillId in skills.activeOrder) {
                val index = when (skillId) {
                    "active_haste" -> 0
                    "active_basic_amp" -> 1
                    "active_heavy" -> 2
                    "active_dot" -> 3
                    else -> -1
                }
                if (index < 0 || tick < cooldownUntil[index]) continue
                when (skillId) {
                    "active_haste" -> if (skills.hasteBasisPoints > 0) {
                        cooldownUntil[index] = tick + 100
                        hasteUntil = tick + 50 + skills.buffDurationBonusTicks
                        selected = skillId
                        emit("FIGHTER_SKILL_CAST", skillId, null, false, null, null)
                        emit("FIGHTER_BUFF_STARTED", skillId, null, false, null, null)
                        break
                    }
                    "active_basic_amp" -> if (skills.basicAmplificationBasisPoints > 0) {
                        cooldownUntil[index] = tick + 100
                        amplificationUntil = tick + 50 + skills.buffDurationBonusTicks
                        selected = skillId
                        emit("FIGHTER_SKILL_CAST", skillId, null, false, null, null)
                        emit("FIGHTER_BUFF_STARTED", skillId, null, false, null, null)
                        break
                    }
                    "active_heavy" -> if (skills.heavyBasisPoints > 0) {
                        cooldownUntil[index] = tick + 100
                        selected = skillId
                        emit("FIGHTER_SKILL_CAST", skillId, null, false, null, null)
                        hit(target, skills.heavyBasisPoints, true, emit, skillId)
                        break
                    }
                    "active_dot" -> if (skills.dotTotalBasisPoints > 0) {
                        cooldownUntil[index] = tick + 100
                        selected = skillId
                        dotTicks.addAll((1..5).map { tick + it * 10 })
                        emit("FIGHTER_SKILL_CAST", skillId, null, false, null, null)
                        break
                    }
                }
            }
            if (selected == null) {
                val basis = 10_000 + fighter.skills.permanentBasicAmplificationBasisPoints + if (tick < amplificationUntil) fighter.skills.basicAmplificationBasisPoints else 0
                hit(target, basis, true, emit, null)
            }
            val haste = (fighter.skills.permanentHasteBasisPoints + if (tick < hasteUntil) fighter.skills.hasteBasisPoints else 0).coerceAtMost(10_000)
            nextAction += (4.0 / (1.0 + haste / 10_000.0)).coerceAtLeast(2.0)
        }

        fun applyDots(tick: Int, target: Runtime, emit: (String, String?, Int?, Boolean, Int?, Int?) -> Unit) {
            if (!alive || !target.alive) return
            val count = dotTicks.count { it == tick }
            if (count == 0) return
            dotTicks.removeAll { it == tick }
            repeat(count) { hit(target, fighter.skills.dotTotalBasisPoints / 5, false, emit, "active_dot") }
        }

        private fun hit(target: Runtime, basisPoints: Int, canCritical: Boolean, emit: (String, String?, Int?, Boolean, Int?, Int?) -> Unit, skillId: String?) {
            if (!alive || !target.alive) return
            val allDamage = 10_000 + fighter.skills.allDamageBasisPoints
            var totalBasisPoints = roundHalfUp(basisPoints * allDamage / 10_000.0)
            val critical = canCritical && nextBasisPoint() < fighter.skills.criticalChanceBasisPoints
            if (critical) totalBasisPoints = roundHalfUp(totalBasisPoints * 1.5)
            val amount = damage(fighter.stats.attack, fighter.stats.penetration, target.defense, totalBasisPoints)
            val before = target.hp
            target.hp = (target.hp - amount).coerceAtLeast(0)
            emit(if (skillId == null) "FIGHTER_BASIC_ATTACK" else "FIGHTER_HIT", skillId, amount, critical, before, target.hp)
        }

        private fun nextBasisPoint(): Int {
            random += -7046029254386353131L
            var value = random
            value = (value xor (value ushr 30)) * -4658895280553007687L
            value = (value xor (value ushr 27)) * -7723592293110705685L
            return ((value xor (value ushr 31)).ushr(1) % 10_000).toInt()
        }
    }

    private const val ARENA_SEED_MIX = -3310645123371662151L
    private fun damage(attack: Int, penetration: Int, defense: Int, skillBasisPoints: Int): Int {
        val multiplier = 0.5 + 1.0 / (1.0 + kotlin.math.exp(-0.015 * (penetration - defense)))
        return roundHalfUp(attack * (skillBasisPoints / 10_000.0) * multiplier).coerceAtLeast(1)
    }
    private fun roundHalfUp(value: Double): Int = floor(value + 0.5).toInt()
}
