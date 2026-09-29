package com.hanjjak.balancelab

import com.fasterxml.jackson.databind.JsonNode
import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.equipment.domain.EquipmentRules
import com.hanjjak.equipment.domain.EquipmentSlot
import com.hanjjak.sim.FighterStats
import com.hanjjak.sim.RaidCombatEventType
import com.hanjjak.sim.RaidCombatInput
import com.hanjjak.sim.RaidCombatResult
import com.hanjjak.sim.RaidSimulator
import com.hanjjak.sim.SkillProfile
import java.io.File
import kotlin.math.abs
import kotlin.math.floor

internal data class RaidContentCandidate(
    val contentVersion: String,
    val bossInitialAttack: Int,
    val bossInitialDefense: Int,
    val gradeDamageThresholds: LinkedHashMap<String, Long>,
)

internal data class RaidRewardProjection(
    val cosmeticTickets: Long,
    val gemBoxes: Long,
    val rice: Long,
)

internal data class RaidBuildMetrics(
    val sampleCount: Int,
    val medianSurvivalTicks: Int,
    val p10SurvivalTicks: Int,
    val p50SurvivalTicks: Int,
    val p90SurvivalTicks: Int,
    val earlyDamage: Long,
    val lateDamage: Long,
    val p10Damage: Long,
    val p50Damage: Long,
    val p90Damage: Long,
    val bSuccessCount: Int,
    val bSuccessRatePercent: Double,
    val gradeDistribution: LinkedHashMap<String, Int>,
    val fiveMinuteCapCount: Int,
)

internal data class RaidPopulationProjection(
    val activeParticipants: Int,
    val gradeMix: LinkedHashMap<String, Int>,
    val sealContribution: Long,
    val sealSuccessProbabilityPercent: Double,
    val accountDay: RaidRewardProjection,
    val serverDay: RaidRewardProjection,
    val rankDistribution: LinkedHashMap<String, Int>,
    val autoClaimBacklog: Int,
    val threeParticipationConfirmations: RaidRewardProjection,
)

internal data class RaidBalanceReport(
    val content: RaidContentCandidate,
    val reference: RaidBuildMetrics,
    val attackDeficient: RaidBuildMetrics,
    val hpDeficient: RaidBuildMetrics,
    val penetrationDeficient: RaidBuildMetrics,
    val populationProjections: List<RaidPopulationProjection>,
)

internal data class RaidReferenceBuild(
    val contentVersion: String,
    val sourceStageId: String,
    val referenceBuildId: String,
    val equipmentSlotIds: Map<String, String>,
    val mainGemIds: List<String>,
    val cosmeticSetIds: List<String>,
    val level: Int,
    val equipmentQ: Int,
    val seedStart: Long,
    val seedEnd: Long,
    val targetMedianSurvivalTicks: Int,
    val survivalMin: Int,
    val survivalMax: Int,
    val skills: SkillProfile,
)

internal const val RAID_INITIAL_ATTACK_MIN = 1
internal const val RAID_INITIAL_ATTACK_MAX = 20
internal const val RAID_INITIAL_DEFENSE_MIN = 1
internal const val RAID_INITIAL_DEFENSE_MAX = 10

