package com.hanjjak.admin.application

import com.hanjjak.admin.api.AdminProperties
import com.hanjjak.admin.infrastructure.AdminRepository
import org.h2.jdbcx.JdbcDataSource
import org.springframework.jdbc.core.simple.JdbcClient
import java.time.Clock
import java.time.Instant
import java.time.ZoneOffset
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertNotNull
import kotlin.test.assertTrue

class AdminAuthenticationServiceTest {
    private val now = Instant.parse("2026-09-11T00:00:00Z")
    private val dataSource = JdbcDataSource().apply { setURL("jdbc:h2:mem:${System.nanoTime()};MODE=PostgreSQL;DB_CLOSE_DELAY=-1") }
    private val jdbc = JdbcClient.create(dataSource)
    private val repository = AdminRepository(jdbc)
    private val provider = RecordingGitlabProvider()
    private val properties = AdminProperties(
        gitlab = AdminProperties.Gitlab(
            enabled = true,
            clientId = "client-id",
            clientSecret = "client-secret",
            authorizationUri = "https://gitlab.example.com/oauth/authorize",
            tokenUri = "https://gitlab.example.com/oauth/token",
            userUri = "https://gitlab.example.com/api/v4/user",
            allowedUsernames = listOf("allowed-admin"),
            publicBaseUrl = "https://app.example.com",
            successUrl = "https://app.example.com",
            failureUrl = "https://app.example.com",
        ),
    )
    private val service = AdminAuthenticationService(repository, provider, properties, Clock.fixed(now, ZoneOffset.UTC))

    init {
        jdbc.sql("""
            create table admin_operator(
              operator_id uuid primary key,
              username varchar(255) not null unique,
              display_name varchar(120) not null,
              enabled boolean not null default true,
              gitlab_user_id bigint unique,
              updated_at timestamp with time zone not null default current_timestamp
            );
            create table admin_operator_role(operator_id uuid not null, role varchar(32) not null, primary key(operator_id, role));
            create table admin_session(session_token_hash varchar(64) primary key, operator_id uuid not null, created_at timestamp with time zone not null, last_seen_at timestamp with time zone not null, expires_at timestamp with time zone not null, revoked_at timestamp with time zone);
            create table admin_gitlab_oauth_flow(flow_id uuid primary key, state_token_hash varchar(64) not null unique, code_verifier varchar(128) not null, consumed boolean not null default false, created_at timestamp with time zone not null, expires_at timestamp with time zone not null);
        """.trimIndent()).update()
    }

    @Test
    fun `whitelisted GitLab user receives an admin session`() {
        val authorization = service.beginGitlabLogin()
        val state = queryParameter(authorization.url, "state")

        val login = service.completeGitlabLogin(state, "provider-code")

        assertEquals("allowed-admin", login.principal.username)
        assertEquals(setOf("ADMIN"), login.principal.roles.mapTo(linkedSetOf()) { it.name })
        assertNotNull(service.authenticate(login.token))
        assertEquals("read_user", queryParameter(authorization.url, "scope"))
        assertEquals("S256", queryParameter(authorization.url, "code_challenge_method"))
        assertTrue(provider.codeVerifier.isNotBlank())
    }

    @Test
    fun `non-whitelisted user is denied and state cannot be replayed`() {
        val authorization = service.beginGitlabLogin()
        val state = queryParameter(authorization.url, "state")
        provider.profile = AdminGitlabProfile(404, "outside-user", "Outside User", "active", false)

        val denied = assertFailsWith<IllegalArgumentException> {
            service.completeGitlabLogin(state, "provider-code")
        }
        val replay = assertFailsWith<IllegalArgumentException> {
            service.completeGitlabLogin(state, "provider-code")
        }

        assertEquals("ADMIN_GITLAB_ACCESS_DENIED", denied.message)
        assertEquals("ADMIN_GITLAB_STATE_INVALID", replay.message)
        assertEquals(0, jdbc.sql("select count(*) from admin_operator").query(Long::class.java).single())
    }

    private fun queryParameter(url: String, name: String): String = java.net.URI(url).rawQuery.split('&')
        .map { it.split('=', limit = 2) }
        .first { it[0] == name }[1]
        .let { java.net.URLDecoder.decode(it, Charsets.UTF_8) }

    private class RecordingGitlabProvider : AdminGitlabProvider {
        var profile = AdminGitlabProfile(314, "allowed-admin", "Allowed Admin", "active", false)
        var codeVerifier = ""
        override fun exchange(code: String, redirectUri: String, codeVerifier: String): AdminGitlabProfile {
            this.codeVerifier = codeVerifier
            return profile
        }
    }
}
