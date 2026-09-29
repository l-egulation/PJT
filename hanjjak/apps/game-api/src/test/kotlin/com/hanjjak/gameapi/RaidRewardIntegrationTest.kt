package com.hanjjak.gameapi

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.raid.domain.RaidAttemptMode
import com.hanjjak.raid.application.RaidClaimResult
import com.hanjjak.raid.application.RaidService
import com.hanjjak.raid.domain.RaidAttemptId
import com.hanjjak.raid.domain.RaidClaim
import com.hanjjak.raid.domain.RaidClaimId
import com.hanjjak.raid.domain.RaidClaimKind
import com.hanjjak.raid.domain.RaidClaimStatus
import com.hanjjak.raid.domain.RaidFinalRank
import com.hanjjak.raid.domain.RaidGrade
import com.hanjjak.raid.domain.RaidRewardBundle
import com.hanjjak.raid.domain.RaidRules
import com.hanjjak.raid.domain.RaidSessionId
import com.hanjjak.raid.infrastructure.JdbcRaidRepository
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertThrows
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.jdbc.core.simple.JdbcClient
import java.sql.Timestamp
import java.time.Instant
import java.util.UUID
import java.util.concurrent.CountDownLatch
import java.util.concurrent.ExecutionException
import java.util.concurrent.Executors

@GameApiIntegrationTest
@org.springframework.context.annotation.Import(RaidLifecycleSpringIntegrationTest.Configuration::class)
class RaidRewardIntegrationTest {
    @Autowired lateinit var raids: RaidService
    @Autowired lateinit var repository: JdbcRaidRepository
    @Autowired lateinit var jdbc: JdbcClient
    @Autowired lateinit var mapper: ObjectMapper
    @Autowired lateinit var handoff: ControlledRaidMainBattlePort
    @Autowired lateinit var completion: com.hanjjak.raid.application.RaidAttemptCompletionService

    @Test
    fun `running confirmation uses only the elapsed prefix and credits one atomic bundle`() {
        val fixture = fixture()
        val started = raids.startAttempt(fixture.accountId, UUID.randomUUID(), RaidAttemptMode.REWARD)
        val persisted = repository.lockAttempt(RaidAttemptId(started.attemptId), RaidSessionId(started.sessionId), fixture.accountId)
        val timeline = mapper.readTree(persisted.timeline)
        val firstImpactTick = timeline.first { it.path("type").asText() == "PLAYER_IMPACT" }.path("logicalTick").asInt()
        shiftAttempt(started.attemptId, firstImpactTick)
        val versionBefore = accountVersion(fixture.accountId)

        val confirmed = raids.confirmAttempt(fixture.accountId, UUID.randomUUID(), started.attemptId)

        val expectedDamage = timeline.filter { it.path("type").asText() == "PLAYER_IMPACT" && it.path("logicalTick").asInt() <= firstImpactTick }
            .sumOf { it.path("damage").asLong() }
        assertEquals(expectedDamage, confirmed.confirmedResult.damage)
        assertEquals(1, accountVersion(fixture.accountId) - versionBefore)
        assertEquals(5L, ticketBalance(fixture.accountId))
        assertEquals(5_000L, riceBalance(fixture.accountId))
        assertEquals(3L, gemBoxes(fixture.accountId))
        assertEquals(expectedDamage, scalar("select total_damage from raid_contribution where account_id='${fixture.accountId}'"))
        assertEquals(1L, scalar("select count(*) from wallet_ledger where account_id='${fixture.accountId}' and source_type='RAID_REWARD'"))
        assertEquals(1L, scalar("select count(*) from outbox_event where event_type='RAID_ATTEMPT_CONFIRMED' and aggregate_id='${started.attemptId}'"))
    }
    @Test
    fun `positive tick zero confirmation uses only tick zero impacts`() {
        val fixture = fixture()
        val started = raids.startAttempt(fixture.accountId, UUID.randomUUID(), RaidAttemptMode.REWARD)
        val persisted = repository.lockAttempt(RaidAttemptId(started.attemptId), RaidSessionId(started.sessionId), fixture.accountId)
        val timeline = mapper.readTree(persisted.timeline)
        jdbc.sql("update raid_attempt set started_at=now()+interval '1 second',completable_at=now()+interval '2 minutes' where id=:id")
            .param("id", started.attemptId).update()
        val confirmed = raids.confirmAttempt(fixture.accountId, UUID.randomUUID(), started.attemptId)
        val expected = timeline.filter { it.path("type").asText() == "PLAYER_IMPACT" && it.path("logicalTick").asInt() == 0 }.sumOf { it.path("damage").asLong() }
        assertEquals(expected, confirmed.confirmedResult.damage)
    }

