package com.hanjjak.account.api

import org.springframework.core.MethodParameter
import org.springframework.validation.BeanPropertyBindingResult
import org.springframework.validation.FieldError
import org.springframework.web.bind.MethodArgumentNotValidException
import org.springframework.web.bind.MissingRequestHeaderException
import org.springframework.web.method.annotation.MethodArgumentTypeMismatchException
import java.time.Clock
import java.time.Instant
import java.time.ZoneOffset
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals

class ApiExceptionHandlerTest {
    private val handler = ApiExceptionHandler(Clock.fixed(Instant.parse("2026-09-07T00:00:00Z"), ZoneOffset.UTC))
    private val parameter = MethodParameter(
        ApiExceptionHandlerTest::class.java.getDeclaredMethod("endpoint", AuthenticationController.SignupCredentials::class.java),
        0,
    )

    @Test
    fun `body validation error uses the common envelope without rejected secrets`() {
        val request = AuthenticationController.SignupCredentials("player@example.com", "short", "한짝")
        val binding = BeanPropertyBindingResult(request, "request").apply {
            addError(FieldError("request", "password", "short", false, arrayOf("Size"), emptyArray(), "too short"))
        }

        val response = handler.invalidBody(MethodArgumentNotValidException(parameter, binding))

        assertEquals(422, response.statusCode.value())
        assertEquals("PASSWORD_TOO_SHORT", response.body?.code)
        assertEquals("password", response.body?.details?.single()?.field)
        assertEquals(null, response.body?.details?.single()?.value)
    }

    @Test
    fun `missing idempotency key uses the common envelope`() {
        val response = handler.missingHeader(MissingRequestHeaderException("Idempotency-Key", parameter))

        assertEquals(400, response.statusCode.value())
        assertEquals("IDEMPOTENCY_KEY_REQUIRED", response.body?.code)
    }

    @Test
    fun `malformed idempotency key uses the common envelope`() {
        val exception = MethodArgumentTypeMismatchException("not-a-uuid", UUID::class.java, "idempotencyKey", parameter, null)
        val response = handler.invalidArgumentType(exception)

        assertEquals(400, response.statusCode.value())
        assertEquals("INVALID_IDEMPOTENCY_KEY", response.body?.code)
    }

    @Test
    fun `malformed ranking session id uses a declared raid code`() {
        val exception = MethodArgumentTypeMismatchException("not-a-uuid", UUID::class.java, "sessionId", parameter, null)
        val response = handler.invalidArgumentType(exception)

        assertEquals(400, response.statusCode.value())
        assertEquals("RAID_SESSION_STALE", response.body?.code)
    }

    @Test
    fun `raid internal errors map to declared raid codes with nullable details`() {
        val expected = mapOf(
            "INVALID_CURSOR" to "RAID_SESSION_STALE",
            "RAID_PRACTICE_CANNOT_CONFIRM" to "RAID_INVALID_TRANSITION",
            "RAID_CLAIM_STATE_CONFLICT" to "RAID_STATE_VERSION_CONFLICT",
            "RAID_COMMAND_IN_PROGRESS" to "RAID_ATTEMPT_RUNNING",
            "RAID_CONTENT_VERSION_UNAVAILABLE" to "RAID_CONTENT_LOCKED",
        )

        expected.forEach { (rawCode, declaredCode) ->
            val response = handler.invalidRequest(IllegalArgumentException(rawCode))
            assertEquals(declaredCode, response.body?.code)
            assertEquals(declaredCode, response.body?.details?.singleOrNull()?.code)
        }
    }

    @Suppress("UNUSED_PARAMETER")
    private fun endpoint(request: AuthenticationController.SignupCredentials) = Unit
}
