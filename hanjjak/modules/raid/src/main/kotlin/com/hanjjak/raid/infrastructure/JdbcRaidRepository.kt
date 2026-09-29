package com.hanjjak.raid.infrastructure

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.raid.application.RaidRepository
import com.hanjjak.raid.domain.RaidAccountState
import com.hanjjak.raid.domain.RaidAttempt
import com.hanjjak.raid.domain.RaidAttemptId
import com.hanjjak.raid.domain.RaidAttemptMode
import com.hanjjak.raid.domain.RaidAttemptResult
import com.hanjjak.raid.domain.RaidAttemptStatus
import com.hanjjak.raid.domain.RaidClaim
import com.hanjjak.raid.domain.RaidClaimId
import com.hanjjak.raid.domain.RaidClaimKind
import com.hanjjak.raid.domain.RaidClaimStatus
import com.hanjjak.raid.domain.RaidCommandRecord
import com.hanjjak.raid.domain.RaidCommandStatus
import com.hanjjak.raid.domain.RaidConfirmedResult
import com.hanjjak.raid.domain.RaidContribution
import com.hanjjak.raid.domain.RaidFinalRank
import com.hanjjak.raid.domain.RaidGrade
import com.hanjjak.raid.domain.RaidRewardBundle
import com.hanjjak.raid.domain.RaidRewardSlot
import com.hanjjak.raid.domain.RaidSlotStatus
import com.hanjjak.raid.domain.RaidSession
import com.hanjjak.raid.domain.RaidSessionId
import com.hanjjak.raid.domain.RaidSessionStatus
import com.hanjjak.raid.domain.RaidSettlementAccount
import com.hanjjak.raid.domain.RaidSettlementAccountStatus
import com.hanjjak.raid.domain.RaidSettlementPhase
import org.springframework.jdbc.core.simple.JdbcClient
import java.sql.ResultSet
import java.sql.Timestamp
import java.time.Instant
import java.util.UUID

