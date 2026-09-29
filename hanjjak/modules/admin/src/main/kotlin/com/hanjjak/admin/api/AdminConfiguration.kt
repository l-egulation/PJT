package com.hanjjak.admin.api

import org.springframework.boot.context.properties.EnableConfigurationProperties
import org.springframework.context.annotation.Configuration

@Configuration
@EnableConfigurationProperties(AdminProperties::class)
class AdminConfiguration
