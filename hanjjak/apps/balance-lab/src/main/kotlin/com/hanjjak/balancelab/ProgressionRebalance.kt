package com.hanjjak.balancelab

import com.fasterxml.jackson.databind.JsonNode
import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.equipment.application.EquipmentBalanceCatalog
import com.hanjjak.equipment.domain.EquipmentBalancePolicy
import com.hanjjak.equipment.domain.EquipmentGrade
import com.hanjjak.equipment.domain.EquipmentRules
import com.hanjjak.equipment.domain.EquipmentSlot
import com.hanjjak.equipment.domain.EquipmentSlotState
import com.hanjjak.gems.domain.GemOption
import com.hanjjak.gems.domain.GemRules
import com.hanjjak.inventory.domain.DropTable
import com.hanjjak.inventory.domain.MaterialType
import com.hanjjak.inventory.infrastructure.JsonMaterialDropContent
import com.hanjjak.progression.domain.CharacterProgression
import com.hanjjak.progression.domain.ProgressionGrant
import com.hanjjak.progression.domain.ProgressionRules
import com.hanjjak.sim.CombatSimulator
import com.hanjjak.sim.CycleInput
import com.hanjjak.sim.EnemyStats
import com.hanjjak.sim.FighterStats
import com.hanjjak.sim.SkillProfile
import java.io.File
import java.util.UUID
import kotlin.math.min
import kotlin.math.roundToInt

/** The progression values and applied economy content used by one simulation run. */
data class ProgressionStageContent(
    val stageId: String,
    val normalExperience: Long,
    val bossExperience: Long,
    val normalRice: Long,
    val bossRice: Long,
    val firstClearRice: Long,
    val firstClearItems: Map<String, Long>,
    val directSkillId: String?,
)

internal data class ProgressionSimulationContent(
    val stages: List<StageRow>,
    val progression: List<ProgressionStageContent>,
    val dropTable: DropTable,
    val equipmentPolicy: EquipmentBalancePolicy,
    val chapterOneNormalQuantity: Long,
    val chapterOneBossQuantity: Long,
) {
    init {
        require(stages.size == progression.size) { "STAGE_AND_PROGRESSION_SIZE_MISMATCH" }
        require(stages.isNotEmpty()) { "STAGES_REQUIRED" }
    }

    val chapterOneM1PerFullCycle: Long
        get() = chapterOneNormalQuantity * 20 + chapterOneBossQuantity
}

data class ChapterArrivalReport(
    val chapter: Int,
    val p50Minutes: Double,
    val p90Minutes: Double,
    val accountCount: Int,
)

data class BossAcceptanceReport(
    val chapter: Int,
    val stageIndex: Int,
    val bossType: String,
    val referenceSuccessRate: Double,
    val deficientSuccessRate: Double,
    val bossHp: Int,
    val bossAttack: Int,
    val defense: Int,
)

data class PromotionProgressionReport(
    val key: String,
    val p50StageIndex: Int,
    val p90StageIndex: Int,
)

enum class CohortKind {
    MARKET_USE,
    NO_MARKET,
    MARKET_LIQUIDITY_SHORTAGE,
}

data class ProgressionCohortReport(
    val kind: CohortKind,
    val accountCount: Int,
    val filledOrders: Long,
    val requestedOrders: Long,
    val p50ChapterFourMinutes: Double,
    val p90ChapterFourMinutes: Double,
) {
    val marketUseAccepted: Boolean
        get() = kind == CohortKind.MARKET_USE && filledOrders > 0
}

data class ProgressionRebalanceReport(
    val accountCount: Int,
    val chapters: List<ChapterArrivalReport>,
    val bosses: List<BossAcceptanceReport>,
    val promotions: List<PromotionProgressionReport>,
    val cohorts: List<ProgressionCohortReport>,
    val marketVolume: Long,
    val marketGrossAmount: Long,
    val marketSellerFee: Long,
    val marketUnmatchedQuantity: Long,
    val marketSystemSellerQuantity: Long,
    val riceSupply: Long,
    val riceConsumption: Long,
    val chapterOneM1PerFullCycle: Long,
    val stageTenRepeatItems: Long,
) {
    val cohortCount: Int
        get() = accountCount

    fun chapter(number: Int): ChapterArrivalReport = chapters.first { it.chapter == number }

    fun promotion(key: String): PromotionProgressionReport = promotions.first { it.key == key }

    fun toCsv(): String = buildString {
        appendLine("section,key,p50,p90,referenceSuccessRate,deficientSuccessRate,details")
        chapters.forEach {
            appendLine("chapter,${it.chapter},${it.p50Minutes.one()},${it.p90Minutes.one()},,,,accounts=${it.accountCount}")
        }
        bosses.forEach {
            appendLine(
                "boss,${it.chapter}-${it.stageIndex},,,${it.referenceSuccessRate.one()}," +
                    "${it.deficientSuccessRate.one()},${it.bossType};hp=${it.bossHp};attack=${it.bossAttack};defense=${it.defense}",
            )
        }
        promotions.forEach { appendLine("promotion,${it.key},${it.p50StageIndex},${it.p90StageIndex},,,") }
        cohorts.forEach {
            appendLine(
                "cohort,${it.kind},${it.p50ChapterFourMinutes.one()},${it.p90ChapterFourMinutes.one()},,," +
                    "filled=${it.filledOrders};requested=${it.requestedOrders}",
            )
        }
        appendLine(
            "economy,marketVolume,$marketVolume,,,,gross=$marketGrossAmount;fee=$marketSellerFee;" +
                "unmatched=$marketUnmatchedQuantity",
        )
        appendLine("economy,riceSupply,$riceSupply,,,,consumed=$riceConsumption")
        appendLine(
            "economy,chapterOneM1PerFullCycle,$chapterOneM1PerFullCycle,,,," +
                "stageTenRepeatItems=$stageTenRepeatItems",
        )
    }

    fun toEconomyJson(): String = buildString {
        appendLine("{")
        appendLine("  \"accountCount\": $accountCount,")
        appendLine(
            "  \"market\": {\"volume\": $marketVolume, \"grossAmount\": $marketGrossAmount, " +
                "\"sellerFee\": $marketSellerFee, \"unmatchedQuantity\": $marketUnmatchedQuantity, " +
                "\"systemSellerQuantity\": $marketSystemSellerQuantity},",
        )
        appendLine("  \"rice\": {\"supply\": $riceSupply, \"consumption\": $riceConsumption},")
        appendLine("  \"chapterOneM1PerFullCycle\": $chapterOneM1PerFullCycle,")
        appendLine("  \"stageTenRepeatItems\": $stageTenRepeatItems,")
        appendLine("  \"cohorts\": [")
        cohorts.forEachIndexed { index, cohort ->
            append(
                "    {\"kind\": \"${cohort.kind}\", \"accountCount\": ${cohort.accountCount}, " +
                    "\"filledOrders\": ${cohort.filledOrders}, \"requestedOrders\": ${cohort.requestedOrders}, " +
                    "\"marketUseAccepted\": ${cohort.marketUseAccepted}, " +
                    "\"p50ChapterFourMinutes\": ${cohort.p50ChapterFourMinutes.one()}, " +
                    "\"p90ChapterFourMinutes\": ${cohort.p90ChapterFourMinutes.one()}}\n",
            )
            if (index != cohorts.lastIndex) append(",")
        }
        appendLine("  ]")
        appendLine("}")
    }
}