internal fun deriveRaidBalance(root: File = repositoryRoot()): RaidBalanceReport {
    val build = loadReferenceBuild(root.resolve("apps/balance-lab/src/main/resources/raid-reference-build-v1.json"), root)
    val referencePlayer = player(build.level, build.equipmentQ, build.equipmentQ, build.equipmentQ)
    val attackDeficientPlayer = player(build.level, 0, build.equipmentQ, build.equipmentQ)
    val hpDeficientPlayer = player(build.level, build.equipmentQ, 0, build.equipmentQ)
    val penetrationDeficientPlayer = player(build.level, build.equipmentQ, build.equipmentQ, 0)
    val seeds = build.seedStart..build.seedEnd
    val candidates = mutableListOf<RaidBalanceReport>()
    for (bossAttack in RAID_INITIAL_ATTACK_MIN..RAID_INITIAL_ATTACK_MAX) for (bossDefense in RAID_INITIAL_DEFENSE_MIN..RAID_INITIAL_DEFENSE_MAX) {
        val referenceResults = seeds.map { simulate(build, referencePlayer, bossAttack, bossDefense, it) }
        val bThreshold = percentile(referenceResults.map { it.totalDamage }.sorted(), 0.10)
        val thresholds = gradeThresholds(bThreshold)
        val reference = metrics(referenceResults, thresholds)
        if (reference.bSuccessCount < 900 || reference.medianSurvivalTicks !in build.survivalMin..build.survivalMax) continue
        val attackDeficient = metrics(seeds.map { simulate(build, attackDeficientPlayer, bossAttack, bossDefense, it) }, thresholds)
        val hpDeficient = metrics(seeds.map { simulate(build, hpDeficientPlayer, bossAttack, bossDefense, it) }, thresholds)
        val penetrationDeficient = metrics(seeds.map { simulate(build, penetrationDeficientPlayer, bossAttack, bossDefense, it) }, thresholds)
        val content = RaidContentCandidate(build.contentVersion, bossAttack, bossDefense, thresholds)
        val report = RaidBalanceReport(content, reference, attackDeficient, hpDeficient, penetrationDeficient, emptyList())
        if (attackDeficient.earlyDamage < reference.earlyDamage && hpDeficient.medianSurvivalTicks < reference.medianSurvivalTicks && penetrationDeficient.lateDamage * 100 <= reference.lateDamage * 95) candidates += report
    }
    require(candidates.isNotEmpty()) { "RAID_NO_VALID_CANDIDATE" }
    val selected = candidates.minWith(compareBy<RaidBalanceReport> { abs(it.reference.medianSurvivalTicks - build.targetMedianSurvivalTicks) }.thenBy { it.content.bossInitialAttack }.thenBy { it.content.bossInitialDefense }.thenBy { it.content.gradeDamageThresholds.getValue("B") })
    return selected.copy(populationProjections = populationProjections(selected.reference))
}

private fun gradeThresholds(bThreshold: Long): LinkedHashMap<String, Long> {
    val thresholds = linkedMapOf("D" to roundHalfUp(bThreshold * 0.35), "C" to roundHalfUp(bThreshold * 0.65), "B" to bThreshold, "A" to roundHalfUp(bThreshold * 1.50), "S" to roundHalfUp(bThreshold * 2.20), "SS" to roundHalfUp(bThreshold * 3.20), "SSS" to roundHalfUp(bThreshold * 4.50))
    require(thresholds.values.all { it > 0 } && thresholds.values.zipWithNext().all { (left, right) -> right > left }) { "RAID_THRESHOLDS_NOT_STRICT" }
    return thresholds
}

