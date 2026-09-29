package com.hanjjak.gameapi

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.cosmetics.application.CosmeticsCommandRepository
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.stereotype.Component
import java.util.UUID

@Component
class JdbcCosmeticsCommandRepository(private val jdbc: JdbcClient, private val mapper: ObjectMapper) : CosmeticsCommandRepository {
    override fun incrementStateVersion(accountId: UUID): Long = jdbc.sql("update account set state_version=state_version+1 where id=:account returning state_version")
        .param("account", accountId).query(Long::class.java).single()
    override fun lockAccount(accountId: UUID) {
        jdbc.sql("select id from account where id=:account for update").param("account", accountId).query(UUID::class.java).single()
    }
    override fun find(accountId: UUID, key: UUID): CosmeticsCommandRepository.Record? = jdbc.sql("select fingerprint,result_json::text from command_record where account_id=:account and idempotency_key=:key")
        .param("account", accountId).param("key", key).query { row, _ -> CosmeticsCommandRepository.Record(row.getString("fingerprint"), row.getString("result_json")) }.optional().orElse(null)
    override fun save(accountId: UUID, key: UUID, fingerprint: String, resultJson: String) = save(accountId, key, fingerprint, resultJson, null)

    override fun save(accountId: UUID, key: UUID, fingerprint: String, resultJson: String, audit: com.hanjjak.cosmetics.application.CosmeticAudit?) {
        jdbc.sql("insert into command_record(command_id,account_id,idempotency_key,fingerprint,status,result_json,expires_at) values (:command,:account,:key,:fingerprint,'SUCCEEDED',cast(:result as jsonb),now()+interval '7 days')")
            .params(mapOf("command" to UUID.randomUUID(), "account" to accountId, "key" to key, "fingerprint" to fingerprint, "result" to resultJson)).update()
        audit?.let { value ->
            jdbc.sql("""insert into cosmetic_audit_event(event_id,account_id,idempotency_key,operation,content_version,request_fingerprint,result_json,algorithm_version,key_id,reproduction_token,details_json) values (:event,:account,:key,:operation,:version,:fingerprint,cast(:result as jsonb),:algorithm,:keyId,:token,cast(:details as jsonb))""")
                .params(mapOf("event" to UUID.randomUUID(), "account" to accountId, "key" to key, "operation" to value.operation.name, "version" to value.contentVersion, "fingerprint" to fingerprint, "result" to resultJson, "algorithm" to value.reproduction?.algorithmVersion, "keyId" to value.reproduction?.keyId, "token" to value.reproduction?.reproductionToken, "details" to mapper.writeValueAsString(value.details))).update()
        }
    }
}