internal fun referenceLevel(stageIndex: Int): Int = 2 * stageIndex - 1

internal fun referenceQ(stageIndex: Int): Int {
    require(stageIndex in 1..100)
    val endpoints = listOf(
        1 to 0, 10 to 25, 20 to 54, 30 to 64, 40 to 93,
        50 to 116, 60 to 139, 70 to 162, 80 to 185, 90 to 208, 100 to 231,
    )
    val segment = endpoints.zipWithNext().first { stageIndex in it.first.first..it.second.first }
    val left = segment.first
    val right = segment.second
    return (
        left.second +
            (stageIndex - left.first).toDouble() / (right.first - left.first) *
            (right.second - left.second)
        ).roundToInt()
}

private data class ReferenceBuild(val fighter: FighterStats, val skills: SkillProfile)

/** Growth-wall check axes rotate with period 3, so chapter c matches chapter ((c - 1) % 3) + 1. */
internal fun bossTypeFor(chapter: Int, number: Int): String = when (((chapter - 1) % 3) + 1 to number) {
    1 to 4, 2 to 7, 3 to 10 -> "ATTACK_CHECK"
    1 to 7, 2 to 10, 3 to 4 -> "HP_CHECK"
    1 to 10, 2 to 4, 3 to 7 -> "PENETRATION_CHECK"
    else -> "STANDARD"
}

internal fun referenceGemLevels(stageIndex: Int): List<Int> = when {
    stageIndex == 30 -> listOf(2, 2, 2, 3, 3, 3)
    stageIndex >= 40 -> List(6) { 4 }
    else -> emptyList()
}

private fun referenceBuild(stageIndex: Int, type: String = bossTypeFor((stageIndex - 1) / 10 + 1, (stageIndex - 1) % 10 + 1)): ReferenceBuild {
    val q = referenceQ(stageIndex)
    val gemLevels = referenceGemLevels(stageIndex)
    return ReferenceBuild(
        referenceFighter(referenceLevel(stageIndex), q, q, q, gemLevels, type),
        normalOneSkills(stageIndex),
    )
}

private fun deficientBuild(stageIndex: Int, type: String): ReferenceBuild {
    val q = referenceQ(stageIndex)
    val gemLevels = referenceGemLevels(stageIndex)
    val fighter = when (type) {
        "ATTACK_CHECK" -> referenceFighter(referenceLevel(stageIndex), 0, q, q, gemLevels, type)
        "HP_CHECK" -> referenceFighter(referenceLevel(stageIndex), q, 0, q, gemLevels, type)
        "PENETRATION_CHECK" -> referenceFighter(referenceLevel(stageIndex), q, q, 0, gemLevels, type)
        else -> referenceFighter(referenceLevel(stageIndex), q, q, q, gemLevels, type)
    }
    return ReferenceBuild(fighter, normalOneSkills(stageIndex))
}

internal fun referenceFighter(
    level: Int,
    attackQ: Int,
    hpQ: Int,
    penetrationQ: Int,
    gemLevels: List<Int>,
    type: String,
): FighterStats {
    val weapon = EquipmentRules.referenceStatSummary(EquipmentSlot.WEAPON, attackQ)
    val gloves = EquipmentRules.referenceStatSummary(EquipmentSlot.GLOVES, attackQ)
    val armor = EquipmentRules.referenceStatSummary(EquipmentSlot.ARMOR, hpQ)
    val helmet = EquipmentRules.referenceStatSummary(EquipmentSlot.HELMET, hpQ)
    val cape = EquipmentRules.referenceStatSummary(EquipmentSlot.CAPE, penetrationQ)
    val shoes = EquipmentRules.referenceStatSummary(EquipmentSlot.SHOES, penetrationQ)
    val gemAttack = if (type == "HP_CHECK") 0 else gemLevels.sumOf { GemRules.fixedValue(it, GemOption.FLAT_ATTACK) }
    val gemHp = if (type == "HP_CHECK") gemLevels.sumOf { GemRules.fixedValue(it, GemOption.FLAT_HP) } else 0
    return FighterStats(
        attack = 40 + 2 * (level - 1) + weapon.attack + gloves.attack + gemAttack,
        maxHp = 400 + 20 * (level - 1) + armor.maxHp + helmet.maxHp + gemHp,
        penetration = 20 + cape.penetration + shoes.penetration,
    )
}

internal fun normalOneSkills(stageIndex: Int): SkillProfile = SkillProfile(
    heavyBasisPoints = if (stageIndex >= 2) 20_000 else 0,
    dotTotalBasisPoints = if (stageIndex >= 3) 25_000 else 0,
    hasteBasisPoints = if (stageIndex >= 4) 2_000 else 0,
    basicAmplificationBasisPoints = if (stageIndex >= 5) 2_000 else 0,
    criticalChanceBasisPoints = if (stageIndex >= 6) 500 else 0,
    allDamageBasisPoints = if (stageIndex >= 7) 500 else 0,
)

