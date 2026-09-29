package com.hanjjak.equipment.domain

import java.util.UUID

/** Immutable, versioned equipment costs and promotion gates loaded from game content. */
data class EquipmentBalanceContent(
    val authority: String,
    val contentVersion: String,
    val compatibilityOnly: Boolean,
    val enhancementMaterials: Map<EquipmentGrade, Map<Int, List<Long>>>,
    val enhancementRice: Map<EquipmentGrade, List<Long>>,
    val promotion: Map<EquipmentGrade, EquipmentPromotionContent>,
) {
    init {
        require(authority in setOf("working", "applied")) { "equipment content authority missing" }
        require(contentVersion.isNotBlank()) { "invalid equipment content" }
        EquipmentGrade.entries.forEach { grade ->
            val rows = enhancementRice[grade]
            require(rows == null || rows.size == 29) { "invalid equipment rice rows: $grade" }
            enhancementMaterials[grade].orEmpty().forEach { (generation, values) ->
                require(generation in 1..4 && values.size == 29) { "invalid equipment material rows: $grade M$generation" }
                require(values.all { it >= 0 }) { "negative equipment material row: $grade M$generation" }
            }
        }
    }
}

data class EquipmentPromotionContent(
    val requiredStageId: String,
    val materials: Map<Int, Long>,
    val rice: Long,
    val resultGrade: EquipmentGrade,
)

/** Costs and gates for one account-owned equipment balance version. */
class EquipmentBalancePolicy internal constructor(private val content: EquipmentBalanceContent) {
    fun enhancementCost(state: EquipmentSlotState): EquipmentCost {
        require(state.enhancementLevel < EquipmentRules.MAX_ENHANCEMENT_LEVEL) { "EQUIPMENT_MAX_ENHANCEMENT" }
        val row = state.enhancementLevel - 1
        val rice = content.enhancementRice[state.grade]?.getOrNull(row)
            ?: throw IllegalArgumentException("EQUIPMENT_BALANCE_MISSING")
        val materials = content.enhancementMaterials[state.grade].orEmpty()
            .toSortedMap()
            .flatMap { (generation, quantities) ->
                val quantity = quantities.getOrNull(row) ?: throw IllegalArgumentException("EQUIPMENT_BALANCE_MISSING")
                materialFamilies.map { (id, name) -> MaterialCost("${id}_M$generation", "$name M$generation", quantity) }
            }
        return EquipmentCost(rice.toLong(), materials)
    }

    fun promotionCost(grade: EquipmentGrade): EquipmentCost {
        val row = content.promotion[grade] ?: throw IllegalArgumentException("EQUIPMENT_MAX_GRADE")
        require(grade != EquipmentGrade.LEGENDARY && row.resultGrade == EquipmentRules.nextGrade(grade)) { "EQUIPMENT_MAX_GRADE" }
        val materials = row.materials
            .filterValues { it > 0 }
            .toSortedMap()
            .flatMap { (generation, quantity) ->
                materialFamilies.map { (id, name) -> MaterialCost("${id}_M$generation", "$name M$generation", quantity) }
            }
        return EquipmentCost(row.rice, materials)
    }

    fun requiredPromotionStage(grade: EquipmentGrade): String? = when (grade) {
        EquipmentGrade.LEGENDARY -> null
        else -> content.promotion[grade]?.requiredStageId
    }

    companion object {
        private val materialFamilies = listOf("POTATO" to "감자", "SWEET_POTATO" to "고구마", "CORN" to "옥수수")

        fun legacy(): EquipmentBalancePolicy {
            val accountId = UUID(0, 0)
            val state = { grade: EquipmentGrade, level: Int -> EquipmentSlotState(accountId, EquipmentSlot.WEAPON, grade, level) }
            val materials = EquipmentGrade.entries.associateWith { grade ->
                (1..grade.index + 1).associateWith { generation ->
                    (1..29).map { level ->
                        EquipmentRules.enhancementCost(state(grade, level)).materials.first { it.itemId.endsWith("_M$generation") }.requiredQuantity
                    }
                }
            }
            val rice = EquipmentGrade.entries.associateWith { grade ->
                (1..29).map { level -> EquipmentRules.enhancementCost(state(grade, level)).riceCost }
            }
            val promotions = mapOf(
                EquipmentGrade.NORMAL to EquipmentPromotionContent("stage.01-10", mapOf(2 to 10), 1_000, EquipmentGrade.RARE),
                EquipmentGrade.RARE to EquipmentPromotionContent("stage.02-10", mapOf(3 to 10), 5_000, EquipmentGrade.EPIC),
                EquipmentGrade.EPIC to EquipmentPromotionContent("stage.03-10", mapOf(4 to 10), 25_000, EquipmentGrade.LEGENDARY),
            )
            return EquipmentBalancePolicy(EquipmentBalanceContent("applied", "v1", true, materials, rice, promotions))
        }
    }
}
