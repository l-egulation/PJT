package com.hanjjak.balancelab

import com.hanjjak.equipment.domain.EquipmentGrade
import com.hanjjak.equipment.domain.EquipmentRules
import com.hanjjak.equipment.domain.EquipmentSlot
import com.hanjjak.equipment.domain.EquipmentSlotState
import com.hanjjak.inventory.domain.DropTable
import com.hanjjak.inventory.domain.MaterialType
import java.time.Instant
import java.util.UUID
import kotlin.math.floor

internal data class EconomyMetricRow(
    val itemId: String,
    val itemFamily: String,
    val generation: Int?,
    var droppedQuantity: Long = 0,
    var consumedQuantity: Long = 0,
    var listedQuantity: Long = 0,
    var tradeCount: Long = 0,
    var tradedQuantity: Long = 0,
    var tradeAmount: Long = 0,
    var feeAmount: Long = 0,
    var settlementAmount: Long = 0,
)

internal data class EconomySimulationReport(
    val generatedAt: Instant,
    val criteria: EconomyCriteria,
    val progressionTarget: Map<Int, LevelTarget>,
    val fiveDayLevelBandCheck: FiveDayLevelBandCheck,
    val sevenDay: SevenDayEconomyResult,
    val metrics: List<EconomyMetricRow>,
) {
    fun toJson(): String = buildString {
        appendLine("{")
        appendLine("  \"generatedAt\": \"$generatedAt\",")
        appendLine("  \"criteria\": {")
        appendLine("    \"fiveDayP50EffectiveCombatHours\": ${criteria.fiveDayP50EffectiveCombatHours},")
        appendLine("    \"sevenDayP50EffectiveCombatHours\": ${criteria.sevenDayP50EffectiveCombatHours},")
        appendLine("    \"orderSheetsIncluded\": ${criteria.orderSheetsIncluded}")
        appendLine("  },")
        appendLine("  \"progressionTarget\": {")
        progressionTarget.entries.forEachIndexed { index, (level, target) ->
            append("    \"$level\": { \"minutes\": ${target.minutes.format1()}, \"hours\": ${target.hours.format2()} }")
            appendLine(if (index == progressionTarget.size - 1) "" else ",")
        }
        appendLine("  },")
        appendLine("  \"fiveDayLevelBandCheck\": {")
        appendLine("    \"expectedLevelBand\": \"${fiveDayLevelBandCheck.expectedLevelBand}\",")
        appendLine("    \"actualHoursForBand\": [${fiveDayLevelBandCheck.actualHoursForBand.joinToString { it.format2() }}],")
        appendLine("    \"passes\": ${fiveDayLevelBandCheck.passes}")
        appendLine("  },")
        appendLine("  \"sevenDay\": {")
        appendLine("    \"effectiveCombatMinutes\": ${sevenDay.effectiveCombatMinutes},")
        appendLine("    \"cycles\": ${sevenDay.cycles},")
        appendLine("    \"endingLevel\": ${sevenDay.endingLevel},")
        appendLine("    \"endingStage\": \"${sevenDay.endingStage}\",")
        appendLine("    \"riceGenerated\": ${sevenDay.riceGenerated},")
        appendLine("    \"riceConsumedByEquipment\": ${sevenDay.riceConsumedByEquipment},")
        appendLine("    \"tradeAmount\": ${sevenDay.tradeAmount},")
        appendLine("    \"feeAmount\": ${sevenDay.feeAmount},")
        appendLine("    \"ecosystemRiceRemaining\": ${sevenDay.ecosystemRiceRemaining},")
        appendLine("    \"unlockedSlotCount\": ${sevenDay.unlockedSlotCount},")
        appendLine("    \"enhancementCount\": ${sevenDay.enhancementCount},")
        appendLine("    \"listedQuantity\": ${sevenDay.listedQuantity},")
        appendLine("    \"tradedQuantity\": ${sevenDay.tradedQuantity}")
        appendLine("  },")
        appendLine("  \"metrics\": [")
        metrics.forEachIndexed { index, row ->
            appendLine("    {")
            appendLine("      \"itemId\": \"${row.itemId}\",")
            appendLine("      \"itemFamily\": \"${row.itemFamily}\",")
            appendLine("      \"generation\": ${row.generation ?: "null"},")
            appendLine("      \"droppedQuantity\": ${row.droppedQuantity},")
            appendLine("      \"consumedQuantity\": ${row.consumedQuantity},")
            appendLine("      \"listedQuantity\": ${row.listedQuantity},")
            appendLine("      \"tradeCount\": ${row.tradeCount},")
            appendLine("      \"tradedQuantity\": ${row.tradedQuantity},")
            appendLine("      \"tradeAmount\": ${row.tradeAmount},")
            appendLine("      \"feeAmount\": ${row.feeAmount},")
            appendLine("      \"settlementAmount\": ${row.settlementAmount}")
            append("    }")
            appendLine(if (index == metrics.size - 1) "" else ",")
        }
        appendLine("  ]")
        appendLine("}")
    }
}

