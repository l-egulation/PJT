package com.hanjjak.gameapi

import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty
import org.springframework.context.annotation.Configuration
import org.springframework.scheduling.annotation.EnableScheduling

@Configuration(proxyBeanMethods = false)
@EnableScheduling
@ConditionalOnProperty(
    prefix = "spring.task.scheduling",
    name = ["enabled"],
    havingValue = "true",
    matchIfMissing = true,
)
class SchedulingConfiguration
