package com.hanjjak.mail.application

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.mail.domain.MailClaimAllResult
import com.hanjjak.mail.domain.MailClaimResult
import com.hanjjak.mail.domain.MailCommandResult
import com.hanjjak.mail.domain.MailPage
import com.hanjjak.wallet.application.WalletService
import org.springframework.transaction.annotation.Transactional
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.time.Clock
import java.time.Instant
import java.util.UUID

open class MailService(
    private val repository: MailRepository,
    private val wallet: WalletService,
    private val mapper: ObjectMapper,
    private val clock: Clock,
) {
    open fun stateVersion(accountId: UUID): Long = repository.stateVersion(accountId)

    open fun createSettlementMail(accountId: UUID, tradeId: UUID, riceAmount: Long): UUID =
        repository.createSettlementMail(accountId, tradeId, riceAmount)

    /** 정산 메일을 모아서 한 번에 만든다. 구매 트랜잭션의 DB 왕복을 줄이는 용도다. */
    open fun createSettlementMails(requests: List<SettlementMailRequest>): List<UUID> =
        repository.createSettlementMails(requests)

    open fun list(accountId: UUID, claimable: Boolean?, cursor: Instant?, limit: Int = 30): MailPage {
        require(limit in 1..30) { "INVALID_LIMIT" }
        val rows = repository.list(accountId, claimable, cursor, limit + 1)
        return MailPage(rows.take(limit), rows.getOrNull(limit)?.createdAt?.toString(), repository.count(accountId, claimable))
    }
    open fun count(accountId: UUID, claimable: Boolean?): Long = repository.count(accountId, claimable)


    @Transactional
    open fun claim(accountId: UUID, idempotencyKey: UUID, mailId: UUID): MailCommandResult<MailClaimResult> {
        val fingerprint = sha256("mail-claim\u0000$mailId")
        repository.command(accountId, idempotencyKey)?.let { existing ->
            require(existing.fingerprint == fingerprint) { "IDEMPOTENCY_KEY_REUSED" }
            return MailCommandResult(existing.commandId, idempotencyKey, result = mapper.readValue(existing.resultJson, MailClaimResult::class.java))
        }
        val mail = repository.lockClaimable(accountId, mailId) ?: throw IllegalArgumentException("MAIL_NOT_CLAIMABLE")
        val claimedAt = clock.instant()
        repository.markClaimed(listOf(mailId), claimedAt)
        val balance = if (mail.riceAmount > 0) wallet.credit(accountId, mail.riceAmount, "MAIL_CLAIM", mailId) else wallet.balance(accountId)
        val commandId = UUID.randomUUID()
        val result = MailClaimResult(mailId, mail.riceAmount, claimedAt, balance)
        repository.saveCommand(commandId, accountId, idempotencyKey, fingerprint, mapper.writeValueAsString(result))
        return MailCommandResult(commandId, idempotencyKey, result = result)
    }

    @Transactional
    open fun claimAll(accountId: UUID, idempotencyKey: UUID): MailCommandResult<MailClaimAllResult> {
        val fingerprint = sha256("mail-claim-all")
        repository.command(accountId, idempotencyKey)?.let { existing ->
            require(existing.fingerprint == fingerprint) { "IDEMPOTENCY_KEY_REUSED" }
            return MailCommandResult(existing.commandId, idempotencyKey, result = mapper.readValue(existing.resultJson, MailClaimAllResult::class.java))
        }
        val mails = repository.lockClaimableAll(accountId)
        val mailIds = mails.map { it.mailId }
        val totalRice = mails.sumOf { it.riceAmount }
        val claimedAt = clock.instant()
        repository.markClaimed(mailIds, claimedAt)
        val commandId = UUID.randomUUID()
        val balance = if (totalRice > 0) wallet.credit(accountId, totalRice, "MAIL_CLAIM_ALL", commandId) else wallet.balance(accountId)
        val result = MailClaimAllResult(mailIds, totalRice, balance)
        repository.saveCommand(commandId, accountId, idempotencyKey, fingerprint, mapper.writeValueAsString(result))
        return MailCommandResult(commandId, idempotencyKey, result = result)
    }

    private fun sha256(value: String): String = MessageDigest.getInstance("SHA-256")
        .digest(value.toByteArray(StandardCharsets.UTF_8)).joinToString("") { "%02x".format(it) }
}
