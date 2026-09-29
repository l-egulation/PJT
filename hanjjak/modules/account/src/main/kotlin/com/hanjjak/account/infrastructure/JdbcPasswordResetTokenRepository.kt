package com.hanjjak.account.infrastructure

import com.hanjjak.account.application.PasswordResetToken
import com.hanjjak.account.application.PasswordResetTokenRepository
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.stereotype.Repository
import java.time.Instant
import java.time.OffsetDateTime
import java.time.ZoneOffset

@Repository
class JdbcPasswordResetTokenRepository(private val jdbc: JdbcClient) : PasswordResetTokenRepository {
    override fun replaceActive(accountId: java.util.UUID, token: PasswordResetToken) {
        jdbc.sql("select id from account where id = :account for update")
            .param("account", accountId)
            .query(java.util.UUID::class.java)
            .single()
        jdbc.sql("update password_reset_token set consumed_at = :now where account_id = :account and consumed_at is null")
            .params(mapOf("now" to offset(token.createdAt), "account" to accountId))
            .update()
        jdbc.sql(
            """
            insert into password_reset_token(id, account_id, token_hash, created_at, expires_at, consumed_at)
            values (:id, :account, :hash, :created, :expires, null)
            """.trimIndent(),
        ).params(
            mapOf(
                "id" to token.id,
                "account" to token.accountId,
                "hash" to token.tokenHash,
                "created" to offset(token.createdAt),
                "expires" to offset(token.expiresAt),
            ),
        ).update()
    }

    override fun consume(tokenHash: String, consumedAt: Instant): PasswordResetToken? = jdbc.sql(
        """
        update password_reset_token
        set consumed_at = :consumed
        where token_hash = :hash and consumed_at is null and expires_at > :consumed
        returning id, account_id, token_hash, created_at, expires_at, consumed_at
        """.trimIndent(),
    ).params(mapOf("hash" to tokenHash, "consumed" to offset(consumedAt)))
        .query { row, _ ->
            PasswordResetToken(
                row.getObject("id", java.util.UUID::class.java),
                row.getObject("account_id", java.util.UUID::class.java),
                row.getString("token_hash"),
                row.getObject("created_at", OffsetDateTime::class.java).toInstant(),
                row.getObject("expires_at", OffsetDateTime::class.java).toInstant(),
                row.getObject("consumed_at", OffsetDateTime::class.java)?.toInstant(),
            )
        }
        .optional()
        .orElse(null)

    override fun updatePassword(accountId: java.util.UUID, passwordHash: String): Long = jdbc.sql(
        """
        update account
        set password_hash = :passwordHash, state_version = state_version + 1
        where id = :account and deleted_at is null
        returning state_version
        """.trimIndent(),
    ).params(mapOf("account" to accountId, "passwordHash" to passwordHash))
        .query(Long::class.java)
        .optional()
        .orElseThrow { IllegalArgumentException("ACCOUNT_NOT_FOUND") }

    private fun offset(instant: Instant): OffsetDateTime = OffsetDateTime.ofInstant(instant, ZoneOffset.UTC)
}
