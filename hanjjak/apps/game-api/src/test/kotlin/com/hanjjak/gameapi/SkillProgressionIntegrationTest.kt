package com.hanjjak.gameapi

import com.hanjjak.skills.application.SkillService
import com.hanjjak.skills.domain.SkillActionKind
import com.hanjjak.skills.domain.SkillGrade
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertThrows
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.jdbc.core.simple.JdbcClient
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.util.UUID

@GameApiIntegrationTest
class SkillProgressionIntegrationTest {
    @Autowired private lateinit var jdbc: JdbcClient
    @Autowired private lateinit var skills: SkillService
    @Autowired private lateinit var rolls: QueueSkillRollSource



    @Test
    fun `normal enhancement charges ninety rice and replays same key without double charge`() {
        val account = account(rice = 1_000, books = mapOf("normal" to 3))
        skills.enhance(account, UUID(0, 1), "active_heavy")
        val before = wallet(account)
        val key = UUID(0, 2)
        rolls.enqueue(0)
        val first = skills.enhance(account, key, "active_heavy")
        val replay = skills.enhance(account, key, "active_heavy")
        assertEquals(true, first.success)
        assertEquals(first, replay)
        assertEquals(2, skill(account).level)
        assertEquals(before - 90, wallet(account))
        assertEquals(1, rolls.draws)
    }

    @Test
    fun `enhancement uses server roll source rather than idempotency key`() {
        val account = account(rice = 1_000, books = mapOf("normal" to 3))
        state(account, "active_heavy", SkillGrade.NORMAL, 1)
        rolls.enqueue(9_999)
        val result = skills.enhance(account, UUID(0, 0), "active_heavy")
        assertFalse(result.success)
        assertEquals(1, rolls.draws)
    }

    @Test
    fun `empty roll queue fails closed`() {
        assertThrows(IllegalStateException::class.java) { rolls.nextBasisPoint() }
        assertEquals(1, rolls.draws)
    }

    @Test
    fun `same enhancement key replays stored outcome without drawing again`() {
        val account = account(rice = 1_000, books = mapOf("normal" to 3))
        state(account, "active_heavy", SkillGrade.NORMAL, 1)
        rolls.enqueue(9_999, 0)
        val key = UUID.randomUUID()
        val first = skills.enhance(account, key, "active_heavy")
        val replay = skills.enhance(account, key, "active_heavy")
        assertEquals(first, replay)
        assertEquals(1, rolls.draws)
    }

    @Test
    fun `v1 enhancement replay transforms stored state without mutation or entropy`() {
        val account = account(rice = 800, books = mapOf("normal" to 4))
        state(account, "active_heavy", SkillGrade.EPIC, 10, bonus = 500)
        val key = UUID.randomUUID()
        val oldResult = """
            {"skill":{"skillId":"active_heavy","name":"구 스킬","active":true,"unlocked":true,"grade":"NORMAL","gradeName":"구 노말","level":2,"equippedSlot":1,"effectText":"stored target","bookItemId":"skillbook:active_heavy:normal","availableBooks":7,"riceCost":123,"nextSuccessBasisPoints":4321},"success":false,"state":{"skills":[{"skillId":"active_heavy","name":"구 스킬","active":true,"unlocked":true,"grade":"NORMAL","gradeName":"구 노말","level":2,"equippedSlot":1,"effectText":"stored target","bookItemId":"skillbook:active_heavy:normal","availableBooks":7,"riceCost":123,"nextSuccessBasisPoints":4321},{"skillId":"active_dot","name":"구 지속","active":true,"unlocked":false,"grade":null,"gradeName":null,"level":0,"equippedSlot":null,"effectText":"stored other","bookItemId":"skillbook:active_dot:normal","availableBooks":3,"riceCost":99,"nextSuccessBasisPoints":10000}],"activeLoadout":["active_dot"],"riceBalance":654}}
        """.trimIndent()
        insertCommand(account, key, "skill\u0000enhance\u0000active_heavy", oldResult, version = 1.toShort())
        val beforeBook = quantity(account, "skillbook:active_heavy:normal")
        val beforeWallet = wallet(account)
        val beforeVersion = accountVersion(account)
        val beforeDraws = rolls.draws
        val replay = skills.enhance(account, key, "active_heavy")
        assertFalse(replay.success)
        assertEquals(listOf("active_dot"), replay.state.activeLoadout)
        assertEquals(654, replay.state.riceBalance)
        assertEquals(2, replay.state.skills.size)
        assertEquals(SkillActionKind.ENHANCE, replay.skill.action.kind)
        assertEquals(123, replay.skill.action.riceCost)
        assertEquals(4321, replay.skill.action.successBasisPoints)
        assertEquals("stored target", replay.skill.effectText)
        assertEquals(beforeBook, quantity(account, "skillbook:active_heavy:normal"))
        assertEquals(beforeWallet, wallet(account))
        assertEquals(beforeVersion, accountVersion(account))
        assertEquals(beforeDraws, rolls.draws)
    }

