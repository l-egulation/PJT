package com.hanjjak.skills.domain

import com.fasterxml.jackson.databind.JsonNode
import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.sim.SkillProfile
import java.io.InputStream

/** Immutable, versioned values used by skill progression and combat callers. */
data class SkillEffect(val baseValue: Int, val stepValue: Int)
data class SkillGradeContent(
    val enhancementRiceCoefficient: Long,
    val promotionRiceCost: Long,
    val bookRequirements: Map<SkillGrade, Int>,
)
data class SkillContentSkill(
    val skillId: String,
    val name: String,
    val active: Boolean,
    val effects: Map<SkillGrade, SkillEffect>,
)
data class SkillContent(
    val authority: String,
    val contentVersion: String,
    val mvpMaxGrade: SkillGrade,
    val minLevel: Int,
    val maxLevel: Int,
    val successRatesBasisPoints: Map<Int, Int>,
    val grades: Map<SkillGrade, SkillGradeContent>,
    val skills: List<SkillContentSkill>,
)

/** Reads only applied content and validates every field needed by pure rules. */
class SkillContentLoader(private val mapper: ObjectMapper) {
    fun load(): SkillContent = requireNotNull(javaClass.getResourceAsStream("/skills/skills.json")) {
        "applied skill content missing"
    }.use(::load)

    fun load(input: InputStream): SkillContent {
        val root = mapper.readTree(input)
        require(root.isObject) { "skill content must be an object" }
        requireKeys(root, "authority", "contentVersion", "mvpMaxGrade", "levelRange", "successRatesBasisPoints", "grades", "skills")
        require(root.path("authority").asText() == "applied") { "skill content is not applied" }
        val contentVersion = requiredText(root, "contentVersion")
        val maxGrade = parseGrade(requiredText(root, "mvpMaxGrade"))
        val levels = root.path("levelRange")
        require(levels.isObject && levels.fieldNames().asSequence().toSet() == setOf("min", "max")) { "invalid skill level range" }
        val minLevel = levels.path("min").asInt(0)
        val maxLevel = levels.path("max").asInt(0)
        require(minLevel == 1 && maxLevel == 10) { "invalid skill level range" }

        val success = root.path("successRatesBasisPoints")
        require(success.isObject) { "missing success rates" }
        val successRates = success.properties().asSequence().associate { (key, value) ->
            val level = key.toIntOrNull() ?: throw IllegalArgumentException("invalid success level: $key")
            require(level in 2..maxLevel && value.isInt && value.asInt() in 1..10_000) { "invalid success rate: $key" }
            level to value.asInt()
        }
        require(successRates.keys == (2..maxLevel).toSet()) { "missing success rates" }

        val gradeNode = root.path("grades")
        require(gradeNode.isObject) { "missing skill grades" }
        require(gradeNode.fieldNames().asSequence().toSet() == SkillGrade.entries.map { it.name }.toSet()) { "missing skill grades" }
        val grades = SkillGrade.entries.associateWith { grade -> parseGradeContent(gradeNode.path(grade.name), grade) }

        val skillNode = root.path("skills")
        require(skillNode.isArray) { "missing skills" }
        val skills = skillNode.map { node -> parseSkill(node, grades.keys) }
        require(skills.map { it.skillId }.toSet() == EXPECTED_SKILL_IDS) { "skill content must define exactly six skills" }
        require(skills.size == EXPECTED_SKILL_IDS.size) { "duplicate skill id" }
        require(skills.all { it.active == EXPECTED_ACTIVE_BY_ID.getValue(it.skillId) }) { "skill active classification must match the approved catalog" }
        require(maxGrade <= SkillGrade.EPIC) { "MVP max grade cannot exceed EPIC" }
        return SkillContent("applied", contentVersion, maxGrade, minLevel, maxLevel, successRates, grades, skills)
    }

    private fun parseGradeContent(node: JsonNode, grade: SkillGrade): SkillGradeContent {
        require(node.isObject) { "missing grade content: $grade" }
        requireKeys(node, "enhancementRiceCoefficient", "promotionRiceCost", "bookRequirements")
        val coefficient = requiredPositiveLong(node, "enhancementRiceCoefficient")
        val promotion = requiredNonNegativeLong(node, "promotionRiceCost")
        if (grade == SkillGrade.NORMAL) require(promotion == 0L) { "normal grade cannot have promotion cost" }
        else require(promotion > 0L) { "missing promotion cost: $grade" }
        val booksNode = node.path("bookRequirements")
        require(booksNode.isObject) { "missing book requirements: $grade" }
        val books = booksNode.properties().asSequence().associate { (key, value) ->
            val bookGrade = parseGrade(key)
            require(value.isInt && value.asInt() > 0) { "invalid book requirement: $grade/$key" }
            bookGrade to value.asInt()
        }
        require(books.keys == SkillGrade.entries.take(grade.ordinal + 1).toSet()) { "invalid book requirements: $grade" }
        return SkillGradeContent(coefficient, promotion, books)
    }

