package com.hanjjak.battle.api

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.battle.application.BattleSkillProvider
import com.hanjjak.battle.application.BattleStatsProvider
import com.hanjjak.sim.ArenaFighter
import com.hanjjak.sim.ArenaInput
import com.hanjjak.sim.ArenaResult
import com.hanjjak.sim.ArenaSimulator
import com.hanjjak.sim.ArenaTrace
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RequestBody
import org.springframework.web.bind.annotation.RequestHeader
import org.springframework.web.bind.annotation.RequestMapping
import org.springframework.web.bind.annotation.RestController
import java.util.UUID

data class ArenaOpponent(val accountId: UUID, val nickname: String, val rating: Int, val level: Int)
data class ArenaBattleView(val battleId: UUID, val opponent: ArenaOpponent, val result: ArenaResult, val events: List<com.hanjjak.sim.ArenaEvent>, val hpMultiplier: Int)

open class ArenaService(
    private val mapper: ObjectMapper,
    private val stats: BattleStatsProvider,
    private val skills: BattleSkillProvider,
    private val jdbc: JdbcClient,
) {
    @Transactional(readOnly = true)
    fun opponents(accountId: UUID): List<ArenaOpponent> = jdbc.sql(
        """select a.id,c.nickname,c.level,coalesce(r.rating,1000) rating
           from account a join character c on c.account_id=a.id
           left join arena_rating r on r.account_id=a.id
           where a.id<>:account and a.deleted_at is null
           order by abs(coalesce(r.rating,1000)-coalesce((select rating from arena_rating where account_id=:account),1000)),a.id limit 5""",
    ).param("account", accountId).query { row, _ ->
        ArenaOpponent(row.getObject("id", UUID::class.java), row.getString("nickname"), row.getInt("rating"), row.getInt("level"))
    }.list()
    @Transactional
    fun battle(accountId: UUID, opponentId: UUID, idempotencyKey: UUID): ArenaBattleView {
        require(accountId != opponentId) { "ARENA_SELF_MATCH" }
        lockAccounts(accountId, opponentId)
        val existing = jdbc.sql("select battle_id,defender_account_id,result_json::text,events_json::text from arena_battle where attacker_account_id=:account and idempotency_key=:key")
            .params(mapOf("account" to accountId, "key" to idempotencyKey)).query { row, _ ->
                ExistingBattle(row.getObject("battle_id", UUID::class.java), row.getObject("defender_account_id", UUID::class.java), row.getString("result_json"), row.getString("events_json"))
            }.optional().orElse(null)
        if (existing != null) {
            require(existing.defenderId == opponentId) { "IDEMPOTENCY_KEY_REUSED" }
            return view(existing.battleId, accountId, existing.defenderId, existing.resultJson, existing.eventsJson)
        }
        val opponent = opponent(accountId, opponentId)
        val attackerStats = stats.forAccount(accountId)
        val defenderStats = stats.forAccount(opponentId)
        val attacker = ArenaFighter(accountId.toString(), attackerStats, skills.forAccount(accountId))
        val defender = ArenaFighter(opponentId.toString(), defenderStats, skills.forAccount(opponentId))
        val input = ArenaInput("arena-v1", idempotencyKey.mostSignificantBits xor idempotencyKey.leastSignificantBits, attacker, defender)
        val trace = ArenaSimulator.simulateWithEvents(input)
        val battleId = UUID.randomUUID()
        jdbc.sql("insert into arena_battle(battle_id,idempotency_key,attacker_account_id,defender_account_id,status,content_version,seed,input_json,result_json,events_json) values (:id,:key,:account,:opponent,'COMPLETED',:version,:seed,cast(:input as jsonb),cast(:result as jsonb),cast(:events as jsonb))")
            .params(mapOf("id" to battleId, "key" to idempotencyKey, "account" to accountId, "opponent" to opponentId, "version" to input.contentVersion, "seed" to input.seed, "input" to mapper.writeValueAsString(input), "result" to mapper.writeValueAsString(trace.result), "events" to mapper.writeValueAsString(trace.events))).update()
        val winner = trace.result.winnerId == accountId.toString()
        jdbc.sql("insert into arena_rating(account_id,rating,wins,losses) values (:account,case when :winner then 1025 else 975 end,case when :winner then 1 else 0 end,case when :winner then 0 else 1 end) on conflict(account_id) do update set rating=greatest(0,arena_rating.rating+case when :winner then 25 else -25 end),wins=arena_rating.wins+case when :winner then 1 else 0 end,losses=arena_rating.losses+case when :winner then 0 else 1 end,updated_at=now())")
            .params(mapOf("account" to accountId, "winner" to winner)).update()
        return ArenaBattleView(battleId, opponent, trace.result, trace.events, input.hpMultiplier)
    }

    private fun lockAccounts(accountId: UUID, opponentId: UUID) {
        jdbc.sql("select id from account where id in (:account,:opponent) and deleted_at is null order by id for update")
            .params(mapOf("account" to accountId, "opponent" to opponentId)).query(UUID::class.java).list()
            .also { require(it.size == 2) { "ARENA_OPPONENT_NOT_FOUND" } }
    }

    private fun opponent(accountId: UUID, opponentId: UUID): ArenaOpponent = jdbc.sql("select a.id,c.nickname,c.level,coalesce(r.rating,1000) rating from account a join character c on c.account_id=a.id left join arena_rating r on r.account_id=a.id where a.id=:opponent and a.id<>:account and a.deleted_at is null")
        .params(mapOf("account" to accountId, "opponent" to opponentId)).query { row, _ -> ArenaOpponent(row.getObject("id", UUID::class.java), row.getString("nickname"), row.getInt("rating"), row.getInt("level")) }.optional().orElseThrow { IllegalArgumentException("ARENA_OPPONENT_NOT_FOUND") }

    fun stateVersion(accountId: UUID): Long = jdbc.sql("select state_version from account where id=:account")
        .param("account", accountId).query(Long::class.java).single()

    private fun view(battleId: UUID, accountId: UUID, opponentId: UUID, resultJson: String, eventsJson: String): ArenaBattleView = ArenaBattleView(battleId, opponent(accountId, opponentId), mapper.readValue(resultJson, ArenaResult::class.java), mapper.readValue(eventsJson, mapper.typeFactory.constructCollectionType(List::class.java, com.hanjjak.sim.ArenaEvent::class.java)), 30)
    private data class ExistingBattle(val battleId: UUID, val defenderId: UUID, val resultJson: String, val eventsJson: String)
}


@RestController
@RequestMapping("/api/v1/arena")
class ArenaController(private val arena: ArenaService, private val clock: java.time.Clock) {
    data class Envelope<T>(val requestId: UUID, val serverTime: java.time.Instant, val stateVersion: Long, val data: T)
    data class BattleRequest(val opponentId: UUID)

    @GetMapping("/opponents")
    fun opponents(session: jakarta.servlet.http.HttpSession): Envelope<List<ArenaOpponent>> {
        val accountId = account(session)
        return envelope(accountId, arena.opponents(accountId))
    }

    @PostMapping("/battles")
    fun battle(@RequestHeader("Idempotency-Key") key: UUID, @RequestBody request: BattleRequest, session: jakarta.servlet.http.HttpSession): Envelope<ArenaBattleView> {
        val accountId = account(session)
        return envelope(accountId, arena.battle(accountId, request.opponentId, key))
    }

    private fun account(session: jakarta.servlet.http.HttpSession): UUID = session.getAttribute("accountId") as? UUID
        ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")
    private fun <T> envelope(accountId: UUID, data: T) = Envelope(UUID.randomUUID(), clock.instant(), arena.stateVersion(accountId), data)
}
