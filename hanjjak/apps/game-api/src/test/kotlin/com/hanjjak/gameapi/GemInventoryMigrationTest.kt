package com.hanjjak.gameapi

import org.flywaydb.core.Flyway
import org.flywaydb.core.api.FlywayException
import org.junit.jupiter.api.Assertions.*
import org.junit.jupiter.api.BeforeEach
import org.junit.jupiter.api.Test
import java.sql.DriverManager
import java.util.UUID

import java.sql.SQLException

class GemInventoryMigrationTest {
    private lateinit var schema: String
    private lateinit var account: UUID
    private lateinit var gem: UUID

    @BeforeEach
    fun prepare() {
        schema = "gems_${UUID.randomUUID().toString().replace("-", "")}"
        flyway("22").migrate()
        account = UUID.randomUUID()
        gem = UUID.randomUUID()
        sql("insert into account(id,email,password_hash,state_version,created_at) values ('$account','$account@test.local','test',1,now())")
        sql("insert into gem_instance(gem_id,account_id,level,option,value,locked,content_version) values ('$gem','$account',1,'FLAT_ATTACK',50,false,'test')")
    }

    @Test
    fun `backfill stacks identical attributes but retains different values and locks`() {
        sql("insert into gem_instance(gem_id,account_id,level,option,value,locked,content_version) select gen_random_uuid(),'$account',1,'FLAT_ATTACK',50,true,'test' from generate_series(1,99)")
        sql("insert into gem_instance(gem_id,account_id,level,option,value,locked,content_version) values (gen_random_uuid(),'$account',1,'FLAT_ATTACK',51,false,'test')")
        flyway().migrate()
        assertEquals(101, scalar("select count(*) from inventory_instance"))
        assertEquals(2, scalar("select count(distinct stack_key) from inventory_instance"))
        assertEquals(3, scalar("select sum((n-1)/99+1) from (select count(*) n from inventory_instance group by item_id,stack_key) groups"))
        assertEquals(99, scalar("select count(*) from gem_instance where locked"))
        assertEquals(99, scalar("select count(*) from inventory_instance where locked"))
        assertEquals(3, scalar("select count(distinct inventory_slot_id) from inventory_instance"))
        assertEquals(0, scalar("select count(*) from (select inventory_slot_id from inventory_instance group by inventory_slot_id having count(distinct locked)>1 or count(*)>99) invalid"))
    }

    @Test
    fun `escrow migration conserves active stock without returning expired stock twice`() {
        flyway("23").migrate()
        sql("insert into inventory_stack values ('$account','POTATO_M1',100,30,1)")
        for (status in listOf("ACTIVE", "EXPIRED", "CANCELLED")) {
            sql("insert into market_listing values (gen_random_uuid(),'$account','POTATO_M1',30,30,10,'$status',now(),now(),now()+interval '1 hour')")
        }
        flyway().migrate()
        assertEquals(70, scalar("select quantity from inventory_stack"))
        assertEquals(0, scalar("select reserved_quantity from inventory_stack"))
        assertEquals(30, scalar("select sum(remaining_quantity) from market_listing"))
        flyway().migrate()
        assertEquals(70, scalar("select quantity from inventory_stack"))
    }

    @Test
    fun `escrow mismatch fails without modifying stock`() {
        flyway("23").migrate()
        sql("insert into inventory_stack values ('$account','POTATO_M1',100,30,1)")
        assertThrows(FlywayException::class.java) { flyway().migrate() }
        assertEquals(100, scalar("select quantity from inventory_stack"))
        assertEquals(30, scalar("select reserved_quantity from inventory_stack"))
        assertEquals(0, scalar("select count(*) from flyway_schema_history where version='25'"))
    }

    @Test
    fun `separating legacy gem locks rejects capacity overflow atomically`() {
        sql("insert into gem_instance(gem_id,account_id,level,option,value,locked,content_version) values (gen_random_uuid(),'$account',1,'FLAT_ATTACK',50,true,'test')")
        fill(199)
        flyway("25").migrate()
        assertThrows(FlywayException::class.java) { flyway().migrate() }
        assertEquals(1, scalar("select count(*) from gem_instance where locked"))
        assertEquals(0, scalar("select count(*) from flyway_schema_history where version='26'"))
    }

    @Test
    fun `material expansion overflow rolls back schema and preserves quantities`() {
        sql("insert into inventory_stack values ('$account','POTATO_M1',199801,0,1)")
        assertThrows(FlywayException::class.java) { flyway().migrate() }
        assertEquals(199801, scalar("select quantity from inventory_stack"))
        assertEquals(0, scalar("select count(*) from flyway_schema_history where version='23'"))
    }

    @Test
    fun `backfill preserves gem identity and appends after existing acquisition sequence`() {
        sql("insert into inventory_stack values ('$account','GEM_BOX',1,0,300)")
        flyway().migrate()
        assertEquals(1, scalar("select count(*) from inventory_instance where instance_id='$gem' and account_id='$account' and item_id='gem:1:flat_attack'"))
        assertEquals(301, scalar("select acquired_sequence from inventory_instance"))
        flyway().migrate()
        assertEquals(1, scalar("select count(*) from inventory_instance"))
    }

    @Test
    fun `already mapped gem does not consume a second slot at capacity`() {
        sql("insert into inventory_instance values ('$gem','$account','gem:1:flat_attack',true,1)")
        fill(199)
        flyway().migrate()
        assertEquals(200, scalar("select count(*) from inventory_instance"))
        assertEquals(1, scalar("select count(*) from inventory_instance where instance_id='$gem' and reserved_for_sale"))
    }

    @Test
    fun `overflow rejects transaction without losing gems or applying migration`() {
        fill(200)
        assertThrows(FlywayException::class.java) { flyway().migrate() }
        assertEquals(200, scalar("select count(*) from inventory_instance"))
        assertEquals(1, scalar("select count(*) from gem_instance"))
        assertEquals(0, scalar("select count(*) from flyway_schema_history where version='23'"))
    }

    @Test
    fun `mismatching existing identity fails without overwriting it`() {
        sql("insert into inventory_instance values ('$gem','$account','gem:2:flat_attack',false,1)")
        assertThrows(FlywayException::class.java) { flyway().migrate() }
        assertEquals(1, scalar("select count(*) from inventory_instance where item_id='gem:2:flat_attack'"))
        assertEquals(0, scalar("select count(*) from flyway_schema_history where version='23'"))
    }
    @Test
    fun `ticket policy migration preserves unrelated checks that mention tickets`() {
        flyway("51").migrate()
        sql("alter table gem_account_state add constraint gem_ticket_parity_check check (tickets = tickets)")

        flyway().migrate()

        assertEquals(1, scalar("select count(*) from pg_constraint where conrelid='gem_account_state'::regclass and conname='gem_ticket_parity_check'"))
        assertThrows(SQLException::class.java) { sql("insert into gem_account_state(account_id,tickets,last_ticket_at) values ('$account',-1,now())") }
    }


    private fun fill(count: Int) = sql("insert into inventory_instance select gen_random_uuid(),'$account','gem:1:flat_attack',false,n+1 from generate_series(1,$count) n")
    private fun flyway(target: String = "latest") = Flyway.configure()
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
    companion object {
        private val database = PostgresTestDatabase.connection
    }
}
