package com.hanjjak.gameapi

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.cosmetics.infrastructure.JsonCosmeticsContent
import com.hanjjak.inventory.domain.ItemCategory
import org.junit.jupiter.api.Assertions.*
import org.junit.jupiter.api.Test

class CosmeticSelectorBoxItemsTest {
    @Test
    fun `current content supplies only mapped boxes with non null display names`() {
        val content = javaClass.getResourceAsStream("/cosmetics/cosmetics.json")!!.use {
            JsonCosmeticsContent(it, ObjectMapper()).content
        }
        val items = CosmeticSelectorBoxItems(content).items()
        assertEquals(content.sets.mapNotNull { it.boxItemId }, items.map { it.itemId })
        assertEquals(1, items.size)
        assertEquals("세트 #11 선택 상자", items.single().displayName)
        assertEquals(ItemCategory.COSMETIC_BOX, items.single().category)
        assertFalse(items.single().tradeable)
        assertTrue(items.single().stackable)
    }
}
