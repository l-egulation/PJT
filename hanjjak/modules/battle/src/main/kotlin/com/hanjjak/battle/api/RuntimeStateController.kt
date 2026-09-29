package com.hanjjak.battle.api

import com.fasterxml.jackson.annotation.JsonInclude
import com.hanjjak.battle.application.BattleStatsProvider
import com.hanjjak.battle.application.BattleModeService
import jakarta.servlet.http.HttpSession
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.RequestMapping
import org.springframework.web.bind.annotation.RestController
import java.time.Clock
import java.time.Instant
import java.util.UUID

@RestController
@RequestMapping("/api/v1/runtime-state")
class RuntimeStateController(
    private val jdbc: JdbcClient,
    private val battleModes: BattleModeService,
    private val stats: BattleStatsProvider,
    private val clock: Clock,
    private val balanceVersions: com.hanjjak.account.application.BalanceVersionService,
) {
    @JsonInclude(JsonInclude.Include.ALWAYS)
    data class RuntimeState(
        val accountId: UUID,
        val stateVersion: Long,
        val contentVersion: String,
        val currentStageId: String,
        val idleMode: String,
        val repeatStageId: String?,
        val maxHp: Int,
    )
    data class Envelope<T>(val requestId: UUID, val serverTime: Instant, val stateVersion: Long, val data: T)

    @GetMapping
    fun state(session: HttpSession): Envelope<RuntimeState> {
        val accountId = session.getAttribute("accountId") as? UUID
            ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")
        val stateVersion = jdbc.sql("select state_version from account where id=:account")
            .param("account", accountId)
            .query(Long::class.java)
            .single()
        val currentStageId = jdbc.sql("select current_stage_id from account_runtime_state where account_id=:account")
            .param("account", accountId)
            .query(String::class.java)
            .optional()
            .orElse("stage.01-01")
        val battleMode = battleModes.snapshot(accountId)
        val runtime = RuntimeState(accountId, stateVersion, balanceVersions.current(accountId), currentStageId, battleMode.idleMode, battleMode.repeatStageId, stats.forAccount(accountId).maxHp)
        return Envelope(UUID.randomUUID(), clock.instant(), stateVersion, runtime)
    }

}
