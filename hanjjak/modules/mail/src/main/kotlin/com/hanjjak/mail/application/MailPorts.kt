package com.hanjjak.mail.application

import com.hanjjak.mail.domain.MailMessage
import java.time.Instant
import java.util.UUID

data class SettlementMailRequest(val accountId: UUID, val tradeId: UUID, val riceAmount: Long)

interface MailRepository {
    data class StoredCommand(val commandId: UUID, val fingerprint: String, val resultJson: String)

    fun stateVersion(accountId: UUID): Long
    fun command(accountId: UUID, idempotencyKey: UUID): StoredCommand?
    fun saveCommand(commandId: UUID, accountId: UUID, idempotencyKey: UUID, fingerprint: String, resultJson: String)
    fun createSettlementMail(accountId: UUID, tradeId: UUID, riceAmount: Long): UUID

    /**
     * 정산 메일 여러 건을 한 번에 만든다. 반환 순서는 입력 순서와 같다.
     * 기본 구현은 한 건씩 만든다. 왕복을 줄이려는 구현만 재정의한다.
     */
    fun createSettlementMails(requests: List<SettlementMailRequest>): List<UUID> =
        requests.map { createSettlementMail(it.accountId, it.tradeId, it.riceAmount) }
    fun list(accountId: UUID, claimable: Boolean?, cursor: Instant?, limit: Int): List<MailMessage>
    fun count(accountId: UUID, claimable: Boolean?): Long = 0
    fun lockClaimable(accountId: UUID, mailId: UUID): MailMessage?
    fun lockClaimableAll(accountId: UUID): List<MailMessage>
    fun markClaimed(mailIds: List<UUID>, claimedAt: Instant)
}
