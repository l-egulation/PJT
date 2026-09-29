package com.hanjjak.equipment.domain

import java.util.UUID
import kotlin.math.pow
import kotlin.math.roundToInt

enum class EquipmentSlot(val label: String) {
    WEAPON("무기"),
    GLOVES("장갑"),
    ARMOR("갑옷"),
    HELMET("투구"),
    CAPE("망토"),
    SHOES("신발"),
}

enum class EquipmentGrade(val index: Int, val label: String) {
    NORMAL(0, "노말"),
    RARE(1, "희귀"),
    EPIC(2, "영웅"),
    LEGENDARY(3, "전설"),
}

data class MaterialCost(
    val itemId: String,
    val displayName: String,
    val requiredQuantity: Long,
    val availableQuantity: Long = 0,
)

data class EquipmentCost(
    val riceCost: Long,
    val materials: List<MaterialCost>,
)

data class EquipmentSlotState(
    val accountId: UUID,
    val slot: EquipmentSlot,
    val grade: EquipmentGrade,
    val enhancementLevel: Int,
) {
    init {
        require(enhancementLevel in 1..EquipmentRules.MAX_ENHANCEMENT_LEVEL)
    }
}

data class EquipmentBattleStats(
    val weaponQ: Int = 0,
    val glovesQ: Int = 0,
    val armorQ: Int = 0,
    val helmetQ: Int = 0,
    val capeQ: Int = 0,
    val shoesQ: Int = 0,
)

data class EquipmentStatSummary(val attack: Int, val maxHp: Int, val penetration: Int)

data class EquipmentGrowthSummary(
    val grade: EquipmentGrade?,
    val gradeName: String?,
    val enhancementLevel: Int,
    val q: Int,
    val stats: EquipmentStatSummary,
)

data class EquipmentActionSummary(
    val cost: EquipmentCost,
    val result: EquipmentGrowthSummary,
    val statIncrease: EquipmentStatSummary,
    val executable: Boolean,
    val disabledReason: String?,
)

data class EquipmentPromotionSummary(
    val requiredStageId: String,
    val chapterCleared: Boolean,
    val maxEnhancementReached: Boolean,
    val cost: EquipmentCost,
    val result: EquipmentGrowthSummary,
    val executable: Boolean,
    val disabledReason: String?,
)

data class EquipmentSlotSummary(
    val slot: EquipmentSlot,
    val slotName: String,
    val unlocked: Boolean,
    val current: EquipmentGrowthSummary,
    val unlock: EquipmentActionSummary?,
    val enhance: EquipmentActionSummary?,
    val promote: EquipmentPromotionSummary?,
    val growthComplete: Boolean,
)

data class EquipmentState(val slots: List<EquipmentSlotSummary>, val riceBalance: Long)
data class EquipmentCommandResult(val slot: EquipmentSlotSummary, val state: EquipmentState)

object EquipmentRules {
    const val MAX_ENHANCEMENT_LEVEL: Int = 30
    const val GRADE_Q_STRIDE: Int = 39

    private enum class StatKind { ATTACK, MAX_HP, PENETRATION }

    private data class SlotCurve(
        val kind: StatKind,
        val base: Int,
        val linearUnit: Double,
        val scaledIndex: Int,
    )

    private val slotCurves = arrayOf(
        SlotCurve(StatKind.ATTACK, base = 36, linearUnit = 0.0, scaledIndex = 0),
        SlotCurve(StatKind.ATTACK, base = 24, linearUnit = 0.0, scaledIndex = 1),
        SlotCurve(StatKind.MAX_HP, base = 360, linearUnit = 0.0, scaledIndex = 2),
        SlotCurve(StatKind.MAX_HP, base = 240, linearUnit = 0.0, scaledIndex = 3),
        SlotCurve(StatKind.PENETRATION, base = 0, linearUnit = 3.6, scaledIndex = -1),
        SlotCurve(StatKind.PENETRATION, base = 0, linearUnit = 2.4, scaledIndex = -1),
    )

