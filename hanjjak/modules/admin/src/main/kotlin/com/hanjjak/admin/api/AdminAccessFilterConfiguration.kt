package com.hanjjak.admin.api

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.admin.application.AdminAuthenticationService
import com.hanjjak.admin.infrastructure.AdminAuditRepository
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.boot.web.servlet.FilterRegistrationBean
import org.springframework.core.Ordered
import java.time.Clock

@Configuration
class AdminAccessFilterConfiguration {
    @Bean
    fun adminAccessFilterInstance(
        authentication: AdminAuthenticationService,
        audit: AdminAuditRepository,
        properties: AdminProperties,
        mapper: ObjectMapper,
        clock: Clock,
    ): AdminAccessFilter = AdminAccessFilter(authentication, audit, properties.allowedOrigins, mapper, clock)

    @Bean
    fun adminAccessFilterRegistration(filter: AdminAccessFilter) = FilterRegistrationBean(filter).apply {
        order = Ordered.HIGHEST_PRECEDENCE + 10
        addUrlPatterns("/api/admin/v1/*")
    }
}
