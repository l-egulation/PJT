package com.hanjjak.equipment.domain

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.equipment.application.EquipmentBalanceCatalog
import com.hanjjak.equipment.infrastructure.JsonEquipmentBalanceCatalog
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertNull
import kotlin.test.assertTrue

class EquipmentRulesTest {
    private val accountId = UUID(0, 1)

    private fun state(grade: EquipmentGrade, level: Int, slot: EquipmentSlot = EquipmentSlot.WEAPON) =
        EquipmentSlotState(accountId, slot, grade, level)
    private val scaledSlots = listOf(
        EquipmentSlot.WEAPON,
        EquipmentSlot.GLOVES,
        EquipmentSlot.ARMOR,
        EquipmentSlot.HELMET,
    )

    private fun primary(summary: EquipmentStatSummary): Int =
        summary.attack.takeIf { it != 0 }
            ?: summary.maxHp.takeIf { it != 0 }
            ?: summary.penetration

    @Test
    fun `every equipment enhancement increases its primary integer stat`() {
        EquipmentSlot.entries.forEach { slot ->
            EquipmentGrade.entries.forEach { grade ->
                (1 until EquipmentRules.MAX_ENHANCEMENT_LEVEL).forEach { level ->
                    val current = EquipmentRules.statSummary(slot, state(grade, level, slot))
                    val next = EquipmentRules.statSummary(slot, state(grade, level + 1, slot))
                    assertTrue(primary(next) - primary(current) >= 1, "$slot $grade +$level")
                }
            }
        }
    }

    @Test
    fun `same enhancement step gains more attack or hp in every higher grade`() {
        scaledSlots.forEach { slot ->
            (1 until EquipmentRules.MAX_ENHANCEMENT_LEVEL).forEach { level ->
                val deltas = EquipmentGrade.entries.map { grade ->
                    primary(EquipmentRules.statSummary(slot, state(grade, level + 1, slot))) -
                        primary(EquipmentRules.statSummary(slot, state(grade, level, slot)))
                }
                assertTrue(deltas.zipWithNext().all { (lower, higher) -> higher > lower }, "$slot +$level: $deltas")
            }
        }
    }

    @Test
    fun `promotion one remains above previous grade thirty`() {
        scaledSlots.forEach { slot ->
            EquipmentGrade.entries.zipWithNext().forEach { (grade, nextGrade) ->
                val before = primary(EquipmentRules.statSummary(slot, state(grade, 30, slot)))
                val after = primary(EquipmentRules.statSummary(slot, state(nextGrade, 1, slot)))
                assertTrue(after > before, "$slot $grade -> $nextGrade")
            }
        }
    }

    @Test
    fun `normal glove zero-gain transitions now gain exactly one`() {
        listOf(1, 3, 6, 10, 15).forEach { level ->
            val current = primary(EquipmentRules.statSummary(EquipmentSlot.GLOVES, state(EquipmentGrade.NORMAL, level, EquipmentSlot.GLOVES)))
            val next = primary(EquipmentRules.statSummary(EquipmentSlot.GLOVES, state(EquipmentGrade.NORMAL, level + 1, EquipmentSlot.GLOVES)))
            assertEquals(1, next - current, "GLOVES NORMAL +$level")
        }
    }


    private fun content(version: String): EquipmentBalanceContent {
        val root = java.io.File(System.getProperty("user.dir")).parentFile.parentFile
        val relativePath = if (version == "v1") "v1" else "progression-rebalance-v1"
        return JsonEquipmentBalanceCatalog(
            root.resolve("packages/game-content/versions/$relativePath/equipment/equipment.json").inputStream(),
            ObjectMapper(),
        ).content
    }

    private fun policy(version: String): EquipmentBalancePolicy {
        val loaded = content(version)
        return EquipmentBalanceCatalog(mapOf(loaded.contentVersion to loaded)).policy(version)
    }

    private fun rebalancePolicy(): EquipmentBalancePolicy = policy("progression-rebalance-v1")

