package com.hanjjak.gems.application

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.gems.domain.*
import com.hanjjak.sim.*
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.transaction.annotation.Transactional
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.sql.ResultSet
import java.sql.Timestamp
import java.time.Clock
import java.time.Instant
import java.time.LocalDate
import java.time.ZoneId
import java.util.UUID

open class GemDungeonService(
    private val jdbc: JdbcClient,
    private val mapper: ObjectMapper,
    private val clock: Clock,
    private val gems: GemService,
    private val players: GemDungeonPlayerProvider,
    private val catalog: GemDungeonCatalog,
    private val inventory: com.hanjjak.inventory.application.InventoryReservationService,
    private val inventoryRepository: com.hanjjak.inventory.application.InventoryRepository,
    private val history: com.hanjjak.battle.application.BattleHistoryRecorder,
    private val policy: GemDungeonPolicy = GemDungeonPolicy(),
) {
    private data class StoredCommand(val fingerprint: String, val resultJson: String)
    private data class ChallengeRow(
        val challengeId: UUID,
        val boss: GemPreset,
        val stage: Int,
        val status: GemDungeonChallengeStatus,
        val minimumCompleteAt: Instant,
        val expiresAt: Instant,
        val rewardGemBoxes: Long,
        val contentVersion: String,
        val battleJson: String,
    )
    private val kst = ZoneId.of("Asia/Seoul")

    @Transactional
    open fun today(accountId: UUID, requestedBoss: GemPreset? = null): GemDungeonToday {
        lockAccount(accountId)
        requireUnlocked(accountId)
        expireActive(accountId)
        refreshTickets(accountId)
        val testAccess = hasTestAccess(accountId)
        val boss = resolveBoss(requestedBoss, testAccess)
        val progress = progress(accountId)
        val highest = progress.first { it.boss == boss }.highestClearedStage
        return GemDungeonToday(
            boss,
            tickets(accountId),
            secondsUntilNextTicket(accountId),
            progress,
            (highest + 1).takeIf { it <= 10 },
            highest.takeIf { it > 0 },
            active(accountId)?.let(::view),
            testAccess,
        )
    }

    @Transactional
    open fun start(accountId: UUID, idempotencyKey: UUID, requestedBoss: GemPreset? = null): GemDungeonChallengeView = command(
        accountId,
        idempotencyKey,
        "gem-dungeon\u0000start\u0000${requestedBoss?.name ?: "TODAY"}",
        GemDungeonChallengeView::class.java,
    ) {
        requireUnlocked(accountId)
        expireActive(accountId)
        require(active(accountId) == null) { "GEM_DUNGEON_ACTIVE_CHALLENGE" }
        refreshTickets(accountId)
        require(tickets(accountId) > 0) { "GEM_TICKET_EMPTY" }
        val boss = resolveBoss(requestedBoss, hasTestAccess(accountId))
        val highest = progress(accountId).first { it.boss == boss }.highestClearedStage
        val stage = highest + 1
        require(stage <= 10) { "GEM_DUNGEON_ALL_STAGES_CLEARED" }
        val challengeId = UUID.randomUUID()
        val definition = catalog.require(boss, stage)
        val base = players.snapshot(accountId, boss)
        val player = base.player
        val skills = base.skills
        val seed = idempotencyKey.mostSignificantBits xor idempotencyKey.leastSignificantBits
        val battle = DungeonCombatSimulator.simulate(
            DungeonCombatInput(
                catalog.contentVersion,
                seed,
                if (boss == GemPreset.SURVIVAL) DungeonMode.SURVIVE else DungeonMode.DEFEAT_BOSS,
                player,
                skills,
                DungeonBossStats(
                    definition.bossHp,
                    definition.bossAttack,
                    definition.defense,
                    catalog.durationTicks,
                    definition.strikeDamage,
                ),
            ),
        )
        val now = clock.instant()
        val minimumCompleteAt = now.plusMillis(battle.elapsedTicks * 100L)
        val expiresAt = minimumCompleteAt.plusSeconds(catalog.completionWindowSeconds)
        val reward = if (battle.success) 9L + stage else 0L
        val presetSnapshot = presetGemIds(accountId, boss)
        jdbc.sql(
            """insert into gem_dungeon_challenge(
                challenge_id,account_id,boss_type,stage,kst_date,status,seed,content_version,preset_snapshot,player_snapshot,battle_result,reward_gem_boxes,started_at,minimum_complete_at,expires_at
            ) values (:challenge,:account,:boss,:stage,:date,'ACTIVE',:seed,:version,cast(:preset as jsonb),cast(:player as jsonb),cast(:battle as jsonb),:reward,:started,:minimum,:expires)""",
        ).params(
            mapOf(
                "challenge" to challengeId, "account" to accountId, "boss" to boss.name, "stage" to stage,
                "date" to LocalDate.now(clock.withZone(kst)), "seed" to seed, "version" to catalog.contentVersion,
                "preset" to mapper.writeValueAsString(presetSnapshot), "player" to mapper.writeValueAsString(mapOf("player" to player, "skills" to skills)),
                "battle" to mapper.writeValueAsString(battle), "reward" to reward, "started" to dbTime(now),
                "minimum" to dbTime(minimumCompleteAt), "expires" to dbTime(expiresAt),
            ),
        ).update()
        history.dungeonEntered(
            accountId,
            challengeId,
            dungeonId(boss, stage),
            catalog.contentVersion,
            player,
            now,
        )
        reserveRewardCapacity(accountId, challengeId)
        incrementStateVersion(accountId)
        view(challenge(challengeId, accountId, false)!!)
    }

    @Transactional
    open fun complete(accountId: UUID, challengeId: UUID, idempotencyKey: UUID): GemDungeonCompletion = command(
        accountId,
        idempotencyKey,
        "gem-dungeon\u0000complete\u0000$challengeId",
        GemDungeonCompletion::class.java,
    ) {
        val row = challenge(challengeId, accountId, true) ?: throw IllegalArgumentException("GEM_DUNGEON_CHALLENGE_NOT_FOUND")
        if (row.status != GemDungeonChallengeStatus.ACTIVE) return@command GemDungeonCompletion(view(row), gems.state(accountId))
        val now = clock.instant()
        if (now.isAfter(row.expiresAt)) {
            resolveChallenge(row.challengeId, GemDungeonChallengeStatus.EXPIRED, "RELEASED", now)
            incrementStateVersion(accountId)
            return@command GemDungeonCompletion(view(challenge(challengeId, accountId, false)!!), gems.state(accountId))
        }
        require(!now.isBefore(row.minimumCompleteAt)) { "GEM_DUNGEON_COMPLETE_TOO_EARLY" }
        val battle = mapper.readValue(row.battleJson, DungeonCombatResult::class.java)
        if (!battle.success) {
            resolveChallenge(row.challengeId, GemDungeonChallengeStatus.FAILED, "RELEASED", now)
        } else {
            refreshTickets(accountId)
            require(tickets(accountId) > 0) { "GEM_TICKET_EMPTY" }
            jdbc.sql("update gem_account_state set tickets=tickets-1,last_ticket_at=case when tickets>=:max then :boundary else last_ticket_at end where account_id=:account")
                .params(mapOf("max" to policy.ticketMaxStock, "boundary" to dbTime(policy.ticketSlotStartedAt(now)), "account" to accountId)).update()
            grantGemBoxes(accountId, row.rewardGemBoxes)
            jdbc.sql("insert into gem_dungeon_progress(account_id,boss_type,highest_cleared_stage) values (:account,:boss,:stage) on conflict(account_id,boss_type) do update set highest_cleared_stage=greatest(gem_dungeon_progress.highest_cleared_stage,excluded.highest_cleared_stage)")
                .params(mapOf("account" to accountId, "boss" to row.boss.name, "stage" to row.stage)).update()
            jdbc.sql("insert into gem_dungeon_first_clear(account_id,boss_type,stage,claimed_at,challenge_id) values (:account,:boss,:stage,:now,:challenge) on conflict(account_id,boss_type,stage) do nothing")
                .params(mapOf("account" to accountId, "boss" to row.boss.name, "stage" to row.stage, "now" to dbTime(now), "challenge" to row.challengeId)).update()
            resolveChallenge(row.challengeId, GemDungeonChallengeStatus.SUCCEEDED, "CONSUMED", now)
        }
        history.dungeonReturned(
            accountId,
            row.challengeId,
            dungeonId(row.boss, row.stage),
            if (battle.success) "DUNGEON_CLEARED" else requireNotNull(battle.failureCode),
            now,
        )
        incrementStateVersion(accountId)
        GemDungeonCompletion(view(challenge(challengeId, accountId, false)!!), gems.state(accountId))
    }

    @Transactional
    open fun abort(accountId: UUID, challengeId: UUID, idempotencyKey: UUID): GemDungeonCompletion = command(
        accountId,
        idempotencyKey,
        "gem-dungeon\u0000abort\u0000$challengeId",
        GemDungeonCompletion::class.java,
    ) {
        val row = challenge(challengeId, accountId, true) ?: throw IllegalArgumentException("GEM_DUNGEON_CHALLENGE_NOT_FOUND")
        if (row.status == GemDungeonChallengeStatus.ACTIVE) {
            resolveChallenge(row.challengeId, GemDungeonChallengeStatus.ABORTED, "RELEASED", clock.instant())
            incrementStateVersion(accountId)
            history.dungeonReturned(
                accountId,
                row.challengeId,
                dungeonId(row.boss, row.stage),
                "DUNGEON_ABORTED",
                clock.instant(),
            )
        }
        GemDungeonCompletion(view(challenge(challengeId, accountId, false)!!), gems.state(accountId))
    }

    @Transactional
    open fun sweep(accountId: UUID, idempotencyKey: UUID, request: GemDungeonSweepRequest): GemDungeonSweepResult = command(
        accountId,
        idempotencyKey,
        "gem-dungeon\u0000sweep\u0000${request.count}",
        GemDungeonSweepResult::class.java,
    ) {
        requireUnlocked(accountId)
        expireActive(accountId)
        require(active(accountId) == null) { "GEM_DUNGEON_ACTIVE_CHALLENGE" }
        require(request.count in 1..policy.ticketMaxStock) { "INVALID_GEM_DUNGEON_SWEEP_COUNT" }
        refreshTickets(accountId)
        require(tickets(accountId) >= request.count) { "GEM_TICKET_EMPTY" }
        val boss = todayBoss()
        val stage = progress(accountId).first { it.boss == boss }.highestClearedStage
        require(stage > 0) { "GEM_DUNGEON_SWEEP_LOCKED" }
        requireRewardCapacity(accountId)
        val reward = ((9L + stage) * 3L / 10L) * request.count
        val now = clock.instant()
        jdbc.sql("update gem_account_state set tickets=tickets-:count,last_ticket_at=case when tickets>=:max then :boundary else last_ticket_at end where account_id=:account")
            .params(mapOf("count" to request.count, "max" to policy.ticketMaxStock, "boundary" to dbTime(policy.ticketSlotStartedAt(now)), "account" to accountId)).update()
        grantGemBoxes(accountId, reward)
        incrementStateVersion(accountId)
        GemDungeonSweepResult(boss, stage, request.count, reward, gems.state(accountId))
    }

    private fun reserveRewardCapacity(accountId: UUID, challengeId: UUID) {
        val kind = if (hasGemBoxStack(accountId)) "STACK_RIGHT" else {
            require(inventoryRepository.hasCapacityReservation(accountId, "GEM_BOX") || inventoryRepository.usedSlotCount(accountId) < com.hanjjak.inventory.domain.INVENTORY_MAX_SLOTS) { "INVENTORY_CAPACITY_EXCEEDED" }
            "EMPTY_SLOT"
        }
        jdbc.sql("insert into inventory_capacity_reservation(reservation_id,challenge_id,account_id,item_id,reservation_kind,status,created_at) values (:reservation,:challenge,:account,'GEM_BOX',:kind,'ACTIVE',:now)")
            .params(mapOf("reservation" to UUID.randomUUID(), "challenge" to challengeId, "account" to accountId, "kind" to kind, "now" to dbTime(clock.instant()))).update()
    }

    private fun requireRewardCapacity(accountId: UUID) {
        require(hasGemBoxStack(accountId) || inventoryRepository.hasCapacityReservation(accountId, "GEM_BOX") || inventoryRepository.usedSlotCount(accountId) < com.hanjjak.inventory.domain.INVENTORY_MAX_SLOTS) { "INVENTORY_CAPACITY_EXCEEDED" }
    }

    private fun hasGemBoxStack(accountId: UUID): Boolean = jdbc.sql("select count(*) from inventory_stack where account_id=:account and item_id='GEM_BOX' and quantity>0")
        .param("account", accountId).query(Long::class.java).single() > 0

    private fun resolveChallenge(challengeId: UUID, status: GemDungeonChallengeStatus, reservationStatus: String, now: Instant) {
        jdbc.sql("update gem_dungeon_challenge set status=:status,completed_at=:now where challenge_id=:challenge and status='ACTIVE'")
            .params(mapOf("status" to status.name, "now" to dbTime(now), "challenge" to challengeId)).update()
        jdbc.sql("update inventory_capacity_reservation set status=:status,resolved_at=:now where challenge_id=:challenge and status='ACTIVE'")
            .params(mapOf("status" to reservationStatus, "now" to dbTime(now), "challenge" to challengeId)).update()
    }

    private fun expireActive(accountId: UUID) {
        val now = clock.instant()
        jdbc.sql("select challenge_id from gem_dungeon_challenge where account_id=:account and status='ACTIVE' and expires_at<:now for update")
            .params(mapOf("account" to accountId, "now" to dbTime(now))).query(UUID::class.java).list()
            .forEach { resolveChallenge(it, GemDungeonChallengeStatus.EXPIRED, "RELEASED", now) }
    }

    private fun active(accountId: UUID): ChallengeRow? = jdbc.sql("select * from gem_dungeon_challenge where account_id=:account and status='ACTIVE'")
        .param("account", accountId).query(::mapChallenge).optional().orElse(null)

    private fun challenge(challengeId: UUID, accountId: UUID, lock: Boolean): ChallengeRow? = jdbc.sql("select * from gem_dungeon_challenge where challenge_id=:challenge and account_id=:account${if (lock) " for update" else ""}")
        .params(mapOf("challenge" to challengeId, "account" to accountId)).query(::mapChallenge).optional().orElse(null)

    private fun mapChallenge(row: ResultSet, ignored: Int) = ChallengeRow(
        row.getObject("challenge_id", UUID::class.java),
        GemPreset.valueOf(row.getString("boss_type")),
        row.getInt("stage"),
        GemDungeonChallengeStatus.valueOf(row.getString("status")),
        row.getTimestamp("minimum_complete_at").toInstant(),
        row.getTimestamp("expires_at").toInstant(),
        row.getLong("reward_gem_boxes"),
        row.getString("content_version"),
        row.getString("battle_result"),
    )

    private fun view(row: ChallengeRow) = GemDungeonChallengeView(
        row.challengeId, row.boss, row.stage, row.status, row.minimumCompleteAt, row.expiresAt,
        row.rewardGemBoxes, row.contentVersion, mapper.readValue(row.battleJson, DungeonCombatResult::class.java),
    )

    private fun progress(accountId: UUID): List<GemDungeonProgress> {
        val stored = jdbc.sql("select boss_type,highest_cleared_stage from gem_dungeon_progress where account_id=:account")
            .param("account", accountId).query { row, _ -> GemPreset.valueOf(row.getString("boss_type")) to row.getInt("highest_cleared_stage") }.list().toMap()
        return listOf(GemPreset.SURVIVAL, GemPreset.BERSERK, GemPreset.ARMORED).map { GemDungeonProgress(it, stored[it] ?: 0) }
    }

    private fun presetGemIds(accountId: UUID, preset: GemPreset): List<UUID> = jdbc.sql("select gem_id from gem_loadout where account_id=:account and preset=:preset order by slot_index")
        .params(mapOf("account" to accountId, "preset" to preset.name)).query(UUID::class.java).list()

    private fun requireUnlocked(accountId: UUID) = require(jdbc.sql("select count(*) from stage_progress where account_id=:account and stage_id='stage.01-05' and first_cleared_at is not null")
        .param("account", accountId).query(Long::class.java).single() > 0) { "GEMS_LOCKED" }

    private fun refreshTickets(accountId: UUID) {
        jdbc.sql("insert into gem_account_state(account_id,tickets,last_ticket_at) values (:account,:grant,:now) on conflict(account_id) do nothing")
            .params(mapOf("account" to accountId, "grant" to policy.ticketInitialGrant, "now" to dbTime(clock.instant()))).update()
        val row = jdbc.sql("select tickets,last_ticket_at from gem_account_state where account_id=:account for update").param("account", accountId)
            .query { rs, _ -> rs.getInt("tickets") to rs.getTimestamp("last_ticket_at").toInstant() }.single()
        val now = clock.instant()
        val currentTickets = row.first.coerceAtMost(policy.ticketMaxStock)
        val gained = (policy.ticketSlotIndex(now) - policy.ticketSlotIndex(row.second)).coerceAtLeast(0).toInt()
        val nextTickets = if (currentTickets >= policy.ticketMaxStock) currentTickets else (currentTickets + gained).coerceAtMost(policy.ticketMaxStock)
        if (nextTickets == row.first && gained <= 0) return
        jdbc.sql("update gem_account_state set tickets=:tickets,last_ticket_at=:time where account_id=:account")
            .params(
                mapOf(
                    "tickets" to nextTickets,
                    "time" to dbTime(if (gained > 0) policy.ticketSlotStartedAt(now) else row.second),
                    "account" to accountId,
                ),
            ).update()
    }

    private fun tickets(accountId: UUID): Int = jdbc.sql("select tickets from gem_account_state where account_id=:account")
        .param("account", accountId).query(Int::class.java).optional().orElse(0)

    private fun secondsUntilNextTicket(accountId: UUID): Long = jdbc.sql("select tickets from gem_account_state where account_id=:account")
        .param("account", accountId).query(Int::class.java).optional()
        .map { if (it >= policy.ticketMaxStock) 0L else policy.secondsUntilNextTicketSlot(clock.instant()) }.orElse(0)

    private fun todayBoss(): GemPreset = GemRules.bossForSlot(policy.slotIndex(clock.instant()))

    private fun hasTestAccess(accountId: UUID): Boolean = jdbc.sql("select count(*) from gem_dungeon_test_access where account_id=:account")
        .param("account", accountId).query(Long::class.java).single() > 0

    private fun resolveBoss(requestedBoss: GemPreset?, testAccess: Boolean): GemPreset {
        if (requestedBoss == null) return todayBoss()
        require(requestedBoss != GemPreset.MAIN) { "INVALID_GEM_DUNGEON_BOSS" }
        require(testAccess) { "GEM_DUNGEON_TEST_ACCESS_REQUIRED" }
        return requestedBoss
    }

    private fun dungeonId(boss: GemPreset, stage: Int): String = "gem-dungeon.${boss.name.lowercase()}.$stage"

    private fun dbTime(value: Instant): Timestamp = Timestamp.from(value)

    private fun grantGemBoxes(accountId: UUID, quantity: Long) {
        inventory.grantStackOrReject(accountId, "GEM_BOX", quantity)
    }

    private fun lockAccount(accountId: UUID) {
        jdbc.sql("select id from account where id=:account for update").param("account", accountId).query(UUID::class.java)
            .optional().orElseThrow { IllegalArgumentException("AUTHENTICATION_REQUIRED") }
    }

    private fun incrementStateVersion(accountId: UUID): Long = jdbc.sql("update account set state_version=state_version+1 where id=:account returning state_version")
        .param("account", accountId).query(Long::class.java).single()

    private fun <T> command(accountId: UUID, key: UUID, source: String, type: Class<T>, action: () -> T): T {
        lockAccount(accountId)
        val fingerprint = sha256(source)
        commandRow(accountId, key)?.let { existing ->
            require(existing.fingerprint == fingerprint) { "IDEMPOTENCY_KEY_REUSED" }
            return mapper.readValue(existing.resultJson, type)
        }
        val result = action()
        jdbc.sql("insert into gem_command_record(command_id,account_id,idempotency_key,fingerprint,result_json) values (:command,:account,:key,:fingerprint,cast(:result as jsonb))")
            .params(mapOf("command" to UUID.randomUUID(), "account" to accountId, "key" to key, "fingerprint" to fingerprint, "result" to mapper.writeValueAsString(result))).update()
        return result
    }

    private fun commandRow(accountId: UUID, key: UUID): StoredCommand? = jdbc.sql("select fingerprint,result_json::text from gem_command_record where account_id=:account and idempotency_key=:key")
        .params(mapOf("account" to accountId, "key" to key)).query { row, _ -> StoredCommand(row.getString("fingerprint"), row.getString("result_json")) }.optional().orElse(null)

    private fun sha256(value: String): String = MessageDigest.getInstance("SHA-256").digest(value.toByteArray(StandardCharsets.UTF_8)).joinToString("") { "%02x".format(it) }
}
