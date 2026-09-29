package com.hanjjak.progression.application

import com.hanjjak.progression.domain.FirstClearRewardDefinition
import com.hanjjak.progression.domain.ProgressionContentEntry
import com.hanjjak.progression.domain.ProgressionGrant
import com.hanjjak.progression.infrastructure.JsonProgressionContent

class ProgressionRewardCatalog(private val contents: Map<String, JsonProgressionContent>) {
    fun grant(balanceVersion: String, stageId: String, boss: Boolean): ProgressionGrant {
        val entry = entry(balanceVersion, stageId)
        return ProgressionGrant(if (boss) entry.bossExperience else entry.normalExperience, if (boss) entry.bossRice else entry.normalRice)
    }

    fun firstClearOrNull(balanceVersion: String, stageId: String): FirstClearRewardDefinition? = entry(balanceVersion, stageId).firstClear

    fun entry(balanceVersion: String, stageId: String): ProgressionContentEntry = contents[normalize(balanceVersion)]?.entries?.firstOrNull { it.stageId == stageId }
        ?: throw IllegalArgumentException("PROGRESSION_CONTENT_NOT_FOUND")

    private fun normalize(version: String) = if (version == "enemy-v1-applied") "v1" else version
}