    private fun legacyPolicy(): EquipmentBalancePolicy = policy("v1")

    @Test
    fun `legacy JSON preserves exact promotion rows`() {
        val policy = legacyPolicy()

        assertEquals("stage.01-10", policy.requiredPromotionStage(EquipmentGrade.NORMAL))
        assertEquals(EquipmentCost(1_000, materialCosts(2, 10)), policy.promotionCost(EquipmentGrade.NORMAL))
        assertEquals("stage.02-10", policy.requiredPromotionStage(EquipmentGrade.RARE))
        assertEquals(EquipmentCost(5_000, materialCosts(3, 10)), policy.promotionCost(EquipmentGrade.RARE))
        assertEquals("stage.03-10", policy.requiredPromotionStage(EquipmentGrade.EPIC))
        assertEquals(EquipmentCost(25_000, materialCosts(4, 10)), policy.promotionCost(EquipmentGrade.EPIC))
    }

    private fun materialCosts(generation: Int, quantity: Long): List<MaterialCost> =
        listOf("POTATO" to "감자", "SWEET_POTATO" to "고구마", "CORN" to "옥수수").map { (id, name) ->
            MaterialCost("${id}_M$generation", "$name M$generation", quantity)
        }

    @Test
    fun `policy returns parsed promotion gate without content version override`() {
        val source = content("progression-rebalance-v1")
        val customStage = "stage.07-07"
        val patched = source.copy(
            promotion = source.promotion + (EquipmentGrade.RARE to source.promotion.getValue(EquipmentGrade.RARE).copy(requiredStageId = customStage)),
        )
        val policy = EquipmentBalanceCatalog(mapOf(source.contentVersion to patched)).policy(source.contentVersion)

        assertEquals(customStage, policy.requiredPromotionStage(EquipmentGrade.RARE))
    }

    @Test
    fun `progression rebalance policy uses exact enhancement brackets and promotion gates`() {
        val policy = rebalancePolicy()

        assertMaterials(policy.enhancementCost(state(EquipmentGrade.RARE, 1)), m1 = 74, m2 = 8)
        assertMaterials(policy.enhancementCost(state(EquipmentGrade.RARE, 29)), m1 = 206, m2 = 23)
        assertMaterials(policy.enhancementCost(state(EquipmentGrade.EPIC, 1)), m1 = 206, m2 = 41, m3 = 8)
        assertMaterials(policy.enhancementCost(state(EquipmentGrade.EPIC, 29)), m1 = 573, m2 = 115, m3 = 23)
        assertEquals(930, policy.enhancementCost(state(EquipmentGrade.RARE, 1)).riceCost)
        assertEquals(1_770, policy.enhancementCost(state(EquipmentGrade.RARE, 29)).riceCost)

        assertPromotion(policy.promotionCost(EquipmentGrade.NORMAL), m1 = 74, m2 = 1, rice = 870)
        assertPromotion(policy.promotionCost(EquipmentGrade.RARE), m1 = 206, m2 = 23, m3 = 1, rice = 1_770)
        assertEquals("stage.03-10", policy.requiredPromotionStage(EquipmentGrade.RARE))
        assertEquals("stage.05-10", policy.requiredPromotionStage(EquipmentGrade.EPIC))

        // Keyed by full itemId: collapsing to the generation would let two of the three families regress unnoticed.
        val epicPromotion = policy.promotionCost(EquipmentGrade.EPIC).materials.associate { it.itemId to it.requiredQuantity }
        assertEquals(
            listOf("POTATO", "SWEET_POTATO", "CORN").flatMap { family ->
                listOf("${family}_M1" to 573L, "${family}_M2" to 115L, "${family}_M3" to 23L, "${family}_M4" to 1L)
            }.toMap(),
            epicPromotion,
        )
        assertEquals(2_670, policy.promotionCost(EquipmentGrade.EPIC).riceCost)
    }

