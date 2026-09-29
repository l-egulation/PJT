package com.hanjjak.battle.api

import com.hanjjak.account.application.MaterialPreferenceService
import com.hanjjak.stage.application.StageCatalog
import jakarta.servlet.http.HttpSession
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.RequestMapping
import org.springframework.web.bind.annotation.RestController
import java.time.Clock
import java.util.UUID

@RestController
@RequestMapping("/api/v1/stages")
class StageController(
    private val stages: StageCatalog,
    private val materialPreferences: MaterialPreferenceService,
    private val jdbc: JdbcClient,
    private val clock: Clock,
    private val balanceVersions: com.hanjjak.account.application.BalanceVersionService,
) {
    data class Summary(
        val stageId: String,
        val unlocked: Boolean,
        val clearCount: Long,
        val contentVersion: String,
        val normalMonsterIds: List<String>,
        val bossMonsterId: String?,
        val backgroundId: String?,
        val bossOnly: Boolean,
        val isCurrent: Boolean,
        val isRepeatTarget: Boolean,
        val repeatEligible: Boolean,
    )
    data class Envelope<T>(val requestId: UUID, val serverTime: java.time.Instant, val stateVersion: Long, val data: T)

    @GetMapping
    fun list(session: HttpSession): Envelope<List<Summary>> {
        val accountId = session.getAttribute("accountId") as? UUID
            ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")
        materialPreferences.requireSelected(accountId)
        val cleared = jdbc.sql("select stage_id, highest_clear_count from stage_progress where account_id=:account and first_cleared_at is not null")
            .param("account", accountId)
            .query { rs, _ -> rs.getString("stage_id") to rs.getLong("highest_clear_count") }
            .list()
            .toMap()
        val balanceVersion = balanceVersions.current(accountId)
        val stateVersion = jdbc.sql("select state_version from account where id=:account").param("account", accountId).query(Long::class.java).single()
        val runtimeState = jdbc.sql("select current_stage_id,repeat_stage_id from account_runtime_state where account_id=:account")
            .param("account", accountId)
            .query { row, _ -> row.getString("current_stage_id") to row.getString("repeat_stage_id") }
            .optional()
            .orElse("stage.01-01" to null)
        return Envelope(
            UUID.randomUUID(),
            clock.instant(),
            stateVersion,
            stages.all(balanceVersion).map { stage ->
                val previousIndex = stage.id.globalIndex - 1
                val unlocked = stage.id.globalIndex == 1 || cleared.containsKey("stage.%02d-%02d".format((previousIndex - 1) / 10 + 1, (previousIndex - 1) % 10 + 1))
                Summary(
                    stageId = stage.id.key,
                    unlocked = unlocked,
                    clearCount = cleared[stage.id.key] ?: 0,
                    contentVersion = balanceVersion,
                    normalMonsterIds = stage.normalMonsterIds,
                    bossMonsterId = stage.bossMonsterId,
                    backgroundId = stage.backgroundId,
                    bossOnly = stage.bossOnly,
                    isCurrent = stage.id.key == runtimeState.first,
                    isRepeatTarget = stage.id.key == runtimeState.second,
                    repeatEligible = unlocked && stage.id.number in 1..9,
                )
            },
        )
    }
}
