package com.hanjjak.admin.application

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.cosmetics.domain.CosmeticDefinition
import com.hanjjak.cosmetics.domain.CosmeticGrade
import com.hanjjak.cosmetics.domain.CosmeticSlot
import com.hanjjak.cosmetics.domain.CosmeticsContent
import com.hanjjak.equipment.domain.EquipmentGrade
import com.hanjjak.equipment.domain.EquipmentSlot
import com.hanjjak.gems.domain.GemOption
import com.hanjjak.inventory.application.InventoryRepository
import com.hanjjak.inventory.application.ItemCatalog
import com.hanjjak.inventory.domain.InventoryInstance
import com.hanjjak.inventory.domain.InventoryStack
import com.hanjjak.inventory.infrastructure.StaticItemCatalog
import com.hanjjak.stage.application.InMemoryStageCatalog
import com.hanjjak.stage.domain.BossType
import com.hanjjak.stage.domain.StageDefinition
import com.hanjjak.stage.domain.StageId
import org.h2.jdbcx.JdbcDataSource
import org.springframework.jdbc.core.simple.JdbcClient
import java.time.Clock
import java.time.Instant
import java.time.ZoneOffset
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertNotNull

class AdminUserManagementServiceTest {
    private val accountId = UUID.randomUUID()
    private val operatorId = UUID.randomUUID()
    private val jdbc = JdbcClient.create(JdbcDataSource().apply { setURL("jdbc:h2:mem:${System.nanoTime()};MODE=PostgreSQL;NON_KEYWORDS=VALUE;DB_CLOSE_DELAY=-1") })
    private val inventory = MemoryInventory(jdbc, StaticItemCatalog())
    private val content = CosmeticsContent(
        "test", 1, CosmeticGrade.entries.associateWith { 250_000 }, CosmeticGrade.entries.associateWith { listOf(1, 2, 3, 4, 5) },
        listOf(CosmeticDefinition("cosmetic-001", "테스트 치장", null, CosmeticGrade.NORMAL, CosmeticSlot.HEAD, "set-1")), emptyList(),
    )
    private val stages = InMemoryStageCatalog((1..5).map { StageDefinition(StageId(1, it), 1, 0, 1, 1, 1, 1, BossType.STANDARD) })
    private val service: AdminUserManagementService

    init {
        jdbc.sql("""
            create table account(id uuid primary key,state_version bigint not null,deleted_at timestamp with time zone);
            create table character(account_id uuid primary key,level integer not null,experience bigint not null,cosmetics_unlocked boolean not null default false);
            create table wallet_balance(account_id uuid primary key,balance bigint not null,updated_at timestamp with time zone);
            create table wallet_ledger(ledger_id uuid primary key,account_id uuid,delta bigint,balance_after bigint,source_type varchar,source_id uuid,created_at timestamp with time zone);
            create table account_runtime_state(account_id uuid primary key,current_stage_id varchar);
            create table stage_progress(account_id uuid,stage_id varchar,unlocked boolean,highest_clear_count bigint,content_version varchar,primary key(account_id,stage_id));
            create table inventory_stack(account_id uuid,item_id varchar,quantity bigint,reserved_quantity bigint,acquired_sequence bigint,primary key(account_id,item_id));
            create table inventory_instance(instance_id uuid primary key,account_id uuid,item_id varchar,reserved_for_sale boolean,acquired_sequence bigint,stack_key varchar,inventory_slot_id uuid,locked boolean);
            create table inventory_capacity_reservation(account_id uuid,item_id varchar,status varchar);
            create table gem_instance(gem_id uuid primary key,account_id uuid,level integer,option varchar,value integer,locked boolean,content_version varchar,created_at timestamp with time zone);
            create table gem_loadout(account_id uuid,gem_id uuid);
            create table cosmetic_collection_state(account_id uuid,cosmetic_id varchar,registered_quantity integer,unregistered_quantity integer,reserved_quantity integer,primary key(account_id,cosmetic_id));
            create table cosmetic_equipment(account_id uuid,cosmetic_id varchar);
            create table equipment_slot_state(account_id uuid,slot varchar,grade varchar,enhancement_level integer,unlocked_at timestamp with time zone,updated_at timestamp with time zone,primary key(account_id,slot));
            create table game_session(account_id uuid,status varchar,closed_at timestamp with time zone);
            create table battle_session(account_id uuid,status varchar,closed_at timestamp with time zone);
            create table account_active_session(account_id uuid);
            create table admin_user_command(operator_id uuid,idempotency_key uuid,account_id uuid,action varchar,fingerprint varchar,result_json varchar,created_at timestamp with time zone,primary key(operator_id,idempotency_key));
        """.trimIndent()).update()
        jdbc.sql("insert into account values (:account,1,null)").param("account", accountId).update()
        jdbc.sql("insert into character(account_id,level,experience) values (:account,1,0)").param("account", accountId).update()
        jdbc.sql("insert into wallet_balance values (:account,0,:now)").params(mapOf("account" to accountId, "now" to Instant.now())).update()
        service = AdminUserManagementService(jdbc, inventory, inventory.catalog, stages, content, ObjectMapper().findAndRegisterModules(), Clock.fixed(Instant.parse("2026-09-11T00:00:00Z"), ZoneOffset.UTC))
    }

