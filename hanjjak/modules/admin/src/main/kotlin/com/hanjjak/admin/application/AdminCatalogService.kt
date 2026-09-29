package com.hanjjak.admin.application

import com.hanjjak.cosmetics.domain.CosmeticsContent
import com.hanjjak.inventory.application.ItemCatalog
import com.hanjjak.stage.application.StageCatalog
import org.springframework.stereotype.Service

@Service
class AdminCatalogService(
    private val items: ItemCatalog,
    private val cosmetics: CosmeticsContent,
    private val stages: StageCatalog,
) {
    data class Item(val itemId: String, val displayName: String, val category: String, val stackable: Boolean, val tradeable: Boolean)
    data class Cosmetic(val cosmeticId: String, val displayName: String, val grade: String, val slot: String)
    data class Stage(val stageId: String, val displayName: String)
    data class Catalog(val items: List<Item>, val cosmetics: List<Cosmetic>, val stages: List<Stage>)

    fun catalog(): Catalog = Catalog(
        items.all().map { Item(it.itemId, it.displayName, it.category.name, it.stackable, it.tradeable) },
        cosmetics.cosmetics.map { Cosmetic(it.id, it.displayName ?: it.id, it.grade.name, it.slot.name) },
        stages.all().map { Stage(it.id.key, it.id.key) },
    )
}