/**
 * Normal-monster attack is a fixed share of the reference build's max HP so that one hit costs the
 * same fraction of the player's health in every chapter. The previous flat `2 + stageIndex / 2`
 * ramp was decoupled from HP and decayed to noise by chapter 4. Measured ceiling: 0.015 kills the
 * reference build outright around stages 10-20, so the ratio sits at 0.01.
 */
internal const val NORMAL_ATTACK_HP_RATIO: Double = 0.01

/**
 * Growth walls carry less chip damage than farming stages, matching the approved v1 shape. A
 * penetration wall blocks the player's output, so its cycle runs long and chip damage compounds:
 * measured survivable shares there bottom out near 0.0071, against 0.0127 on the other walls.
 */
internal const val WALL_ATTACK_HP_RATIO: Double = 0.0075
internal const val PENETRATION_WALL_ATTACK_HP_RATIO: Double = 0.006

internal fun normalAttackHpRatio(bossType: String): Double = when (bossType) {
    "STANDARD" -> NORMAL_ATTACK_HP_RATIO
    "PENETRATION_CHECK" -> PENETRATION_WALL_ATTACK_HP_RATIO
    else -> WALL_ATTACK_HP_RATIO
}

/**
 * Chapters 1-4 keep their approved linear HP curve. From chapter 5 the pool tracks the reference
 * build instead, because equipment stats grow geometrically in Q (`1.05^(Q/2)`) while the linear
 * formula does not: extending the linear curve to stage 100 left the player with nine times the
 * enemy pool, turning normal monsters into decoration.
 */
internal const val NORMAL_HP_REFERENCE_RATIO: Double = 1.0
internal const val LINEAR_HP_LAST_STAGE: Int = 40

/** Enemy synthesis is content-shaped, not a clock target or a sampled time curve. */
private fun normalStats(stageIndex: Int, bossType: String): EnemyStats {
    val level = referenceLevel(stageIndex)
    val q = referenceQ(stageIndex)
    val referenceMaxHp = referenceFighter(level, q, q, q, referenceGemLevels(stageIndex), "STANDARD").maxHp
    val hp = if (stageIndex <= LINEAR_HP_LAST_STAGE) {
        600 + level * 30 + q * 50
    } else {
        (NORMAL_HP_REFERENCE_RATIO * referenceMaxHp).roundToInt()
    }
    return EnemyStats(
        hp = hp.coerceAtLeast(1),
        attack = (normalAttackHpRatio(bossType) * referenceMaxHp).roundToInt().coerceAtLeast(1),
        defense = q * 5,
    )
}

private fun combat(
    player: ReferenceBuild,
    normal: EnemyStats,
    boss: EnemyStats,
    normalCount: Int,
    limit: Int?,
    strikes: Map<Int, Int>,
    seed: Long,
) = CombatSimulator.simulate(
    CycleInput(
        contentVersion = "progression-rebalance-v1",
        seed = seed,
        player = player.fighter,
        normal = normal,
        boss = boss,
        skills = player.skills,
        normalCount = normalCount,
        bossTimeLimitTicks = limit,
        scheduledStrikes = strikes,
    ),
)

private fun greatestBossHp(build: ReferenceBuild, normal: EnemyStats, normalCount: Int, bossAttack: Int, limit: Int): Int {
    val minimumSuccesses = (1L..1_000L).count {
        combat(build, normal, EnemyStats(1, bossAttack, normal.defense), normalCount, limit, emptyMap(), it).success
    }
    require(minimumSuccesses >= 900) { "REFERENCE_NORMAL_ENCOUNTER_FAILED" }
    var low = 1
    var high = build.fighter.attack * 200
    while (low < high) {
        val middle = low + (high - low + 1) / 2
        val successes = (1L..1_000L).count {
            combat(build, normal, EnemyStats(middle, bossAttack, normal.defense), normalCount, limit, emptyMap(), it).success
        }
        if (successes >= 900) low = middle else high = middle - 1
    }
    return low
}

private fun greatestStrikeDamage(
    build: ReferenceBuild,
    normal: EnemyStats,
    normalCount: Int,
    bossHp: Int,
    limit: Int?,
): Int {
    var low = 1
    var high = (build.fighter.maxHp - 1).coerceAtLeast(1)
    while (low < high) {
        val middle = low + (high - low + 1) / 2
        val successes = (1L..1_000L).count {
            combat(build, normal, EnemyStats(bossHp, 0, normal.defense), normalCount, limit, mapOf(30 to middle), it).success
        }
        if (successes >= 900) low = middle else high = middle - 1
    }
    return low
}

private fun greatestBossAttack(build: ReferenceBuild, normal: EnemyStats, normalCount: Int, bossHp: Int, defense: Int): Int {
    var low = 0
    var high = build.fighter.maxHp
    while (low < high) {
        val middle = low + (high - low + 1) / 2
        val successes = (1L..1_000L).count {
            combat(build, normal, EnemyStats(bossHp, middle, defense), normalCount, null, emptyMap(), it).success
        }
        if (successes >= 900) low = middle else high = middle - 1
    }
    return low
}

private fun bossAcceptanceReports(stages: List<StageRow>): List<BossAcceptanceReport> = stages
    .filter { it.number() in setOf(4, 7, 10) }
    .map { stage ->
        val stageIndex = globalStageIndex(stage.id)
        val chapter = (stageIndex - 1) / 10 + 1
        val within = stage.number()
        val reference = referenceBuild(stageIndex, stage.bossType)
        val deficient = deficientBuild(stageIndex, stage.bossType)
        val normal = EnemyStats(stage.normalHp, stage.normalAttack, stage.defense)
        val boss = EnemyStats(stage.bossHp, stage.bossAttack, stage.defense)
        val normalCount = if (within == 10) 0 else 20
        val limit = stage.timeLimitTicks()
        val strikes = if (stage.bossType == "HP_CHECK") scheduledStrikes(stage.pattern) else emptyMap()
        val referenceRate = (1L..1_000L).count {
            combat(reference, normal, boss, normalCount, limit, strikes, it).success
        } / 1_000.0
        val deficientRate = (1L..1_000L).count {
            combat(deficient, normal, boss, normalCount, limit, strikes, it).success
        } / 1_000.0
        BossAcceptanceReport(
            chapter,
            within,
            stage.bossType,
            referenceRate,
            deficientRate,
            stage.bossHp,
            stage.bossAttack,
            stage.defense,
        )
    }

