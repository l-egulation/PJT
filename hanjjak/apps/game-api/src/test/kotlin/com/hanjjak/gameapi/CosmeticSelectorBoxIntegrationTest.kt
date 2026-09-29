package com.hanjjak.gameapi

import com.hanjjak.cosmetics.domain.CosmeticsContent
import org.junit.jupiter.api.Assertions.*
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.transaction.PlatformTransactionManager
import org.springframework.transaction.support.TransactionTemplate
import java.util.UUID

@GameApiIntegrationTest
class CosmeticSelectorBoxIntegrationTest {
    @Autowired lateinit var jdbc: JdbcClient
    @Autowired lateinit var wallet: JdbcCosmeticsWallet
    @Autowired lateinit var content: CosmeticsContent
    @Autowired lateinit var transactions: PlatformTransactionManager

    @Test
    fun `claim read and open share inventory after legacy table removal`() {
        val account = account()
        val box = content.sets.mapNotNull { it.boxItemId }.first()
        TransactionTemplate(transactions).executeWithoutResult { wallet.claimBoxes(account, "test-banner", box, 2) }
        assertEquals(2L, wallet.milestone(account, "test-banner", box).ownedBoxQuantity)
        TransactionTemplate(transactions).executeWithoutResult { wallet.openBox(account, box) }
        assertEquals(1L, wallet.milestone(account, "test-banner", box).ownedBoxQuantity)
        assertEquals(1L, quantity(account, box))
        assertEquals(0L, jdbc.sql("select count(*) from information_schema.tables where table_schema='public' and table_name='cosmetic_selector_box'")
            .query(Long::class.java).single())
    }

    @Test
    fun `full inventory claim rolls back milestone progress`() {
        val account = account()
        jdbc.sql("insert into inventory_instance(instance_id,account_id,item_id,reserved_for_sale,acquired_sequence) select gen_random_uuid(),:account,'gem:1:flat_attack',false,n from generate_series(1,200) n")
            .param("account", account).update()
        val box = content.sets.mapNotNull { it.boxItemId }.first()
        val error = assertThrows(IllegalArgumentException::class.java) {
            TransactionTemplate(transactions).executeWithoutResult { wallet.claimBoxes(account, "test-banner", box, 1) }
        }
        assertEquals("INVENTORY_CAPACITY_EXCEEDED", error.message)
        assertEquals(0L, quantity(account, box))
        assertEquals(0L, jdbc.sql("select claimed_box_count from cosmetic_banner_progress where account_id=:account")
            .param("account", account).query(Long::class.java).single())
    }

    private fun quantity(account: UUID, box: String): Long = jdbc.sql("select quantity from inventory_stack where account_id=:account and item_id=:box")
        .param("account", account).param("box", box).query(Long::class.java).optional().orElse(0L)

    private fun account(): UUID {
        val account = UUID.randomUUID()
        jdbc.sql("insert into account(id,email,password_hash,state_version,created_at) values (:account,:email,'test',1,now())")
            .param("account", account).param("email", "$account@test.local").update()
        jdbc.sql("insert into cosmetic_banner_progress(account_id,banner_id,total_successful_draws,claimed_box_count) values (:account,'test-banner',400,0)")
            .param("account", account).update()
        return account
    }

}