/** PostgreSQL adapter. Callers acquire locks in account-first order. */
class JdbcRaidRepository(
    private val jdbc: JdbcClient,
    private val mapper: ObjectMapper = ObjectMapper(),
) : RaidRepository {
    override fun openSession(now: Instant): RaidSession? = jdbc.sql(
        "select * from raid_session where status='OPEN' and settles_at>:now order by settles_at,id limit 1",
    ).param("now", Timestamp.from(now)).query { rs, _ -> session(rs) }.optional().orElse(null)

    override fun dueOpenSessions(asOf: Instant, limit: Int): List<RaidSession> {
        require(limit in 1..1_000) { "INVALID_LIMIT" }
        return jdbc.sql("select * from raid_session where status='OPEN' and settles_at<=:asOf order by settles_at,id limit :limit for update")
            .params(mapOf("asOf" to Timestamp.from(asOf), "limit" to limit)).query { rs, _ -> session(rs) }.list()
    }

    override fun lockSession(sessionId: RaidSessionId): RaidSession = jdbc.sql(
        "select * from raid_session where id=:id for update",
    ).param("id", sessionId.value).query { rs, _ -> session(rs) }.optional()
        .orElseThrow { IllegalArgumentException("RAID_SESSION_NOT_FOUND") }

    override fun session(sessionId: RaidSessionId): RaidSession? = jdbc.sql(
        "select * from raid_session where id=:id",
    ).param("id", sessionId.value).query { rs, _ -> session(rs) }.optional().orElse(null)

    override fun lockAccount(accountId: UUID) {
        val found = jdbc.sql("select id from account where id=:account for update")
            .param("account", accountId).query(UUID::class.java).optional().isPresent
        if (!found) throw IllegalArgumentException("ACCOUNT_NOT_FOUND")
    }

    override fun accountState(sessionId: RaidSessionId, accountId: UUID): RaidAccountState? = jdbc.sql(
        "select * from raid_account_state where session_id=:session and account_id=:account",
    ).params(mapOf("session" to sessionId.value, "account" to accountId)).query { rs, _ -> accountState(rs) }
        .optional().orElse(null)

    override fun lockAccountState(sessionId: RaidSessionId, accountId: UUID): RaidAccountState = jdbc.sql(
        "select * from raid_account_state where session_id=:session and account_id=:account for update",
    ).params(mapOf("session" to sessionId.value, "account" to accountId)).query { rs, _ -> accountState(rs) }
        .optional().orElseThrow { IllegalArgumentException("RAID_ACCOUNT_STATE_NOT_FOUND") }

    override fun lockSlot(sessionId: RaidSessionId, accountId: UUID, ordinal: Int): RaidRewardSlot = jdbc.sql(
        "select * from raid_reward_slot where session_id=:session and account_id=:account and ordinal=:ordinal for update",
    ).params(mapOf("session" to sessionId.value, "account" to accountId, "ordinal" to ordinal)).query { rs, _ -> slot(rs) }
        .optional().orElseThrow { IllegalArgumentException("RAID_SLOT_NOT_FOUND") }

    override fun lockAttempt(attemptId: RaidAttemptId, sessionId: RaidSessionId, accountId: UUID): RaidAttempt = jdbc.sql(
        "select * from raid_attempt where id=:id and session_id=:session and account_id=:account for update",
    ).params(mapOf("id" to attemptId.value, "session" to sessionId.value, "account" to accountId))
        .query { rs, _ -> attempt(rs) }.optional().orElseThrow { IllegalArgumentException("RAID_ATTEMPT_NOT_FOUND") }

    override fun lockClaim(claimId: RaidClaimId, accountId: UUID): RaidClaim = jdbc.sql(
        "select * from raid_reward_claim where id=:id and account_id=:account for update",
    ).params(mapOf("id" to claimId.value, "account" to accountId)).query { rs, _ -> claim(rs) }
        .optional().orElseThrow { IllegalArgumentException("RAID_CLAIM_NOT_FOUND") }

    override fun slots(sessionId: RaidSessionId, accountId: UUID): List<RaidRewardSlot> = jdbc.sql(
        "select * from raid_reward_slot where session_id=:session and account_id=:account order by ordinal",
    ).params(mapOf("session" to sessionId.value, "account" to accountId)).query { rs, _ -> slot(rs) }.list()

    override fun attemptResult(attemptId: RaidAttemptId): RaidAttemptResult? = jdbc.sql(
        "select result_json::text from raid_attempt where id=:id",
    ).param("id", attemptId.value).query { rs, _ -> rs.getString("result_json") }.optional().orElse(null)?.let(::parseAttemptResult)

    override fun currentAttempt(accountId: UUID): RaidAttempt? = jdbc.sql(
        "select * from raid_attempt where account_id=:account and status in ('RUNNING','RESULT_HELD') order by started_at desc,id desc limit 1",
    ).param("account", accountId).query { rs, _ -> attempt(rs) }.optional().orElse(null)

    override fun dueAttempts(asOf: Instant, after: UUID?, limit: Int): List<RaidAttempt> {
        require(limit in 1..1_000) { "INVALID_LIMIT" }
        val sql = if (after == null) {
            "select * from raid_attempt where (status='RUNNING' and completable_at<=:asOf) or resume_pending order by id limit :limit"
        } else {
            "select * from raid_attempt where ((status='RUNNING' and completable_at<=:asOf) or resume_pending) and id>:after order by id limit :limit"
        }
        val spec = jdbc.sql(sql).params(mapOf("asOf" to Timestamp.from(asOf), "limit" to limit))
        return (if (after == null) spec else spec.param("after", after)).query { rs, _ -> attempt(rs) }.list()
    }

    override fun settlementAccounts(sessionId: RaidSessionId, after: UUID?, limit: Int): List<UUID> {
        require(limit in 1..1_000) { "INVALID_LIMIT" }
        val sql = if (after == null) {
            "select account_id from raid_settlement_account where session_id=:session and status='PENDING' order by account_id limit :limit"
        } else {
            "select account_id from raid_settlement_account where session_id=:session and status='PENDING' and account_id>:after order by account_id limit :limit"
        }
        val spec = jdbc.sql(sql).params(mapOf("session" to sessionId.value, "limit" to limit))
        return (if (after == null) spec else spec.param("after", after)).query(UUID::class.java).list()
    }

    override fun settlementAccountCount(sessionId: RaidSessionId): Long = jdbc.sql(
        "select count(*) from raid_settlement_account where session_id=:session",
    ).param("session", sessionId.value).query(Long::class.java).single()

    override fun finalizedSettlementAccountCount(sessionId: RaidSessionId): Long = jdbc.sql(
        "select count(*) from raid_settlement_account where session_id=:session and status='FINALIZED'",
    ).param("session", sessionId.value).query(Long::class.java).single()

    override fun settlementAttempts(sessionId: RaidSessionId, accountId: UUID): List<RaidAttempt> = jdbc.sql(
        "select * from raid_attempt where session_id=:session and account_id=:account order by started_at,id",
    ).params(mapOf("session" to sessionId.value, "account" to accountId))
        .query { rs, _ -> attempt(rs) }.list()



    override fun contributions(sessionId: RaidSessionId): List<RaidContribution> = jdbc.sql(
        "select * from raid_contribution where session_id=:session and confirmed_attempts>0 order by session_id,account_id",
    ).param("session", sessionId.value).query { rs, _ -> contribution(rs) }.list()

    override fun finalRanks(sessionId: RaidSessionId): List<RaidFinalRank> = jdbc.sql(
        "select * from raid_final_rank where session_id=:session order by competitive_rank,account_id",
    ).param("session", sessionId.value).query { rs, _ -> finalRank(rs) }.list()

    override fun rankCount(sessionId: RaidSessionId): Long = jdbc.sql(
        "select count(*) from raid_final_rank where session_id=:session",
    ).param("session", sessionId.value).query(Long::class.java).single()

    override fun dailyRankClaimCount(sessionId: RaidSessionId): Long = jdbc.sql(
        "select count(*) from raid_reward_claim where session_id=:session and source_kind='DAILY_RANK'",
    ).param("session", sessionId.value).query(Long::class.java).single()

    override fun findSessionBySettlesAt(settlesAt: Instant): RaidSession? = jdbc.sql(
        "select * from raid_session where settles_at=:settlesAt",
    ).param("settlesAt", Timestamp.from(settlesAt)).query { rs, _ -> session(rs) }.optional().orElse(null)

    override fun settlingSessions(asOf: Instant): List<RaidSession> = jdbc.sql(
        "select * from raid_session where status='SETTLING' and settles_at<=:asOf order by settles_at,id",
    ).param("asOf", Timestamp.from(asOf)).query { rs, _ -> session(rs) }.list()

    override fun freezeSettlementWorkset(sessionId: RaidSessionId, cutoffAt: Instant): Int = jdbc.sql(
        """
        insert into raid_settlement_account(session_id,account_id,status,cutoff_at)
        select :session, participant.account_id, 'PENDING', :cutoff
        from (
          select account_id from raid_account_state where session_id=:session
          union select account_id from raid_reward_slot where session_id=:session
          union select account_id from raid_attempt where session_id=:session
          union select account_id from raid_confirmed_result where session_id=:session
          union select account_id from raid_reward_claim where session_id=:session
        ) participant
        on conflict (session_id,account_id) do nothing
        """.trimIndent(),
    ).params(mapOf("session" to sessionId.value, "cutoff" to Timestamp.from(cutoffAt))).update()

    override fun lockSettlementAccount(sessionId: RaidSessionId, accountId: UUID): RaidSettlementAccount = jdbc.sql(
        "select * from raid_settlement_account where session_id=:session and account_id=:account for update",
    ).params(mapOf("session" to sessionId.value, "account" to accountId)).query { rs, _ -> settlementAccount(rs) }
        .optional().orElseThrow { IllegalArgumentException("RAID_SETTLEMENT_ACCOUNT_NOT_FOUND") }

    override fun finalizeSettlementAccount(sessionId: RaidSessionId, accountId: UUID, finalizedAt: Instant): Boolean = jdbc.sql(
        "update raid_settlement_account set status='FINALIZED',finalized_at=:finalizedAt where session_id=:session and account_id=:account and status='PENDING'",
    ).params(mapOf("session" to sessionId.value, "account" to accountId, "finalizedAt" to Timestamp.from(finalizedAt))).update() == 1

    override fun saveSession(session: RaidSession) {
        val inserted = jdbc.sql(
            """
            insert into raid_session(id,content_version,reward_version,settles_at,status,cutoff_at,settlement_cursor,settlement_account_count,finalized_account_count,settlement_phase,completed_at)
            values (:id,:content,:reward,:settlesAt,:status,:cutoffAt,:cursor,:accountCount,:finalized,:phase,:completedAt)
            on conflict (id) do nothing
            """.trimIndent(),
        ).params(sessionParams(session)).update()
        if (inserted == 1) return
        val updated = jdbc.sql(
            """
            update raid_session set status=:status, cutoff_at=coalesce(cutoff_at,:cutoffAt), settlement_cursor=:cursor,
              settlement_account_count=:accountCount, finalized_account_count=:finalized, settlement_phase=:phase, completed_at=:completedAt, updated_at=now()
            where id=:id and (status=:status or (status='OPEN' and :status='SETTLING') or (status='SETTLING' and :status in ('SETTLED_SUCCESS','SETTLED_FAILURE')))
              and content_version=:content and reward_version=:reward and settles_at=:settlesAt
              and (cutoff_at is null or cutoff_at=:cutoffAt)
              and settlement_account_count<=:accountCount and finalized_account_count<=:finalized
              and (case settlement_phase
                    when 'ROTATE_SESSION' then 0 when 'FREEZE_WORKSET' then 1 when 'FINALIZE_ACCOUNTS' then 2
                    when 'MATERIALIZE_RANKS' then 3 when 'SETTLED' then 4 end)
                  <= (case :phase
                    when 'ROTATE_SESSION' then 0 when 'FREEZE_WORKSET' then 1 when 'FINALIZE_ACCOUNTS' then 2
                    when 'MATERIALIZE_RANKS' then 3 when 'SETTLED' then 4 end)
            """.trimIndent(),
        ).params(sessionParams(session)).update()
        if (updated != 1) throw IllegalArgumentException("RAID_SESSION_STATE_CONFLICT")
    }

    override fun saveAccountState(state: RaidAccountState) {
        val updated = jdbc.sql(
            """
            insert into raid_account_state(session_id,account_id,current_slot_ordinal,slots_terminal,reward_attempts_started,state_version,auto_finalized)
            values (:session,:account,:currentSlot,:terminal,:attempts,:version,:autoFinalized)
            on conflict (session_id,account_id) do update set current_slot_ordinal=:currentSlot,slots_terminal=:terminal,
              reward_attempts_started=:attempts,state_version=:version,auto_finalized=:autoFinalized,updated_at=now()
              where raid_account_state.state_version + 1 = :version
                and raid_account_state.current_slot_ordinal <= :currentSlot
                and raid_account_state.slots_terminal <= :terminal
                and raid_account_state.reward_attempts_started <= :attempts
                and (not raid_account_state.auto_finalized or :autoFinalized)
            """.trimIndent(),
        ).params(stateParams(state)).update()
        if (updated != 1) throw IllegalArgumentException("RAID_ACCOUNT_STATE_CONFLICT")
    }

    override fun saveSlot(slot: RaidRewardSlot) {
        val updated = jdbc.sql(
            """
            insert into raid_reward_slot(id,session_id,account_id,ordinal,status,attempts_started,current_attempt_id,terminal_at)
            values (:id,:session,:account,:ordinal,:status,:attempts,:attempt,:terminalAt)
            on conflict (id) do update set status=:status,attempts_started=:attempts,current_attempt_id=:attempt,terminal_at=:terminalAt,updated_at=now()
            where raid_reward_slot.session_id=:session
              and raid_reward_slot.account_id=:account
              and raid_reward_slot.ordinal=:ordinal
              and raid_reward_slot.attempts_started<=:attempts
              and (
                raid_reward_slot.status=:status
                or (raid_reward_slot.status='AVAILABLE' and :status in ('ACTIVE','EXPIRED'))
                or (raid_reward_slot.status='ACTIVE' and :status in ('RESULT_HELD','CONFIRMED','DISCARDED','EXPIRED'))
                or (raid_reward_slot.status='RESULT_HELD' and :status in ('ACTIVE','CONFIRMED','DISCARDED','EXPIRED'))
              )
            """.trimIndent(),
        ).params(mapOf("id" to slot.slotId, "session" to slot.sessionId.value, "account" to slot.accountId,
            "ordinal" to slot.ordinal, "status" to slot.status.name, "attempts" to slot.attemptsStarted,
            "attempt" to slot.currentAttemptId?.value, "terminalAt" to slot.terminalAt?.let(Timestamp::from))).update()
        if (updated != 1) throw IllegalArgumentException("RAID_SLOT_STATE_CONFLICT")
    }


    override fun saveAttempt(attempt: RaidAttempt) {
        val inserted = jdbc.sql(
            """
            insert into raid_attempt(id,session_id,account_id,mode,slot_id,attempt_ordinal,status,input_snapshot,result_json,timeline_json,seed,started_at,completable_at,ended_at,resume_pending)
            values (:id,:session,:account,:mode,:slot,:ordinal,:status,cast(:snapshot as jsonb),cast(:result as jsonb),cast(:timeline as jsonb),:seed,:started,:completable,:ended,:resumePending)
            on conflict (id) do nothing
            """.trimIndent(),
        ).params(attemptParams(attempt)).update()
        if (inserted == 1) return
        val updated = jdbc.sql(
            """
            update raid_attempt set status=:status,ended_at=:ended,resume_pending=:resumePending
            where id=:id and session_id=:session and account_id=:account
              and (status=:status
                or (status='RUNNING' and :status in ('RESULT_HELD','DISCARDED','CONFIRMED'))
                or (status='RESULT_HELD' and :status in ('DISCARDED','CONFIRMED')))
              and (ended_at is null or ended_at=:ended)
            """.trimIndent(),
        ).params(attemptParams(attempt)).update()
        if (updated != 1) throw IllegalArgumentException("RAID_ATTEMPT_STATE_CONFLICT")
    }

    override fun clearResumePending(attemptId: RaidAttemptId, sessionId: RaidSessionId, accountId: UUID): Boolean = jdbc.sql(
        "update raid_attempt set resume_pending=false where id=:id and session_id=:session and account_id=:account and resume_pending",
    ).params(mapOf("id" to attemptId.value, "session" to sessionId.value, "account" to accountId)).update() == 1

    override fun appendConfirmedResult(result: RaidConfirmedResult) {
        jdbc.sql(
            """
            insert into raid_confirmed_result(id,attempt_id,session_id,account_id,damage,grade,seal_contribution,reward_version,
              reward_tickets,reward_gem_boxes,reward_rice,result_json,confirmed_at)
            values (:id,:attempt,:session,:account,:damage,:grade,:contribution,:rewardVersion,:tickets,:gems,:rice,cast(:result as jsonb),:confirmedAt)
            """.trimIndent(),
        ).params(confirmedParams(result)).update()
    }
    override fun lockContribution(sessionId: RaidSessionId, accountId: UUID): RaidContribution {
        jdbc.sql("insert into raid_contribution(session_id,account_id) values (:session,:account) on conflict (session_id,account_id) do nothing")
            .params(mapOf("session" to sessionId.value, "account" to accountId)).update()
        return jdbc.sql("select * from raid_contribution where session_id=:session and account_id=:account for update")
            .params(mapOf("session" to sessionId.value, "account" to accountId)).query { rs, _ -> contribution(rs) }.single()
    }

    /** Applies a confirmation delta atomically; SQL arithmetic rejects Long overflow. */
    override fun upsertContribution(contribution: RaidContribution) {
        jdbc.sql(
            """
            insert into raid_contribution(session_id,account_id,seal_contribution,total_damage,confirmed_attempts,highest_damage)
            values (:session,:account,:contribution,:damage,:attempts,:highest)
            on conflict (session_id,account_id) do update set
              seal_contribution=raid_contribution.seal_contribution+excluded.seal_contribution,
              total_damage=raid_contribution.total_damage+excluded.total_damage,
              confirmed_attempts=raid_contribution.confirmed_attempts+excluded.confirmed_attempts,
              highest_damage=greatest(raid_contribution.highest_damage,excluded.highest_damage),updated_at=now()
            where raid_contribution.seal_contribution <= 9223372036854775807-excluded.seal_contribution
              and raid_contribution.total_damage <= 9223372036854775807-excluded.total_damage
              and raid_contribution.confirmed_attempts <= 2147483647-excluded.confirmed_attempts
            """.trimIndent(),
        ).params(mapOf("session" to contribution.sessionId.value, "account" to contribution.accountId,
            "contribution" to contribution.sealContribution, "damage" to contribution.damage,
            "attempts" to contribution.confirmedAttempts, "highest" to contribution.highestDamage)).update().also {
            require(it == 1) { "RAID_CONTRIBUTION_OVERFLOW" }
        }
    }

    override fun saveFinalRank(rank: RaidFinalRank) {
        jdbc.sql(
            """
            insert into raid_final_rank(session_id,account_id,competitive_rank,seal_contribution,total_damage,highest_damage)
            values (:session,:account,:rank,:contribution,:damage,:highest)
            on conflict (session_id,account_id) do nothing
            """.trimIndent(),
        ).params(mapOf("session" to rank.sessionId.value, "account" to rank.accountId, "rank" to rank.competitiveRank,
            "contribution" to rank.sealContribution, "damage" to rank.totalDamage, "highest" to rank.highestDamage)).update()
    }

    override fun insertClaim(claim: RaidClaim) {
        jdbc.sql(
            """
            insert into raid_reward_claim(id,session_id,account_id,source_kind,source_id,status,reward_version,reward_tickets,reward_gem_boxes,reward_rice,claimable_at,claimed_at)
            values (:id,:session,:account,:kind,:source,:status,:rewardVersion,:tickets,:gems,:rice,:claimableAt,:claimedAt)
            on conflict (source_kind,source_id,account_id) do nothing
            """.trimIndent(),
        ).params(claimParams(claim)).update()
    }

    override fun claims(accountId: UUID): List<RaidClaim> = jdbc.sql(
        "select * from raid_reward_claim where account_id=:account order by claimable_at,id",
    ).param("account", accountId).query { rs, _ -> claim(rs) }.list()

    override fun claimCommand(accountId: UUID, idempotencyKey: UUID): RaidCommandRecord? = jdbc.sql(
        "select command_id,account_id,idempotency_key,fingerprint,result_json::text,status,created_at,expires_at from raid_command_record where account_id=:account and idempotency_key=:key",
    ).params(mapOf("account" to accountId, "key" to idempotencyKey)).query { rs, _ -> command(rs) }.optional().orElse(null)

    override fun recordCommand(accountId: UUID, idempotencyKey: UUID, fingerprint: String, resultJson: String?, status: RaidCommandStatus, expiresAt: Instant?): Boolean = jdbc.sql(
        """
        insert into raid_command_record(command_id,account_id,idempotency_key,fingerprint,result_json,status,expires_at)
        values (:id,:account,:key,:fingerprint,cast(:result as jsonb),:status,:expiresAt)
        on conflict (account_id,idempotency_key) do nothing
        """.trimIndent(),
    ).params(mapOf("id" to UUID.randomUUID(), "account" to accountId, "key" to idempotencyKey, "fingerprint" to fingerprint,
        "result" to resultJson, "status" to status.name, "expiresAt" to expiresAt?.let(Timestamp::from))).update() == 1

    override fun markClaimed(claimId: RaidClaimId, accountId: UUID, claimedAt: Instant): Boolean = jdbc.sql(
        "update raid_reward_claim set status='CLAIMED',claimed_at=:claimedAt where id=:id and account_id=:account and status='CLAIMABLE'",
    ).params(mapOf("id" to claimId.value, "account" to accountId, "claimedAt" to Timestamp.from(claimedAt))).update() == 1
    override fun claim(claimId: RaidClaimId, accountId: UUID): RaidClaim? = jdbc.sql(
        "select * from raid_reward_claim where id=:id and account_id=:account",
    ).params(mapOf("id" to claimId.value, "account" to accountId)).query { rs, _ -> claim(rs) }.optional().orElse(null)

    override fun incrementAccountStateVersion(accountId: UUID): Long = jdbc.sql(
        "update account set state_version=state_version+1 where id=:account returning state_version",
    ).param("account", accountId).query(Long::class.java).single()


    private fun session(rs: ResultSet) = RaidSession(RaidSessionId(rs.getObject("id", UUID::class.java)), rs.getString("content_version"), rs.getString("reward_version"),
        rs.instant("settles_at"), RaidSessionStatus.valueOf(rs.getString("status")), rs.instantOrNull("cutoff_at"), rs.uuidOrNull("settlement_cursor"),
        rs.getLong("settlement_account_count"), rs.getLong("finalized_account_count"),
        rs.getString("settlement_phase")?.let(RaidSettlementPhase::valueOf) ?: RaidSettlementPhase.ROTATE_SESSION,
        rs.instantOrNull("completed_at"))
    private fun confirmedResult(rs: ResultSet) = RaidConfirmedResult(
        rs.getObject("id", UUID::class.java), RaidAttemptId(rs.getObject("attempt_id", UUID::class.java)),
        RaidSessionId(rs.getObject("session_id", UUID::class.java)), rs.getObject("account_id", UUID::class.java),
        rs.getLong("damage"), RaidGrade.valueOf(rs.getString("grade")), rs.getLong("seal_contribution"),
        rs.getString("reward_version"), rs.instant("confirmed_at"),
        RaidRewardBundle(rs.getLong("reward_tickets"), rs.getLong("reward_gem_boxes"), rs.getLong("reward_rice")),
        rs.getString("result_json"),
    )
    private fun finalRank(rs: ResultSet) = RaidFinalRank(
        RaidSessionId(rs.getObject("session_id", UUID::class.java)), rs.getObject("account_id", UUID::class.java),
        rs.getInt("competitive_rank"), rs.getLong("seal_contribution"), rs.getLong("total_damage"), rs.getLong("highest_damage"),
    )
    private fun accountState(rs: ResultSet) = RaidAccountState(RaidSessionId(rs.getObject("session_id", UUID::class.java)), rs.getObject("account_id", UUID::class.java),
        rs.getInt("current_slot_ordinal"), rs.getInt("slots_terminal"), rs.getInt("reward_attempts_started"), rs.getLong("state_version"), rs.getBoolean("auto_finalized"))
    private fun slot(rs: ResultSet) = RaidRewardSlot(RaidSessionId(rs.getObject("session_id", UUID::class.java)), rs.getObject("account_id", UUID::class.java), rs.getInt("ordinal"),
        RaidSlotStatus.valueOf(rs.getString("status")), rs.getInt("attempts_started"), rs.uuidOrNull("current_attempt_id")?.let(::RaidAttemptId), rs.instantOrNull("terminal_at"), rs.getObject("id", UUID::class.java))
    private fun attempt(rs: ResultSet) = RaidAttempt(RaidAttemptId(rs.getObject("id", UUID::class.java)), RaidSessionId(rs.getObject("session_id", UUID::class.java)), rs.getObject("account_id", UUID::class.java),
        RaidAttemptMode.valueOf(rs.getString("mode")), rs.uuidOrNull("slot_id"), rs.getInt("attempt_ordinal"), RaidAttemptStatus.valueOf(rs.getString("status")),
        rs.getString("input_snapshot"), rs.getString("result_json"), rs.getString("timeline_json"), rs.instant("started_at"), rs.instant("completable_at"), rs.instantOrNull("ended_at"), rs.getLong("seed"), rs.getBoolean("resume_pending"))
    private fun claim(rs: ResultSet) = RaidClaim(RaidClaimId(rs.getObject("id", UUID::class.java)), RaidSessionId(rs.getObject("session_id", UUID::class.java)), rs.getObject("account_id", UUID::class.java),
        RaidClaimKind.valueOf(rs.getString("source_kind")), rs.getObject("source_id", UUID::class.java), RaidClaimStatus.valueOf(rs.getString("status")), rs.getString("reward_version"),
        RaidRewardBundle(rs.getLong("reward_tickets"), rs.getLong("reward_gem_boxes"), rs.getLong("reward_rice")), rs.instant("claimable_at"), rs.instantOrNull("claimed_at"))
    private fun contribution(rs: ResultSet) = RaidContribution(RaidSessionId(rs.getObject("session_id", UUID::class.java)), rs.getObject("account_id", UUID::class.java), rs.getLong("seal_contribution"), rs.getLong("total_damage"), rs.getInt("confirmed_attempts"), rs.getLong("highest_damage"))
    private fun settlementAccount(rs: ResultSet) = RaidSettlementAccount(RaidSessionId(rs.getObject("session_id", UUID::class.java)), rs.getObject("account_id", UUID::class.java), RaidSettlementAccountStatus.valueOf(rs.getString("status")), rs.instant("created_at"), rs.instant("cutoff_at"), rs.instantOrNull("finalized_at"))
    private fun command(rs: ResultSet) = RaidCommandRecord(rs.getObject("command_id", UUID::class.java), rs.getObject("account_id", UUID::class.java), rs.getObject("idempotency_key", UUID::class.java), rs.getString("fingerprint"), rs.getString("result_json"), RaidCommandStatus.valueOf(rs.getString("status")), rs.instant("created_at"), rs.instantOrNull("expires_at"))
    private fun parseAttemptResult(json: String): RaidAttemptResult {
        val node = mapper.readTree(json)
        return RaidAttemptResult(
            node["damage"].asLong(),
            RaidGrade.valueOf(node["grade"].asText()),
            node["sealContribution"].asInt(),
            Instant.parse(node["endedAt"].asText()),
        )
    }
    private fun stateParams(state: RaidAccountState) = mapOf<String, Any?>("session" to state.sessionId.value, "account" to state.accountId, "currentSlot" to state.currentSlotOrdinal, "terminal" to state.slotsTerminal, "attempts" to state.rewardAttemptsStarted, "version" to state.stateVersion, "autoFinalized" to state.autoFinalized)
    private fun attemptParams(attempt: RaidAttempt) = mapOf<String, Any?>("id" to attempt.attemptId.value, "session" to attempt.sessionId.value, "account" to attempt.accountId, "mode" to attempt.mode.name, "slot" to attempt.slotId, "ordinal" to attempt.attemptOrdinal, "status" to attempt.status.name, "snapshot" to attempt.inputSnapshot, "result" to attempt.result, "timeline" to attempt.timeline, "seed" to attempt.seed, "started" to Timestamp.from(attempt.startedAt), "completable" to Timestamp.from(attempt.completableAt), "ended" to attempt.endedAt?.let(Timestamp::from), "resumePending" to attempt.resumePending)
    private fun confirmedParams(result: RaidConfirmedResult) = mapOf<String, Any?>("id" to result.resultId, "attempt" to result.attemptId.value, "session" to result.sessionId.value, "account" to result.accountId, "damage" to result.damage, "grade" to result.grade.name, "contribution" to result.sealContribution, "rewardVersion" to result.rewardVersion, "tickets" to result.rewardBundle.cosmeticTickets, "gems" to result.rewardBundle.gemBoxes, "rice" to result.rewardBundle.rice, "result" to result.resultJson, "confirmedAt" to Timestamp.from(result.confirmedAt))
    private fun claimParams(claim: RaidClaim) = mapOf<String, Any?>("id" to claim.claimId.value, "session" to claim.sessionId.value, "account" to claim.accountId, "kind" to claim.kind.name, "source" to claim.sourceId, "status" to claim.status.name, "rewardVersion" to claim.rewardVersion, "tickets" to claim.bundle.cosmeticTickets, "gems" to claim.bundle.gemBoxes, "rice" to claim.bundle.rice, "claimableAt" to Timestamp.from(claim.claimableAt), "claimedAt" to claim.claimedAt?.let(Timestamp::from))
    private fun sessionParams(session: RaidSession) = mapOf<String, Any?>("id" to session.sessionId.value, "content" to session.contentVersion, "reward" to session.rewardVersion, "settlesAt" to Timestamp.from(session.settlesAt), "status" to session.status.name, "cutoffAt" to session.cutoffAt?.let(Timestamp::from), "cursor" to session.batchCursor, "accountCount" to session.settlementAccountCount, "finalized" to session.finalizedAccountCount, "phase" to session.settlementPhase.name, "completedAt" to session.completedAt?.let(Timestamp::from))
    private fun ResultSet.instant(column: String): Instant = getTimestamp(column).toInstant()
    private fun ResultSet.instantOrNull(column: String): Instant? = getTimestamp(column)?.toInstant()
    private fun ResultSet.uuidOrNull(column: String): UUID? = getObject(column, UUID::class.java)
}
