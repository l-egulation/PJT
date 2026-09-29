package com.hanjjak.skills.application

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.inventory.infrastructure.StaticItemCatalog
import com.hanjjak.skills.domain.SkillDefinition
import com.hanjjak.skills.domain.SkillActionKind
import com.hanjjak.skills.domain.SkillGrade
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertTrue

class LegacySkillResultReaderTest {
    private val mapper = ObjectMapper()
    private val reader = LegacySkillResultReader(
        mapper,
        { id -> SkillDefinition(id, if (id == "active_heavy") "Stored fallback" else "Other", id.startsWith("active_")) },
        StaticItemCatalog(),
    )

    @Test
    fun `enhancement transforms every stored summary and preserves state envelope`() {
        val json = """
            {
              "skill": { "skillId":"active_heavy", "unlocked":true, "grade":"NORMAL", "level":2, "effectText":"stored target", "bookItemId":"skillbook:active_heavy:normal", "availableBooks":7, "riceCost":180, "nextSuccessBasisPoints":6500 },
              "success":false,
              "state": { "activeLoadout":["active_dot"], "riceBalance":321, "skills":[
                { "skillId":"active_heavy", "name":"Stored heavy", "active":true, "unlocked":true, "grade":"NORMAL", "gradeName":"Stored grade", "level":2, "equippedSlot":1, "effectText":"stored target", "bookItemId":"skillbook:active_heavy:normal", "availableBooks":7, "riceCost":180, "nextSuccessBasisPoints":6500 },
                { "skillId":"active_dot", "name":"Stored dot", "active":true, "unlocked":false, "grade":null, "level":0, "effectText":"stored other", "bookItemId":"skillbook:active_dot:normal", "availableBooks":4, "riceCost":100, "nextSuccessBasisPoints":10000 }
              ] }
            }
        """.trimIndent()
        val result = reader.readEnhance(json)
        assertFalse(result.success)
        assertEquals(listOf("active_dot"), result.state.activeLoadout)
        assertEquals(321, result.state.riceBalance)
        assertEquals(2, result.state.skills.size)
        assertEquals(SkillActionKind.ENHANCE, result.skill.action.kind)
        assertEquals(180, result.skill.action.riceCost)
        assertEquals(6500, result.skill.action.successBasisPoints)
        assertEquals("Stored dot", result.state.skills[1].name)
        assertEquals(SkillActionKind.UNLOCK, result.state.skills[1].action.kind)
    }

    @Test
    fun `level ten legacy summary is complete and has no executable requirements`() {
        val state = reader.readState("""
            { "activeLoadout":[], "riceBalance":999, "skills":[
              { "skillId":"active_heavy", "unlocked":true, "grade":"NORMAL", "level":10, "effectText":"stored max", "bookItemId":"missing", "availableBooks":99, "riceCost":999, "nextSuccessBasisPoints":1 }
            ] }
        """.trimIndent())
        val summary = state.skills.single()
        assertEquals(SkillActionKind.COMPLETE, summary.action.kind)
        assertEquals(0, summary.action.riceCost)
        assertEquals(0, summary.action.successBasisPoints)
        assertTrue(summary.action.books.isEmpty())
        assertFalse(summary.action.executable)
    }

    @Test
    fun `removed legacy skill uses stored identity without current definition`() {
        val removedReader = LegacySkillResultReader(mapper, { null }, StaticItemCatalog())
        val state = removedReader.readState("""
            { "activeLoadout":["legacy_skill"], "riceBalance":77, "skills":[
              { "skillId":"legacy_skill", "name":"보존된 스킬", "active":true, "unlocked":true, "grade":"NORMAL", "level":1, "effectText":"old", "bookItemId":"legacy-book", "availableBooks":2, "riceCost":10, "nextSuccessBasisPoints":9000 }
            ] }
        """.trimIndent())
        assertEquals("보존된 스킬", state.skills.single().name)
        assertTrue(state.skills.single().active)
        assertEquals(listOf("legacy_skill"), state.activeLoadout)
    }

}
