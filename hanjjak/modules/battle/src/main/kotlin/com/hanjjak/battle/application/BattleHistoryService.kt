package com.hanjjak.battle.application

import com.fasterxml.jackson.annotation.JsonInclude
import com.fasterxml.jackson.databind.JsonNode
import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.sim.CycleInput
import com.hanjjak.sim.CycleResult
import com.hanjjak.sim.CombatRenderingEvent
import com.hanjjak.sim.FighterStats
import com.hanjjak.inventory.application.ItemCatalog
import com.hanjjak.inventory.domain.RewardResult
import com.hanjjak.progression.domain.ProgressionRewardResult
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.stereotype.Service
import java.sql.ResultSet
import java.time.Clock
import java.time.Instant
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID

@Service
class BattleHistoryService(
    private val jdbc: JdbcClient,
    private val mapper: ObjectMapper,
    private val clock: Clock,
    private val itemCatalog: ItemCatalog,
) : BattleHistoryRecorder {
    enum class EventType { STAGE_ENTERED, STAGE_CLEARED, STAGE_FAILED, DUNGEON_ENTERED, RETURNED }
    enum class SourceType { BATTLE_SESSION, GEM_DUNGEON_CHALLENGE }

    @JsonInclude(JsonInclude.Include.ALWAYS)
    data class Event(
        val eventId: UUID,
        val type: EventType,
        val occurredAt: Instant,
        val stageId: String?,
        val dungeonId: String?,
        val resultCode: String,
        val messageKey: String,
        val contentVersion: String?,
        val combatSnapshot: JsonNode?,
    )

    data class RewardSnapshot(
        val itemId: String,
        val displayName: String,
        val quantity: Long,
    )

    data class CombatSnapshot(
        val attack: Int,
        val maxHp: Int,
        val penetration: Int,
        val stageEnteredAt: Instant,
        val remainingHp: Int,
        val defeatedNormals: Int,
        val lastEnemyRemainingHp: Int,
        val normalCount: Int,
        val elapsedTicks: Int,
        val experienceGained: Long,
        val riceGained: Long,
        val rewards: List<RewardSnapshot>,
    ) {
        companion object {
            fun stage(
                input: CycleInput,
                result: CycleResult,
                reward: RewardResult,
                progression: ProgressionRewardResult,
                itemCatalog: ItemCatalog,
                stageEnteredAt: Instant,
                renderingTimeline: List<CombatRenderingEvent>?,
            ): CombatSnapshot {
                val eventRemainingHp = renderingTimeline?.asReversed()
                    ?.firstNotNullOfOrNull { event -> event.hpAfter.takeIf { event.target == "PLAYER" } }
                val remainingHp = if (result.success) eventRemainingHp ?: result.remainingHp else result.remainingHp
                val lastEnemyRemainingHp = renderingTimeline?.asReversed()
                    ?.firstNotNullOfOrNull { event -> event.hpAfter.takeIf { event.target == "ENEMY" } }
                    ?: 0
                return CombatSnapshot(
                    attack = input.player.attack,
                    maxHp = input.player.maxHp,
                    penetration = input.player.penetration,
                    stageEnteredAt = stageEnteredAt,
                    remainingHp = remainingHp,
                    defeatedNormals = result.defeatedNormals,
                    lastEnemyRemainingHp = lastEnemyRemainingHp,
                    normalCount = input.normalCount,
                    elapsedTicks = result.elapsedTicks,
                    experienceGained = progression.experienceGained,
                    riceGained = progression.riceGained,
                    rewards = reward.rewards.filter { it.grantedQuantity > 0 }.map {
                        RewardSnapshot(it.itemId, itemCatalog.find(it.itemId)?.displayName ?: it.itemId, it.grantedQuantity)
                    },
                )
            }
        }
    }

    fun recordStageEntered(accountId: UUID, sourceId: UUID, stageId: String, occurredAt: Instant = clock.instant()) = record(
        accountId,
        SourceType.BATTLE_SESSION,
        sourceId,
        EventType.STAGE_ENTERED,
        stageId,
        null,
        "STAGE_ENTERED",
        null,
        null,
        occurredAt,
    )

    fun recordStageResult(
        accountId: UUID,
        sourceId: UUID,
        stageId: String,
        contentVersion: String,
        input: CycleInput,
        result: CycleResult,
        reward: RewardResult,
        progression: ProgressionRewardResult,
        occurredAt: Instant = clock.instant(),
        renderingTimeline: List<CombatRenderingEvent>? = null,
    ) = record(
        accountId,
        SourceType.BATTLE_SESSION,
        sourceId,
        if (result.success) EventType.STAGE_CLEARED else EventType.STAGE_FAILED,
        stageId,
        null,
        if (result.success) "STAGE_CLEARED" else requireNotNull(result.failureCode) { "FAILED_BATTLE_REQUIRES_CODE" },
        contentVersion.takeUnless { result.success },
        CombatSnapshot.stage(
            input,
            result,
            reward,
            progression,
            itemCatalog,
            stageEnteredAt(accountId, sourceId) ?: occurredAt.minusMillis(result.elapsedTicks * TICK_DURATION_MILLISECONDS),
            renderingTimeline,
        ),
        occurredAt,
    )

    fun recordStageReturned(
        accountId: UUID,
        sourceId: UUID,
        stageId: String,
        resultCode: String,
        occurredAt: Instant = clock.instant(),
    ) = record(
        accountId,
        SourceType.BATTLE_SESSION,
        sourceId,
        EventType.RETURNED,
        stageId,
        null,
        resultCode,
        null,
        null,
        occurredAt,
    )

    override fun dungeonEntered(
        accountId: UUID,
        sourceId: UUID,
        dungeonId: String,
        contentVersion: String,
        player: FighterStats,
        occurredAt: Instant,
    ) = record(
        accountId,
        SourceType.GEM_DUNGEON_CHALLENGE,
        sourceId,
        EventType.DUNGEON_ENTERED,
        null,
        dungeonId,
        "DUNGEON_ENTERED",
        contentVersion,
        player,
        occurredAt,
    )

    override fun dungeonReturned(
        accountId: UUID,
        sourceId: UUID,
        dungeonId: String,
        resultCode: String,
        occurredAt: Instant,
    ) = record(
        accountId,
        SourceType.GEM_DUNGEON_CHALLENGE,
        sourceId,
        EventType.RETURNED,
        null,
        dungeonId,
        resultCode,
        null,
        null,
        occurredAt,
    )

    fun recent(accountId: UUID): List<Event> = jdbc.sql(
        """select event_id,event_type,occurred_at,stage_id,dungeon_id,result_code,content_version,combat_snapshot::text
           from battle_history_event where account_id=:account
           order by occurred_at desc,event_sequence desc limit :limit""",
    ).params(mapOf("account" to accountId, "limit" to HISTORY_LIMIT)).query(::mapEvent).list()

    fun latestStageFailure(accountId: UUID, stageId: String): Event? = jdbc.sql(
        """select event_id,event_type,occurred_at,stage_id,dungeon_id,result_code,content_version,combat_snapshot::text
           from battle_history_event
           where account_id=:account and stage_id=:stage and event_type='STAGE_FAILED'
           order by occurred_at desc,event_sequence desc limit 1""",
    ).params(mapOf("account" to accountId, "stage" to stageId)).query(::mapEvent).optional().orElse(null)
    fun stateVersion(accountId: UUID): Long = jdbc.sql("select state_version from account where id=:account")
        .param("account", accountId)
        .query(Long::class.java)
        .optional()
        .orElseThrow { IllegalArgumentException("AUTHENTICATION_REQUIRED") }


    private fun record(
        accountId: UUID,
        sourceType: SourceType,
        sourceId: UUID,
        eventType: EventType,
        stageId: String?,
        dungeonId: String?,
        resultCode: String,
        contentVersion: String?,
        combatSnapshot: Any?,
        occurredAt: Instant,
    ) {
        val statement = jdbc.sql(
            """insert into battle_history_event(
                 event_id,account_id,occurred_at,event_type,stage_id,dungeon_id,result_code,content_version,combat_snapshot,source_type,source_id
               ) values (
                 :event,:account,:occurred,:type,:stage,:dungeon,:result,:version,cast(:snapshot as jsonb),:sourceType,:sourceId
               ) on conflict(account_id,source_type,source_id,event_type,result_code) do nothing""",
        ).params(
            mapOf(
                "event" to eventId(accountId, sourceType, sourceId, eventType, resultCode),
                "account" to accountId,
                "occurred" to OffsetDateTime.ofInstant(occurredAt, ZoneOffset.UTC),
                "type" to eventType.name,
                "result" to resultCode,
                "sourceType" to sourceType.name,
                "sourceId" to sourceId,
            ),
        )
        statement.param("stage", stageId, java.sql.Types.VARCHAR)
        statement.param("dungeon", dungeonId, java.sql.Types.VARCHAR)
        statement.param("version", contentVersion, java.sql.Types.VARCHAR)
        statement.param("snapshot", combatSnapshot?.let(mapper::writeValueAsString), java.sql.Types.VARCHAR)
        statement.update()
        jdbc.sql(
            """delete from battle_history_event where event_id in (
                 select event_id from battle_history_event where account_id=:account
                 order by occurred_at desc,event_sequence desc offset :limit
               )""",
        ).params(mapOf("account" to accountId, "limit" to HISTORY_LIMIT)).update()
    }

    private fun mapEvent(row: ResultSet, ignored: Int): Event {
        val resultCode = row.getString("result_code")
        return Event(
            row.getObject("event_id", UUID::class.java),
            EventType.valueOf(row.getString("event_type")),
            row.getObject("occurred_at", OffsetDateTime::class.java).toInstant(),
            row.getString("stage_id"),
            row.getString("dungeon_id"),
            resultCode,
            messageKey(resultCode),
            row.getString("content_version"),
            row.getString("combat_snapshot")?.let(mapper::readTree),
        )
    }

    private fun stageEnteredAt(accountId: UUID, sourceId: UUID): Instant? = jdbc.sql(
        """select occurred_at from battle_history_event
           where account_id=:account and source_type='BATTLE_SESSION' and source_id=:sourceId and event_type='STAGE_ENTERED'
           order by occurred_at desc,event_sequence desc limit 1""",
    ).params(mapOf("account" to accountId, "sourceId" to sourceId))
        .query(OffsetDateTime::class.java)
        .optional()
        .map(OffsetDateTime::toInstant)
        .orElse(null)

    private fun eventId(
        accountId: UUID,
        sourceType: SourceType,
        sourceId: UUID,
        eventType: EventType,
        resultCode: String,
    ): UUID = UUID.nameUUIDFromBytes(
        "$accountId:$sourceType:$sourceId:$eventType:$resultCode".toByteArray(Charsets.UTF_8),
    )

    private fun messageKey(resultCode: String): String = "battle.history.${resultCode.lowercase().replace('_', '.')}"

    private companion object {
        const val HISTORY_LIMIT = 50
        const val TICK_DURATION_MILLISECONDS = 100L
    }
}
