package com.hanjjak.progression.domain

data class FirstClearRewardDefinition(
    val rice: Long,
    val items: List<FirstClearItem>,
    val directSkillId: String?,
)

data class FirstClearItem(val itemId: String, val quantity: Long)

data class ProgressionContentEntry(
    val stageId: String,
    val normalExperience: Long,
    val bossExperience: Long,
    val normalRice: Long,
    val bossRice: Long,
    val firstClear: FirstClearRewardDefinition?,
)