    private fun parseSkill(node: JsonNode, grades: Set<SkillGrade>): SkillContentSkill {
        require(node.isObject) { "invalid skill" }
        requireKeys(node, "skillId", "name", "active", "effects")
        val id = requiredText(node, "skillId")
        val name = requiredText(node, "name")
        require(node.path("active").isBoolean) { "invalid active flag: $id" }
        val effectNode = node.path("effects")
        require(effectNode.isObject && effectNode.fieldNames().asSequence().toSet() == grades.map { it.name }.toSet()) { "missing skill effects: $id" }
        val effects = grades.associateWith { grade ->
            val effect = effectNode.path(grade.name)
            require(effect.isObject) { "missing skill effect: $id/$grade" }
            requireKeys(effect, "baseValue", "stepValue")
            val base = requiredPositiveInt(effect, "baseValue")
            val step = requiredPositiveInt(effect, "stepValue")
            SkillEffect(base, step)
        }
        return SkillContentSkill(id, name, node.path("active").asBoolean(), effects)
    }

    private fun requireKeys(node: JsonNode, vararg keys: String) {
        require(node.fieldNames().asSequence().toSet() == keys.toSet()) { "unexpected or missing skill fields" }
    }
    private fun requiredText(node: JsonNode, field: String): String {
        val value = node.path(field)
        require(value.isTextual && value.asText().isNotBlank()) { "missing skill field: $field" }
        return value.asText()
    }
    private fun requiredPositiveLong(node: JsonNode, field: String): Long { val value = node.path(field); require(value.isIntegralNumber && value.asLong() > 0) { "invalid skill value: $field" }; return value.asLong() }
    private fun requiredNonNegativeLong(node: JsonNode, field: String): Long { val value = node.path(field); require(value.isIntegralNumber && value.asLong() >= 0) { "invalid skill value: $field" }; return value.asLong() }
    private fun requiredPositiveInt(node: JsonNode, field: String): Int { val value = node.path(field); require(value.isInt && value.asInt() > 0) { "invalid skill value: $field" }; return value.asInt() }
    private fun parseGrade(value: String): SkillGrade = runCatching { SkillGrade.valueOf(value) }.getOrElse { throw IllegalArgumentException("invalid skill grade: $value") }

    companion object {
        fun load(mapper: ObjectMapper, input: InputStream): SkillContent = SkillContentLoader(mapper).load(input)
        val EXPECTED_SKILL_IDS = setOf("active_heavy", "active_dot", "active_haste", "active_basic_amp", "passive_critical", "passive_all_damage")
        val EXPECTED_ACTIVE_BY_ID = mapOf(
            "active_heavy" to true, "active_dot" to true, "active_haste" to true, "active_basic_amp" to true,
            "passive_critical" to false, "passive_all_damage" to false,
        )
    }
}

/** Pure calculations. It reads no wallet, inventory, account, or persistence state. */
class SkillRules(private val content: SkillContent) {
    private val byId = content.skills.associateBy { it.skillId }
    val definitions: List<SkillDefinition> get() = content.skills.map { SkillDefinition(it.skillId, it.name, it.active) }

