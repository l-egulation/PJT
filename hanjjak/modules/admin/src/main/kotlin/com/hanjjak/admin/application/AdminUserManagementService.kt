package com.hanjjak.admin.application

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.cosmetics.domain.CosmeticsContent
import com.hanjjak.cosmetics.domain.CosmeticsRules
import com.hanjjak.equipment.domain.EquipmentGrade
import com.hanjjak.equipment.domain.EquipmentSlot
import com.hanjjak.gems.domain.GemOption
import com.hanjjak.gems.domain.GemRules
import com.hanjjak.inventory.application.InventoryRepository
import com.hanjjak.inventory.application.ItemCatalog
import com.hanjjak.inventory.domain.INVENTORY_MAX_SLOTS
import com.hanjjak.inventory.domain.InventoryInstance
import com.hanjjak.inventory.domain.InventorySlots
import com.hanjjak.inventory.domain.InventoryStack
import com.hanjjak.progression.domain.ProgressionRules
import com.hanjjak.stage.application.StageCatalog
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.stereotype.Service
import org.springframework.transaction.annotation.Transactional
import java.nio.charset.StandardCharsets
import java.sql.Timestamp
import java.security.MessageDigest
import java.time.Clock
import java.util.UUID

@Service
class AdminUserManagementService(
    private val jdbc: JdbcClient,
    private val inventory: InventoryRepository,
    private val itemCatalog: ItemCatalog,
    private val stageCatalog: StageCatalog,
    private val cosmetics: CosmeticsContent,
    private val mapper: ObjectMapper,
    private val clock: Clock,
) {
    enum class NumericMode { SET, ADD }

    data class InventoryItemState(
        val itemId: String,
        val displayName: String,
        val category: String,
        val quantity: Long,
        val reservedQuantity: Long,
        val stackable: Boolean,
    )
    data class CosmeticStateView(
        val cosmeticId: String,
        val displayName: String?,
        val grade: String,
        val slot: String,
        val registeredQuantity: Int,
        val unregisteredQuantity: Int,
        val reservedQuantity: Int,
        val star: Int,
    )
    data class EquipmentStateView(val slot: String, val grade: String, val enhancementLevel: Int)
    data class UserState(
        val accountId: UUID,
        val stateVersion: Long,
        val level: Int,
        val experience: Long,
        val rice: Long,
        val currentStageId: String?,
        val highestUnlockedStageId: String?,
        val items: List<InventoryItemState>,
        val cosmetics: List<CosmeticStateView>,
        val equipment: List<EquipmentStateView>,
    )
    data class MutationResult(val before: UserState, val after: UserState, val replayed: Boolean)
    data class StoredCommand(val fingerprint: String, val resultJson: String)

    @Transactional(readOnly = true)
    fun state(accountId: UUID): UserState = snapshot(accountId)

    @Transactional
    fun adjustProgression(
        operatorId: UUID,
        key: UUID,
        accountId: UUID,
        level: Int?,
        experience: Long?,
        reason: String,
    ): MutationResult = execute(operatorId, key, accountId, "ADJUST_USER_PROGRESSION", reason, "${level ?: "KEEP"}:${experience ?: "KEEP"}") {
        require(level != null || experience != null) { "ADMIN_USER_CHANGE_REQUIRED" }
        val current = jdbc.sql("select level,experience from character where account_id=:account for update")
            .param("account", accountId)
            .query { row, _ -> row.getInt("level") to row.getLong("experience") }
            .single()
        val nextLevel = level ?: current.first
        val nextExperience = experience ?: current.second
        require(nextLevel in 1..ProgressionRules.MAX_LEVEL) { "INVALID_LEVEL" }
        require(nextExperience in ProgressionRules.totalExperienceForLevel(nextLevel)..ProgressionRules.totalExperienceForLevel(ProgressionRules.MAX_LEVEL)) { "INVALID_EXPERIENCE" }
        jdbc.sql("update character set level=:level,experience=:experience where account_id=:account")
            .params(mapOf("account" to accountId, "level" to nextLevel, "experience" to nextExperience))
            .update()
        invalidateRuntime(accountId)
    }

    @Transactional
    fun adjustRice(
        operatorId: UUID,
        key: UUID,
        accountId: UUID,
        mode: NumericMode,
        amount: Long,
        reason: String,
    ): MutationResult = execute(operatorId, key, accountId, "ADJUST_USER_RICE", reason, "$mode:$amount") {
        require(amount >= 0) { "INVALID_RICE_AMOUNT" }
        val before = jdbc.sql("select balance from wallet_balance where account_id=:account for update")
            .param("account", accountId)
            .query(Long::class.java)
            .single()
        val after = when (mode) {
            NumericMode.SET -> amount
            NumericMode.ADD -> Math.addExact(before, amount)
        }
        require(after != before) { "ADMIN_USER_CHANGE_REQUIRED" }
        jdbc.sql("update wallet_balance set balance=:balance,updated_at=:now where account_id=:account")
            .params(mapOf("account" to accountId, "balance" to after, "now" to Timestamp.from(clock.instant())))
            .update()
        jdbc.sql("insert into wallet_ledger(ledger_id,account_id,delta,balance_after,source_type,source_id,created_at) values (:ledger,:account,:delta,:balance,'ADMIN_USER_ADJUSTMENT',:source,:now)")
            .params(mapOf("ledger" to UUID.randomUUID(), "account" to accountId, "delta" to after - before, "balance" to after, "source" to key, "now" to Timestamp.from(clock.instant())))
            .update()
    }

    @Transactional
    fun adjustItem(
        operatorId: UUID,
        key: UUID,
        accountId: UUID,
        itemId: String,
        mode: NumericMode,
        quantity: Long,
        reason: String,
    ): MutationResult = execute(operatorId, key, accountId, "ADJUST_USER_ITEM", reason, "$itemId:$mode:$quantity") {
        require(quantity >= 0) { "INVALID_QUANTITY" }
        val definition = itemCatalog.require(itemId)
        require(definition.stackable) { "ADMIN_INSTANCE_ITEM_REQUIRES_GEM_COMMAND" }
        val current = inventory.lockStack(accountId, itemId)
        val next = when (mode) {
            NumericMode.SET -> quantity
            NumericMode.ADD -> Math.addExact(current?.quantity ?: 0, quantity)
        }
        require(next >= (current?.reservedQuantity ?: 0)) { "ADMIN_ITEM_BELOW_RESERVED_QUANTITY" }
        val capacityReserved = inventory.hasCapacityReservation(accountId, itemId)
        val beforeSlots = current?.let { InventorySlots.stackSlots(it.quantity, definition, capacityReserved) } ?: 0
        val afterSlots = InventorySlots.stackSlots(next, definition, capacityReserved)
        require(inventory.usedSlotCount(accountId).toLong() - beforeSlots + afterSlots <= INVENTORY_MAX_SLOTS) { "INVENTORY_CAPACITY_EXCEEDED" }
        require(current != null || next > 0) { "ADMIN_USER_CHANGE_REQUIRED" }
        if (current == null && next > 0) {
            inventory.saveStack(InventoryStack(accountId, itemId, next, 0, inventory.nextAcquiredSequence(accountId)))
        } else if (current != null) {
            inventory.saveStack(current.copy(quantity = next))
        }
    }

    @Transactional
    fun adjustGem(
        operatorId: UUID,
        key: UUID,
        accountId: UUID,
        level: Int,
        option: GemOption,
        delta: Int,
        reason: String,
    ): MutationResult = execute(operatorId, key, accountId, "ADJUST_USER_GEM", reason, "$level:$option:$delta") {
        require(level in 1..7 && delta != 0) { "INVALID_GEM_ADJUSTMENT" }
        require(option in GemRules.optionsFor(level).map { it.first }) { "INVALID_GEM_OPTION" }
        if (delta > 0) grantGems(accountId, level, option, delta) else removeGems(accountId, level, option, -delta)
    }

    @Transactional
    fun unlockStages(
        operatorId: UUID,
        key: UUID,
        accountId: UUID,
        throughStageId: String,
        reason: String,
    ): MutationResult = execute(operatorId, key, accountId, "UNLOCK_USER_STAGES", reason, throughStageId) {
        val target = stageCatalog.require(throughStageId)
        stageCatalog.all().filter { it.id.globalIndex <= target.id.globalIndex }.forEach { stage ->
            val updated = jdbc.sql("update stage_progress set unlocked=true where account_id=:account and stage_id=:stage")
                .params(mapOf("account" to accountId, "stage" to stage.id.key)).update()
            if (updated == 0) {
                jdbc.sql("insert into stage_progress(account_id,stage_id,unlocked,highest_clear_count,content_version) values (:account,:stage,true,0,'v1')")
                    .params(mapOf("account" to accountId, "stage" to stage.id.key)).update()
            }
        }
        val runtimeExists = jdbc.sql("select count(*) from account_runtime_state where account_id=:account")
            .param("account", accountId).query(Long::class.java).single() > 0
        if (!runtimeExists) jdbc.sql("insert into account_runtime_state(account_id,current_stage_id) values (:account,'stage.01-01')")
            .param("account", accountId).update()
        if (target.id.globalIndex >= 5) {
            jdbc.sql("update character set cosmetics_unlocked=true where account_id=:account")
                .param("account", accountId)
                .update()
        }
    }

    @Transactional
    fun adjustCosmetic(
        operatorId: UUID,
        key: UUID,
        accountId: UUID,
        cosmeticId: String,
        registeredQuantity: Int,
        unregisteredQuantity: Int,
        reason: String,
    ): MutationResult = execute(operatorId, key, accountId, "ADJUST_USER_COSMETIC", reason, "$cosmeticId:$registeredQuantity:$unregisteredQuantity") {
        val definition = cosmetics.cosmetics.firstOrNull { it.id == cosmeticId }
            ?: throw IllegalArgumentException("COSMETIC_NOT_FOUND")
        val thresholds = cosmetics.starThresholdsByGrade.getValue(definition.grade)
        require(registeredQuantity in 0..thresholds.last() && unregisteredQuantity >= 0) { "INVALID_COSMETIC_QUANTITY" }
        val reserved = jdbc.sql("select reserved_quantity from cosmetic_collection_state where account_id=:account and cosmetic_id=:cosmetic for update")
            .params(mapOf("account" to accountId, "cosmetic" to cosmeticId))
            .query(Int::class.java)
            .optional()
            .orElse(0)
        require(unregisteredQuantity >= reserved) { "ADMIN_COSMETIC_BELOW_RESERVED_QUANTITY" }
        val existing = jdbc.sql("select count(*) from cosmetic_collection_state where account_id=:account and cosmetic_id=:cosmetic")
            .params(mapOf("account" to accountId, "cosmetic" to cosmeticId)).query(Long::class.java).single() > 0
        val parameters = mapOf(
            "account" to accountId,
            "cosmetic" to cosmeticId,
            "registered" to registeredQuantity,
            "unregistered" to unregisteredQuantity,
            "reserved" to reserved,
        )
        if (existing) {
            jdbc.sql("update cosmetic_collection_state set registered_quantity=:registered,unregistered_quantity=:unregistered where account_id=:account and cosmetic_id=:cosmetic")
                .params(parameters).update()
        } else {
            jdbc.sql("insert into cosmetic_collection_state(account_id,cosmetic_id,registered_quantity,unregistered_quantity,reserved_quantity) values (:account,:cosmetic,:registered,:unregistered,:reserved)")
                .params(parameters).update()
        }
        if (registeredQuantity > 0) {
            jdbc.sql("update character set cosmetics_unlocked=true where account_id=:account")
                .param("account", accountId)
                .update()
        } else {
            jdbc.sql("update cosmetic_equipment set cosmetic_id=null where account_id=:account and cosmetic_id=:cosmetic")
                .params(mapOf("account" to accountId, "cosmetic" to cosmeticId))
                .update()
        }
        invalidateRuntime(accountId)
    }

    @Transactional
    fun adjustEquipment(
        operatorId: UUID,
        key: UUID,
        accountId: UUID,
        slot: EquipmentSlot,
        grade: EquipmentGrade?,
        enhancementLevel: Int?,
        reason: String,
    ): MutationResult = execute(operatorId, key, accountId, "ADJUST_USER_EQUIPMENT", reason, "$slot:${grade ?: "LOCKED"}:${enhancementLevel ?: 0}") {
        require((grade == null && enhancementLevel == null) || (grade != null && enhancementLevel in 1..30)) { "INVALID_EQUIPMENT_STATE" }
        if (grade == null) {
            jdbc.sql("delete from equipment_slot_state where account_id=:account and slot=:slot")
                .params(mapOf("account" to accountId, "slot" to slot.name))
                .update()
        } else {
            val existing = jdbc.sql("select count(*) from equipment_slot_state where account_id=:account and slot=:slot")
                .params(mapOf("account" to accountId, "slot" to slot.name)).query(Long::class.java).single() > 0
            val parameters = mapOf(
                "account" to accountId,
                "slot" to slot.name,
                "grade" to grade.name,
                "level" to requireNotNull(enhancementLevel),
                "now" to Timestamp.from(clock.instant()),
            )
            if (existing) {
                jdbc.sql("update equipment_slot_state set grade=:grade,enhancement_level=:level,updated_at=:now where account_id=:account and slot=:slot")
                    .params(parameters).update()
            } else {
                jdbc.sql("insert into equipment_slot_state(account_id,slot,grade,enhancement_level,unlocked_at,updated_at) values (:account,:slot,:grade,:level,:now,:now)")
                    .params(parameters).update()
            }
        }
        invalidateRuntime(accountId)
    }

    private fun execute(
        operatorId: UUID,
        key: UUID,
        accountId: UUID,
        action: String,
        reason: String,
        fingerprintInput: String,
        mutation: () -> Unit,
    ): MutationResult {
        val normalizedReason = reason.trim().also { require(it.length in 3..500) { "ADMIN_USER_REASON_REQUIRED" } }
        val fingerprint = sha256("$action\u0000$accountId\u0000$fingerprintInput\u0000$normalizedReason")
        command(operatorId, key)?.let { stored ->
            require(stored.fingerprint == fingerprint) { "IDEMPOTENCY_KEY_REUSED" }
            return mapper.readValue(stored.resultJson, MutationResult::class.java).copy(replayed = true)
        }
        lockAccount(accountId)
        val before = snapshot(accountId)
        mutation()
        val after = snapshot(accountId)
        require(after != before) { "ADMIN_USER_CHANGE_REQUIRED" }
        incrementStateVersion(accountId)
        val versionedAfter = snapshot(accountId)
        val result = MutationResult(before, versionedAfter, false)
        jdbc.sql("insert into admin_user_command(operator_id,idempotency_key,account_id,action,fingerprint,result_json,created_at) values (:operator,:key,:account,:action,:fingerprint,:result,:now)")
            .params(
                mapOf(
                    "operator" to operatorId,
                    "key" to key,
                    "account" to accountId,
                    "action" to action,
                    "fingerprint" to fingerprint,
                    "result" to mapper.writeValueAsString(result),
                    "now" to Timestamp.from(clock.instant()),
                ),
            ).update()
        return result
    }

    private fun grantGems(accountId: UUID, level: Int, option: GemOption, count: Int) {
        repeat(count) {
            val gemId = UUID.randomUUID()
            val itemId = "gem:$level:${option.name.lowercase()}"
            val value = GemRules.fixedValue(level, option)
            val locked = GemRules.autoLocked(option)
            val stackKey = InventorySlots.gemKey(level, option.name, value)
            val peers = inventory.instances(accountId).filter { member ->
                member.itemId == itemId && member.stackKey == stackKey && member.locked == locked
            }
            val slotId = peers.lastOrNull()?.inventorySlotId?.let { candidate ->
                candidate.takeIf { peers.count { member -> member.inventorySlotId == candidate } < InventorySlots.GEM_LIMIT }
            } ?: gemId
            require(inventory.usedSlotCount(accountId) + (if (slotId == gemId) 1 else 0) <= INVENTORY_MAX_SLOTS) { "INVENTORY_CAPACITY_EXCEEDED" }
            inventory.addInstance(
                InventoryInstance(gemId, accountId, itemId, false, inventory.nextAcquiredSequence(accountId), stackKey, slotId, locked),
            )
            jdbc.sql("insert into gem_instance(gem_id,account_id,level,option,value,locked,content_version,created_at) values (:gem,:account,:level,:option,:value,:locked,'gem-v1-draft',:now)")
                .params(
                    mapOf(
                        "gem" to gemId,
                        "account" to accountId,
                        "level" to level,
                        "option" to option.name,
                        "value" to value,
                        "locked" to locked,
                        "now" to Timestamp.from(clock.instant()),
                    ),
                ).update()
        }
    }

    private fun removeGems(accountId: UUID, level: Int, option: GemOption, count: Int) {
        val ids = jdbc.sql(
            """
            select g.gem_id
            from gem_instance g
            join inventory_instance i on i.instance_id=g.gem_id
            where g.account_id=:account and g.level=:level and g.option=:option
              and i.reserved_for_sale=false
              and not exists(select 1 from gem_loadout l where l.gem_id=g.gem_id)
            order by g.created_at desc,g.gem_id desc
            limit :limit
            for update of g
            """.trimIndent(),
        )
            .params(mapOf("account" to accountId, "level" to level, "option" to option.name, "limit" to count))
            .query(UUID::class.java)
            .list()
        require(ids.size == count) { "ADMIN_INSUFFICIENT_ADJUSTABLE_GEMS" }
        inventory.removeInstances(accountId, ids)
        jdbc.sql("delete from gem_instance where account_id=:account and gem_id in (:ids)")
            .params(mapOf("account" to accountId, "ids" to ids))
            .update()
    }

    private fun command(operatorId: UUID, key: UUID): StoredCommand? = jdbc.sql("select fingerprint,result_json from admin_user_command where operator_id=:operator and idempotency_key=:key")
        .params(mapOf("operator" to operatorId, "key" to key))
        .query(StoredCommand::class.java)
        .optional()
        .orElse(null)

    private fun lockAccount(accountId: UUID) {
        jdbc.sql("select id from account where id=:account and deleted_at is null for update")
            .param("account", accountId)
            .query(UUID::class.java)
            .optional()
            .orElseThrow { IllegalArgumentException("ADMIN_ACCOUNT_NOT_FOUND") }
    }

    private fun incrementStateVersion(accountId: UUID) {
        jdbc.sql("update account set state_version=state_version+1 where id=:account")
            .param("account", accountId)
            .update()
    }

    private fun invalidateRuntime(accountId: UUID) {
        val now = Timestamp.from(clock.instant())
        jdbc.sql("update battle_session set status='ABORTED',closed_at=:now where account_id=:account and status='ACTIVE'")
            .params(mapOf("now" to now, "account" to accountId))
            .update()
        jdbc.sql("update game_session set status='REPLACED',closed_at=:now where account_id=:account and status='ACTIVE'")
            .params(mapOf("now" to now, "account" to accountId))
            .update()
        jdbc.sql("delete from account_active_session where account_id=:account")
            .param("account", accountId)
            .update()
    }

    private fun snapshot(accountId: UUID): UserState {
        val identity = jdbc.sql(
            """
            select a.state_version,c.level,c.experience,w.balance,ars.current_stage_id
            from account a
            join character c on c.account_id=a.id
            join wallet_balance w on w.account_id=a.id
            left join account_runtime_state ars on ars.account_id=a.id
            where a.id=:account and a.deleted_at is null
            """.trimIndent(),
        )
            .param("account", accountId)
            .query { row, _ ->
                IdentityRow(
                    row.getLong("state_version"),
                    row.getInt("level"),
                    row.getLong("experience"),
                    row.getLong("balance"),
                    row.getString("current_stage_id"),
                )
            }
            .optional()
            .orElseThrow { IllegalArgumentException("ADMIN_ACCOUNT_NOT_FOUND") }
        val items = inventory.stacks(accountId).map { stack ->
            val definition = itemCatalog.require(stack.itemId)
            InventoryItemState(stack.itemId, definition.displayName, definition.category.name, stack.quantity, stack.reservedQuantity, true)
        } + inventory.instances(accountId).groupBy { it.itemId }.map { (itemId, instances) ->
            val definition = itemCatalog.require(itemId)
            InventoryItemState(itemId, definition.displayName, definition.category.name, instances.size.toLong(), instances.count { it.reservedForSale }.toLong(), false)
        }
        val cosmeticStates = jdbc.sql("select cosmetic_id,registered_quantity,unregistered_quantity,reserved_quantity from cosmetic_collection_state where account_id=:account")
            .param("account", accountId)
            .query { row, _ ->
                val definition = cosmetics.cosmetics.first { it.id == row.getString("cosmetic_id") }
                val registered = row.getInt("registered_quantity")
                CosmeticStateView(
                    definition.id,
                    definition.displayName,
                    definition.grade.name,
                    definition.slot.name,
                    registered,
                    row.getInt("unregistered_quantity"),
                    row.getInt("reserved_quantity"),
                    CosmeticsRules.star(registered, cosmetics.starThresholdsByGrade.getValue(definition.grade)),
                )
            }
            .list()
        val equipmentStates = jdbc.sql("select slot,grade,enhancement_level from equipment_slot_state where account_id=:account order by slot")
            .param("account", accountId)
            .query { row, _ -> EquipmentStateView(row.getString("slot"), row.getString("grade"), row.getInt("enhancement_level")) }
            .list()
        val highestStage = jdbc.sql("select max(stage_id) from stage_progress where account_id=:account and unlocked=true")
            .param("account", accountId)
            .query(String::class.java)
            .optional()
            .orElse(null)
        return UserState(
            accountId,
            identity.stateVersion,
            identity.level,
            identity.experience,
            identity.rice,
            identity.currentStageId,
            highestStage,
            items.sortedBy { it.itemId },
            cosmeticStates.sortedBy { it.cosmeticId },
            equipmentStates,
        )
    }

    private fun sha256(value: String): String = MessageDigest.getInstance("SHA-256")
        .digest(value.toByteArray(StandardCharsets.UTF_8))
        .joinToString("") { "%02x".format(it) }

    private data class IdentityRow(
        val stateVersion: Long,
        val level: Int,
        val experience: Long,
        val rice: Long,
        val currentStageId: String?,
    )
}
