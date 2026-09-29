package com.hanjjak.gameapi.character

import com.hanjjak.account.application.AccountRepository
import com.hanjjak.cosmetics.application.CosmeticsService
import com.hanjjak.equipment.application.EquipmentRepository
import com.hanjjak.gems.application.GemService
import com.hanjjak.gems.domain.GemPreset
import com.hanjjak.gameapi.ranking.CombatPowerFormula
import com.hanjjak.skills.application.SkillProfileProvider
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.stereotype.Service
import org.springframework.transaction.annotation.Isolation
import org.springframework.transaction.annotation.Transactional
import java.util.UUID

data class CharacterStatsView(val nickname: String, val level: Int, val experience: Long, val combatPower: Long, val cosmeticsUnlocked: Boolean, val contentVersion: String, val stats: List<CharacterStat>, val notices: List<String>)
data class CharacterSnapshot(
    val stateVersion: Long,
    val view: CharacterStatsView,
    val calculation: CharacterCalculation,
    val unlockedSkillIds: List<String>,
    val activeSkillLoadout: List<String>,
)

@Service
class CharacterStatsService(
    private val accounts: AccountRepository,
    private val equipment: EquipmentRepository,
    private val gems: GemService,
    private val skills: SkillProfileProvider,
    private val cosmetics: CosmeticsService,
    private val jdbc: JdbcClient,
) {
    @Transactional(readOnly = true, isolation = Isolation.REPEATABLE_READ)
    fun snapshot(accountId: UUID): CharacterSnapshot = snapshot(accountId, GemPreset.MAIN)

    @Transactional(readOnly = true, isolation = Isolation.REPEATABLE_READ)
    fun snapshot(accountId: UUID, preset: GemPreset): CharacterSnapshot {
        val account = accounts.findById(accountId) ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")
        val character = accounts.findCharacter(accountId) ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")
        val unlocked = jdbc.sql("select cosmetics_unlocked from character where account_id=:account").param("account", accountId).query(Boolean::class.java).single()
        val content = cosmetics.content()
        val collection = if (unlocked) cosmetics.snapshotForCombat(accountId) else null
        val sets = content.sets.mapNotNull { set ->
            val star = collection?.setStars?.get(set.id) ?: 0
            val effects = set.effects[star] ?: return@mapNotNull null
            val fallback = set.id.substringAfterLast('-')
            CharacterSetBonus(set.id, "${set.displayName ?: "세트 #$fallback"} ${star}성", effects.map { CharacterSetEffect(it.statId, it.value, it.unit.name) })
        }
        val (equipmentStates, equipmentNotice) = equipmentStates(accountId)
        val equipmentBySlot = equipmentStates.associateBy { it.slot }
        val skillSnapshot = skills.combatSnapshot(accountId)
        val result = CharacterStatsCalculator.calculate(character.level, equipmentBySlot, gems.equipped(accountId, preset), skillSnapshot.profile, sets)
        return CharacterSnapshot(
            account.stateVersion,
            CharacterStatsView(character.nickname, character.level, character.experience, CombatPowerFormula.calculate(result.fighter, result.skills), unlocked, content.version, result.stats,
                listOfNotNull(
                    equipmentNotice,
                    "일시적인 전투 버프는 제외되며, 준비 중인 치장 이름과 이미지는 번호로 표시됩니다.",
                )),
            result,
            skillSnapshot.unlockedSkillIds,
            skillSnapshot.activeLoadout,
        )
    }

    /**
     * Some existing development databases predate the permanent-equipment migration. PostgreSQL
     * aborts a transaction after an undefined-table error, so probe the schema before querying the
     * repository and keep the otherwise useful base snapshot available during migration repair.
     */
    private fun equipmentStates(accountId: UUID): Pair<List<com.hanjjak.equipment.domain.EquipmentSlotState>, String?> {
        val equipmentTableExists = jdbc.sql("select to_regclass('equipment_slot_state') is not null")
            .query(Boolean::class.java)
            .single()
        if (!equipmentTableExists) {
            return emptyList<com.hanjjak.equipment.domain.EquipmentSlotState>() to
                "장비 성장 데이터가 준비되지 않아 장비 추가 능력치를 제외해 표시합니다."
        }
        return equipment.slotStates(accountId) to null
    }
}
