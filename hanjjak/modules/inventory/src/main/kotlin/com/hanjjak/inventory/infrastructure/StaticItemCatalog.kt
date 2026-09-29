package com.hanjjak.inventory.infrastructure

import com.hanjjak.inventory.application.ItemCatalog
import com.hanjjak.inventory.domain.ItemCategory
import com.hanjjak.inventory.domain.ItemDefinition

class StaticItemCatalog(additional: List<ItemDefinition> = emptyList()) : ItemCatalog {
    private val items: Map<String, ItemDefinition> = buildList {
        val materials = listOf(
            "POTATO" to listOf("감자 한 조각", "미니 감자", "감자", "황금 감자", "전설 감자"),
            "SWEET_POTATO" to listOf("고구마 한 조각", "미니 고구마", "고구마", "황금 고구마", "전설 고구마"),
            "CORN" to listOf("옥수수 한 알", "미니 옥수수", "옥수수", "황금 옥수수", "전설 옥수수"),
        )
        for ((id, names) in materials) for (generation in 1..5) add(
            ItemDefinition(
                itemId = "${id}_M$generation",
                displayName = names[generation - 1],
                icon = "/assets/items/${id.lowercase()}-m$generation.webp",
                category = ItemCategory.MATERIAL,
                description = "장비 제작과 강화에 사용하는 ${generation}세대 재료",
                acquisitionSources = if (generation < 5) listOf("챕터 $generation 이후 자동 파밍") else listOf("공급처 확정 전"),
                usages = listOf("장비 제작", "장비 강화"),
                tradeable = true,
                stackable = true,
            ),
        )
        val skills = listOf(
            "active_heavy" to "한짝의 일격",
            "active_dot" to "마! 쫄이나",
            "active_haste" to "잘게 더 잘게!",
            "active_basic_amp" to "화력 최대로!",
            "passive_critical" to "회심의 간",
            "passive_all_damage" to "오늘의 특선",
        )
        val grades = listOf("normal" to "노말", "rare" to "희귀", "epic" to "영웅", "legendary" to "전설")
        for ((skillId, skillName) in skills) for ((gradeId, gradeName) in grades) add(
            ItemDefinition(
                itemId = "skillbook:$skillId:$gradeId",
                displayName = "$gradeName $skillName 비법서",
                icon = "/assets/items/skillbook-$skillId-$gradeId.webp",
                category = ItemCategory.SKILL_BOOK,
                description = "$skillName 스킬의 $gradeName 등급 성장에 사용하는 전용 스킬북입니다.",
                acquisitionSources = listOf("챕터 1~4 파밍 스테이지 1~9"),
                usages = listOf("스킬 해금", "스킬 승급", "스킬 강화"),
                tradeable = true,
                stackable = true,
            ),
        )
        add(
            ItemDefinition(
                itemId = "GEM_BOX",
                displayName = "보석함",
                icon = "/assets/items/gem-box.webp",
                category = ItemCategory.GEM_BOX,
                description = "열면 보석 한 개를 획득하는 계정 귀속 상자입니다.",
                acquisitionSources = listOf("일일 보석 던전 최초 클리어", "일일 보석 던전 소탕"),
                usages = listOf("보석함 개봉"),
                tradeable = false,
                stackable = true,
            ),
        )
        for (level in 1..7) for ((optionId, optionName) in listOf("flat_attack" to "고정 공격력", "flat_hp" to "고정 최대 HP", "attack_percent" to "공격력%", "flat_penetration" to "고정 방어 관통", "critical_chance" to "치명타 확률", "attack_speed" to "공격속도", "haste" to "공격속도")) add(
            ItemDefinition(
                itemId = "gem:$level:$optionId",
                displayName = "${level}레벨 $optionName 보석",
                icon = "/assets/items/gem-$optionId.webp",
                category = ItemCategory.GEM,
                description = "장착하거나 합성할 수 있는 보석입니다.",
                acquisitionSources = listOf("보석함 개봉", "보석 합성"),
                usages = listOf("보석 프리셋", "보석 합성"),
                tradeable = true,
                stackable = false,
            ),
        )
        addAll(additional)
    }.also { definitions ->
        val duplicated = definitions.groupingBy { it.itemId }.eachCount().filterValues { it > 1 }.keys
        require(duplicated.isEmpty()) { "DUPLICATE_ITEM_DEFINITION: $duplicated" }
    }.associateBy { it.itemId }

    override fun all(): List<ItemDefinition> = items.values.toList()
    override fun find(itemId: String): ItemDefinition? = items[itemId]
}
