package com.hanjjak.gameapi

import org.flywaydb.core.Flyway
import org.flywaydb.core.api.FlywayException
import org.junit.jupiter.api.Assertions.*
import org.junit.jupiter.api.BeforeEach
import org.junit.jupiter.api.Test
import java.sql.DriverManager
import java.util.UUID


class CosmeticSelectorBoxMigrationTest {
    private lateinit var schema: String
    private lateinit var account: UUID

    @BeforeEach
    fun prepareLegacySchema() {
        schema = "migration_${UUID.randomUUID().toString().replace("-", "")}"
        flyway("21").migrate()
        account = UUID.randomUUID()
        sql("insert into account(id,email,password_hash,state_version,created_at) values ('$account','$account@test.local','test',1,now())")
    }

    @Test
    fun `merges quantities preserving reservation and order at full capacity`() {
        instances(199)
        sql("insert into inventory_stack values ('$account','box-a',5,2,300)")
        box("box-a", 7)
        flyway().migrate()
        assertEquals(12, scalar("select quantity from inventory_stack"))
        assertEquals(2, scalar("select reserved_quantity from inventory_stack"))
        assertEquals(300, scalar("select acquired_sequence from inventory_stack"))
        assertLegacyRemoved()
    }

    @Test
    fun `new stack fits exact capacity and zero legacy quantities are ignored`() {
        instances(199)
        box("box-a", 3)
        box("box-zero", 0)
        flyway().migrate()
        assertEquals(1, scalar("select count(*) from inventory_stack"))
        assertEquals(3, scalar("select quantity from inventory_stack"))
        assertEquals(200, scalar("select acquired_sequence from inventory_stack"))
        assertLegacyRemoved()
    }

    @Test
    fun `zero existing stack requires capacity and failed migration preserves all data`() {
        instances(200)
        sql("insert into inventory_stack values ('$account','box-a',0,0,201)")
        box("box-a", 3)
        assertRejected()
        assertEquals(0, scalar("select quantity from inventory_stack"))
        assertEquals(3, scalar("select quantity from cosmetic_selector_box"))
        assertEquals(200, scalar("select count(*) from inventory_instance"))
    }

    @Test
    fun `active empty slot reservation prevents overflow and release allows retry`() {
        instances(199)
        box("box-a", 3)
        reservation("EMPTY_SLOT", "ACTIVE")
        assertRejected()
        assertEquals(3, scalar("select quantity from cosmetic_selector_box"))
        assertEquals(0, scalar("select count(*) from inventory_stack"))
        sql("update inventory_capacity_reservation set status='RELEASED',resolved_at=now()")
        flyway().migrate()
        assertEquals(3, scalar("select quantity from inventory_stack"))
        assertLegacyRemoved()
    }

    @Test
    fun `stack right reservation does not occupy an extra slot`() {
        instances(198)
        sql("insert into inventory_stack values ('$account','reserved-item',1,0,199)")
        reservation("STACK_RIGHT", "ACTIVE")
        box("box-a", 3)
        flyway().migrate()
        assertEquals(2, scalar("select count(*) from inventory_stack"))
        assertLegacyRemoved()
    }

    private fun flyway(target: String = "22"): Flyway = Flyway.configure()
        .dataSource(database.jdbcUrl, database.username, database.password)
        .schemas(schema).defaultSchema(schema).locations("classpath:db/migration").target(target).load()

    private fun sql(statement: String) {
        DriverManager.getConnection(database.jdbcUrl, database.username, database.password).use { connection ->
            connection.createStatement().use { it.execute("set search_path to $schema"); it.execute(statement) }
        }
    }

    private fun scalar(statement: String): Long = DriverManager.getConnection(database.jdbcUrl, database.username, database.password).use { connection ->
        connection.createStatement().use {
            it.execute("set search_path to $schema")
            it.executeQuery(statement).use { result -> check(result.next()); result.getLong(1) }
        }
    }

    private fun instances(count: Int) = sql("insert into inventory_instance select gen_random_uuid(),'$account','gem:1:flat_attack',false,n from generate_series(1,$count) n")
    private fun box(id: String, quantity: Int) = sql("insert into cosmetic_selector_box values ('$account','$id',$quantity)")

    private fun reservation(kind: String, status: String) {
        val challenge = UUID.randomUUID()
        sql("""
            insert into gem_dungeon_challenge(challenge_id,account_id,boss_type,stage,kst_date,status,seed,content_version,preset_snapshot,player_snapshot,battle_result,reward_gem_boxes,started_at,minimum_complete_at,expires_at)
            values ('$challenge','$account','SURVIVAL',1,current_date,'ACTIVE',1,'v1','{}','{}','{}',1,now(),now(),now()+interval '1 hour');
            insert into inventory_capacity_reservation values (gen_random_uuid(),'$challenge','$account','reserved-item','$kind','$status',now(),null)
        """.trimIndent())
    }

    private fun assertRejected() {
        val error = assertThrows(FlywayException::class.java) { flyway().migrate() }
        assertTrue(generateSequence<Throwable>(error) { it.cause }.any { it.message?.contains("would exceed the 200 inventory slots") == true })
        assertEquals(0, scalar("select count(*) from flyway_schema_history where version='22'"))
    }

    private fun assertLegacyRemoved() {
        assertEquals(0, scalar("select count(*) from information_schema.tables where table_schema='$schema' and table_name='cosmetic_selector_box'"))
        assertEquals(1, scalar("select count(*) from flyway_schema_history where version='22' and success"))
    }

    companion object {
        private val database = PostgresTestDatabase.connection
    }
}
