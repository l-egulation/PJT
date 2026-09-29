package com.hanjjak.account.infrastructure

import com.hanjjak.account.api.ApiSecurityProperties
import com.hanjjak.account.application.BalanceVersionRepository
import com.hanjjak.account.application.BalanceVersionService
import com.hanjjak.account.application.PasswordResetProperties
import com.hanjjak.account.application.SocialLoginProperties
import org.springframework.beans.factory.annotation.Value
import org.springframework.boot.context.properties.EnableConfigurationProperties
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.core.task.TaskExecutor
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.scheduling.annotation.EnableAsync
import org.springframework.scheduling.concurrent.ThreadPoolTaskExecutor
import org.springframework.security.crypto.bcrypt.BCryptPasswordEncoder
import org.springframework.security.crypto.password.PasswordEncoder
import java.time.Clock
import java.security.SecureRandom

@EnableAsync
@EnableConfigurationProperties(ApiSecurityProperties::class, PasswordResetProperties::class, SocialLoginProperties::class)
@Configuration
class AccountConfiguration {
    @Bean fun passwordEncoder(): PasswordEncoder = BCryptPasswordEncoder()
    @Bean fun clock(): Clock = Clock.systemUTC()
    @Bean
    fun balanceVersionService(repository: BalanceVersionRepository, @Value("${'$'}{hanjjak.balance.active-version:enemy-v1-applied}") activeVersion: String): BalanceVersionService = BalanceVersionService(repository, activeVersion)
    @Bean fun secureRandom(): SecureRandom = SecureRandom()
    @Bean("passwordResetMailExecutor")
    fun passwordResetMailExecutor(): TaskExecutor = ThreadPoolTaskExecutor().apply {
        corePoolSize = 1
        maxPoolSize = 4
        queueCapacity = 100
        setThreadNamePrefix("password-reset-mail-")
        initialize()
    }
}
