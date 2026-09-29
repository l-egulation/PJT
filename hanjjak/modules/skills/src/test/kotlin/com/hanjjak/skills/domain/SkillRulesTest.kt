package com.hanjjak.skills.domain

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.sim.CombatSimulator
import com.hanjjak.sim.CycleInput
import com.hanjjak.sim.EnemyStats
import com.hanjjak.sim.FighterStats
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertFalse
import kotlin.test.assertTrue

class SkillRulesTest {
    private val content = SkillContentLoader(ObjectMapper()).load()
    private val rules = SkillRules(content)

    @Test
    fun `versioned enhancement costs use grade coefficients times current level`() {
        assertEquals(90, rules.enhancementRiceCost(SkillGrade.NORMAL, 1))
        assertEquals(810, rules.enhancementRiceCost(SkillGrade.NORMAL, 9))
        assertEquals(180, rules.enhancementRiceCost(SkillGrade.RARE, 1))
        assertEquals(360, rules.enhancementRiceCost(SkillGrade.EPIC, 1))
        assertEquals(720, rules.enhancementRiceCost(SkillGrade.LEGENDARY, 1))
        assertEquals(6_480, rules.enhancementRiceCost(SkillGrade.LEGENDARY, 9))
    }

    @Test
    fun `promotion rice and per attempt composite book costs are versioned`() {
        assertEquals(100, rules.unlockRiceCost())
        assertEquals(4_050, rules.promotionRiceCost(SkillGrade.RARE))
        assertEquals(8_100, rules.promotionRiceCost(SkillGrade.EPIC))
        assertEquals(16_200, rules.promotionRiceCost(SkillGrade.LEGENDARY))
        assertEquals(mapOf(SkillGrade.NORMAL to 1), rules.bookRequirements(SkillGrade.NORMAL))
        assertEquals(mapOf(SkillGrade.NORMAL to 2, SkillGrade.RARE to 1), rules.bookRequirements(SkillGrade.RARE))
        assertEquals(mapOf(SkillGrade.NORMAL to 4, SkillGrade.RARE to 2, SkillGrade.EPIC to 1), rules.bookRequirements(SkillGrade.EPIC))
        assertEquals(mapOf(SkillGrade.NORMAL to 8, SkillGrade.RARE to 4, SkillGrade.EPIC to 2, SkillGrade.LEGENDARY to 1), rules.bookRequirements(SkillGrade.LEGENDARY))
        assertEquals("skillbook:active_heavy:epic", rules.bookItemId("active_heavy", SkillGrade.EPIC))
    }

    @Test
    fun `skill and skillbook catalog names match approved display content`() {
        val catalog = com.hanjjak.inventory.infrastructure.StaticItemCatalog()
        val names = mapOf(
            "active_heavy" to "한짝의 일격",
            "active_dot" to "마! 쫄이나",
            "active_haste" to "잘게 더 잘게!",
            "active_basic_amp" to "화력 최대로!",
            "passive_critical" to "회심의 간",
            "passive_all_damage" to "오늘의 특선",
        )
        names.forEach { (skillId, displayName) ->
            assertEquals(displayName, rules.definitions.first { it.skillId == skillId }.name)
            assertEquals("전설 $displayName 비법서", catalog.require("skillbook:$skillId:legendary").displayName)
        }
    }

    @Test
    fun `success rates follow target level and failure bonus is capped`() {
        assertEquals(7_000, rules.successBasisPoints(2))
        assertEquals(2_000, rules.successBasisPoints(10))
        assertEquals(7_500, rules.successBasisPoints(2, 500))
        assertEquals(10_000, rules.successBasisPoints(10, 8_500))
        assertEquals(10_000, rules.successBasisPoints(10, 20_000))
        assertFailsWith<IllegalArgumentException> { rules.successBasisPoints(1) }
        assertFailsWith<IllegalArgumentException> { rules.successBasisPoints(11) }
    }

    @Test
    fun `all six skills expose approved base and level ten effects at every grade`() {
        val expected = mapOf(
            "active_heavy" to mapOf(SkillGrade.NORMAL to (200 to 290), SkillGrade.RARE to (320 to 455), SkillGrade.EPIC to (480 to 660), SkillGrade.LEGENDARY to (700 to 925)),
            "active_dot" to mapOf(SkillGrade.NORMAL to (250 to 385), SkillGrade.RARE to (420 to 600), SkillGrade.EPIC to (640 to 865), SkillGrade.LEGENDARY to (920 to 1_190)),
            "active_haste" to mapOf(SkillGrade.NORMAL to (20 to 29), SkillGrade.RARE to (32 to 41), SkillGrade.EPIC to (45 to 54), SkillGrade.LEGENDARY to (60 to 69)),
            "active_basic_amp" to mapOf(SkillGrade.NORMAL to (20 to 29), SkillGrade.RARE to (32 to 41), SkillGrade.EPIC to (45 to 54), SkillGrade.LEGENDARY to (60 to 69)),
            "passive_critical" to mapOf(SkillGrade.NORMAL to (5 to 14), SkillGrade.RARE to (16 to 25), SkillGrade.EPIC to (28 to 37), SkillGrade.LEGENDARY to (40 to 49)),
            "passive_all_damage" to mapOf(SkillGrade.NORMAL to (5 to 14), SkillGrade.RARE to (16 to 25), SkillGrade.EPIC to (28 to 37), SkillGrade.LEGENDARY to (40 to 49)),
        )
        for ((skillId, grades) in expected) for ((grade, values) in grades) {
            assertEquals(values.first, rules.effectValue(skillId, grade, 1))
            assertEquals(values.second, rules.effectValue(skillId, grade, 10))
        }
    }

