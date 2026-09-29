package com.hanjjak.gameapi.ranking

import com.hanjjak.account.domain.MaterialType
import com.hanjjak.gameapi.character.CharacterStatsService
import com.hanjjak.sim.FighterStats
import com.hanjjak.sim.SkillProfile
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.stereotype.Service
import org.springframework.transaction.annotation.Isolation
import org.springframework.transaction.annotation.Transactional
import java.math.BigDecimal
import java.math.RoundingMode
import java.time.Instant
import kotlin.math.exp
import java.util.UUID

const val COMBAT_POWER_FORMULA_VERSION = "combat-power-v2"

data class CombatPowerRankingEntry(
    val rank: Int,
    val overallRank: Int,
    val nickname: String,
    val level: Int,
    val materialType: MaterialType,
    val displayName: String,
    val combatPower: Long,
    val appearance: RankingAppearance,
    val updatedAt: Instant,
)

data class RankingAppearance(
    val head: String?,
    val top: String?,
    val bottom: String?,
    val gloves: String?,
    val shoes: String?,
    val cape: String?,
)

data class MaterialRanking(
    val materialType: MaterialType,
    val displayName: String,
    val entries: List<CombatPowerRankingEntry>,
)

data class CombatPowerRankingView(
    val formulaVersion: String,
    val generatedAt: Instant,
    val sourceStateVersion: Long,
    val overallTop: List<CombatPowerRankingEntry>,
    val specializations: List<MaterialRanking>,
    val myEntry: CombatPowerRankingEntry?,
)

object CombatPowerFormula {
    private const val STANDARD_DEFENSE = 100
    private const val WINDOW_TICKS = 600
    private const val COOLDOWN_TICKS = 100
    private const val BASE_BUFF_TICKS = 50
    private const val BASIS_POINTS = 10_000.0
    private val BASELINE_DPS = standardDps(FighterStats(40, 400, 20), SkillProfile())

    fun calculate(fighter: FighterStats, skills: SkillProfile = SkillProfile()): Long {
        val hpRatio = fighter.maxHp / 400.0
        val offenseRatio = standardDps(fighter, skills) / BASELINE_DPS
        return BigDecimal.valueOf(1_000 * (hpRatio + 2 * offenseRatio))
            .setScale(0, RoundingMode.HALF_UP)
            .longValueExact()
    }

    private fun standardDps(fighter: FighterStats, skills: SkillProfile): Double {
        var totalDamage = 0.0
        var nextAction = 0.0
        var hasteUntil = 0
        var amplificationUntil = 0
        val cooldownUntil = mutableMapOf<String, Int>()
        val dotTicks = IntArray(WINDOW_TICKS + BASE_BUFF_TICKS + 1)

        for (tick in 0 until WINDOW_TICKS) {
            if (dotTicks[tick] > 0) {
                totalDamage += dotTicks[tick] * expectedHit(fighter, skills, skills.dotTotalBasisPoints / 5.0, canCritical = false)
            }
            if (tick + 1e-9 < nextAction) continue

            val selected = skills.activeOrder.firstOrNull { skillId ->
                tick >= (cooldownUntil[skillId] ?: 0) && when (skillId) {
                    "active_haste" -> skills.hasteBasisPoints > 0
                    "active_basic_amp" -> skills.basicAmplificationBasisPoints > 0
                    "active_heavy" -> skills.heavyBasisPoints > 0
                    "active_dot" -> skills.dotTotalBasisPoints > 0
                    else -> false
                }
            }
            when (selected) {
                "active_haste" -> {
                    cooldownUntil[selected] = tick + COOLDOWN_TICKS
                    hasteUntil = tick + BASE_BUFF_TICKS + skills.buffDurationBonusTicks
                }
                "active_basic_amp" -> {
                    cooldownUntil[selected] = tick + COOLDOWN_TICKS
                    amplificationUntil = tick + BASE_BUFF_TICKS + skills.buffDurationBonusTicks
                }
                "active_heavy" -> {
                    cooldownUntil[selected] = tick + COOLDOWN_TICKS
                    totalDamage += expectedHit(fighter, skills, skills.heavyBasisPoints.toDouble(), canCritical = true)
                }
                "active_dot" -> {
                    cooldownUntil[selected] = tick + COOLDOWN_TICKS
                    repeat(5) { index -> dotTicks[tick + (index + 1) * 10]++ }
                }
                else -> totalDamage += expectedBasicHit(fighter, skills, tick, amplificationUntil)
            }

            val haste = (skills.permanentHasteBasisPoints + if (tick < hasteUntil) skills.hasteBasisPoints else 0)
                .coerceAtMost(10_000)
            nextAction += (4.0 / (1.0 + haste / BASIS_POINTS)).coerceAtLeast(2.0)
        }
        return totalDamage / (WINDOW_TICKS / 10.0)
    }

