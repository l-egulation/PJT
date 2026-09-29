package com.hanjjak.account.infrastructure

import com.hanjjak.account.application.BalanceVersionRepository
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.stereotype.Repository
import java.time.Instant
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID

@Repository
class JdbcBalanceVersionRepository(private val jdbc: JdbcClient) : BalanceVersionRepository {
    override fun current(accountId: UUID): String? = jdbc.sql(
        "select balance_version from account_balance_state where account_id=:account",
    ).param("account", accountId).query(String::class.java).optional().orElse(null)

    override fun assign(accountId: UUID, balanceVersion: String, appliedAt: Instant): String = jdbc.sql(
        "insert into account_balance_state(account_id,balance_version,applied_at,retroactive_completed_at,provisioned_during_rollout) " +
            "values (:account,:version,:appliedAt,case when :version='progression-rebalance-v1' then :appliedAt else null end,false) on conflict(account_id) do update set " +
            "balance_version=excluded.balance_version,applied_at=excluded.applied_at," +
            "retroactive_completed_at=excluded.retroactive_completed_at,provisioned_during_rollout=false " +
            "where account_balance_state.provisioned_during_rollout returning balance_version",
    ).params(mapOf("account" to accountId, "version" to balanceVersion, "appliedAt" to OffsetDateTime.ofInstant(appliedAt, ZoneOffset.UTC)))
        .query(String::class.java).optional().orElseGet { current(accountId) ?: throw IllegalArgumentException("BALANCE_VERSION_MISSING") }
}