    @Test
    fun `admin adjustments preserve domain boundaries and return full state`() {
        service.adjustProgression(operatorId, UUID.randomUUID(), accountId, 10, 50_000, "성장 복구")
        service.adjustRice(operatorId, UUID.randomUUID(), accountId, AdminUserManagementService.NumericMode.ADD, 5_000, "쌀 보상")
        service.adjustItem(operatorId, UUID.randomUUID(), accountId, "POTATO_M1", AdminUserManagementService.NumericMode.SET, 77, "재료 복구")
        service.unlockStages(operatorId, UUID.randomUUID(), accountId, "stage.01-05", "진행 복구")
        service.adjustCosmetic(operatorId, UUID.randomUUID(), accountId, "cosmetic-001", 1, 2, "치장 복구")
        service.adjustEquipment(operatorId, UUID.randomUUID(), accountId, EquipmentSlot.WEAPON, EquipmentGrade.RARE, 12, "장비 복구")
        val state = service.adjustGem(operatorId, UUID.randomUUID(), accountId, 1, GemOption.FLAT_ATTACK, 2, "보석 복구").after

        assertEquals(10, state.level)
        assertEquals(50_000, state.experience)
        assertEquals(5_000, state.rice)
        assertEquals("stage.01-05", state.highestUnlockedStageId)
        assertEquals(77, state.items.first { it.itemId == "POTATO_M1" }.quantity)
        assertEquals(2, state.items.first { it.itemId == "gem:1:flat_attack" }.quantity)
        assertEquals(2, state.cosmetics.single().unregisteredQuantity)
        assertEquals(12, state.equipment.single().enhancementLevel)
    }

    @Test
    fun `same idempotency key replays and reserved inventory cannot be deleted`() {
        val key = UUID.randomUUID()
        val first = service.adjustRice(operatorId, key, accountId, AdminUserManagementService.NumericMode.ADD, 100, "보상 지급")
        val replay = service.adjustRice(operatorId, key, accountId, AdminUserManagementService.NumericMode.ADD, 100, "보상 지급")
        inventory.saveStack(InventoryStack(accountId, "POTATO_M1", 10, 7, 1))

        val error = assertFailsWith<IllegalArgumentException> {
            service.adjustItem(operatorId, UUID.randomUUID(), accountId, "POTATO_M1", AdminUserManagementService.NumericMode.SET, 6, "잘못된 차감")
        }

        assertEquals(100, first.after.rice)
        assertEquals(true, replay.replayed)
        assertEquals("ADMIN_ITEM_BELOW_RESERVED_QUANTITY", error.message)
    }

