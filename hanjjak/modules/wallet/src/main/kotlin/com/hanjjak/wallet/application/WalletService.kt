package com.hanjjak.wallet.application

import org.springframework.transaction.annotation.Transactional
import java.util.UUID

open class WalletService(private val repository: WalletRepository) {
    @Transactional
    open fun debit(accountId: UUID, amount: Long, sourceType: String, sourceId: UUID, bumpStateVersion: Boolean = true): Long {
        require(amount > 0) { "INVALID_RICE_AMOUNT" }
        require(sourceType.isNotBlank()) { "INVALID_SOURCE_TYPE" }
        val current = repository.lockBalance(accountId)
        require(current >= amount) { "INSUFFICIENT_RICE" }
        return repository.applyDelta(accountId, -amount, sourceType, sourceId, bumpStateVersion)
    }

    /** 여러 건을 모아 차감한다. 잔액 update 횟수를 줄이려는 용도이고 ledger는 건별로 남는다. */
    @Transactional
    open fun debitBatched(accountId: UUID, entries: List<WalletDebitEntry>, bumpStateVersion: Boolean = true): Long {
        require(entries.isNotEmpty()) { "EMPTY_DEBIT_ENTRIES" }
        entries.forEach {
            require(it.amount > 0) { "INVALID_RICE_AMOUNT" }
            require(it.sourceType.isNotBlank()) { "INVALID_SOURCE_TYPE" }
        }
        // applyBatchedDebit가 잔액 조건을 포함한 원자 UPDATE로 검증·차감한다.
        // 구매 경로는 이미 wallet.balance로 같은 행을 잠갔으므로 여기서 다시 SELECT FOR UPDATE하지 않는다.
        return repository.applyBatchedDebit(accountId, entries, bumpStateVersion)
    }

    @Transactional
    open fun credit(accountId: UUID, amount: Long, sourceType: String, sourceId: UUID, bumpStateVersion: Boolean = true): Long {
        require(amount > 0) { "INVALID_RICE_AMOUNT" }
        require(sourceType.isNotBlank()) { "INVALID_SOURCE_TYPE" }
        repository.lockBalance(accountId)
        return repository.applyDelta(accountId, amount, sourceType, sourceId, bumpStateVersion)
    }
    /**
     * Checks checked Long capacity while the caller-owned wallet row lock is held.
     * This intentionally does not acquire the account lock or increment state_version.
     */
    open fun preflightCreditLocked(accountId: UUID, amount: Long) {
        require(amount >= 0) { "INVALID_RICE_AMOUNT" }
        if (amount == 0L) return
        val current = repository.lockBalance(accountId)
        Math.addExact(current, amount)
    }

    /** Applies a previously preflighted credit under the caller's transaction locks. */
    open fun creditLocked(accountId: UUID, amount: Long, sourceType: String, sourceId: UUID): Long {
        require(amount >= 0) { "INVALID_RICE_AMOUNT" }
        require(sourceType.isNotBlank()) { "INVALID_SOURCE_TYPE" }
        if (amount == 0L) return repository.lockBalance(accountId)
        return repository.applyDelta(accountId, amount, sourceType, sourceId, bumpStateVersion = false)
    }


    open fun balance(accountId: UUID): Long = repository.lockBalance(accountId)
}
