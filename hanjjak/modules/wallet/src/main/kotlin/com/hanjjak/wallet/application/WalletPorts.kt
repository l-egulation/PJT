package com.hanjjak.wallet.application

import java.util.UUID

data class WalletDebitEntry(val amount: Long, val sourceType: String, val sourceId: UUID)

interface WalletRepository {
    fun lockBalance(accountId: UUID): Long
    /** bumpStateVersion=false면 account.state_version 증가를 호출자에게 맡긴다. (실험용) */
    fun applyDelta(accountId: UUID, delta: Long, sourceType: String, sourceId: UUID, bumpStateVersion: Boolean = true): Long

    /**
     * 여러 건을 한 번에 차감한다. 잔액 update는 총액으로 1회만 하고, ledger는 건별로 남긴다.
     * ledger의 balance_after는 입력 순서대로 차감한 중간 잔액이다. 건별로 차감할 때와 값이 같다.
     */
    fun applyBatchedDebit(accountId: UUID, entries: List<WalletDebitEntry>, bumpStateVersion: Boolean = true): Long
}
