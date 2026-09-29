package com.hanjjak.gameapi

import org.springframework.test.context.DynamicPropertyRegistry
import org.testcontainers.containers.PostgreSQLContainer
import java.sql.DriverManager
import java.util.UUID

object PostgresTestDatabase {
    data class Connection(
        val jdbcUrl: String,
        val username: String,
        val password: String,
    ) {
        fun register(registry: DynamicPropertyRegistry) {
            registry.add("spring.datasource.url") { jdbcUrl }
            registry.add("spring.datasource.username") { username }
            registry.add("spring.datasource.password") { password }
        }
    }
    private val managedContainer: PostgreSQLContainer<Nothing>? by lazy {
        if (System.getenv("TEST_DATABASE_URL").isNullOrBlank()) {
            PostgreSQLContainer<Nothing>("postgres:17.11-alpine").apply { start() }
        } else {
            null
        }
    }

    val connection: Connection by lazy {
        val externalUrl = System.getenv("TEST_DATABASE_URL")?.trim().orEmpty()
        if (externalUrl.isNotEmpty()) {
            Connection(
                jdbcUrl = externalUrl,
                username = requireEnvironment("TEST_DATABASE_USER"),
                password = requireEnvironment("TEST_DATABASE_PASSWORD"),
            )
        } else {
            requireNotNull(managedContainer).let { container ->
                Connection(container.jdbcUrl, container.username, container.password)
            }
        }
    }

    val workerConnection: Connection by lazy {
        schema("worker_${System.getProperty("org.gradle.test.worker", "local")}")
    }

    fun schema(prefix: String): Connection {
        val base = connection
        val normalizedPrefix = prefix.lowercase().replace(Regex("[^a-z0-9_]"), "_").take(24)
        val schema = "${normalizedPrefix}_${UUID.randomUUID().toString().replace("-", "").take(12)}"
        DriverManager.getConnection(base.jdbcUrl, base.username, base.password).use { connection ->
            connection.createStatement().use { statement -> statement.execute("create schema $schema") }
        }
        val separator = if ('?' in base.jdbcUrl) '&' else '?'
        return base.copy(jdbcUrl = "${base.jdbcUrl}${separator}currentSchema=$schema")
    }

    private fun requireEnvironment(name: String): String =
        System.getenv(name)?.takeIf(String::isNotBlank) ?: error("$name is required with TEST_DATABASE_URL")
}
