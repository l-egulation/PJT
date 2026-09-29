package com.hanjjak.stage.infrastructure

import com.fasterxml.jackson.databind.JsonNode
import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.stage.application.StageCatalog
import com.hanjjak.stage.domain.BossType
import com.hanjjak.stage.domain.StageDefinition
import com.hanjjak.stage.domain.StageId
import java.io.InputStream

class JsonStageCatalog(input: InputStream, mapper: ObjectMapper) : StageCatalog {
    private val definitions: List<StageDefinition> = mapper.readTree(input).path("stages").map { node ->
        StageDefinition(
            id = parseId(node.path("id").asText()),
            enemyLevel = node.path("enemyLevel").asInt(),
            enemyDefense = node.path("enemyDefense").asInt(),
            normalHp = node.path("normalHp").asInt(),
            normalAttack = node.path("normalAttack").asInt(),
            bossHp = node.path("bossHp").asInt(),
            bossAttack = node.path("bossAttack").asInt(),
            bossType = BossType.valueOf(node.path("bossType").asText()),
            normalMonsterIds = node.path("normalMonsterIds").map { it.asText() },
            bossMonsterId = node.path("bossMonsterId").takeUnless { it.isMissingNode }?.asText(),
            backgroundId = node.path("backgroundId").takeUnless { it.isMissingNode }?.asText(),
            timeLimitTicks = timeLimit(node),
            scheduledStrikes = strikes(node.path("pattern").asText()),
        )
    }.also { rows ->
        require(rows.isNotEmpty() && rows.size % StageId.STAGES_PER_CHAPTER == 0 && rows.size <= StageId.MAX_GLOBAL_INDEX) {
            "expected whole chapters up to ${StageId.MAX_GLOBAL_INDEX} stages, got ${rows.size}"
        }
    }

    override fun all(): List<StageDefinition> = definitions

    private fun parseId(value: String): StageId {
        val parts = value.removePrefix("stage.").split("-")
        return StageId(parts[0].toInt(), parts[1].toInt())
    }

    private fun timeLimit(node: JsonNode): Int? {
        if (node.path("bossType").asText() != BossType.ATTACK_CHECK.name) return null
        val seconds = Regex("(\\d+)초 제한").find(node.path("pattern").asText())?.groupValues?.get(1)?.toInt()
            ?: throw IllegalArgumentException("attack check without time limit")
        return seconds * 10
    }

    private fun strikes(pattern: String): Map<Int, Int> {
        val damage = Regex("([0-9,]+)$").find(pattern)?.groupValues?.get(1)?.replace(",", "")?.toIntOrNull() ?: return emptyMap()
        return Regex("[0-9]+").findAll(pattern.substringBefore("초 강타")).associate { it.value.toInt() * 10 to damage }
    }
}