    private val materialFamilies = listOf("POTATO" to "감자", "SWEET_POTATO" to "고구마", "CORN" to "옥수수")
    private val generationQuantity = longArrayOf(125, 25, 5, 1)
    private val scaledContributions = buildScaledContributions()

    fun allMaterialItemIds(): Set<String> = materialFamilies.flatMap { (id, _) -> (1..4).map { generation -> "${id}_M$generation" } }.toSet()

    fun unlockCost(slot: EquipmentSlot): EquipmentCost {
        @Suppress("UNUSED_VARIABLE") val requiredSlot = slot
        return EquipmentCost(100, materialsForGeneration(1, 10))
    }

    fun enhancementCost(state: EquipmentSlotState): EquipmentCost {
        require(state.enhancementLevel < MAX_ENHANCEMENT_LEVEL) { "EQUIPMENT_MAX_ENHANCEMENT" }
        val bundles = bundleCount(state).toLong()
        val materials = (1..state.grade.index + 1).flatMap { generation ->
            materialsForGeneration(generation, generationQuantity[generation - 1] * bundles)
        }
        return EquipmentCost(30L * costStep(state), materials)
    }

    fun promotionCost(grade: EquipmentGrade): EquipmentCost = when (grade) {
        EquipmentGrade.NORMAL -> EquipmentCost(1_000, materialsForGeneration(2, 10))
        EquipmentGrade.RARE -> EquipmentCost(5_000, materialsForGeneration(3, 10))
        EquipmentGrade.EPIC -> EquipmentCost(25_000, materialsForGeneration(4, 10))
        EquipmentGrade.LEGENDARY -> throw IllegalArgumentException("EQUIPMENT_MAX_GRADE")
    }

    fun nextGrade(grade: EquipmentGrade): EquipmentGrade? = EquipmentGrade.entries.getOrNull(grade.index + 1)

    fun requiredPromotionStage(grade: EquipmentGrade): String? = when (grade) {
        EquipmentGrade.NORMAL -> "stage.01-10"
        EquipmentGrade.RARE -> "stage.02-10"
        EquipmentGrade.EPIC -> "stage.03-10"
        EquipmentGrade.LEGENDARY -> null
    }

    fun costStep(state: EquipmentSlotState): Int = state.grade.index * MAX_ENHANCEMENT_LEVEL + state.enhancementLevel
    fun bundleCount(state: EquipmentSlotState): Int = (costStep(state) + 9) / 10
    fun q(state: EquipmentSlotState): Int = currentQ(state.grade, state.enhancementLevel)

    fun statSummary(slot: EquipmentSlot, state: EquipmentSlotState?): EquipmentStatSummary =
        statSummary(slot, state?.grade, state?.enhancementLevel ?: 0)

    fun referenceStatSummary(slot: EquipmentSlot, q: Int): EquipmentStatSummary {
        require(q >= 0) { "INVALID_EQUIPMENT_Q" }
        val exactState = gradeAndLevel(q)
        return if (exactState == null) rawStatSummary(slot, q) else statSummary(slot, exactState.first, exactState.second)
    }

    private fun statSummary(slot: EquipmentSlot, grade: EquipmentGrade?, level: Int): EquipmentStatSummary {
        val curve = slotCurves[slot.ordinal]
        val contribution = if (grade == null) {
            rawContribution(curve, 0)
        } else if (curve.scaledIndex >= 0) {
            scaledContributions[contributionIndex(curve.scaledIndex, grade.index, level - 1)]
        } else {
            rawContribution(curve, currentQ(grade, level))
        }
        return summarize(curve, contribution)
    }