    private class MemoryInventory(private val jdbc: JdbcClient, val catalog: ItemCatalog) : InventoryRepository {
        override fun lockAccount(accountId: UUID) = Unit
        override fun stateVersion(accountId: UUID) = jdbc.sql("select state_version from account where id=:account").param("account", accountId).query(Long::class.java).single()
        override fun incrementStateVersion(accountId: UUID): Long { jdbc.sql("update account set state_version=state_version+1 where id=:account").param("account", accountId).update(); return stateVersion(accountId) }
        override fun stacks(accountId: UUID) = jdbc.sql("select account_id,item_id,quantity,reserved_quantity,acquired_sequence from inventory_stack where account_id=:account and quantity>0").param("account", accountId).query(InventoryStack::class.java).list()
        override fun instances(accountId: UUID) = jdbc.sql("select instance_id,account_id,item_id,reserved_for_sale,acquired_sequence,stack_key,inventory_slot_id,locked from inventory_instance where account_id=:account").param("account", accountId).query(InventoryInstance::class.java).list()

        override fun saveStack(stack: InventoryStack) { jdbc.sql("merge into inventory_stack(account_id,item_id,quantity,reserved_quantity,acquired_sequence) key(account_id,item_id) values (:account,:item,:quantity,:reserved,:sequence)").params(mapOf("account" to stack.accountId,"item" to stack.itemId,"quantity" to stack.quantity,"reserved" to stack.reservedQuantity,"sequence" to stack.acquiredSequence)).update() }
        override fun addInstance(instance: InventoryInstance) { jdbc.sql("insert into inventory_instance values (:id,:account,:item,:reserved,:sequence,:stack,:slot,:locked)").params(mapOf("id" to instance.instanceId,"account" to instance.accountId,"item" to instance.itemId,"reserved" to instance.reservedForSale,"sequence" to instance.acquiredSequence,"stack" to instance.stackKey,"slot" to instance.inventorySlotId,"locked" to instance.locked)).update() }
        override fun removeInstances(accountId: UUID, instanceIds: List<UUID>) { if (instanceIds.isNotEmpty()) jdbc.sql("delete from inventory_instance where account_id=:account and instance_id in (:ids)").params(mapOf("account" to accountId,"ids" to instanceIds)).update() }
        override fun setInstancesReserved(accountId: UUID, instanceIds: List<UUID>, reserved: Boolean) {
            if (instanceIds.isEmpty()) return
            jdbc.sql("update inventory_instance set reserved_for_sale=:reserved where account_id=:account and instance_id in (:ids)")
                .params(mapOf("reserved" to reserved, "account" to accountId, "ids" to instanceIds)).update()
        }
        override fun transferInstances(fromAccountId: UUID, toAccountId: UUID, instanceIds: List<UUID>, reserved: Boolean) {
            if (instanceIds.isEmpty()) return
            jdbc.sql("update inventory_instance set account_id=:to,reserved_for_sale=:reserved where account_id=:from and instance_id in (:ids)")
                .params(mapOf("from" to fromAccountId, "to" to toAccountId, "reserved" to reserved, "ids" to instanceIds)).update()
        }
        override fun hasCapacityReservation(accountId: UUID, itemId: String) = false
        override fun lockStack(accountId: UUID, itemId: String) = jdbc.sql("select account_id,item_id,quantity,reserved_quantity,acquired_sequence from inventory_stack where account_id=:account and item_id=:item").params(mapOf("account" to accountId,"item" to itemId)).query(InventoryStack::class.java).optional().orElse(null)
        override fun usedSlotCount(accountId: UUID) = stacks(accountId).size + instances(accountId).map { it.inventorySlotId ?: it.instanceId }.distinct().size
        override fun nextAcquiredSequence(accountId: UUID) = (stacks(accountId).maxOfOrNull { it.acquiredSequence } ?: 0).coerceAtLeast(instances(accountId).maxOfOrNull { it.acquiredSequence } ?: 0) + 1
        override fun rewardCommand(accountId: UUID, idempotencyKey: UUID) = null
        override fun saveRewardCommand(accountId: UUID, idempotencyKey: UUID, fingerprint: String, resultJson: String) = Unit
    }
}
