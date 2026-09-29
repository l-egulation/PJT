package com.hanjjak.cosmetics.infrastructure

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.cosmetics.domain.CosmeticGrade
import java.io.File
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertNull

class JsonCosmeticsContentTest {
    @Test
    fun `loads the applied numbered placeholder catalog`() {
        val root = File(System.getProperty("user.dir")).parentFile.parentFile
        val file = root.resolve("packages/game-content/versions/v1/cosmetics/cosmetics.json")
        val content = JsonCosmeticsContent(file.inputStream(), ObjectMapper()).content
        assertEquals("cosmetics-v6-angel-yakgwa-back", content.version)
        assertEquals(5_000, content.singleRiceCost)
        // 파일에는 66종·11세트가 그대로 있지만, 부위별 모션 아트가 없는 다섯 세트는 런타임에서 빠진다.
        // 남는 것은 모션이 갖춰진 다섯 세트와, 뽑기 배너를 가진 전설 스시야다.
        assertEquals(48, content.cosmetics.size)
        assertEquals(8, content.sets.size)
        assertEquals(
            listOf(
                "cosmetic-set-01", "cosmetic-set-02", "cosmetic-set-03", "cosmetic-set-06",
                "cosmetic-set-07", "cosmetic-set-08", "cosmetic-set-10", "cosmetic-set-11",
            ),
            content.sets.map { it.id },
        )
        assertEquals(listOf(1, 3, 6, 10, 18), content.starThresholdsByGrade[CosmeticGrade.NORMAL])
        assertEquals(listOf(1, 3, 6, 12, 24), content.starThresholdsByGrade[CosmeticGrade.RARE])
        assertEquals(listOf(1, 2, 4, 8, 16), content.starThresholdsByGrade[CosmeticGrade.EPIC])
        assertEquals(listOf(1, 2, 3, 5, 10), content.starThresholdsByGrade[CosmeticGrade.LEGENDARY])
        assertNull(content.cosmetics.first().displayName)
        assertNull(content.cosmetics.first().imageUrl)
        assertEquals(1, content.sets.count { it.bannerId != null })
    }

    @Test
    fun `keeps every numbered entry in the file so saved ownership still points at the same cosmetic`() {
        val root = File(System.getProperty("user.dir")).parentFile.parentFile
        val file = root.resolve("packages/game-content/versions/v1/cosmetics/cosmetics.json")
        val raw = ObjectMapper().readTree(file)
        assertEquals(66, raw.path("cosmetics").size())
        assertEquals(11, raw.path("sets").size())
        val inactive = raw.path("sets").filter { !it.path("active").asBoolean(true) }.map { it.path("setId").asText() }
        assertEquals(
            listOf("cosmetic-set-04", "cosmetic-set-05", "cosmetic-set-09"),
            inactive,
        )
    }
}
