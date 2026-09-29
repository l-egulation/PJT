package com.hanjjak.gameapi.raid

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.gameapi.character.CharacterStatsService
import com.hanjjak.raid.application.RaidBattleHandoff
import com.hanjjak.raid.application.RaidCombatSnapshot
import com.hanjjak.raid.application.RaidCombatSnapshotProvider
import com.hanjjak.raid.application.RaidContentPort
import com.hanjjak.raid.application.RaidContentSnapshot
import com.hanjjak.raid.application.RaidMainBattlePort
import com.hanjjak.raid.application.RaidProgressPort
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.stereotype.Component
import java.time.Instant
import java.util.UUID

import com.hanjjak.cosmetics.application.CosmeticsWallet
import com.hanjjak.events.application.DomainEventPublisher
import com.hanjjak.inventory.application.InventoryRewardService
import com.hanjjak.raid.application.RaidRewardPort
import com.hanjjak.raid.domain.RaidRewardBundle
import com.hanjjak.wallet.application.WalletService
import org.springframework.transaction.annotation.Transactional
@Component
class CharacterRaidCombatSnapshotProvider(
    private val characterStats: CharacterStatsService,
) : RaidCombatSnapshotProvider {
    override fun snapshot(accountId: UUID): RaidCombatSnapshot {
        val snapshot = characterStats.snapshot(accountId)
        val cosmeticEffectIds = snapshot.calculation.stats.flatMap { stat -> stat.sources.filter { it.applied && it.category == "COSMETIC" }.map { it.sourceId } }.distinct().sorted()
        return RaidCombatSnapshot(
            snapshot.calculation.fighter,
            snapshot.calculation.skills,
            snapshot.view.contentVersion,
            snapshot.view.level,
            defense = 0, // Player defense is not a current combat stat; boss damage is direct HP loss.
            skillIds = snapshot.unlockedSkillIds,
            mainGemPreset = "MAIN",
            cosmeticEffectIds = cosmeticEffectIds,
            activeSkillLoadout = snapshot.activeSkillLoadout,
        )
    }
}

@Component
class JsonRaidContentPort(
    mapper: ObjectMapper,
) : RaidContentPort {
    private val content: RaidContentSnapshot
    init {
        val node = mapper.readTree(requireNotNull(javaClass.getResourceAsStream("/raids/raids.json")) { "raid content missing" })
        content = RaidContentSnapshot(
            contentVersion = node.path("contentVersion").asText(),
            rewardVersion = node.path("contentVersion").asText(),
            unlockStageId = node.path("unlockStageId").asText(),
            sealTarget = node.path("sealTarget").asLong(),
            bossInitialAttack = node.path("bossInitialAttack").asInt(),
            bossInitialDefense = node.path("bossInitialDefense").asInt(),
            maxTicks = node.path("attemptLimitTicks").asInt(3_000),
            escalationIntervalTicks = node.path("escalationIntervalTicks").asInt(50),
            escalationBasisPoints = node.path("escalationBasisPoints").asInt(500),
            gradeDamageThresholds = node.path("gradeDamageThresholds").properties().asSequence().associate { it.key to it.value.asLong() },
            sealContributions = node.path("sealContributions").properties().asSequence().associate { it.key to it.value.asInt() },
            authority = node.path("authority").asText("working"),
        )
        com.hanjjak.raid.domain.RaidRules.validateTables(content.gradeDamageThresholds, content.sealContributions)
    }
    override fun current(): RaidContentSnapshot = content
}

@Component
class JdbcRaidProgressPort(
    private val jdbc: JdbcClient,
) : RaidProgressPort {
    override fun hasFirstCleared(accountId: UUID, stageId: String): Boolean = jdbc.sql(
        "select count(*) from stage_progress where account_id=:account and stage_id=:stage and first_cleared_at is not null",
    ).params(mapOf("account" to accountId, "stage" to stageId)).query(Long::class.java).single() > 0
}

