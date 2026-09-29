package com.hanjjak.admin.infrastructure

import org.h2.jdbcx.JdbcDataSource
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.jdbc.datasource.DataSourceTransactionManager
import java.time.Instant
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertNotNull

class AdminAuditRepositoryTest {
    @Test
    fun `mutation audit filters by administrator and keeps reason snapshots`() {
        val dataSource = JdbcDataSource().apply { setURL("jdbc:h2:mem:${System.nanoTime()};MODE=PostgreSQL;DB_CLOSE_DELAY=-1") }
        val jdbc = JdbcClient.create(dataSource)
        jdbc.sql("""
            create table admin_audit_event(
              audit_id uuid primary key, occurred_at timestamp with time zone, operator_id uuid, username varchar,
              action varchar, target_type varchar, target_id varchar, outcome varchar, request_id uuid,
              remote_address varchar, mutation boolean, reason varchar, before_summary varchar, after_summary varchar, idempotency_key uuid
            )
        """.trimIndent()).update()
        val repository = AdminAuditRepository(jdbc, DataSourceTransactionManager(dataSource))
        val operatorId = UUID.randomUUID()
        val key = UUID.randomUUID()
        repository.record(
            AdminAuditRepository.Record(
                Instant.now(), operatorId, "gitlab-user", "ADJUST_USER_RICE", "ACCOUNT", UUID.randomUUID().toString(), "SUCCEEDED",
                UUID.randomUUID(), "127.0.0.1", true, "고객 지원", "{\"rice\":0}", "{\"rice\":100}", key,
            ),
        )
        repository.record(AdminAuditRepository.Record(Instant.now(), null, "anonymous", "LOGIN", "ADMIN_SESSION", null, "SUCCEEDED", UUID.randomUUID(), "127.0.0.1"))

        val event = repository.list(10, operatorId, "ADJUST_USER_RICE", true).single()

        assertEquals("gitlab-user", event.username)
        assertEquals("고객 지원", event.reason)
        assertEquals(key, event.idempotencyKey)
        assertNotNull(event.beforeSummary)
        assertNotNull(event.afterSummary)
    }
}
