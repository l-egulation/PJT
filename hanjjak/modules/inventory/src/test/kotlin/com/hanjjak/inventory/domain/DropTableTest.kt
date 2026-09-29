package com.hanjjak.inventory.domain

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertTrue

class DropTableTest {
    private val drops = DropTable()

    @Test
    fun `grade unlock stages can drop books for every skill`() {
        val rareSkills = (0L until 300_000L)
            .flatMap { seed -> drops.rollEnemy("stage.02-01", MaterialType.POTATO, seed, 1, false) }
            .mapNotNull { reward -> reward.itemId.takeIf { it.endsWith(":rare") } }
            .toSet()
        val epicSkills = (0L until 300_000L)
            .flatMap { seed -> drops.rollEnemy("stage.04-01", MaterialType.POTATO, seed, 1, false) }
            .mapNotNull { reward -> reward.itemId.takeIf { it.endsWith(":epic") } }
            .toSet()

        assertEquals(SKILL_BOOK_IDS.map { "skillbook:$it:rare" }.toSet(), rareSkills)
        assertEquals(SKILL_BOOK_IDS.map { "skillbook:$it:epic" }.toSet(), epicSkills)
    }

    @Test
    fun `same stage preference and seed produce the same drops`() {
        val first = drops.rollStage("stage.04-09", MaterialType.POTATO, 42, 20, true)
        val second = drops.rollStage("stage.04-09", MaterialType.POTATO, 42, 20, true)
        assertEquals(first, second)
    }
    @Test
    fun `stage rewards are the sum of deterministic enemy rewards`() {
        val expected = ((1..7).flatMap { drops.rollEnemy("stage.03-04", MaterialType.CORN, 99, it, false) } +
            drops.rollEnemy("stage.03-04", MaterialType.CORN, 99, 21, true))
            .groupingBy { it.itemId }
            .fold(0L) { quantity, reward -> quantity + reward.quantity }
            .toSortedMap()
            .map { (itemId, quantity) -> ItemReward(itemId, quantity) }

        assertEquals(expected, drops.rollStage("stage.03-04", MaterialType.CORN, 99, 7, true))
    }


    @Test
    fun `breakthrough stages never drop items`() {
        assertTrue(drops.rollStage("stage.04-10", MaterialType.CORN, 42, 20, true).isEmpty())
    }

    @Test
    fun `material identifiers remain inside the chapter generation pool`() {
        repeat(1_000) { seed ->
            val results = drops.rollStage("stage.02-09", MaterialType.SWEET_POTATO, seed.toLong(), 20, true)
            assertTrue(results.filter { !it.itemId.startsWith("skillbook:") }.all { it.itemId.endsWith("M1") || it.itemId.endsWith("M2") })
        }
    }
    @Test
    fun `rebalance chapter one guarantees two normal and ten boss material units`() {
        val content = MaterialDropContent(
            contentVersion = "progression-rebalance-v1",
            dropChanceBasisPoints = 1_500,
            quantityPerSuccess = 10,
            normalEnemyRolls = 1,
            bossRolls = 5,
            chapterGenerationWeights = mapOf(
                1 to listOf(1, 0, 0, 0),
                2 to listOf(5, 1, 0, 0),
                3 to listOf(25, 5, 1, 0),
                4 to listOf(125, 25, 5, 1),
            ),
            chapterPolicies = mapOf(
                1 to ChapterDropPolicy(DropMode.GUARANTEED, 2, 10, listOf(1, 0, 0, 0)),
                2 to ChapterDropPolicy(DropMode.CHANCE, 10, 10, listOf(5, 1, 0, 0)),
                3 to ChapterDropPolicy(DropMode.CHANCE, 10, 10, listOf(25, 5, 1, 0)),
                4 to ChapterDropPolicy(DropMode.CHANCE, 10, 10, listOf(125, 25, 5, 1)),
            ),
        )
        val materials = DropTable(content).rollStage("stage.01-09", MaterialType.CORN, 99, 20, true)
            .filterNot { it.itemId.startsWith("skillbook:") }

        assertEquals(50, materials.sumOf { it.quantity })
        assertTrue(materials.all { it.itemId.endsWith("M1") })
    }

    companion object {
        private val SKILL_BOOK_IDS = listOf(
            "active_heavy",
            "active_dot",
            "active_haste",
            "active_basic_amp",
            "passive_critical",
            "passive_all_damage",
        )
    }
}
