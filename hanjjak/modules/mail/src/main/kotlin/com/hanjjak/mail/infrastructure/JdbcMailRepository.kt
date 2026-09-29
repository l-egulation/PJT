package com.hanjjak.mail.infrastructure

import com.hanjjak.mail.application.SettlementMailRequest
import com.hanjjak.mail.application.MailRepository
import com.hanjjak.mail.domain.MailMessage
import org.springframework.jdbc.core.simple.JdbcClient
import java.sql.Timestamp
import java.time.Instant
import java.util.UUID

class JdbcMailRepository(private val jdbc: JdbcClient) : MailRepository {
    override fun stateVersion(accountId: UUID): Long = jdbc.sql("select state_version from account where id=:account")
        .param("account", accountId)
        .query(Long::class.java)
        .single()

    override fun command(accountId: UUID, idempotencyKey: UUID): MailRepository.StoredCommand? = jdbc.sql("select command_id,fingerprint,result_json::text from command_record where account_id=:account and idempotency_key=:key")
        .param("account", accountId).param("key", idempotencyKey)
        .query { row, _ -> MailRepository.StoredCommand(row.getObject("command_id", UUID::class.java), row.getString("fingerprint"), row.getString("result_json")) }
        .optional().orElse(null)

    override fun saveCommand(commandId: UUID, accountId: UUID, idempotencyKey: UUID, fingerprint: String, resultJson: String) {
        jdbc.sql("insert into command_record(command_id,account_id,idempotency_key,fingerprint,status,result_json,expires_at) values (:command,:account,:key,:fingerprint,'SUCCEEDED',cast(:result as jsonb),now()+interval '7 days')")
            .params(mapOf("command" to commandId, "account" to accountId, "key" to idempotencyKey, "fingerprint" to fingerprint, "result" to resultJson))
            .update()
    }

    override fun createSettlementMail(accountId: UUID, tradeId: UUID, riceAmount: Long): UUID {
        require(riceAmount >= 0) { "INVALID_RICE_AMOUNT" }
        val mailId = UUID.randomUUID()
        jdbc.sql("insert into mail_message(mail_id,account_id,type,rice_amount,source_trade_id,claimed,created_at) values (:mail,:account,'MARKET_SETTLEMENT',:rice,:trade,false,now())")
            .params(mapOf("mail" to mailId, "account" to accountId, "rice" to riceAmount, "trade" to tradeId))
            .update()
        return mailId
    }

    override fun createSettlementMails(requests: List<SettlementMailRequest>): List<UUID> {
        if (requests.isEmpty()) return emptyList()
        requests.forEach { require(it.riceAmount >= 0) { "INVALID_RICE_AMOUNT" } }
        val mailIds = requests.map { UUID.randomUUID() }
        // 왕복을 줄이려고 한 문장으로 모아 넣는다. 값 자체는 한 건씩 넣을 때와 같다.
        val values = requests.indices.joinToString(",") { i ->
            "(:mail$i,:account$i,'MARKET_SETTLEMENT',:rice$i,:trade$i,false,now())"
        }
        val params = mutableMapOf<String, Any>()
        requests.forEachIndexed { i, request ->
            params["mail$i"] = mailIds[i]
            params["account$i"] = request.accountId
            params["rice$i"] = request.riceAmount
            params["trade$i"] = request.tradeId
        }
        jdbc.sql("insert into mail_message(mail_id,account_id,type,rice_amount,source_trade_id,claimed,created_at) values $values")
            .params(params).update()
        return mailIds
    }

    override fun list(accountId: UUID, claimable: Boolean?, cursor: Instant?, limit: Int): List<MailMessage> {
        val where = buildString {
            append("account_id=:account")
            if (claimable == true) append(" and claimed=false")
            if (claimable == false) append(" and claimed=true")
            if (cursor != null) append(" and created_at < :cursor")
        }
        val params = mutableMapOf<String, Any>("account" to accountId, "limit" to limit)
        if (cursor != null) params["cursor"] = Timestamp.from(cursor)
        return jdbc.sql("select mail_id,account_id,type,rice_amount,claimed,created_at,claimed_at from mail_message where $where order by created_at desc, mail_id desc limit :limit")
            .params(params)
            .query(::mapMail)
            .list()
    }
    override fun count(accountId: UUID, claimable: Boolean?): Long {
        val claimedClause = when (claimable) { true -> " and claimed=false"; false -> " and claimed=true"; null -> "" }
        return jdbc.sql("select count(*) from mail_message where account_id=:account$claimedClause")
            .param("account", accountId).query(Long::class.java).single()
    }

    override fun lockClaimable(accountId: UUID, mailId: UUID): MailMessage? = jdbc.sql("select mail_id,account_id,type,rice_amount,claimed,created_at,claimed_at from mail_message where account_id=:account and mail_id=:mail and claimed=false for update")
        .params(mapOf("account" to accountId, "mail" to mailId))
        .query(::mapMail)
        .optional().orElse(null)

    override fun lockClaimableAll(accountId: UUID): List<MailMessage> = jdbc.sql("select mail_id,account_id,type,rice_amount,claimed,created_at,claimed_at from mail_message where account_id=:account and claimed=false order by created_at, mail_id for update")
        .param("account", accountId)
        .query(::mapMail)
        .list()

    override fun markClaimed(mailIds: List<UUID>, claimedAt: Instant) {
        if (mailIds.isEmpty()) return
        jdbc.sql("update mail_message set claimed=true, claimed_at=:claimedAt where mail_id in (:mailIds) and claimed=false")
            .params(mapOf("mailIds" to mailIds, "claimedAt" to Timestamp.from(claimedAt)))
            .update()
    }

    private fun mapMail(row: java.sql.ResultSet, rowNum: Int): MailMessage = MailMessage(
        mailId = row.getObject("mail_id", UUID::class.java),
        accountId = row.getObject("account_id", UUID::class.java),
        type = row.getString("type"),
        riceAmount = row.getLong("rice_amount"),
        claimed = row.getBoolean("claimed"),
        createdAt = row.getTimestamp("created_at").toInstant(),
        claimedAt = row.getTimestamp("claimed_at")?.toInstant(),
    )
}