private data class SimAccount(
    val id: Int,
    val primaryMaterial: MaterialType,
    val marketCohort: CohortKind,
    var progression: CharacterProgression = CharacterProgression(1, 0, 0),
    val inventory: MutableMap<String, Long> = linkedMapOf(),
    val equipment: MutableMap<EquipmentSlot, EquipmentSlotState> = linkedMapOf(),
    val skills: MutableSet<String> = linkedSetOf(),
    val firstClears: MutableSet<String> = linkedSetOf(),
    val arrivalMinutes: MutableMap<Int, Double> = linkedMapOf(),
    var elapsedMinutes: Double = 0.0,
    var normalToRareStage: Int? = null,
    var rareToEpicStage: Int? = null,
    var riceGenerated: Long = 0,
    var riceConsumed: Long = 0,
    var requestedOrders: Long = 0,
    var filledOrders: Long = 0,
    var unmatchedOrders: Long = 0,
    var stalledAtStage: Int? = null
) {
    val marketOptIn: Boolean get() = marketCohort != CohortKind.NO_MARKET
}

private data class Listing(
    val account: SimAccount,
    val itemId: String,
    var quantity: Long,
    val price: Long,
    val time: Double,
)

private data class Demand(
    val account: SimAccount,
    val itemId: String,
    val quantity: Long,
    val maxPrice: Long,
    val reservedRice: Long,
    val time: Double,
)

private data class MarketResult(
    var volume: Long = 0,
    var gross: Long = 0,
    var fee: Long = 0,
    var unmatched: Long = 0,
)

internal fun loadProgressionSimulationContent(
    root: File = File("packages/game-content/versions/progression-rebalance-v1"),
): ProgressionSimulationContent {
    val mapper = ObjectMapper()
    val stageRows = parseSimulationStages(mapper.readTree(root.resolve("stages/stages.json")))
    val progression = parseProgression(mapper.readTree(root.resolve("progression/progression.json")))
    val dropRoot = mapper.readTree(root.resolve("drops/material-drops.json"))
    val dropContent = JsonMaterialDropContent(root.resolve("drops/material-drops.json").inputStream(), mapper).content
    val equipmentContent = com.hanjjak.equipment.infrastructure.JsonEquipmentBalanceCatalog(
        root.resolve("equipment/equipment.json").inputStream(),
        mapper,
    ).content
    val policy = EquipmentBalanceCatalog(
        mapOf(equipmentContent.contentVersion to equipmentContent),
    ).policy(equipmentContent.contentVersion)
    val chapter = dropRoot.path("chapters").path("1")
    return ProgressionSimulationContent(
        stages = stageRows,
        progression = progression,
        dropTable = DropTable(dropContent),
        equipmentPolicy = policy,
        chapterOneNormalQuantity = chapter.path("normalQuantity").asLong(2),
        chapterOneBossQuantity = chapter.path("bossQuantity").asLong(10),
    )
}

internal fun simulateProgressionRebalance(seeds: LongRange): ProgressionRebalanceReport =
    simulateProgressionRebalance(seeds, loadProgressionSimulationContent())

/**
 * Simulates accounts stage-by-stage. A market settlement is deliberately performed at every
 * boundary, before the next target stage is attempted, so a transfer can change later growth.
 */
internal fun simulateProgressionRebalance(
    seeds: LongRange,
    content: ProgressionSimulationContent,
): ProgressionRebalanceReport {
    require(!seeds.isEmpty())
    val seedList = seeds.toList()
    require(seedList.size <= 10_000)
    val accounts = seedList.mapIndexed { index, seed ->
        simulateAccount(index, seed, content)
    }
    val market = MarketResult()

    for (stageIndex in 1..content.progression.size) {
        accounts.forEach { account ->
            if (account.stalledAtStage != null) return@forEach
            try {
                advanceToStage(account, stageIndex, seedList[account.id], content)
            } catch (error: IllegalStateException) {
                if (error.message?.startsWith("STAGE_NOT_CLEARABLE:") == true) {
                    account.stalledAtStage = stageIndex
                } else {
                    throw error
                }
            }
        }
        settleMarket(accounts.filter { it.stalledAtStage == null }, content.equipmentPolicy, stageIndex, market)
        accounts.filter { it.stalledAtStage == null }.forEach { account ->
            growEquipment(account, content.equipmentPolicy, stageIndex)
            if (stageIndex % 10 == 0) {
                account.arrivalMinutes[stageIndex / 10] = account.elapsedMinutes
            }
        }
    }

    val economy = summarizeEconomy(accounts, market)
    val referenceArrivals = seedList.map { seed -> referenceArrivalMinutes(seed, content) }
    val arrivals = (1..10).map { chapter ->
        val values = referenceArrivals.map { it.getValue(chapter) }
        ChapterArrivalReport(
            chapter = chapter,
            p50Minutes = percentile(values, .5),
            p90Minutes = percentile(values, .9),
            accountCount = values.size,
        )
    }
    val bosses = bossAcceptanceReports(content.stages)
    return ProgressionRebalanceReport(
        accountCount = accounts.size,
        chapters = arrivals,
        bosses = bosses,
        promotions = economy.promotions,
        cohorts = economy.cohorts,
        marketVolume = market.volume,
        marketGrossAmount = market.gross,
        marketSellerFee = market.fee,
        marketUnmatchedQuantity = market.unmatched,
        marketSystemSellerQuantity = 0,
        riceSupply = accounts.sumOf { it.riceGenerated },
        riceConsumption = accounts.sumOf { it.riceConsumed } + market.fee,
        chapterOneM1PerFullCycle = content.chapterOneM1PerFullCycle,
        stageTenRepeatItems = stageTenRepeatItems(content),
    )
}


