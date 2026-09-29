package com.hanjjak.equipment.application

import com.hanjjak.equipment.domain.EquipmentBattleStats
import com.hanjjak.equipment.domain.EquipmentSlot
import com.hanjjak.equipment.domain.EquipmentSlotState
import com.hanjjak.equipment.domain.MaterialCost
import java.util.UUID

interface EquipmentStatsProvider {
    fun stats(accountId: UUID): EquipmentBattleStats
}

interface EquipmentRepository {
    data class StoredCommand(val fingerprint: String, val resultJson: String)

    fun lockAccount(accountId: UUID)
    fun balanceVersion(accountId: UUID): String
    fun riceBalance(accountId: UUID): Long
    fun materialBalances(accountId: UUID, itemIds: Collection<String>): Map<String, Long>
    fun slotStates(accountId: UUID): List<EquipmentSlotState>
    fun findSlotState(accountId: UUID, slot: EquipmentSlot): EquipmentSlotState?
    fun firstClearedStages(accountId: UUID, stageIds: Collection<String>): Set<String>
    fun consumeMaterials(accountId: UUID, materials: List<MaterialCost>)
    fun createSlotState(state: EquipmentSlotState)
    fun updateSlotState(state: EquipmentSlotState)
    fun incrementStateVersion(accountId: UUID): Long
    fun command(accountId: UUID, idempotencyKey: UUID): StoredCommand?
    fun saveCommand(accountId: UUID, idempotencyKey: UUID, fingerprint: String, resultJson: String)
}