    @Test
    fun `no material generation costs less after a promotion than the grade below it`() {
        val policy = rebalancePolicy()
        val closing = { grade: EquipmentGrade -> policy.enhancementCost(state(grade, 29)).materials.associate { it.itemId to it.requiredQuantity } }
        val opening = { grade: EquipmentGrade -> policy.enhancementCost(state(grade, 1)).materials.associate { it.itemId to it.requiredQuantity } }
        var compared = 0
        for ((lower, upper) in listOf(
            EquipmentGrade.NORMAL to EquipmentGrade.RARE,
            EquipmentGrade.RARE to EquipmentGrade.EPIC,
            EquipmentGrade.EPIC to EquipmentGrade.LEGENDARY,
        )) {
            val before = closing(lower)
            opening(upper).forEach { (itemId, quantity) ->
                val previous = before[itemId] ?: return@forEach
                compared += 1
                assertTrue(quantity >= previous, "$upper $itemId dropped from $previous to $quantity")
            }
        }
        // 3 families x (M1) + (M1,M2) + (M1,M2,M3). Without this the whole invariant passes vacuously
        // if a generation disappears - the exact regression it exists to catch.
        assertEquals(18, compared, "non-decrease invariant compared nothing: a generation went missing")
    }

    @Test
    fun `progression rebalance enhancement rows are monotonic and match six slot SSOT totals`() {
        val policy = rebalancePolicy()
        for (grade in listOf(EquipmentGrade.NORMAL, EquipmentGrade.RARE, EquipmentGrade.EPIC)) {
            val rows = (1..29).map { level -> policy.enhancementCost(state(grade, level)).materials.associate { it.itemId to it.requiredQuantity } }
            rows.first().keys.forEach { itemId ->
                assertEquals(true, rows.zipWithNext().all { (left, right) -> right.getValue(itemId) >= left.getValue(itemId) })
            }
        }
        val normal = (1..29).sumOf { policy.enhancementCost(state(EquipmentGrade.NORMAL, it)).materials.first { m -> m.itemId == "POTATO_M1" }.requiredQuantity }
        val rareM1 = (1..29).sumOf { policy.enhancementCost(state(EquipmentGrade.RARE, it)).materials.first { m -> m.itemId == "POTATO_M1" }.requiredQuantity }
        val rareM2 = (1..29).sumOf { policy.enhancementCost(state(EquipmentGrade.RARE, it)).materials.first { m -> m.itemId == "POTATO_M2" }.requiredQuantity }
        val epicM1 = (1..29).sumOf { policy.enhancementCost(state(EquipmentGrade.EPIC, it)).materials.first { m -> m.itemId == "POTATO_M1" }.requiredQuantity }
        val epicM2 = (1..29).sumOf { policy.enhancementCost(state(EquipmentGrade.EPIC, it)).materials.first { m -> m.itemId == "POTATO_M2" }.requiredQuantity }
        val epicM3 = (1..29).sumOf { policy.enhancementCost(state(EquipmentGrade.EPIC, it)).materials.first { m -> m.itemId == "POTATO_M3" }.requiredQuantity }
        assertEquals(9_996, normal * 6)
        assertEquals(22_200, rareM1 * 6)
        assertEquals(2_400, rareM2 * 6)
        assertEquals(61_800, epicM1 * 6)
        assertEquals(12_360, epicM2 * 6)
        assertEquals(2_472, epicM3 * 6)
    }

    private fun assertMaterials(cost: EquipmentCost, m1: Long, m2: Long = 0, m3: Long = 0) {
        val actual = cost.materials.associate { it.itemId.substringAfter("_M") to it.requiredQuantity }
        assertEquals(m1, actual["1"])
        assertEquals(if (m2 == 0L) null else m2, actual["2"])
        assertEquals(if (m3 == 0L) null else m3, actual["3"])
    }

    private fun assertPromotion(cost: EquipmentCost, m1: Long, m2: Long = 0, m3: Long = 0, rice: Long) {
        assertMaterials(cost, m1, m2, m3)
        assertEquals(rice, cost.riceCost)
    }

