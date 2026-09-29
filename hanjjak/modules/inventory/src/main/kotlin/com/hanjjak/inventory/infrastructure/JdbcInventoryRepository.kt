package com.hanjjak.inventory.infrastructure

import com.hanjjak.inventory.application.InventoryRepository
import com.hanjjak.inventory.application.ItemCatalog
import com.hanjjak.inventory.domain.InventorySlots
import com.hanjjak.inventory.domain.InventoryInstance
import com.hanjjak.inventory.domain.InventoryStack
import org.springframework.jdbc.core.simple.JdbcClient
import java.util.UUID

class JdbcInventoryRepository(private val jdbc: JdbcClient, private val catalog: ItemCatalog) : InventoryRepository {
    override fun lockAccount(accountId: UUID) {
        jdbc.sql("select id from account where id=:account for update").param("account", accountId).query(UUID::class.java).single()
    }

    override fun stateVersion(accountId: UUID): Long = jdbc.sql("select state_version from account where id=:account")
        .param("account", accountId).query(Long::class.java).single()

    override fun incrementStateVersion(accountId: UUID): Long = jdbc.sql("update account set state_version=state_version+1 where id=:account returning state_version")
        .param("account", accountId).query(Long::class.java).single()

    override fun incrementStateVersions(accountIds: Collection<UUID>) {
        val ids = accountIds.distinct().sorted()
        if (ids.isEmpty()) return
        jdbc.sql("update account set state_version=state_version+1 where id in (:accounts)")
            .param("accounts", ids)
            .update()
    }

    override fun stacks(accountId: UUID): List<InventoryStack> = jdbc.sql("select account_id,item_id,quantity,reserved_quantity,acquired_sequence from inventory_stack where account_id=:account and quantity>0")
        .param("account", accountId).query(InventoryStack::class.java).list()

    override fun instances(accountId: UUID): List<InventoryInstance> = jdbc.sql("select instance_id,account_id,item_id,reserved_for_sale,acquired_sequence,stack_key,inventory_slot_id,locked from inventory_instance where account_id=:account")
        .param("account", accountId).query(InventoryInstance::class.java).list()

    override fun saveStack(stack: InventoryStack) {
        jdbc.sql("insert into inventory_stack(account_id,item_id,quantity,reserved_quantity,acquired_sequence) values (:account,:item,:quantity,:reserved,:sequence) on conflict(account_id,item_id) do update set quantity=excluded.quantity,reserved_quantity=excluded.reserved_quantity,acquired_sequence=excluded.acquired_sequence")
            .params(mapOf("account" to stack.accountId, "item" to stack.itemId, "quantity" to stack.quantity, "reserved" to stack.reservedQuantity, "sequence" to stack.acquiredSequence)).update()
    }

    override fun saveStacks(stacks: List<InventoryStack>) {
        if (stacks.isEmpty()) return
        val params = mutableMapOf<String, Any>()
        val values = stacks.mapIndexed { index, stack ->
            params["account$index"] = stack.accountId
            params["item$index"] = stack.itemId
            params["quantity$index"] = stack.quantity
            params["reserved$index"] = stack.reservedQuantity
            params["sequence$index"] = stack.acquiredSequence
            "(cast(:account$index as uuid), cast(:item$index as varchar), cast(:quantity$index as bigint), cast(:reserved$index as bigint), cast(:sequence$index as bigint))"
        }.joinToString(",")
        jdbc.sql("insert into inventory_stack(account_id,item_id,quantity,reserved_quantity,acquired_sequence) values $values on conflict(account_id,item_id) do update set quantity=excluded.quantity,reserved_quantity=excluded.reserved_quantity,acquired_sequence=excluded.acquired_sequence")
            .params(params)
            .update()
    }

    override fun addInstance(instance: InventoryInstance) {
        jdbc.sql("insert into inventory_instance(instance_id,account_id,item_id,reserved_for_sale,acquired_sequence,stack_key,inventory_slot_id,locked) values (:instance,:account,:item,:reserved,:sequence,:stackKey,:slot,:locked)")
            .params(mapOf("instance" to instance.instanceId, "account" to instance.accountId, "item" to instance.itemId, "reserved" to instance.reservedForSale, "sequence" to instance.acquiredSequence, "stackKey" to instance.stackKey, "slot" to instance.inventorySlotId, "locked" to instance.locked)).update()
    }

