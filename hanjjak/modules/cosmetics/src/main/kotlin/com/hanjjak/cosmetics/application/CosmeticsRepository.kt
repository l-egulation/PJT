package com.hanjjak.cosmetics.application

import com.hanjjak.cosmetics.domain.CosmeticSlot
import com.hanjjak.cosmetics.domain.CosmeticState
import com.hanjjak.cosmetics.domain.EquipmentSlot
import java.util.UUID

interface CosmeticsRepository {
    fun stateVersion(accountId: UUID): Long
    fun findStates(accountId: UUID): List<CosmeticState>
    fun findStateForUpdate(accountId: UUID, cosmeticId: String): CosmeticState?
    fun lockAccount(accountId: UUID)
    fun saveState(state: CosmeticState)
    fun findEquipment(accountId: UUID): List<EquipmentSlot>
    fun saveEquipment(accountId: UUID, slot: CosmeticSlot, cosmeticId: String?)
}
