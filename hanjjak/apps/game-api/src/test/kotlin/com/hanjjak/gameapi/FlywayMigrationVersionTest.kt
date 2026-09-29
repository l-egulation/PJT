package com.hanjjak.gameapi

import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test
import org.springframework.core.io.support.PathMatchingResourcePatternResolver

class FlywayMigrationVersionTest {
    @Test
    fun `migration versions are unique on the runtime classpath`() {
        val migrationPattern = Regex("^V([^_]+)__.+\\.sql$")
        val duplicates = PathMatchingResourcePatternResolver()
            .getResources("classpath*:db/migration/V*__*.sql")
            .mapNotNull { resource ->
                val filename = resource.filename ?: return@mapNotNull null
                migrationPattern.matchEntire(filename)?.groupValues?.get(1)?.let { version -> version to filename }
            }
            .groupBy({ it.first }, { it.second })
            .filterValues { filenames -> filenames.size > 1 }

        assertTrue(duplicates.isEmpty(), "Duplicate Flyway migration versions: $duplicates")
    }
    @Test
    fun `main and raid migration versions stay distinct`() {
        val filenames = PathMatchingResourcePatternResolver()
            .getResources("classpath*:db/migration/V*__*.sql")
            .mapNotNull { it.filename }
            .toSet()

        assertTrue(
            setOf(
                "V53__account_balance_version.sql",
                "V54__first_clear_reward_inbox.sql",
                "V56__daily_seal_raid.sql",
                "V57__raid_battle_handoff.sql",
                "V58__raid_settlement_phases.sql",
            ).all(filenames::contains),
            "Expected main-owned V53/V54 and raid V56/V57/V58 migrations, found: $filenames",
        )
    }

    @Test
    fun `daily seal raid migration reserves version 56`() {
        val migrationPattern = Regex("^V([^_]+)__daily_seal_raid\\.sql$")
        val migration = PathMatchingResourcePatternResolver()
            .getResources("classpath*:db/migration/V*__daily_seal_raid.sql")
            .mapNotNull { resource -> resource.filename?.let { filename -> migrationPattern.matchEntire(filename)?.groupValues?.get(1) } }
        assertTrue(migration.contains("56"), "Expected V56 daily seal raid migration, found: $migration")
    }

    @Test
    fun `raid settlement migration reserves version 58`() {
        val migrationPattern = Regex("^V([^_]+)__raid_settlement_phases\\.sql$")
        val migration = PathMatchingResourcePatternResolver()
            .getResources("classpath*:db/migration/V*__raid_settlement_phases.sql")
            .mapNotNull { resource -> resource.filename?.let { filename -> migrationPattern.matchEntire(filename)?.groupValues?.get(1) } }
        assertTrue(migration.contains("58"), "Expected V58 raid settlement migration, found: $migration")
    }

    @Test
    fun `raid battle handoff migration reserves version 57`() {
        val migrationPattern = Regex("^V([^_]+)__raid_battle_handoff\\.sql$")
        val migration = PathMatchingResourcePatternResolver()
            .getResources("classpath*:db/migration/V*__raid_battle_handoff.sql")
            .mapNotNull { resource -> resource.filename?.let { filename -> migrationPattern.matchEntire(filename)?.groupValues?.get(1) } }

        assertTrue(migration.contains("57"), "Expected V57 raid battle handoff migration, found: $migration")
    }
}
