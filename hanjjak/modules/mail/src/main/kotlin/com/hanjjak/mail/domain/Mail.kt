package com.hanjjak.mail.domain

import java.time.Instant
import java.util.UUID

data class MailMessage(
    val mailId: UUID,
    val accountId: UUID,
    val type: String,
    val riceAmount: Long,
    val claimed: Boolean,
    val createdAt: Instant,
    val claimedAt: Instant?,
)

data class MailPage(val items: List<MailMessage>, val nextCursor: String?, val totalItems: Long)
data class MailClaimResult(val mailId: UUID, val riceAmount: Long, val claimedAt: Instant, val walletBalance: Long)
data class MailClaimAllResult(val claimedMailIds: List<UUID>, val totalRiceAmount: Long, val walletBalance: Long)
data class MailCommandResult<T>(val commandId: UUID, val idempotencyKey: UUID, val status: String = "SUCCEEDED", val result: T)