    @Test
    fun `live confirmation resumes the exact paused battle once`() {
        val fixture = fixture()
        handoff.handoff = com.hanjjak.raid.application.RaidBattleHandoff(UUID.randomUUID(), UUID.randomUUID(), "stage.01-01")
        val started = raids.startAttempt(fixture.accountId, UUID.randomUUID(), RaidAttemptMode.REWARD)
        val persisted = repository.lockAttempt(RaidAttemptId(started.attemptId), RaidSessionId(started.sessionId), fixture.accountId)
        val firstImpactTick = mapper.readTree(persisted.timeline).first { it.path("type").asText() == "PLAYER_IMPACT" }.path("logicalTick").asInt()
        shiftAttempt(started.attemptId, firstImpactTick)
        val expected = handoff.handoff

        raids.confirmAttempt(fixture.accountId, UUID.randomUUID(), started.attemptId)

        assertEquals(listOf(expected), handoff.resumed)
        handoff.handoff = null
    }

    @Test
    fun `held confirmation uses the complete persisted terminal result`() {
        val fixture = fixture()
        val started = raids.startAttempt(fixture.accountId, UUID.randomUUID(), RaidAttemptMode.REWARD)
        terminalizeForConfirmation(started.attemptId)
        val persisted = repository.lockAttempt(RaidAttemptId(started.attemptId), RaidSessionId(started.sessionId), fixture.accountId)
        val terminalDamage = mapper.readTree(persisted.result).path("damage").asLong()
        val confirmed = raids.confirmAttempt(fixture.accountId, UUID.randomUUID(), started.attemptId)
        assertEquals(terminalDamage, confirmed.confirmedResult.damage)
    }

    @Test
    fun `terminalizer and confirmation race settles one result and one reward`() {
        val fixture = fixture()
        val started = raids.startAttempt(fixture.accountId, UUID.randomUUID(), RaidAttemptMode.REWARD)
        jdbc.sql("update raid_attempt set started_at=now()-interval '2 seconds',completable_at=now()-interval '1 second' where id=:id")
            .param("id", started.attemptId).update()
        val start = CountDownLatch(1)
        val pool = Executors.newFixedThreadPool(2)
        try {
            val terminalize = pool.submit { start.await(); completion.terminalizeOne(fixture.accountId, started.attemptId, Instant.now()) }
            val confirm = pool.submit<com.hanjjak.raid.application.RaidConfirmationResult> { start.await(); raids.confirmAttempt(fixture.accountId, UUID.randomUUID(), started.attemptId) }
            start.countDown()
            terminalize.get()
            confirm.get()
        } finally {
            pool.shutdownNow()
        }
        assertEquals(1L, scalar("select count(*) from raid_confirmed_result where attempt_id='${started.attemptId}'"))
        assertEquals(1L, scalar("select count(*) from wallet_ledger where account_id='${fixture.accountId}' and source_type='RAID_REWARD'"))
    }

