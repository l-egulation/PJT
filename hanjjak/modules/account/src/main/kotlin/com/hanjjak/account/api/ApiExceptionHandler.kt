package com.hanjjak.account.api

import com.fasterxml.jackson.databind.exc.InvalidFormatException
import com.hanjjak.account.domain.MaterialAlreadySelectedException
import com.hanjjak.account.domain.MaterialType
import com.hanjjak.account.api.FirstClearRewardCapacityException
import org.springframework.http.HttpStatus
import org.springframework.http.ResponseEntity
import org.springframework.http.converter.HttpMessageNotReadableException
import org.springframework.web.bind.MethodArgumentNotValidException
import org.springframework.web.bind.MissingRequestHeaderException
import org.springframework.web.method.annotation.MethodArgumentTypeMismatchException
import org.springframework.web.bind.annotation.ExceptionHandler
import org.springframework.web.bind.annotation.RestControllerAdvice
import java.time.Clock
import java.time.Instant
import java.util.UUID

@RestControllerAdvice
class ApiExceptionHandler(private val clock: Clock) {
    data class ErrorDetail(val field: String? = null, val code: String, val messageKey: String, val value: Any? = null)
    data class ErrorEnvelope(
        val requestId: UUID,
        val serverTime: Instant,
        val code: String,
        val messageKey: String,
        val retryable: Boolean,
        val details: List<ErrorDetail>? = null,
    )

    @ExceptionHandler(HttpMessageNotReadableException::class)
    fun unreadableRequest(exception: HttpMessageNotReadableException): ResponseEntity<ErrorEnvelope> {
        val invalidMaterial = generateSequence(exception.cause) { it.cause }
            .filterIsInstance<InvalidFormatException>()
            .any { it.targetType == MaterialType::class.java }
        val code = if (invalidMaterial || exception.message?.contains("materialType") == true) "INVALID_MATERIAL_TYPE" else "VALIDATION_FAILED"
        return error(HttpStatus.UNPROCESSABLE_ENTITY, code)
    }

    @ExceptionHandler(InvalidFormatException::class)
    fun invalidFormat(exception: InvalidFormatException): ResponseEntity<ErrorEnvelope> {
        val code = if (exception.targetType == MaterialType::class.java) "INVALID_MATERIAL_TYPE" else "VALIDATION_FAILED"
        return error(HttpStatus.UNPROCESSABLE_ENTITY, code)
    }

    @ExceptionHandler(MaterialAlreadySelectedException::class)
    fun materialAlreadySelected(exception: MaterialAlreadySelectedException): ResponseEntity<ErrorEnvelope> = error(
        HttpStatus.CONFLICT,
        "MATERIAL_ALREADY_SELECTED",
        listOf(ErrorDetail("primaryMaterialType", "MATERIAL_ALREADY_SELECTED", "error.material.already.selected", exception.current)),
    )

    @ExceptionHandler(FirstClearRewardCapacityException::class)
    fun firstClearRewardCapacity(exception: FirstClearRewardCapacityException): ResponseEntity<ErrorEnvelope> = error(
        HttpStatus.CONFLICT,
        "INVENTORY_CAPACITY_EXCEEDED",
        listOf(
            ErrorDetail("requiredSlots", "INVENTORY_CAPACITY_EXCEEDED", "error.inventory.capacity.exceeded", exception.requiredSlots),
            ErrorDetail("availableSlots", "INVENTORY_CAPACITY_EXCEEDED", "error.inventory.capacity.exceeded", exception.availableSlots),
            ErrorDetail("missingSlots", "INVENTORY_CAPACITY_EXCEEDED", "error.inventory.capacity.exceeded", exception.missingSlots),
        ),
    )