private fun referenceArrivalMinutes(seed: Long, content: ProgressionSimulationContent): Map<Int, Double> {
    var progression = CharacterProgression(1, 0, 0)
    var elapsedMinutes = 0.0
    val arrivals = linkedMapOf<Int, Double>()

    fun runCycle(stageIndex: Int): Boolean {
        val stage = content.stages[stageIndex - 1]
        val reward = content.progression[stageIndex - 1]
        val normalCount = if (stage.number() == 10) 0 else 20
        val build = referenceBuild(stageIndex, stage.bossType)
        val cycleSeed = seed xor (stageIndex.toLong() * 1_000_003L) xor elapsedMinutes.toBits()
        val result = CombatSimulator.simulate(
            CycleInput(
                contentVersion = "progression-rebalance-v1",
                seed = cycleSeed,
                player = build.fighter,
                normal = EnemyStats(stage.normalHp, stage.normalAttack, stage.defense),
                boss = EnemyStats(stage.bossHp, stage.bossAttack, stage.defense),
                skills = build.skills,
                normalCount = normalCount,
                bossTimeLimitTicks = stage.timeLimitTicks(),
                scheduledStrikes = if (stage.bossType == "HP_CHECK") scheduledStrikes(stage.pattern) else emptyMap(),
            ),
        )
        val defeatedWeight = result.defeatedNormals + if (result.success) 5 else 0
        elapsedMinutes += if (defeatedWeight > 0) defeatedWeight / 50.0 else 0.05
        val grant = ProgressionGrant(
            experience = result.defeatedNormals * reward.normalExperience + if (result.success) reward.bossExperience else 0,
            rice = result.defeatedNormals * reward.normalRice + if (result.success) reward.bossRice else 0,
        )
        val applied = ProgressionRules.applyReward(progression, grant)
        progression = CharacterProgression(applied.levelAfter, applied.experienceAfter, applied.riceBalance)
        return result.success
    }

    for (stageIndex in content.stages.indices.map { it + 1 }) {
        val bossOnly = content.stages[stageIndex - 1].number() == 10
        if (bossOnly) {
            while (progression.level < referenceLevel(stageIndex)) runCycle(stageIndex - 1)
        }
        var attempts = 0
        while (!runCycle(stageIndex)) {
            attempts++
            check(attempts < 1_000) { "REFERENCE_STAGE_FAILED:${content.stages[stageIndex - 1].id}" }
        }
        if (!bossOnly) {
            while (progression.level < referenceLevel(stageIndex)) runCycle(stageIndex)
        }
        if (stageIndex % 10 == 0) arrivals[stageIndex / 10] = elapsedMinutes
    }
    return arrivals
}

private fun simulateAccount(
    id: Int,
    seed: Long,
    content: ProgressionSimulationContent,
): SimAccount = SimAccount(
    id = id,
    primaryMaterial = MaterialType.entries[id % MaterialType.entries.size],
    marketCohort = when (id % 4) {
        0 -> CohortKind.MARKET_USE
        1 -> CohortKind.NO_MARKET
        else -> CohortKind.MARKET_LIQUIDITY_SHORTAGE
    },
).also {
    require(seed == seed && content.stages.isNotEmpty())
}

private fun advanceToStage(
    account: SimAccount,
    stageIndex: Int,
    seed: Long,
    content: ProgressionSimulationContent,
) {
    val target = content.stages[stageIndex - 1]
    val targetReward = content.progression[stageIndex - 1]
    val bossOnly = target.number() == 10

    if (bossOnly) {
        val farmIndex = stageIndex - 1
        require(farmIndex >= 1) { "BOSS_STAGE_REQUIRES_PRIOR_FARM" }
        while (account.progression.level < referenceLevel(stageIndex)) {
            runCycle(account, farmIndex, seed, content)
            growEquipment(account, content.equipmentPolicy, stageIndex)
        }
    }

    clearStage(account, stageIndex, seed, content)
    if (account.firstClears.add(targetReward.stageId)) {
        applyReward(account, ProgressionGrant(0, targetReward.firstClearRice))
        targetReward.firstClearItems.forEach { (itemId, quantity) ->
            addInventory(account, itemId, quantity)
        }
        targetReward.directSkillId?.let(account.skills::add)
    }
    growEquipment(account, content.equipmentPolicy, stageIndex)

    if (!bossOnly) {
        while (account.progression.level < referenceLevel(stageIndex)) {
            runCycle(account, stageIndex, seed, content)
            growEquipment(account, content.equipmentPolicy, stageIndex)
        }
    }
}

private fun clearStage(
    account: SimAccount,
    stageIndex: Int,
    seed: Long,
    content: ProgressionSimulationContent,
) {
    repeat(10_000) {
        val beforeProgression = account.progression
        if (runCycle(account, stageIndex, seed, content)) return
        if (content.stages[stageIndex - 1].number() == 10) {
            runCycle(account, stageIndex - 1, seed, content)
        }
        growEquipment(account, content.equipmentPolicy, stageIndex)
        if (account.progression == beforeProgression) {
            error("STAGE_NOT_CLEARABLE:${content.stages[stageIndex - 1].id}")
        }
    }
    error("STAGE_NOT_CLEARABLE:${content.stages[stageIndex - 1].id}")
}

private fun runCycle(
    account: SimAccount,
    stageIndex: Int,
    seed: Long,
    content: ProgressionSimulationContent,
): Boolean {
    val stage = content.stages[stageIndex - 1]
    val reward = content.progression[stageIndex - 1]
    val normalCount = if (stage.number() == 10) 0 else 20
    val cycleSeed = seed xor (stageIndex.toLong() * 1_000_003L) xor account.elapsedMinutes.toBits()
    val player = playerBuild(account, stageIndex, stage.bossType)
    val result = CombatSimulator.simulate(
        CycleInput(
            contentVersion = "progression-rebalance-v1",
            seed = cycleSeed,
            player = player.fighter,
            normal = EnemyStats(stage.normalHp, stage.normalAttack, stage.defense),
            boss = EnemyStats(stage.bossHp, stage.bossAttack, stage.defense),
            skills = player.skills,
            normalCount = normalCount,
            bossTimeLimitTicks = stage.timeLimitTicks(),
            scheduledStrikes = if (stage.bossType == "HP_CHECK") scheduledStrikes(stage.pattern) else emptyMap(),
        ),
    )
    val defeatedWeight = result.defeatedNormals + if (result.success) 5 else 0
    account.elapsedMinutes += if (defeatedWeight > 0) defeatedWeight / 50.0 else 0.05
    applyReward(
        account,
        ProgressionGrant(
            experience = result.defeatedNormals * reward.normalExperience + if (result.success) reward.bossExperience else 0,
            rice = result.defeatedNormals * reward.normalRice + if (result.success) reward.bossRice else 0,
        ),
    )
    content.dropTable.rollStage(
        stageId = stage.id,
        primaryMaterial = account.primaryMaterial,
        seed = cycleSeed,
        defeatedNormals = result.defeatedNormals,
        bossDefeated = result.success,
    ).forEach { addInventory(account, it.itemId, it.quantity) }
    return result.success
}


