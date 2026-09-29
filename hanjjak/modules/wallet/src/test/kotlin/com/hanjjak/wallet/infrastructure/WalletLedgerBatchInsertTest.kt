package com.hanjjak.wallet.infrastructure

import com.hanjjak.wallet.application.WalletDebitEntry
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertTrue

class WalletLedgerBatchInsertTest {
    @Test
    fun `build debit insert keeps per entry ledger balances`() {
        val accountId = UUID.fromString("00000000-0000-0000-0000-000000000001")
        val firstTradeId = UUID.fromString("00000000-0000-0000-0000-000000000101")
        val secondTradeId = UUID.fromString("00000000-0000-0000-0000-000000000102")
        val statement = WalletLedgerBatchInsert.buildDebit(
            accountId,
            100,
            listOf(
                WalletDebitEntry(30, "MARKET_PURCHASE", firstTradeId),
                WalletDebitEntry(20, "MARKET_PURCHASE", secondTradeId),
            ),
        )

        assertTrue(statement.sql.startsWith("insert into wallet_ledger"))
        assertEquals(2, Regex("cast\\(:ledger\\d+ as uuid\\)").findAll(statement.sql).count())
        assertEquals(accountId, statement.params["account0"])
        assertEquals(accountId, statement.params["account1"])
        assertEquals(-30L, statement.params["delta0"])
        assertEquals(70L, statement.params["balance0"])
        assertEquals(firstTradeId, statement.params["sourceId0"])
        assertEquals(-20L, statement.params["delta1"])
        assertEquals(50L, statement.params["balance1"])
        assertEquals(secondTradeId, statement.params["sourceId1"])
    }
}
