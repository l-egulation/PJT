package com.hanjjak.account.infrastructure

import com.hanjjak.account.application.LinkedSocialIdentity
import com.hanjjak.account.application.SocialIdentityRepository
import com.hanjjak.account.application.SocialProvider
import org.springframework.dao.DuplicateKeyException
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.stereotype.Repository
import java.time.Instant
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID

@Repository
class JdbcSocialIdentityRepository(private val jdbc: JdbcClient) : SocialIdentityRepository {
    override fun findAccountId(provider: SocialProvider, subject: String): UUID? = jdbc.sql(
        "select account_id from social_identity where provider = :provider and provider_subject = :subject",
    )
        .params(mapOf("provider" to provider.id, "subject" to subject))
        .query(UUID::class.java)
        .optional()
        .orElse(null)

    override fun link(accountId: UUID, provider: SocialProvider, subject: String, email: String?, now: Instant) {
        try {
            jdbc.sql(
                """
                insert into social_identity(provider, provider_subject, account_id, email, linked_at, last_login_at)
                values (:provider, :subject, :account, :email, :now, :now)
                """.trimIndent(),
            )
                .params(
                    mapOf(
                        "provider" to provider.id,
                        "subject" to subject,
                        "account" to accountId,
                        "email" to email,
                        "now" to OffsetDateTime.ofInstant(now, ZoneOffset.UTC),
                    ),
                )
                .update()
        } catch (_: DuplicateKeyException) {
            throw IllegalArgumentException("SOCIAL_IDENTITY_ALREADY_LINKED")
        }
    }

    override fun touch(provider: SocialProvider, subject: String, email: String?, now: Instant) {
        jdbc.sql(
            """
            update social_identity
            set email = :email, last_login_at = :now
            where provider = :provider and provider_subject = :subject
            """.trimIndent(),
        )
            .params(
                mapOf(
                    "provider" to provider.id,
                    "subject" to subject,
                    "email" to email,
                    "now" to OffsetDateTime.ofInstant(now, ZoneOffset.UTC),
                ),
            )
            .update()
    }

    override fun list(accountId: UUID): List<LinkedSocialIdentity> = jdbc.sql(
        "select provider, email, linked_at, last_login_at from social_identity where account_id = :account order by linked_at",
    )
        .param("account", accountId)
        .query { row, _ ->
            LinkedSocialIdentity(
                SocialProvider.fromId(row.getString("provider")),
                row.getString("email"),
                row.getObject("linked_at", OffsetDateTime::class.java).toInstant(),
                row.getObject("last_login_at", OffsetDateTime::class.java).toInstant(),
            )
        }
        .list()

    override fun unlink(accountId: UUID, provider: SocialProvider): Boolean = jdbc.sql(
        "delete from social_identity where account_id = :account and provider = :provider",
    )
        .params(mapOf("account" to accountId, "provider" to provider.id))
        .update() == 1

    override fun hasPassword(accountId: UUID): Boolean = jdbc.sql(
        "select password_hash is not null from account where id = :account and deleted_at is null",
    )
        .param("account", accountId)
        .query(Boolean::class.java)
        .optional()
        .orElse(false)
}