@Component
class JdbcRaidMainBattlePort(
    private val battles: com.hanjjak.battle.application.BattleSessionService,
) : RaidMainBattlePort {

    override fun pauseLockedWithHandoff(accountId: UUID, commandId: UUID): RaidBattleHandoff? {
        battles.abortForRaidWithHandoffLocked(accountId)?.let { return RaidBattleHandoff(it.battleSessionId, it.gameSessionId, it.stageId) }
        return null
    }
    override fun resumeLocked(accountId: UUID, commandId: UUID, handoff: RaidBattleHandoff): RaidBattleHandoff? {
        return battles.resumeAfterRaidLocked(accountId, commandId, handoff.battleSessionId, handoff.gameSessionId)?.let { RaidBattleHandoff(it.battleSessionId, handoff.gameSessionId, it.stageId) }
    }
}
/** Composition-root adapter for one atomic raid reward bundle. */
@Component
class JdbcRaidRewardPort(
    private val jdbc: JdbcClient,
    private val wallet: WalletService,
    private val inventory: InventoryRewardService,
    private val cosmetics: CosmeticsWallet,
) : RaidRewardPort {
    override fun preflightLocked(accountId: UUID, bundle: RaidRewardBundle) {
        require(bundle.cosmeticTickets >= 0 && bundle.gemBoxes >= 0 && bundle.rice >= 0) { "INVALID_REWARD_BUNDLE" }
        wallet.preflightCreditLocked(accountId, bundle.rice)
        cosmetics.preflightCreditTicketsLocked(accountId, bundle.cosmeticTickets)
        inventory.preflightGrantLocked(accountId, "GEM_BOX", bundle.gemBoxes)
    }

    override fun creditLocked(accountId: UUID, sourceId: UUID, bundle: RaidRewardBundle) {
        if (bundle.rice > 0) wallet.creditLocked(accountId, bundle.rice, "RAID_REWARD", sourceId)
        if (bundle.cosmeticTickets > 0) cosmetics.creditTicketsLocked(accountId, bundle.cosmeticTickets, sourceId)
        if (bundle.gemBoxes > 0) inventory.grantLocked(accountId, "GEM_BOX", bundle.gemBoxes)
}
data class RaidApiRanking(
    val sessionId: UUID,
    val settled: Boolean,
    val currentRank: Int?,
    val currentSealContribution: Long,
    val topEntries: List<RaidApiRankingEntry>,
    val nextCursor: String?,
    val totalEligibleAccounts: Long,
)

data class RaidApiRankingEntry(val rank: Int, val nickname: String, val sealContribution: Long)

@Component
class RaidApiQueryAdapter(
    private val jdbc: JdbcClient,
    private val repository: com.hanjjak.raid.application.RaidRepository,
) : com.hanjjak.raid.application.RaidQueryPort {
    override fun stateVersion(accountId: UUID): Long = jdbc.sql("select state_version from account where id=:account")
        .param("account", accountId).query(Long::class.java).single()

    override fun sealedContribution(sessionId: com.hanjjak.raid.domain.RaidSessionId): Long = repository.contributions(sessionId)
        .fold(0L) { total, contribution -> Math.addExact(total, contribution.sealContribution) }

    override fun ranking(accountId: UUID, session: com.hanjjak.raid.domain.RaidSession, cursor: String?, limit: Int): com.hanjjak.raid.application.RaidRankingApiView {
        require(limit in 1..100) { "INVALID_LIMIT" }
        val sessionId = com.hanjjak.raid.domain.RaidSessionId(session.sessionId.value)
        val ranks = if (session.status == com.hanjjak.raid.domain.RaidSessionStatus.OPEN) {
            com.hanjjak.raid.application.RaidSettlementService.computeRanks(sessionId, repository.contributions(sessionId))
        } else repository.finalRanks(sessionId)
        val public = com.hanjjak.raid.application.RaidSettlementService.publicRanks(
            ranks, accountId, com.hanjjak.raid.domain.RaidRankCursor.decode(cursor), limit,
        )
        val accountIds = (public.items + listOfNotNull(public.caller)).map { it.accountId }.distinct()
        val names = if (accountIds.isEmpty()) emptyMap() else jdbc.sql("select account_id,nickname from character where account_id in (:accounts)")
            .param("accounts", accountIds).query { rs, _ -> rs.getObject("account_id", UUID::class.java) to rs.getString("nickname") }.list().toMap()
        fun entry(row: com.hanjjak.raid.domain.RaidRankingRow) = com.hanjjak.raid.application.RaidRankingEntryView(row.competitiveRank, names[row.accountId] ?: "", row.sealContribution)
        return com.hanjjak.raid.application.RaidRankingApiView(
            session.sessionId.value,
            session.status != com.hanjjak.raid.domain.RaidSessionStatus.OPEN,
            public.caller?.competitiveRank,
            public.caller?.sealContribution ?: 0L,
            public.items.map(::entry),
            public.nextCursor?.encode(),
            ranks.size.toLong(),
        )
    }

    override fun claims(accountId: UUID, cursor: String?): com.hanjjak.raid.application.RaidClaimsView {
        val all = repository.claims(accountId).sortedWith(compareBy<com.hanjjak.raid.domain.RaidClaim> { it.claimableAt }.thenBy { it.claimId.value })
        val start = cursor?.let { raw ->
            val id = raw.substringAfterLast(':', missingDelimiterValue = "")
            val parsed = runCatching { UUID.fromString(id) }.getOrElse { throw IllegalArgumentException("INVALID_CLAIM_CURSOR") }
            val index = all.indexOfFirst { it.claimId.value == parsed }
            require(index >= 0) { "INVALID_CLAIM_CURSOR" }
            index + 1
        } ?: 0
        val page = all.drop(start).take(100)
        val next = page.lastOrNull()?.takeIf { start + page.size < all.size }?.let { "${it.claimableAt.toEpochMilli()}:${it.claimId.value}" }
        fun view(claim: com.hanjjak.raid.domain.RaidClaim) = com.hanjjak.raid.application.RaidClaimView(claim.claimId.value, claim.sessionId.value, claim.kind, claim.status, claim.bundle, claim.claimableAt, claim.claimedAt)
        return com.hanjjak.raid.application.RaidClaimsView(page.map(::view), all.count { it.status == com.hanjjak.raid.domain.RaidClaimStatus.CLAIMABLE }.toLong(), all.count { it.status == com.hanjjak.raid.domain.RaidClaimStatus.CLAIMED }.toLong(), next)
    }
}
}