private fun populationProjections(reference: RaidBuildMetrics): List<RaidPopulationProjection> {
    val gradeMix = LinkedHashMap(reference.gradeDistribution)
    val contributionByGrade = linkedMapOf("PARTICIPATION" to 0L, "D" to 100L, "C" to 167L, "B" to 233L, "A" to 300L, "S" to 367L, "SS" to 433L, "SSS" to 500L)
    val rewardByGrade = linkedMapOf("PARTICIPATION" to RaidRewardProjection(5, 3, 5_000), "D" to RaidRewardProjection(6, 3, 5_750), "C" to RaidRewardProjection(7, 4, 6_500), "B" to RaidRewardProjection(7, 4, 7_250), "A" to RaidRewardProjection(8, 5, 8_000), "S" to RaidRewardProjection(9, 5, 8_750), "SS" to RaidRewardProjection(10, 6, 9_500), "SSS" to RaidRewardProjection(10, 6, 10_000))
    val weightedContribution = gradeMix.entries.sumOf { (grade, count) -> count.toLong() * contributionByGrade.getValue(grade) } / reference.sampleCount.toDouble()
    val weightedReward = gradeMix.entries.fold(RaidRewardProjection(0, 0, 0)) { total, (grade, count) ->
        val reward = rewardByGrade.getValue(grade)
        RaidRewardProjection(total.cosmeticTickets + reward.cosmeticTickets * count, total.gemBoxes + reward.gemBoxes * count, total.rice + reward.rice * count)
    }
    val perAccount = RaidRewardProjection(weightedReward.cosmeticTickets * 3 / reference.sampleCount, weightedReward.gemBoxes * 3 / reference.sampleCount, weightedReward.rice * 3 / reference.sampleCount)
    return listOf(36, 72, 144, 1_000).map { active ->
        val sealContribution = roundHalfUp(weightedContribution * active)
        val success = sealContribution >= 50_000
        val first = minOf(1, active)
        val secondToTenth = minOf((active - first).coerceAtLeast(0), 9)
        val eleventhToHundredth = minOf((active - first - secondToTenth).coerceAtLeast(0), 90)
        val rest = (active - first - secondToTenth - eleventhToHundredth).coerceAtLeast(0)
        val rankDistribution = linkedMapOf("FIRST" to first, "SECOND_TO_TENTH" to secondToTenth, "ELEVENTH_TO_HUNDREDTH" to eleventhToHundredth, "REST" to rest)
        val rankRewards = if (success) linkedMapOf("FIRST" to RaidRewardProjection(11, 7, 11_000), "SECOND_TO_TENTH" to RaidRewardProjection(10, 6, 10_000), "ELEVENTH_TO_HUNDREDTH" to RaidRewardProjection(9, 5, 9_000), "REST" to RaidRewardProjection(8, 5, 8_000)) else linkedMapOf("FIRST" to RaidRewardProjection(8, 5, 7_700), "SECOND_TO_TENTH" to RaidRewardProjection(7, 4, 7_000), "ELEVENTH_TO_HUNDREDTH" to RaidRewardProjection(6, 4, 6_300), "REST" to RaidRewardProjection(6, 4, 5_600))
        val personalSupply = RaidRewardProjection(perAccount.cosmeticTickets * active, perAccount.gemBoxes * active, perAccount.rice * active)
        val rankSupply = rankDistribution.entries.fold(RaidRewardProjection(0, 0, 0)) { total, (band, count) ->
            val reward = rankRewards.getValue(band)
            RaidRewardProjection(total.cosmeticTickets + reward.cosmeticTickets * count, total.gemBoxes + reward.gemBoxes * count, total.rice + reward.rice * count)
        }
        RaidPopulationProjection(active, gradeMix, sealContribution, if (success) 100.0 else 0.0, perAccount, RaidRewardProjection(personalSupply.cosmeticTickets + rankSupply.cosmeticTickets, personalSupply.gemBoxes + rankSupply.gemBoxes, personalSupply.rice + rankSupply.rice), rankDistribution, (active - 200).coerceAtLeast(0), RaidRewardProjection(15, 9, 15_000))
    }
}

fun main() {
    val root = repositoryRoot()
    val report = deriveRaidBalance(root)
    val reportFile = root.resolve("build/reports/balance/raid-mvp-v1-working.json")
    reportFile.parentFile.mkdirs()
    reportFile.writeText(reportJson(report))
    val contentFile = root.resolve("packages/game-content/versions/v1/raids/raids.json")
    contentFile.parentFile.mkdirs()
    contentFile.writeText(contentJson(report.content))
    println("raid seeds=1000 bossAttack=${report.content.bossInitialAttack} bossDefense=${report.content.bossInitialDefense} B=${report.content.gradeDamageThresholds.getValue("B")} report=${reportFile.path}")
}

