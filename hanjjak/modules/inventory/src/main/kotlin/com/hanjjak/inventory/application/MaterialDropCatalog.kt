package com.hanjjak.inventory.application

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.inventory.domain.DropTable
import com.hanjjak.inventory.infrastructure.JsonMaterialDropContent

class MaterialDropCatalog(private val tables: Map<String, DropTable>) {
    fun table(balanceVersion: String): DropTable = tables[if (balanceVersion == "enemy-v1-applied") "v1" else balanceVersion]
        ?: throw IllegalArgumentException("BALANCE_VERSION_UNKNOWN")

    companion object {
        fun fromResources(mapper: ObjectMapper): MaterialDropCatalog = MaterialDropCatalog(
            mapOf(
                "v1" to DropTable(JsonMaterialDropContent(resource("/v1/drops/material-drops.json"), mapper).content),
                "progression-rebalance-v1" to DropTable(JsonMaterialDropContent(resource("/progression-rebalance-v1/drops/material-drops.json"), mapper).content),
            ),
        )
        private fun resource(path: String) = requireNotNull(MaterialDropCatalog::class.java.getResourceAsStream(path)) { "drop content missing: $path" }
    }
}
