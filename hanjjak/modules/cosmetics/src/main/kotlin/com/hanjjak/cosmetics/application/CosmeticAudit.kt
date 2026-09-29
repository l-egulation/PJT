package com.hanjjak.cosmetics.application

enum class CosmeticOperation {
    DRAW,
    REGISTRATION,
    EQUIPMENT,
    MILESTONE_CLAIM,
    SELECTOR_BOX_OPEN,
}

data class DrawReproduction(
    val algorithmVersion: String,
    val keyId: String,
    val reproductionToken: String,
)

data class CosmeticAudit(
    val operation: CosmeticOperation,
    val contentVersion: String,
    val details: Map<String, Any?>,
    val reproduction: DrawReproduction? = null,
) {
    init {
        require(contentVersion.isNotBlank()) { "COSMETICS_CONTENT_VERSION_REQUIRED" }
        if (operation == CosmeticOperation.DRAW) requireNotNull(reproduction) { "DRAW_REPRODUCTION_REQUIRED" }
        else require(reproduction == null) { "DRAW_REPRODUCTION_NOT_ALLOWED" }
    }
}