internal fun loadReferenceBuild(file: File, root: File): RaidReferenceBuild {
    val mapper = ObjectMapper()
    val node = mapper.readTree(file)
    fun required(name: String): JsonNode = node.get(name) ?: error("missing $name")
    fun string(name: String): String = required(name).takeIf { it.isTextual }?.textValue() ?: error("invalid $name")
    fun int(name: String): Int = required(name).takeIf { it.isInt }?.intValue() ?: error("invalid $name")
    fun long(name: String): Long = required(name).takeIf { it.isIntegralNumber }?.longValue() ?: error("invalid $name")
    fun strings(value: JsonNode, name: String): List<String> {
        require(value.isArray && value.all { it.isTextual }) { "invalid $name" }
        return value.map(JsonNode::textValue)
    }
    val contentVersion = string("contentVersion")
    require(contentVersion == "raid-mvp-v1-working") { "reference content version must be raid-mvp-v1-working" }
    val seedStart = long("seedStart")
    val seedEnd = long("seedEnd")
    require(seedStart == 1L && seedEnd == 1_000L) { "reference seed range must be 1 through 1000" }
    val targetMedian = int("targetMedianSurvivalTicks")
    require(targetMedian == 600) { "reference target median survival must be 600 ticks" }
    val stages = mapper.readTree(root.resolve("packages/game-content/versions/v1/stages/stages.json"))
    val stageId = string("sourceStageId")
    val stage = stages.path("stages").firstOrNull { it.path("id").asText() == stageId } ?: error("reference stage not in production content: $stageId")
    require(stageId == "stage.01-02") { "reference source stage must be stage.01-02" }
    val level = int("level")
    val equipmentQ = int("equipmentQ")
    require(stage.path("enemyLevel").asInt(-1) == level && stage.path("referenceQ").asInt(-1) == equipmentQ) { "reference build does not match production stage baseline" }
    require(stage.path("referenceSkill").asText() == "강타 N1") { "reference skill does not match production stage baseline" }

    val expectedSlots = EquipmentSlot.entries.map { it.name }.toSet()
    val equipmentNode = required("equipment")
    require(equipmentNode.isObject && equipmentNode.fieldNames().asSequence().toSet() == expectedSlots) { "reference equipment slots must match production slots" }
    val equipment = equipmentNode.fields().asSequence().associate { (slot, value) ->
        require(value.isTextual && value.textValue() == "q$equipmentQ") { "reference equipment ID does not match production Q: $slot" }
        slot to value.textValue()
    }
    require(string("referenceBuildId") == "rb-v1-${stageId}-q${equipmentQ}-N1") { "reference build id drift" }

    val mainGem = required("mainGemPreset")
    val mainGemIds = strings(mainGem.path("gemIds"), "MAIN gem IDs")
    require(mainGemIds.isEmpty() && mainGem.path("effects").isObject && mainGem.path("effects").size() == 0) { "stage.01-02 baseline must have explicit empty MAIN gem data" }
    val cosmetics = required("cosmetics")
    val cosmeticSetIds = strings(cosmetics.path("setIds"), "cosmetic set IDs")
    require(cosmeticSetIds.isEmpty() && cosmetics.path("effects").isObject && cosmetics.path("effects").size() == 0) { "stage.01-02 baseline must have explicit empty cosmetic data" }

    val skillsNode = required("skills")
    val activeOrder = strings(skillsNode.path("activeOrder"), "active skill order")
    val productionSkills = mapper.readTree(root.resolve("packages/game-content/versions/v1/skills/skills.json")).path("skills")
    val heavy = productionSkills.firstOrNull { it.path("skillId").asText() == "active_heavy" } ?: error("production active_heavy skill missing")
    require(heavy.path("active").asBoolean(false) && heavy.path("effects").path("NORMAL").path("baseValue").asInt(-1) == 200) { "production active_heavy N1 baseline drift" }
    fun skillInt(name: String): Int = skillsNode.path(name).takeIf { it.isInt }?.intValue() ?: error("invalid skill $name")
    require(activeOrder == listOf("active_heavy") && skillInt("heavyBasisPoints") == 20_000) { "reference active skill data does not match production baseline" }
    require(skillInt("heavyBasisPoints") == heavy.path("effects").path("NORMAL").path("baseValue").intValue() * 100) { "reference heavy skill does not match production value" }
    require(skillInt("dotTotalBasisPoints") == 0 && skillInt("hasteBasisPoints") == 0 && skillInt("basicAmplificationBasisPoints") == 0 && skillInt("criticalChanceBasisPoints") == 0 && skillInt("allDamageBasisPoints") == 0) { "reference skill profile must explicitly disable unselected skills" }
    val survival = required("acceptableMedianSurvivalTicks")
    val survivalMin = survival.path("min").takeIf { it.isInt }?.intValue() ?: error("invalid survival min")
    val survivalMax = survival.path("max").takeIf { it.isInt }?.intValue() ?: error("invalid survival max")
    require(survivalMin == 550 && survivalMax == 650) { "reference acceptable survival range must be 550 through 650 ticks" }
    return RaidReferenceBuild(contentVersion, stageId, string("referenceBuildId"), equipment, mainGemIds, cosmeticSetIds, level, equipmentQ, seedStart, seedEnd, targetMedian, survivalMin, survivalMax, SkillProfile(skillInt("heavyBasisPoints"), skillInt("dotTotalBasisPoints"), skillInt("hasteBasisPoints"), skillInt("basicAmplificationBasisPoints"), skillInt("criticalChanceBasisPoints"), skillInt("allDamageBasisPoints"), activeOrder = activeOrder))
}
private fun repositoryRoot(): File {
    val current = File(System.getProperty("user.dir")).canonicalFile
    return generateSequence(current) { it.parentFile }
        .firstOrNull { it.resolve("packages/game-content").isDirectory }
        ?: error("repository root not found from $current")
}
private fun player(build: RaidReferenceBuild, attackQ: Int = build.equipmentQ, hpQ: Int = build.equipmentQ, penetrationQ: Int = build.equipmentQ): FighterStats = player(build.level, attackQ, hpQ, penetrationQ)