internal data class EconomyCriteria(
    val fiveDayP50EffectiveCombatHours: Int = 16,
    val sevenDayP50EffectiveCombatHours: Int = 24,
    val orderSheetsIncluded: Boolean = false,
)

internal data class LevelTarget(val minutes: Double, val hours: Double)
internal data class FiveDayLevelBandCheck(val expectedLevelBand: String, val actualHoursForBand: List<Double>, val passes: Boolean)
internal data class SevenDayEconomyResult(
    val effectiveCombatMinutes: Int,
    val cycles: Int,
    val endingLevel: Int,
    val endingStage: String,
    val riceGenerated: Long,
    val riceConsumedByEquipment: Long,
    val tradeAmount: Long,
    val feeAmount: Long,
    val ecosystemRiceRemaining: Long,
    val unlockedSlotCount: Int,
    val enhancementCount: Int,
    val listedQuantity: Long,
    val tradedQuantity: Long,
)

internal fun simulateP50Economy(stages: List<StageRow>, now: Instant = Instant.now()): EconomySimulationReport {
    require(stages.isNotEmpty()) { "STAGES_REQUIRED" }
    val criteria = EconomyCriteria()
    val progressionTargets = listOf(44, 45, 46).associateWith { level ->
        val minutes = targetMinutes(level)
        LevelTarget(minutes, minutes / 60.0)
    }
    val fiveDayHours = criteria.fiveDayP50EffectiveCombatHours.toDouble()
    val bandHours = listOf(progressionTargets.getValue(44).hours, progressionTargets.getValue(46).hours)
    val inventory = linkedMapOf<String, Long>()
    val metrics = linkedMapOf<String, EconomyMetricRow>()
    val dropTable = DropTable()
    val sevenDayMinutes = criteria.sevenDayP50EffectiveCombatHours * 60
    val cycles = floor(sevenDayMinutes * KILLS_PER_MINUTE / KILLS_PER_CYCLE).toInt()
    var riceGenerated = 0L
    var riceConsumed = 0L
    var endingLevel = 1
    var endingStage = stages.first().id

    repeat(cycles) { cycle ->
        val elapsedMinutes = cycle * KILLS_PER_CYCLE / KILLS_PER_MINUTE
        endingLevel = levelAt(elapsedMinutes)
        val stageIndex = stageIndexForLevel(endingLevel, stages.size)
        val stage = stages[stageIndex - 1]
        endingStage = stage.id
        riceGenerated += RICE_PER_CYCLE_MULTIPLIER * stageIndex
        dropTable.rollStage(stage.id, MaterialType.POTATO, 1_000_000L + cycle, 20, bossDefeated = true).forEach { reward ->
            inventory.add(reward.itemId, reward.quantity)
            metrics.row(reward.itemId).droppedQuantity += reward.quantity
        }
    }

    var unlockedSlotCount = 0
    val equipment = mutableListOf<EquipmentSlotState>()
    for (slot in EquipmentSlot.entries) {
        val cost = EquipmentRules.unlockCost(slot)
        val materials = cost.materials.associate { it.itemId to it.requiredQuantity }
        if (inventory.canConsume(materials) && riceGenerated - riceConsumed >= cost.riceCost) {
            inventory.consume(materials) { itemId, quantity -> metrics.row(itemId).consumedQuantity += quantity }
            riceConsumed += cost.riceCost
            unlockedSlotCount += 1
            equipment += EquipmentSlotState(UUID(0, 1), slot, EquipmentGrade.NORMAL, 1)
        }
    }

    var enhancementCount = 0
    equipment.replaceAll { initial ->
        var current = initial
        while (current.enhancementLevel < EquipmentRules.MAX_ENHANCEMENT_LEVEL) {
            val cost = EquipmentRules.enhancementCost(current)
            val materials = cost.materials.associate { it.itemId to it.requiredQuantity }
            if (!inventory.canConsume(materials) || riceGenerated - riceConsumed < cost.riceCost) break
            inventory.consume(materials) { itemId, quantity -> metrics.row(itemId).consumedQuantity += quantity }
            riceConsumed += cost.riceCost
            enhancementCount += 1
            current = current.copy(enhancementLevel = current.enhancementLevel + 1)
        }
        current
    }

    var tradeAmount = 0L
    var feeAmount = 0L
    var listedQuantity = 0L
    var tradedQuantity = 0L
    inventory.filterKeys { "_M" in it }.toSortedMap().forEach { (itemId, quantity) ->
        val quantityToTrade = quantity / 10
        if (quantityToTrade <= 0) return@forEach
        val unitPrice = syntheticUnitPrice(itemId)
        val amount = quantityToTrade * unitPrice
        val fee = amount / 10
        val row = metrics.row(itemId)
        row.listedQuantity += quantityToTrade
        row.tradeCount += 1
        row.tradedQuantity += quantityToTrade
        row.tradeAmount += amount
        row.feeAmount += fee
        row.settlementAmount += amount - fee
        listedQuantity += quantityToTrade
        tradedQuantity += quantityToTrade
        tradeAmount += amount
        feeAmount += fee
    }

    val sevenDay = SevenDayEconomyResult(
        effectiveCombatMinutes = sevenDayMinutes,
        cycles = cycles,
        endingLevel = levelAt(sevenDayMinutes.toDouble()),
        endingStage = endingStage,
        riceGenerated = riceGenerated,
        riceConsumedByEquipment = riceConsumed,
        tradeAmount = tradeAmount,
        feeAmount = feeAmount,
        ecosystemRiceRemaining = riceGenerated - riceConsumed - feeAmount,
        unlockedSlotCount = unlockedSlotCount,
        enhancementCount = enhancementCount,
        listedQuantity = listedQuantity,
        tradedQuantity = tradedQuantity,
    )

    return EconomySimulationReport(
        generatedAt = now,
        criteria = criteria,
        progressionTarget = progressionTargets,
        fiveDayLevelBandCheck = FiveDayLevelBandCheck("44-46", bandHours, fiveDayHours in bandHours[0]..bandHours[1]),
        sevenDay = sevenDay,
        metrics = metrics.values.sortedBy { it.itemId },
    )
}

