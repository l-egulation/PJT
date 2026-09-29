package com.hanjjak.wallet.infrastructure

import com.hanjjak.wallet.application.WalletDebitEntry
import com.hanjjak.wallet.application.WalletRepository
import org.springframework.jdbc.core.simple.JdbcClient
import java.util.UUID

class JdbcWalletRepository(private val jdbc: JdbcClient, private val batchLedgerInsert: Boolean = true) : WalletRepository {
    override fun lockBalance(accountId: UUID): Long = jdbc.sql("select balance from wallet_balance where account_id=:account for update")
        .param("account", accountId)
        .query(Long::class.java)
        .optional()
        .orElseThrow { IllegalArgumentException("WALLET_NOT_FOUND") }

    override fun applyBatchedDebit(accountId: UUID, entries: List<WalletDebitEntry>, bumpStateVersion: Boolean): Long {
        if (entries.isEmpty()) return lockBalance(accountId)
        val total = entries.sumOf { it.amount }
        // 잔액은 총액으로 한 번만 줄인다. 음수 방지 조건은 건별 차감 때와 같은 자리에 그대로 둔다.
        val updated = jdbc.sql("update wallet_balance set balance=balance-:total, updated_at=now() where account_id=:account and balance-:total>=0 returning balance")
            .params(mapOf("account" to accountId, "total" to total))
            .query(Long::class.java)
            .optional()
            .orElseThrow { IllegalArgumentException("INSUFFICIENT_RICE") }
        insertDebitLedger(accountId, updated + total, entries)
        if (bumpStateVersion) {
            jdbc.sql("update account set state_version=state_version+1 where id=:account")
                .param("account", accountId)
                .update()
        }
        return updated
    }

    private fun insertDebitLedger(accountId: UUID, balanceBefore: Long, entries: List<WalletDebitEntry>) {
        if (batchLedgerInsert) {
            val statement = WalletLedgerBatchInsert.buildDebit(accountId, balanceBefore, entries)
            jdbc.sql(statement.sql).params(statement.params).update()
            return
        }
        var running = balanceBefore
        entries.forEach { entry ->
            running -= entry.amount
            jdbc.sql("insert into wallet_ledger(ledger_id,account_id,delta,balance_after,source_type,source_id,created_at) values (:ledger,:account,:delta,:balance,:sourceType,:sourceId,now())")
                .params(mapOf("ledger" to UUID.randomUUID(), "account" to accountId, "delta" to -entry.amount, "balance" to running, "sourceType" to entry.sourceType, "sourceId" to entry.sourceId))
                .update()
        }
    }

    override fun applyDelta(accountId: UUID, delta: Long, sourceType: String, sourceId: UUID, bumpStateVersion: Boolean): Long {
        val updated = jdbc.sql("update wallet_balance set balance=balance+:delta, updated_at=now() where account_id=:account and balance+:delta>=0 returning balance")
            .params(mapOf("account" to accountId, "delta" to delta))
            .query(Long::class.java)
            .optional()
            .orElseThrow { IllegalArgumentException("INSUFFICIENT_RICE") }
        jdbc.sql("insert into wallet_ledger(ledger_id,account_id,delta,balance_after,source_type,source_id,created_at) values (:ledger,:account,:delta,:balance,:sourceType,:sourceId,now())")
            .params(mapOf("ledger" to UUID.randomUUID(), "account" to accountId, "delta" to delta, "balance" to updated, "sourceType" to sourceType, "sourceId" to sourceId))
            .update()
        if (bumpStateVersion) {
            jdbc.sql("update account set state_version=state_version+1 where id=:account")
                .param("account", accountId)
                .update()
        }
        return updated
    }
}

internal data class WalletLedgerBatchStatement(val sql: String, val params: Map<String, Any>)

internal object WalletLedgerBatchInsert {
    fun buildDebit(accountId: UUID, balanceBefore: Long, entries: List<WalletDebitEntry>): WalletLedgerBatchStatement {
        require(entries.isNotEmpty()) { "EMPTY_DEBIT_ENTRIES" }
        val params = mutableMapOf<String, Any>()
        var running = balanceBefore
        val values = entries.mapIndexed { index, entry ->
            running -= entry.amount
            params["ledger$index"] = UUID.randomUUID()
            params["account$index"] = accountId
            params["delta$index"] = -entry.amount
            params["balance$index"] = running
            params["sourceType$index"] = entry.sourceType
            params["sourceId$index"] = entry.sourceId
            "(cast(:ledger$index as uuid), cast(:account$index as uuid), cast(:delta$index as bigint), cast(:balance$index as bigint), cast(:sourceType$index as varchar), cast(:sourceId$index as uuid), now())"
        }.joinToString(",")
        return WalletLedgerBatchStatement(
            "insert into wallet_ledger(ledger_id,account_id,delta,balance_after,source_type,source_id,created_at) values $values",
            params,
        )
    }
}
