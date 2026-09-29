package com.hanjjak.battle.api

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.battle.application.FirstClearRewardRepository
import com.hanjjak.battle.application.FirstClearRewardService
import com.hanjjak.battle.application.OfflineRewardSettings
import com.hanjjak.battle.application.StageBattleService
import com.hanjjak.battle.infrastructure.JdbcFirstClearRewardRepository
import com.hanjjak.inventory.application.InventoryReservationService
import com.hanjjak.progression.application.ProgressionRewardCatalog
import com.hanjjak.skills.application.SkillService
import com.hanjjak.stage.application.StageCatalog
import com.hanjjak.stage.infrastructure.VersionedStageCatalog
import com.hanjjak.wallet.application.WalletService
import org.springframework.boot.context.properties.EnableConfigurationProperties
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.jdbc.core.simple.JdbcClient

@Configuration
@EnableConfigurationProperties(OfflineRewardSettings::class)
class BattleConfiguration {
    @Bean
    fun stageCatalog(mapper: ObjectMapper): StageCatalog = VersionedStageCatalog.fromResources(mapper)
    @Bean fun stageBattleService(stages: StageCatalog): StageBattleService = StageBattleService(stages)
    @Bean fun firstClearRewardRepository(jdbc: JdbcClient, mapper: ObjectMapper): FirstClearRewardRepository = JdbcFirstClearRewardRepository(jdbc, mapper)
    @Bean fun firstClearRewardService(repository: FirstClearRewardRepository, catalog: ProgressionRewardCatalog, inventory: InventoryReservationService, wallet: WalletService, skills: SkillService, mapper: ObjectMapper, events: com.hanjjak.events.application.DomainEventPublisher) = FirstClearRewardService(repository, catalog, inventory, wallet, skills, mapper, events)

    @Bean
    fun arenaService(
        mapper: ObjectMapper,
        stats: com.hanjjak.battle.application.BattleStatsProvider,
        skills: com.hanjjak.battle.application.BattleSkillProvider,
        jdbc: org.springframework.jdbc.core.simple.JdbcClient,
    ) = ArenaService(mapper, stats, skills, jdbc)
}