    @Test
    fun `v1 loadout replay transforms every stored summary without mutation`() {
        val account = account(rice = 500, books = emptyMap())
        val key = UUID.randomUUID()
        val oldState = """
            {"skills":[{"skillId":"active_heavy","name":"구 강타","active":true,"unlocked":true,"grade":"NORMAL","gradeName":"노말","level":3,"equippedSlot":1,"effectText":"old effect","bookItemId":"skillbook:active_heavy:normal","availableBooks":8,"riceCost":270,"nextSuccessBasisPoints":6000},{"skillId":"active_dot","name":"구 지속","active":true,"unlocked":false,"grade":null,"gradeName":null,"level":0,"equippedSlot":null,"effectText":"old locked","bookItemId":"skillbook:active_dot:normal","availableBooks":2,"riceCost":100,"nextSuccessBasisPoints":10000}],"activeLoadout":["active_heavy"],"riceBalance":321}
        """.trimIndent()
        insertCommand(account, key, "skill\u0000loadout\u0000active_heavy", oldState, version = 1.toShort())
        val beforeVersion = accountVersion(account)
        val replay = skills.updateLoadout(account, key, com.hanjjak.skills.domain.SkillLoadoutUpdateRequest(listOf("active_heavy")))
        assertEquals(listOf("active_heavy"), replay.activeLoadout)
        assertEquals(321, replay.riceBalance)
        assertEquals(2, replay.skills.size)
        assertEquals(SkillActionKind.ENHANCE, replay.skills.first { it.skillId == "active_heavy" }.action.kind)
        assertEquals(SkillActionKind.UNLOCK, replay.skills.first { it.skillId == "active_dot" }.action.kind)
        assertEquals(beforeVersion, accountVersion(account))
        assertEquals(0, jdbc.sql("select count(*) from skill_loadout where account_id=:account").param("account", account).query(Long::class.java).single())
    }

    @Test
    fun `new command stores result version two and replay draws once`() {
        val account = account(rice = 1_000, books = mapOf("normal" to 3))
        state(account, "active_heavy", SkillGrade.NORMAL, 1)
        rolls.enqueue(9_999, 0)
        val key = UUID.randomUUID()
        val first = skills.enhance(account, key, "active_heavy")
        val replay = skills.enhance(account, key, "active_heavy")
        assertEquals(first, replay)
        assertEquals(1, rolls.draws)
        assertEquals(2, jdbc.sql("select result_version from skill_command_record where account_id=:account and idempotency_key=:key").params(mapOf("account" to account, "key" to key)).query(Short::class.java).single().toInt())
    }

