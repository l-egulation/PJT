package com.hanjjak.battle.application

import com.fasterxml.jackson.core.type.TypeReference
import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.battle.domain.ProgressionBackfillReport
import com.hanjjak.inventory.application.InventoryReservationService
import com.hanjjak.inventory.domain.ItemReward
import com.hanjjak.progression.application.ProgressionRewardCatalog
import org.springframework.core.io.ClassPathResource
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.stereotype.Service
import java.time.OffsetDateTime
import java.util.UUID

/** Read-only preview and resumable orchestration for existing-account migration. */
@Service
open class ProgressionRebalanceBackfillService(
    private val jdbc: JdbcClient,
    private val processor: ProgressionRebalanceAccountProcessor,
    private val catalog: ProgressionRewardCatalog,
    private val inventory: InventoryReservationService,
    private val mapper: ObjectMapper,
) {
    @org.springframework.transaction.annotation.Transactional(readOnly = true)
    open fun preview(batchSize: Int = DEFAULT_BATCH_SIZE): ProgressionBackfillReport {
        require(batchSize > 0) { "INVALID_BATCH_SIZE" }
        var cursor: UUID? = null
        var report = ProgressionBackfillReport.empty(targetAccounts = 0, targetStages = 0)
        while (true) {
            val accountIds = targetAccountPage(batchSize, cursor)
            if (accountIds.isEmpty()) return report
            val stagesByAccount = clearedStagesByAccount(accountIds)
            val existingByStage = existingRewardsByStage(accountIds)
            val bundlesByAccount = linkedMapOf<UUID, MutableList<List<ItemReward>>>()
            var pageStages = 0
            var alreadyApplied = 0
            var riceTotal = 0L
            val quantities = linkedMapOf<String, Long>()
            var immediate = 0
            var pending = 0
            accountIds.forEach { accountId ->
                val bundles = bundlesByAccount.getOrPut(accountId) { mutableListOf() }
                stagesByAccount[accountId].orEmpty().forEach { stageId ->
                    pageStages = Math.addExact(pageStages, 1)
                    val definition = catalog.firstClearOrNull(TARGET_VERSION, stageId) ?: return@forEach
                    val existing = existingByStage[accountId to stageId]
                    if (existing != null) {
                        alreadyApplied++
                        riceTotal = Math.addExact(riceTotal, existing.riceGranted)
                        existing.items.forEach { item ->
                            quantities[item.itemId] = Math.addExact(quantities[item.itemId] ?: 0L, item.quantity)
                        }
                        if (existing.itemStatus == "PENDING") pending++ else immediate++
                    } else {
                        riceTotal = Math.addExact(riceTotal, definition.rice)
                        definition.items.forEach { item ->
                            quantities[item.itemId] = Math.addExact(quantities[item.itemId] ?: 0L, item.quantity)
                        }
                        bundles += definition.items.map { ItemReward(it.itemId, it.quantity) }
                    }
                }
            }
            inventory.previewStackGrants(bundlesByAccount).values.flatten().forEach { plan ->
                if (plan.canGrant) immediate++ else pending++
            }
            report = report.mergePreview(
                ProgressionBackfillReport(
                    targetAccounts = accountIds.size,
                    targetStages = pageStages,
                    alreadyApplied = alreadyApplied,
                    riceTotal = riceTotal,
                    quantitiesByItem = quantities,
                    immediateCandidates = immediate,
                    pendingCandidates = pending,
                    failedAccountIds = emptyList(),
                    deferredAccountIds = emptyList(),
                    switchedAccountCount = 0,
                ),
            )
            cursor = accountIds.last()
        }
    }

    open fun apply(batchSize: Int): ProgressionBackfillReport {
        require(batchSize > 0) { "INVALID_BATCH_SIZE" }
        verifyManifestApplied()
        return applyAccountIds(targetAccountIds(batchSize))
    }

    /** Processes every eligible account once in stable UUID order; failed accounts remain eligible for a later retry. */
    open fun applyUntilComplete(batchSize: Int): ProgressionBackfillReport {
        require(batchSize > 0) { "INVALID_BATCH_SIZE" }
        verifyManifestApplied()
        var cursor: UUID? = null
        var report = ProgressionBackfillReport.empty(targetAccounts = 0, targetStages = 0)
        while (true) {
            val accountIds = targetAccountPage(batchSize, cursor)
            if (accountIds.isEmpty()) return report
            report = report.mergeApply(applyAccountIds(accountIds))
            cursor = accountIds.last()
        }
    }

    private fun applyAccountIds(accountIds: List<UUID>): ProgressionBackfillReport {
        val targetStages = accountIds.sumOf { accountId -> clearedStages(accountId).size }
        var report = ProgressionBackfillReport.empty(accountIds.size, targetStages)
        val failed = mutableListOf<UUID>()
        val deferred = mutableListOf<UUID>()
        accountIds.forEach { accountId ->
            try {
                report = report.merge(processor.applyOne(accountId))
            } catch (exception: RuntimeException) {
                if (exception.message == ProgressionRebalanceAccountProcessor.LEGACY_SESSION_ACTIVE) {
                    deferred += accountId
                } else {
                    failed += accountId
                    markFailedAttempt(accountId)
                }
            }
        }
        return report.copy(failedAccountIds = failed.sorted(), deferredAccountIds = deferred.sorted())
    }

    private fun targetAccountPage(batchSize: Int, cursor: UUID?): List<UUID> {
        val query = if (cursor == null) {
            jdbc.sql("$PREVIEW_TARGETS_SQL order by account_id limit :batchSize").params(TARGET_PARAMS)
        } else {
            jdbc.sql("$PREVIEW_TARGETS_SQL and account_id>:cursor order by account_id limit :batchSize")
                .params(TARGET_PARAMS).param("cursor", cursor)
        }
        return query.param("batchSize", batchSize).query(UUID::class.java).list()
    }

    private fun targetAccountIds(batchSize: Int): List<UUID> = jdbc.sql(
        "$APPLY_TARGETS_SQL limit :batchSize",
    ).params(TARGET_PARAMS).param("batchSize", batchSize).query(UUID::class.java).list()

    private fun clearedStages(accountId: UUID): List<String> = jdbc.sql(
        "select stage_id from stage_progress where account_id=:account and first_cleared_at is not null order by stage_id",
    ).param("account", accountId).query(String::class.java).list()

    private fun clearedStagesByAccount(accountIds: List<UUID>): Map<UUID, List<String>> {
        if (accountIds.isEmpty()) return emptyMap()
        return jdbc.sql(
            "select account_id,stage_id from stage_progress where account_id in (:accounts) " +
                "and first_cleared_at is not null order by account_id,stage_id",
        ).param("accounts", accountIds)
            .query { row, _ -> row.getObject("account_id", UUID::class.java) to row.getString("stage_id") }
            .list().groupBy({ it.first }, { it.second })
    }

    private fun existingRewardsByStage(accountIds: List<UUID>): Map<Pair<UUID, String>, FirstClearRewardRepository.RewardRow> {
        if (accountIds.isEmpty()) return emptyMap()
        return jdbc.sql(
            "select reward_id,account_id,stage_id,reward_version,source_id,rice_granted,unlocked_skill_id," +
                "item_status,items_json::text,required_slots,claimed_at from stage_first_clear_reward " +
                "where account_id in (:accounts) and reward_version=:version and item_status in ('CLAIMED','PENDING')",
        ).params(mapOf("accounts" to accountIds, "version" to TARGET_VERSION))
            .query { row, _ -> rewardRow(row) }.list().associateBy { it.accountId to it.stageId }
    }

    private fun rewardRow(row: java.sql.ResultSet) = FirstClearRewardRepository.RewardRow(
        row.getObject("reward_id", UUID::class.java), row.getObject("account_id", UUID::class.java),
        row.getString("stage_id"), row.getString("reward_version"), row.getObject("source_id", UUID::class.java),
        row.getLong("rice_granted"), row.getString("unlocked_skill_id"), row.getString("item_status"),
        mapper.readValue(row.getString("items_json"), object : TypeReference<List<ItemReward>>() {}),
        row.getInt("required_slots"), row.getObject("claimed_at", OffsetDateTime::class.java)?.toInstant(),
    )

    private fun markFailedAttempt(accountId: UUID) {
        jdbc.sql(
            "update account_balance_state set backfill_last_attempted_at=now()," +
                "backfill_failure_count=backfill_failure_count+1 where account_id=:account and retroactive_completed_at is null",
        ).param("account", accountId).update()
    }

    private fun verifyManifestApplied() {
        val manifest = ClassPathResource("progression-rebalance-v1/manifest.json")
        require(manifest.exists()) { "BALANCE_VERSION_NOT_APPLIED" }
        val root = manifest.inputStream.use(mapper::readTree)
        require(root.path("authority").asText() == "applied") { "BALANCE_VERSION_NOT_APPLIED" }
        require(root.path("contentVersion").asText() == TARGET_VERSION) { "BALANCE_VERSION_NOT_APPLIED" }
    }

    private fun ProgressionBackfillReport.merge(result: AccountBackfillResult): ProgressionBackfillReport = copy(
        alreadyApplied = alreadyApplied + result.alreadyApplied,
        riceTotal = Math.addExact(riceTotal, result.riceTotal),
        quantitiesByItem = result.quantitiesByItem.entries.fold(quantitiesByItem.toMutableMap()) { all, (item, quantity) ->
            all[item] = Math.addExact(all[item] ?: 0L, quantity)
            all
        },
        immediateCandidates = immediateCandidates + result.immediateCandidates,
        pendingCandidates = pendingCandidates + result.pendingCandidates,
        switchedAccountCount = switchedAccountCount + if (result.switched) 1 else 0,
    )

    private fun ProgressionBackfillReport.mergePreview(page: ProgressionBackfillReport): ProgressionBackfillReport = copy(
        targetAccounts = Math.addExact(targetAccounts, page.targetAccounts),
        targetStages = Math.addExact(targetStages, page.targetStages),
        alreadyApplied = Math.addExact(alreadyApplied, page.alreadyApplied),
        riceTotal = Math.addExact(riceTotal, page.riceTotal),
        quantitiesByItem = page.quantitiesByItem.entries.fold(quantitiesByItem.toMutableMap()) { all, (item, quantity) ->
            all[item] = Math.addExact(all[item] ?: 0L, quantity)
            all
        },
        immediateCandidates = Math.addExact(immediateCandidates, page.immediateCandidates),
        pendingCandidates = Math.addExact(pendingCandidates, page.pendingCandidates),
    )

    private fun ProgressionBackfillReport.mergeApply(page: ProgressionBackfillReport): ProgressionBackfillReport = copy(
        targetAccounts = Math.addExact(targetAccounts, page.targetAccounts),
        targetStages = Math.addExact(targetStages, page.targetStages),
        alreadyApplied = Math.addExact(alreadyApplied, page.alreadyApplied),
        riceTotal = Math.addExact(riceTotal, page.riceTotal),
        quantitiesByItem = page.quantitiesByItem.entries.fold(quantitiesByItem.toMutableMap()) { all, (item, quantity) ->
            all[item] = Math.addExact(all[item] ?: 0L, quantity)
            all
        },
        immediateCandidates = Math.addExact(immediateCandidates, page.immediateCandidates),
        pendingCandidates = Math.addExact(pendingCandidates, page.pendingCandidates),
        failedAccountIds = (failedAccountIds + page.failedAccountIds).distinct().sorted(),
        deferredAccountIds = (deferredAccountIds + page.deferredAccountIds).distinct().sorted(),
        switchedAccountCount = Math.addExact(switchedAccountCount, page.switchedAccountCount),
    )

    companion object {
        const val TARGET_VERSION = ProgressionRebalanceAccountProcessor.TARGET_VERSION
        const val LEGACY_VERSION = ProgressionRebalanceAccountProcessor.LEGACY_VERSION
        private const val DEFAULT_BATCH_SIZE = 100
        private val TARGET_PARAMS = mapOf("legacy" to LEGACY_VERSION, "version" to TARGET_VERSION)
        private const val PREVIEW_TARGETS_SQL =
            "select account_id from account_balance_state where retroactive_completed_at is null " +
                "and balance_version in (:legacy,:version)"
        private const val APPLY_TARGETS_SQL =
            "select account_id from account_balance_state where retroactive_completed_at is null " +
                "and balance_version in (:legacy,:version) order by backfill_last_attempted_at nulls first,account_id"
    }
}
