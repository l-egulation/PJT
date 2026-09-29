package com.hanjjak.balancelab

import com.hanjjak.equipment.domain.EquipmentRules
import com.hanjjak.equipment.domain.EquipmentSlot
import com.hanjjak.sim.CombatSimulator
import com.hanjjak.sim.CycleInput
import com.hanjjak.sim.EnemyStats
import com.hanjjak.sim.FighterStats
import com.hanjjak.sim.SkillProfile
import java.io.File
import kotlin.math.exp
import kotlin.math.floor
import kotlin.math.roundToInt

private const val ENCOUNTER_TICKS = 150
private const val STRIKE_COUNT = 7
private const val VERIFICATION_SEEDS = 1_000

private data class DungeonBuild(
    val stage: Int,
    val level: Int,
    val q: Int,
    val skillToken: String,
    val gemLevels: List<Int>,
)

private data class GemBonus(
    val flatAttack: Int = 0,
    val flatHp: Int = 0,
    val attackPercent: Int = 0,
    val flatPenetration: Int = 0,
    val criticalChance: Int = 0,
    val haste: Int = 0,
)

private enum class BossType { SURVIVAL, BERSERK, ARMORED }

fun main() {
    val builds = listOf(
        DungeonBuild(1, 41, 70, "N10", emptyList()),
        DungeonBuild(2, 47, 88, "N10", listOf(1, 1, 1, 2, 2, 2)),
        DungeonBuild(3, 53, 98, "R2", List(6) { 2 }),
        DungeonBuild(4, 59, 109, "R7", listOf(2, 2, 2, 3, 3, 3)),
        DungeonBuild(5, 67, 128, "R9", List(6) { 3 }),
        DungeonBuild(6, 73, 138, "E1", listOf(3, 3, 3, 4, 4, 4)),
        DungeonBuild(7, 79, 149, "E4", List(6) { 4 }),
        DungeonBuild(8, 100, 170, "E4", List(6) { 5 }),
        DungeonBuild(9, 130, 200, "E4", listOf(6, 5, 5, 5, 5, 5)),
        DungeonBuild(10, 170, 240, "E4", listOf(7, 6, 6, 6, 6, 6)),
    )
    val preUnlockBuild = DungeonBuild(0, 39, 69, "N10", emptyList())
    val rows = mutableListOf<String>()

    for (type in BossType.entries) {
        for (build in builds) {
            val recommended = snapshot(build, type)
            val deficientBuild = if (build.stage == 1) preUnlockBuild else builds[build.stage - 2]
            val deficient = snapshot(deficientBuild, type)
            when (type) {
                BossType.SURVIVAL -> {
                    val strikeDamage = floor((recommended.first.maxHp - 1) / STRIKE_COUNT.toDouble()).toInt().coerceAtLeast(1)
                    val recommendedRemaining = recommended.first.maxHp - strikeDamage * STRIKE_COUNT
                    val deficientSuccess = deficient.first.maxHp > strikeDamage * STRIKE_COUNT
                    rows += csv(type, build, bossHp = 1, bossAttack = 0, defense = 0, strikeDamage, recommendedRemaining, 100.0, if (deficientSuccess) 100.0 else 0.0, ENCOUNTER_TICKS)
                }
                BossType.BERSERK, BossType.ARMORED -> {
                    val defense = if (type == BossType.BERSERK) 0 else defenseForMultiplier(recommended.first.penetration, 0.75)
                    val noCritical = recommended.second.copy(criticalChanceBasisPoints = 0)
                    val bossHp = maximumGuaranteedBossHp(recommended.first, noCritical, defense)
                    val recommendedResults = results(recommended.first, recommended.second, defense, bossHp)
                    val deficientResults = results(deficient.first, deficient.second, defense, bossHp)
                    check(recommendedResults.all { it.first }) { "$type stage ${build.stage} is not guaranteed" }
                    rows += csv(
                        type, build, bossHp, 0, defense, 0, recommended.first.maxHp,
                        recommendedResults.count { it.first } * 100.0 / VERIFICATION_SEEDS,
                        deficientResults.count { it.first } * 100.0 / VERIFICATION_SEEDS,
                        recommendedResults.maxOf { it.second },
                    )
                }
            }
        }
    }

    val output = File("build/reports/balance/gem-dungeon-candidate.csv")
    output.parentFile.mkdirs()
    output.writeText(
        "bossType,stage,level,q,skillToken,bossHp,bossAttack,defense,strikeDamage,recommendedRemainingHp,recommendedSuccessRate,previousBuildSuccessRate,worstClearTicks\n" +
            rows.joinToString("\n", postfix = "\n"),
    )
    println("simulated=${BossType.entries.size * builds.size} seeds=$VERIFICATION_SEEDS report=${output.path}")
}

