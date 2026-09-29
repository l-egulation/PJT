package com.hanjjak.stage.infrastructure

import com.hanjjak.stage.application.InMemoryStageCatalog
import com.hanjjak.stage.domain.BossType
import com.hanjjak.stage.domain.StageDefinition
import com.hanjjak.stage.domain.StageId
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith

class VersionedStageCatalogTest {
    @Test
    fun `versioned catalog selects by qualified version and fails closed`() {
        val stage = StageDefinition(StageId(1, 1), 1, 0, 1, 1, 1, 1, BossType.STANDARD)
        val catalog = VersionedStageCatalog(
            mapOf(
                "v1" to InMemoryStageCatalog(listOf(stage)),
                "progression-rebalance-v1" to InMemoryStageCatalog(listOf(stage)),
            ),
        )
        assertEquals(stage, catalog.require("progression-rebalance-v1", "stage.01-01"))
        assertFailsWith<IllegalArgumentException> {
            catalog.require("unknown-version", "stage.01-01")
        }
    }
}
