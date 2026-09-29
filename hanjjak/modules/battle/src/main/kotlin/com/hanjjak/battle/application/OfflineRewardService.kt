package com.hanjjak.battle.application

import com.fasterxml.jackson.annotation.JsonInclude
import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.account.application.BalanceVersionService
import com.hanjjak.account.application.OfflineRewardLifecycle
import com.hanjjak.inventory.application.InventoryRewardService
import com.hanjjak.inventory.application.MaterialDropCatalog
import com.hanjjak.inventory.application.MaterialPreferenceProvider
import com.hanjjak.inventory.domain.ItemReward
import com.hanjjak.inventory.domain.RewardResult
import com.hanjjak.progression.application.ProgressionRewardService
import com.hanjjak.progression.application.ProgressionRewardCatalog
import com.hanjjak.progression.domain.ProgressionGrant
import com.hanjjak.sim.CombatSimulator
import com.hanjjak.stage.application.StageCatalog
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.stereotype.Service
import org.springframework.transaction.annotation.Transactional
import java.math.BigDecimal
import java.math.RoundingMode
import java.time.Clock
import java.time.Duration
import java.time.Instant
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID

@Service
class OfflineRewardService(
    private val mapper: ObjectMapper,
    private val jdbc: JdbcClient,
    private val stages: StageCatalog,
    private val battles: StageBattleService,
    private val stats: BattleStatsProvider,
    private val materials: MaterialPreferenceProvider,
    private val drops: MaterialDropCatalog,
    private val rewardCatalog: ProgressionRewardCatalog,
    private val balanceVersions: BalanceVersionService,
    private val progression: ProgressionRewardService,
    private val inventory: InventoryRewardService,
    private val clock: Clock,
    private val settings: OfflineRewardSettings,
) : OfflineRewardLifecycle {
    @JsonInclude(JsonInclude.Include.ALWAYS)
    data class RewardLine(val itemId: String, val quantity: Long)

    @JsonInclude(JsonInclude.Include.ALWAYS)
    data class Pending(
        val jobId: UUID,
        val status: String,
        val stageId: String,
        val startedAt: Instant,
        val lastHeartbeatAt: Instant,
        val accrualEndedAt: Instant?,
        val eligibleSeconds: Long,
        val offlineSeconds: Long,
        val experienceGained: Long,
        val riceGained: Long,
        val rewards: List<RewardLine>,
    )

    @JsonInclude(JsonInclude.Include.ALWAYS)
    data class ClaimResult(
        val jobId: UUID,
        val status: String,
        val stageId: String,
        val eligibleSeconds: Long,
        val experienceGained: Long,
        val riceGained: Long,
        val rewards: RewardResult,
        val claimedAt: Instant,
        val offlineSeconds: Long = eligibleSeconds,
    )

    private data class Job(
        val id: UUID,
        val accountId: UUID,
        val gameSessionId: UUID,
        val stageId: String,
        val contentVersion: String,
        val startedAt: Instant,
        val lastHeartbeatAt: Instant,
        val accrualEndedAt: Instant?,
        val status: String,
        val rateJson: String,
        val resultJson: String?,
    )

    private data class Rate(
        val experiencePerMinute: BigDecimal,
        val ricePerMinute: BigDecimal,
        val itemsPerMinute: Map<String, BigDecimal>,
    )

    private data class Frozen(
        val eligibleSeconds: Long,
        val experienceGained: Long,
        val riceGained: Long,
        val rewards: List<RewardLine>,
        val offlineSeconds: Long? = null,
    )

    @Transactional
    override fun startForSession(accountId: UUID, gameSessionId: UUID) {
        lockAccount(accountId)
        if (hasOpenJob(accountId)) return
        val stageId = currentStageId(accountId)
        val now = clock.instant()
        val contentVersion = balanceVersions.current(accountId)
        val rate = mapper.writeValueAsString(calculateRate(accountId, stageId, contentVersion))
        val combat = mapper.writeValueAsString(stats.forAccount(accountId))
        jdbc.sql(
            """insert into offline_job(
                 job_id,account_id,game_session_id,stage_id,content_version,reward_rate_snapshot_json,
                 combat_snapshot_json,started_at,last_heartbeat_at,status,created_at,updated_at
               ) values (
                 :job,:account,:session,:stage,:version,cast(:rate as jsonb),cast(:combat as jsonb),
                 :now,:now,'ACTIVE',:now,:now
               )""".trimIndent(),
        ).params(
            mapOf(
                "job" to UUID.randomUUID(),
                "account" to accountId,
                "session" to gameSessionId,
                "stage" to stageId,
                "version" to contentVersion,
                "rate" to rate,
                "combat" to combat,
                "now" to utc(now),
            ),
        ).update()
    }
    @Transactional
    override fun touchForSession(accountId: UUID, gameSessionId: UUID, heartbeatAt: Instant) {
        jdbc.sql(
            """update offline_job set last_heartbeat_at=:heartbeat,updated_at=:heartbeat
               where account_id=:account and game_session_id=:session and status='ACTIVE'""".trimIndent(),
        ).params(mapOf("account" to accountId, "session" to gameSessionId, "heartbeat" to utc(heartbeatAt))).update()
    }

    @Transactional
    override fun markDisconnectedForAccount(accountId: UUID, disconnectedAt: Instant) {
        jdbc.sql(
            """update offline_job set last_heartbeat_at=:disconnected,updated_at=:disconnected
               where account_id=:account and status='ACTIVE'""".trimIndent(),
        ).params(mapOf("account" to accountId, "disconnected" to utc(disconnectedAt))).update()
    }

    @Transactional
    override fun cancelForSession(accountId: UUID, gameSessionId: UUID) {
        jdbc.sql("update offline_job set status='CANCELLED',updated_at=:now where account_id=:account and game_session_id=:session and status='ACTIVE'")
            .params(mapOf("account" to accountId, "session" to gameSessionId, "now" to utc(clock.instant()))).update()
    }

    @Transactional
    override fun cancelForAccount(accountId: UUID) {
        jdbc.sql("update offline_job set status='CANCELLED',updated_at=:now where account_id=:account and status='ACTIVE'")
            .params(mapOf("account" to accountId, "now" to utc(clock.instant()))).update()
    }

    @Transactional
    override fun freezeForSession(accountId: UUID, gameSessionId: UUID, reason: String) {
        lockAccount(accountId)
        findActive(accountId, gameSessionId)?.let { freezeOrCancel(it, clock.instant()) }
    }

    @Transactional
    override fun freezeForAccount(accountId: UUID, reason: String) {
        lockAccount(accountId)
        activeJobs(accountId).forEach { freezeOrCancel(it, clock.instant()) }
    }


    @Transactional
    fun pending(accountId: UUID): Pending? {
        lockAccount(accountId)
        val job = jdbc.sql(selectSql() + " where account_id=:account and status='CLAIMABLE' order by created_at desc,job_id desc limit 1")
            .param("account", accountId)
            .query(::readJob)
            .optional()
            .orElse(null)
            ?: return null
        return pending(job)
    }

    @Transactional
    fun claim(accountId: UUID, key: UUID): ClaimResult {
        lockAccount(accountId)
        val previous = jdbc.sql("select fingerprint,result_json::text from offline_reward_command where account_id=:account and idempotency_key=:key")
            .params(mapOf("account" to accountId, "key" to key))
            .query { row, _ ->
                require(row.getString("fingerprint") == CLAIM_FINGERPRINT) { "IDEMPOTENCY_KEY_REUSED" }
                val json = row.getString("result_json")
                val result = mapper.readValue(json, ClaimResult::class.java)
                if (mapper.readTree(json).has("offlineSeconds")) result else result.copy(offlineSeconds = offlineSecondsForJob(result.jobId, result.eligibleSeconds))
            }
            .optional()
            .orElse(null)
        if (previous != null) return previous

        val now = clock.instant()
        val job = jdbc.sql(selectSql() + " where account_id=:account and status='CLAIMABLE' order by created_at desc,job_id desc limit 1 for update")
            .param("account", accountId)
            .query(::readJob)
            .optional()
            .orElseThrow { IllegalArgumentException("OFFLINE_REWARD_NOT_FOUND") }
        val frozen = mapper.readValue(requireNotNull(job.resultJson), Frozen::class.java)
        val reward = inventory.grantOnce(
            accountId,
            job.id,
            "offline:${job.stageId}:${job.contentVersion}",
            frozen.rewards.map { ItemReward(it.itemId, it.quantity) },
        ).result
        val growth = progression.grantOffline(accountId, job.id, ProgressionGrant(frozen.experienceGained, frozen.riceGained)).result
        val offlineSeconds = frozen.offlineSeconds ?: offlineSeconds(job)
        val result = ClaimResult(job.id, "CLAIMED", job.stageId, frozen.eligibleSeconds, growth.experienceGained, growth.riceGained, reward, now, offlineSeconds)
        jdbc.sql("update offline_job set status='CLAIMED',claimed_at=:now,updated_at=:now where job_id=:job")
            .params(mapOf("job" to job.id, "now" to utc(now))).update()
        jdbc.sql("insert into offline_reward_command(command_id,account_id,idempotency_key,fingerprint,result_json,created_at) values (:command,:account,:key,:fingerprint,cast(:result as jsonb),:now)")
            .params(mapOf("command" to UUID.randomUUID(), "account" to accountId, "key" to key, "fingerprint" to CLAIM_FINGERPRINT, "result" to mapper.writeValueAsString(result), "now" to utc(now))).update()
        jdbc.sql("select id from game_session where account_id=:account and status='ACTIVE' order by created_at desc limit 1")
            .param("account", accountId).query(UUID::class.java).optional().ifPresent { startForSession(accountId, it) }
        return result
    }
    fun stateVersion(accountId: UUID): Long = jdbc.sql("select state_version from account where id=:account")
        .param("account", accountId).query(Long::class.java).single()


    private fun freezeOrCancel(job: Job, endedAt: Instant) {
        val policy = settings.forAccount(job.accountId)
        val offlineSeconds = OfflineRewardPolicy.offlineSeconds(job.lastHeartbeatAt, endedAt)
        val seconds = OfflineRewardPolicy.eligibleSeconds(
            job.lastHeartbeatAt,
            endedAt,
            policy.gracePeriodSeconds,
            policy.bucketSeconds,
            policy.maxAccrualSeconds,
        )
        if (seconds == 0L) {
            jdbc.sql("update offline_job set status='CANCELLED',accrual_ended_at=:ended,updated_at=:ended where job_id=:job")
                .params(mapOf("job" to job.id, "ended" to utc(endedAt))).update()
            return
        }
        val contentVersion = balanceVersions.current(job.accountId)
        val stageId = currentStageId(job.accountId)
        val rate = calculateRate(job.accountId, stageId, contentVersion)
        val frozen = Frozen(
            eligibleSeconds = seconds,
            experienceGained = OfflineRewardPolicy.payout(rate.experiencePerMinute, seconds, policy.rewardMultiplier),
            riceGained = OfflineRewardPolicy.payout(rate.ricePerMinute, seconds, policy.rewardMultiplier),
            rewards = rate.itemsPerMinute.map { (itemId, perMinute) ->
                RewardLine(itemId, OfflineRewardPolicy.payout(perMinute, seconds, policy.rewardMultiplier))
            }.filter { it.quantity > 0 },
            offlineSeconds = offlineSeconds,
        )
        jdbc.sql("""update offline_job set stage_id=:stage,content_version=:version,reward_rate_snapshot_json=cast(:rate as jsonb),
            combat_snapshot_json=cast(:combat as jsonb),status='CLAIMABLE',accrual_ended_at=:ended,result_json=cast(:result as jsonb),updated_at=:ended
            where job_id=:job""").params(mapOf(
            "job" to job.id,
            "stage" to stageId,
            "version" to contentVersion,
            "rate" to mapper.writeValueAsString(rate),
            "combat" to mapper.writeValueAsString(stats.forAccount(job.accountId)),
            "ended" to utc(endedAt),
            "result" to mapper.writeValueAsString(frozen),
        )).update()
    }

    private fun currentStageId(accountId: UUID): String = jdbc.sql("select current_stage_id from account_runtime_state where account_id=:account")
        .param("account", accountId)
        .query(String::class.java)
        .optional()
        .orElse("stage.01-01")

    private fun calculateRate(accountId: UUID, stageId: String, contentVersion: String): Rate {
        val stage = stages.require(contentVersion, stageId)
        val cycle = battles.input(stageId, stats.forAccount(accountId), 1L, contentVersion)
        val minutes = BigDecimal(CombatSimulator.simulateWithEvents(cycle).result.elapsedTicks.coerceAtLeast(1) * 100L)
            .divide(BigDecimal(60_000), 12, RoundingMode.HALF_UP)
        val normal = rewardCatalog.grant(contentVersion, stageId, false)
        val boss = rewardCatalog.grant(contentVersion, stageId, true)
        val normalCount = if (stage.id.number == 10 || stage.bossOnly) 0 else 20
        val selected = materials.primaryMaterial(accountId)
        val table = drops.table(contentVersion)
        val dropContent = table.content
        val rolls = normalCount * dropContent.normalEnemyRolls + if (normalCount == 0) 0 else dropContent.bossRolls
        val expectedItems = BigDecimal(dropContent.quantityPerSuccess)
            .multiply(BigDecimal(dropContent.dropChanceBasisPoints))
            .divide(BigDecimal(10_000), 12, RoundingMode.HALF_UP)
            .multiply(BigDecimal("0.8"))
            .multiply(BigDecimal(rolls))
        val weights = dropContent.chapterGenerationWeights.getValue(stage.id.chapter)
        val totalWeight = weights.sum().toBigDecimal()
        val itemRates = weights.mapIndexedNotNull { index, weight ->
            if (weight == 0 || totalWeight == BigDecimal.ZERO) null
            else "${selected.name}_M${index + 1}" to expectedItems.multiply(BigDecimal(weight))
                .divide(totalWeight, 12, RoundingMode.HALF_UP)
                .divide(minutes, 12, RoundingMode.HALF_UP)
        }.toMap()
        return Rate(
            BigDecimal(normal.experience * normalCount + boss.experience).divide(minutes, 12, RoundingMode.HALF_UP),
            BigDecimal(normal.rice * normalCount + boss.rice).divide(minutes, 12, RoundingMode.HALF_UP),
            itemRates,
        )
    }

    private fun pending(job: Job): Pending {
        val result = job.resultJson?.let { mapper.readValue(it, Frozen::class.java) }
        val offlineSeconds = result?.offlineSeconds ?: offlineSeconds(job)
        return Pending(job.id, job.status, job.stageId, job.startedAt, job.lastHeartbeatAt, job.accrualEndedAt, result?.eligibleSeconds ?: 0, offlineSeconds, result?.experienceGained ?: 0, result?.riceGained ?: 0, result?.rewards ?: emptyList())
    }

    private fun offlineSeconds(job: Job): Long = job.accrualEndedAt
        ?.let { OfflineRewardPolicy.offlineSeconds(job.lastHeartbeatAt, it) }
        ?: 0

    private fun offlineSecondsForJob(jobId: UUID, fallback: Long): Long = jdbc.sql("select last_heartbeat_at,accrual_ended_at from offline_job where job_id=:job")
        .param("job", jobId)
        .query { row, _ ->
            val lastHeartbeatAt = row.getObject("last_heartbeat_at", OffsetDateTime::class.java).toInstant()
            val endedAt = row.getObject("accrual_ended_at", OffsetDateTime::class.java)?.toInstant()
            endedAt?.let { OfflineRewardPolicy.offlineSeconds(lastHeartbeatAt, it) } ?: fallback
        }
        .optional()
        .orElse(fallback)

    private fun hasOpenJob(accountId: UUID): Boolean = jdbc.sql("select exists(select 1 from offline_job where account_id=:account and status in ('ACTIVE','CLAIMABLE'))")
        .param("account", accountId).query(Boolean::class.java).single()

    private fun activeJobs(accountId: UUID): List<Job> = jdbc.sql(selectSql() + " where account_id=:account and status='ACTIVE' for update")
        .param("account", accountId).query(::readJob).list()

    private fun findActive(accountId: UUID, sessionId: UUID): Job? = jdbc.sql(selectSql() + " where account_id=:account and game_session_id=:session and status='ACTIVE' for update")
        .params(mapOf("account" to accountId, "session" to sessionId)).query(::readJob).optional().orElse(null)

    private fun readJob(row: java.sql.ResultSet, rowNumber: Int): Job = Job(
        row.getObject("job_id", UUID::class.java),
        row.getObject("account_id", UUID::class.java),
        row.getObject("game_session_id", UUID::class.java),
        row.getString("stage_id"),
        row.getString("content_version"),
        row.getObject("started_at", OffsetDateTime::class.java).toInstant(),
        row.getObject("last_heartbeat_at", OffsetDateTime::class.java).toInstant(),
        row.getObject("accrual_ended_at", OffsetDateTime::class.java)?.toInstant(),
        row.getString("status"),
        row.getString("reward_rate_snapshot_json"),
        row.getString("result_json"),
    )

    private fun lockAccount(accountId: UUID) {
        jdbc.sql("select id from account where id=:account for update").param("account", accountId).query(UUID::class.java).single()
    }
    private fun utc(value: Instant): OffsetDateTime = OffsetDateTime.ofInstant(value, ZoneOffset.UTC)
    private fun selectSql(): String = "select job_id,account_id,game_session_id,stage_id,content_version,started_at,last_heartbeat_at,accrual_ended_at,status,reward_rate_snapshot_json::text,result_json::text from offline_job"

    private companion object {
        const val CLAIM_FINGERPRINT = "claim"
    }
}

