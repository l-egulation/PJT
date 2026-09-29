package com.hanjjak.gameapi

import com.hanjjak.cosmetics.application.CosmeticsWallet
import com.hanjjak.inventory.application.InventoryReservationService
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.stereotype.Component
import java.util.UUID

@Component
class JdbcCosmeticsWallet(
    private val jdbc: JdbcClient,
    private val inventory: InventoryReservationService,
    private val wallet: com.hanjjak.wallet.application.WalletService,
) : CosmeticsWallet {
    override fun balanceForUpdate(accountId: UUID): CosmeticsWallet.Balance = jdbc.sql("select w.balance as rice,c.cosmetic_ticket_balance,c.cosmetics_unlocked from character c join wallet_balance w on w.account_id=c.account_id where c.account_id=:account for update")
        .param("account", accountId).query { row, _ -> CosmeticsWallet.Balance(row.getLong("rice"), row.getLong("cosmetic_ticket_balance"), row.getBoolean("cosmetics_unlocked")) }.single()
    override fun milestone(accountId: UUID, bannerId: String, boxItemId: String): CosmeticsWallet.Milestone {
        val progress = jdbc.sql("select total_successful_draws,claimed_box_count from cosmetic_banner_progress where account_id=:account and banner_id=:banner")
            .param("account", accountId).param("banner", bannerId)
            .query { row, _ -> row.getLong("total_successful_draws") to row.getLong("claimed_box_count") }.optional().orElse(0L to 0L)
        val boxes = jdbc.sql("select quantity-reserved_quantity from inventory_stack where account_id=:account and item_id=:box")
            .param("account", accountId).param("box", boxItemId).query(Long::class.java).optional().orElse(0L)
        return CosmeticsWallet.Milestone(progress.first, progress.second, boxes)
    }

    override fun debit(accountId: UUID, rice: Long, tickets: Long, sourceId: UUID) {
        val ticketChanged = jdbc.sql("update character set cosmetic_ticket_balance=cosmetic_ticket_balance-:tickets where account_id=:account and cosmetic_ticket_balance>=:tickets")
            .params(mapOf("account" to accountId, "tickets" to tickets)).update()
        require(ticketChanged == 1) { "INSUFFICIENT_GACHA_FUNDS" }
        if (rice > 0) wallet.debit(accountId, rice, "COSMETIC_GACHA", sourceId, bumpStateVersion = false)
    }

    override fun addDraws(accountId: UUID, bannerId: String, count: Int) {
        jdbc.sql("insert into cosmetic_banner_progress(account_id,banner_id,total_successful_draws,claimed_box_count) values (:account,:banner,:count,0) on conflict(account_id,banner_id) do update set total_successful_draws=cosmetic_banner_progress.total_successful_draws+:count")
            .params(mapOf("account" to accountId,"banner" to bannerId,"count" to count)).update()
    }

    override fun claimBoxes(accountId: UUID, bannerId: String, boxItemId: String, count: Long): Long {
        val progress = jdbc.sql("select total_successful_draws,claimed_box_count from cosmetic_banner_progress where account_id=:account and banner_id=:banner for update").param("account", accountId).param("banner", bannerId).query { row, _ -> row.getLong(1) to row.getLong(2) }.optional().orElse(0L to 0L)
        val available = progress.first / 200 - progress.second
        require(count in 1..available) { "MILESTONE_NOT_CLAIMABLE" }
        jdbc.sql("update cosmetic_banner_progress set claimed_box_count=claimed_box_count+:count where account_id=:account and banner_id=:banner").params(mapOf("count" to count,"account" to accountId,"banner" to bannerId)).update()
        inventory.grantStackOrReject(accountId, boxItemId, count)
        return available - count
    }

    override fun openBox(accountId: UUID, boxItemId: String) {
        try {
            inventory.consumeAvailable(accountId, boxItemId, 1)
        } catch (error: IllegalArgumentException) {
            throw IllegalArgumentException("SELECTOR_BOX_NOT_OWNED", error)
        }
    }
    override fun preflightCreditTicketsLocked(accountId: UUID, amount: Long) {
        require(amount >= 0) { "INVALID_TICKET_AMOUNT" }
        if (amount == 0L) return
        jdbc.sql("select cosmetic_ticket_balance from character where account_id=:account for update")
            .param("account", accountId).query(Long::class.java).single().let { Math.addExact(it, amount) }
    }

    override fun creditTicketsLocked(accountId: UUID, amount: Long, sourceId: UUID) {
        require(amount >= 0) { "INVALID_TICKET_AMOUNT" }
        if (amount == 0L) return
        val changed = jdbc.sql("update character set cosmetic_ticket_balance=cosmetic_ticket_balance+:amount where account_id=:account and cosmetic_ticket_balance+:amount>=0")
            .params(mapOf("account" to accountId, "amount" to amount)).update()
        require(changed == 1) { "COSMETIC_TICKET_OVERFLOW" }
    }
}
