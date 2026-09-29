package com.hanjjak.skills.application

import com.fasterxml.jackson.databind.JsonNode
import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.inventory.application.InventoryReservationService
import com.hanjjak.inventory.application.ItemCatalog
import com.hanjjak.sim.SkillProfile
import com.hanjjak.skills.domain.*
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.transaction.annotation.Transactional
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.security.SecureRandom
import java.util.UUID

data class SkillCombatSnapshot(val profile: SkillProfile, val unlockedSkillIds: List<String>, val activeLoadout: List<String>)

interface SkillProfileProvider {
    fun profile(accountId: UUID): SkillProfile
    fun combatSnapshot(accountId: UUID): SkillCombatSnapshot
}

fun interface SkillRollSource { fun nextBasisPoint(): Int }

class SecureSkillRollSource(private val random: SecureRandom = SecureRandom()) : SkillRollSource {
    override fun nextBasisPoint(): Int = random.nextInt(10_000)
}

internal fun validatedRoll(source: SkillRollSource): Int = source.nextBasisPoint().also { require(it in 0..9_999) { "SKILL_ROLL_OUT_OF_RANGE" } }

open class SkillService(
    private val jdbc: JdbcClient,
    private val mapper: ObjectMapper,
    private val inventory: InventoryReservationService,
    private val catalog: ItemCatalog,
    private val rules: SkillRules,
    private val rollSource: SkillRollSource,
) : SkillProfileProvider {
    private val legacyReader = LegacySkillResultReader(mapper, { skillId -> runCatching { rules.requireDefinition(skillId) }.getOrNull() }, catalog)
    private data class StoredCommand(val fingerprint: String, val resultJson: String, val resultVersion: Short)
    @Transactional(readOnly = true)
    open fun state(accountId: UUID): SkillScreenState = buildState(accountId)

    @Transactional
    open fun enhance(accountId: UUID, idempotencyKey: UUID, skillId: String): SkillEnhanceResult =
        commandEnhance(accountId, idempotencyKey, "skill\u0000enhance\u0000$skillId") {
            val definition = rules.requireDefinition(skillId)
            val current = stateRow(accountId, skillId, lock = true)
            val action = actionFor(definition, current, bookBalances(accountId), riceBalance(accountId))
            require(action.kind == SkillActionKind.UNLOCK || action.kind == SkillActionKind.ENHANCE) {
                when (action.kind) {
                    SkillActionKind.PROMOTE -> "SKILL_PROMOTION_REQUIRED"
                    SkillActionKind.COMPLETE -> "SKILL_MAX_LEVEL"
                    SkillActionKind.LOCKED -> action.disabledReason ?: "SKILL_ACTION_LOCKED"
                    else -> "SKILL_ACTION_NOT_EXECUTABLE"
                }
            }
            require(action.executable) { action.disabledReason ?: "SKILL_ACTION_NOT_EXECUTABLE" }
            consume(accountId, action.books, action.riceCost)
            val success = current == null || validatedRoll(rollSource) < action.successBasisPoints
            val updated = if (success) {
                if (current == null) SkillState(skillId, SkillGrade.NORMAL, 1, 0)
                else current.copy(level = checkNotNull(action.targetLevel), failureBonusBasisPoints = 0)
            } else {
                current!!.copy(failureBonusBasisPoints = (current.failureBonusBasisPoints + 500).coerceAtMost(10_000))
            }
            upsert(accountId, updated)
            if (success && definition.active && current == null) appendLoadoutIfNeeded(accountId, skillId)
            incrementStateVersion(accountId)
            val nextState = buildState(accountId)
            SkillEnhanceResult(nextState.skills.first { it.skillId == skillId }, success, nextState)
        }
    @Transactional
    open fun unlockFromFirstClear(accountId: UUID, skillId: String, bumpStateVersion: Boolean = true): Boolean {
        val definition = rules.requireDefinition(skillId)
        jdbc.sql("select id from account where id=:account for update").param("account", accountId).query(UUID::class.java).single()
        val inserted = jdbc.sql("insert into skill_state(account_id,skill_id,grade,level,failure_bonus_basis_points) values (:account,:skill,'NORMAL',1,0) on conflict(account_id,skill_id) do nothing")
            .params(mapOf("account" to accountId, "skill" to skillId)).update() == 1
        if (inserted && definition.active) appendLoadoutIfNeeded(accountId, skillId)
        if (inserted && bumpStateVersion) incrementStateVersion(accountId)
        return inserted
    }

    @Transactional
    open fun promote(accountId: UUID, idempotencyKey: UUID, skillId: String): SkillEnhanceResult =
        commandEnhance(accountId, idempotencyKey, "skill\u0000promote\u0000$skillId") {
            val definition = rules.requireDefinition(skillId)
            val current = stateRow(accountId, skillId, lock = true) ?: throw IllegalArgumentException("SKILL_LOCKED")
            val action = actionFor(definition, current, bookBalances(accountId), riceBalance(accountId))
            require(action.kind == SkillActionKind.PROMOTE) {
                when (action.kind) {
                    SkillActionKind.LOCKED -> action.disabledReason ?: "SKILL_GRADE_LOCKED"
                    SkillActionKind.COMPLETE -> "SKILL_MAX_GRADE"
                    else -> "SKILL_PROMOTION_REQUIRED"
                }
            }
            require(action.executable) { action.disabledReason ?: "SKILL_ACTION_NOT_EXECUTABLE" }
            consume(accountId, action.books, action.riceCost)
            val updated = current.copy(grade = checkNotNull(action.targetGrade), level = checkNotNull(action.targetLevel), failureBonusBasisPoints = 0)
            upsert(accountId, updated)
            incrementStateVersion(accountId)
            val nextState = buildState(accountId)
            SkillEnhanceResult(nextState.skills.first { it.skillId == skillId }, true, nextState)
        }

    @Transactional
    open fun updateLoadout(accountId: UUID, idempotencyKey: UUID, request: SkillLoadoutUpdateRequest): SkillScreenState =
        commandState(accountId, idempotencyKey, "skill\u0000loadout\u0000${request.skillIds.joinToString(",")}") {
            require(request.skillIds.size <= 4) { "SKILL_LOADOUT_TOO_LARGE" }
            require(request.skillIds.distinct().size == request.skillIds.size) { "SKILL_LOADOUT_DUPLICATE" }
            val states = skillStates(accountId).associateBy { it.skillId }
            request.skillIds.forEach { skillId ->
                require(rules.canEquip(skillId)) { "SKILL_NOT_ACTIVE" }
                require(states.containsKey(skillId)) { "SKILL_LOCKED" }
            }
            jdbc.sql("delete from skill_loadout where account_id=:account").param("account", accountId).update()
            request.skillIds.forEachIndexed { index, skillId ->
                jdbc.sql("insert into skill_loadout(account_id,slot_index,skill_id) values (:account,:slot,:skill)")
                    .params(mapOf("account" to accountId, "slot" to index + 1, "skill" to skillId)).update()
            }
            incrementStateVersion(accountId)
            buildState(accountId)
        }

    @Transactional(readOnly = true)
    override fun profile(accountId: UUID): SkillProfile = rules.profile(skillStates(accountId), loadout(accountId))
    @Transactional(readOnly = true)
    override fun combatSnapshot(accountId: UUID): SkillCombatSnapshot {
        val states = skillStates(accountId)
        val activeLoadout = loadout(accountId)
        return SkillCombatSnapshot(rules.profile(states, activeLoadout), states.map { it.skillId }.sorted(), activeLoadout)
    }

    private fun buildState(accountId: UUID): SkillScreenState {
        val states = skillStates(accountId).associateBy { it.skillId }
        val equipped = loadout(accountId)
        val books = bookBalances(accountId)
        val rice = riceBalance(accountId)
        val summaries = rules.definitions.map { definition ->
            val state = states[definition.skillId]
            SkillSummary(
                skillId = definition.skillId,
                name = definition.name,
                active = definition.active,
                unlocked = state != null,
                grade = state?.grade,
                gradeName = state?.grade?.label,
                level = state?.level ?: 0,
                equippedSlot = equipped.indexOf(definition.skillId).takeIf { it >= 0 }?.plus(1),
                effectText = rules.effectText(state),
                action = actionFor(definition, state, books, rice),
            )
        }
        return SkillScreenState(summaries, equipped, rice)
    }

    private fun actionFor(definition: SkillDefinition, state: SkillState?, books: Map<String, Long> = emptyMap(), rice: Long = 0): SkillActionSummary {
        val targetGrade: SkillGrade?
        val targetLevel: Int?
        val kind: SkillActionKind
        val requiredRice: Long
        val chance: Int
        when {
            state == null -> {
                kind = SkillActionKind.UNLOCK
                targetGrade = SkillGrade.NORMAL
                targetLevel = 1
                requiredRice = rules.unlockRiceCost()
                chance = 10_000
            }
            state.level < 10 -> {
                kind = SkillActionKind.ENHANCE
                targetGrade = state.grade
                targetLevel = state.level + 1
                requiredRice = rules.enhancementRiceCost(state.grade, state.level)
                chance = rules.successBasisPoints(targetLevel, state.failureBonusBasisPoints)
            }
            else -> {
                targetGrade = rules.promotionTarget(state.grade)
                targetLevel = if (targetGrade == null) null else 1
                if (targetGrade == null) {
                    kind = SkillActionKind.COMPLETE
                    requiredRice = 0
                    chance = 10_000
                } else if (!rules.isGradeAccessible(targetGrade)) {
                    kind = SkillActionKind.LOCKED
                    requiredRice = rules.promotionRiceCost(targetGrade)
                    chance = 10_000
                } else {
                    kind = SkillActionKind.PROMOTE
                    requiredRice = rules.promotionRiceCost(targetGrade)
                    chance = 10_000
                }
            }
        }
        val requiredBooks = if (kind == SkillActionKind.COMPLETE || kind == SkillActionKind.LOCKED) emptyMap() else rules.bookRequirements(targetGrade!!)
        val requirements = requiredBooks.map { (grade, quantity) ->
            val itemId = rules.bookItemId(definition.skillId, grade)
            SkillBookRequirement(itemId, catalog.require(itemId).displayName, quantity.toLong(), books[itemId] ?: 0)
        }
        val disabled = when {
            kind == SkillActionKind.COMPLETE -> null
            kind == SkillActionKind.LOCKED -> "SKILL_GRADE_LOCKED"
            requirements.any { it.availableQuantity < it.requiredQuantity } -> "INSUFFICIENT_SKILLBOOK"
            rice < requiredRice -> "INSUFFICIENT_RICE"
            else -> null
        }
        return SkillActionSummary(kind, targetGrade, targetLevel, requirements, requiredRice, chance, kind != SkillActionKind.COMPLETE && disabled == null, disabled)
    }

    private fun skillStates(accountId: UUID): List<SkillState> = jdbc.sql("select skill_id,grade,level,failure_bonus_basis_points from skill_state where account_id=:account")
        .param("account", accountId).query { row, _ -> SkillState(row.getString("skill_id"), SkillGrade.valueOf(row.getString("grade")), row.getInt("level"), row.getInt("failure_bonus_basis_points")) }.list()

    private fun stateRow(accountId: UUID, skillId: String, lock: Boolean): SkillState? = jdbc.sql("select skill_id,grade,level,failure_bonus_basis_points from skill_state where account_id=:account and skill_id=:skill${if (lock) " for update" else ""}")
        .params(mapOf("account" to accountId, "skill" to skillId)).query { row, _ -> SkillState(row.getString("skill_id"), SkillGrade.valueOf(row.getString("grade")), row.getInt("level"), row.getInt("failure_bonus_basis_points")) }.optional().orElse(null)

    private fun loadout(accountId: UUID): List<String> = jdbc.sql("select skill_id from skill_loadout where account_id=:account order by slot_index")
        .param("account", accountId).query(String::class.java).list()

    private fun bookBalances(accountId: UUID): Map<String, Long> = jdbc.sql("select item_id,quantity-reserved_quantity as available from inventory_stack where account_id=:account and item_id like 'skillbook:%'")
        .param("account", accountId).query { row, _ -> row.getString("item_id") to row.getLong("available") }.list().toMap()

    private fun riceBalance(accountId: UUID): Long = jdbc.sql("select balance from wallet_balance where account_id=:account").param("account", accountId).query(Long::class.java).single()

    private fun consume(accountId: UUID, books: List<SkillBookRequirement>, rice: Long) {
        books.forEach { requirement ->
            try {
                inventory.consumeAvailable(accountId, requirement.itemId, requirement.requiredQuantity)
            } catch (error: IllegalArgumentException) {
                if (error.message == "INSUFFICIENT_AVAILABLE_QUANTITY") throw IllegalArgumentException("INSUFFICIENT_SKILLBOOK", error)
                throw error
            }
        }
        require(jdbc.sql("update wallet_balance set balance=balance-:rice,updated_at=now() where account_id=:account and balance>=:rice")
            .params(mapOf("account" to accountId, "rice" to rice)).update() == 1) { "INSUFFICIENT_RICE" }
    }

    private fun upsert(accountId: UUID, state: SkillState) = jdbc.sql("insert into skill_state(account_id,skill_id,grade,level,failure_bonus_basis_points) values (:account,:skill,:grade,:level,:bonus) on conflict(account_id,skill_id) do update set grade=excluded.grade,level=excluded.level,failure_bonus_basis_points=excluded.failure_bonus_basis_points")
        .params(mapOf("account" to accountId, "skill" to state.skillId, "grade" to state.grade.name, "level" to state.level, "bonus" to state.failureBonusBasisPoints)).update()

    private fun appendLoadoutIfNeeded(accountId: UUID, skillId: String) {
        val current = loadout(accountId)
        if (skillId in current || current.size >= 4) return
        jdbc.sql("insert into skill_loadout(account_id,slot_index,skill_id) values (:account,:slot,:skill)")
            .params(mapOf("account" to accountId, "slot" to current.size + 1, "skill" to skillId)).update()
    }

    private fun incrementStateVersion(accountId: UUID): Long = jdbc.sql("update account set state_version=state_version+1 where id=:account returning state_version")
        .param("account", accountId).query(Long::class.java).single()
    private fun commandEnhance(accountId: UUID, idempotencyKey: UUID, source: String, action: () -> SkillEnhanceResult): SkillEnhanceResult = commandTyped(accountId, idempotencyKey, source, SkillEnhanceResult::class.java, action)
    private fun commandState(accountId: UUID, idempotencyKey: UUID, source: String, action: () -> SkillScreenState): SkillScreenState = commandTyped(accountId, idempotencyKey, source, SkillScreenState::class.java, action)

    private fun <T> commandTyped(accountId: UUID, idempotencyKey: UUID, source: String, type: Class<T>, action: () -> T): T {
        val fingerprint = sha256(source)
        jdbc.sql("select id from account where id=:account for update").param("account", accountId).query(UUID::class.java).optional().orElseThrow { IllegalArgumentException("AUTHENTICATION_REQUIRED") }
        commandRow(accountId, idempotencyKey)?.let { existing ->
            require(existing.fingerprint == fingerprint) { "IDEMPOTENCY_KEY_REUSED" }
            @Suppress("UNCHECKED_CAST")
            return when {
                existing.resultVersion.toInt() == 1 && type == SkillEnhanceResult::class.java -> legacyReader.readEnhance(existing.resultJson) as T
                existing.resultVersion.toInt() == 1 && type == SkillScreenState::class.java -> legacyReader.readState(existing.resultJson) as T
                else -> mapper.readValue(existing.resultJson, type)
            }
        }
        val result = action()
        jdbc.sql("insert into skill_command_record(command_id,account_id,idempotency_key,fingerprint,result_version,result_json) values (:command,:account,:key,:fingerprint,2,cast(:result as jsonb))")
            .params(mapOf("command" to UUID.randomUUID(), "account" to accountId, "key" to idempotencyKey, "fingerprint" to fingerprint, "result" to mapper.writeValueAsString(result))).update()
        return result
    }

    private fun commandRow(accountId: UUID, key: UUID): StoredCommand? = jdbc.sql("select fingerprint,result_json::text,result_version from skill_command_record where account_id=:account and idempotency_key=:key")
        .params(mapOf("account" to accountId, "key" to key)).query { row, _ -> StoredCommand(row.getString("fingerprint"), row.getString("result_json"), row.getShort("result_version")) }.optional().orElse(null)


    private fun sha256(value: String): String = MessageDigest.getInstance("SHA-256").digest(value.toByteArray(StandardCharsets.UTF_8)).joinToString("") { "%02x".format(it) }

}
