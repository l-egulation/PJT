package com.hanjjak.mail.api

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.mail.application.MailRepository
import com.hanjjak.mail.application.MailService
import com.hanjjak.mail.infrastructure.JdbcMailRepository
import com.hanjjak.wallet.application.WalletService
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.jdbc.core.simple.JdbcClient
import java.time.Clock

@Configuration
class MailConfiguration {
    @Bean fun mailRepository(jdbc: JdbcClient): MailRepository = JdbcMailRepository(jdbc)
    @Bean fun mailService(repository: MailRepository, wallet: WalletService, mapper: ObjectMapper, clock: Clock) = MailService(repository, wallet, mapper, clock)
}
