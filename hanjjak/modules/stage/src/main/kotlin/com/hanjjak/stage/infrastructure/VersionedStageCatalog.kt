package com.hanjjak.stage.infrastructure

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.stage.application.StageCatalog
import com.hanjjak.stage.domain.StageDefinition

/** Selects immutable stage definitions by the account/session balance version. */
class VersionedStageCatalog(private val catalogs: Map<String, StageCatalog>) : StageCatalog {
    override fun all(): List<StageDefinition> = all("enemy-v1-applied")

    override fun all(balanceVersion: String): List<StageDefinition> = catalog(balanceVersion).all()

    override fun require(balanceVersion: String, stageKey: String): StageDefinition = catalog(balanceVersion).require(stageKey)

    private fun catalog(balanceVersion: String): StageCatalog = catalogs[normalize(balanceVersion)]
        ?: throw IllegalArgumentException("BALANCE_VERSION_UNKNOWN")

    private fun normalize(balanceVersion: String): String = when (balanceVersion) {
        "enemy-v1-applied" -> "v1"
        else -> balanceVersion
    }

    companion object {
        fun fromResources(mapper: ObjectMapper): VersionedStageCatalog = VersionedStageCatalog(
            mapOf(
                "v1" to JsonStageCatalog(resource("/v1/stages/stages.json"), mapper),
                "progression-rebalance-v1" to JsonStageCatalog(resource("/progression-rebalance-v1/stages/stages.json"), mapper),
            ),
        )

        private fun resource(path: String) = requireNotNull(VersionedStageCatalog::class.java.getResourceAsStream(path)) {
            "stage content missing: $path"
        }
    }
}
