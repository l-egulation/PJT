package com.hanjjak.inventory.domain

import kotlin.random.Random

enum class DropMode { GUARANTEED, CHANCE }

data class ChapterDropPolicy(
    val mode: DropMode,
    val normalQuantity: Long,
    val bossQuantity: Long,
    val generationWeights: List<Int>,
) {
    init {
        require(normalQuantity > 0 && bossQuantity > 0)
        require(generationWeights.size == 4 && generationWeights.all { it >= 0 })
    }
}

data class MaterialDropContent(
    val contentVersion: String,
    val dropChanceBasisPoints: Int,
    val quantityPerSuccess: Long,
    val normalEnemyRolls: Int,
    val bossRolls: Int,
    val chapterGenerationWeights: Map<Int, List<Int>>,
    val chapterPolicies: Map<Int, ChapterDropPolicy> = emptyMap(),
) {
    init {
        require(contentVersion.isNotBlank())
        require(dropChanceBasisPoints in 0..10_000)
        require(quantityPerSuccess > 0)
        require(normalEnemyRolls >= 0 && bossRolls >= 0)
        require((1..4).all { chapter ->
            val weights = chapterGenerationWeights[chapter]
            weights != null && weights.size == 4 && weights.all { it >= 0 } && weights.drop(chapter).all { it == 0 } && weights.take(chapter).any { it > 0 }
        })
        require(chapterPolicies.isEmpty() || (1..4).all { chapter -> chapterPolicies[chapter]?.generationWeights == chapterGenerationWeights[chapter] })
    }

    companion object {
        fun working() = MaterialDropContent(
            contentVersion = "material-drops-v1-draft",
            dropChanceBasisPoints = 1_500,
            quantityPerSuccess = 10,
            normalEnemyRolls = 1,
            bossRolls = 5,
            chapterGenerationWeights = mapOf(
                1 to listOf(1, 0, 0, 0),
                2 to listOf(5, 1, 0, 0),
                3 to listOf(25, 5, 1, 0),
                4 to listOf(125, 25, 5, 1),
            ),
        )
    }
}

class DropTable(val content: MaterialDropContent = MaterialDropContent.working()) {
    val contentVersion: String get() = content.contentVersion

    fun rollStage(
        stageId: String,
        primaryMaterial: MaterialType,
        seed: Long,
        defeatedNormals: Int,
        bossDefeated: Boolean,
    ): List<ItemReward> {
        require(defeatedNormals in 0..20) { "INVALID_DEFEATED_NORMALS" }
        val rewards = (1..defeatedNormals).flatMap { enemyIndex ->
            rollEnemy(stageId, primaryMaterial, seed, enemyIndex, false)
        } + if (bossDefeated) rollEnemy(stageId, primaryMaterial, seed, 21, true) else emptyList()
        return aggregate(rewards)
    }

    fun rollEnemy(
        stageId: String,
        primaryMaterial: MaterialType,
        seed: Long,
        enemyIndex: Int,
        boss: Boolean,
    ): List<ItemReward> {
        require((boss && enemyIndex in 1..21) || (!boss && enemyIndex in 1..20)) { "INVALID_ENEMY_INDEX" }
        val (chapter, stageNumber) = parseStage(stageId)
        if (stageNumber == 10) return emptyList()
        val random = Random(enemySeed(seed, enemyIndex, boss))
        val rewards = mutableListOf<ItemReward>()
        val policy = content.chapterPolicies[chapter]
        if (policy?.mode == DropMode.GUARANTEED) {
            val generation = weighted(random, policy.generationWeights.mapIndexed { index, weight -> index to weight }) + 1
            val material = weighted(random, materialWeights(primaryMaterial))
            rewards += ItemReward("${material.name}_M$generation", if (boss) policy.bossQuantity else policy.normalQuantity)
        } else {
            val materialRolls = if (boss) content.bossRolls else content.normalEnemyRolls
            repeat(materialRolls) {
                if (random.nextInt(10_000) < content.dropChanceBasisPoints) {
                    val weights = policy?.generationWeights ?: content.chapterGenerationWeights.getValue(chapter)
                    val generation = weighted(random, weights.mapIndexed { index, weight -> index to weight }) + 1
                    val material = weighted(random, materialWeights(primaryMaterial))
                    val quantity = policy?.let { if (boss) it.bossQuantity else it.normalQuantity } ?: content.quantityPerSuccess
                    rewards += ItemReward("${material.name}_M$generation", quantity)
                }
            }
        }
        if (random.nextInt(10_000) < SKILL_BOOK_DROP_BASIS_POINTS) {
            rewards += ItemReward(skillBookId(chapter, random), 1)
        }
        return aggregate(rewards)
    }

    private fun aggregate(rewards: List<ItemReward>): List<ItemReward> = rewards
        .groupingBy { it.itemId }
        .fold(0L) { quantity, reward -> quantity + reward.quantity }
        .toSortedMap()
        .map { (itemId, quantity) -> ItemReward(itemId, quantity) }

    private fun enemySeed(seed: Long, enemyIndex: Int, boss: Boolean): Long =
        seed xor (enemyIndex.toLong() * -7046029254386353131L) xor if (boss) -4658895280553007687L else 0L

    private fun skillBookId(chapter: Int, random: Random): String {
        val grade = weighted(random, SKILL_GRADE_WEIGHTS.getValue(chapter))
        val skill = SKILLS[random.nextInt(SKILLS.size)]
        return "skillbook:$skill:$grade"
    }

    private fun materialWeights(primary: MaterialType): List<Pair<MaterialType, Int>> = MaterialType.entries.map {
        it to if (it == primary) 80 else 10
    }

    private fun <T> weighted(random: Random, values: List<Pair<T, Int>>): T {
        val total = values.sumOf { it.second }
        var roll = random.nextInt(total)
        for ((value, weight) in values) {
            if (roll < weight) return value
            roll -= weight
        }
        error("invalid weight table")
    }

    private fun parseStage(stageId: String): Pair<Int, Int> {
        val parts = stageId.removePrefix("stage.").split("-")
        require(parts.size == 2) { "INVALID_STAGE_ID" }
        val chapter = parts[0].toIntOrNull() ?: throw IllegalArgumentException("INVALID_STAGE_ID")
        val stage = parts[1].toIntOrNull() ?: throw IllegalArgumentException("INVALID_STAGE_ID")
        require(chapter in 1..10 && stage in 1..10) { "INVALID_STAGE_ID" }
        return chapter to stage
    }

    companion object {
        private const val SKILL_BOOK_DROP_BASIS_POINTS = 50
        private val SKILLS = listOf("active_heavy", "active_dot", "active_haste", "active_basic_amp", "passive_critical", "passive_all_damage")
        // Chapters 5-10 reuse the chapter-4 mix. Legendary skill books stay out of scope until
        // their first placement is designed, so no new grade is introduced here.
        private val SKILL_GRADE_WEIGHTS = mapOf(
            1 to listOf("normal" to 1),
            2 to listOf("normal" to 2, "rare" to 1),
            3 to listOf("normal" to 2, "rare" to 1),
        ) + (4..10).associateWith { listOf("normal" to 4, "rare" to 2, "epic" to 1) }
    }
}
