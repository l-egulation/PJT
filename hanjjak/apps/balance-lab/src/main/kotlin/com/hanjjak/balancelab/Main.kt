package com.hanjjak.balancelab

import com.hanjjak.sim.*
import com.hanjjak.equipment.domain.EquipmentRules
import com.hanjjak.equipment.domain.EquipmentSlot
import java.io.File
import kotlin.math.roundToInt

internal data class StageRow(
    val id: String, val level: Int, val q: Int, val skill: String,
    val normalHp: Int, val normalAttack: Int, val bossType: String,
    val bossHp: Int, val bossAttack: Int, val defense: Int, val pattern: String,
)

fun main(args: Array<String>) {
    if (args.firstOrNull() == "progression-rebalance") {
        val accountCount = args.firstOrNull { it.startsWith("--accounts=") }
            ?.substringAfter("=")
            ?.toIntOrNull()
            ?: 10_000
        require(accountCount in 1..10_000) { "INVALID_ACCOUNT_COUNT" }
        runProgressionRebalance(write = "--write" in args, accountCount = accountCount)
        return
    }
    val source = File("packages/game-content/versions/v1/stages/stages.json").readText()
    val stages = parseStages(source)
    val output = File("build/reports/balance").apply { mkdirs() }.resolve("enemy-v1-working.csv")
    val economyOutput = File("build/reports").apply { mkdirs() }.resolve("pre-kafka-j11-validation.json")
    val rows = stages.map { stage ->
        val player = referenceStats(stage.level, stage.q)
        val skills = skillProfile(stage.skill)
        val input = CycleInput(
            "enemy-v1-working", 1, player,
            EnemyStats(stage.normalHp, stage.normalAttack, stage.defense),
            EnemyStats(stage.bossHp, stage.bossAttack, stage.defense),
            skills,
            bossTimeLimitTicks = when (stage.bossType) { "ATTACK_CHECK" -> if (stage.id == "stage.03-10") 120 else 100; else -> null },
            scheduledStrikes = scheduledStrikes(stage.pattern),
        )
        val baselineResults = (1L..1000L).map { seed -> CombatSimulator.simulate(input.copy(seed = seed)) }
        val deficientPlayer = deficientStats(stage) ?: player
        val deficientSkills = deficientSkill(stage) ?: skills
        val deficientResults = (1L..1000L).map { seed -> CombatSimulator.simulate(input.copy(seed = seed, player = deficientPlayer, skills = deficientSkills)) }
        val successRate = baselineResults.count { it.success } / 10.0
        val deficientSuccessRate = deficientResults.count { it.success } / 10.0
        val hpValues = baselineResults.filter { it.success }.map { it.remainingHp }.sorted()
        listOf(stage.id, stage.level, stage.q, stage.defense, "%.1f".format(successRate), "%.1f".format(deficientSuccessRate), deficientResults.firstOrNull { !it.success }?.failureCode.orEmpty(), percentile(hpValues, 0.10), percentile(hpValues, 0.50), percentile(hpValues, 0.90)).joinToString(",")
    }
    output.writeText("stage,level,q,enemyDefense,successRate,deficientSuccessRate,deficientFailure,p10Hp,p50Hp,p90Hp\n${rows.joinToString("\n")}\n")
    economyOutput.writeText(simulateP50Economy(stages).toJson())
    println("simulated=${stages.size} seeds=1000 report=${output.path} j11Report=${economyOutput.path}")
}

