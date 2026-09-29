package com.hanjjak.cosmetics.api

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.cosmetics.domain.CosmeticsContent
import com.hanjjak.cosmetics.infrastructure.JsonCosmeticsContent
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration

@Configuration
class CosmeticsConfiguration {
    @Bean
    fun cosmeticsContent(mapper: ObjectMapper): CosmeticsContent = JsonCosmeticsContent(
        requireNotNull(javaClass.getResourceAsStream("/cosmetics/cosmetics.json")) { "applied cosmetics content missing" },
        mapper,
    ).content
}