    @Test
    fun `contribution and outbox late failures each roll back the whole confirmation`() {
        for (target in listOf("raid_contribution", "outbox_event")) {
            val fixture = fixture()
            val started = raids.startAttempt(fixture.accountId, UUID.randomUUID(), RaidAttemptMode.REWARD)
            if (target == "raid_contribution") {
                jdbc.sql("insert into raid_contribution(session_id,account_id) values (:session,:account)")
                    .params(mapOf("session" to fixture.sessionId, "account" to fixture.accountId)).update()
            }
            terminalizeForConfirmation(started.attemptId)
            withLateFailure(target, fixture.accountId) {
                assertThrows(org.springframework.dao.DataAccessException::class.java) {
                    raids.confirmAttempt(fixture.accountId, UUID.randomUUID(), started.attemptId)
                }
            }
            assertEquals(0L, ticketBalance(fixture.accountId))
            assertEquals(0L, riceBalance(fixture.accountId))
            assertEquals(0L, gemBoxes(fixture.accountId))
            assertEquals(0L, scalar("select count(*) from raid_confirmed_result where attempt_id='${started.attemptId}'"))
        }
    }

    @Test
    fun `running confirmation respects tick forty nine and fifty boundaries`() {
        val atFortyNine = fixture()
        val first = raids.startAttempt(atFortyNine.accountId, UUID.randomUUID(), RaidAttemptMode.REWARD)
        val storedFirst = repository.lockAttempt(RaidAttemptId(first.attemptId), RaidSessionId(first.sessionId), atFortyNine.accountId)
        val timelineFirst = mapper.readTree(storedFirst.timeline)
        shiftAttempt(first.attemptId, 49)
        val confirmedFortyNine = raids.confirmAttempt(atFortyNine.accountId, UUID.randomUUID(), first.attemptId)
        val expectedFortyNine = timelineFirst.filter { it.path("type").asText() == "PLAYER_IMPACT" && it.path("logicalTick").asInt() <= 49 }.sumOf { it.path("damage").asLong() }
        assertEquals(expectedFortyNine, confirmedFortyNine.confirmedResult.damage)

        val atFifty = fixture()
        val second = raids.startAttempt(atFifty.accountId, UUID.randomUUID(), RaidAttemptMode.REWARD)
        val storedSecond = repository.lockAttempt(RaidAttemptId(second.attemptId), RaidSessionId(second.sessionId), atFifty.accountId)
        val timelineSecond = mapper.readTree(storedSecond.timeline)
        shiftAttempt(second.attemptId, 50)
        val confirmedFifty = raids.confirmAttempt(atFifty.accountId, UUID.randomUUID(), second.attemptId)
        val expectedFifty = timelineSecond.filter { it.path("type").asText() == "PLAYER_IMPACT" && it.path("logicalTick").asInt() <= 50 }.sumOf { it.path("damage").asLong() }
        assertEquals(expectedFifty, confirmedFifty.confirmedResult.damage)
    }

    @Test
    fun `wallet overflow rolls back whole confirmation bundle`() {
        val fixture = fixture()
        jdbc.sql("update wallet_balance set balance=:max where account_id=:account")
            .params(mapOf("max" to Long.MAX_VALUE, "account" to fixture.accountId)).update()
        val started = raids.startAttempt(fixture.accountId, UUID.randomUUID(), RaidAttemptMode.REWARD)
        terminalizeForConfirmation(started.attemptId)
        val versionBefore = accountVersion(fixture.accountId)

        assertThrows(ArithmeticException::class.java) {
            raids.confirmAttempt(fixture.accountId, UUID.randomUUID(), started.attemptId)
        }

        assertEquals(Long.MAX_VALUE, riceBalance(fixture.accountId))
        assertEquals(0L, ticketBalance(fixture.accountId))
        assertEquals(0L, gemBoxes(fixture.accountId))
        assertEquals(versionBefore, accountVersion(fixture.accountId))
        assertEquals(0L, scalar("select count(*) from raid_confirmed_result where account_id='${fixture.accountId}'"))
    }


