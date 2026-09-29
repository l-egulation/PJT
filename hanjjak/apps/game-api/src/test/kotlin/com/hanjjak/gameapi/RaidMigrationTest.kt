package com.hanjjak.gameapi

import org.flywaydb.core.Flyway
import org.junit.jupiter.api.Test
import java.sql.DriverManager
import java.sql.SQLException
import kotlin.test.assertFailsWith
import kotlin.test.assertTrue
class RaidMigrationTest {
    @Test
    fun `empty database migration creates the complete raid persistence schema`() {
        val database = PostgresTestDatabase.schema("raid_migration")
        val flyway = Flyway.configure()
            .dataSource(database.jdbcUrl, database.username, database.password)
            .schemas(database.jdbcUrl.substringAfter("currentSchema="))
            .defaultSchema(database.jdbcUrl.substringAfter("currentSchema="))
            .locations("classpath:db/migration")
            .load()

        flyway.migrate()
        DriverManager.getConnection(database.jdbcUrl, database.username, database.password).use { connection ->
            connection.createStatement().use { statement ->
                statement.executeQuery(
                    "select count(*) from information_schema.tables where table_schema=current_schema() and table_name like 'raid_%'",
                ).use { result ->
                    result.next()
                    assertTrue(result.getInt(1) >= 10, "raid migration must create all persistence tables")
                }
                for ((table, column) in listOf("battle_session" to "raid_handoff_consumed_at", "raid_attempt" to "resume_pending")) {
                    statement.executeQuery(
                        "select count(*) from information_schema.columns where table_schema=current_schema() and table_name='$table' and column_name='$column'",
                    ).use { result ->
                        result.next()
                        assertTrue(result.getInt(1) == 1, "V57 must add $table.$column")
                    }
                }
                for (index in listOf("battle_session_raid_handoff", "raid_attempt_resume_pending")) {
                    statement.executeQuery("select count(*) from pg_indexes where schemaname=current_schema() and indexname='$index'").use { result ->
                        result.next()
                        assertTrue(result.getInt(1) == 1, "V57 must add $index")
                    }
                }
            }
        }
    }

    @Test
    fun `empty migration exposes named raid constraints and indexes`() {
        val database = PostgresTestDatabase.schema("raid_constraints")
        Flyway.configure()
            .dataSource(database.jdbcUrl, database.username, database.password)
            .schemas(database.jdbcUrl.substringAfter("currentSchema="))
            .defaultSchema(database.jdbcUrl.substringAfter("currentSchema="))
            .locations("classpath:db/migration")
            .load()
            .migrate()

        DriverManager.getConnection(database.jdbcUrl, database.username, database.password).use { connection ->
            connection.createStatement().use { statement ->
                statement.executeQuery("select indexname from pg_indexes where schemaname=current_schema() and indexname like 'raid_%'")
                    .use { result ->
                        val indexes = buildSet { while (result.next()) add(result.getString(1)) }
                        assertTrue(setOf("raid_session_one_open", "raid_attempt_one_running_account", "raid_reward_claim_one_claimable").all(indexes::contains))
                    }
                statement.executeQuery("select conname from pg_constraint where connamespace=current_schema()::regnamespace and conname like 'raid_%'")
                    .use { result ->
                        val constraints = buildSet { while (result.next()) add(result.getString(1)) }
                        assertTrue(setOf("raid_attempt_reward_ordinal_ck", "raid_confirmed_result_attempt_owner_fk", "raid_confirmed_result_identity_uq", "raid_reward_slot_current_attempt_fk", "raid_reward_claim_kind_source_ck", "raid_reward_claim_auto_personal_source_fk", "raid_reward_claim_daily_rank_source_fk").all(constraints::contains))
                    }
            }
        }
    }

    @Test
    fun `migration enforces reward ordinal ownership and non-null confirmed result`() {
        val database = PostgresTestDatabase.schema("raid_constraints_behavior")
        Flyway.configure()
            .dataSource(database.jdbcUrl, database.username, database.password)
            .schemas(database.jdbcUrl.substringAfter("currentSchema="))
            .defaultSchema(database.jdbcUrl.substringAfter("currentSchema="))
            .locations("classpath:db/migration")
            .load()
            .migrate()
        val account = java.util.UUID.randomUUID()
        val session = java.util.UUID.randomUUID()
        val slot = java.util.UUID.randomUUID()
        val attempt = java.util.UUID.randomUUID()
        DriverManager.getConnection(database.jdbcUrl, database.username, database.password).use { connection ->
            connection.createStatement().use { statement ->
                statement.executeUpdate("insert into account(id,email,password_hash,state_version,created_at) values ('$account','$account@test.local','test',1,now())")
                statement.executeUpdate("insert into raid_session(id,content_version,reward_version,settles_at,status) values ('$session','raid-v1','reward-v1',now()+interval '1 hour','OPEN')")
                statement.executeUpdate("insert into raid_account_state(session_id,account_id) values ('$session','$account')")
                statement.executeUpdate("insert into raid_reward_slot(id,session_id,account_id,ordinal,status,attempts_started) values ('$slot','$session','$account',1,'ACTIVE',0)")
                assertFailsWith<SQLException> {
                    statement.executeUpdate("insert into raid_attempt(id,session_id,account_id,mode,slot_id,attempt_ordinal,status,input_snapshot,result_json,timeline_json,seed,started_at,completable_at) values ('$attempt','$session','$account','REWARD','$slot',4,'RUNNING','{}','{}','[]',1,now(),now())")
                }

                val otherAccount = java.util.UUID.randomUUID()
                statement.executeUpdate("insert into account(id,email,password_hash,state_version,created_at) values ('$otherAccount','$otherAccount@test.local','test',1,now())")
                statement.executeUpdate("insert into raid_final_rank(session_id,account_id,competitive_rank,seal_contribution,total_damage,highest_damage) values ('$session','$account',1,100,10,10)")
                statement.executeUpdate("insert into raid_final_rank(session_id,account_id,competitive_rank,seal_contribution,total_damage,highest_damage) values ('$session','$otherAccount',1,100,10,10)")
            }
        }
    }
}
