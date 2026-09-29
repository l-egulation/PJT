package com.hanjjak.cosmetics.domain

import com.fasterxml.jackson.annotation.JsonInclude
import java.util.UUID

enum class CosmeticGrade { NORMAL, RARE, EPIC, LEGENDARY }
enum class CosmeticSlot { HEAD, TOP, BOTTOM, GLOVES, SHOES, CAPE }
enum class RegistrationMode { ONE, UNTIL_NEXT_STAR }
enum class EffectUnit { BASIS_POINTS, PERCENTAGE_POINTS, SECONDS }

data class StatEffect(val statId: String, val value: Int, val unit: EffectUnit)
data class CosmeticDefinition(val id: String, val displayName: String?, val imageUrl: String?, val grade: CosmeticGrade, val slot: CosmeticSlot, val setId: String)
data class SetDefinition(
    val id: String,
    val displayName: String?,
    val grade: CosmeticGrade,
    val bannerId: String?,
    val boxItemId: String?,
    val members: Map<CosmeticSlot, String>,
    val effects: Map<Int, List<StatEffect>>,
)
data class CosmeticsContent(
    val version: String,
    val singleRiceCost: Long,
    val gradeProbabilities: Map<CosmeticGrade, Int>,
    val starThresholdsByGrade: Map<CosmeticGrade, List<Int>>,
    val cosmetics: List<CosmeticDefinition>,
    val sets: List<SetDefinition>,
)
data class CosmeticState(
    val accountId: UUID,
    val cosmeticId: String,
    val registeredQuantity: Int,
    val unregisteredQuantity: Int,
    val reservedQuantity: Int,
) {
    val availableUnregisteredQuantity: Int get() = unregisteredQuantity - reservedQuantity
    init {
        require(registeredQuantity >= 0 && unregisteredQuantity >= 0 && reservedQuantity >= 0)
        require(availableUnregisteredQuantity >= 0)
    }
}
data class EquipmentSlot(val accountId: UUID, val slot: CosmeticSlot, val cosmeticId: String?)

@JsonInclude(JsonInclude.Include.ALWAYS)
data class CosmeticView(
    val cosmeticId: String,
    val displayName: String?,
    val imageUrl: String?,
    val grade: CosmeticGrade,
    val slot: CosmeticSlot,
    val registeredQuantity: Int,
    val unregisteredQuantity: Int,
    val reservedQuantity: Int,
    val availableUnregisteredQuantity: Int,
    val cosmeticStar: Int,
    val nextStarThreshold: Int?,
    val neededForNextStar: Int?,
    val canUpgrade: Boolean = false,
    val upgradeDisabledReason: String? = "REFRESH_REQUIRED",
)

data class CollectionSnapshot(
    val states: List<CosmeticView>,
    val equipment: Map<CosmeticSlot, String?>,
    val uniqueRegisteredCount: Int,
    val setStars: Map<String, Int>,
    val setEffects: Map<String, List<StatEffect>>,
    val totalEffects: List<StatEffect>,
    val contentVersion: String,
)

data class VersionedResult<T>(val data: T, val stateVersion: Long)

@JsonInclude(JsonInclude.Include.ALWAYS)
data class CosmeticCatalogItem(
    val cosmeticId: String,
    val displayName: String?,
    val grade: CosmeticGrade,
    val slot: CosmeticSlot,
    val setId: String,
    val imageUrl: String?,
)

@JsonInclude(JsonInclude.Include.ALWAYS)
data class CosmeticCatalogSet(
    val setId: String,
    val displayName: String?,
    val grade: CosmeticGrade,
    val members: Map<CosmeticSlot, String>,
    val effects: Map<Int, List<StatEffect>>,
)

data class CosmeticsCatalog(
    val contentVersion: String,
    val cosmetics: List<CosmeticCatalogItem>,
    val sets: List<CosmeticCatalogSet>,
)
