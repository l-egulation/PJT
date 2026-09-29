package com.hanjjak.gameapi

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.raid.application.RaidService
import com.hanjjak.raid.application.RaidSettlementService
import com.hanjjak.raid.domain.RaidAttemptId
import com.hanjjak.raid.domain.RaidAttemptMode
import com.hanjjak.raid.domain.RaidSessionId
import com.hanjjak.raid.domain.RaidSessionStatus
import com.hanjjak.raid.domain.RaidSettlementPhase
import com.hanjjak.raid.domain.RaidSlotStatus
import com.hanjjak.raid.infrastructure.JdbcRaidRepository
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.context.annotation.Import
import org.springframework.jdbc.core.simple.JdbcClient
import java.sql.Timestamp
import java.time.Instant
import java.util.UUID
import java.util.concurrent.CountDownLatch
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit

@GameApiIntegrationTest
@Import(RaidLifecycleSpringIntegrationTest.Configuration::class)
class RaidSettlementIntegrationTest {
    @Autowired lateinit var settlement: RaidSettlementService
    @Autowired lateinit var raids: RaidService
    @Autowired lateinit var repository: JdbcRaidRepository
    @Autowired lateinit var jdbc: JdbcClient
    @Autowired lateinit var mapper: ObjectMapper

    @Test
    fun `concurrent schedulers settle each frozen account once and restart without duplicates`() {
        val cutoff = RaidSettlementService.settlementAt(Instant.now())
        val sessionId = UUID.randomUUID()
        jdbc.sql("insert into raid_session(id,content_version,reward_version,settles_at,status) values (:id,'raid-mvp-v1-working','raid-mvp-v1-working',:cutoff,'OPEN')")
            .params(mapOf("id" to sessionId, "cutoff" to Timestamp.from(cutoff))).update()
        val immediate = account("immediate")
        val deferred = account("deferred")
        val attempts = listOf(immediate, deferred).associateWith {
            raids.startAttempt(it, UUID.randomUUID(), RaidAttemptMode.REWARD)
        }
        attempts.values.forEach { attempt ->
            val persisted = repository.lockAttempt(RaidAttemptId(attempt.attemptId), RaidSessionId(sessionId), attempts.entries.single { it.value == attempt }.key)
            assertTrue(mapper.readTree(persisted.result).path("damage").asLong() >= 6_955L)
        }
        fillInventory(deferred)

        val start = CountDownLatch(1)
        val pool = Executors.newFixedThreadPool(2)
        try {
            val calls = (1..2).map {
                pool.submit<List<RaidSessionId>> {
                    start.await()
                    settlement.settleDueSessions(cutoff)
                }
            }
            start.countDown()
            calls.forEach { it.get(30, TimeUnit.SECONDS) }
        } finally {
            pool.shutdownNow()
        }

        val settled = repository.session(RaidSessionId(sessionId))!!
        assertEquals(RaidSessionStatus.SETTLED_FAILURE, settled.status)
        assertEquals(RaidSettlementPhase.SETTLED, settled.settlementPhase)
        assertEquals(2L, settled.settlementAccountCount)
        assertEquals(2L, settled.finalizedAccountCount)
        assertEquals(2L, count("raid_confirmed_result", sessionId))
        assertEquals(2L, count("raid_final_rank", sessionId))
        assertEquals(2L, claimCount(sessionId, "DAILY_RANK"))
        assertEquals(1L, claimCount(sessionId, "AUTO_PERSONAL"))
        assertEquals(1L, ledgerCount(immediate))
        assertEquals(0L, ledgerCount(deferred))
        assertTrue(ticketBalance(immediate) > 0)
        assertEquals(0L, ticketBalance(deferred))
        assertEquals(
            listOf(RaidSlotStatus.CONFIRMED, RaidSlotStatus.EXPIRED, RaidSlotStatus.EXPIRED),
            slotStatuses(immediate),
        )
        assertEquals(
            listOf(RaidSlotStatus.CONFIRMED, RaidSlotStatus.EXPIRED, RaidSlotStatus.EXPIRED),
            slotStatuses(deferred),
        )
        assertEquals(1L, jdbc.sql("select count(*) from raid_session where status='OPEN' and settles_at=:next")
            .param("next", Timestamp.from(RaidSettlementService.nextSettlementAt(cutoff))).query(Long::class.java).single())

        settlement.settleDueSessions(cutoff.plusSeconds(1))
        assertEquals(2L, count("raid_confirmed_result", sessionId))
        assertEquals(2L, count("raid_final_rank", sessionId))
        assertEquals(3L, jdbc.sql("select count(*) from raid_reward_claim where session_id=:session")
            .param("session", sessionId).query(Long::class.java).single())
        assertEquals(2L, jdbc.sql("select count(*) from outbox_event where event_type='RAID_ATTEMPT_CONFIRMED'").query(Long::class.java).single())
    }

    private fun account(label: String): UUID {
        val accountId = UUID.randomUUID()
        val now = Instant.now()
        jdbc.sql("insert into account(id,email,password_hash,state_version,created_at) values (:id,:email,'test',1,:now)")
            .params(mapOf("id" to accountId, "email" to "$label-$accountId@test.local", "now" to Timestamp.from(now))).update()
        jdbc.sql("insert into character(id,account_id,level,experience,rice,nickname) values (:id,:account,500,0,0,:nickname)")
            .params(mapOf("id" to UUID.randomUUID(), "account" to accountId, "nickname" to "$label-${accountId.toString().take(8)}")).update()
        jdbc.sql("insert into stage_progress(account_id,stage_id,unlocked,first_cleared_at,highest_clear_count,content_version) values (:account,'stage.01-02',true,:now,1,'v1')")
            .params(mapOf("account" to accountId, "now" to Timestamp.from(now))).update()
        return accountId
    }

    private fun fillInventory(accountId: UUID) {
        jdbc.sql("insert into inventory_instance(instance_id,account_id,item_id,reserved_for_sale,acquired_sequence) select gen_random_uuid(),:account,'gem:1:flat_attack',false,n from generate_series(1,200) n")
            .param("account", accountId).update()
    }

    private fun count(table: String, sessionId: UUID): Long {
        require(table in setOf("raid_confirmed_result", "raid_final_rank"))
        return jdbc.sql("select count(*) from $table where session_id=:session").param("session", sessionId).query(Long::class.java).single()
    }

    private fun claimCount(sessionId: UUID, kind: String): Long = jdbc.sql(
        "select count(*) from raid_reward_claim where session_id=:session and source_kind=:kind",
    ).params(mapOf("session" to sessionId, "kind" to kind)).query(Long::class.java).single()

    private fun ledgerCount(accountId: UUID): Long = jdbc.sql(
        "select count(*) from wallet_ledger where account_id=:account and source_type='RAID_REWARD'",
    ).param("account", accountId).query(Long::class.java).single()

    private fun ticketBalance(accountId: UUID): Long = jdbc.sql(
        "select cosmetic_ticket_balance from character where account_id=:account",
    ).param("account", accountId).query(Long::class.java).single()

    private fun slotStatuses(accountId: UUID): List<RaidSlotStatus> = jdbc.sql(
        "select status from raid_reward_slot where account_id=:account order by ordinal",
    ).param("account", accountId).query(String::class.java).list().map(RaidSlotStatus::valueOf)
}