    @Test
    fun `normal ten promotes to rare using composite books and promotion rice`() {
        val account = account(rice = 4_050, books = mapOf("normal" to 2, "rare" to 1))
        state(account, "active_heavy", SkillGrade.NORMAL, 10)
        val result = skills.promote(account, UUID(0, 3), "active_heavy")
        assertEquals(SkillGrade.RARE, result.skill.grade)
        assertEquals(1, result.skill.level)
        assertEquals(0, quantity(account, "skillbook:active_heavy:normal"))
        assertEquals(0, quantity(account, "skillbook:active_heavy:rare"))
        assertEquals(0, wallet(account))
    }

    @Test
    fun `rare enhancement charges normal and rare composite books`() {
        val account = account(rice = 180, books = mapOf("normal" to 2, "rare" to 1))
        state(account, "active_heavy", SkillGrade.RARE, 1)
        val action = skills.state(account).skills.first { it.skillId == "active_heavy" }.action
        assertEquals(listOf(2L, 1L), action.books.map { it.requiredQuantity })
        rolls.enqueue(0)
        val result = skills.enhance(account, UUID(0, 4), "active_heavy")
        assertEquals(2, result.skill.level)
        assertEquals(1, rolls.draws)
        assertEquals(0, quantity(account, "skillbook:active_heavy:normal"))
        assertEquals(0, quantity(account, "skillbook:active_heavy:rare"))
        assertEquals(0, wallet(account))
    }

    @Test
    fun `missing later book rolls back earlier book consumption`() {
        val account = account(rice = 180, books = mapOf("normal" to 2))
        state(account, "active_heavy", SkillGrade.RARE, 1)
        assertEquals("INSUFFICIENT_SKILLBOOK", assertThrows(IllegalArgumentException::class.java) {
            skills.enhance(account, UUID(0, 5), "active_heavy")
        }.message)
        assertEquals(2, quantity(account, "skillbook:active_heavy:normal"))
        assertEquals(180, wallet(account))
        assertEquals(1, skill(account).level)
    }

    @Test
    fun `missing rice rolls back all composite book consumption`() {
        val account = account(rice = 0, books = mapOf("normal" to 2, "rare" to 1))
        state(account, "active_heavy", SkillGrade.RARE, 1)
        assertEquals("INSUFFICIENT_RICE", assertThrows(IllegalArgumentException::class.java) {
            skills.enhance(account, UUID(0, 6), "active_heavy")
        }.message)
        assertEquals(2, quantity(account, "skillbook:active_heavy:normal"))
        assertEquals(1, quantity(account, "skillbook:active_heavy:rare"))
        assertEquals(1, skill(account).level)
    }

    @Test
    fun `late rare book update failure rolls back earlier book wallet state and command`() {
        val account = account(rice = 180, books = mapOf("normal" to 2, "rare" to 1))
        state(account, "active_heavy", SkillGrade.RARE, 1)
        val version = accountVersion(account)
        withInventoryUpdateFailure(account) {
            assertThrows(Exception::class.java) {
                skills.enhance(account, UUID(0, 10), "active_heavy")
            }
        }
        assertEquals(2, quantity(account, "skillbook:active_heavy:normal"))
        assertEquals(1, quantity(account, "skillbook:active_heavy:rare"))
        assertEquals(180, wallet(account))
        assertEquals(1, skill(account).level)
        assertEquals(version, accountVersion(account))
        assertEquals(0, commandCount(account))
    }

    private fun insertCommand(account: UUID, key: UUID, source: String, result: String, version: Short) {
        val fingerprint = MessageDigest.getInstance("SHA-256")
            .digest(source.toByteArray(StandardCharsets.UTF_8))
            .joinToString("") { "%02x".format(it) }
        jdbc.sql("insert into skill_command_record(command_id,account_id,idempotency_key,fingerprint,result_version,result_json) values (:command,:account,:key,:fingerprint,:version,cast(:result as jsonb))")
            .params(mapOf("command" to UUID.randomUUID(), "account" to account, "key" to key, "fingerprint" to fingerprint, "version" to version, "result" to result)).update()
    }

