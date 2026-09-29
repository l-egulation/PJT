package com.hanjjak.progression.infrastructure

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.progression.application.ProgressionRewardCatalog
import com.hanjjak.progression.domain.FirstClearItem
import com.hanjjak.progression.domain.FirstClearRewardDefinition
import com.hanjjak.progression.domain.ProgressionContentEntry
import java.io.InputStream

class JsonProgressionContent(input: InputStream, mapper: ObjectMapper) {
    val contentVersion: String
    val entries: List<ProgressionContentEntry>
    init {
        val root = mapper.readTree(input)
        require(root.path("authority").asText() in setOf("working", "applied")) { "progression content authority missing" }
        contentVersion = root.path("contentVersion").asText()
        entries = root.path("stages").map { node ->
            ProgressionContentEntry(node.path("stageId").asText(), node.path("normalExperience").asLong(), node.path("bossExperience").asLong(), node.path("normalRice").asLong(), node.path("bossRice").asLong(), node.path("firstClear").takeUnless { it.isMissingNode || it.isNull }?.let { first -> FirstClearRewardDefinition(first.path("rice").asLong(), first.path("items").map { item -> FirstClearItem(item.path("itemId").asText(), item.path("quantity").asLong()) }, first.path("directSkillId").takeUnless { it.isMissingNode || it.isNull }?.asText()) })
        }
        require(contentVersion.isNotBlank() && entries.isNotEmpty()) { "invalid progression content" }
    }
    companion object {
        fun fromResources(mapper: ObjectMapper): ProgressionRewardCatalog {
            val v1 = JsonProgressionContent(resource("/v1/progression/progression.json"), mapper)
            val rebalance = JsonProgressionContent(resource("/progression-rebalance-v1/progression/progression.json"), mapper)
            return ProgressionRewardCatalog(mapOf("v1" to v1, "progression-rebalance-v1" to rebalance))
        }
        private fun resource(path: String) = requireNotNull(JsonProgressionContent::class.java.getResourceAsStream(path)) { "progression content missing: $path" }
    }
}
