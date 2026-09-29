package com.hanjjak.gameapi

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.battle.application.ProgressionRebalanceBackfillService
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.jdbc.core.simple.JdbcClient
import java.util.UUID
import com.hanjjak.battle.domain.ProgressionBackfillReport
import org.junit.jupiter.api.BeforeEach
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertTrue

@GameApiIntegrationTest
class ProgressionBackfillIntegrationTest {
    @Autowired lateinit var jdbc: JdbcClient
    @Autowired lateinit var backfill: ProgressionRebalanceBackfillService

    @BeforeEach
    fun cleanAccounts() {
        jdbc.sql("truncate table account cascade").update()
    }

    @Test
    fun `failed lowest account does not starve the next backfill batch`() {
        val failing = UUID.fromString("00000000-0000-0000-0000-000000000001")
        val healthy = UUID.fromString("00000000-0000-0000-0000-000000000002")
        listOf(failing, healthy).forEach { account ->
            jdbc.sql("insert into account(id,email,password_hash,state_version,created_at) values (:account,:email,'test',1,now())")
                .params(mapOf("account" to account, "email" to "$account@test.local"))
                .update()
        }
        jdbc.sql(
            "update account_balance_state set balance_version='enemy-v1-applied',retroactive_completed_at=null " +
                "where account_id in (:failing,:healthy)",
        ).params(mapOf("failing" to failing, "healthy" to healthy)).update()
        jdbc.sql(
            "insert into stage_progress(account_id,stage_id,unlocked,first_cleared_at,highest_clear_count,content_version) " +
                "values (:account,'stage.invalid',true,now(),1,'enemy-v1-applied')",
        ).param("account", failing).update()

        val first = backfill.apply(1)
        val second = backfill.apply(1)

        assertEquals(listOf(failing), first.failedAccountIds)
        assertEquals(1, second.switchedAccountCount)
        assertEquals(
            "progression-rebalance-v1",
            jdbc.sql("select balance_version from account_balance_state where account_id=:account")
                .param("account", healthy).query(String::class.java).single(),
        )
        assertEquals(
            1,
            jdbc.sql("select backfill_failure_count from account_balance_state where account_id=:account")
                .param("account", failing).query(Int::class.java).single(),
        )
    }

    @Test
    fun `dry run pages every eligible account exactly once`() {
        repeat(3) { legacyAccount() }

        val preview = backfill.preview(batchSize = 1)

        assertEquals(3, preview.targetAccounts)
        assertEquals(0, preview.targetStages)
        assertEquals(0L, jdbc.sql("select count(*) from stage_first_clear_reward").query(Long::class.java).single())
    }

    @Test
    fun `rewardless final stage migrates without a reward row`() {
        val account = legacyAccount()
        jdbc.sql(
            "insert into stage_progress(account_id,stage_id,unlocked,first_cleared_at,highest_clear_count,content_version) " +
                "values (:account,'stage.04-10',true,now(),1,'enemy-v1-applied')",
        ).param("account", account).update()

        val preview = backfill.preview(1)
        val applied = backfill.apply(1)

        assertEquals(0, preview.immediateCandidates + preview.pendingCandidates)
        assertEquals(1, applied.switchedAccountCount)
        assertEquals(0L, jdbc.sql("select count(*) from stage_first_clear_reward where account_id=:account")
            .param("account", account).query(Long::class.java).single())
        assertEquals(2, jdbc.sql("select state_version from account where id=:account")
            .param("account", account).query(Long::class.java).single())
    }

    @Test
    fun `active legacy battle session defers migration until it closes`() {
        val account = legacyAccount()
        val gameSession = UUID.randomUUID()
        val battleSession = UUID.randomUUID()
        jdbc.sql("insert into game_session(id,account_id,status,created_at,last_heartbeat_at) values (:id,:account,'ACTIVE',now(),now())")
            .params(mapOf("id" to gameSession, "account" to account)).update()
        jdbc.sql(
            "insert into battle_session(id,account_id,game_session_id,token_hash,status,stage_id,content_version,seed,input_json,started_at,last_heartbeat_at,completable_at) " +
                "values (:id,:account,:game,'token','ACTIVE','stage.01-01','enemy-v1-applied',1,'{}',now(),now(),now())",
        ).params(mapOf("id" to battleSession, "account" to account, "game" to gameSession)).update()

        val deferred = backfill.apply(1)
        assertEquals(emptyList(), deferred.failedAccountIds)
        assertEquals(listOf(account), deferred.deferredAccountIds)
        assertEquals(0, jdbc.sql("select backfill_failure_count from account_balance_state where account_id=:account")
            .param("account", account).query(Int::class.java).single())
        assertEquals("enemy-v1-applied", balanceVersion(account))

        jdbc.sql("update battle_session set status='ABORTED',closed_at=now() where id=:id")
            .param("id", battleSession).update()
        val retried = backfill.apply(1)
        assertEquals(1, retried.switchedAccountCount)
        assertEquals(emptyList(), retried.deferredAccountIds)
        assertEquals("progression-rebalance-v1", balanceVersion(account))
    }

    private fun legacyAccount(): UUID = UUID.randomUUID().also { account ->
        jdbc.sql("insert into account(id,email,password_hash,state_version,created_at) values (:account,:email,'test',1,now())")
            .params(mapOf("account" to account, "email" to "$account@test.local")).update()
        jdbc.sql("update account_balance_state set balance_version='enemy-v1-applied',retroactive_completed_at=null where account_id=:account")
            .param("account", account).update()
    }

    private fun balanceVersion(account: UUID): String = jdbc.sql(
        "select balance_version from account_balance_state where account_id=:account",
    ).param("account", account).query(String::class.java).single()
    @Test
    fun `report serializes all backfill counters and failed accounts`() {
        val account = java.util.UUID.randomUUID()
        val report = ProgressionBackfillReport(
            targetAccounts = 2,
            targetStages = 40,
            alreadyApplied = 3,
            riceTotal = 4176,
            quantitiesByItem = mapOf("POTATO_M1" to 178L),
            immediateCandidates = 4,
            pendingCandidates = 1,
            failedAccountIds = listOf(account),
            deferredAccountIds = listOf(UUID(0, 2)),
            switchedAccountCount = 1,
        )
        val json = ObjectMapper().writeValueAsString(report)
        assertTrue(json.contains("\"targetAccounts\":2"))
        assertTrue(json.contains("\"riceTotal\":4176"))
        assertTrue(json.contains(account.toString()))
        assertTrue(json.contains("\"deferredAccountIds\":[\"00000000-0000-0000-0000-000000000002\"]"))
        assertEquals(2, report.targetAccountCount)
        assertEquals(40, report.targetStageCount)
    }
}