    @Test
    fun `wallet update failure rolls back every consumed book state and command`() {
        val account = account(rice = 180, books = mapOf("normal" to 2, "rare" to 1))
        state(account, "active_heavy", SkillGrade.RARE, 1)
        val version = accountVersion(account)

        withWalletUpdateFailure(account) {
            assertThrows(Exception::class.java) {
                skills.enhance(account, UUID(0, 11), "active_heavy")
            }
        }
        assertEquals(2, quantity(account, "skillbook:active_heavy:normal"))
        assertEquals(1, quantity(account, "skillbook:active_heavy:rare"))
        assertEquals(180, wallet(account))
        assertEquals(1, skill(account).level)
        assertEquals(version, accountVersion(account))
        assertEquals(0, commandCount(account))
    }

    @Test
    fun `same key with another action or skill is rejected`() {
        val account = account(rice = 2_000, books = mapOf("normal" to 3))
        skills.enhance(account, UUID(0, 7), "active_heavy")
        assertEquals("IDEMPOTENCY_KEY_REUSED", assertThrows(IllegalArgumentException::class.java) {
            skills.promote(account, UUID(0, 7), "active_heavy")
        }.message)
        assertEquals("IDEMPOTENCY_KEY_REUSED", assertThrows(IllegalArgumentException::class.java) {
            skills.enhance(account, UUID(0, 7), "active_dot")
        }.message)
    }

    @Test
    fun `failed enhancement persists five percentage point bonus and success resets it`() {
        val account = account(rice = 1_000, books = mapOf("normal" to 3))
        state(account, "active_heavy", SkillGrade.NORMAL, 1)
        rolls.enqueue(9_999, 0)
        val failed = skills.enhance(account, UUID(0, 8_000), "active_heavy")
        assertFalse(failed.success)
        assertEquals(500, skill(account).failureBonusBasisPoints)
        val succeeded = skills.enhance(account, UUID(0, 0), "active_heavy")
        assertTrue(succeeded.success)
        assertEquals(0, skill(account).failureBonusBasisPoints)
        assertEquals(2, rolls.draws)
    }

    @Test
    fun `epic ten is locked without catalog lookup or consumption`() {
        val account = account(rice = 0, books = emptyMap())
        state(account, "active_heavy", SkillGrade.EPIC, 10)
        val summary = skills.state(account).skills.first { it.skillId == "active_heavy" }
        assertEquals(SkillActionKind.LOCKED, summary.action.kind)
        assertFalse(summary.action.executable)
        assertEquals("SKILL_GRADE_LOCKED", summary.action.disabledReason)
        assertTrue(summary.action.books.isEmpty())
        assertEquals("SKILL_GRADE_LOCKED", assertThrows(IllegalArgumentException::class.java) {
            skills.promote(account, UUID(0, 9), "active_heavy")
        }.message)
        assertEquals(0, wallet(account))
    }

    @Test
    fun `profile uses grade-specific skill effects`() {
        val account = account(rice = 0, books = emptyMap())
        state(account, "active_heavy", SkillGrade.RARE, 1)
        state(account, "active_dot", SkillGrade.EPIC, 1)
        /* 해금하면 액티브는 칸에 함께 올라간다. 여기서는 상태만 직접 넣었으므로 칸도 직접 채운다. */
        equip(account, "active_heavy", "active_dot")
        val profile = skills.profile(account)
        assertEquals(32_000, profile.heavyBasisPoints)
        assertEquals(64_000, profile.dotTotalBasisPoints)
    }

    private fun accountVersion(account: UUID): Long = jdbc.sql("select state_version from account where id=:account").param("account", account).query(Long::class.java).single()
    private fun commandCount(account: UUID): Long = jdbc.sql("select count(*) from skill_command_record where account_id=:account").param("account", account).query(Long::class.java).single()

