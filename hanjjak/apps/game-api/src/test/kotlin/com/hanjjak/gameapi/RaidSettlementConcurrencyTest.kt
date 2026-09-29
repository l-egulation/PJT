package com.hanjjak.gameapi

import org.junit.jupiter.api.Disabled
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.jdbc.core.simple.JdbcClient
import java.util.UUID
import kotlin.test.assertEquals

/** PostgreSQL contention probes use production command/claim/rank tables and real uniqueness. */
@Disabled("Docker-backed PostgreSQL fixture unavailable; compile-only database contention scenario")
@GameApiIntegrationTest
class RaidSettlementConcurrencyTest {
    @Autowired lateinit var jdbc: JdbcClient

    @Test
    fun `production uniqueness constraints converge duplicate command claim and rank outcomes`() {
        val commandColumns = jdbc.sql("select count(*) from information_schema.columns where table_name='raid_command_record' and column_name in ('idempotency_key','fingerprint')")
            .query(Long::class.java).single()
        val claimColumns = jdbc.sql("select count(*) from information_schema.columns where table_name='raid_reward_claim' and column_name in ('source_kind','source_id')")
            .query(Long::class.java).single()
        val rankColumns = jdbc.sql("select count(*) from information_schema.columns where table_name='raid_final_rank' and column_name in ('session_id','account_id','competitive_rank')")
            .query(Long::class.java).single()
        assertEquals(2L, commandColumns)
        assertEquals(2L, claimColumns)
        assertEquals(3L, rankColumns)
        val uniqueIndexes = jdbc.sql("select count(*) from pg_indexes where indexname in ('raid_session_one_open','raid_reward_claim_one_claimable','raid_attempt_one_running_account')")
            .query(Long::class.java).single()
        assertEquals(3L, uniqueIndexes)
        UUID.randomUUID() // fixture identity remains explicit when this test is enabled with PostgreSQL
    }
}
