package com.hanjjak.gameapi

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.battle.application.FirstClearRewardService
import com.hanjjak.battle.application.FirstClearRewardPresenter
import com.hanjjak.inventory.application.InventoryRepository
import com.hanjjak.progression.application.ProgressionRewardCatalog
import com.hanjjak.progression.infrastructure.JsonProgressionContent
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.boot.test.context.SpringBootTest
import org.springframework.boot.test.context.TestConfiguration
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Import
import org.springframework.context.annotation.Primary
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.test.context.DynamicPropertyRegistry
import org.springframework.test.context.DynamicPropertySource
import org.testcontainers.containers.PostgreSQLContainer
import org.testcontainers.junit.jupiter.Container
import org.testcontainers.junit.jupiter.Testcontainers
import java.io.ByteArrayInputStream
import java.util.UUID
import java.util.concurrent.CountDownLatch
import java.util.concurrent.Executors
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertTrue

@SpringBootTest
@Testcontainers(disabledWithoutDocker = true)
@Import(FirstClearRewardIntegrationTest.ContentConfiguration::class)
class FirstClearRewardIntegrationTest {
    @Autowired lateinit var jdbc: JdbcClient
    @Autowired lateinit var rewards: FirstClearRewardService
    @Autowired lateinit var presenter: FirstClearRewardPresenter
    @Autowired lateinit var inventory: InventoryRepository

    @Test
    fun `first-clear service delivers rice and claimed item bundle exactly once`() {
        val account = account(0)
        val source = UUID.randomUUID()
        val result = rewards.apply(account, "stage.test", "v1", source)!!
        assertEquals(100, result.riceGranted)
        assertEquals("CLAIMED", result.itemStatus.name)
        assertEquals(1, jdbc.sql("select quantity from inventory_stack where account_id=:account and item_id='POTATO_M1'").param("account", account).query(Long::class.java).single())
        assertEquals(null, rewards.apply(account, "stage.test", "v1", source))
    }

    @Test
    fun `full inventory records pending and claim becomes atomic after capacity frees`() {
        val account = account(200)
        val pending = rewards.apply(account, "stage.test", "v1", UUID.randomUUID())!!
        assertEquals("PENDING", pending.itemStatus.name)
        assertEquals(0, jdbc.sql("select count(*) from inventory_stack where account_id=:account and item_id='POTATO_M1'").param("account", account).query(Int::class.java).single())
        jdbc.sql("delete from inventory_instance where account_id=:account and instance_id=(select instance_id from inventory_instance where account_id=:account limit 1)")
            .param("account", account).update()
        val key = UUID.randomUUID()
        assertEquals(1, jdbc.sql("select state_version from account where id=:account").param("account", account).query(Long::class.java).single())
        val claimed = rewards.claim(account, pending.rewardId, key)
        val claimedView = presenter.present(claimed)
        assertTrue(claimed.claimedAt != null)
        assertEquals(2, jdbc.sql("select state_version from account where id=:account").param("account", account).query(Long::class.java).single())
        jdbc.sql("delete from inventory_instance where account_id=:account and instance_id=(select instance_id from inventory_instance where account_id=:account limit 1)")
            .param("account", account).update()
        val replay = rewards.claim(account, pending.rewardId, key)
        assertEquals(claimed, replay)
        assertEquals(claimedView, presenter.present(replay))
        assertEquals(2, jdbc.sql("select state_version from account where id=:account").param("account", account).query(Long::class.java).single())
        assertEquals(1, jdbc.sql("select quantity from inventory_stack where account_id=:account and item_id='POTATO_M1'").param("account", account).query(Long::class.java).single())
    }

    @Test
    fun `claim while capacity remains full returns current slot facts without mutation`() {
        val account = account(200)
        val pending = rewards.apply(account, "stage.test", "v1", UUID.randomUUID())!!
        val error = assertFailsWith<com.hanjjak.account.api.FirstClearRewardCapacityException> {
            rewards.claim(account, pending.rewardId, UUID.randomUUID())
        }

        assertEquals(1, error.requiredSlots)
        assertEquals(0, error.availableSlots)
        assertEquals(1, error.missingSlots)
        assertEquals(0L, jdbc.sql("select count(*) from inventory_stack where account_id=:account and item_id='POTATO_M1'")
            .param("account", account).query(Long::class.java).single())
    }

    @Test
    fun `concurrent new claim keys have one winner`() {
        val account = account(200)
        val pending = rewards.apply(account, "stage.test", "v1", UUID.randomUUID())!!
        jdbc.sql("delete from inventory_instance where account_id=:account and instance_id=(select instance_id from inventory_instance where account_id=:account limit 1)")
            .param("account", account).update()
        val executor = Executors.newFixedThreadPool(2)
        val ready = CountDownLatch(2)
        val start = CountDownLatch(1)
        val futures = listOf(UUID.randomUUID(), UUID.randomUUID()).map { key ->
            executor.submit<Pair<Boolean, String?>> {
                ready.countDown()
                start.await()
                try {
                    rewards.claim(account, pending.rewardId, key)
                    true to null
                } catch (exception: IllegalArgumentException) {
                    false to exception.message
                }
            }
        }
        ready.await()
        start.countDown()
        val results = try {
            futures.map { it.get() }
        } finally {
            executor.shutdown()
        }
        assertEquals(1, results.count { it.first })
        assertTrue(results.any { it.second == "FIRST_CLEAR_REWARD_ALREADY_CLAIMED" })
    }

    private fun account(occupied: Int): UUID {
        val account = UUID.randomUUID()
        jdbc.sql("insert into account(id,email,password_hash,state_version,created_at) values (:a,:email,'test',1,now())")
            .params(mapOf("a" to account, "email" to "$account@test.local")).update()
        jdbc.sql("insert into character(id,account_id,level,experience,rice,nickname) values (gen_random_uuid(),:a,1,0,0,:nickname)").params(mapOf("a" to account, "nickname" to "test-${account.toString().take(8)}")).update()
        repeat(occupied) { jdbc.sql("insert into inventory_instance(instance_id,account_id,item_id,reserved_for_sale,acquired_sequence) values (gen_random_uuid(),:a,'gem:1:flat_attack',false,:seq)").params(mapOf("a" to account, "seq" to it + 1)).update() }
        return account
    }

    @TestConfiguration
    class ContentConfiguration {
        @Bean
        @Primary
        fun firstClearCatalog(mapper: ObjectMapper): ProgressionRewardCatalog {
            val json = """{"authority":"applied","contentVersion":"v1","stages":[{"stageId":"stage.test","normalExperience":0,"bossExperience":0,"normalRice":0,"bossRice":0,"firstClear":{"rice":100,"items":[{"itemId":"POTATO_M1","quantity":1}],"directSkillId":null}}]}"""
            val content = JsonProgressionContent(ByteArrayInputStream(json.toByteArray()), mapper)
            return ProgressionRewardCatalog(mapOf("v1" to content))
        }
    }

    companion object {
        @Container @JvmStatic val postgres = PostgreSQLContainer<Nothing>("postgres:17-alpine")
        @DynamicPropertySource @JvmStatic fun properties(registry: DynamicPropertyRegistry) {
            registry.add("spring.datasource.url", postgres::getJdbcUrl)
            registry.add("spring.datasource.username", postgres::getUsername)
            registry.add("spring.datasource.password", postgres::getPassword)
        }
    }
}