    @ExceptionHandler(IllegalArgumentException::class)
    fun invalidRequest(exception: IllegalArgumentException): ResponseEntity<ErrorEnvelope> {
        val rawCode = exception.message ?: "VALIDATION_FAILED"
        val code = when (rawCode) {
            "RAID_SESSION_NOT_OPEN", "RAID_SESSION_CLOSED", "RAID_SESSION_STALE", "RAID_SESSION_NOT_FOUND", "RAID_SESSION_NOT_SETTLING",
            "RAID_SETTLEMENT_PHASE_CONFLICT", "RAID_SETTLEMENT_CUTOFF_CONFLICT", "RAID_SETTLEMENT_ACCOUNT_CONFLICT", "RAID_SETTLEMENT_SLOT_COUNT_CONFLICT",
            "RAID_SETTLEMENT_MULTIPLE_REWARD_ATTEMPTS", "RAID_SETTLEMENT_ACCOUNTS_PENDING", "RAID_RANK_COMPLETENESS_FAILED", "RAID_RANK_CLAIM_COMPLETENESS_FAILED",
            "RAID_SESSION_STATE_CONFLICT", "RAID_ACCOUNT_STATE_CONFLICT", "RAID_SLOT_STATE_CONFLICT", "RAID_ATTEMPT_STATE_CONFLICT", "RAID_CONTRIBUTION_OVERFLOW",
            "RAID_SETTLEMENT_ACCOUNT_NOT_FOUND" -> "RAID_SESSION_STALE"
            "RAID_SESSION_SETTLED" -> "RAID_SESSION_SETTLED"
            "RAID_PRACTICE_CANNOT_CONFIRM", "RAID_PRACTICE_LOCKED", "RAID_ATTEMPT_NOT_CURRENT", "RAID_INVALID_TRANSITION", "RAID_ATTEMPT_NOT_FOUND", "RAID_SLOT_NOT_FOUND", "RAID_SETTLEMENT_SLOT_NOT_FOUND", "RAID_SETTLEMENT_ATTEMPT_NOT_CURRENT" -> "RAID_INVALID_TRANSITION"
            "RAID_COMMAND_REPLAY_RACE", "RAID_CLAIM_STATE_CONFLICT" -> "RAID_STATE_VERSION_CONFLICT"
            "RAID_COMMAND_IN_PROGRESS" -> "RAID_ATTEMPT_RUNNING"
            "RAID_CONTENT_VERSION_UNAVAILABLE" -> "RAID_CONTENT_LOCKED"
            "INVALID_COMMAND_ID", "INVALID_CLAIM_CURSOR", "INVALID_RANK_CURSOR", "INVALID_CURSOR" -> "RAID_SESSION_STALE"
            else -> when {
                rawCode.startsWith("RAID_") -> "RAID_INVALID_TRANSITION"
                rawCode.startsWith("INVALID_") -> "RAID_SESSION_STALE"
                else -> rawCode
            }
        }
        val status = when (code) {
            "AUTHENTICATION_REQUIRED", "LOGIN_FAILED" -> HttpStatus.UNAUTHORIZED
            "GACHA_CONTENT_UNAVAILABLE", "COSMETIC_SERVICE_UNAVAILABLE", "SOCIAL_PROVIDER_DISABLED", "RAID_MAIN_BATTLE_TRANSITION_FAILED" -> HttpStatus.SERVICE_UNAVAILABLE
            "ITEM_NOT_FOUND", "MARKET_INSTRUMENT_NOT_FOUND", "MARKET_ORDER_NOT_FOUND", "MARKET_DELIVERY_NOT_FOUND", "MAIL_NOT_FOUND", "BATTLE_SESSION_NOT_FOUND", "ACCOUNT_NOT_FOUND", "GACHA_BANNER_NOT_FOUND", "COSMETIC_NOT_FOUND", "SELECTOR_BOX_NOT_FOUND", "OFFLINE_REWARD_NOT_FOUND", "RAID_CLAIM_NOT_FOUND" -> HttpStatus.NOT_FOUND
            "COSMETIC_SYSTEM_LOCKED" -> HttpStatus.FORBIDDEN
            "EMAIL_ALREADY_EXISTS", "IDEMPOTENCY_KEY_REUSED", "MATERIAL_ALREADY_SELECTED", "GAME_SESSION_NOT_ACTIVE", "BATTLE_SESSION_CLOSED", "BATTLE_SESSION_EXPIRED", "BATTLE_SESSION_NOT_READY", "BATTLE_SETTLEMENT_NOT_READY", "MARKET_LIST_CHANGED", "MARKET_NO_FILL", "MARKET_ORDER_NOT_ACTIVE", "MARKET_DELIVERY_ALREADY_CLAIMED", "SELF_CROSS_NOT_ALLOWED", "MARKET_ACTIVE_ORDER_LIMIT", "MARKET_INSTRUMENT_INACTIVE", "ACCOUNT_ALREADY_DELETED", "INSUFFICIENT_GACHA_FUNDS", "COSMETIC_NOT_OWNED", "COSMETIC_ALREADY_MAX_STAR", "INSUFFICIENT_UNREGISTERED_COSMETICS", "COSMETIC_SLOT_MISMATCH", "NO_CLAIMABLE_SELECTOR_BOX", "MILESTONE_NOT_CLAIMABLE", "SELECTOR_BOX_NOT_OWNED", "COSMETIC_STATE_CONFLICT", "SOCIAL_IDENTITY_ALREADY_LINKED", "LAST_LOGIN_METHOD_REQUIRED", "RAID_CONTENT_LOCKED", "RAID_NO_AVAILABLE_SLOT", "RAID_ATTEMPT_RUNNING", "RAID_ATTEMPT_RESULT_HELD", "RAID_ATTEMPT_LIMIT_REACHED", "RAID_INVALID_TRANSITION", "RAID_ZERO_DAMAGE_CANNOT_CONFIRM", "RAID_REWARD_CAPACITY", "RAID_SESSION_STALE", "RAID_SESSION_SETTLED", "RAID_CLAIM_ALREADY_CLAIMED", "RAID_STATE_VERSION_CONFLICT" -> HttpStatus.CONFLICT
            "INVALID_CATEGORY", "INVALID_SORT", "INVALID_CURSOR", "INVALID_LIMIT", "INVALID_ITEM_ID", "INVALID_QUANTITY", "INVALID_UNIT_PRICE", "INVALID_LEVELS", "INVALID_UNREAD_SEQUENCE", "INVALID_STAGE_ID", "INVALID_ENEMY_INDEX", "BATTLE_SESSION_TOKEN_INVALID", "INVALID_PREDICTION_HASH", "INVALID_RENDERING_CHECKPOINT", "INVALID_DRAW_COUNT", "INVALID_REGISTRATION_MODE", "INVALID_EQUIPMENT_SLOT", "INVALID_CLAIM_QUANTITY", "INVALID_SELECTOR_COSMETIC", "PASSWORD_RESET_TOKEN_INVALID", "SOCIAL_PROVIDER_NOT_FOUND", "SOCIAL_STATE_INVALID", "SOCIAL_SIGNUP_TOKEN_INVALID" -> HttpStatus.BAD_REQUEST
            "MARKET_MUTATION_RATE_LIMITED" -> HttpStatus.TOO_MANY_REQUESTS
            "MATERIAL_PREFERENCE_UNAVAILABLE" -> HttpStatus.SERVICE_UNAVAILABLE
            else -> HttpStatus.UNPROCESSABLE_ENTITY
        }
        return error(status, code, details = listOf(ErrorDetail(null, code, "error.${code.lowercase().replace('_', '.')}")))
    }

