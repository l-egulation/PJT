package com.hanjjak.skills.application

import com.fasterxml.jackson.databind.JsonNode
import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.inventory.application.ItemCatalog
import com.hanjjak.skills.domain.SkillActionKind
import com.hanjjak.skills.domain.SkillActionSummary
import com.hanjjak.skills.domain.SkillDefinition
import com.hanjjak.skills.domain.SkillEnhanceResult
import com.hanjjak.skills.domain.SkillGrade
import com.hanjjak.skills.domain.SkillBookRequirement
import com.hanjjak.skills.domain.SkillScreenState
import com.hanjjak.skills.domain.SkillSummary

/** Reads immutable v1 command snapshots without consulting current progression state. */
internal class LegacySkillResultReader(
    private val mapper: ObjectMapper,
    private val definition: (String) -> SkillDefinition?,
    private val catalog: ItemCatalog,
) {
    fun readEnhance(json: String): SkillEnhanceResult {
        val root = mapper.readTree(json)
        val target = root.path("skill").takeUnless(JsonNode::isMissingNode) ?: root
        val state = readState(root.path("state").takeUnless(JsonNode::isMissingNode) ?: root)
        val targetSummary = readSummary(target, state.riceBalance)
        return SkillEnhanceResult(targetSummary, root.path("success").asBoolean(), state.copy(skills = state.skills.map { if (it.skillId == targetSummary.skillId) targetSummary else it }))
    }

    fun readState(json: String): SkillScreenState = readState(mapper.readTree(json))

    private fun readState(node: JsonNode): SkillScreenState {
        val rice = node.path("riceBalance").asLong(0)
        val skills = node.path("skills").takeIf(JsonNode::isArray)?.map { readSummary(it, rice) } ?: emptyList()
        val loadout = node.path("activeLoadout").takeIf(JsonNode::isArray)?.map(JsonNode::asText) ?: emptyList()
        return SkillScreenState(skills, loadout, rice)
    }

    private fun readSummary(node: JsonNode, riceBalance: Long): SkillSummary {
        val skillId = node.path("skillId").asText()
        val known = runCatching { definition(skillId) }.getOrNull()
        val storedName = storedText(node, "name")
        val storedActive = node.path("active").takeUnless { it.isMissingNode || it.isNull }?.asBoolean()
        val name = storedName ?: known?.name ?: skillId
        val active = storedActive ?: known?.active ?: false
        val unlocked = node.path("unlocked").asBoolean(false)
        val grade = node.path("grade").asText(null)?.let { runCatching { SkillGrade.valueOf(it) }.getOrNull() }
        val level = node.path("level").asInt(0)
        val actionKind = when {
            !unlocked -> SkillActionKind.UNLOCK
            level < 10 -> SkillActionKind.ENHANCE
            else -> SkillActionKind.COMPLETE
        }
        val targetGrade = if (actionKind == SkillActionKind.UNLOCK) SkillGrade.NORMAL else grade
        val targetLevel = when (actionKind) {
            SkillActionKind.UNLOCK -> 1
            SkillActionKind.ENHANCE -> level + 1
            else -> null
        }
        val bookItemId = storedText(node, "bookItemId")
        val availableBooks = storedLong(node, "availableBooks")
        val books = if (actionKind == SkillActionKind.COMPLETE || bookItemId == null) emptyList()
        else listOf(SkillBookRequirement(bookItemId, catalog.find(bookItemId)?.displayName ?: bookItemId, 1, availableBooks))
        val riceCost = if (actionKind == SkillActionKind.COMPLETE) 0 else storedLong(node, "riceCost")
        val successBasisPoints = if (actionKind == SkillActionKind.COMPLETE) 0 else storedLong(node, "nextSuccessBasisPoints").toInt()
        val disabledReason = when {
            actionKind == SkillActionKind.COMPLETE -> null
            availableBooks < 1 -> "INSUFFICIENT_SKILLBOOK"
            riceBalance < riceCost -> "INSUFFICIENT_RICE"
            else -> null
        }
        val action = SkillActionSummary(actionKind, targetGrade, targetLevel, books, riceCost, successBasisPoints, actionKind != SkillActionKind.COMPLETE && disabledReason == null, disabledReason)
        return SkillSummary(skillId, name, active, unlocked, grade, storedText(node, "gradeName") ?: grade?.label, level, node.path("equippedSlot").takeUnless { it.isMissingNode || it.isNull }?.asInt(), node.path("effectText").asText(""), action)
    }

    private fun storedText(node: JsonNode, field: String): String? = node.path(field).takeUnless { it.isMissingNode || it.isNull }?.asText()
    private fun storedLong(node: JsonNode, field: String): Long = node.path(field).takeUnless { it.isMissingNode || it.isNull }?.asLong() ?: 0
}
