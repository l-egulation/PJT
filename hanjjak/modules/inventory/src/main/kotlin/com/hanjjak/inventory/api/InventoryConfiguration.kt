package com.hanjjak.inventory.api

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.inventory.application.*
import com.hanjjak.inventory.domain.ItemDefinition
import com.hanjjak.inventory.infrastructure.*
import org.springframework.beans.factory.ObjectProvider
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.jdbc.core.simple.JdbcClient

@Configuration
class InventoryConfiguration {
    @Bean
    fun itemCatalog(sources: ObjectProvider<ItemDefinitionSource>): ItemCatalog {
        val additional = mutableListOf<ItemDefinition>()
        sources.orderedStream().forEach { additional.addAll(it.items()) }
        return StaticItemCatalog(additional)
    }
    @Bean fun inventoryRepository(jdbc: JdbcClient, catalog: ItemCatalog): InventoryRepository = JdbcInventoryRepository(jdbc, catalog)
    @Bean fun materialPreferenceProvider(jdbc: JdbcClient): MaterialPreferenceProvider = JdbcMaterialPreferenceProvider(jdbc)
    @Bean fun inventoryQueries(repository: InventoryRepository, catalog: ItemCatalog) = InventoryQueryService(repository, catalog)
    @Bean fun inventoryRewards(repository: InventoryRepository, catalog: ItemCatalog, mapper: ObjectMapper) = InventoryRewardService(repository, catalog, mapper)
    @Bean fun inventoryReservations(repository: InventoryRepository, catalog: ItemCatalog) = InventoryReservationService(repository, catalog)
    @Bean fun inventoryInstances(repository: InventoryRepository, catalog: ItemCatalog) = InventoryInstanceService(repository, catalog)
    @Bean fun marketInventory(repository: InventoryRepository, catalog: ItemCatalog) = MarketInventoryService(repository, catalog)
    @Bean fun materialDropCatalog(mapper: ObjectMapper) = MaterialDropCatalog.fromResources(mapper)
    @Bean fun battleRewards(preferences: MaterialPreferenceProvider, drops: MaterialDropCatalog, rewards: InventoryRewardService) = BattleRewardService(preferences, drops, rewards)
}
