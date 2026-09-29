package com.hanjjak.account.api

import com.fasterxml.jackson.databind.ObjectMapper
import org.springframework.boot.autoconfigure.condition.ConditionalOnWebApplication
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.http.HttpHeaders
import org.springframework.security.config.Customizer
import org.springframework.security.config.annotation.web.builders.HttpSecurity
import org.springframework.security.web.access.intercept.AuthorizationFilter
import org.springframework.security.web.SecurityFilterChain
import org.springframework.web.cors.CorsConfiguration
import org.springframework.web.cors.CorsConfigurationSource
import org.springframework.web.cors.UrlBasedCorsConfigurationSource
import java.time.Clock

@Configuration
@ConditionalOnWebApplication(type = ConditionalOnWebApplication.Type.SERVLET)
class SecurityConfiguration {
    @Bean
    fun apiRequestGuardFilter(
        properties: ApiSecurityProperties,
        sessions: AccountSessionRegistry,
        rateLimiter: RequestRateLimiter,
        objectMapper: ObjectMapper,
        clock: Clock,
    ): ApiRequestGuardFilter = ApiRequestGuardFilter(properties, sessions, rateLimiter, objectMapper, clock)

    @Bean
    fun corsConfigurationSource(properties: ApiSecurityProperties): CorsConfigurationSource {
        val playerConfiguration = corsConfiguration(properties.allowedOrigins)
        val adminConfiguration = corsConfiguration(properties.allowedOrigins)
        return UrlBasedCorsConfigurationSource().apply {
            registerCorsConfiguration("/api/v1/**", playerConfiguration)
            registerCorsConfiguration("/api/admin/v1/**", adminConfiguration)
        }
    }

    private fun corsConfiguration(origins: List<String>) = CorsConfiguration().apply {
        allowedOrigins = origins
        allowedMethods = listOf("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
        allowedHeaders = listOf(HttpHeaders.ACCEPT, HttpHeaders.CONTENT_TYPE, "Idempotency-Key", "X-Request-Id", "X-Correlation-Id", "X-Game-Session-Id", "X-Battle-Token")
        exposedHeaders = listOf("Retry-After", "X-Request-Id", "X-Correlation-Id")
        allowCredentials = true
    }

    @Bean
    fun securityFilterChain(
        http: HttpSecurity,
        apiRequestGuardFilter: ApiRequestGuardFilter,
    ): SecurityFilterChain = http
        .cors(Customizer.withDefaults())
        .csrf { it.disable() }
        .authorizeHttpRequests { auth ->
            auth.requestMatchers("/api/v1/**", "/api/admin/v1/**", "/ws/chat", "/actuator/health/**").permitAll()
                .anyRequest().denyAll()
        }
        .addFilterBefore(apiRequestGuardFilter, AuthorizationFilter::class.java)
        .build()
}
