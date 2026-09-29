package com.hanjjak.gameapi

import com.hanjjak.cosmetics.domain.CosmeticsContent
import com.hanjjak.inventory.application.ItemDefinitionSource
import com.hanjjak.inventory.domain.ItemCategory
import com.hanjjak.inventory.domain.ItemDefinition
import org.springframework.stereotype.Component

/**
 * 치장 세트 콘텐츠에서 선택 상자 아이템 정의를 파생해 인벤토리 카탈로그에 공급한다.
 * 상자 식별자와 표시명의 정본은 치장 콘텐츠이므로 인벤토리 카탈로그에 다시 적지 않는다.
 */
@Component
class CosmeticSelectorBoxItems(private val content: CosmeticsContent) : ItemDefinitionSource {
    override fun items(): List<ItemDefinition> = content.sets.mapNotNull { set ->
        val boxItemId = set.boxItemId ?: return@mapNotNull null
        val setName = set.displayName ?: "세트 #${set.id.takeLast(2)}"
        ItemDefinition(
            itemId = boxItemId,
            displayName = "$setName 선택 상자",
            icon = "/assets/items/$boxItemId.webp",
            category = ItemCategory.COSMETIC_BOX,
            description = "열어서 $setName 치장 하나를 직접 골라 받는 상자입니다.",
            acquisitionSources = listOf("$setName 뽑기 마일스톤 수령"),
            usages = listOf("치장 선택 상자 개봉"),
            tradeable = false,
            stackable = true,
        )
    }
}
