package com.hanjjak.gameapi.character

import com.fasterxml.jackson.annotation.JsonInclude
import com.hanjjak.equipment.domain.*
import com.hanjjak.gems.domain.*
import com.hanjjak.sim.FighterStats
import com.hanjjak.sim.SkillProfile
import kotlin.math.floor

@JsonInclude(JsonInclude.Include.ALWAYS)
data class StatSource(val sourceId: String, val label: String, val category: String, val value: Double, val unit: String, val applied: Boolean = true, val reason: String? = null)
data class CharacterStat(val statId: String, val label: String, val unit: String, val total: Int, val base: Int, val additional: Int, val sources: List<StatSource>, val calculation: String)
data class CharacterSetEffect(val statId: String, val value: Int, val unit: String)
data class CharacterSetBonus(val id: String, val name: String, val effects: List<CharacterSetEffect>)
data class CharacterCalculation(val stats: List<CharacterStat>, val fighter: FighterStats, val skills: SkillProfile)

/** The same permanent snapshot feeds combat and the character sheet. Temporary buffs stay in SkillProfile. */
object CharacterStatsCalculator {
    fun calculate(level: Int, equipment: Map<EquipmentSlot, EquipmentSlotState>, gems: List<GemInstance>, skill: SkillProfile, sets: List<CharacterSetBonus>): CharacterCalculation {
        require(level in 1..500)
        val rows = mutableListOf<CharacterStat>()
        fun source(id: String, label: String, category: String, value: Number, unit: String = "POINTS", applied: Boolean = true, reason: String? = null) =
            StatSource(id, label, category, value.toDouble(), unit, applied, reason)
        fun equipmentSource(slot: EquipmentSlot): StatSource {
            val item = equipment[slot]
            val summary = EquipmentRules.statSummary(slot, item)
            val amount = when (slot) {
                EquipmentSlot.WEAPON, EquipmentSlot.GLOVES -> summary.attack
                EquipmentSlot.ARMOR, EquipmentSlot.HELMET -> summary.maxHp
                EquipmentSlot.CAPE, EquipmentSlot.SHOES -> summary.penetration
            }
            return source(
                "equipment.${slot.name}",
                "${slot.label} ${item?.let { "${it.grade.label} +${it.enhancementLevel}" } ?: "기본값"}",
                "EQUIPMENT",
                amount,
            )
        }
        fun gemSources(option: GemOption): List<StatSource> {
            val candidates = gems.filter { it.option == option }
            val sumAll = option == GemOption.FLAT_ATTACK || option == GemOption.FLAT_HP
            val winner = candidates.maxByOrNull { it.value }?.gemId
            return candidates.map { gem ->
                val applied = sumAll || winner == gem.gemId
                source(gem.gemId.toString(), "${gem.level}레벨 ${option.label} 보석", "GEM", gem.value,
                    if (option in setOf(GemOption.ATTACK_PERCENT, GemOption.CRITICAL_CHANCE, GemOption.HASTE)) "BASIS_POINTS" else "POINTS",
                    applied, if (applied) null else "같은 특수 옵션은 가장 높은 하나만 적용")
            }
        }
        fun setSources(key: String, unit: String) = sets.flatMap { set -> set.effects.filter { it.statId == key && it.unit == unit && it.value != 0 }.map { source(set.id, set.name, "COSMETIC", it.value, unit) } }
        fun List<StatSource>.sumApplied() = filter { it.applied }.sumOf { it.value }
        fun mainStat(id: String, label: String, initial: Int, perLevel: Int, slots: List<StatSource>, gemOption: GemOption, percentKey: String) {
            val basics = listOf(source("character.initial", "초기 능력치", "BASE", initial), source("character.level", "레벨 $level 성장", "BASE", perLevel * (level - 1))) + slots
            val base = rounded(basics.sumApplied())
            val flat = gemSources(gemOption)
            val gemPercent = if (id == "attack") gemSources(GemOption.ATTACK_PERCENT) else emptyList()
            val setPercent = setSources(percentKey, "BASIS_POINTS")
            val beforePercent = base + flat.sumApplied()
            val afterGem = beforePercent * (1 + gemPercent.sumApplied() / 10000)
            val total = rounded(afterGem * (1 + setPercent.sumApplied() / 10000))
            rows += CharacterStat(id, label, "POINTS", total, base, total - base, basics + flat + gemPercent + setPercent,
                "기본 $base + 고정 ${flat.sumApplied()} → 보석 ${gemPercent.sumApplied() / 100}% → 세트 ${setPercent.sumApplied() / 100}% → 최종 반올림 $total")
        }
        mainStat("attack", "공격력", 40, 2, listOf(equipmentSource(EquipmentSlot.WEAPON), equipmentSource(EquipmentSlot.GLOVES)), GemOption.FLAT_ATTACK, "attackPercent")
        mainStat("maxHp", "최대 HP", 400, 20, listOf(equipmentSource(EquipmentSlot.ARMOR), equipmentSource(EquipmentSlot.HELMET)), GemOption.FLAT_HP, "maxHpPercent")
        mainStat("penetration", "방어 관통", 20, 0, listOf(equipmentSource(EquipmentSlot.CAPE), equipmentSource(EquipmentSlot.SHOES)), GemOption.FLAT_PENETRATION, "defensePenetrationPercent")
        fun extra(id: String, label: String, sources: List<StatSource>, cap: Int? = null, unit: String = "BASIS_POINTS"): Int {
            val raw = rounded(sources.sumApplied())
            val total = cap?.let { raw.coerceAtMost(it) } ?: raw
            rows += CharacterStat(id, label, unit, total, 0, total, sources, if (cap != null) "상시 효과 합계 $raw, 상한 $cap 적용 결과 $total" else "상시 효과 합계 $total")
            return total
        }
        val critical = extra("criticalChance", "치명타 확률", listOf(source("passive_critical", "치명타 패시브", "SKILL", skill.criticalChanceBasisPoints, "BASIS_POINTS")) + gemSources(GemOption.CRITICAL_CHANCE) + setSources("criticalChancePoint", "BASIS_POINTS"), 7500)
        val haste = extra("attackSpeed", "기본공격 속도 증가", gemSources(GemOption.HASTE) + setSources("attackSpeedPercent", "BASIS_POINTS"), 10000)
        val basicDamage = extra("basicAttackDamage", "기본공격 피해 증가", setSources("basicAttackDamagePercent", "BASIS_POINTS"))
        extra("allDamage", "모든 피해 증가", listOf(source("passive_all_damage", "모든 피해 패시브", "SKILL", skill.allDamageBasisPoints, "BASIS_POINTS")))
        val duration = extra("buffDuration", "버프 지속시간 증가", setSources("buffDurationSeconds", "SECONDS"), unit = "SECONDS")
        return CharacterCalculation(rows, FighterStats(rows[0].total, rows[1].total, rows[2].total), skill.copy(
            criticalChanceBasisPoints = critical, permanentHasteBasisPoints = haste,
            permanentBasicAmplificationBasisPoints = basicDamage, buffDurationBonusTicks = duration * 10,
        ))
    }
    private fun rounded(value: Double): Int = floor(value + 0.5).toInt()
}