private data class PlayerBuild(val fighter: FighterStats, val skills: SkillProfile)

private fun playerBuild(account: SimAccount, stageIndex: Int, type: String): PlayerBuild {
    val level = account.progression.level
    val weapon = EquipmentRules.statSummary(EquipmentSlot.WEAPON, account.equipment[EquipmentSlot.WEAPON])
    val gloves = EquipmentRules.statSummary(EquipmentSlot.GLOVES, account.equipment[EquipmentSlot.GLOVES])
    val armor = EquipmentRules.statSummary(EquipmentSlot.ARMOR, account.equipment[EquipmentSlot.ARMOR])
    val helmet = EquipmentRules.statSummary(EquipmentSlot.HELMET, account.equipment[EquipmentSlot.HELMET])
    val cape = EquipmentRules.statSummary(EquipmentSlot.CAPE, account.equipment[EquipmentSlot.CAPE])
    val shoes = EquipmentRules.statSummary(EquipmentSlot.SHOES, account.equipment[EquipmentSlot.SHOES])
    val gemLevels = referenceGemLevels(stageIndex)
    val gemAttack = if (type == "HP_CHECK") 0 else gemLevels.sumOf { GemRules.fixedValue(it, GemOption.FLAT_ATTACK) }
    val gemHp = if (type == "HP_CHECK") gemLevels.sumOf { GemRules.fixedValue(it, GemOption.FLAT_HP) } else 0
    return PlayerBuild(
        fighter = FighterStats(
            attack = 40 + 2 * (level - 1) + weapon.attack + gloves.attack + gemAttack,
            maxHp = 400 + 20 * (level - 1) + armor.maxHp + helmet.maxHp + gemHp,
            penetration = 20 + cape.penetration + shoes.penetration,
        ),
        skills = skillProfile(account.skills),
    )
}

private fun skillProfile(skills: Set<String>): SkillProfile = SkillProfile(
    heavyBasisPoints = if ("active_heavy" in skills) 20_000 else 0,
    dotTotalBasisPoints = if ("active_dot" in skills) 25_000 else 0,
    hasteBasisPoints = if ("active_haste" in skills) 2_000 else 0,
    basicAmplificationBasisPoints = if ("active_basic_amp" in skills) 2_000 else 0,
    criticalChanceBasisPoints = if ("passive_critical" in skills) 500 else 0,
    allDamageBasisPoints = if ("passive_all_damage" in skills) 500 else 0,
)

private fun applyReward(account: SimAccount, grant: ProgressionGrant) {
    val result = ProgressionRules.applyReward(account.progression, grant)
    account.progression = CharacterProgression(result.levelAfter, result.experienceAfter, result.riceBalance)
    account.riceGenerated += grant.rice
}

private fun addInventory(account: SimAccount, itemId: String, quantity: Long) {
    account.inventory[itemId] = (account.inventory[itemId] ?: 0) + quantity
}

private fun growEquipment(account: SimAccount, policy: EquipmentBalancePolicy, stageIndex: Int) {
    for (slot in EquipmentSlot.entries) {
        if (slot !in account.equipment) {
            val cost = EquipmentRules.unlockCost(slot)
            if (account.canPay(cost)) {
                account.pay(cost)
                account.equipment[slot] = EquipmentSlotState(
                    UUID(0, account.id.toLong() + 1),
                    slot,
                    EquipmentGrade.NORMAL,
                    1,
                )
            }
        }
    }
    val checkpoint = nextGrowthCheckpoint(stageIndex)
    val baselineQ = referenceQ(stageIndex)
    val wallQ = referenceQ(checkpoint)
    val priority = growthPriority(checkpoint)
    while (true) {
        val slots = EquipmentSlot.entries
            .filter { slot ->
                val target = if (slot in priority) wallQ else baselineQ
                (account.equipment[slot]?.let(EquipmentRules::q) ?: 0) < target
            }
            .sortedWith(
                compareBy<EquipmentSlot> { if (it in priority) 0 else 1 }
                    .thenBy { account.equipment[it]?.let(EquipmentRules::q) ?: 0 }
                    .thenBy { it.ordinal },
            )
        var changed = false
        for (slot in slots) {
            val state = account.equipment[slot] ?: continue
            if (state.enhancementLevel < EquipmentRules.MAX_ENHANCEMENT_LEVEL) {
                val cost = policy.enhancementCost(state)
                if (account.canPay(cost)) {
                    account.pay(cost)
                    account.equipment[slot] = state.copy(enhancementLevel = state.enhancementLevel + 1)
                    changed = true
                    break
                }
                continue
            }
            val next = EquipmentRules.nextGrade(state.grade) ?: continue
            val requiredStage = policy.requiredPromotionStage(state.grade)?.let(::globalStageIndex) ?: Int.MAX_VALUE
            if (stageIndex < requiredStage) continue
            val cost = policy.promotionCost(state.grade)
            if (account.canPay(cost)) {
                account.pay(cost)
                account.equipment[slot] = state.copy(grade = next, enhancementLevel = 1)
                if (next == EquipmentGrade.RARE && account.normalToRareStage == null) {
                    account.normalToRareStage = stageIndex
                }
                if (next == EquipmentGrade.EPIC && account.rareToEpicStage == null) {
                    account.rareToEpicStage = stageIndex
                }
                changed = true
                break
            }
        }
        if (!changed) return
    }
}

private fun nextGrowthCheckpoint(stageIndex: Int): Int {
    val chapterStart = ((stageIndex - 1) / 10) * 10
    val within = (stageIndex - 1) % 10 + 1
    return chapterStart + (listOf(4, 7, 10).firstOrNull { it >= within } ?: 10)
}

private fun growthPriority(checkpoint: Int): Set<EquipmentSlot> = when (
    bossTypeFor((checkpoint - 1) / 10 + 1, (checkpoint - 1) % 10 + 1)
) {
    "ATTACK_CHECK" -> setOf(EquipmentSlot.WEAPON, EquipmentSlot.GLOVES)
    "HP_CHECK" -> setOf(EquipmentSlot.ARMOR, EquipmentSlot.HELMET)
    "PENETRATION_CHECK" -> setOf(EquipmentSlot.CAPE, EquipmentSlot.SHOES)
    else -> emptySet()
}

