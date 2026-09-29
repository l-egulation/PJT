package com.hanjjak.progression.infrastructure

import com.hanjjak.progression.application.ProgressionRepository
import com.hanjjak.progression.domain.CharacterProgression
import org.springframework.jdbc.core.simple.JdbcClient
import java.util.UUID

class JdbcProgressionRepository(private val jdbc: JdbcClient) : ProgressionRepository {
    override fun lockAccount(accountId: UUID) {
        jdbc.sql("select id from account where id=:account for update")
            .param("account", accountId)
            .query(UUID::class.java)
            .optional()
            .orElseThrow { IllegalArgumentException("AUTHENTICATION_REQUIRED") }
    }

    override fun progression(accountId: UUID): CharacterProgression = jdbc.sql("select c.level,c.experience,w.balance as rice_balance from character c join wallet_balance w on w.account_id=c.account_id where c.account_id=:account for update")
        .param("account", accountId)
        .query { row, _ -> CharacterProgression(row.getInt("level"), row.getLong("experience"), row.getLong("rice_balance")) }
        .optional()
        .orElseThrow { IllegalArgumentException("CHARACTER_NOT_FOUND") }

    override fun saveProgression(accountId: UUID, progression: CharacterProgression) {
        jdbc.sql("update character set level=:level,experience=:experience where account_id=:account")
            .params(mapOf("account" to accountId, "level" to progression.level, "experience" to progression.experience))
            .update()
        jdbc.sql("update account set state_version=state_version+1 where id=:account")
            .param("account", accountId)
            .update()
    }

    override fun command(accountId: UUID, idempotencyKey: UUID): ProgressionRepository.StoredCommand? = jdbc.sql("select fingerprint,result_json::text from progression_reward_command where account_id=:account and idempotency_key=:key")
        .param("account", accountId)
        .param("key", idempotencyKey)
        .query { row, _ -> ProgressionRepository.StoredCommand(row.getString("fingerprint"), row.getString("result_json")) }
        .optional()
        .orElse(null)

    override fun saveCommand(accountId: UUID, idempotencyKey: UUID, fingerprint: String, resultJson: String) {
        jdbc.sql("insert into progression_reward_command(command_id,account_id,idempotency_key,fingerprint,result_json) values (:command,:account,:key,:fingerprint,cast(:result as jsonb))")
            .params(mapOf("command" to UUID.randomUUID(), "account" to accountId, "key" to idempotencyKey, "fingerprint" to fingerprint, "result" to resultJson))
            .update()
    }
}