    @Test
    fun `promotion advances one grade and legendary is inaccessible in MVP`() {
        assertEquals(SkillGrade.RARE, rules.promotionTarget(SkillGrade.NORMAL))
        assertEquals(SkillGrade.EPIC, rules.promotionTarget(SkillGrade.RARE))
        assertEquals(SkillGrade.LEGENDARY, rules.promotionTarget(SkillGrade.EPIC))
        assertEquals(null, rules.promotionTarget(SkillGrade.LEGENDARY))
        assertTrue(rules.isGradeAccessible(SkillGrade.NORMAL))
        assertTrue(rules.isGradeAccessible(SkillGrade.RARE))
        assertTrue(rules.isGradeAccessible(SkillGrade.EPIC))
        assertFalse(rules.isGradeAccessible(SkillGrade.LEGENDARY))
        assertTrue(rules.canPromote(SkillGrade.NORMAL, 10))
        assertFalse(rules.canPromote(SkillGrade.NORMAL, 9))
        assertFalse(rules.canPromote(SkillGrade.EPIC, 10))
    }

    @Test
    fun `failure bonus is independent by skill grade and target level`() {
        val state = SkillState("active_heavy", SkillGrade.RARE, 3, 1_000)
        assertEquals(7_000, rules.successBasisPoints(state))
        val other = SkillState("active_dot", SkillGrade.RARE, 3, 0)
        assertEquals(6_000, rules.successBasisPoints(other))
    }

    @Test
    fun `strict loader rejects missing grades skills invalid values and inaccessible mvp grade`() {
        assertFailsWith<IllegalArgumentException> { loadMutated { (it as com.fasterxml.jackson.databind.node.ObjectNode).remove("grades") } }
        assertFailsWith<IllegalArgumentException> { loadMutated { (it.path("skills") as com.fasterxml.jackson.databind.node.ArrayNode).remove(0) } }
        assertFailsWith<IllegalArgumentException> { loadMutated { (it.path("grades").path("NORMAL") as com.fasterxml.jackson.databind.node.ObjectNode).put("enhancementRiceCoefficient", 0) } }
        assertFailsWith<IllegalArgumentException> { loadMutated { (it.path("levelRange") as com.fasterxml.jackson.databind.node.ObjectNode).put("min", 0) } }
        assertFailsWith<IllegalArgumentException> { loadMutated { (it as com.fasterxml.jackson.databind.node.ObjectNode).put("mvpMaxGrade", "LEGENDARY") } }
    }

    private fun loadMutated(change: (com.fasterxml.jackson.databind.JsonNode) -> Unit): SkillContent {
        val mapper = ObjectMapper()
        val input = requireNotNull(javaClass.getResourceAsStream("/skills/skills.json"))
        val root = mapper.readTree(input)
        change(root)
        return SkillContentLoader(mapper).load(root.toString().byteInputStream())
    }
    @Test
    fun `strict loader rejects active passive role drift`() {
        assertFailsWith<IllegalArgumentException> {
            loadMutated { (it.path("skills").get(4) as com.fasterxml.jackson.databind.node.ObjectNode).put("active", true) }
        }
    }

    @Test
    fun `an unequipped active stops fighting`() {
        val heavy = SkillState("active_heavy", SkillGrade.NORMAL, 1, 0)
        val equipped = rules.profile(listOf(heavy), listOf("active_heavy"))
        val benched = rules.profile(listOf(heavy), emptyList())
        assertTrue(equipped.heavyBasisPoints > 0)
        /* 해금은 그대로지만 칸에서 뺐으니 힘을 보태지 않는다. */
        assertEquals(0, benched.heavyBasisPoints)
        /* 예전에는 칸이 비면 기본 네 개를 되돌려 놓아, 다 빼도 스킬이 나갔다. */
        assertEquals(emptyList(), benched.activeOrder)
    }

    @Test
    fun `first-clear passives affect combat without occupying a loadout slot`() {
        val critical = SkillState("passive_critical", SkillGrade.NORMAL, 1, 0)
        val damage = SkillState("passive_all_damage", SkillGrade.NORMAL, 1, 0)
        val criticalProfile = rules.profile(listOf(critical), emptyList())
        val damageProfile = rules.profile(listOf(damage), emptyList())
        assertEquals(500, criticalProfile.criticalChanceBasisPoints)
        assertEquals(500, damageProfile.allDamageBasisPoints)

        val input = CycleInput(
            contentVersion = "test",
            seed = 1,
            player = FighterStats(100, 1_000, 0),
            normal = EnemyStats(1, 0, 0),
            boss = EnemyStats(10_000, 0, 0),
            normalCount = 0,
        )
        val baseline = CombatSimulator.simulateWithEvents(input.copy(skills = rules.profile(emptyList(), emptyList())))
        val boosted = CombatSimulator.simulateWithEvents(input.copy(skills = damageProfile))
        val baselineHit = baseline.renderingTimeline.first { it.type == "PLAYER_ATTACK_IMPACT" }.damage!!
        val boostedHit = boosted.renderingTimeline.first { it.type == "PLAYER_ATTACK_IMPACT" }.damage!!
        assertTrue(boostedHit > baselineHit)
    }
}