    @Test
    fun `all slots use the same one-time unlock cost`() {
        EquipmentSlot.entries.forEach { slot ->
            val cost = EquipmentRules.unlockCost(slot)
            assertEquals(100, cost.riceCost)
            assertEquals(
                mapOf("POTATO_M1" to 10L, "SWEET_POTATO_M1" to 10L, "CORN_M1" to 10L),
                cost.materials.associate { it.itemId to it.requiredQuantity },
            )
        }
    }

    @Test
    fun `enhancement rice cost remains continuous across grade boundaries`() {
        assertEquals(30, EquipmentRules.enhancementCost(state(EquipmentGrade.NORMAL, 1)).riceCost)
        assertEquals(870, EquipmentRules.enhancementCost(state(EquipmentGrade.NORMAL, 29)).riceCost)
        assertEquals(930, EquipmentRules.enhancementCost(state(EquipmentGrade.RARE, 1)).riceCost)
        assertEquals(1_770, EquipmentRules.enhancementCost(state(EquipmentGrade.RARE, 29)).riceCost)
        assertEquals(1_830, EquipmentRules.enhancementCost(state(EquipmentGrade.EPIC, 1)).riceCost)
        assertEquals(2_670, EquipmentRules.enhancementCost(state(EquipmentGrade.EPIC, 29)).riceCost)
        assertEquals(2_730, EquipmentRules.enhancementCost(state(EquipmentGrade.LEGENDARY, 1)).riceCost)
        assertEquals(3_570, EquipmentRules.enhancementCost(state(EquipmentGrade.LEGENDARY, 29)).riceCost)
        assertEquals("EQUIPMENT_MAX_ENHANCEMENT", assertFailsWith<IllegalArgumentException> {
            EquipmentRules.enhancementCost(state(EquipmentGrade.NORMAL, 30))
        }.message)
    }

    @Test
    fun `material bundles cross ten-step boundaries and accumulate generations`() {
        assertEquals(1, EquipmentRules.bundleCount(state(EquipmentGrade.NORMAL, 1)))
        assertEquals(1, EquipmentRules.bundleCount(state(EquipmentGrade.NORMAL, 10)))
        assertEquals(2, EquipmentRules.bundleCount(state(EquipmentGrade.NORMAL, 11)))
        assertEquals(3, EquipmentRules.bundleCount(state(EquipmentGrade.NORMAL, 29)))
        assertEquals(4, EquipmentRules.bundleCount(state(EquipmentGrade.RARE, 1)))
        assertEquals(12, EquipmentRules.bundleCount(state(EquipmentGrade.LEGENDARY, 29)))

        val rare = EquipmentRules.enhancementCost(state(EquipmentGrade.RARE, 1)).materials.associate { it.itemId to it.requiredQuantity }
        assertEquals(500, rare["POTATO_M1"])
        assertEquals(500, rare["SWEET_POTATO_M1"])
        assertEquals(500, rare["CORN_M1"])
        assertEquals(100, rare["POTATO_M2"])
        assertEquals(100, rare["SWEET_POTATO_M2"])
        assertEquals(100, rare["CORN_M2"])
        assertNull(rare["POTATO_M3"])

        val legendary = EquipmentRules.enhancementCost(state(EquipmentGrade.LEGENDARY, 29)).materials.associate { it.itemId to it.requiredQuantity }
        assertEquals(1_500, legendary["POTATO_M1"])
        assertEquals(300, legendary["SWEET_POTATO_M2"])
        assertEquals(60, legendary["CORN_M3"])
        assertEquals(12, legendary["POTATO_M4"])
    }

