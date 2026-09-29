package com.hanjjak.stage.domain

data class StageId(val chapter: Int, val number: Int) {
    init { require(chapter in 1..MAX_CHAPTER && number in 1..STAGES_PER_CHAPTER) }
    val key: String = "stage.%02d-%02d".format(chapter, number)
    val globalIndex: Int = STAGES_PER_CHAPTER * (chapter - 1) + number

    companion object {
        const val MAX_CHAPTER: Int = 10
        const val STAGES_PER_CHAPTER: Int = 10
        const val MAX_GLOBAL_INDEX: Int = MAX_CHAPTER * STAGES_PER_CHAPTER
    }
}

data class StageDefinition(
    val id: StageId,
    val enemyLevel: Int,
    val enemyDefense: Int,
    val normalHp: Int,
    val normalAttack: Int,
    val bossHp: Int,
    val bossAttack: Int,
    val bossType: BossType,
    val normalMonsterIds: List<String> = emptyList(),
    val bossMonsterId: String? = null,
    val backgroundId: String? = null,
    val timeLimitTicks: Int? = null,
    val scheduledStrikes: Map<Int, Int> = emptyMap(),
) {
    val bossOnly: Boolean = id.number == StageId.STAGES_PER_CHAPTER
}

enum class BossType { STANDARD, ATTACK_CHECK, HP_CHECK, PENETRATION_CHECK }