internal object OfflineRewardPolicy {
    const val TIMEOUT_SECONDS = 90L
    const val GRACE_PERIOD_SECONDS = 0L
    const val BUCKET_SECONDS = 60L
    const val MAX_ACCRUAL_SECONDS = 28_800L
    val REWARD_MULTIPLIER: BigDecimal = BigDecimal("0.5")

    fun offlineSeconds(lastHeartbeatAt: Instant, accrualEndedAt: Instant): Long =
        Duration.between(lastHeartbeatAt, accrualEndedAt).seconds.coerceAtLeast(0)

    fun eligibleSeconds(lastHeartbeatAt: Instant, accrualEndedAt: Instant, gracePeriodSeconds: Long = GRACE_PERIOD_SECONDS, bucketSeconds: Long = BUCKET_SECONDS, maxAccrualSeconds: Long = MAX_ACCRUAL_SECONDS): Long {
        require(bucketSeconds > 0) { "bucketSeconds must be positive" }
        val capped = (offlineSeconds(lastHeartbeatAt, accrualEndedAt) - gracePeriodSeconds).coerceIn(0, maxAccrualSeconds)
        return capped / bucketSeconds * bucketSeconds
    }

    fun payout(perMinute: BigDecimal, eligibleSeconds: Long, rewardMultiplier: BigDecimal = REWARD_MULTIPLIER): Long =
        perMinute.multiply(rewardMultiplier).multiply(BigDecimal(eligibleSeconds)).divide(BigDecimal(60), 0, RoundingMode.DOWN).longValueExact()
}