    @Test
    fun `promotion rules bind each grade to chapter cost and next grade`() {
        val rare = EquipmentRules.promotionCost(EquipmentGrade.NORMAL)
        assertEquals(EquipmentGrade.RARE, EquipmentRules.nextGrade(EquipmentGrade.NORMAL))
        assertEquals("stage.01-10", EquipmentRules.requiredPromotionStage(EquipmentGrade.NORMAL))
        assertEquals(1_000, rare.riceCost)
        assertEquals(setOf("POTATO_M2", "SWEET_POTATO_M2", "CORN_M2"), rare.materials.map { it.itemId }.toSet())
        assertEquals(setOf(10L), rare.materials.map { it.requiredQuantity }.toSet())

        assertEquals(5_000, EquipmentRules.promotionCost(EquipmentGrade.RARE).riceCost)
        assertEquals("stage.02-10", EquipmentRules.requiredPromotionStage(EquipmentGrade.RARE))
        assertEquals(25_000, EquipmentRules.promotionCost(EquipmentGrade.EPIC).riceCost)
        assertEquals("stage.03-10", EquipmentRules.requiredPromotionStage(EquipmentGrade.EPIC))
        assertNull(EquipmentRules.nextGrade(EquipmentGrade.LEGENDARY))
        assertNull(EquipmentRules.requiredPromotionStage(EquipmentGrade.LEGENDARY))
        assertEquals("EQUIPMENT_MAX_GRADE", assertFailsWith<IllegalArgumentException> {
            EquipmentRules.promotionCost(EquipmentGrade.LEGENDARY)
        }.message)
    }

    @Test
    fun `promotion starts ten q above previous grade maximum`() {
        assertEquals(30, EquipmentRules.q(state(EquipmentGrade.NORMAL, 30)))
        assertEquals(40, EquipmentRules.q(state(EquipmentGrade.RARE, 1)))
        assertEquals(69, EquipmentRules.q(state(EquipmentGrade.RARE, 30)))
        assertEquals(79, EquipmentRules.q(state(EquipmentGrade.EPIC, 1)))
        assertEquals(108, EquipmentRules.q(state(EquipmentGrade.EPIC, 30)))
        assertEquals(118, EquipmentRules.q(state(EquipmentGrade.LEGENDARY, 1)))
        assertEquals(147, EquipmentRules.q(state(EquipmentGrade.LEGENDARY, 30)))
    }
    @Test
    fun `runtime contribution table preserves approved representative values`() {
        assertEquals(EquipmentStatSummary(37, 0, 0), EquipmentRules.statSummary(EquipmentSlot.WEAPON, state(EquipmentGrade.NORMAL, 1, EquipmentSlot.WEAPON)))
        assertEquals(EquipmentStatSummary(26, 0, 0), EquipmentRules.statSummary(EquipmentSlot.GLOVES, state(EquipmentGrade.NORMAL, 2, EquipmentSlot.GLOVES)))
        assertEquals(EquipmentStatSummary(0, 1_938, 0), EquipmentRules.statSummary(EquipmentSlot.ARMOR, state(EquipmentGrade.RARE, 30, EquipmentSlot.ARMOR)))
        assertEquals(EquipmentStatSummary(0, 2_320, 0), EquipmentRules.statSummary(EquipmentSlot.HELMET, state(EquipmentGrade.EPIC, 15, EquipmentSlot.HELMET)))
    }

    @Test
    fun `reference uses guaranteed values only for attainable q and raw curve elsewhere`() {
        val runtime = EquipmentRules.statSummary(
            EquipmentSlot.GLOVES,
            state(EquipmentGrade.RARE, 1, EquipmentSlot.GLOVES),
        )
        assertEquals(runtime, EquipmentRules.referenceStatSummary(EquipmentSlot.GLOVES, 40))
        assertEquals(132, EquipmentRules.referenceStatSummary(EquipmentSlot.GLOVES, 70).attack)
        assertEquals(8_374, EquipmentRules.referenceStatSummary(EquipmentSlot.GLOVES, 240).attack)
    }

    @Test
    fun `permanent slots map to battle q while missing slots retain baseline`() {
        val stats = EquipmentRules.aggregateStats(listOf(
            state(EquipmentGrade.NORMAL, 3, EquipmentSlot.WEAPON),
            state(EquipmentGrade.RARE, 1, EquipmentSlot.ARMOR),
        ))

        assertEquals(3, stats.weaponQ)
        assertEquals(40, stats.armorQ)
        assertEquals(0, stats.glovesQ)
    }
}
