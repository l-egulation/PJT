package com.hanjjak.progression.domain

import kotlin.math.roundToLong

data class CharacterProgression(
    val level: Int,
    val experience: Long,
    val riceBalance: Long,
)

data class ProgressionGrant(
    val experience: Long,
    val rice: Long,
)

data class ProgressionRewardResult(
    val experienceGained: Long,
    val riceGained: Long,
    val levelBefore: Int,
    val levelAfter: Int,
    val experienceBefore: Long,
    val experienceAfter: Long,
    val experienceToNextLevel: Long,
    val riceBalance: Long,
)

object ProgressionRules {
    const val MAX_LEVEL: Int = 500
    const val MAX_STAGE_INDEX: Int = 100
    private const val STAGES_PER_CHAPTER: Int = 10

    fun grantForBattle(stageId: String, defeatedNormals: Int, bossDefeated: Boolean): ProgressionGrant {
        require(defeatedNormals in 0..20) { "INVALID_DEFEATED_NORMALS" }
        val normal = grantForEnemy(stageId, false)
        val boss = grantForEnemy(stageId, true)
        return ProgressionGrant(
            experience = defeatedNormals * normal.experience + if (bossDefeated) boss.experience else 0,
            rice = defeatedNormals * normal.rice + if (bossDefeated) boss.rice else 0,
        )
    }

    fun grantForEnemy(stageId: String, boss: Boolean): ProgressionGrant {
        val globalIndex = globalIndex(stageId)
        val normalXp = experienceForNormal(globalIndex)
        val normalRice = globalIndex.toLong()
        return ProgressionGrant(
            experience = if (boss) normalXp * 5 else normalXp,
            rice = if (boss) normalRice * 10 else normalRice,
        )
    }

    fun applyReward(current: CharacterProgression, grant: ProgressionGrant): ProgressionRewardResult {
        require(current.level in 1..MAX_LEVEL) { "INVALID_LEVEL" }
        require(current.experience >= 0) { "INVALID_EXPERIENCE" }
        require(current.riceBalance >= 0) { "INVALID_RICE" }
        require(grant.experience >= 0) { "INVALID_EXPERIENCE_REWARD" }
        require(grant.rice >= 0) { "INVALID_RICE_REWARD" }

        val experienceBefore = current.experience.coerceAtMost(totalExperienceForLevel(MAX_LEVEL))
        val experienceAfter = if (current.level >= MAX_LEVEL) {
            totalExperienceForLevel(MAX_LEVEL)
        } else {
            Math.addExact(experienceBefore, grant.experience).coerceAtMost(totalExperienceForLevel(MAX_LEVEL))
        }
        var levelAfter = current.level
        while (levelAfter < MAX_LEVEL && experienceAfter >= totalExperienceForLevel(levelAfter + 1)) {
            levelAfter++
        }
        val riceAfter = Math.addExact(current.riceBalance, grant.rice)
        return ProgressionRewardResult(
            experienceGained = experienceAfter - experienceBefore,
            riceGained = grant.rice,
            levelBefore = current.level,
            levelAfter = levelAfter,
            experienceBefore = experienceBefore,
            experienceAfter = experienceAfter,
            experienceToNextLevel = if (levelAfter >= MAX_LEVEL) 0 else totalExperienceForLevel(levelAfter + 1) - experienceAfter,
            riceBalance = riceAfter,
        )
    }

    fun experienceForNormal(globalIndex: Int): Long {
        require(globalIndex in 1..MAX_STAGE_INDEX) { "INVALID_STAGE_INDEX" }
        val referenceLevel = 2 * globalIndex - 1
        val levelMinutes = 6.0 + (11.0 / 30.0) * (2 * referenceLevel - 1)
        return (1_000.0 * referenceLevel / (50.0 * levelMinutes)).roundToLong().coerceAtLeast(1)
    }

    fun totalExperienceForLevel(level: Int): Long {
        require(level in 1..MAX_LEVEL) { "INVALID_LEVEL" }
        return 500L * level * (level - 1)
    }

    private fun globalIndex(stageId: String): Int {
        val match = Regex("^stage\\.(\\d{2})-(\\d{2})$").matchEntire(stageId) ?: throw IllegalArgumentException("STAGE_NOT_FOUND")
        val chapter = match.groupValues[1].toInt()
        val number = match.groupValues[2].toInt()
        require(chapter in 1..(MAX_STAGE_INDEX / STAGES_PER_CHAPTER) && number in 1..STAGES_PER_CHAPTER) { "STAGE_NOT_FOUND" }
        return STAGES_PER_CHAPTER * (chapter - 1) + number
    }
}
