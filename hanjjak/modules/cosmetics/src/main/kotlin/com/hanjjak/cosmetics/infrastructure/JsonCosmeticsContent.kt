package com.hanjjak.cosmetics.infrastructure

import com.fasterxml.jackson.databind.JsonNode
import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.cosmetics.domain.CosmeticDefinition
import com.hanjjak.cosmetics.domain.CosmeticGrade
import com.hanjjak.cosmetics.domain.CosmeticSlot
import com.hanjjak.cosmetics.domain.CosmeticsContent
import com.hanjjak.cosmetics.domain.EffectUnit
import com.hanjjak.cosmetics.domain.SetDefinition
import com.hanjjak.cosmetics.domain.StatEffect
import java.io.InputStream

class JsonCosmeticsContent(input: InputStream, mapper: ObjectMapper) {
    val content: CosmeticsContent

    init {
        val root = mapper.readTree(input)
        require(root.path("authority").asText() == "applied") { "cosmetics content is not applied" }
        val cosmetics = root.path("cosmetics").map { node ->
            requireFields(node, "cosmeticId", "displayName", "imageUrl", "grade", "slot", "setId")
            CosmeticDefinition(
                node.path("cosmeticId").asText(), node.nullableText("displayName"), node.nullableText("imageUrl"),
                CosmeticGrade.valueOf(node.path("grade").asText()), CosmeticSlot.valueOf(node.path("slot").asText()), node.path("setId").asText(),
            )
        }
        val inactiveSetIds = root.path("sets").filter { it.has("active") && !it.path("active").asBoolean() }.map { it.path("setId").asText() }.toSet()
        val sets = root.path("sets").map { node ->
            requireFields(node, "setId", "displayName", "grade", "bannerId", "boxItemId", "members", "effects")
            val members = CosmeticSlot.entries.associateWith { slot -> node.path("members").path(slot.name).asText() }
            val effects = node.path("effects").associate { row -> row.path("star").asInt() to row.path("effects").map { it.toEffect() } }
            SetDefinition(
                node.path("setId").asText(), node.nullableText("displayName"), CosmeticGrade.valueOf(node.path("grade").asText()),
                node.nullableText("bannerId"), node.nullableText("boxItemId"), members, effects,
            )
        }
        val probabilities = root.path("gradeProbabilityMillionths").let { node -> CosmeticGrade.entries.associateWith { node.path(it.name).asInt() } }
        require(probabilities.values.sum() == 1_000_000)
        val thresholds = root.path("starThresholdsByGrade").let { node -> CosmeticGrade.entries.associateWith { grade -> node.path(grade.name).map { it.asInt() } } }
        require(thresholds.values.all { it.size == 5 && it == it.sorted().distinct() && it.first() == 1 })
        require(cosmetics.map { it.id } == (1..66).map { "cosmetic-%03d".format(it) })
        require(sets.map { it.id } == (1..11).map { "cosmetic-set-%02d".format(it) })
        require(CosmeticGrade.entries.associateWith { grade -> sets.count { it.grade == grade } } == mapOf(CosmeticGrade.NORMAL to 4, CosmeticGrade.RARE to 3, CosmeticGrade.EPIC to 3, CosmeticGrade.LEGENDARY to 1))
        for (set in sets) for ((slot, id) in set.members) require(cosmetics.singleOrNull { it.id == id && it.slot == slot && it.grade == set.grade && it.setId == set.id } != null)
        require(sets.flatMap { it.members.values }.sorted() == cosmetics.map { it.id }.sorted())
        require(sets.filter { it.bannerId != null || it.boxItemId != null }.single().let { it.grade == CosmeticGrade.LEGENDARY && it.bannerId != null && it.boxItemId != null })
        require(inactiveSetIds.none { id -> sets.single { it.id == id }.bannerId != null || sets.single { it.id == id }.boxItemId != null }) { "inactive set cannot own a banner or selector box" }
        val activeSets = sets.filterNot { it.id in inactiveSetIds }
        val activeMemberIds = activeSets.flatMap { it.members.values }.toSet()
        val activeCosmetics = cosmetics.filter { it.id in activeMemberIds }
        content = CosmeticsContent(root.path("contentVersion").asText(), root.path("singleRiceCost").asLong(), probabilities, thresholds, activeCosmetics, activeSets)
    }

    private fun requireFields(node: JsonNode, vararg names: String) = names.forEach { require(node.has(it)) { "missing cosmetics field: $it" } }
    private fun JsonNode.nullableText(name: String) = path(name).takeUnless(JsonNode::isNull)?.asText()
    private fun JsonNode.toEffect() = StatEffect(path("statId").asText(), path("value").asInt(), EffectUnit.valueOf(path("unit").asText()))
}
