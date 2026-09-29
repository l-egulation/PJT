package com.hanjjak.gameapi

import org.junit.jupiter.api.Test
import org.springframework.boot.test.context.runner.ApplicationContextRunner
import org.springframework.scheduling.config.TaskManagementConfigUtils
import kotlin.test.assertFalse
import kotlin.test.assertTrue

class SchedulingConfigurationTest {
    private val contextRunner = ApplicationContextRunner()
        .withUserConfiguration(SchedulingConfiguration::class.java)

    @Test
    fun `scheduling is enabled by default`() {
        contextRunner.run { context ->
            assertTrue(context.containsBean(TaskManagementConfigUtils.SCHEDULED_ANNOTATION_PROCESSOR_BEAN_NAME))
        }
    }

    @Test
    fun `tests can disable scheduling`() {
        contextRunner
            .withPropertyValues("spring.task.scheduling.enabled=false")
            .run { context ->
                assertFalse(context.containsBean(TaskManagementConfigUtils.SCHEDULED_ANNOTATION_PROCESSOR_BEAN_NAME))
            }
    }
}
