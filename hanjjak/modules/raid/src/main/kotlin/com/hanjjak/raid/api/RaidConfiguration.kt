package com.hanjjak.raid.api

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.raid.application.RaidAttemptCompletionService
import com.hanjjak.raid.application.RaidAttemptTerminalizer
import com.hanjjak.raid.application.RaidClock
import com.hanjjak.raid.application.RaidCombatSnapshotProvider
import com.hanjjak.raid.application.RaidContentPort
import com.hanjjak.raid.application.RaidMainBattlePort
import com.hanjjak.raid.application.RaidProgressPort
import com.hanjjak.raid.application.RaidQueryPort
import com.hanjjak.raid.application.RaidRepository
import com.hanjjak.raid.application.RaidRewardPort
import com.hanjjak.raid.application.RaidService
import com.hanjjak.raid.application.RaidSettlementService
import com.hanjjak.raid.infrastructure.JdbcRaidRepository
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.scheduling.annotation.EnableScheduling
import java.time.Clock

@Configuration(proxyBeanMethods = false)
@EnableScheduling
class RaidConfiguration {
    @Bean
    fun raidController(service: RaidService, clock: Clock, queries: com.hanjjak.raid.application.RaidQueryPort) = RaidController(service, clock, queries)
    @Bean
    fun raidRepository(jdbc: org.springframework.jdbc.core.simple.JdbcClient, mapper: ObjectMapper): RaidRepository = JdbcRaidRepository(jdbc, mapper)
    @Bean
    fun raidClock(clock: Clock): RaidClock = RaidClock { clock.instant() }

    @Bean
    fun raidAttemptCompletionService(repository: RaidRepository, mainBattle: RaidMainBattlePort, mapper: ObjectMapper) =
        RaidAttemptCompletionService(repository, mainBattle, mapper)

    @Bean
    fun raidService(
        repository: RaidRepository,
        clock: RaidClock,
        snapshotProvider: RaidCombatSnapshotProvider,
        contentPort: RaidContentPort,
        progress: RaidProgressPort,
        mainBattle: RaidMainBattlePort,
        completion: RaidAttemptCompletionService,
        mapper: ObjectMapper,
        rewardPort: RaidRewardPort,
        events: com.hanjjak.events.application.DomainEventPublisher,
        queries: RaidQueryPort,
    ) = RaidService(repository, clock, snapshotProvider, contentPort, progress, mainBattle, completion, mapper, rewardPort, events, queries)

    @Bean
    fun raidAttemptTerminalizer(repository: RaidRepository, completion: RaidAttemptCompletionService, clock: RaidClock) =
        RaidAttemptTerminalizer(repository, completion, clock)

    @Bean
    fun raidSessionRotationService(repository: RaidRepository, contentPort: RaidContentPort) =
        com.hanjjak.raid.application.RaidSessionRotationService(repository, contentPort)

    @Bean
    fun raidSettlementPhaseService(repository: RaidRepository) =
        com.hanjjak.raid.application.RaidSettlementPhaseService(repository)

    @Bean
    fun raidSettlementAccountService(
        repository: RaidRepository,
        rewardPort: com.hanjjak.raid.application.RaidRewardPort,
        mainBattle: RaidMainBattlePort,
        events: com.hanjjak.events.application.DomainEventPublisher,
        mapper: ObjectMapper,
    ) = com.hanjjak.raid.application.RaidSettlementAccountService(repository, rewardPort, mainBattle, events, mapper)

    @Bean
    fun raidSettlementRankService(repository: RaidRepository) =
        com.hanjjak.raid.application.RaidSettlementRankService(repository)

    @Bean
    fun raidSettlementCompletionService(repository: RaidRepository) =
        com.hanjjak.raid.application.RaidSettlementCompletionService(repository)

    @Bean
    fun raidSettlementService(
        repository: RaidRepository,
        clock: RaidClock,
        rotation: com.hanjjak.raid.application.RaidSessionRotationService,
        phases: com.hanjjak.raid.application.RaidSettlementPhaseService,
        accounts: com.hanjjak.raid.application.RaidSettlementAccountService,
        ranks: com.hanjjak.raid.application.RaidSettlementRankService,
        completion: com.hanjjak.raid.application.RaidSettlementCompletionService,
    ) = RaidSettlementService(repository, clock, rotation, phases, accounts, ranks, completion)

    @Bean
    fun raidSettlementScheduler(service: RaidSettlementService) = RaidSettlementScheduler(service)
}

open class RaidSettlementScheduler(private val settlement: RaidSettlementService) {
    @jakarta.annotation.PostConstruct
    fun initialize() { settlement.rotateSession() }

    @org.springframework.scheduling.annotation.Scheduled(fixedDelay = 30_000)
    fun settle() { settlement.settleDueSessions() }
}
