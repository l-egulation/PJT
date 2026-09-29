package com.hanjjak.account.application

import org.springframework.boot.context.properties.ConfigurationProperties
import java.time.Duration

@ConfigurationProperties("hanjjak.social-login")
data class SocialLoginProperties(
    val publicBaseUrl: String = "http://localhost:8080",
    val frontendSuccessUrl: String = "http://localhost:5173",
    val frontendFailureUrl: String = "http://localhost:5173",
    val flowLifetime: Duration = Duration.ofMinutes(10),
    val providers: Map<String, Provider> = emptyMap(),
) {
    data class Provider(
        val enabled: Boolean = false,
        val clientId: String = "",
        val clientSecret: String = "",
        val issuer: String = "",
        val authorizationUri: String = "",
        val tokenUri: String = "",
        val userInfoUri: String = "",
        val jwkSetUri: String = "",
        val scopes: List<String> = emptyList(),
        val oidc: Boolean = false,
        val pkce: Boolean = true,
    )
}
