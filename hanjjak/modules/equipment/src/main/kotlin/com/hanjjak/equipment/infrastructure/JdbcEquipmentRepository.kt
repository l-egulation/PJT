package com.hanjjak.equipment.infrastructure

import com.hanjjak.equipment.application.EquipmentRepository
import com.hanjjak.inventory.application.InventoryReservationService
import com.hanjjak.equipment.domain.EquipmentGrade
import com.hanjjak.equipment.domain.EquipmentSlot
import com.hanjjak.equipment.domain.EquipmentSlotState
import com.hanjjak.equipment.domain.MaterialCost
import org.springframework.jdbc.core.simple.JdbcClient
import java.util.UUID

class JdbcEquipmentRepository(private val jdbc: JdbcClient, private val inventory: InventoryReservationService) : EquipmentRepository {
    override fun lockAccount(accountId: UUID) {
        jdbc.sql("select id from account where id=:account for update")
            .param("account", accountId)
            .query(UUID::class.java)
            .optional()
            .orElseThrow { IllegalArgumentException("AUTHENTICATION_REQUIRED") }
    }
    override fun balanceVersion(accountId: UUID): String = jdbc.sql("select balance_version from account_balance_state where account_id=:account")
        .param("account", accountId).query(String::class.java).single()

    override fun riceBalance(accountId: UUID): Long = jdbc.sql("select balance from wallet_balance where account_id=:account")
        .param("account", accountId)
        .query(Long::class.java)
        .single()

    override fun materialBalances(accountId: UUID, itemIds: Collection<String>): Map<String, Long> {
        if (itemIds.isEmpty()) return emptyMap()
        return jdbc.sql("select item_id,quantity-reserved_quantity as available from inventory_stack where account_id=:account and item_id in (:items)")
            .param("account", accountId)
            .param("items", itemIds)
            .query { row, _ -> row.getString("item_id") to row.getLong("available") }
            .list()
            .toMap()
    }

    override fun slotStates(accountId: UUID): List<EquipmentSlotState> = jdbc.sql(
        "select account_id,slot,grade,enhancement_level from equipment_slot_state where account_id=:account order by slot",
    ).param("account", accountId).query(::mapSlotState).list()

    override fun findSlotState(accountId: UUID, slot: EquipmentSlot): EquipmentSlotState? = jdbc.sql(
        "select account_id,slot,grade,enhancement_level from equipment_slot_state where account_id=:account and slot=:slot for update",
    ).params(mapOf("account" to accountId, "slot" to slot.name)).query(::mapSlotState).optional().orElse(null)

    override fun firstClearedStages(accountId: UUID, stageIds: Collection<String>): Set<String> {
        if (stageIds.isEmpty()) return emptySet()
        return jdbc.sql("select stage_id from stage_progress where account_id=:account and stage_id in (:stages) and first_cleared_at is not null")
            .param("account", accountId)
            .param("stages", stageIds)
            .query(String::class.java)
            .list()
            .toSet()
    }

    override fun consumeMaterials(accountId: UUID, materials: List<MaterialCost>) {
        for (material in materials) {
            try {
                inventory.consumeAvailable(accountId, material.itemId, material.requiredQuantity)
            } catch (error: IllegalArgumentException) {
                if (error.message == "INSUFFICIENT_AVAILABLE_QUANTITY") throw IllegalArgumentException("INSUFFICIENT_MATERIALS", error)
                throw error
            }
        }
    }

    override fun createSlotState(state: EquipmentSlotState) {
        jdbc.sql("insert into equipment_slot_state(account_id,slot,grade,enhancement_level,unlocked_at,updated_at) values (:account,:slot,:grade,:level,now(),now())")
            .params(mapOf("account" to state.accountId, "slot" to state.slot.name, "grade" to state.grade.name, "level" to state.enhancementLevel))
            .update()
    }

    override fun updateSlotState(state: EquipmentSlotState) {
        val changed = jdbc.sql("update equipment_slot_state set grade=:grade,enhancement_level=:level,updated_at=now() where account_id=:account and slot=:slot")
            .params(mapOf("account" to state.accountId, "slot" to state.slot.name, "grade" to state.grade.name, "level" to state.enhancementLevel))
            .update()
        require(changed == 1) { "EQUIPMENT_NOT_UNLOCKED" }
    }

    override fun incrementStateVersion(accountId: UUID): Long = jdbc.sql("update account set state_version=state_version+1 where id=:account returning state_version")
        .param("account", accountId)
        .query(Long::class.java)
        .single()

    override fun command(accountId: UUID, idempotencyKey: UUID): EquipmentRepository.StoredCommand? = jdbc.sql("select fingerprint,result_json::text from equipment_command_record where account_id=:account and idempotency_key=:key")
        .param("account", accountId)
        .param("key", idempotencyKey)
        .query { row, _ -> EquipmentRepository.StoredCommand(row.getString("fingerprint"), row.getString("result_json")) }
        .optional()
        .orElse(null)

    override fun saveCommand(accountId: UUID, idempotencyKey: UUID, fingerprint: String, resultJson: String) {
        jdbc.sql("insert into equipment_command_record(command_id,account_id,idempotency_key,fingerprint,result_json) values (:command,:account,:key,:fingerprint,cast(:result as jsonb))")
            .params(mapOf("command" to UUID.randomUUID(), "account" to accountId, "key" to idempotencyKey, "fingerprint" to fingerprint, "result" to resultJson))
            .update()
    }

    private fun mapSlotState(row: java.sql.ResultSet, ignored: Int): EquipmentSlotState = EquipmentSlotState(
        accountId = row.getObject("account_id", UUID::class.java),
        slot = EquipmentSlot.valueOf(row.getString("slot")),
        grade = EquipmentGrade.valueOf(row.getString("grade")),
        enhancementLevel = row.getInt("enhancement_level"),
    )
}
