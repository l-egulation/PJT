package com.hanjjak.account.api

import com.hanjjak.account.application.AuthenticationService
import com.hanjjak.account.application.GameSessionLifecycle
import com.hanjjak.account.application.LinkedSocialIdentity
import com.hanjjak.account.application.PendingSocialIdentity
import com.hanjjak.account.application.SocialFlow
import com.hanjjak.account.application.SocialLoginService
import com.hanjjak.account.application.SocialProvider
import com.hanjjak.account.application.SocialProviderStatus
import jakarta.servlet.http.HttpServletRequest
import jakarta.servlet.http.HttpServletResponse
import jakarta.validation.Valid
import jakarta.validation.constraints.NotBlank
import jakarta.validation.constraints.Size
import org.springframework.http.HttpStatus
import org.springframework.web.bind.annotation.DeleteMapping
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.PathVariable
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RequestBody
import org.springframework.web.bind.annotation.RequestHeader
import org.springframework.web.bind.annotation.RequestMapping
import org.springframework.web.bind.annotation.RequestParam
import org.springframework.web.bind.annotation.ResponseStatus
import org.springframework.web.bind.annotation.RestController
import java.io.Serializable
import java.security.MessageDigest
import java.time.Clock
import java.util.UUID

@RestController
@RequestMapping("/api/v1/auth/social")
class SocialLoginController(
    private val socialLogin: SocialLoginService,
    private val authenticationController: AuthenticationController,
    private val authentication: AuthenticationService,
    private val gameSessions: GameSessionLifecycle,
    private val sessions: AccountSessionRegistry,
    private val rateLimiter: RequestRateLimiter,
    private val clock: Clock,
) {
    data class ProviderView(val id: String, val displayName: String, val enabled: Boolean)
    data class BeginResponse(val authorizationUrl: String)
    data class PendingSignupView(val token: String, val provider: String, val email: String?, val suggestedNickname: String?)
    data class PendingSignupRequest(@field:NotBlank @field:Size(max = 128) val token: String, @field:NotBlank @field:Size(max = 20) val nickname: String)
    data class ConnectedIdentityView(val provider: String, val displayName: String, val email: String?)
    data class ConnectedIdentities(val passwordEnabled: Boolean, val providers: List<ConnectedIdentityView>)

    @GetMapping("/providers")
    fun providers(): AuthenticationController.Envelope<List<ProviderView>> = envelope(
        socialLogin.statuses().map { it.toView() },
    )

    @PostMapping("/{provider}/begin")
    fun begin(
        @PathVariable provider: String,
        @RequestHeader("Idempotency-Key") idempotencyKey: UUID,
        request: HttpServletRequest,
    ): AuthenticationController.Envelope<BeginResponse> {
        val socialProvider = SocialProvider.fromId(provider)
        rateLimiter.checkAuthentication("social:${socialProvider.id}", remoteAddress(request), true)
        val flow = beginFlow(request, idempotencyKey, socialProvider, null)
        return envelope(BeginResponse(socialLogin.authorizationUrl(flow)))
    }

    @PostMapping("/{provider}/link/begin")
    fun beginLink(
        @PathVariable provider: String,
        @RequestHeader("Idempotency-Key") idempotencyKey: UUID,
        request: HttpServletRequest,
    ): AuthenticationController.Envelope<BeginResponse> {
        val accountId = accountId(request)
        val flow = beginFlow(request, idempotencyKey, SocialProvider.fromId(provider), accountId)
        return envelope(BeginResponse(socialLogin.authorizationUrl(flow)))
    }

    @GetMapping("/{provider}/callback")
    fun callback(
        @PathVariable provider: String,
        @RequestParam(required = false) code: String?,
        @RequestParam(required = false) state: String?,
        @RequestParam(required = false) error: String?,
        request: HttpServletRequest,
        response: HttpServletResponse,
    ) {
        val session = request.getSession(false)
        val flow = session?.getAttribute(FLOW_SESSION_KEY) as? SocialFlow
        session?.removeAttribute(FLOW_SESSION_KEY)
        session?.removeAttribute(BEGIN_COMMAND_SESSION_KEY)
        val destination = runCatching {
            require(error == null && !code.isNullOrBlank() && !state.isNullOrBlank()) { "SOCIAL_AUTHORIZATION_DENIED" }
            require(flow != null && flow.provider == SocialProvider.fromId(provider)) { "SOCIAL_STATE_INVALID" }
            val result = socialLogin.complete(flow, state, code)
            when {
                result.authentication != null -> {
                    if (flow.accountId == null) {
                        gameSessions.replaceForLogin(result.authentication.account.id)
                        establishSession(request, result.authentication.account.id)
                    }
                    socialLogin.successUrl()
                }
                result.pendingSignup != null && result.pendingIdentity != null -> {
                    request.session.setAttribute(PENDING_SESSION_KEY, result.pendingIdentity)
                    socialLogin.pendingSignupUrl(result.pendingSignup.token)
                }
                else -> throw IllegalArgumentException("SOCIAL_PROVIDER_RESPONSE_INVALID")
            }
        }.getOrElse { failure -> socialLogin.failureUrl(failure.message ?: "SOCIAL_LOGIN_FAILED") }
        response.status = HttpServletResponse.SC_FOUND
        response.setHeader("Location", destination)
    }

    @GetMapping("/pending")
    fun pending(request: HttpServletRequest): AuthenticationController.Envelope<PendingSignupView> {
        val pending = request.getSession(false)?.getAttribute(PENDING_SESSION_KEY) as? PendingSocialIdentity
            ?: throw IllegalArgumentException("SOCIAL_SIGNUP_REQUIRED")
        return envelope(PendingSignupView(pending.token, pending.provider.id, pending.email, pending.suggestedNickname))
    }

    @PostMapping("/signup")
    fun finishSignup(
        @RequestHeader("Idempotency-Key") idempotencyKey: UUID,
        @Valid @RequestBody body: PendingSignupRequest,
        request: HttpServletRequest,
    ): AuthenticationController.Envelope<AuthenticationController.Identity> {
        val session = request.getSession(false) ?: throw IllegalArgumentException("SOCIAL_SIGNUP_REQUIRED")
        val fingerprint = sha256("${body.token}\u0000${body.nickname.trim()}")
        val existing = session.getAttribute(SIGNUP_COMMAND_SESSION_KEY) as? SocialSignupCommand
        if (existing != null) {
            require(existing.idempotencyKey == idempotencyKey && existing.fingerprint == fingerprint) { "IDEMPOTENCY_KEY_REUSED" }
            return authenticationController.identityEnvelope(authentication.session(existing.accountId))
        }
        val pending = session.getAttribute(PENDING_SESSION_KEY) as? PendingSocialIdentity
            ?: throw IllegalArgumentException("SOCIAL_SIGNUP_REQUIRED")
        require(MessageDigest.isEqual(pending.token.toByteArray(), body.token.toByteArray())) { "SOCIAL_SIGNUP_TOKEN_INVALID" }
        val result = socialLogin.finishSignup(pending, body.nickname)
        session.removeAttribute(PENDING_SESSION_KEY)
        session.setAttribute(SIGNUP_COMMAND_SESSION_KEY, SocialSignupCommand(idempotencyKey, fingerprint, result.account.id))
        establishSession(request, result.account.id)
        return authenticationController.identityEnvelope(result)
    }

    @GetMapping("/connections")
    fun connections(request: HttpServletRequest): AuthenticationController.Envelope<ConnectedIdentities> {
        val accountId = accountId(request)
        return envelope(ConnectedIdentities(socialLogin.passwordEnabled(accountId), socialLogin.list(accountId).map { it.toView() }))
    }

    @DeleteMapping("/connections/{provider}")
    @ResponseStatus(HttpStatus.OK)
    fun unlink(
        @PathVariable provider: String,
        @RequestHeader("Idempotency-Key") idempotencyKey: UUID,
        request: HttpServletRequest,
    ): AuthenticationController.Envelope<ConnectedIdentities> {
        val accountId = accountId(request)
        val socialProvider = SocialProvider.fromId(provider)
        val existing = request.session.getAttribute(UNLINK_COMMAND_SESSION_KEY) as? SocialUnlinkCommand
        if (existing != null && existing.idempotencyKey == idempotencyKey) {
            require(existing.provider == socialProvider) { "IDEMPOTENCY_KEY_REUSED" }
            return connections(request)
        }
        socialLogin.unlink(accountId, socialProvider)
        request.session.setAttribute(UNLINK_COMMAND_SESSION_KEY, SocialUnlinkCommand(idempotencyKey, socialProvider))
        return connections(request)
    }

    private fun beginFlow(request: HttpServletRequest, idempotencyKey: UUID, provider: SocialProvider, accountId: UUID?): SocialFlow {
        val existing = request.session.getAttribute(BEGIN_COMMAND_SESSION_KEY) as? SocialBeginCommand
        if (existing != null && existing.idempotencyKey == idempotencyKey) {
            require(existing.provider == provider && existing.accountId == accountId) { "IDEMPOTENCY_KEY_REUSED" }
            request.session.setAttribute(FLOW_SESSION_KEY, existing.flow)
            return existing.flow
        }
        val flow = socialLogin.begin(provider, accountId)
        request.session.setAttribute(BEGIN_COMMAND_SESSION_KEY, SocialBeginCommand(idempotencyKey, provider, accountId, flow))
        request.session.setAttribute(FLOW_SESSION_KEY, flow)
        return flow
    }

    private fun accountId(request: HttpServletRequest): UUID = request.getSession(false)?.getAttribute("accountId") as? UUID
        ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")

    private fun establishSession(request: HttpServletRequest, accountId: UUID) {
        val session = request.getSession(true)
        request.changeSessionId()
        session.setAttribute("accountId", accountId)
        sessions.activate(accountId, session.id)
    }

    private fun <T> envelope(data: T): AuthenticationController.Envelope<T> = AuthenticationController.Envelope(UUID.randomUUID(), clock.instant(), 0, data)
    private fun remoteAddress(request: HttpServletRequest): String = request.remoteAddr.orEmpty().ifBlank { "unknown" }
    private fun sha256(value: String): String = MessageDigest.getInstance("SHA-256").digest(value.toByteArray()).joinToString("") { "%02x".format(it) }
    private fun SocialProviderStatus.toView() = ProviderView(provider.id, provider.displayName, enabled)
    private fun LinkedSocialIdentity.toView() = ConnectedIdentityView(provider.id, provider.displayName, email)

    private data class SocialBeginCommand(val idempotencyKey: UUID, val provider: SocialProvider, val accountId: UUID?, val flow: SocialFlow) : Serializable
    private data class SocialSignupCommand(val idempotencyKey: UUID, val fingerprint: String, val accountId: UUID) : Serializable
    private data class SocialUnlinkCommand(val idempotencyKey: UUID, val provider: SocialProvider) : Serializable

    private companion object {
        const val FLOW_SESSION_KEY = "socialLoginFlow"
        const val PENDING_SESSION_KEY = "pendingSocialIdentity"
        const val BEGIN_COMMAND_SESSION_KEY = "socialLoginBeginCommand"
        const val SIGNUP_COMMAND_SESSION_KEY = "socialSignupCommand"
        const val UNLINK_COMMAND_SESSION_KEY = "socialUnlinkCommand"
    }
}
