package com.hanjjak.market.api

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.events.application.DomainEventPublisher
import com.hanjjak.inventory.application.ItemCatalog
import com.hanjjak.inventory.application.MarketInventoryService
import com.hanjjak.mail.application.MailService
import com.hanjjak.market.application.AdminMarketService
import com.hanjjak.market.application.MarketDeliveryService
import com.hanjjak.market.application.MarketInstrumentCatalog
import com.hanjjak.market.application.OrderBookRepository
import com.hanjjak.market.application.OrderBookService
import com.hanjjak.market.infrastructure.JdbcOrderBookRepository
import com.hanjjak.wallet.application.WalletService
import org.springframework.boot.ApplicationRunner
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.jdbc.core.simple.JdbcClient
import java.time.Clock

@Configuration
class MarketConfiguration {
    @Bean fun orderBookRepository(jdbc: JdbcClient, mapper: ObjectMapper): OrderBookRepository = JdbcOrderBookRepository(jdbc, mapper)
    @Bean fun marketInstrumentCatalog(repository: OrderBookRepository, catalog: ItemCatalog, clock: Clock) = MarketInstrumentCatalog(repository, catalog, clock)
    @Bean fun orderBookService(
        repository: OrderBookRepository,
        inventory: MarketInventoryService,
        wallet: WalletService,
        mail: MailService,
        catalog: ItemCatalog,
        mapper: ObjectMapper,
        clock: Clock,
        events: DomainEventPublisher,
        instruments: MarketInstrumentCatalog,
    ) = OrderBookService(repository, inventory, wallet, mail, catalog, mapper, clock, events, instruments)
    @Bean fun marketDeliveryService(repository: OrderBookRepository, inventory: MarketInventoryService, catalog: ItemCatalog, mapper: ObjectMapper, clock: Clock, mail: MailService) = MarketDeliveryService(repository, inventory, catalog, mapper, clock, mail)
    @Bean fun adminMarketService(
        repository: OrderBookRepository,
        wallet: WalletService,
        catalog: ItemCatalog,
        mapper: ObjectMapper,
        clock: Clock,
        events: DomainEventPublisher,
    ) = AdminMarketService(repository, wallet, catalog, mapper, clock, events)
    @Bean fun initializeOrderBook(service: OrderBookService) = ApplicationRunner {
        service.synchronizeCatalog()
        service.migrateLegacyMarket()
    }
}
