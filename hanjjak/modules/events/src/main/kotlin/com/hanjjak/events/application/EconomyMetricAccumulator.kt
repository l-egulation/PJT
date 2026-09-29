package com.hanjjak.events.application

import com.fasterxml.jackson.databind.JsonNode
import java.time.Instant
import java.time.LocalDate
import java.time.ZoneId

const val RICE_METRIC_ITEM_ID = "RICE"

data class EconomyDailyMetric(
    val metricDate: LocalDate,
    val itemFamily: String,
    val generation: Int?,
    val itemId: String,
    val listedQuantity: Long,
    val cancelledQuantity: Long,
    val tradeCount: Long,
    val tradedQuantity: Long,
    val tradeAmount: Long,
    val feeAmount: Long,
    val settlementAmount: Long,
    val droppedQuantity: Long,
    val consumedQuantity: Long,
    val riceGenerated: Long,
    val riceConsumed: Long,
)

data class EconomyMetricRefreshRequest(val from: LocalDate, val to: LocalDate)

class EconomyMetricAccumulator(private val zone: ZoneId = ZoneId.of("UTC")) {
    private val rows = linkedMapOf<Key, MutableMetric>()

    fun record(eventType: String, envelope: JsonNode, occurredAt: Instant) {
        val date = occurredAt.atZone(zone).toLocalDate()
        when (eventType) {
            "MARKET_LISTING_CREATED" -> row(date, envelope.itemId()).listedQuantity += envelope.quantity()
            "MARKET_ORDER_CREATED" -> if (envelope.payload().path("side").asText() == "SELL") row(date, envelope.itemId()).listedQuantity += envelope.quantity()
            "MARKET_LISTING_CANCELLED", "MARKET_ORDER_CANCELLED" -> row(date, envelope.itemId()).cancelledQuantity += envelope.quantity()
            "MARKET_TRADE_COMPLETED" -> row(date, envelope.itemId()).apply {
                tradeCount += 1
                tradedQuantity += envelope.quantity()
                tradeAmount += envelope.longAt("totalPrice")
                feeAmount += envelope.payload().longAt("fee")
                settlementAmount += envelope.payload().longAt("settlementAmount")
            }
            "ITEM_DROPPED" -> row(date, envelope.itemId()).droppedQuantity += envelope.quantity()
            "BATTLE_ENEMY_SETTLED" -> row(date, RICE_METRIC_ITEM_ID).riceGenerated += envelope.payload().longAt("riceGained")
            "STAGE_FIRST_CLEAR_REWARD", "STAGE_FIRST_CLEAR_REWARD_CLAIMED" -> {
                val payload = envelope.payload()
                if (eventType == "STAGE_FIRST_CLEAR_REWARD") {
                    row(date, RICE_METRIC_ITEM_ID).riceGenerated += payload.longAt("riceGranted")
                    payload.path("grantedItems").forEach { item ->
                        row(date, item.textAt("itemId")).droppedQuantity += item.longAt("quantity")
                    }
                } else {
                    payload.path("grantedItems").forEach { item ->
                        row(date, item.textAt("itemId")).droppedQuantity += item.longAt("quantity")
                    }
                }
            }
            "EQUIPMENT_CRAFTED", "EQUIPMENT_ENHANCEMENT_ATTEMPTED", "EQUIPMENT_PROMOTED" -> {
                val payload = envelope.payload()
                payload.path("consumedItems").forEach { item -> row(date, item.textAt("itemId")).consumedQuantity += item.longAt("quantity") }
                row(date, RICE_METRIC_ITEM_ID).riceConsumed += payload.longAt("riceCost")
            }
        }
    }

    fun snapshot(): List<EconomyDailyMetric> = rows.values
        .filter { it.hasValue() }
        .sortedWith(compareBy<MutableMetric> { it.date }.thenBy { it.itemId })
        .map { it.toMetric() }

    private fun row(date: LocalDate, itemId: String): MutableMetric = rows.getOrPut(Key(date, itemId)) { MutableMetric(date, itemId, itemFamily(itemId), itemGeneration(itemId)) }

    private data class Key(val date: LocalDate, val itemId: String)

    private data class MutableMetric(
        val date: LocalDate,
        val itemId: String,
        val itemFamily: String,
        val generation: Int?,
        var listedQuantity: Long = 0,
        var cancelledQuantity: Long = 0,
        var tradeCount: Long = 0,
        var tradedQuantity: Long = 0,
        var tradeAmount: Long = 0,
        var feeAmount: Long = 0,
        var settlementAmount: Long = 0,
        var droppedQuantity: Long = 0,
        var consumedQuantity: Long = 0,
        var riceGenerated: Long = 0,
        var riceConsumed: Long = 0,
    ) {
        fun hasValue(): Boolean = listedQuantity + cancelledQuantity + tradeCount + tradedQuantity + tradeAmount + feeAmount + settlementAmount + droppedQuantity + consumedQuantity + riceGenerated + riceConsumed > 0

        fun toMetric(): EconomyDailyMetric = EconomyDailyMetric(
            metricDate = date,
            itemFamily = itemFamily,
            generation = generation,
            itemId = itemId,
            listedQuantity = listedQuantity,
            cancelledQuantity = cancelledQuantity,
            tradeCount = tradeCount,
            tradedQuantity = tradedQuantity,
            tradeAmount = tradeAmount,
            feeAmount = feeAmount,
            settlementAmount = settlementAmount,
            droppedQuantity = droppedQuantity,
            consumedQuantity = consumedQuantity,
            riceGenerated = riceGenerated,
            riceConsumed = riceConsumed,
        )
    }
}

private fun itemFamily(itemId: String): String = when {
    itemId == RICE_METRIC_ITEM_ID -> RICE_METRIC_ITEM_ID
    "_M" in itemId -> itemId.substringBefore("_M")
    ":" in itemId -> itemId.substringBefore(":")
    else -> itemId
}

private fun itemGeneration(itemId: String): Int? = Regex("_M(\\d+)$").find(itemId)?.groupValues?.get(1)?.toInt()

private fun JsonNode.payload(): JsonNode = path("payload")
private fun JsonNode.itemId(): String = textAt("itemId")
private fun JsonNode.quantity(): Long = longAt("quantity")
private fun JsonNode.textAt(name: String): String = path(name).takeIf { it.isTextual }?.asText() ?: throw IllegalArgumentException("EVENT_FIELD_REQUIRED:$name")
private fun JsonNode.longAt(name: String): Long = path(name).takeIf { it.isNumber }?.asLong() ?: 0
