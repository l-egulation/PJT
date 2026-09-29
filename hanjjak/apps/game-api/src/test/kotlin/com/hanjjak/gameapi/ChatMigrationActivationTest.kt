package com.hanjjak.gameapi

import java.nio.file.Files
import java.nio.file.Path
import kotlin.test.Test
import kotlin.test.assertEquals

class ChatMigrationActivationTest {
    @Test
    fun `chat migration uses the next free version after buy orders`() {
        val migrations = Path.of("src/main/resources/db/migration")
        val versions = Files.list(migrations).use { stream ->
            stream.filter { it.fileName.toString().endsWith(".sql") }
                .map { it.fileName.toString().substringAfter('V').substringBefore('_').toInt() }
                .toList()
        }
        assertEquals(versions.size, versions.toSet().size, "Flyway migration versions must be unique")
        assertEquals(true, Files.exists(migrations.resolve("V42__chat_community.sql")))
    }
}
