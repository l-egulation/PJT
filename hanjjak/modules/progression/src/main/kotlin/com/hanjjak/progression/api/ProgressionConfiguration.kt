package com.hanjjak.progression.api

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.progression.application.ProgressionRepository
import com.hanjjak.progression.application.ProgressionRewardCatalog
import com.hanjjak.progression.application.ProgressionRewardService
import com.hanjjak.progression.infrastructure.JdbcProgressionRepository
import com.hanjjak.progression.infrastructure.JsonProgressionContent
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.jdbc.core.simple.JdbcClient

@Configuration
class ProgressionConfiguration {
    @Bean fun progressionRepository(jdbc: JdbcClient): ProgressionRepository = JdbcProgressionRepository(jdbc)
    @Bean fun progressionRewardService(repository: ProgressionRepository, mapper: ObjectMapper, wallet: com.hanjjak.wallet.application.WalletService) = ProgressionRewardService(repository, mapper, wallet)
    @Bean fun progressionRewardCatalog(mapper: ObjectMapper): ProgressionRewardCatalog = JsonProgressionContent.fromResources(mapper)
}