    @Test
    fun `zero damage confirmation changes no reward or raid state`() {
        val fixture = fixture()
        val started = raids.startAttempt(fixture.accountId, UUID.randomUUID(), RaidAttemptMode.REWARD)
        jdbc.sql("update raid_attempt set started_at=now(),completable_at=now()+interval '1 minute',timeline_json='[]'::jsonb where id=:id").param("id", started.attemptId).update()
        val versionBefore = accountVersion(fixture.accountId)

        val error = assertThrows(IllegalArgumentException::class.java) {
            raids.confirmAttempt(fixture.accountId, UUID.randomUUID(), started.attemptId)
        }

        assertEquals("RAID_ZERO_DAMAGE_CANNOT_CONFIRM", error.message)
        assertEquals(versionBefore, accountVersion(fixture.accountId))
        assertEquals(0L, ticketBalance(fixture.accountId))
        assertEquals(0L, riceBalance(fixture.accountId))
        assertEquals(0L, scalar("select count(*) from raid_confirmed_result where account_id='${fixture.accountId}'"))
        assertEquals("RUNNING", jdbc.sql("select status from raid_attempt where id=:id").param("id", started.attemptId).query(String::class.java).single())
    }

    @Test
    fun `inventory capacity failure rolls back tickets rice contribution and command`() {
        val fixture = fixture()
        fillInventory(fixture.accountId)
        val started = raids.startAttempt(fixture.accountId, UUID.randomUUID(), RaidAttemptMode.REWARD)
        terminalizeForConfirmation(started.attemptId)
        val versionBefore = accountVersion(fixture.accountId)

        assertThrows(IllegalArgumentException::class.java) {
            raids.confirmAttempt(fixture.accountId, UUID.randomUUID(), started.attemptId)
        }

        assertEquals(versionBefore, accountVersion(fixture.accountId))
        assertEquals(0L, ticketBalance(fixture.accountId))
        assertEquals(0L, riceBalance(fixture.accountId))
        assertEquals(0L, scalar("select count(*) from raid_confirmed_result where account_id='${fixture.accountId}'"))
        assertEquals(0L, scalar("select count(*) from raid_contribution where account_id='${fixture.accountId}'"))
        assertEquals(1L, scalar("select count(*) from raid_command_record where account_id='${fixture.accountId}'"))
    }

    @Test
    fun `ticket overflow rolls back whole confirmation bundle`() {
        val fixture = fixture()
        jdbc.sql("update character set cosmetic_ticket_balance=:max where account_id=:account")
            .params(mapOf("max" to Long.MAX_VALUE, "account" to fixture.accountId)).update()
        val started = raids.startAttempt(fixture.accountId, UUID.randomUUID(), RaidAttemptMode.REWARD)
        terminalizeForConfirmation(started.attemptId)
        val versionBefore = accountVersion(fixture.accountId)

        assertThrows(ArithmeticException::class.java) {
            raids.confirmAttempt(fixture.accountId, UUID.randomUUID(), started.attemptId)
        }

        assertEquals(Long.MAX_VALUE, ticketBalance(fixture.accountId))
        assertEquals(0L, riceBalance(fixture.accountId))
        assertEquals(0L, gemBoxes(fixture.accountId))
        assertEquals(versionBefore, accountVersion(fixture.accountId))
        assertEquals(0L, scalar("select count(*) from raid_confirmed_result where account_id='${fixture.accountId}'"))
    }