    private fun buildScaledContributions(): IntArray {
        val contributions = IntArray(SCALED_SLOT_COUNT * EquipmentGrade.entries.size * MAX_ENHANCEMENT_LEVEL)
        for (curve in slotCurves) {
            if (curve.scaledIndex < 0) continue
            for (grade in EquipmentGrade.entries) {
                val firstIndex = contributionIndex(curve.scaledIndex, grade.index, 0)
                contributions[firstIndex] = scaled(curve.base, startQ(grade))
                for (levelIndex in 1 until MAX_ENHANCEMENT_LEVEL) {
                    val currentQ = currentQ(grade, levelIndex)
                    val rawDelta = scaled(curve.base, currentQ + 1) - scaled(curve.base, currentQ)
                    val lowerGradeMinimum = if (grade.index == 0) {
                        1
                    } else {
                        val lowerCurrent = contributions[contributionIndex(curve.scaledIndex, grade.index - 1, levelIndex)]
                        val lowerPrevious = contributions[contributionIndex(curve.scaledIndex, grade.index - 1, levelIndex - 1)]
                        lowerCurrent - lowerPrevious + 1
                    }
                    val delta = maxOf(1, rawDelta, lowerGradeMinimum)
                    contributions[contributionIndex(curve.scaledIndex, grade.index, levelIndex)] =
                        contributions[contributionIndex(curve.scaledIndex, grade.index, levelIndex - 1)] + delta
                }
            }
        }
        return contributions
    }

    private fun contributionIndex(slotIndex: Int, gradeIndex: Int, levelIndex: Int): Int =
        (slotIndex * EquipmentGrade.entries.size + gradeIndex) * MAX_ENHANCEMENT_LEVEL + levelIndex

    private fun startQ(grade: EquipmentGrade): Int = grade.index * GRADE_Q_STRIDE + 1

    private fun currentQ(grade: EquipmentGrade, level: Int): Int = startQ(grade) + level - 1

    private fun gradeAndLevel(q: Int): Pair<EquipmentGrade, Int>? {
        if (q < 1) return null
        val zeroBasedQ = q - 1
        val gradeIndex = zeroBasedQ / GRADE_Q_STRIDE
        val level = zeroBasedQ % GRADE_Q_STRIDE + 1
        if (gradeIndex >= EquipmentGrade.entries.size || level > MAX_ENHANCEMENT_LEVEL) return null
        return EquipmentGrade.entries[gradeIndex] to level
    }

    private fun rawStatSummary(slot: EquipmentSlot, q: Int): EquipmentStatSummary {
        val curve = slotCurves[slot.ordinal]
        return summarize(curve, rawContribution(curve, q))
    }

    private fun rawContribution(curve: SlotCurve, q: Int): Int = when (curve.kind) {
        StatKind.ATTACK, StatKind.MAX_HP -> scaled(curve.base, q)
        StatKind.PENETRATION -> (curve.linearUnit * q).roundToInt()
    }

    private fun summarize(curve: SlotCurve, contribution: Int): EquipmentStatSummary = when (curve.kind) {
        StatKind.ATTACK -> EquipmentStatSummary(attack = contribution, maxHp = 0, penetration = 0)
        StatKind.MAX_HP -> EquipmentStatSummary(attack = 0, maxHp = contribution, penetration = 0)
        StatKind.PENETRATION -> EquipmentStatSummary(attack = 0, maxHp = 0, penetration = contribution)
    }

    fun aggregateStats(states: Collection<EquipmentSlotState>): EquipmentBattleStats {
        val bySlot = states.associateBy { it.slot }
        return EquipmentBattleStats(
            weaponQ = bySlot[EquipmentSlot.WEAPON]?.let(::q) ?: 0,
            glovesQ = bySlot[EquipmentSlot.GLOVES]?.let(::q) ?: 0,
            armorQ = bySlot[EquipmentSlot.ARMOR]?.let(::q) ?: 0,
            helmetQ = bySlot[EquipmentSlot.HELMET]?.let(::q) ?: 0,
            capeQ = bySlot[EquipmentSlot.CAPE]?.let(::q) ?: 0,
            shoesQ = bySlot[EquipmentSlot.SHOES]?.let(::q) ?: 0,
        )
    }

    private fun materialsForGeneration(generation: Int, quantity: Long): List<MaterialCost> = materialFamilies.map { (id, name) ->
        MaterialCost("${id}_M$generation", "$name M$generation", quantity)
    }

    private fun scaled(base: Int, q: Int): Int = (base * 1.05.pow(q / 2.0)).roundToInt()

    private const val SCALED_SLOT_COUNT: Int = 4
}