private fun settleMarket(
    accounts: List<SimAccount>,
    policy: EquipmentBalancePolicy,
    stageIndex: Int,
    result: MarketResult,
) {
    val listings = mutableListOf<Listing>()
    val demands = mutableListOf<Demand>()
    accounts.forEach { account ->
        val needs = nextEquipmentNeeds(account, policy, stageIndex)

        val requiredByItem = needs.materials
            .groupingBy { it.itemId }
            .fold(0L) { total, material -> total + material.requiredQuantity }
        val itemIds = (account.inventory.keys + requiredByItem.keys).toSet()
        itemIds.forEach { itemId ->
            val available = account.inventory[itemId] ?: 0
            val needed = requiredByItem[itemId] ?: 0
            val surplus = (available - needed).coerceAtLeast(0)
            val shortage = (needed - available).coerceAtLeast(0)
            val price = materialPrice(itemId, stageIndex)
            if (account.marketOptIn && surplus > 0) {
                listings += Listing(account, itemId, surplus, price, account.elapsedMinutes)
            }
            if (account.marketOptIn && shortage > 0 && account.progression.riceBalance > needs.riceCost) {
                demands += Demand(account, itemId, shortage, price, needs.riceCost, account.elapsedMinutes)
                account.requestedOrders += shortage
            }
        }
    }

    for ((itemId, buyerOrders) in demands.groupBy { it.itemId }) {
        val sellers = listings
            .filter { it.itemId == itemId }
            .sortedWith(compareBy<Listing> { it.price }.thenBy { it.time }.thenBy { it.account.id })
        var sellerIndex = 0
        for (buyer in buyerOrders.sortedWith(
            compareBy<Demand> { if (it.account.marketCohort == CohortKind.MARKET_USE) 0 else 1 }
                .thenBy { it.maxPrice }
                .thenBy { it.time }
                .thenBy { it.account.id },
        )) {
            var remaining = buyer.quantity
            while (remaining > 0) {
                while (sellerIndex < sellers.size && sellers[sellerIndex].quantity == 0L) sellerIndex++
                if (sellerIndex >= sellers.size) break
                var candidateIndex = sellerIndex
                if (sellers[candidateIndex].account.id == buyer.account.id) {
                    candidateIndex++
                    while (candidateIndex < sellers.size && sellers[candidateIndex].quantity == 0L) candidateIndex++
                }
                if (candidateIndex >= sellers.size) break
                val seller = sellers[candidateIndex]
                if (seller.price > buyer.maxPrice) break
                val affordableRice = (buyer.account.progression.riceBalance - buyer.reservedRice).coerceAtLeast(0)
                val affordable = affordableRice / seller.price
                val quantity = min(min(remaining, seller.quantity), affordable)
                if (quantity == 0L) break
                val gross = quantity * seller.price
                val fee = gross / 10
                seller.quantity -= quantity
                remaining -= quantity
                buyer.account.inventory[itemId] = (buyer.account.inventory[itemId] ?: 0) + quantity
                seller.account.inventory[itemId] = (seller.account.inventory[itemId] ?: 0) - quantity
                buyer.account.progression = buyer.account.progression.copy(
                    riceBalance = buyer.account.progression.riceBalance - gross,
                )
                seller.account.progression = seller.account.progression.copy(
                    riceBalance = seller.account.progression.riceBalance + gross - fee,
                )
                buyer.account.filledOrders += quantity
                result.volume += quantity
                result.gross += gross
                result.fee += fee
            }
            buyer.account.unmatchedOrders += remaining
            result.unmatched += remaining
        }
    }
}

private data class EquipmentNeeds(
    val riceCost: Long,
    val materials: List<com.hanjjak.equipment.domain.MaterialCost>,
)

private fun nextEquipmentNeeds(
    account: SimAccount,
    policy: EquipmentBalancePolicy,
    stageIndex: Int,
): EquipmentNeeds {
    val costs = mutableListOf<com.hanjjak.equipment.domain.EquipmentCost>()
    EquipmentSlot.entries.forEach { slot ->
        val state = account.equipment[slot]
        if (state == null) {
            costs += EquipmentRules.unlockCost(slot)
            return@forEach
        }
        if (state.enhancementLevel < EquipmentRules.MAX_ENHANCEMENT_LEVEL) {
            costs += policy.enhancementCost(state)
            return@forEach
        }
        val next = EquipmentRules.nextGrade(state.grade)
        val required = policy.requiredPromotionStage(state.grade)?.let(::globalStageIndex) ?: Int.MAX_VALUE
        if (next != null && stageIndex >= required) costs += policy.promotionCost(state.grade)
    }
    return EquipmentNeeds(
        riceCost = costs.sumOf { it.riceCost },
        materials = costs.flatMap { it.materials },
    )
}

private fun materialPrice(itemId: String, stageIndex: Int): Long {
    val generation = Regex("_M(\\d+)$").find(itemId)?.groupValues?.get(1)?.toIntOrNull() ?: 1
    val generationValue = listOf(1L, 5L, 25L, 125L).getOrElse(generation - 1) { 1L }
    return generationValue * 100 + stageIndex * 10L
}

private fun SimAccount.canPay(cost: com.hanjjak.equipment.domain.EquipmentCost): Boolean =
    progression.riceBalance >= cost.riceCost &&
        cost.materials.all { (inventory[it.itemId] ?: 0) >= it.requiredQuantity }

private fun SimAccount.pay(cost: com.hanjjak.equipment.domain.EquipmentCost) {
    cost.materials.forEach { material ->
        inventory[material.itemId] = (inventory[material.itemId] ?: 0) - material.requiredQuantity
    }
    progression = progression.copy(riceBalance = progression.riceBalance - cost.riceCost)
    riceConsumed += cost.riceCost
}

private fun StageRow.number(): Int = id.substringAfter('-').toInt()

private fun StageRow.timeLimitTicks(): Int? {
    if (bossType != "ATTACK_CHECK") return null
    val seconds = Regex("(\\d+)초 제한").find(pattern)?.groupValues?.get(1)?.toIntOrNull()
        ?: error("ATTACK_CHECK_TIME_LIMIT_MISSING:$id")
    return seconds * 10
}

