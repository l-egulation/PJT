package com.hanjjak.account.infrastructure

import com.hanjjak.account.application.AuthenticationCommand
import com.hanjjak.account.application.AuthenticationCommandRepository
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.stereotype.Repository
import java.util.UUID

@Repository
class JdbcAuthenticationCommandRepository(private val jdbc: JdbcClient) : AuthenticationCommandRepository {
    override fun lock(idempotencyKey: UUID) {
        val most = idempotencyKey.mostSignificantBits
        val least = idempotencyKey.leastSignificantBits
        jdbc.sql("select pg_advisory_xact_lock(:first, :second)")
            .param("first", (most xor (most ushr 32)).toInt())
            .param("second", (least xor (least ushr 32)).toInt())
            .query { _, _ -> true }
            .single()
    }

    override fun find(idempotencyKey: UUID): AuthenticationCommand? = jdbc.sql("select idempotency_key, fingerprint, account_id, result_json->>'characterId' as character_id from command_record where idempotency_key = :key")
        .param("key", idempotencyKey)
        .query { row, _ -> AuthenticationCommand(row.getObject("idempotency_key", UUID::class.java), row.getString("fingerprint"), row.getObject("account_id", UUID::class.java), UUID.fromString(row.getString("character_id"))) }
        .optional().orElse(null)

    override fun save(command: AuthenticationCommand) {
        jdbc.sql("insert into command_record(command_id,account_id,idempotency_key,fingerprint,status,result_json,expires_at) values (:command,:account,:key,:fingerprint,'SUCCEEDED',cast(:result as jsonb),now()+interval '7 days')")
            .params(mapOf("command" to UUID.randomUUID(), "account" to command.accountId, "key" to command.idempotencyKey, "fingerprint" to command.fingerprint, "result" to "{\"characterId\":\"${command.characterId}\"}"))
            .update()
    }
}
