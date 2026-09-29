package com.hanjjak.account.infrastructure

import com.fasterxml.jackson.databind.JsonNode
import com.hanjjak.account.application.SocialLoginProperties
import com.hanjjak.account.application.SocialProfile
import com.hanjjak.account.application.SocialProvider
import com.hanjjak.account.application.SocialProviderClient
import org.springframework.http.MediaType
import org.springframework.security.oauth2.core.DelegatingOAuth2TokenValidator
import org.springframework.security.oauth2.core.OAuth2TokenValidator
import org.springframework.security.oauth2.jwt.Jwt
import org.springframework.security.oauth2.jwt.JwtClaimValidator
import org.springframework.security.oauth2.jwt.JwtValidators
import org.springframework.security.oauth2.jwt.NimbusJwtDecoder
import org.springframework.security.oauth2.jose.jws.SignatureAlgorithm
import org.springframework.stereotype.Component
import org.springframework.util.LinkedMultiValueMap
import org.springframework.web.client.RestClient
import java.security.MessageDigest
import java.util.concurrent.ConcurrentHashMap

@Component
class HttpSocialProviderClient(
    private val properties: SocialLoginProperties,
    restClientBuilder: RestClient.Builder,
) : SocialProviderClient {
    private val restClient = restClientBuilder.build()
    private val decoders = ConcurrentHashMap<SocialProvider, NimbusJwtDecoder>()

    override fun exchange(
        provider: SocialProvider,
        code: String,
        redirectUri: String,
        codeVerifier: String,
        expectedNonce: String,
        state: String,
    ): SocialProfile {
        val configuration = properties.providers[provider.id] ?: throw IllegalArgumentException("SOCIAL_PROVIDER_DISABLED")
        val form = LinkedMultiValueMap<String, String>().apply {
            add("grant_type", "authorization_code")
            add("client_id", configuration.clientId)
            if (configuration.clientSecret.isNotBlank()) add("client_secret", configuration.clientSecret)
            add("redirect_uri", redirectUri)
            add("code", code)
            if (configuration.pkce) add("code_verifier", codeVerifier)
            if (provider == SocialProvider.NAVER) add("state", state)
        }
        val token = runCatching {
            restClient.post()
                .uri(configuration.tokenUri)
                .contentType(MediaType.APPLICATION_FORM_URLENCODED)
                .accept(MediaType.APPLICATION_JSON)
                .body(form)
                .retrieve()
                .body(JsonNode::class.java)
        }.getOrElse { throw IllegalArgumentException("SOCIAL_PROVIDER_RESPONSE_INVALID") }
            ?: throw IllegalArgumentException("SOCIAL_PROVIDER_RESPONSE_INVALID")

        return if (configuration.oidc) {
            val rawIdToken = token.path("id_token").takeUnless(JsonNode::isMissingNode)?.asText()?.takeIf(String::isNotBlank)
                ?: throw IllegalArgumentException("SOCIAL_ID_TOKEN_INVALID")
            val jwt = runCatching { decoder(provider, configuration).decode(rawIdToken) }
                .getOrElse { throw IllegalArgumentException("SOCIAL_ID_TOKEN_INVALID") }
            val nonce = jwt.getClaimAsString("nonce") ?: throw IllegalArgumentException("SOCIAL_NONCE_INVALID")
            require(constantTimeEquals(nonce, expectedNonce)) { "SOCIAL_NONCE_INVALID" }
            SocialProfile(
                jwt.subject?.takeIf(String::isNotBlank) ?: throw IllegalArgumentException("SOCIAL_ID_TOKEN_INVALID"),
                jwt.getClaimAsString("email")?.takeIf(String::isNotBlank),
                sequenceOf("nickname", "preferred_username", "name")
                    .mapNotNull(jwt::getClaimAsString)
                    .firstOrNull(String::isNotBlank),
            )
        } else {
            val accessToken = token.path("access_token").takeUnless(JsonNode::isMissingNode)?.asText()?.takeIf(String::isNotBlank)
                ?: throw IllegalArgumentException("SOCIAL_ACCESS_TOKEN_INVALID")
            val body = runCatching {
                restClient.get()
                    .uri(configuration.userInfoUri)
                    .headers { it.setBearerAuth(accessToken) }
                    .accept(MediaType.APPLICATION_JSON)
                    .retrieve()
                    .body(JsonNode::class.java)
            }.getOrElse { throw IllegalArgumentException("SOCIAL_PROVIDER_RESPONSE_INVALID") }
                ?: throw IllegalArgumentException("SOCIAL_PROVIDER_RESPONSE_INVALID")
            naverProfile(body)
        }
    }

    private fun decoder(provider: SocialProvider, configuration: SocialLoginProperties.Provider): NimbusJwtDecoder =
        decoders.computeIfAbsent(provider) {
            NimbusJwtDecoder.withJwkSetUri(configuration.jwkSetUri)
                .jwsAlgorithm(SignatureAlgorithm.RS256)
                .build()
                .apply {
                    val issuer: OAuth2TokenValidator<Jwt> = JwtValidators.createDefaultWithIssuer(configuration.issuer)
                    val audience = JwtClaimValidator<List<String>>("aud") { audiences ->
                        audiences?.contains(configuration.clientId) == true
                    }
                    setJwtValidator(DelegatingOAuth2TokenValidator(issuer, audience))
                }
        }

    private fun naverProfile(body: JsonNode): SocialProfile {
        require(body.path("resultcode").asText() == "00") { "SOCIAL_PROVIDER_RESPONSE_INVALID" }
        val response = body.path("response")
        return SocialProfile(
            response.path("id").asText().takeIf(String::isNotBlank)
                ?: throw IllegalArgumentException("SOCIAL_PROVIDER_RESPONSE_INVALID"),
            response.path("email").asText().takeIf(String::isNotBlank),
            response.path("nickname").asText().takeIf(String::isNotBlank),
        )
    }

    private fun constantTimeEquals(left: String, right: String): Boolean = MessageDigest.isEqual(left.toByteArray(), right.toByteArray())
}
