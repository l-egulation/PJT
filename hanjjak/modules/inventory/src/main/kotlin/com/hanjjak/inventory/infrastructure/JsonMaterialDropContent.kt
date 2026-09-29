package com.hanjjak.inventory.infrastructure

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.inventory.domain.ChapterDropPolicy
import com.hanjjak.inventory.domain.DropMode
import com.hanjjak.inventory.domain.MaterialDropContent
import java.io.InputStream

class JsonMaterialDropContent(input: InputStream, mapper: ObjectMapper) {
    val content: MaterialDropContent

    init {
        val root = mapper.readTree(input)
        require(root.path("authority").asText() in setOf("working", "applied")) { "material drop content authority missing" }
        // Read every chapter the content declares rather than a fixed 1..4 window, so adding
        // chapters to the content file is enough to make them drop materials.
        val weightsNode = root.path("chapterGenerationWeights")
        val declaredChapters = weightsNode.fieldNames().asSequence().mapNotNull(String::toIntOrNull).sorted().toList()
        require(declaredChapters.isNotEmpty()) { "material drop content chapters missing" }
        val legacyWeights = declaredChapters.associateWith { chapter ->
            weightsNode.path(chapter.toString()).map { it.asInt() }
        }
        val policies = root.path("chapters").takeIf { it.isObject }?.let { chapters ->
            chapters.fieldNames().asSequence().mapNotNull(String::toIntOrNull).associateWith { chapter ->
                val policy = chapters.path(chapter.toString())
                ChapterDropPolicy(
                    mode = DropMode.valueOf(policy.path("mode").asText()),
                    normalQuantity = policy.path("normalQuantity").asLong(),
                    bossQuantity = policy.path("bossQuantity").asLong(),
                    generationWeights = policy.path("generationWeights").map { it.asInt() },
                )
            }
        } ?: emptyMap()
        content = MaterialDropContent(
            contentVersion = root.path("contentVersion").asText(),
            dropChanceBasisPoints = root.path("dropChanceBasisPoints").asInt(),
            quantityPerSuccess = root.path("quantityPerSuccess").asLong(),
            normalEnemyRolls = root.path("normalEnemyRolls").asInt(),
            bossRolls = root.path("bossRolls").asInt(),
            chapterGenerationWeights = legacyWeights,
            chapterPolicies = policies,
        )
    }
}