    private fun withInventoryUpdateFailure(account: UUID, block: () -> Unit) {
        val suffix = account.toString().replace("-", "")
        val function = "fail_skill_inventory_$suffix"
        val trigger = "fail_skill_inventory_trigger_$suffix"
        jdbc.sql("create function $function() returns trigger language plpgsql as $$ begin raise exception 'TEST_LATE_BOOK_FAILURE'; end; $$").update()
        jdbc.sql("create trigger $trigger before update on inventory_stack for each row when (old.account_id = '$account'::uuid and old.item_id = 'skillbook:active_heavy:rare') execute function $function()").update()
        try { block() } finally {
            jdbc.sql("drop trigger if exists $trigger on inventory_stack").update()
            jdbc.sql("drop function if exists $function()").update()
        }
    }

    private fun withWalletUpdateFailure(account: UUID, block: () -> Unit) {
        val suffix = account.toString().replace("-", "")
        val function = "fail_skill_wallet_$suffix"
        val trigger = "fail_skill_wallet_trigger_$suffix"
        jdbc.sql("create function $function() returns trigger language plpgsql as $$ begin raise exception 'TEST_WALLET_FAILURE'; end; $$").update()
        jdbc.sql("create trigger $trigger before update on wallet_balance for each row when (old.account_id = '$account'::uuid) execute function $function()").update()
        try { block() } finally {
            jdbc.sql("drop trigger if exists $trigger on wallet_balance").update()
            jdbc.sql("drop function if exists $function()").update()
        }
    }

    private fun account(rice: Long, books: Map<String, Long>): UUID {
        val id = UUID.randomUUID()
        jdbc.sql("insert into account(id,email,password_hash,state_version,created_at) values (:id,:email,'test',1,now())")
            .params(mapOf("id" to id, "email" to "$id@test.local")).update()
        jdbc.sql("insert into character(id,account_id,nickname,level,experience,rice) values (:character,:account,'tester',1,0,:rice)")
            .params(mapOf("character" to UUID.randomUUID(), "account" to id, "rice" to rice)).update()
        books.forEach { (grade, quantity) ->
            jdbc.sql("insert into inventory_stack values (:account,:item,:quantity,0,1)")
                .params(mapOf("account" to id, "item" to "skillbook:active_heavy:$grade", "quantity" to quantity)).update()
        }
        return id
    }

    private fun state(account: UUID, skillId: String, grade: SkillGrade, level: Int, bonus: Int = 0) {
        jdbc.sql("insert into skill_state(account_id,skill_id,grade,level,failure_bonus_basis_points) values (:account,:skill,:grade,:level,:bonus)")
            .params(mapOf("account" to account, "skill" to skillId, "grade" to grade.name, "level" to level, "bonus" to bonus)).update()
    }

    /** 장착한 액티브만 싸운다. 상태를 직접 넣은 시험에서는 칸도 직접 채워야 한다. */
    private fun equip(account: UUID, vararg skillIds: String) {
        skillIds.forEachIndexed { index, skillId ->
            jdbc.sql("insert into skill_loadout(account_id,slot_index,skill_id) values (:account,:slot,:skill)")
                .params(mapOf("account" to account, "slot" to index + 1, "skill" to skillId)).update()
        }
    }

    private fun skill(account: UUID) = jdbc.sql("select skill_id,grade,level,failure_bonus_basis_points from skill_state where account_id=:account")
        .param("account", account).query { row, _ -> row.getInt("level") to row.getInt("failure_bonus_basis_points") }.single().let { StoredSkill(it.first, it.second) }

    private data class StoredSkill(val level: Int, val failureBonusBasisPoints: Int)
    private fun quantity(account: UUID, item: String): Long = jdbc.sql("select coalesce(quantity,0) from inventory_stack where account_id=:account and item_id=:item")
        .params(mapOf("account" to account, "item" to item)).query(Long::class.java).optional().orElse(0L)
    private fun wallet(account: UUID): Long = jdbc.sql("select balance from wallet_balance where account_id=:account").param("account", account).query(Long::class.java).single()

}