private const val KILLS_PER_MINUTE = 50.0
private const val KILLS_PER_CYCLE = 21.0
private const val RICE_PER_CYCLE_MULTIPLIER = 30L

private fun targetMinutes(level: Int): Double {
    val x = level - 1
    return 6.0 * x + (11.0 / 30.0) * x * x
}

private fun levelAt(minutes: Double): Int {
    var level = 1
    while (level < 500 && targetMinutes(level + 1) <= minutes) level += 1
    return level
}

private fun stageIndexForLevel(level: Int, stageCount: Int): Int = ((level + 1) / 2).coerceIn(1, stageCount)
private fun syntheticUnitPrice(itemId: String): Long = 100L * (Regex("_M([0-9]+)$").find(itemId)?.groupValues?.get(1)?.toLong() ?: 1L)
private fun MutableMap<String, Long>.add(itemId: String, quantity: Long) { this[itemId] = (this[itemId] ?: 0L) + quantity }
private fun Map<String, Long>.canConsume(costs: Map<String, Long>): Boolean = costs.all { (itemId, quantity) -> (this[itemId] ?: 0L) >= quantity }
private fun MutableMap<String, Long>.consume(costs: Map<String, Long>, onConsumed: (String, Long) -> Unit) {
    costs.forEach { (itemId, quantity) -> this[itemId] = this.getValue(itemId) - quantity }
    costs.forEach { (itemId, quantity) -> onConsumed(itemId, quantity) }
}
private fun MutableMap<String, EconomyMetricRow>.row(itemId: String): EconomyMetricRow = getOrPut(itemId) { EconomyMetricRow(itemId, itemFamily(itemId), itemGeneration(itemId)) }
private fun itemFamily(itemId: String): String = itemId.substringBefore("_M")
private fun itemGeneration(itemId: String): Int? = Regex("_M([0-9]+)$").find(itemId)?.groupValues?.get(1)?.toInt()
private fun Double.format1(): String = "%.1f".format(this)
private fun Double.format2(): String = "%.2f".format(this)