private fun runProgressionRebalance(write: Boolean, accountCount: Int) {
    val rows = generatedStageRows()
    if (write) {
        val stageFile = File("packages/game-content/versions/progression-rebalance-v1/stages/stages.json")
        stageFile.writeText(updateStageValues(stageFile.readText(), rows))
    }
    val report = simulateProgressionRebalance(1L..accountCount.toLong())
    val p50 = (1..10).map { report.chapter(it).p50Minutes }
    require(p50.zipWithNext().all { (earlier, later) -> later > earlier }) {
        "chapter arrival must increase monotonically\n${report.toCsv()}"
    }
    require((1..10).all { report.chapter(it).p90Minutes >= report.chapter(it).p50Minutes }) {
        "chapter P90 must not fall below P50\n${report.toCsv()}"
    }
    require(report.bosses.all { it.referenceSuccessRate >= 0.90 && it.deficientSuccessRate < it.referenceSuccessRate }) {
        "boss acceptance gate failed\n${report.toCsv()}"
    }
    require(
        report.promotion("NORMAL_TO_RARE").p50StageIndex <= 15 &&
            report.promotion("RARE_TO_EPIC").p50StageIndex <= 35,
    ) { "promotion gate failed\n${report.toCsv()}" }
    if (write) {
        File("docs/70-plans/mvp-release/verification").mkdirs()
        File("docs/70-plans/mvp-release/verification/progression-rebalance-v1.csv").writeText(report.toCsv())
        File("docs/70-plans/mvp-release/verification/progression-rebalance-v1-economy.json").writeText(report.toEconomyJson())
        println("progression-rebalance accounts=$accountCount wrote=${rows.size} stages csv=docs/70-plans/mvp-release/verification/progression-rebalance-v1.csv economy=docs/70-plans/mvp-release/verification/progression-rebalance-v1-economy.json")
    } else {
        println(report.toCsv())
    }
}

private fun updateStageValues(json: String, rows: List<StageRow>): String {
    val objectPattern = Regex("\\{\\s*\\\"id\\\":\\s*\\\"stage\\.[^\"]+\\\"[\\s\\S]*?\\n    \\}")
    val objects = objectPattern.findAll(json).toList()
    require(objects.size == rows.size) { "expected ${rows.size} stage objects, got ${objects.size}" }
    var index = 0
    return objectPattern.replace(json) { match ->
        val row = rows[index++]
        var value = match.value
        value = value.replace(Regex("\\\"enemyLevel\\\":\\s*\\d+"), "\\\"enemyLevel\\\": ${row.level}")
        value = value.replace(Regex("\\\"referenceQ\\\":\\s*\\d+"), "\\\"referenceQ\\\": ${row.q}")
        value = value.replace(Regex("\\\"referenceSkill\\\":\\s*\\\"[^\"]*\\\""), "\\\"referenceSkill\\\": \\\"${row.skill}\\\"")
        value = value.replace(Regex("\\\"normalHp\\\":\\s*\\d+"), "\\\"normalHp\\\": ${row.normalHp}")
        value = value.replace(Regex("\\\"normalAttack\\\":\\s*\\d+"), "\\\"normalAttack\\\": ${row.normalAttack}")
        value = value.replace(Regex("\\\"bossType\\\":\\s*\\\"[^\"]+\\\""), "\\\"bossType\\\": \\\"${row.bossType}\\\"")
        value = value.replace(Regex("\\\"bossHp\\\":\\s*\\d+"), "\\\"bossHp\\\": ${row.bossHp}")
        value = value.replace(Regex("\\\"bossAttack\\\":\\s*\\d+"), "\\\"bossAttack\\\": ${row.bossAttack}")
        value = value.replace(Regex("\\\"enemyDefense\\\":\\s*\\d+"), "\\\"enemyDefense\\\": ${row.defense}")
        value.replace(Regex("\\\"pattern\\\":\\s*\\\"[^\"]*\\\""), "\\\"pattern\\\": \\\"${row.pattern}\\\"")
    }
}

private fun referenceStats(level: Int, q: Int): FighterStats = referenceStats(level, q, q, q)

