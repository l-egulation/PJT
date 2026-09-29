package com.hanjjak.wallet.api

import com.hanjjak.wallet.application.WalletRepository
import com.hanjjak.wallet.application.WalletService
import com.hanjjak.wallet.infrastructure.JdbcWalletRepository
import org.springframework.beans.factory.annotation.Value
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.jdbc.core.simple.JdbcClient

@Configuration
class WalletConfiguration {
    @Bean fun walletRepository(jdbc: JdbcClient, @Value("\${wallet.batch-ledger-insert:true}") batchLedgerInsert: Boolean): WalletRepository = JdbcWalletRepository(jdbc, batchLedgerInsert)
    @Bean fun walletService(repository: WalletRepository) = WalletService(repository)
}
