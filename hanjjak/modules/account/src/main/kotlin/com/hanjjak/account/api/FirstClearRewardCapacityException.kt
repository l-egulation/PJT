package com.hanjjak.account.api

class FirstClearRewardCapacityException(
    val requiredSlots: Int,
    val availableSlots: Int,
) : RuntimeException("INVENTORY_CAPACITY_EXCEEDED") {
    val missingSlots: Int = (requiredSlots - availableSlots).coerceAtLeast(0)
}
