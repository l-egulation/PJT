package com.hanjjak.equipment.infrastructure

import com.fasterxml.jackson.databind.JsonNode
import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.equipment.application.EquipmentBalanceCatalog
import com.hanjjak.equipment.domain.EquipmentBalanceContent
import com.hanjjak.equipment.domain.EquipmentGrade
import com.hanjjak.equipment.domain.EquipmentPromotionContent
import java.io.InputStream

class JsonEquipmentBalanceCatalog(input: InputStream, mapper: ObjectMapper) {
    val content: EquipmentBalanceContent

    init {
        val root = mapper.readTree(input)
        val materials = EquipmentGrade.entries.associateWith { grade ->
            val gradeNode = root.path("enhancementMaterials").path(grade.name)
            if (gradeNode.isMissingNode) emptyMap() else gradeNode.fields().asSequence().associate { (key, values) ->
                key.removePrefix("M").toInt() to values.map(JsonNode::asLong)
            }
        }
        val rice = EquipmentGrade.entries.associateWith { grade ->
            root.path("enhancementRice").path(grade.name).map(JsonNode::asLong)
        }
        val promotions = EquipmentGrade.entries.associateWith { grade ->
            val node = root.path("promotion").path(grade.name)
            EquipmentPromotionContent(
                requiredStageId = node.path("requiredStageId").asText(),
                materials = node.path("materials").fields().asSequence().associate { (key, value) -> key.removePrefix("M").toInt() to value.asLong() },
                rice = node.path("rice").asLong(),
                resultGrade = EquipmentGrade.valueOf(node.path("resultGrade").asText()),
            )
        }
        content = EquipmentBalanceContent(
            authority = root.path("authority").asText(),
            contentVersion = root.path("contentVersion").asText(),
            compatibilityOnly = root.path("compatibilityOnly").asBoolean(false),
            enhancementMaterials = materials,
            enhancementRice = rice,
            promotion = promotions,
        )
    }

    companion object {
        fun fromResources(mapper: ObjectMapper): EquipmentBalanceCatalog = EquipmentBalanceCatalog(
            mapOf(
                "v1" to JsonEquipmentBalanceCatalog(resource("/v1/equipment/equipment.json"), mapper).content,
                "progression-rebalance-v1" to JsonEquipmentBalanceCatalog(resource("/progression-rebalance-v1/equipment/equipment.json"), mapper).content,
            ),
        )

        private fun resource(path: String) = requireNotNull(JsonEquipmentBalanceCatalog::class.java.getResourceAsStream(path)) {
            "equipment content missing: $path"
        }
    }
}