    override fun lockStack(accountId: UUID, itemId: String): InventoryStack? = jdbc.sql("select account_id,item_id,quantity,reserved_quantity,acquired_sequence from inventory_stack where account_id=:account and item_id=:item for update")
        .param("account", accountId).param("item", itemId)
        .query(InventoryStack::class.java)
        .optional()
        .orElse(null)

    override fun removeInstances(accountId: UUID, instanceIds: List<UUID>) {
        if (instanceIds.isEmpty()) return
        jdbc.sql("delete from inventory_instance where account_id=:account and instance_id in (:ids)")
            .params(mapOf("account" to accountId, "ids" to instanceIds)).update()
    }

    override fun setInstancesReserved(accountId: UUID, instanceIds: List<UUID>, reserved: Boolean) {
        if (instanceIds.isEmpty()) return
        val updated = jdbc.sql("update inventory_instance set reserved_for_sale=:reserved where account_id=:account and instance_id in (:ids)")
            .params(mapOf("reserved" to reserved, "account" to accountId, "ids" to instanceIds)).update()
        require(updated == instanceIds.distinct().size) { "INSTANCE_NOT_AVAILABLE" }
    }

    override fun transferInstances(fromAccountId: UUID, toAccountId: UUID, instanceIds: List<UUID>, reserved: Boolean) {
        if (instanceIds.isEmpty()) return
        val updated = jdbc.sql("update inventory_instance set account_id=:to,reserved_for_sale=:reserved,acquired_sequence=case when :from=:to then acquired_sequence else (select coalesce(max(acquired_sequence),0)+1 from inventory_instance where account_id=:to) end where account_id=:from and instance_id in (:ids)")
            .params(mapOf("from" to fromAccountId, "to" to toAccountId, "reserved" to reserved, "ids" to instanceIds)).update()
        require(updated == instanceIds.distinct().size) { "INSTANCE_NOT_AVAILABLE" }
        if (fromAccountId != toAccountId) {
            jdbc.sql("update gem_instance set account_id=:to where account_id=:from and gem_id in (:ids)")
                .params(mapOf("from" to fromAccountId, "to" to toAccountId, "ids" to instanceIds)).update()
        }
    }

    override fun hasCapacityReservation(accountId: UUID, itemId: String): Boolean = jdbc.sql(
        "select exists(select 1 from inventory_capacity_reservation where account_id=:account and item_id=:item and status='ACTIVE')",
    ).params(mapOf("account" to accountId, "item" to itemId)).query(Boolean::class.java).single()

    override fun capacityReservedItemIds(accountId: UUID, itemIds: Collection<String>): Set<String> {
        val distinctItemIds = itemIds.distinct()
        if (distinctItemIds.isEmpty()) return emptySet()
        return jdbc.sql(
            "select distinct item_id from inventory_capacity_reservation " +
                "where account_id=:account and item_id in (:items) and status='ACTIVE'",
        ).params(mapOf("account" to accountId, "items" to distinctItemIds)).query(String::class.java).list().toSet()
    }

    override fun lockStacks(keys: List<InventoryRepository.StackKey>): Map<InventoryRepository.StackKey, InventoryStack> {
        val distinctKeys = keys.distinct()
        if (distinctKeys.isEmpty()) return emptyMap()
        val params = mutableMapOf<String, Any>()
        val tuples = distinctKeys.mapIndexed { index, key ->
            params["account$index"] = key.accountId
            params["item$index"] = key.itemId
            "(cast(:account$index as uuid), cast(:item$index as varchar))"
        }.joinToString(",")
        return jdbc.sql("select account_id,item_id,quantity,reserved_quantity,acquired_sequence from inventory_stack where (account_id,item_id) in ($tuples) order by account_id,item_id for update")
            .params(params)
            .query(InventoryStack::class.java)
            .list()
            .associateBy { InventoryRepository.StackKey(it.accountId, it.itemId) }
    }

