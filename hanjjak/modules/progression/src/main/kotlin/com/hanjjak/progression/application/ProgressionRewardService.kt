package com.hanjjak.progression.application

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.progression.domain.ProgressionRewardResult
import com.hanjjak.progression.domain.ProgressionRules
import com.hanjjak.wallet.application.WalletService
import org.springframework.transaction.annotation.Transactional
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.util.UUID

open class ProgressionRewardService(
    private val repository: ProgressionRepository,
    private val mapper: ObjectMapper,
    private val wallet: WalletService,
) {
    data class Execution(val result: ProgressionRewardResult, val replayed: Boolean)

    @Transactional
    open fun grantEnemy(
        accountId: UUID,
        settlementId: UUID,
        stageId: String,
        enemyIndex: Int,
        boss: Boolean,
        grant: com.hanjjak.progression.domain.ProgressionGrant = ProgressionRules.grantForEnemy(stageId, boss),
    ): Execution {
        require((boss && enemyIndex in 1..21) || (!boss && enemyIndex in 1..20)) { "INVALID_ENEMY_INDEX" }
        val fingerprint = sha256("battle-enemy\u0000$stageId\u0000$enemyIndex\u0000$boss\u0000${grant.experience}\u0000${grant.rice}")
        repository.lockAccount(accountId)
        repository.command(accountId, settlementId)?.let { existing ->
            require(existing.fingerprint == fingerprint) { "IDEMPOTENCY_KEY_REUSED" }
            return Execution(mapper.readValue(existing.resultJson, ProgressionRewardResult::class.java), true)
        }
        val current = repository.progression(accountId)
        val result = ProgressionRules.applyReward(current, grant)
        if (result.experienceGained > 0 || result.riceGained > 0 || result.levelAfter != result.levelBefore) {
            if (result.riceGained > 0) wallet.credit(accountId, result.riceGained, "BATTLE_ENEMY_SETTLED", settlementId)
            repository.saveProgression(accountId, current.copy(level = result.levelAfter, experience = result.experienceAfter, riceBalance = result.riceBalance))
        }
        repository.saveCommand(accountId, settlementId, fingerprint, mapper.writeValueAsString(result))
        return Execution(result, false)
    }

    @Transactional
    open fun grantOffline(accountId: UUID, jobId: UUID, grant: com.hanjjak.progression.domain.ProgressionGrant): Execution {
        val fingerprint = sha256("offline\u0000${grant.experience}\u0000${grant.rice}")
        repository.lockAccount(accountId)
        repository.command(accountId, jobId)?.let { existing ->
            require(existing.fingerprint == fingerprint) { "IDEMPOTENCY_KEY_REUSED" }
            return Execution(mapper.readValue(existing.resultJson, ProgressionRewardResult::class.java), true)
        }
        val current = repository.progression(accountId)
        val result = ProgressionRules.applyReward(current, grant)
        if (result.experienceGained > 0 || result.riceGained > 0 || result.levelAfter != result.levelBefore) {
            if (result.riceGained > 0) wallet.credit(accountId, result.riceGained, "OFFLINE_REWARD", jobId)
            repository.saveProgression(accountId, current.copy(level = result.levelAfter, experience = result.experienceAfter, riceBalance = result.riceBalance))
        }
        repository.saveCommand(accountId, jobId, fingerprint, mapper.writeValueAsString(result))
        return Execution(result, false)
    }

    @Transactional(readOnly = true)
    open fun current(accountId: UUID): ProgressionRewardResult = ProgressionRules.applyReward(
        repository.progression(accountId),
        com.hanjjak.progression.domain.ProgressionGrant(0, 0),
    )

    private fun sha256(value: String): String = MessageDigest.getInstance("SHA-256")
        .digest(value.toByteArray(StandardCharsets.UTF_8)).joinToString("") { "%02x".format(it) }
}
