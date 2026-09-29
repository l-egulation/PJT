package com.hanjjak.cosmetics.domain

import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertNull

class CosmeticsRulesTest {
    private val account = UUID.randomUUID()
    private val thresholds = mapOf(
        CosmeticGrade.NORMAL to listOf(1, 3, 6, 10, 18),
        CosmeticGrade.RARE to listOf(1, 3, 6, 12, 24),
        CosmeticGrade.EPIC to listOf(1, 2, 4, 8, 16),
        CosmeticGrade.LEGENDARY to listOf(1, 2, 3, 5, 10),
    )

    @Test
    fun `first receipt registers and duplicate stays unregistered`() {
        val (first, isNew) = CosmeticsRules.receive(null, account, "cosmetic-061")
        val (duplicate, duplicateIsNew) = CosmeticsRules.receive(first, account, "cosmetic-061")
        assertEquals(true, isNew)
        assertEquals(false, duplicateIsNew)
        assertEquals(1, duplicate.registeredQuantity)
        assertEquals(1, duplicate.unregisteredQuantity)
    }

    @Test
    fun `grade-specific thresholds produce different stars`() {
        assertEquals(4, CosmeticsRules.star(10, thresholds.getValue(CosmeticGrade.NORMAL)))
        assertEquals(3, CosmeticsRules.star(10, thresholds.getValue(CosmeticGrade.RARE)))
        assertEquals(4, CosmeticsRules.star(10, thresholds.getValue(CosmeticGrade.EPIC)))
        assertEquals(5, CosmeticsRules.star(10, thresholds.getValue(CosmeticGrade.LEGENDARY)))
    }

    @Test
    fun `until next star consumes exact available duplicates`() {
        val state = CosmeticState(account, "cosmetic-061", 2, 5, 1)
        val result = CosmeticsRules.register(state, RegistrationMode.UNTIL_NEXT_STAR, thresholds.getValue(CosmeticGrade.LEGENDARY))
        assertEquals(3, result.registeredQuantity)
        assertEquals(4, result.unregisteredQuantity)
        assertEquals(3, CosmeticsRules.star(result.registeredQuantity, thresholds.getValue(CosmeticGrade.LEGENDARY)))
    }

    @Test
    fun `reserved duplicates cannot be registered`() {
        val state = CosmeticState(account, "cosmetic-061", 2, 1, 1)
        assertFailsWith<IllegalArgumentException> {
            CosmeticsRules.register(state, RegistrationMode.UNTIL_NEXT_STAR, thresholds.getValue(CosmeticGrade.LEGENDARY))
        }
    }

    private fun content(): CosmeticsContent {
        val definitions = CosmeticSlot.entries.mapIndexed { index, slot ->
            CosmeticDefinition("cosmetic-${String.format("%03d", index + 61)}", null, null, CosmeticGrade.LEGENDARY, slot, "cosmetic-set-11")
        }
        val effects = mapOf(
            1 to listOf(StatEffect("attackPercent", 25, EffectUnit.BASIS_POINTS)),
            2 to listOf(StatEffect("attackPercent", 50, EffectUnit.BASIS_POINTS)),
        )
        return CosmeticsContent(
            "test-content", 5_000, CosmeticGrade.entries.associateWith { 250_000 }, thresholds,
            definitions,
            listOf(SetDefinition("cosmetic-set-11", null, CosmeticGrade.LEGENDARY, "cosmetic-banner-01", "cosmetic-selector-box-01", definitions.associate { it.slot to it.id }, effects)),
        )
    }

    @Test
    fun `a slot still holding a set that left the game comes back empty`() {
        val content = content()
        val worn = content.cosmetics.first()
        val equipment = listOf(
            EquipmentSlot(account, worn.slot, worn.id),
            EquipmentSlot(account, CosmeticSlot.CAPE, "cosmetic-025"),
        )
        val result = CosmeticsRules.snapshot(listOf(CosmeticState(account, worn.id, 1, 0, 0)), equipment, content)
        assertEquals(worn.id, result.equipment[worn.slot])
        // 악마 세트처럼 게임에서 내린 치장은 목록에 없으므로 착용 중이라고 알리지 않는다.
        assertNull(result.equipment[CosmeticSlot.CAPE])
    }

    @Test
    fun `missing one of six pieces yields zero set star and no effect`() {
        val states = content().cosmetics.dropLast(1).map { CosmeticState(account, it.id, 10, 0, 0) }
        val result = CosmeticsRules.snapshot(states, emptyList(), content())
        assertEquals(0, result.setStars["cosmetic-set-11"])
        assertEquals(emptyList(), result.setEffects["cosmetic-set-11"])
        assertEquals(emptyList(), result.totalEffects)
    }

    @Test
    fun `set uses minimum star and sums generic effects once`() {
        val content = content()
        val states = content.cosmetics.mapIndexed { index, definition ->
            CosmeticState(account, definition.id, if (index == 0) 1 else 2, 0, 0)
        }
        val result = CosmeticsRules.snapshot(states, emptyList(), content)
        assertEquals(1, result.setStars["cosmetic-set-11"])
        assertEquals(listOf(StatEffect("attackPercent", 25, EffectUnit.BASIS_POINTS)), result.totalEffects)
        val upgraded = CosmeticsRules.snapshot(states.map { it.copy(registeredQuantity = 2) }, emptyList(), content)
        assertEquals(listOf(StatEffect("attackPercent", 50, EffectUnit.BASIS_POINTS)), upgraded.totalEffects)
    }

    @Test
    fun `maximum star rejects registration without consuming surplus`() {
        val state = CosmeticState(account, "cosmetic-061", 10, 7, 1)
        val error = assertFailsWith<IllegalArgumentException> {
            CosmeticsRules.register(state, RegistrationMode.UNTIL_NEXT_STAR, thresholds.getValue(CosmeticGrade.LEGENDARY))
        }
        assertEquals("COSMETIC_MAX_STAR", error.message)
        assertEquals(7, state.unregisteredQuantity)
    }

    @Test
    fun `snapshot uses each cosmetic grade thresholds and keeps nullable names`() {
        val content = content()
        val states = listOf(CosmeticState(account, "cosmetic-061", 2, 3, 1))
        val result = CosmeticsRules.snapshot(states, emptyList(), content)
        val head = result.states.first { it.cosmeticId == "cosmetic-061" }
        assertEquals(null, head.displayName)
        assertEquals(2, head.cosmeticStar)
        assertEquals(3, head.nextStarThreshold)
        assertEquals(true, head.canUpgrade)
        assertEquals("NOT_OWNED", result.states.first { it.cosmeticId == "cosmetic-066" }.upgradeDisabledReason)
    }
}
