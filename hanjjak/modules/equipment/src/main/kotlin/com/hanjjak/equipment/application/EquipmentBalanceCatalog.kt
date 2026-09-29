package com.hanjjak.equipment.application

import com.hanjjak.equipment.domain.EquipmentBalanceContent
import com.hanjjak.equipment.domain.EquipmentBalancePolicy

/** Selects immutable equipment cost policy by account-owned balance version. */
class EquipmentBalanceCatalog(private val contents: Map<String, EquipmentBalanceContent>) {
    fun policy(contentVersion: String): EquipmentBalancePolicy =
        contents[normalize(contentVersion)]?.let(::EquipmentBalancePolicy)
            ?: if (normalize(contentVersion) == "v1" && contents.isEmpty()) EquipmentBalancePolicy.legacy() else throw IllegalArgumentException("BALANCE_VERSION_UNKNOWN")

    private fun normalize(version: String): String = if (version == "enemy-v1-applied") "v1" else version
    companion object {
        fun legacy(): EquipmentBalanceCatalog = EquipmentBalanceCatalog(emptyMap())
    }
}
