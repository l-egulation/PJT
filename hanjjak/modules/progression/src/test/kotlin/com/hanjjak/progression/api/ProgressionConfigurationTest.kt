package com.hanjjak.progression.api

import com.hanjjak.progression.application.ProgressionRepository
import com.hanjjak.progression.infrastructure.JdbcProgressionRepository
import kotlin.test.Test
import kotlin.test.assertIs
import org.springframework.jdbc.core.JdbcTemplate
import org.springframework.jdbc.core.simple.JdbcClient

class ProgressionConfigurationTest {
    @Test
    fun `configuration provides the progression repository required by reward service`() {
        val repository: ProgressionRepository = ProgressionConfiguration()
            .progressionRepository(JdbcClient.create(JdbcTemplate()))

        assertIs<JdbcProgressionRepository>(repository)
    }
}
