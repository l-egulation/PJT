package com.hanjjak.cosmetics.api

import com.fasterxml.jackson.annotation.JsonInclude
import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.cosmetics.domain.CosmeticCatalogItem
import com.hanjjak.cosmetics.domain.CosmeticGrade
import com.hanjjak.cosmetics.domain.CosmeticSlot
import com.hanjjak.cosmetics.domain.CosmeticView
import kotlin.test.Test
import kotlin.test.assertTrue

class CosmeticsSerializationTest {
    private val mapper = ObjectMapper().setDefaultPropertyInclusion(JsonInclude.Include.NON_NULL)

    @Test
    fun `catalog retains required null display and image fields under application defaults`() {
        val item = CosmeticCatalogItem("cosmetic-001", null, CosmeticGrade.NORMAL, CosmeticSlot.HEAD, "cosmetic-set-01", null)
        val json = mapper.readTree(mapper.writeValueAsString(item))
        assertTrue(json.has("displayName") && json.get("displayName").isNull)
        assertTrue(json.has("imageUrl") && json.get("imageUrl").isNull)
    }

    @Test
    fun `collection retains required nullable upgrade fields under application defaults`() {
        val maxStar = CosmeticView("cosmetic-001", null, null, CosmeticGrade.NORMAL, CosmeticSlot.HEAD, 2_400, 0, 0, 0, 5, null, null, false, "MAX_STAR")
        val maxJson = mapper.readTree(mapper.writeValueAsString(maxStar))
        assertTrue(maxJson.has("nextStarThreshold") && maxJson.get("nextStarThreshold").isNull)
        assertTrue(maxJson.has("neededForNextStar") && maxJson.get("neededForNextStar").isNull)
        val eligible = maxStar.copy(registeredQuantity = 1, unregisteredQuantity = 1, availableUnregisteredQuantity = 1, cosmeticStar = 1, nextStarThreshold = 2, neededForNextStar = 1, canUpgrade = true, upgradeDisabledReason = null)
        val eligibleJson = mapper.readTree(mapper.writeValueAsString(eligible))
        assertTrue(eligibleJson.has("upgradeDisabledReason") && eligibleJson.get("upgradeDisabledReason").isNull)
    }
}