    override fun usedSlotCount(accountId: UUID): Int {
        val stacks = stacks(accountId).sumOf { InventorySlots.stackSlots(it.quantity, catalog.require(it.itemId)) }
        val instances = InventorySlots.instanceSlots(instances(accountId))
        val capacityReservations = jdbc.sql("select count(distinct r.item_id) from inventory_capacity_reservation r where r.account_id=:account and r.status='ACTIVE' and not exists (select 1 from inventory_stack s where s.account_id=r.account_id and s.item_id=r.item_id and s.quantity>0)")
            .param("account", accountId)
            .query(Int::class.java)
            .single()
        return Math.toIntExact(stacks + instances + capacityReservations)
    }

    override fun stackGrantSnapshot(accountId: UUID, itemIds: Collection<String>): InventoryRepository.StackGrantSnapshot =
        stackGrantSnapshots(mapOf(accountId to itemIds)).getValue(accountId)

    override fun stackGrantSnapshots(itemIdsByAccount: Map<UUID, Collection<String>>): Map<UUID, InventoryRepository.StackGrantSnapshot> {
        if (itemIdsByAccount.isEmpty()) return emptyMap()
        val accountIds = itemIdsByAccount.keys.toList()
        val stacksByAccount = jdbc.sql(
            "select account_id,item_id,quantity,reserved_quantity,acquired_sequence from inventory_stack " +
                "where account_id in (:accounts) and quantity>0",
        ).param("accounts", accountIds).query(InventoryStack::class.java).list().groupBy { it.accountId }
        val instancesByAccount = jdbc.sql(
            "select instance_id,account_id,item_id,reserved_for_sale,acquired_sequence,stack_key,inventory_slot_id,locked " +
                "from inventory_instance where account_id in (:accounts)",
        ).param("accounts", accountIds).query(InventoryInstance::class.java).list().groupBy { it.accountId }
        val reservationsByAccount = jdbc.sql(
            "select distinct account_id,item_id from inventory_capacity_reservation " +
                "where account_id in (:accounts) and status='ACTIVE'",
        ).param("accounts", accountIds).query { row, _ -> row.getObject("account_id", UUID::class.java) to row.getString("item_id") }
            .list().groupBy({ it.first }, { it.second }).mapValues { it.value.toSet() }
        return itemIdsByAccount.mapValues { (accountId, itemIds) ->
            val stacks = stacksByAccount[accountId].orEmpty()
            val instances = instancesByAccount[accountId].orEmpty()
            val reservations = reservationsByAccount[accountId].orEmpty()
            val stackSlots = stacks.sumOf { stack ->
                InventorySlots.stackSlots(stack.quantity, catalog.require(stack.itemId), stack.itemId in reservations)
            }
            val missingReservationSlots = reservations.count { itemId -> stacks.none { it.itemId == itemId && it.quantity > 0 } }
            InventoryRepository.StackGrantSnapshot(
                stacks,
                Math.toIntExact(stackSlots + InventorySlots.instanceSlots(instances) + missingReservationSlots),
                reservations.intersect(itemIds.toSet()),
            )
        }
    }

    override fun nextAcquiredSequence(accountId: UUID): Long = jdbc.sql(
        """
        select coalesce(max(acquired_sequence), 0) + 1
        from (
          select acquired_sequence from inventory_stack where account_id=:account
          union all
          select acquired_sequence from inventory_instance where account_id=:account
        ) owned
        """.trimIndent(),
    ).param("account", accountId).query(Long::class.java).single()

    override fun rewardCommand(accountId: UUID, idempotencyKey: UUID): InventoryRepository.RewardCommand? = jdbc.sql("select fingerprint,result_json::text from inventory_reward_command where account_id=:account and idempotency_key=:key")
        .param("account", accountId).param("key", idempotencyKey)
        .query { row, _ -> InventoryRepository.RewardCommand(row.getString("fingerprint"), row.getString("result_json")) }.optional().orElse(null)

    override fun saveRewardCommand(accountId: UUID, idempotencyKey: UUID, fingerprint: String, resultJson: String) {
        jdbc.sql("insert into inventory_reward_command(command_id,account_id,idempotency_key,fingerprint,result_json) values (:command,:account,:key,:fingerprint,cast(:result as jsonb))")
            .params(mapOf("command" to UUID.randomUUID(), "account" to accountId, "key" to idempotencyKey, "fingerprint" to fingerprint, "result" to resultJson)).update()
    }
}