    @Test
    fun `concurrent confirmation and same key replay credit once`() {
        val fixture = fixture()
        val started = raids.startAttempt(fixture.accountId, UUID.randomUUID(), RaidAttemptMode.REWARD)
        terminalizeForConfirmation(started.attemptId)
        val versionBefore = accountVersion(fixture.accountId)
        val key = UUID.randomUUID()
        val start = CountDownLatch(1)
        val pool = Executors.newFixedThreadPool(2)
        val confirmations = try {
            val results = (1..2).map { pool.submit<com.hanjjak.raid.application.RaidConfirmationResult> { start.await(); raids.confirmAttempt(fixture.accountId, key, started.attemptId) } }
            start.countDown()
            results.map { it.get() }
        } finally {
            pool.shutdownNow()
        }
        assertEquals(1, confirmations.map { it.confirmedResult.resultId }.distinct().size)
        val expected = RaidRules.personalReward(confirmations.first().confirmedResult.grade)
        assertEquals(expected.cosmeticTickets, ticketBalance(fixture.accountId))
        assertEquals(expected.rice, riceBalance(fixture.accountId))
        assertEquals(expected.gemBoxes, gemBoxes(fixture.accountId))
        assertEquals(1L, scalar("select count(*) from raid_confirmed_result where attempt_id='${started.attemptId}'"))
        assertEquals(versionBefore + 1, accountVersion(fixture.accountId))
        assertEquals(1L, scalar("select count(*) from wallet_ledger where account_id='${fixture.accountId}' and source_type='RAID_REWARD'"))
        assertEquals(1L, scalar("select count(*) from outbox_event where event_type='RAID_ATTEMPT_CONFIRMED' and aggregate_id='${started.attemptId}'"))
        assertEquals(1L, scalar("select count(*) from raid_command_record where account_id='${fixture.accountId}' and idempotency_key='$key'"))
        assertThrows(IllegalArgumentException::class.java) {
            raids.confirmAttempt(fixture.accountId, key, UUID.randomUUID())
        }
    }

    @Test
    fun `late inventory credit failure rolls back wallet tickets and raid mutations`() {
        val fixture = fixture()
        val started = raids.startAttempt(fixture.accountId, UUID.randomUUID(), RaidAttemptMode.REWARD)
        terminalizeForConfirmation(started.attemptId)
        val versionBefore = accountVersion(fixture.accountId)

        withGemBoxInsertFailure(fixture.accountId) {
            assertThrows(org.springframework.dao.DataAccessException::class.java) {
                raids.confirmAttempt(fixture.accountId, UUID.randomUUID(), started.attemptId)
            }
        }

        assertEquals(0L, ticketBalance(fixture.accountId))
        assertEquals(0L, riceBalance(fixture.accountId))
        assertEquals(0L, gemBoxes(fixture.accountId))
        assertEquals(versionBefore, accountVersion(fixture.accountId))
        assertEquals("RESULT_HELD", jdbc.sql("select status from raid_attempt where id=:id").param("id", started.attemptId).query(String::class.java).single())
        assertEquals(0L, scalar("select count(*) from raid_confirmed_result where attempt_id='${started.attemptId}'"))
        assertEquals(0L, scalar("select count(*) from wallet_ledger where account_id='${fixture.accountId}' and source_type='RAID_REWARD'"))
    }

    @Test
    fun `whole claim failure stays claimable and concurrent claim credits once`() {
        val fixture = fixture()
        val claim = claim(fixture, RaidRewardBundle(8, 5, 8_000))
        fillInventory(fixture.accountId)
        val versionBefore = accountVersion(fixture.accountId)
        assertThrows(IllegalArgumentException::class.java) {
            raids.claimReward(fixture.accountId, UUID.randomUUID(), claim.claimId.value)
        }
        assertEquals(RaidClaimStatus.CLAIMABLE, repository.lockClaim(claim.claimId, fixture.accountId).status)
        assertEquals(versionBefore, accountVersion(fixture.accountId))
        assertEquals(0L, ticketBalance(fixture.accountId))
        assertEquals(0L, riceBalance(fixture.accountId))

        jdbc.sql("delete from inventory_instance where account_id=:account").param("account", fixture.accountId).update()
        val key = UUID.randomUUID()
        val start = CountDownLatch(1)
        val pool = Executors.newFixedThreadPool(2)
        val claimed = try {
            val results = (1..2).map { pool.submit<RaidClaimResult> { start.await(); raids.claimReward(fixture.accountId, key, claim.claimId.value) } }
            start.countDown()
            results.map { it.get() }
        } finally {
            pool.shutdownNow()
        }
        assertEquals(1, claimed.map { it.claim.claimedAt }.distinct().size)
        assertEquals(RaidClaimStatus.CLAIMED, repository.lockClaim(claim.claimId, fixture.accountId).status)
        assertEquals(8L, ticketBalance(fixture.accountId))
        assertEquals(8_000L, riceBalance(fixture.accountId))
        assertEquals(5L, gemBoxes(fixture.accountId))
        assertEquals(versionBefore + 1, accountVersion(fixture.accountId))
        assertEquals(1L, scalar("select count(*) from outbox_event where event_type='RAID_REWARD_CLAIMED' and aggregate_id='${claim.claimId.value}'"))
        assertEquals(1L, scalar("select count(*) from wallet_ledger where account_id='${fixture.accountId}' and source_type='RAID_REWARD'"))
        assertEquals(1L, scalar("select count(*) from raid_command_record where account_id='${fixture.accountId}' and idempotency_key='$key'"))
    }

