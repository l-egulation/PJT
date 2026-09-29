package com.hanjjak.equipment.api

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.events.application.DomainEventPublisher
import com.hanjjak.equipment.application.EquipmentRepository
import com.hanjjak.equipment.application.EquipmentService
import com.hanjjak.equipment.infrastructure.JdbcEquipmentRepository
import com.hanjjak.wallet.application.WalletService
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.jdbc.core.simple.JdbcClient

@Configuration
class EquipmentConfiguration {
    @Bean fun equipmentRepository(jdbc: JdbcClient, inventory: com.hanjjak.inventory.application.InventoryReservationService): EquipmentRepository = JdbcEquipmentRepository(jdbc, inventory)
    @Bean fun equipmentBalanceCatalog(mapper: ObjectMapper): com.hanjjak.equipment.application.EquipmentBalanceCatalog = com.hanjjak.equipment.infrastructure.JsonEquipmentBalanceCatalog.fromResources(mapper)
    @Bean fun equipmentService(repository: EquipmentRepository, mapper: ObjectMapper, wallet: WalletService, events: DomainEventPublisher, catalog: com.hanjjak.equipment.application.EquipmentBalanceCatalog) = EquipmentService(repository, mapper, wallet, events, catalog)
}
