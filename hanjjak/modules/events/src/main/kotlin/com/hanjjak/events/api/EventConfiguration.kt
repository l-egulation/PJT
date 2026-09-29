package com.hanjjak.events.api

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.events.application.DomainEventPublisher
import com.hanjjak.events.application.EconomyMetricsService
import com.hanjjak.events.infrastructure.JdbcOutboxEventPublisher
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.jdbc.core.simple.JdbcClient

@Configuration
class EventConfiguration {
    @Bean fun domainEventPublisher(jdbc: JdbcClient, mapper: ObjectMapper): DomainEventPublisher = JdbcOutboxEventPublisher(jdbc, mapper)

    @Bean fun economyMetricsService(jdbc: JdbcClient, mapper: ObjectMapper): EconomyMetricsService = EconomyMetricsService(jdbc, mapper)
}