    @ExceptionHandler(MethodArgumentNotValidException::class)
    fun invalidBody(exception: MethodArgumentNotValidException): ResponseEntity<ErrorEnvelope> {
        val details = exception.bindingResult.fieldErrors.map { fieldError ->
            val code = when (fieldError.field) {
                "email" -> "INVALID_EMAIL"
                "password", "newPassword" -> "PASSWORD_TOO_SHORT"
                "token" -> "PASSWORD_RESET_TOKEN_INVALID"
                "currentPassword" -> "CURRENT_PASSWORD_REQUIRED"
                "newPasswordConfirmation" -> "PASSWORD_CONFIRMATION_REQUIRED"
                "nickname" -> if (fieldError.code == "Size" && (fieldError.rejectedValue as? String)?.length?.let { it > 20 } == true) {
                    "NICKNAME_TOO_LONG"
                } else {
                    "NICKNAME_REQUIRED"
                }
                else -> "VALIDATION_FAILED"
            }
            ErrorDetail(fieldError.field, code, "error.${code.lowercase().replace('_', '.')}")
        }.distinctBy { it.field to it.code }
        return error(HttpStatus.UNPROCESSABLE_ENTITY, details.firstOrNull()?.code ?: "VALIDATION_FAILED", details)
    }

    @ExceptionHandler(MissingRequestHeaderException::class)
    fun missingHeader(exception: MissingRequestHeaderException): ResponseEntity<ErrorEnvelope> {
        val code = if (exception.headerName.equals("Idempotency-Key", ignoreCase = true)) "IDEMPOTENCY_KEY_REQUIRED" else "VALIDATION_FAILED"
        return error(HttpStatus.BAD_REQUEST, code, listOf(ErrorDetail(exception.headerName, code, "error.${code.lowercase().replace('_', '.')}")))
    }

    @ExceptionHandler(MethodArgumentTypeMismatchException::class)
    fun invalidArgumentType(exception: MethodArgumentTypeMismatchException): ResponseEntity<ErrorEnvelope> {
        val code = when {
            exception.name.equals("Idempotency-Key", ignoreCase = true) ||
                exception.name.equals("idempotencyKey", ignoreCase = true) -> "INVALID_IDEMPOTENCY_KEY"
            exception.name.equals("sessionId", ignoreCase = true) -> "RAID_SESSION_STALE"
            else -> "VALIDATION_FAILED"
        }
        return error(HttpStatus.BAD_REQUEST, code, listOf(ErrorDetail(exception.name, code, "error.${code.lowercase().replace('_', '.')}")))
    }

    @ExceptionHandler(RateLimitExceededException::class)
    fun rateLimited(exception: RateLimitExceededException): ResponseEntity<ErrorEnvelope> =
        error(HttpStatus.TOO_MANY_REQUESTS, exception.code, retryable = true, retryAfterSeconds = exception.retryAfterSeconds)

    private fun error(
        status: HttpStatus,
        code: String,
        details: List<ErrorDetail>? = null,
        retryable: Boolean = code == "MATERIAL_PREFERENCE_UNAVAILABLE" || code == "RAID_ATTEMPT_RUNNING",
        retryAfterSeconds: Long? = null,
    ): ResponseEntity<ErrorEnvelope> {
        val response = ResponseEntity.status(status)
        if (retryAfterSeconds != null) response.header("Retry-After", retryAfterSeconds.toString())
        return response.body(
            ErrorEnvelope(
                UUID.randomUUID(),
                clock.instant(),
                code,
                "error.${code.lowercase().replace('_', '.')}",
                retryable,
                details,
            ),
        )
    }
}
