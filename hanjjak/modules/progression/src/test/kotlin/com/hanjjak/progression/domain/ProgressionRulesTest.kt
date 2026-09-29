package com.hanjjak.progression.domain

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertTrue

class ProgressionRulesTest {
    @Test
    fun `chapters five through ten grant experience and rice`() {
        val grant = ProgressionRules.grantForEnemy("stage.10-10", boss = true)

        assertEquals(ProgressionRules.experienceForNormal(100) * 5, grant.experience)
        assertEquals(1_000, grant.rice)
        assertTrue(ProgressionRules.experienceForNormal(100) > ProgressionRules.experienceForNormal(40))
    }

    @Test
    fun `stage index beyond one hundred is rejected`() {
        assertFailsWith<IllegalArgumentException> { ProgressionRules.experienceForNormal(101) }
        assertFailsWith<IllegalArgumentException> { ProgressionRules.grantForEnemy("stage.11-01", boss = false) }
    }

    @Test
    fun `battle reward uses level curve and boss reward`() {
        val reward = ProgressionRules.grantForBattle("stage.01-01", defeatedNormals = 20, bossDefeated = true)

        assertEquals(75, reward.experience)
        assertEquals(30, reward.rice)
    }
    @Test
    fun `battle reward is the sum of individual monster rewards`() {
        val normal = ProgressionRules.grantForEnemy("stage.01-03", boss = false)
        val boss = ProgressionRules.grantForEnemy("stage.01-03", boss = true)
        val cycle = ProgressionRules.grantForBattle("stage.01-03", defeatedNormals = 7, bossDefeated = true)

        assertEquals(normal.experience * 7 + boss.experience, cycle.experience)
        assertEquals(normal.rice * 7 + boss.rice, cycle.rice)
    }


    @Test
    fun `normal kills before death still grant progression`() {
        val reward = ProgressionRules.grantForBattle("stage.01-03", defeatedNormals = 7, bossDefeated = false)

        assertEquals(77, reward.experience)
        assertEquals(21, reward.rice)
    }

    @Test
    fun `single reward can advance multiple levels`() {
        val result = ProgressionRules.applyReward(CharacterProgression(level = 1, experience = 0, riceBalance = 10), ProgressionGrant(experience = 3_100, rice = 25))

        assertEquals(1, result.levelBefore)
        assertEquals(3, result.levelAfter)
        assertEquals(3_100, result.experienceAfter)
        assertEquals(35, result.riceBalance)
        assertEquals(2_900, result.experienceToNextLevel)
    }

    @Test
    fun `max level does not store extra experience`() {
        val capped = ProgressionRules.totalExperienceForLevel(ProgressionRules.MAX_LEVEL)
        val result = ProgressionRules.applyReward(CharacterProgression(level = 500, experience = capped, riceBalance = 0), ProgressionGrant(experience = 10_000, rice = 1))

        assertEquals(500, result.levelAfter)
        assertEquals(capped, result.experienceAfter)
        assertEquals(0, result.experienceGained)
        assertEquals(0, result.experienceToNextLevel)
        assertEquals(1, result.riceBalance)
    }
    @Test
    fun `progression rebalance catalog uses exact boundary rewards`() {
        val json = """{"authority":"working","contentVersion":"progression-rebalance-v1","stages":[{"stageId":"stage.01-01","normalExperience":9,"bossExperience":45,"normalRice":1,"bossRice":10},{"stageId":"stage.04-10","normalExperience":75,"bossExperience":375,"normalRice":40,"bossRice":400}]}"""
        val content = com.hanjjak.progression.infrastructure.JsonProgressionContent(java.io.ByteArrayInputStream(json.toByteArray()), com.fasterxml.jackson.databind.ObjectMapper())
        val catalog = com.hanjjak.progression.application.ProgressionRewardCatalog(mapOf("progression-rebalance-v1" to content))
        assertEquals(9, catalog.grant("progression-rebalance-v1", "stage.01-01", false).experience)
        assertEquals(45, catalog.grant("progression-rebalance-v1", "stage.01-01", true).experience)
        assertEquals(75, catalog.grant("progression-rebalance-v1", "stage.04-10", false).experience)
        assertEquals(375, catalog.grant("progression-rebalance-v1", "stage.04-10", true).experience)

        assertEquals(null, catalog.firstClearOrNull("progression-rebalance-v1", "stage.04-10"))
    }
}