private fun referenceStats(level: Int, attackQ: Int, hpQ: Int, penetrationQ: Int): FighterStats {
    val attack = 40 + 2 * (level - 1) +
        EquipmentRules.referenceStatSummary(EquipmentSlot.WEAPON, attackQ).attack +
        EquipmentRules.referenceStatSummary(EquipmentSlot.GLOVES, attackQ).attack
    val hp = 400 + 20 * (level - 1) +
        EquipmentRules.referenceStatSummary(EquipmentSlot.ARMOR, hpQ).maxHp +
        EquipmentRules.referenceStatSummary(EquipmentSlot.HELMET, hpQ).maxHp
    val penetration = 20 +
        EquipmentRules.referenceStatSummary(EquipmentSlot.CAPE, penetrationQ).penetration +
        EquipmentRules.referenceStatSummary(EquipmentSlot.SHOES, penetrationQ).penetration
    return FighterStats(attack, hp, penetration)
}

private fun deficientStats(stage: StageRow): FighterStats? = when (stage.bossType) {
    "ATTACK_CHECK" -> referenceStats(stage.level, 0, stage.q, stage.q)
    "HP_CHECK" -> referenceStats(stage.level, stage.q, 0, stage.q)
    "PENETRATION_CHECK" -> referenceStats(stage.level, stage.q, stage.q, 0)
    else -> null
}

private fun deficientSkill(stage: StageRow): SkillProfile? = when (stage.bossType) {
    "ATTACK_CHECK" -> SkillProfile()
    else -> null
}

private fun skillProfile(token: String): SkillProfile {
    if (token == "없음") return SkillProfile()
    val grade = token.firstOrNull { it in "NRE" } ?: 'N'
    val rank = Regex("[0-9]+").findAll(token).lastOrNull()?.value?.toInt() ?: 1
    val gradeOffset = when (grade) { 'R' -> 1200; 'E' -> 2300; else -> 0 }
    val passive = 500 + gradeOffset + (rank - 1) * 100
    return SkillProfile(
        heavyBasisPoints = 20_000 + gradeOffset * 10 + (rank - 1) * 1_000,
        dotTotalBasisPoints = 25_000 + gradeOffset * 14 + (rank - 1) * 1_500,
        hasteBasisPoints = 2_000 + gradeOffset + (rank - 1) * 100,
        basicAmplificationBasisPoints = 2_000 + gradeOffset + (rank - 1) * 100,
        criticalChanceBasisPoints = passive,
        allDamageBasisPoints = passive,
    )
}

private fun scheduledStrikes(pattern: String): Map<Int, Int> {
    val damage = Regex("([0-9,]+)$").find(pattern)?.groupValues?.get(1)?.replace(",", "")?.toIntOrNull() ?: return emptyMap()
    return Regex("[0-9]+").findAll(pattern.substringBefore("초 강타")).associate { it.value.toInt() * 10 to damage }
}

private fun percentile(values: List<Int>, fraction: Double): Int = if (values.isEmpty()) 0 else values[((values.size - 1) * fraction).roundToInt()]

private fun parseStages(json: String): List<StageRow> {
    val objectPattern = Regex("""\{\s*"id":\s*"(stage\.[^"]+)"[\s\S]*?"enemyLevel":\s*(\d+),[\s\S]*?"referenceQ":\s*(\d+),[\s\S]*?"referenceSkill":\s*"([^"]*)",[\s\S]*?"normalHp":\s*(\d+),[\s\S]*?"normalAttack":\s*(\d+),[\s\S]*?"bossType":\s*"([^"]+)",[\s\S]*?"bossHp":\s*(\d+),[\s\S]*?"bossAttack":\s*(\d+),[\s\S]*?"enemyDefense":\s*(\d+),[\s\S]*?"pattern":\s*"([^"]*)"\s*\}""")
    return objectPattern.findAll(json).map { match ->
        val value = match.groupValues
        StageRow(value[1], value[2].toInt(), value[3].toInt(), value[4], value[5].toInt(), value[6].toInt(), value[7], value[8].toInt(), value[9].toInt(), value[10].toInt(), value[11])
    }.toList().also { require(it.size == 40) { "expected 40 stages, got ${it.size}" } }
}
