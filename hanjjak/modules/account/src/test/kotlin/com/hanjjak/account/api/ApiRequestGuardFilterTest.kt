package com.hanjjak.account.api

import com.fasterxml.jackson.databind.ObjectMapper
import org.mockito.Mockito
import org.springframework.mock.web.MockFilterChain
import org.springframework.mock.web.MockHttpServletRequest
import org.springframework.mock.web.MockHttpServletResponse
import java.time.Clock
import java.time.Duration
import java.time.Instant
import java.time.ZoneOffset
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertTrue

class ApiRequestGuardFilterTest {
    private val clock = Clock.fixed(Instant.parse("2026-09-07T00:00:00Z"), ZoneOffset.UTC)
    private val properties = ApiSecurityProperties(
        allowedOrigins = listOf("http://localhost:5173"),
        authenticationRateLimit = ApiSecurityProperties.RateLimit(2, Duration.ofMinutes(1)),
        mutationRateLimit = ApiSecurityProperties.RateLimit(1, Duration.ofMinutes(1)),
        legacyBattleRewardApiEnabled = false,
    )
    private val sessions = Mockito.mock(AccountSessionRegistry::class.java)
    private val limiter = RequestRateLimiter(properties, clock)
    private val filter = ApiRequestGuardFilter(properties, sessions, limiter, ObjectMapper().findAndRegisterModules(), clock)

    @Test
    fun `mutation without origin is rejected`() {
        val request = apiRequest("POST", "/api/v1/auth/login")
        val response = MockHttpServletResponse()

        filter.doFilter(request, response, MockFilterChain())

        assertEquals(403, response.status)
        assertTrue(response.contentAsString.contains("ORIGIN_FORBIDDEN"))
    }
    @Test
    fun `economy metrics require authentication and reject player sessions`() {
        val anonymous = apiRequest("GET", "/api/v1/metrics/economy/daily")
        val anonymousResponse = MockHttpServletResponse()

        filter.doFilter(anonymous, anonymousResponse, MockFilterChain())

        assertEquals(401, anonymousResponse.status)
        assertTrue(anonymousResponse.contentAsString.contains("AUTHENTICATION_REQUIRED"))

        val accountId = UUID.randomUUID()
        val authenticated = apiRequest("POST", "/api/v1/metrics/economy/daily/refresh").apply {
            addHeader("Origin", "http://localhost:5173")
        }
        val session = authenticated.getSession(true)!!
        session.setAttribute("accountId", accountId)
        Mockito.`when`(sessions.isActive(accountId, session.id)).thenReturn(true)
        val authenticatedResponse = MockHttpServletResponse()

        filter.doFilter(authenticated, authenticatedResponse, MockFilterChain())

        assertEquals(403, authenticatedResponse.status)
        assertTrue(authenticatedResponse.contentAsString.contains("ECONOMY_METRICS_ADMIN_ONLY"))
    }


    @Test
    fun `configured development origin is accepted`() {
        val request = apiRequest("POST", "/api/v1/auth/login").apply {
            addHeader("Origin", "http://localhost:5173")
        }
        val response = MockHttpServletResponse()

        filter.doFilter(request, response, MockFilterChain())

        assertEquals(200, response.status)
    }

    @Test
    fun `legacy battle reward endpoints are blocked before reaching controllers`() {
        listOf(
            "/api/v1/battles/cycles",
            "/api/v1/battles/chapters/1/auto-run",
        ).forEach { uri ->
            val request = apiRequest("POST", uri).apply {
                addHeader("Origin", "http://localhost:5173")
            }
            val response = MockHttpServletResponse()

            filter.doFilter(request, response, MockFilterChain())

            assertEquals(403, response.status)
            assertTrue(response.contentAsString.contains("BATTLE_COMPATIBILITY_API_DISABLED"))
        }
    }

    @Test
    fun `same origin is accepted without an allowlist entry`() {
        val request = apiRequest("POST", "/api/v1/auth/login").apply {
            scheme = "https"
            serverName = "game.example.com"
            serverPort = 443
            addHeader("Origin", "https://game.example.com")
        }
        val response = MockHttpServletResponse()

        filter.doFilter(request, response, MockFilterChain())

        assertEquals(200, response.status)
    }

    @Test
    fun `mutation rate limit returns retry after`() {
        val first = apiRequest("POST", "/api/v1/auth/profile/nickname").apply {
            addHeader("Origin", "http://localhost:5173")
        }
        filter.doFilter(first, MockHttpServletResponse(), MockFilterChain())

        val second = apiRequest("POST", "/api/v1/auth/profile/nickname").apply {
            addHeader("Origin", "http://localhost:5173")
        }
        val response = MockHttpServletResponse()
        filter.doFilter(second, response, MockFilterChain())

        assertEquals(429, response.status)
        assertEquals("60", response.getHeader("Retry-After"))
        assertTrue(response.contentAsString.contains("RATE_LIMITED"))
    }

    @Test
    fun `replaced account session is invalidated before the request continues`() {
        val accountId = UUID.randomUUID()
        val request = apiRequest("GET", "/api/v1/auth/session")
        val session = request.getSession(true)!!
        session.setAttribute("accountId", accountId)
        Mockito.`when`(sessions.isActive(accountId, session.id)).thenReturn(false)
        val response = MockHttpServletResponse()

        filter.doFilter(request, response, MockFilterChain())

        assertFailsWith<IllegalStateException> { session.getAttribute("accountId") }
    }

    @Test
    fun `session is invalidated after its absolute lifetime`() {
        val accountId = UUID.randomUUID()
        val request = apiRequest("GET", "/api/v1/auth/session")
        val session = request.getSession(true)!!
        session.setAttribute("accountId", accountId)
        Mockito.`when`(sessions.isActive(accountId, session.id)).thenReturn(true)
        val expiredClock = Clock.fixed(
            Instant.ofEpochMilli(session.creationTime).plus(Duration.ofDays(31)),
            ZoneOffset.UTC,
        )
        val expiredFilter = ApiRequestGuardFilter(
            properties,
            sessions,
            RequestRateLimiter(properties, expiredClock),
            ObjectMapper().findAndRegisterModules(),
            expiredClock,
        )

        expiredFilter.doFilter(request, MockHttpServletResponse(), MockFilterChain())

        Mockito.verify(sessions).deactivate(accountId, session.id)
        assertFailsWith<IllegalStateException> { session.getAttribute("accountId") }
    }

    private fun apiRequest(method: String, uri: String) = MockHttpServletRequest(method, uri).apply {
        remoteAddr = "127.0.0.1"
        scheme = "http"
        serverName = "localhost"
        serverPort = 8080
    }
}