    @Test
    fun `claim response preserves other claimable and claimed rows and rejects reused key`() {
        val fixture = fixture()
        val first = claim(fixture, RaidRewardBundle(8, 5, 8_000))
        val secondSession = UUID.randomUUID()
        jdbc.sql("update raid_session set status='SETTLING',cutoff_at=settles_at,settlement_phase='FREEZE_WORKSET' where id=:id").param("id", fixture.sessionId).update()
        jdbc.sql("insert into raid_session(id,content_version,reward_version,settles_at,status,cutoff_at,settlement_phase,completed_at) values (:id,'raid-mvp-v1-working','raid-mvp-v1-working',:settles,'SETTLED_SUCCESS',:settles,'SETTLED',now())")
            .params(mapOf("id" to secondSession, "settles" to Timestamp.from(Instant.now().minusSeconds(86_400)))).update()
        val second = claim(Fixture(fixture.accountId, secondSession), RaidRewardBundle(11, 7, 11_000))
        val key = UUID.randomUUID()

        val result = raids.claimReward(fixture.accountId, key, first.claimId.value)

        assertEquals(2, result.state.items.size)
        assertEquals(1L, result.state.claimableCount)
        assertEquals(1L, result.state.claimedCount)
        assertEquals(second.claimId.value, result.state.items.single { it.status == RaidClaimStatus.CLAIMABLE }.claimId)
        assertThrows(IllegalArgumentException::class.java) { raids.claimReward(fixture.accountId, key, second.claimId.value) }
        assertEquals(RaidClaimStatus.CLAIMABLE, repository.lockClaim(second.claimId, fixture.accountId).status)
    }

    private data class Fixture(val accountId: UUID, val sessionId: UUID)

    private fun fixture(): Fixture {
        val accountId = UUID.randomUUID()
        val sessionId = UUID.randomUUID()
        val now = Instant.now()
        jdbc.sql("insert into account(id,email,password_hash,state_version,created_at) values (:id,:email,'test',1,:now)")
            .params(mapOf("id" to accountId, "email" to "$accountId@test.local", "now" to Timestamp.from(now))).update()
        jdbc.sql("insert into character(id,account_id,level,experience,rice,nickname) values (:id,:account,1,0,0,:nickname)")
            .params(mapOf("id" to UUID.randomUUID(), "account" to accountId, "nickname" to "reward-${accountId.toString().take(8)}")).update()
        jdbc.sql("update raid_session set status='SETTLED_FAILURE',cutoff_at=settles_at,settlement_phase='SETTLED',completed_at=now() where status='OPEN'").update()
        jdbc.sql("insert into raid_session(id,content_version,reward_version,settles_at,status) values (:id,'raid-mvp-v1-working','raid-mvp-v1-working',:settles,'OPEN')")
            .params(mapOf("id" to sessionId, "settles" to Timestamp.from(now.plusSeconds(3600)))).update()
        jdbc.sql("insert into stage_progress(account_id,stage_id,unlocked,first_cleared_at,highest_clear_count,content_version) values (:account,'stage.01-02',true,:now,1,'v1')")
            .params(mapOf("account" to accountId, "now" to Timestamp.from(now))).update()
        return Fixture(accountId, sessionId)
    }

