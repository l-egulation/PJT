package com.hanjjak.account.infrastructure

import com.hanjjak.account.application.MaterialPreferenceCommand
import com.hanjjak.account.application.MaterialPreferenceRepository
import com.hanjjak.account.domain.MaterialType
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.stereotype.Repository
import java.util.UUID

@Repository
class JdbcMaterialPreferenceRepository(private val jdbc: JdbcClient) : MaterialPreferenceRepository {
    override fun find(accountId: UUID): MaterialType? = jdbc
        .sql("select material_type from material_preference where account_id=:account")
        .param("account", accountId)
        .query(String::class.java)
        .optional()
        .map(MaterialType::valueOf)
        .orElse(null)

    override fun select(accountId: UUID, materialType: MaterialType): Boolean = jdbc
        .sql("insert into material_preference(account_id,material_type) values (:account,:material) on conflict(account_id) do nothing")
        .param("account", accountId)
        .param("material", materialType.name)
        .update() == 1

    override fun incrementStateVersion(accountId: UUID): Long = jdbc
        .sql("update account set state_version=state_version+1 where id=:account returning state_version")
        .param("account", accountId)
        .query(Long::class.java)
        .single()

    override fun stateVersion(accountId: UUID): Long = jdbc
        .sql("select state_version from account where id=:account")
        .param("account", accountId)
        .query(Long::class.java)
        .single()
    override fun populationCounts(): Map<MaterialType, Long> = jdbc.sql("select material_type, count(*) from material_preference group by material_type")
        .query { row, _ -> MaterialType.valueOf(row.getString("material_type")) to row.getLong("count") }
        .list()
        .toMap()

    override fun lockAccount(accountId: UUID) {
        jdbc.sql("select id from account where id=:account for update")
            .param("account", accountId)
            .query(UUID::class.java)
            .single()
    }

    override fun findCommand(accountId: UUID, idempotencyKey: UUID): MaterialPreferenceCommand? = jdbc
        .sql("select fingerprint from material_preference_command where account_id=:account and idempotency_key=:key")
        .params(mapOf("account" to accountId, "key" to idempotencyKey))
        .query(String::class.java)
        .optional()
        .map { MaterialPreferenceCommand(it) }
        .orElse(null)

    override fun saveCommand(accountId: UUID, idempotencyKey: UUID, fingerprint: String) {
        jdbc.sql("insert into material_preference_command(command_id,account_id,idempotency_key,fingerprint) values (:command,:account,:key,:fingerprint)")
            .params(mapOf("command" to UUID.randomUUID(), "account" to accountId, "key" to idempotencyKey, "fingerprint" to fingerprint))
            .update()
    }
}