private fun player(level: Int, attackQ: Int, hpQ: Int, penetrationQ: Int): FighterStats = FighterStats(
    attack = 40 + 2 * (level - 1) +
        EquipmentRules.referenceStatSummary(EquipmentSlot.WEAPON, attackQ).attack +
        EquipmentRules.referenceStatSummary(EquipmentSlot.GLOVES, attackQ).attack,
    maxHp = 400 + 20 * (level - 1) +
        EquipmentRules.referenceStatSummary(EquipmentSlot.ARMOR, hpQ).maxHp +
        EquipmentRules.referenceStatSummary(EquipmentSlot.HELMET, hpQ).maxHp,
    penetration = 20 +
        EquipmentRules.referenceStatSummary(EquipmentSlot.CAPE, penetrationQ).penetration +
        EquipmentRules.referenceStatSummary(EquipmentSlot.SHOES, penetrationQ).penetration,
)

private fun simulate(build: RaidReferenceBuild, player: FighterStats, bossAttack: Int, bossDefense: Int, seed: Long): RaidCombatResult =
    RaidSimulator.simulate(
        RaidCombatInput(
            contentVersion = build.contentVersion,
            seed = seed,
            player = player,
            skills = build.skills,
            bossInitialAttack = bossAttack,
            bossInitialDefense = bossDefense,
        ),
    )

private fun metrics(results: List<RaidCombatResult>, thresholds: Map<String, Long>): RaidBuildMetrics {
    val damages = results.map { it.totalDamage }.sorted()
    val survivals = results.map { it.elapsedTicks }.sorted()
    val gradeDistribution = linkedMapOf("PARTICIPATION" to 0, "D" to 0, "C" to 0, "B" to 0, "A" to 0, "S" to 0, "SS" to 0, "SSS" to 0)
    results.forEach { result ->
        val grade = when {
            result.totalDamage >= thresholds.getValue("SSS") -> "SSS"
            result.totalDamage >= thresholds.getValue("SS") -> "SS"
            result.totalDamage >= thresholds.getValue("S") -> "S"
            result.totalDamage >= thresholds.getValue("A") -> "A"
            result.totalDamage >= thresholds.getValue("B") -> "B"
            result.totalDamage >= thresholds.getValue("C") -> "C"
            result.totalDamage >= thresholds.getValue("D") -> "D"
            else -> "PARTICIPATION"
        }
        gradeDistribution[grade] = gradeDistribution.getValue(grade) + 1
    }
    val bSuccessCount = results.count { it.totalDamage >= thresholds.getValue("B") }
    return RaidBuildMetrics(
        sampleCount = results.size,
        medianSurvivalTicks = percentile(survivals, 0.50),
        p10SurvivalTicks = percentile(survivals, 0.10),
        p50SurvivalTicks = percentile(survivals, 0.50),
        p90SurvivalTicks = percentile(survivals, 0.90),
        earlyDamage = results.sumOf { damageBetween(it, 0, 99) } / results.size,
        lateDamage = results.sumOf { damageBetween(it, 500, Int.MAX_VALUE) } / results.size,
        p10Damage = percentile(damages, 0.10),
        p50Damage = percentile(damages, 0.50),
        p90Damage = percentile(damages, 0.90),
        bSuccessCount = bSuccessCount,
        bSuccessRatePercent = bSuccessCount * 100.0 / results.size,
        gradeDistribution = gradeDistribution,
        fiveMinuteCapCount = results.count { !it.playerDied && it.elapsedTicks >= 3_000 },
    )
}