    private fun claim(fixture: Fixture, bundle: RaidRewardBundle): RaidClaim {
        repository.saveFinalRank(RaidFinalRank(RaidSessionId(fixture.sessionId), fixture.accountId, 1, 100, 100, 100))
        val claim = RaidClaim(RaidClaimId(UUID.randomUUID()), RaidSessionId(fixture.sessionId), fixture.accountId, RaidClaimKind.DAILY_RANK,
            fixture.sessionId, RaidClaimStatus.CLAIMABLE, "raid-mvp-v1-working", bundle, Instant.now())
        repository.insertClaim(claim)
        return claim
    }

    private fun terminalizeForConfirmation(attemptId: UUID) {
        jdbc.sql("update raid_attempt set status='RESULT_HELD',ended_at=completable_at,resume_pending=false where id=:id")
            .param("id", attemptId).update()
        jdbc.sql("update raid_reward_slot set status='RESULT_HELD' where current_attempt_id=:id").param("id", attemptId).update()
    }

    private fun shiftAttempt(attemptId: UUID, elapsedTick: Int) {
        jdbc.sql("update raid_attempt set started_at=now()-(:millis * interval '1 millisecond') where id=:id")
            .params(mapOf("millis" to elapsedTick * 100L, "id" to attemptId)).update()
    }

    private fun fillInventory(accountId: UUID) {
        jdbc.sql("insert into inventory_instance(instance_id,account_id,item_id,reserved_for_sale,acquired_sequence) select gen_random_uuid(),:account,'gem:1:flat_attack',false,n from generate_series(1,200) n")
            .param("account", accountId).update()
    }

    private fun withGemBoxInsertFailure(accountId: UUID, block: () -> Unit) {
        val suffix = accountId.toString().replace("-", "")
        val function = "fail_raid_gem_box_$suffix"
        val trigger = "fail_raid_gem_box_trigger_$suffix"
        jdbc.sql("create function $function() returns trigger language plpgsql as $$ begin raise exception 'TEST_LATE_GEM_BOX_FAILURE'; end; $$").update()
        jdbc.sql("create trigger $trigger before insert or update on inventory_stack for each row when (new.account_id = '$accountId'::uuid and new.item_id = 'GEM_BOX') execute function $function() ").update()
        try {
            block()
        } finally {
            jdbc.sql("drop trigger if exists $trigger on inventory_stack").update()
            jdbc.sql("drop function if exists $function()").update()
        }
    }

    private fun withLateFailure(table: String, accountId: UUID, block: () -> Unit) {
        require(table in setOf("raid_contribution", "outbox_event"))
        val suffix = accountId.toString().replace("-", "")
        val function = "fail_${table}_$suffix"
        val trigger = "fail_${table}_trigger_$suffix"
        jdbc.sql("create function $function() returns trigger language plpgsql as $$ begin raise exception 'TEST_LATE_FAILURE'; end; $$").update()
        val whenClause = if (table == "raid_contribution") " when (new.account_id = '$accountId'::uuid)" else " when (new.event_type = 'RAID_ATTEMPT_CONFIRMED')"
        jdbc.sql("create trigger $trigger before insert or update on $table for each row$whenClause execute function $function() ").update()
        try {
            block()
        } finally {
            jdbc.sql("drop trigger if exists $trigger on $table").update()
            jdbc.sql("drop function if exists $function()").update()
        }
    }

    private fun accountVersion(accountId: UUID): Long = jdbc.sql("select state_version from account where id=:account").param("account", accountId).query(Long::class.java).single()
    private fun ticketBalance(accountId: UUID): Long = jdbc.sql("select cosmetic_ticket_balance from character where account_id=:account").param("account", accountId).query(Long::class.java).single()
    private fun riceBalance(accountId: UUID): Long = jdbc.sql("select balance from wallet_balance where account_id=:account").param("account", accountId).query(Long::class.java).single()
    private fun gemBoxes(accountId: UUID): Long = jdbc.sql("select coalesce(sum(quantity),0) from inventory_stack where account_id=:account and item_id='GEM_BOX'").param("account", accountId).query(Long::class.java).single()
    private fun scalar(sql: String): Long = jdbc.sql(sql).query(Long::class.java).single()
}
