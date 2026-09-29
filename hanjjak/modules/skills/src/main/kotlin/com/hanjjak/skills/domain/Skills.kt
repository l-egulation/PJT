package com.hanjjak.skills.domain

enum class SkillGrade(val label: String) { NORMAL("노말"), RARE("희귀"), EPIC("영웅"), LEGENDARY("전설") }

enum class SkillActionKind { UNLOCK, ENHANCE, PROMOTE, LOCKED, COMPLETE }

data class SkillDefinition(val skillId: String, val name: String, val active: Boolean)
data class SkillState(val skillId: String, val grade: SkillGrade, val level: Int, val failureBonusBasisPoints: Int)
data class SkillBookRequirement(val itemId: String, val displayName: String, val requiredQuantity: Long, val availableQuantity: Long)
data class SkillActionSummary(
    val kind: SkillActionKind,
    val targetGrade: SkillGrade?,
    val targetLevel: Int?,
    val books: List<SkillBookRequirement>,
    val riceCost: Long,
    val successBasisPoints: Int,
    val executable: Boolean,
    val disabledReason: String?,
)
data class SkillSummary(
    val skillId: String,
    val name: String,
    val active: Boolean,
    val unlocked: Boolean,
    val grade: SkillGrade?,
    val gradeName: String?,
    val level: Int,
    val equippedSlot: Int?,
    val effectText: String,
    val action: SkillActionSummary,
)
data class SkillLoadoutUpdateRequest(val skillIds: List<String>)
data class SkillEnhanceResult(val skill: SkillSummary, val success: Boolean, val state: SkillScreenState)
data class SkillScreenState(val skills: List<SkillSummary>, val activeLoadout: List<String>, val riceBalance: Long)
