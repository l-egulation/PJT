package com.hanjjak.eventconsumers

import org.apache.kafka.clients.admin.NewTopic
import org.springframework.beans.factory.annotation.Value
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.kafka.config.TopicBuilder
import org.springframework.scheduling.annotation.EnableScheduling

@Configuration
@EnableScheduling
class KafkaConfiguration {
    @Bean
    @ConditionalOnProperty(prefix = "events.kafka", name = ["enabled"], havingValue = "true", matchIfMissing = true)
    fun domainEventsTopic(@Value("\${events.kafka.topic:domain-events.v1}") topic: String): NewTopic = TopicBuilder.name(topic).partitions(3).replicas(1).build()
}
