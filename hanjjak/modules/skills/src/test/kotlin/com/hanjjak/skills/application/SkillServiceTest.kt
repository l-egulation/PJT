package com.hanjjak.skills.application

import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertNotNull

class SkillServiceTest {
    @Test
    fun `first-clear unlock API exposes explicit version ownership`() {
        val unlock: (SkillService, UUID, String, Boolean) -> Boolean = SkillService::unlockFromFirstClear
        assertNotNull(unlock)
    }
}
