package com.hanjjak.gems.domain

import com.fasterxml.jackson.databind.ObjectMapper
import java.io.InputStream

data class GemDungeonBossDefinition(
    val boss: GemPreset,
    val stage: Int,
    val bossHp: Int,
    val bossAttack: Int,
    val defense: Int,
    val strikeDamage: Int,
)

class GemDungeonCatalog private constructor(
    val contentVersion: String,
    val durationTicks: Int,
    val completionWindowSeconds: Long,
    private val definitions: Map<Pair<GemPreset, Int>, GemDungeonBossDefinition>,
) {
    fun require(boss: GemPreset, stage: Int): GemDungeonBossDefinition =
        definitions[boss to stage] ?: throw IllegalArgumentException("GEM_DUNGEON_STAGE_NOT_FOUND")

    companion object {
        fun load(mapper: ObjectMapper, input: InputStream, expectedAuthority: String = "applied"): GemDungeonCatalog {
            val root = mapper.readTree(input)
            require(root.path("authority").asText() == expectedAuthority) { "GEM_DUNGEON_CONTENT_NOT_APPLIED" }
            val values = root.path("bosses").flatMap { bossNode ->
                val boss = GemPreset.valueOf(bossNode.path("bossType").asText())
                bossNode.path("stages").map { stageNode ->
                    GemDungeonBossDefinition(
                        boss,
                        stageNode.path("stage").asInt(),
                        stageNode.path("bossHp").asInt(),
                        stageNode.path("bossAttack").asInt(),
                        stageNode.path("defense").asInt(),
                        stageNode.path("strikeDamage").asInt(),
                    )
                }
            }
            require(values.size == 30 && values.map { it.boss to it.stage }.distinct().size == 30) { "INVALID_GEM_DUNGEON_CONTENT" }
            return GemDungeonCatalog(
                root.path("contentVersion").asText(),
                root.path("durationTicks").asInt(),
                root.path("completionWindowSeconds").asLong(),
                values.associateBy { it.boss to it.stage },
            )
        }
    }
}