    fun requireDefinition(skillId: String): SkillDefinition = byId[skillId]?.let { SkillDefinition(it.skillId, it.name, it.active) }
        ?: throw IllegalArgumentException("SKILL_NOT_FOUND")
    fun requiredBooks(grade: SkillGrade): Map<SkillGrade, Int> = bookRequirements(grade)
    fun effect(skillId: String, grade: SkillGrade, level: Int): Int = effectValue(skillId, grade, level)
    fun effectDefinition(skillId: String, grade: SkillGrade): SkillEffect = byId[skillId]?.effects?.get(grade)
        ?: throw IllegalArgumentException("SKILL_NOT_FOUND")
    fun isMvpGradeAccessible(grade: SkillGrade): Boolean = isGradeAccessible(grade)
    fun bookItemId(skillId: String, grade: SkillGrade = SkillGrade.NORMAL): String { requireDefinition(skillId); return "skillbook:$skillId:${grade.name.lowercase()}" }
    fun unlockRiceCost(): Long = 100
    fun enhancementRiceCost(grade: SkillGrade, currentLevel: Int): Long {
        require(currentLevel in content.minLevel until content.maxLevel) { "INVALID_SKILL_LEVEL" }
        return content.grades.getValue(grade).enhancementRiceCoefficient * currentLevel
    }
    fun riceCost(state: SkillState?): Long = if (state == null) unlockRiceCost() else enhancementRiceCost(state.grade, state.level)
    fun nextTargetLevel(state: SkillState?): Int = if (state == null) content.minLevel else state.level + 1
    fun bookRequirements(grade: SkillGrade): Map<SkillGrade, Int> = content.grades.getValue(grade).bookRequirements
    fun promotionRiceCost(targetGrade: SkillGrade): Long { require(targetGrade != SkillGrade.NORMAL) { "INVALID_PROMOTION_TARGET" }; return content.grades.getValue(targetGrade).promotionRiceCost }
    fun successBasisPoints(targetLevel: Int, failureBonusBasisPoints: Int = 0): Int {
        require(targetLevel in 2..content.maxLevel) { "INVALID_SKILL_LEVEL" }
        require(failureBonusBasisPoints >= 0) { "INVALID_FAILURE_BONUS" }
        return (content.successRatesBasisPoints.getValue(targetLevel) + failureBonusBasisPoints).coerceAtMost(10_000)
    }
    fun successBasisPoints(state: SkillState?): Int = if (state == null) 10_000 else successBasisPoints(state.level + 1, state.failureBonusBasisPoints)
    fun effectValue(skillId: String, grade: SkillGrade, level: Int): Int {
        require(level in content.minLevel..content.maxLevel) { "INVALID_SKILL_LEVEL" }
        val effect = byId[skillId]?.effects?.get(grade) ?: throw IllegalArgumentException("SKILL_NOT_FOUND")
        return effect.baseValue + effect.stepValue * (level - content.minLevel)
    }
    fun promotionTarget(currentGrade: SkillGrade): SkillGrade? = SkillGrade.entries.getOrNull(currentGrade.ordinal + 1)
    fun isGradeAccessible(grade: SkillGrade): Boolean = grade.ordinal <= content.mvpMaxGrade.ordinal
    fun canPromote(currentGrade: SkillGrade, currentLevel: Int): Boolean = currentLevel == content.maxLevel && promotionTarget(currentGrade)?.let(::isGradeAccessible) == true
    fun canEquip(skillId: String): Boolean = requireDefinition(skillId).active

    fun effectText(state: SkillState?): String {
        if (state == null) return "미해금"
        val value = effectValue(state.skillId, state.grade, state.level)
        return when (state.skillId) {
            "active_heavy" -> "공격력 ${value}%"
            "active_dot" -> "5초 총 ${value}%"
            "active_haste" -> "5초 공속 +${value}%"
            "active_basic_amp" -> "5초 기본공격 +${value}%"
            "passive_critical" -> "치명타 +${value}%p"
            "passive_all_damage" -> "모든 피해 +${value}%"
            else -> ""
        }
    }

    /**
     * 장착한 액티브만 싸운다. 뺀 스킬의 효과가 그대로 남아 있으면 빼는 일에 뜻이 없고,
     * 전투력 표시도 실제와 어긋난다. 패시브는 장착 칸이 없으므로 해금만 되면 늘 적용된다.
     */
    fun profile(states: Collection<SkillState>, loadout: List<String>): SkillProfile {
        val bySkill = states.associateBy { it.skillId }
        val equipped = loadout.toSet()
        fun value(id: String): Int {
            val state = bySkill[id] ?: return 0
            if (requireDefinition(id).active && id !in equipped) return 0
            return effectValue(id, state.grade, state.level)
        }
        return SkillProfile(
            heavyBasisPoints = value("active_heavy") * 100,
            dotTotalBasisPoints = value("active_dot") * 100,
            hasteBasisPoints = value("active_haste") * 100,
            basicAmplificationBasisPoints = value("active_basic_amp") * 100,
            criticalChanceBasisPoints = value("passive_critical") * 100,
            allDamageBasisPoints = value("passive_all_damage") * 100,
            /* 다 빼 두었으면 아무 스킬도 쓰지 않는다. 예전에는 기본 네 개로 되돌려 놓아,
               장착을 모두 해제해도 스킬이 그대로 나갔다. */
            activeOrder = loadout,
        )
    }

    companion object {
        private val default by lazy { SkillRules(SkillContentLoader(ObjectMapper().findAndRegisterModules()).load()) }
        val definitions: List<SkillDefinition> get() = default.content.skills.map { SkillDefinition(it.skillId, it.name, it.active) }
        fun requireDefinition(skillId: String) = default.requireDefinition(skillId)
        fun bookItemId(skillId: String, grade: SkillGrade = SkillGrade.NORMAL) = default.bookItemId(skillId, grade)
        fun riceCost(state: SkillState?) = default.riceCost(state)
        fun nextTargetLevel(state: SkillState?) = default.nextTargetLevel(state)
        fun successBasisPoints(state: SkillState?) = default.successBasisPoints(state)
        fun canEquip(skillId: String) = default.canEquip(skillId)
        fun effectText(state: SkillState?) = default.effectText(state)
        fun profile(states: Collection<SkillState>, loadout: List<String>) = default.profile(states, loadout)
    }
}