private fun snapshot(build: DungeonBuild, type: BossType): Pair<FighterStats, SkillProfile> {
    val gems = gemBonus(build.gemLevels, type)
    val baseAttack = 40 + 2 * (build.level - 1) +
        EquipmentRules.referenceStatSummary(EquipmentSlot.WEAPON, build.q).attack +
        EquipmentRules.referenceStatSummary(EquipmentSlot.GLOVES, build.q).attack +
        gems.flatAttack
    val baseHp = 400 + 20 * (build.level - 1) +
        EquipmentRules.referenceStatSummary(EquipmentSlot.ARMOR, build.q).maxHp +
        EquipmentRules.referenceStatSummary(EquipmentSlot.HELMET, build.q).maxHp
    val penetration = 20 +
        EquipmentRules.referenceStatSummary(EquipmentSlot.CAPE, build.q).penetration +
        EquipmentRules.referenceStatSummary(EquipmentSlot.SHOES, build.q).penetration
    val fighter = FighterStats(
        attack = (baseAttack * (1 + gems.attackPercent / 10_000.0)).roundToInt(),
        maxHp = baseHp + gems.flatHp,
        penetration = penetration + gems.flatPenetration,
    )
    val grade = build.skillToken.first()
    val level = build.skillToken.drop(1).toInt()
    fun scaled(normal: Int, rare: Int, epic: Int, legendary: Int, normalStep: Int, rareStep: Int = normalStep, epicStep: Int = normalStep, legendaryStep: Int = normalStep): Int =
        when (grade) {
            'N' -> normal + normalStep * (level - 1)
            'R' -> rare + rareStep * (level - 1)
            'E' -> epic + epicStep * (level - 1)
            'L' -> legendary + legendaryStep * (level - 1)
            else -> error("unknown skill grade $grade")
        }
    val skills = SkillProfile(
        heavyBasisPoints = scaled(20_000, 32_000, 48_000, 70_000, 1_000, 1_500, 2_000, 2_500),
        dotTotalBasisPoints = scaled(25_000, 42_000, 64_000, 92_000, 1_500, 2_000, 2_500, 3_000),
        hasteBasisPoints = scaled(2_000, 3_200, 4_500, 6_000, 100) + gems.haste,
        basicAmplificationBasisPoints = scaled(2_000, 3_200, 4_500, 6_000, 100),
        criticalChanceBasisPoints = scaled(500, 1_600, 2_800, 4_000, 100) + gems.criticalChance,
        allDamageBasisPoints = scaled(500, 1_600, 2_800, 4_000, 100),
    )
    return fighter to skills
}

private fun gemBonus(levels: List<Int>, type: BossType): GemBonus {
    if (levels.isEmpty()) return GemBonus()
    val fixedLevels = levels.filter { it <= 5 }
    val flat = fixedLevels.sumOf(::flatValue)
    return when (type) {
        BossType.SURVIVAL -> GemBonus(flatHp = flat + levels.filter { it == 6 }.sumOf { 1_500 })
        BossType.BERSERK -> {
            val level6Count = levels.count { it == 6 }
            GemBonus(
                flatAttack = flat + (level6Count - if (level6Count > 0) 1 else 0).coerceAtLeast(0) * 150,
                attackPercent = if (level6Count > 0) 800 else 0,
                haste = if (7 in levels) 1_200 else 0,
            )
        }
        BossType.ARMORED -> {
            val level6Count = levels.count { it == 6 }
            GemBonus(
                flatAttack = flat + (level6Count - if (level6Count > 0) 1 else 0).coerceAtLeast(0) * 150,
                flatPenetration = when {
                    7 in levels -> 120
                    level6Count > 0 -> 60
                    else -> 0
                },
            )
        }
    }
}

private fun flatValue(level: Int): Int = listOf(0, 3, 7, 15, 32, 70)[level]

private fun maximumGuaranteedBossHp(player: FighterStats, skills: SkillProfile, defense: Int): Int {
    var low = 1
    var high = player.attack * 1_000
    while (low < high) {
        val mid = low + (high - low + 1) / 2
        val success = simulate(player, skills, defense, mid, 1L).success
        if (success) low = mid else high = mid - 1
    }
    return low
}

private fun results(player: FighterStats, skills: SkillProfile, defense: Int, hp: Int): List<Pair<Boolean, Int>> =
    (1L..VERIFICATION_SEEDS.toLong()).map { seed -> simulate(player, skills, defense, hp, seed).let { it.success to it.elapsedTicks } }

private fun simulate(player: FighterStats, skills: SkillProfile, defense: Int, hp: Int, seed: Long) = CombatSimulator.simulate(
    CycleInput(
        contentVersion = "gem-dungeon-candidate",
        seed = seed,
        player = player,
        normal = EnemyStats(1, 0, 0),
        boss = EnemyStats(hp, 0, defense),
        skills = skills,
        normalCount = 0,
        bossTimeLimitTicks = ENCOUNTER_TICKS,
    ),
)

private fun defenseForMultiplier(penetration: Int, target: Double): Int {
    val logistic = target - 0.5
    val delta = -kotlin.math.ln(1.0 / logistic - 1.0) / 0.015
    return (penetration - delta).roundToInt()
}

private fun csv(
    type: BossType,
    build: DungeonBuild,
    bossHp: Int,
    bossAttack: Int,
    defense: Int,
    strikeDamage: Int,
    remainingHp: Int,
    successRate: Double,
    deficientRate: Double,
    worstTicks: Int,
): String = listOf(
    type, build.stage, build.level, build.q, build.skillToken, bossHp, bossAttack, defense, strikeDamage,
    remainingHp, "%.1f".format(successRate), "%.1f".format(deficientRate), worstTicks,
).joinToString(",")
