package com.hanjjak.account.api

import com.hanjjak.account.application.MaterialPreferenceService
import jakarta.servlet.http.HttpServletRequest
import jakarta.servlet.http.HttpServletResponse
import org.springframework.context.annotation.Configuration
import org.springframework.web.servlet.HandlerInterceptor
import org.springframework.web.servlet.config.annotation.InterceptorRegistry
import org.springframework.web.servlet.config.annotation.WebMvcConfigurer
import java.util.UUID

class MaterialSelectionGate(private val preferences: MaterialPreferenceService) : HandlerInterceptor {
    override fun preHandle(request: HttpServletRequest, response: HttpServletResponse, handler: Any): Boolean {
        val accountId = request.getSession(false)?.getAttribute("accountId") as? UUID ?: return true
        preferences.requireSelected(accountId)
        return true
    }
}

@Configuration
class MaterialSelectionGateConfiguration(private val preferences: MaterialPreferenceService) : WebMvcConfigurer {
    override fun addInterceptors(registry: InterceptorRegistry) {
        registry.addInterceptor(MaterialSelectionGate(preferences))
            .addPathPatterns("/api/v1/**")
            .excludePathPatterns("/api/v1/auth/**", "/api/v1/material-preference")
    }
}
