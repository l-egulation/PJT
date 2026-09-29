package com.hanjjak.admin.api

import org.springframework.boot.context.properties.ConfigurationProperties
import java.time.Duration

@ConfigurationProperties("hanjjak.admin")
data class AdminProperties(
    val gitlab: Gitlab = Gitlab(),
    val authenticationRateLimit: AuthenticationRateLimit = AuthenticationRateLimit(),
    val allowedOrigins: List<String> = listOf("http://localhost:5173", "http://127.0.0.1:5173"),
    val session: Session = Session(),
    val deployment: Deployment = Deployment(),
) {
    data class Gitlab(
        val enabled: Boolean = false,
        val clientId: String = "",
        val clientSecret: String = "",
        val authorizationUri: String = "https://lab.ssafy.com/oauth/authorize",
        val tokenUri: String = "https://lab.ssafy.com/oauth/token",
        val userUri: String = "https://lab.ssafy.com/api/v4/user",
        val scopes: List<String> = listOf("read_user"),
        val allowedUsernames: List<String> = emptyList(),
        val publicBaseUrl: String = "http://localhost:8080",
        val successUrl: String = "http://localhost:5173",
        val failureUrl: String = "http://localhost:5173",
        val flowLifetime: Duration = Duration.ofMinutes(10),
    )

    data class AuthenticationRateLimit(
        val maxRequests: Int = 5,
        val window: Duration = Duration.ofMinutes(1),
    )

    data class Session(
        val absoluteLifetime: Duration = Duration.ofHours(8),
        val inactivityTimeout: Duration = Duration.ofMinutes(30),
        val secureCookie: Boolean = false,
    )

    data class Deployment(
        val commitSha: String = "local",
        val contentVersion: String = "v1",
        val contentAuthority: String = "working",
    )
}
