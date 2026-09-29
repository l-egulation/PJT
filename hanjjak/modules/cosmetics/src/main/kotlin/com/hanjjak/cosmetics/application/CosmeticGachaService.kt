package com.hanjjak.cosmetics.application

import com.hanjjak.cosmetics.domain.*
import org.springframework.stereotype.Service
import org.springframework.transaction.annotation.Transactional
import java.util.UUID

@Service
class CosmeticGachaService(
    private val cosmetics: CosmeticsService,
    private val wallet: CosmeticsWallet,
    private val entropy: DrawEntropy,
) {
    data class PaymentEstimate(val ticketCost: Long, val riceCost: Long, val executable: Boolean)
    data class MilestoneView(val totalSuccessfulDraws: Long, val claimedBoxCount: Long, val claimableBoxCount: Long, val drawsUntilNextBox: Long, val boxItemId: String, val ownedBoxQuantity: Long)
    data class Banner(val bannerId: String, val setId: String, val displayName: String?, val singleRiceCost: Long, val ticketBalance: Long, val riceBalance: Long, val oneDraw: PaymentEstimate, val tenDraw: PaymentEstimate, val milestone: MilestoneView)
    data class CosmeticDefinitionView(val cosmeticId: String, val displayName: String?, val imageUrl: String?, val grade: CosmeticGrade, val slot: CosmeticSlot, val setId: String, val probabilityNumerator: Int, val probabilityDenominator: Long, val probabilityDisplay: String)
    data class BannerDetail(val banner: Banner, val gradeProbabilityMillionths: Map<CosmeticGrade, Int>, val pool: List<CosmeticDefinitionView>)
    data class Result(val cosmeticId: String, val grade: CosmeticGrade, @get:com.fasterxml.jackson.annotation.JsonProperty("isNew") @param:com.fasterxml.jackson.annotation.JsonProperty("isNew") val isNew: Boolean)
    data class DrawResponse(val bannerId: String, val ticketCost: Long, val riceCost: Long, val results: List<Result>, val collection: CollectionSnapshot)
    data class AuditedDraw(val response: DrawResponse, val audit: CosmeticAudit)
    fun banners(accountId: UUID): List<Banner> {
        require(entropy.isAvailable) { "GACHA_CONTENT_UNAVAILABLE" }
        val balance = wallet.balanceForUpdate(accountId)
        require(balance.unlocked) { "COSMETICS_LOCKED" }
        return cosmetics.content().sets.filter { it.bannerId != null && it.boxItemId != null }.map {
            val bannerId = requireNotNull(it.bannerId)
            val boxItemId = requireNotNull(it.boxItemId)
            val milestone = wallet.milestone(accountId, bannerId, boxItemId)
            Banner(bannerId, it.id, it.displayName, cosmetics.content().singleRiceCost, balance.tickets, balance.rice, estimate(balance, 1), estimate(balance, 10),
                MilestoneView(milestone.totalSuccessfulDraws, milestone.claimedBoxCount, milestone.claimableBoxCount, milestone.drawsUntilNextBox, boxItemId, milestone.ownedBoxQuantity))
        }
    }

    fun detail(accountId: UUID, bannerId: String): BannerDetail {
        require(entropy.isAvailable) { "GACHA_CONTENT_UNAVAILABLE" }
        val banner = banners(accountId).firstOrNull { it.bannerId == bannerId } ?: throw IllegalArgumentException("GACHA_BANNER_NOT_FOUND")
        val content = cosmetics.content()
        val set = content.sets.first { it.bannerId == bannerId }
        val pool = content.cosmetics.filter { it.grade != CosmeticGrade.LEGENDARY || it.id in set.members.values }.map { definition ->
            val numerator = content.gradeProbabilities.getValue(definition.grade)
            val count = content.cosmetics.count { it.grade == definition.grade && (it.grade != CosmeticGrade.LEGENDARY || it.id in set.members.values) }
            CosmeticDefinitionView(definition.id, definition.displayName, definition.imageUrl, definition.grade, definition.slot, definition.setId, numerator, 1_000_000L * count, formatProbability(numerator, count))
        }
        return BannerDetail(banner, content.gradeProbabilities, pool)
    }

    private fun formatProbability(numerator: Int, count: Int): String = "%.6f".format(java.util.Locale.ROOT, numerator.toDouble() / count.toDouble() / 10_000.0).trimEnd('0').trimEnd('.') + "%"

    @Transactional
    fun draw(accountId: UUID, bannerId: String, count: Int, key: UUID): DrawResponse = executeDraw(accountId, bannerId, count, key).first

    fun drawAudited(accountId: UUID, bannerId: String, count: Int, key: UUID): AuditedDraw {
        val (response, issued) = executeDraw(accountId, bannerId, count, key)
        val content = cosmetics.content()
        val audit = CosmeticAudit(
            CosmeticOperation.DRAW,
            content.version,
            mapOf("bannerId" to bannerId, "count" to count, "ticketSpend" to response.ticketCost, "riceSpend" to response.riceCost, "results" to response.results),
            DrawReproduction(issued.algorithmVersion, issued.keyId, issued.reproductionToken),
        )
        return AuditedDraw(response, audit)
    }

    @Transactional
    private fun executeDraw(accountId: UUID, bannerId: String, count: Int, key: UUID): Pair<DrawResponse, DrawEntropy.Issued> {
        require(count == 1 || count == 10) { "INVALID_DRAW_COUNT" }
        require(entropy.isAvailable) { "GACHA_CONTENT_UNAVAILABLE" }
        val content = cosmetics.content()
        val set = content.sets.firstOrNull { it.bannerId == bannerId } ?: throw IllegalArgumentException("GACHA_BANNER_NOT_FOUND")
        val balance = wallet.balanceForUpdate(accountId)
        require(balance.unlocked) { "COSMETICS_LOCKED" }
        val ticketCost = minOf(balance.tickets, count.toLong())
        val riceCost = (count - ticketCost) * content.singleRiceCost
        require(balance.rice >= riceCost) { "INSUFFICIENT_GACHA_FUNDS" }
        wallet.debit(accountId, riceCost, ticketCost, key)
        val issued = entropy.issue(accountId, bannerId, count, content.version)
        val results = mutableListOf<Result>()
        var snapshot = cosmetics.collection(accountId)
        repeat(count) {
            val grade = drawGrade(issued.random, content.gradeProbabilities)
            val pool = if (grade == CosmeticGrade.LEGENDARY) set.members.values.map { id -> content.cosmetics.first { it.id == id } } else content.cosmetics.filter { it.grade == grade }
            val definition = pool[issued.random.nextInt(pool.size)]
            val received = cosmetics.receive(accountId, definition.id)
            snapshot = received.first
            results += Result(definition.id, grade, received.second)
        }
        wallet.addDraws(accountId, bannerId, count)
        return DrawResponse(bannerId, ticketCost, riceCost, results, snapshot) to issued
    }

    private fun estimate(balance: CosmeticsWallet.Balance, count: Int): PaymentEstimate {
        val tickets = minOf(balance.tickets, count.toLong())
        val rice = (count - tickets) * cosmetics.content().singleRiceCost
        return PaymentEstimate(tickets, rice, balance.rice >= rice)
    }
    private fun drawGrade(random: DrawRandom, probabilities: Map<CosmeticGrade, Int>): CosmeticGrade {
        val roll = random.nextInt(1_000_000)
        var cumulative = 0
        for (grade in CosmeticGrade.entries) {
            cumulative += probabilities.getValue(grade)
            if (roll < cumulative) return grade
        }
        error("invalid probability table")
    }

    @Transactional
    fun claimMilestone(accountId: UUID, bannerId: String, count: Long): Long {
        cosmetics.requireUnlocked(accountId)
        val set = cosmetics.content().sets.firstOrNull { it.bannerId == bannerId && it.boxItemId != null } ?: throw IllegalArgumentException("GACHA_BANNER_NOT_FOUND")
        return wallet.claimBoxes(accountId, bannerId, requireNotNull(set.boxItemId), count)
    }

    @Transactional
    fun openBox(accountId: UUID, boxItemId: String, cosmeticId: String): CollectionSnapshot {
        cosmetics.requireUnlocked(accountId)
        val content = cosmetics.content()
        content.sets.firstOrNull { it.boxItemId == boxItemId } ?: throw IllegalArgumentException("SELECTOR_BOX_NOT_FOUND")
        val selected = content.cosmetics.firstOrNull { it.id == cosmeticId }
        require(selected?.grade == CosmeticGrade.LEGENDARY) { "INVALID_SELECTOR_COSMETIC" }
        wallet.openBox(accountId, boxItemId)
        return cosmetics.receive(accountId, cosmeticId).first
    }

}
