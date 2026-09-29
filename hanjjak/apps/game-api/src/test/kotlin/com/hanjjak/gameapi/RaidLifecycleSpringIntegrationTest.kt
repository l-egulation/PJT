package com.hanjjak.gameapi

import com.hanjjak.raid.domain.RaidAttemptMode
import com.hanjjak.raid.application.RaidMainBattlePort
import com.hanjjak.raid.application.RaidService
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertThrows
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.boot.test.context.TestConfiguration
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Import
import org.springframework.context.annotation.Primary
import org.springframework.jdbc.core.simple.JdbcClient
import java.time.Instant
import java.util.UUID
import java.util.concurrent.CountDownLatch
import java.util.concurrent.Executors

@GameApiIntegrationTest
@Import(RaidLifecycleSpringIntegrationTest.Configuration::class)
class RaidLifecycleSpringIntegrationTest {
    @Autowired lateinit var raids: RaidService
    @Autowired lateinit var jdbc: JdbcClient
    @Autowired lateinit var handoff: ControlledRaidMainBattlePort

    @org.junit.jupiter.api.BeforeEach
    fun resetHandoff() {
        handoff.failPause = false
        handoff.handoff = null
        handoff.resumed.clear()
    }

    @Test
    fun `spring proxied concurrent same key replays one committed attempt`() {
        val accountId = fixture()
        val key = UUID.randomUUID()
        val start = CountDownLatch(1)
        val pool = Executors.newFixedThreadPool(2)
        try {
            val results = (1..2).map {
                pool.submit<com.hanjjak.raid.application.RaidAttemptView> {
                    start.await()
                    raids.startAttempt(accountId, key, RaidAttemptMode.REWARD)
                }
            }
            start.countDown()
            val views = results.map { it.get() }
            assertEquals(1, views.map { it.attemptId }.distinct().size)
            assertEquals(1, jdbc.sql("select count(*) from raid_attempt where account_id=:account").param("account", accountId).query(Int::class.java).single())
            assertEquals(1, jdbc.sql("select count(*) from raid_command_record where account_id=:account").param("account", accountId).query(Int::class.java).single())
        } finally {
            pool.shutdownNow()
        }
    }

    @Test
    fun `spring transaction rolls back state slots attempt and command when pause fails`() {

        val accountId = fixture()
        handoff.failPause = true
        try {
            assertThrows(IllegalStateException::class.java) {
                raids.startAttempt(accountId, UUID.randomUUID(), RaidAttemptMode.REWARD)
            }
        } finally {
            handoff.failPause = false
        }
        assertEquals(0, jdbc.sql("select count(*) from raid_account_state where account_id=:account").param("account", accountId).query(Int::class.java).single())
        assertEquals(0, jdbc.sql("select count(*) from raid_reward_slot where account_id=:account").param("account", accountId).query(Int::class.java).single())
        assertEquals(0, jdbc.sql("select count(*) from raid_attempt where account_id=:account").param("account", accountId).query(Int::class.java).single())
        assertEquals(0, jdbc.sql("select count(*) from raid_command_record where account_id=:account").param("account", accountId).query(Int::class.java).single())
    }

    private fun fixture(): UUID {
        val accountId = UUID.randomUUID()
        val sessionId = UUID.randomUUID()
        val now = Instant.now()
        jdbc.sql("insert into account(id,email,password_hash,state_version,created_at) values (:id,:email,'test',1,:now)")
            .params(mapOf("id" to accountId, "email" to "$accountId@test.local", "now" to java.sql.Timestamp.from(now))).update()
        jdbc.sql("insert into character(id,account_id,level,experience,rice,nickname) values (:id,:account,1,0,0,:nickname)")
            .params(mapOf("id" to UUID.randomUUID(), "account" to accountId, "nickname" to "raid-${accountId.toString().take(8)}")).update()
        jdbc.sql("update raid_session set status='SETTLED_FAILURE',cutoff_at=settles_at,settlement_phase='SETTLED',completed_at=now() where status='OPEN'").update()
        jdbc.sql("insert into raid_session(id,content_version,reward_version,settles_at,status) values (:id,'raid-mvp-v1-working','raid-mvp-v1-working',:settles,'OPEN')")
            .params(mapOf("id" to sessionId, "settles" to java.sql.Timestamp.from(now.plusSeconds(3600)))).update()
        jdbc.sql("insert into stage_progress(account_id,stage_id,unlocked,first_cleared_at,highest_clear_count,content_version) values (:account,'stage.01-02',true,:now,1,'v1')")
            .params(mapOf("account" to accountId, "now" to java.sql.Timestamp.from(now))).update()
        return accountId
    }

    @TestConfiguration(proxyBeanMethods = false)
    class Configuration {
        @Bean
        @Primary
        fun controlledRaidMainBattlePort() = ControlledRaidMainBattlePort()
    }
}

class ControlledRaidMainBattlePort : RaidMainBattlePort {
    @Volatile var failPause = false
    @Volatile var handoff: com.hanjjak.raid.application.RaidBattleHandoff? = null
    val resumed = java.util.concurrent.CopyOnWriteArrayList<com.hanjjak.raid.application.RaidBattleHandoff>()
    override fun pauseLockedWithHandoff(accountId: UUID, commandId: UUID) = if (failPause) error("PAUSE_FAILED") else handoff
    override fun resumeLocked(accountId: UUID, commandId: UUID, handoff: com.hanjjak.raid.application.RaidBattleHandoff): com.hanjjak.raid.application.RaidBattleHandoff? {
        resumed += handoff
        return null
    }
}
