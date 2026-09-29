package com.hanjjak.account.domain
import com.fasterxml.jackson.annotation.JsonProperty

enum class MaterialType(val displayName: String) {
    POTATO("감자"),
    SWEET_POTATO("고구마"),
    CORN("옥수수"),
}

data class MaterialDropRates(
    @get:JsonProperty("POTATO") val potato: Int,
    @get:JsonProperty("SWEET_POTATO") val sweetPotato: Int,
    @get:JsonProperty("CORN") val corn: Int,
)

data class MaterialOption(
    val materialType: MaterialType,
    val displayName: String,
    val dropRates: MaterialDropRates,
)

data class MaterialPreferenceState(
    val selected: Boolean,
    val primaryMaterialType: MaterialType?,
    val options: List<MaterialOption>,
    val populationCounts: Map<MaterialType, Long> = emptyMap(),
)
data class MaterialPreferenceSnapshot<T>(
    val stateVersion: Long,
    val data: T,
)

data class MaterialPreferenceSelection(
    val selected: Boolean,
    val primaryMaterialType: MaterialType,
    val dropRates: MaterialDropRates,
)

class MaterialAlreadySelectedException(val current: MaterialType) : IllegalArgumentException("MATERIAL_ALREADY_SELECTED")