private fun damageBetween(result: RaidCombatResult, fromTick: Int, throughTick: Int): Long = result.events
    .asSequence()
    .filter { it.logicalTick in fromTick..throughTick }
    .filter { it.type == RaidCombatEventType.PLAYER_HIT || it.type == RaidCombatEventType.DOT_HIT }
    .sumOf { it.damage?.toLong() ?: 0L }

private fun <T> percentile(values: List<T>, fraction: Double): T = values[((values.size - 1) * fraction).toInt()]
internal fun roundHalfUp(value: Double): Long = floor(value + 0.5).toLong()

private fun reportJson(report: RaidBalanceReport): String = buildString {
    appendLine("{")
    appendLine("  \"content\": ${contentFragment(report.content)},")
    appendLine("  \"reference\": ${metricsFragment(report.reference)},")
    appendLine("  \"attackDeficient\": ${metricsFragment(report.attackDeficient)},")
    appendLine("  \"hpDeficient\": ${metricsFragment(report.hpDeficient)},")
    appendLine("  \"penetrationDeficient\": ${metricsFragment(report.penetrationDeficient)},")
    appendLine("  \"populationProjections\": [")
    report.populationProjections.forEachIndexed { index, projection ->
        append("    ${populationFragment(projection)}")
        appendLine(if (index == report.populationProjections.lastIndex) "" else ",")
    }
    appendLine("  ]")
    appendLine("}")
}

private fun contentJson(content: RaidContentCandidate): String = buildString {
    appendLine("{")
    appendLine("  \"authority\": \"working\",")
    appendLine("  \"contentVersion\": \"${content.contentVersion}\",")
    appendLine("  \"unlockStageId\": \"stage.01-02\",")
    appendLine("  \"settlementTimeZone\": \"Asia/Seoul\",")
    appendLine("  \"settlementLocalTime\": \"17:30\",")
    appendLine("  \"sealTarget\": 50000,")
    appendLine("  \"attemptLimitTicks\": 3000,")
    appendLine("  \"escalationIntervalTicks\": 50,")
    appendLine("  \"escalationBasisPoints\": 500,")
    appendLine("  \"bossInitialAttack\": ${content.bossInitialAttack},")
    appendLine("  \"bossInitialDefense\": ${content.bossInitialDefense},")
    appendLine("  \"gradeDamageThresholds\": ${mapFragment(content.gradeDamageThresholds)},")
    appendLine("  \"sealContributions\": { \"PARTICIPATION\": 0, \"D\": 100, \"C\": 167, \"B\": 233, \"A\": 300, \"S\": 367, \"SS\": 433, \"SSS\": 500 },")
    appendLine("  \"personalRewards\": ${personalRewardsFragment()},")
    appendLine("  \"successfulRankRewards\": ${successfulRankRewardsFragment()},")
    appendLine("  \"failedRankRewards\": ${failedRankRewardsFragment()}")
    appendLine("}")
}

