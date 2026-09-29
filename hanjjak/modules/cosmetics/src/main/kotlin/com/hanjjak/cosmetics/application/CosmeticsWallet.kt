package com.hanjjak.cosmetics.application

import java.util.UUID

interface CosmeticsWallet {
    data class Balance(val rice: Long, val tickets: Long, val unlocked: Boolean)
    data class Milestone(val totalSuccessfulDraws: Long, val claimedBoxCount: Long, val ownedBoxQuantity: Long) {
        val claimableBoxCount: Long get() = totalSuccessfulDraws / 200 - claimedBoxCount
        val drawsUntilNextBox: Long get() = if (claimableBoxCount > 0) 0 else 200 - totalSuccessfulDraws % 200
    }
    fun balanceForUpdate(accountId: UUID): Balance
    fun milestone(accountId: UUID, bannerId: String, boxItemId: String): Milestone
    fun debit(accountId: UUID, rice: Long, tickets: Long, sourceId: UUID)
    fun addDraws(accountId: UUID, bannerId: String, count: Int)
    fun claimBoxes(accountId: UUID, bannerId: String, boxItemId: String, count: Long): Long
    fun openBox(accountId: UUID, boxItemId: String)
    /** Caller owns the account row lock; validates ticket capacity without bumping state_version. */
    fun preflightCreditTicketsLocked(accountId: UUID, amount: Long)
    /** Caller owns the account row lock; applies tickets without reacquiring account/version. */
    fun creditTicketsLocked(accountId: UUID, amount: Long, sourceId: UUID)
}
