package com.hanjjak.market.application

import com.hanjjak.inventory.application.ItemCatalog
import com.hanjjak.inventory.domain.ItemCategory
import com.hanjjak.market.domain.MarketInstrument
import com.hanjjak.market.domain.MarketInstrumentIds
import com.hanjjak.market.domain.MarketInstrumentStatus
import java.time.Clock

class MarketInstrumentCatalog(
    private val repository: OrderBookRepository,
    private val catalog: ItemCatalog,
    private val clock: Clock,
) {
    fun synchronize() {
        val instruments = catalog.all()
            .asSequence()
            .filter { it.tradeable && it.category in setOf(ItemCategory.MATERIAL, ItemCategory.SKILL_BOOK, ItemCategory.GEM) }
            .flatMap { definition ->
                val canonicalKeys = if (definition.category == ItemCategory.GEM) {
                    val parts = definition.itemId.split(":")
                    val level = parts.getOrNull(1)?.toIntOrNull() ?: return@flatMap emptySequence()
                    val option = when (parts.getOrNull(2)) {
                        "haste" -> "attack_speed"
                        else -> parts.getOrNull(2) ?: return@flatMap emptySequence()
                    }
                    val value = gemValue(level, option)
                    sequenceOf("gem:$level:$option:$value")
                } else {
                    sequenceOf(MarketInstrumentIds.canonicalKey(definition.itemId))
                }
                canonicalKeys.map { canonicalKey ->
                    MarketInstrument(
                        instrumentId = MarketInstrumentIds.id(canonicalKey),
                        canonicalKey = canonicalKey,
                        itemId = definition.itemId,
                        displayName = definition.displayName,
                        category = definition.category.name,
                        attributes = attributes(canonicalKey),
                        status = MarketInstrumentStatus.ACTIVE,
                        marketRevision = 1,
                    )
                }
            }
            .distinctBy { it.instrumentId }
            .sortedBy { it.canonicalKey }
            .toList()
        repository.synchronizeInstruments(instruments, clock.instant())
    }

    private fun attributes(canonicalKey: String): Map<String, String> {
        val parts = canonicalKey.split(":")
        return when (parts.firstOrNull()) {
            "material" -> mapOf("itemId" to parts.drop(1).joinToString(":"))
            "skillbook" -> mapOf("skillId" to parts.getOrElse(1) { "" }, "grade" to parts.getOrElse(2) { "" })
            "gem" -> mapOf("level" to parts.getOrElse(1) { "" }, "option" to parts.getOrElse(2) { "" }, "value" to parts.getOrElse(3) { "" })
            else -> emptyMap()
        }
    }

    private fun gemValue(level: Int, option: String): Int = when (option) {
        "flat_attack" -> listOf(0, 3, 7, 15, 32, 70, 150, 320)[level]
        "flat_hp" -> listOf(0, 30, 70, 150, 320, 700, 1500, 3200)[level]
        "attack_percent" -> if (level == 6) 800 else 1500
        "flat_penetration" -> if (level == 6) 60 else 120
        "critical_chance", "attack_speed" -> 1200
        else -> throw IllegalArgumentException("INVALID_MARKET_ITEM")
    }
}