private fun globalStageIndex(stageId: String): Int {
    val match = Regex("^stage\\.(\\d{2})-(\\d{2})$").matchEntire(stageId) ?: return Int.MAX_VALUE
    return (match.groupValues[1].toInt() - 1) * 10 + match.groupValues[2].toInt()
}

private fun scheduledStrikes(pattern: String): Map<Int, Int> {
    val damage = Regex("([0-9,]+)$").find(pattern)?.groupValues?.get(1)?.replace(",", "")?.toIntOrNull() ?: return emptyMap()
    return Regex("[0-9]+").findAll(pattern.substringBefore("초 강타"))
        .associate { it.value.toInt() * 10 to damage }
}

private data class EconomyResult(
    val cohorts: List<ProgressionCohortReport>,
    val promotions: List<PromotionProgressionReport>,
)

private fun summarizeEconomy(accounts: List<SimAccount>, market: MarketResult): EconomyResult {
    val cohorts = CohortKind.entries.map { kind ->
        val members = accounts.filter { it.marketCohort == kind }
        ProgressionCohortReport(
            kind = kind,
            accountCount = members.size,
            filledOrders = members.sumOf { it.filledOrders },
            requestedOrders = if (kind == CohortKind.NO_MARKET) 0 else members.sumOf { it.requestedOrders },
            p50ChapterFourMinutes = percentile(members.map { it.arrivalMinutes[4] ?: it.elapsedMinutes }, .5),
            p90ChapterFourMinutes = percentile(members.map { it.arrivalMinutes[4] ?: it.elapsedMinutes }, .9),
        )
    }
    val notPromoted = (accounts.maxOfOrNull { account ->
        account.arrivalMinutes.keys.maxOrNull()?.times(10) ?: 0
    } ?: 0) + 1.0
    val normal = accounts.map { it.normalToRareStage?.toDouble() ?: notPromoted }
    val rare = accounts.map { it.rareToEpicStage?.toDouble() ?: notPromoted }
    return EconomyResult(
        cohorts = cohorts,
        promotions = listOf(
            PromotionProgressionReport(
                "NORMAL_TO_RARE",
                percentile(normal, .5).roundToInt(),
                percentile(normal, .9).roundToInt(),
            ),
            PromotionProgressionReport(
                "RARE_TO_EPIC",
                percentile(rare, .5).roundToInt(),
                percentile(rare, .9).roundToInt(),
            ),
        ),
    )
}

private fun stageTenRepeatItems(content: ProgressionSimulationContent): Long =
    content.stages
        .filter { it.number() == 10 }
        .sumOf { stage ->
            content.dropTable.rollStage(stage.id, MaterialType.POTATO, 1L, 20, true)
                .filterNot { it.itemId.startsWith("skillbook:") }
                .sumOf { it.quantity }
        }

private fun parseSimulationStages(root: JsonNode): List<StageRow> = root.path("stages").map { node ->
    StageRow(
        node.path("id").asText(),
        node.path("enemyLevel").asInt(),
        node.path("referenceQ").asInt(),
        node.path("referenceSkill").asText(),
        node.path("normalHp").asInt(),
        node.path("normalAttack").asInt(),
        node.path("bossType").asText(),
        node.path("bossHp").asInt(),
        node.path("bossAttack").asInt(),
        node.path("enemyDefense").asInt(),
        node.path("pattern").asText(),
    )
}

private fun parseProgression(root: JsonNode): List<ProgressionStageContent> = root.path("stages").map { node ->
    val first = node.path("firstClear")
    ProgressionStageContent(
        stageId = node.path("stageId").asText(),
        normalExperience = node.path("normalExperience").asLong(),
        bossExperience = node.path("bossExperience").asLong(),
        normalRice = node.path("normalRice").asLong(),
        bossRice = node.path("bossRice").asLong(),
        firstClearRice = first.path("rice").asLong(),
        firstClearItems = first.path("items").associate {
            it.path("itemId").asText() to it.path("quantity").asLong()
        },
        directSkillId = first.path("directSkillId")
            .takeUnless { it.isNull || it.asText().isBlank() }
            ?.asText(),
    )
}

internal fun generatedStageRows(): List<StageRow> = (1..100).map { stage ->
    val chapter = (stage - 1) / 10 + 1
    val number = (stage - 1) % 10 + 1
    val type = bossTypeFor(chapter, number)
    val normal = normalStats(stage, type)
    val reference = referenceBuild(stage, type)
    val normalCount = if (number == 10) 0 else 20
    val defense = if (type == "PENETRATION_CHECK") {
        reference.fighter.penetration + 90 + stage * 2
    } else {
        normal.defense
    }
    val baseBossAttack = if (type == "HP_CHECK" || type == "PENETRATION_CHECK") 0 else normal.attack * 2
    val bossHp = greatestBossHp(
        reference,
        normal.copy(defense = defense),
        normalCount,
        baseBossAttack,
        if (type == "ATTACK_CHECK") 100 else 120,
    )
    val bossAttack = when (type) {
        "PENETRATION_CHECK" -> greatestBossAttack(reference, normal.copy(defense = defense), normalCount, bossHp, defense)
        else -> baseBossAttack
    }
    val strike = if (type == "HP_CHECK") greatestStrikeDamage(reference, normal, normalCount, bossHp, null) else 0
    val pattern = when (type) {
        "ATTACK_CHECK" -> "10초 제한"
        "HP_CHECK" -> "3초 강타 $strike"
        else -> "—"
    }
    StageRow(
        id = "stage.%02d-%02d".format(chapter, number),
        level = referenceLevel(stage),
        q = referenceQ(stage),
        skill = "N1",
        normalHp = normal.hp,
        normalAttack = normal.attack,
        bossType = type,
        bossHp = bossHp,
        bossAttack = bossAttack,
        defense = defense,
        pattern = pattern,
    )
}

private fun percentile(values: List<Double>, fraction: Double): Double {
    if (values.isEmpty()) return 0.0
    val sorted = values.sorted()
    return sorted[((sorted.size - 1) * fraction).roundToInt()]
}

private fun Double.one(): String = "%.1f".format(this)
