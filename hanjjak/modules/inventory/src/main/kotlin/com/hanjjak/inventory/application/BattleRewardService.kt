package com.hanjjak.inventory.application

import com.hanjjak.inventory.domain.DropTable
import java.util.UUID

class BattleRewardService(
    private val preferences: MaterialPreferenceProvider,
    private val drops: MaterialDropCatalog,
    private val rewards: InventoryRewardService,
) {
    data class Prepared(val contentVersion: String, val rewards: List<com.hanjjak.inventory.domain.ItemReward>)

    fun prepare(
        accountId: UUID,
        stageId: String,
        seed: Long,
        enemyIndex: Int,
        boss: Boolean,
        balanceVersion: String = "enemy-v1-applied",
    ): Prepared {
        val table = drops.table(balanceVersion)
        return Prepared(table.contentVersion, table.rollEnemy(stageId, preferences.primaryMaterial(accountId), seed, enemyIndex, boss))
    }

    fun grantPrepared(
        accountId: UUID,
        settlementId: UUID,
        source: String,
        prepared: List<com.hanjjak.inventory.domain.ItemReward>,
    ): InventoryRewardService.Execution = rewards.grantOnce(accountId, settlementId, source, prepared)

    fun grantEnemy(
        accountId: UUID,
        settlementId: UUID,
        stageId: String,
        seed: Long,
        enemyIndex: Int,
        boss: Boolean,
        balanceVersion: String = "enemy-v1-applied",
    ): InventoryRewardService.Execution {
        val prepared = prepare(accountId, stageId, seed, enemyIndex, boss, balanceVersion)
        return grantPrepared(accountId, settlementId, "battle:$stageId:$enemyIndex:$boss:${prepared.contentVersion}", prepared.rewards)
    }
}