    private fun expectedBasicHit(fighter: FighterStats, skills: SkillProfile, tick: Int, amplificationUntil: Int): Double {
        val basisPoints = BASIS_POINTS + skills.permanentBasicAmplificationBasisPoints +
            if (tick < amplificationUntil) skills.basicAmplificationBasisPoints else 0
        return expectedHit(fighter, skills, basisPoints, canCritical = true)
    }

    private fun expectedHit(fighter: FighterStats, skills: SkillProfile, skillBasisPoints: Double, canCritical: Boolean): Double {
        val defenseMultiplier = 0.5 + 1.0 / (1.0 + exp(-0.015 * (fighter.penetration - STANDARD_DEFENSE)))
        val allDamageMultiplier = 1.0 + skills.allDamageBasisPoints / BASIS_POINTS
        val criticalMultiplier = if (canCritical) 1.0 + 0.5 * skills.criticalChanceBasisPoints / BASIS_POINTS else 1.0
        return fighter.attack * defenseMultiplier * skillBasisPoints / BASIS_POINTS * allDamageMultiplier * criticalMultiplier
    }
}

@Service
class CombatPowerRankingService(
    private val jdbc: JdbcClient,
    private val characterStats: CharacterStatsService,
) {
    private data class RankingCandidate(
        val characterId: UUID,
        val accountId: UUID,
        val nickname: String,
        val materialType: MaterialType,
        val combatPower: Long,
        val sourceStateVersion: Long,
        val updatedAt: Instant,
    )

    private data class RankedEntry(
        val entry: CombatPowerRankingEntry,
        val inOverallTop: Boolean,
        val inMaterialTop: Boolean,
        val isMine: Boolean,
    )

    /**
     * MVP refreshes the small read model before a read. The ranking query stays
     * bounded and deterministic while avoiding a Kafka dependency for the demo.
     */
    @Transactional(isolation = Isolation.REPEATABLE_READ)
    fun snapshot(accountId: UUID, limit: Int): CombatPowerRankingView {
        require(limit in 1..50) { "INVALID_LIMIT" }
        val generatedAt = Instant.now()
        refreshEntries()
        val rankedEntries = jdbc.sql(
            """
            with ranked as (
              select r.character_id, r.account_id, r.nickname, c.level, r.material_type, r.combat_power, r.updated_at,
                     row_number() over (partition by material_type order by combat_power desc, updated_at asc, character_id asc) as material_rank,
                     row_number() over (order by combat_power desc, updated_at asc, character_id asc) as overall_rank
              from ranking_entry r
              join character c on c.id=r.character_id
            )
            select ranked.character_id, ranked.nickname, ranked.level, ranked.material_type, ranked.combat_power, ranked.updated_at, material_rank, overall_rank,
                   head.cosmetic_id as head_cosmetic_id,
                   top_slot.cosmetic_id as top_cosmetic_id,
                   bottom_slot.cosmetic_id as bottom_cosmetic_id,
                   gloves.cosmetic_id as gloves_cosmetic_id,
                   shoes.cosmetic_id as shoes_cosmetic_id,
                   cape.cosmetic_id as cape_cosmetic_id,
                   overall_rank <= 10 as in_overall_top,
                   material_rank <= :limit as in_material_top,
                   character_id=(select id from character where account_id=:account) as is_mine
            from ranked
            left join cosmetic_equipment head on head.account_id=ranked.account_id and head.slot='HEAD'
            left join cosmetic_equipment top_slot on top_slot.account_id=ranked.account_id and top_slot.slot='TOP'
            left join cosmetic_equipment bottom_slot on bottom_slot.account_id=ranked.account_id and bottom_slot.slot='BOTTOM'
            left join cosmetic_equipment gloves on gloves.account_id=ranked.account_id and gloves.slot='GLOVES'
            left join cosmetic_equipment shoes on shoes.account_id=ranked.account_id and shoes.slot='SHOES'
            left join cosmetic_equipment cape on cape.account_id=ranked.account_id and cape.slot='CAPE'
            where overall_rank <= 10
               or material_rank <= :limit
               or character_id=(select id from character where account_id=:account)
            order by overall_rank
            """.trimIndent(),
        ).params(mapOf("account" to accountId, "limit" to limit)).query(::mapRankedEntry).list()
        val sourceStateVersion = jdbc.sql("select state_version from account where id=:account").param("account", accountId).query(Long::class.java).single()
        val overallTop = rankedEntries.filter(RankedEntry::inOverallTop).map(RankedEntry::entry)
        val rankings = MaterialType.entries.map { material ->
            val entries = rankedEntries.asSequence()
                .filter { it.inMaterialTop && it.entry.materialType == material }
                .map(RankedEntry::entry)
                .sortedBy(CombatPowerRankingEntry::rank)
                .toList()
            MaterialRanking(material, "${material.displayName} 전문", entries)
        }
        val my = rankedEntries.singleOrNull(RankedEntry::isMine)?.entry
        return CombatPowerRankingView(COMBAT_POWER_FORMULA_VERSION, generatedAt, sourceStateVersion, overallTop, rankings, my)
    }

    private fun refreshEntries() {
        val candidates = jdbc.sql(
            """
            select c.id as character_id, c.account_id, c.nickname, mp.material_type
            from character c
            join account a on a.id = c.account_id and a.deleted_at is null
            join material_preference mp on mp.account_id=c.account_id
            order by c.id
            """.trimIndent(),
        ).query { row, _ ->
            val characterId = row.getObject("character_id", UUID::class.java)
            val accountId = row.getObject("account_id", UUID::class.java)
            val calculation = characterStats.snapshot(accountId)
            RankingCandidate(
                characterId = characterId,
                accountId = accountId,
                nickname = row.getString("nickname"),
                materialType = MaterialType.valueOf(row.getString("material_type")),
                combatPower = CombatPowerFormula.calculate(calculation.calculation.fighter, calculation.calculation.skills),
                sourceStateVersion = calculation.stateVersion,
                updatedAt = Instant.now(),
            )
        }.list()
        candidates.forEach { candidate ->
            jdbc.sql(
                """
                insert into ranking_entry(character_id, account_id, nickname, material_type, combat_power, formula_version, source_state_version, updated_at)
                values (:character, :account, :nickname, :material, :power, :formula, :state, :updated)
                on conflict (character_id) do update set
                  account_id=excluded.account_id,
                  nickname=excluded.nickname,
                  material_type=excluded.material_type,
                  combat_power=excluded.combat_power,
                  formula_version=excluded.formula_version,
                  source_state_version=excluded.source_state_version,
                  updated_at=excluded.updated_at
                where ranking_entry.nickname is distinct from excluded.nickname
                   or ranking_entry.material_type is distinct from excluded.material_type
                   or ranking_entry.combat_power is distinct from excluded.combat_power
                   or ranking_entry.formula_version is distinct from excluded.formula_version
                   or ranking_entry.source_state_version is distinct from excluded.source_state_version
                """.trimIndent(),
            ).params(
                mapOf(
                    "character" to candidate.characterId,
                    "account" to candidate.accountId,
                    "nickname" to candidate.nickname,
                    "material" to candidate.materialType.name,
                    "power" to candidate.combatPower,
                    "formula" to COMBAT_POWER_FORMULA_VERSION,
                    "state" to candidate.sourceStateVersion,
                    "updated" to java.sql.Timestamp.from(candidate.updatedAt),
                ),
            ).update()
        }
    }

    private fun mapRankedEntry(row: java.sql.ResultSet, ignored: Int): RankedEntry {
        val material = MaterialType.valueOf(row.getString("material_type"))
        return RankedEntry(
            entry = CombatPowerRankingEntry(
                row.getInt("material_rank"),
                row.getInt("overall_rank"),
                row.getString("nickname"),
                row.getInt("level"),
                material,
                "${material.displayName} 전문",
                row.getLong("combat_power"),
                RankingAppearance(
                    head = row.getString("head_cosmetic_id"),
                    top = row.getString("top_cosmetic_id"),
                    bottom = row.getString("bottom_cosmetic_id"),
                    gloves = row.getString("gloves_cosmetic_id"),
                    shoes = row.getString("shoes_cosmetic_id"),
                    cape = row.getString("cape_cosmetic_id"),
                ),
                row.getTimestamp("updated_at").toInstant(),
            ),
            inOverallTop = row.getBoolean("in_overall_top"),
            inMaterialTop = row.getBoolean("in_material_top"),
            isMine = row.getBoolean("is_mine"),
        )
    }
}