private fun contentFragment(content: RaidContentCandidate): String = "{ \"contentVersion\": \"${content.contentVersion}\", \"bossInitialAttack\": ${content.bossInitialAttack}, \"bossInitialDefense\": ${content.bossInitialDefense}, \"gradeDamageThresholds\": ${mapFragment(content.gradeDamageThresholds)} }"
private fun metricsFragment(metrics: RaidBuildMetrics): String = "{ \"sampleCount\": ${metrics.sampleCount}, \"medianSurvivalTicks\": ${metrics.medianSurvivalTicks}, \"p10SurvivalTicks\": ${metrics.p10SurvivalTicks}, \"p50SurvivalTicks\": ${metrics.p50SurvivalTicks}, \"p90SurvivalTicks\": ${metrics.p90SurvivalTicks}, \"earlyDamage\": ${metrics.earlyDamage}, \"lateDamage\": ${metrics.lateDamage}, \"p10Damage\": ${metrics.p10Damage}, \"p50Damage\": ${metrics.p50Damage}, \"p90Damage\": ${metrics.p90Damage}, \"bSuccessCount\": ${metrics.bSuccessCount}, \"bSuccessRatePercent\": ${metrics.bSuccessRatePercent}, \"gradeDistribution\": ${intMapFragment(metrics.gradeDistribution)}, \"fiveMinuteCapCount\": ${metrics.fiveMinuteCapCount} }"
private fun populationFragment(projection: RaidPopulationProjection): String = "{ \"activeParticipants\": ${projection.activeParticipants}, \"gradeMix\": ${intMapFragment(projection.gradeMix)}, \"sealContribution\": ${projection.sealContribution}, \"sealSuccessProbabilityPercent\": ${projection.sealSuccessProbabilityPercent}, \"accountDay\": ${rewardProjectionFragment(projection.accountDay)}, \"serverDay\": ${rewardProjectionFragment(projection.serverDay)}, \"rankDistribution\": ${intMapFragment(projection.rankDistribution)}, \"autoClaimBacklog\": ${projection.autoClaimBacklog}, \"threeParticipationConfirmations\": ${rewardProjectionFragment(projection.threeParticipationConfirmations)} }"
private fun rewardProjectionFragment(reward: RaidRewardProjection): String = "{ \"cosmeticTickets\": ${reward.cosmeticTickets}, \"gemBoxes\": ${reward.gemBoxes}, \"rice\": ${reward.rice} }"
private fun intMapFragment(values: Map<String, Int>): String = values.entries.joinToString(prefix = "{ ", postfix = " }") { (key, value) -> "\"$key\": $value" }
private fun mapFragment(values: Map<String, Long>): String = values.entries.joinToString(prefix = "{ ", postfix = " }") { (key, value) -> "\"$key\": $value" }
private fun reward(tickets: Long, boxes: Long, rice: Long) = "{ \"cosmeticTickets\": $tickets, \"gemBoxes\": $boxes, \"rice\": $rice }"
private fun personalRewardsFragment(): String = linkedMapOf(
    "PARTICIPATION" to reward(5, 3, 5_000), "D" to reward(6, 3, 5_750), "C" to reward(7, 4, 6_500), "B" to reward(7, 4, 7_250),
    "A" to reward(8, 5, 8_000), "S" to reward(9, 5, 8_750), "SS" to reward(10, 6, 9_500), "SSS" to reward(10, 6, 10_000),
).entries.joinToString(prefix = "{ ", postfix = " }") { (key, value) -> "\"$key\": $value" }
private fun successfulRankRewardsFragment(): String = linkedMapOf("FIRST" to reward(11, 7, 11_000), "SECOND_TO_TENTH" to reward(10, 6, 10_000), "ELEVENTH_TO_HUNDREDTH" to reward(9, 5, 9_000), "REST" to reward(8, 5, 8_000)).entries.joinToString(prefix = "{ ", postfix = " }") { (key, value) -> "\"$key\": $value" }
private fun failedRankRewardsFragment(): String = linkedMapOf("FIRST" to reward(8, 5, 7_700), "SECOND_TO_TENTH" to reward(7, 4, 7_000), "ELEVENTH_TO_HUNDREDTH" to reward(6, 4, 6_300), "REST" to reward(6, 4, 5_600)).entries.joinToString(prefix = "{ ", postfix = " }") { (key, value) -> "\"$key\": $value" }
