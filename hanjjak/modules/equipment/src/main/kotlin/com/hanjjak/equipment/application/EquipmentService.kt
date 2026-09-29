package com.hanjjak.equipment.application

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.events.application.DomainEventPublisher
import com.hanjjak.events.application.NoopDomainEventPublisher
import com.hanjjak.equipment.domain.*
import org.springframework.transaction.annotation.Transactional
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.util.UUID

open class EquipmentService(
    private val repository: EquipmentRepository,
    private val mapper: ObjectMapper,
    private val wallet: com.hanjjak.wallet.application.WalletService,
    private val events: DomainEventPublisher = NoopDomainEventPublisher,
    private val balanceCatalog: EquipmentBalanceCatalog = EquipmentBalanceCatalog.legacy(),
) : EquipmentStatsProvider {
    @Transactional(readOnly = true)
    open fun state(accountId: UUID): EquipmentState {
        val policy = balanceCatalog.policy(repository.balanceVersion(accountId))
        return buildState(accountId, policy)
    }


    @Transactional
    open fun unlock(accountId: UUID, idempotencyKey: UUID, slot: EquipmentSlot): EquipmentCommandResult =
        command(accountId, idempotencyKey, "unlock\u0000${slot.name}") { policy ->
            require(repository.findSlotState(accountId, slot) == null) { "EQUIPMENT_ALREADY_UNLOCKED" }
            val cost = EquipmentRules.unlockCost(slot)
            wallet.debit(accountId, cost.riceCost, "EQUIPMENT_UNLOCK", idempotencyKey, bumpStateVersion = false)
            repository.consumeMaterials(accountId, cost.materials)
            val next = EquipmentSlotState(accountId, slot, EquipmentGrade.NORMAL, 1)
            repository.createSlotState(next)
            repository.incrementStateVersion(accountId)
            publishEquipmentEvent("EQUIPMENT_CRAFTED", accountId, idempotencyKey, next, cost, mapOf("quantity" to 1L, "gradeAfter" to next.grade.name, "levelAfter" to next.enhancementLevel))
            result(accountId, slot, policy)
        }

    @Transactional
    open fun enhance(accountId: UUID, idempotencyKey: UUID, slot: EquipmentSlot): EquipmentCommandResult =
        command(accountId, idempotencyKey, "enhance\u0000${slot.name}") { policy ->
            val current = repository.findSlotState(accountId, slot) ?: throw IllegalArgumentException("EQUIPMENT_NOT_UNLOCKED")
            val cost = policy.enhancementCost(current)
            wallet.debit(accountId, cost.riceCost, "EQUIPMENT_ENHANCE", idempotencyKey, bumpStateVersion = false)
            repository.consumeMaterials(accountId, cost.materials)
            val next = current.copy(enhancementLevel = current.enhancementLevel + 1)
            repository.updateSlotState(next)
            repository.incrementStateVersion(accountId)
            publishEquipmentEvent("EQUIPMENT_ENHANCEMENT_ATTEMPTED", accountId, idempotencyKey, next, cost, mapOf("gradeBefore" to current.grade.name, "gradeAfter" to current.grade.name, "levelBefore" to current.enhancementLevel, "levelAfter" to next.enhancementLevel, "success" to true))
            result(accountId, slot, policy)
        }

    @Transactional
    open fun promote(accountId: UUID, idempotencyKey: UUID, slot: EquipmentSlot): EquipmentCommandResult =
        command(accountId, idempotencyKey, "promote\u0000${slot.name}") { policy ->
            val current = repository.findSlotState(accountId, slot) ?: throw IllegalArgumentException("EQUIPMENT_NOT_UNLOCKED")
            val nextGrade = EquipmentRules.nextGrade(current.grade) ?: throw IllegalArgumentException("EQUIPMENT_MAX_GRADE")
            require(current.enhancementLevel == EquipmentRules.MAX_ENHANCEMENT_LEVEL) { "EQUIPMENT_MAX_ENHANCEMENT_REQUIRED" }
            val requiredStage = requireNotNull(policy.requiredPromotionStage(current.grade))
            require(requiredStage in repository.firstClearedStages(accountId, listOf(requiredStage))) { "EQUIPMENT_CHAPTER_NOT_CLEARED" }
            val cost = policy.promotionCost(current.grade)
            wallet.debit(accountId, cost.riceCost, "EQUIPMENT_PROMOTE", idempotencyKey, bumpStateVersion = false)
            repository.consumeMaterials(accountId, cost.materials)
            val next = current.copy(grade = nextGrade, enhancementLevel = 1)
            repository.updateSlotState(next)
            repository.incrementStateVersion(accountId)
            publishEquipmentEvent("EQUIPMENT_PROMOTED", accountId, idempotencyKey, next, cost, mapOf("gradeBefore" to current.grade.name, "gradeAfter" to next.grade.name, "levelBefore" to current.enhancementLevel, "levelAfter" to next.enhancementLevel))
            result(accountId, slot, policy)
        }

    @Transactional(readOnly = true)
    override fun stats(accountId: UUID): EquipmentBattleStats = EquipmentRules.aggregateStats(repository.slotStates(accountId))
    private fun result(accountId: UUID, slot: EquipmentSlot, policy: EquipmentBalancePolicy): EquipmentCommandResult {
        val state = buildState(accountId, policy)
        return EquipmentCommandResult(state.slots.first { it.slot == slot }, state)
    }

    private fun buildState(accountId: UUID, policy: EquipmentBalancePolicy): EquipmentState {
        val states = repository.slotStates(accountId).associateBy { it.slot }
        val balances = repository.materialBalances(accountId, EquipmentRules.allMaterialItemIds())
        val rice = repository.riceBalance(accountId)
        val promotionStages = EquipmentGrade.entries.mapNotNull(policy::requiredPromotionStage).toSet()
        val clearedStages = repository.firstClearedStages(accountId, promotionStages)
        val slots = EquipmentSlot.entries.map { slot -> summarize(slot, states[slot], rice, balances, clearedStages, policy) }
        return EquipmentState(slots, rice)
    }

    private fun summarize(
        slot: EquipmentSlot,
        current: EquipmentSlotState?,
        rice: Long,
        balances: Map<String, Long>,
        clearedStages: Set<String>,
        policy: EquipmentBalancePolicy,
    ): EquipmentSlotSummary {
        if (current == null) {
            val cost = available(EquipmentRules.unlockCost(slot), balances)
            val disabled = affordabilityReason(cost, rice)
            return EquipmentSlotSummary(slot, slot.label, false, growth(slot, null), EquipmentActionSummary(cost, growth(slot, EquipmentSlotState(UUID(0, 0), slot, EquipmentGrade.NORMAL, 1)), EquipmentStatSummary(0, 0, 0), disabled == null, disabled), null, null, false)
        }
        val complete = current.grade == EquipmentGrade.LEGENDARY && current.enhancementLevel == EquipmentRules.MAX_ENHANCEMENT_LEVEL
        val enhance = if (current.enhancementLevel < EquipmentRules.MAX_ENHANCEMENT_LEVEL) {
            val next = current.copy(enhancementLevel = current.enhancementLevel + 1)
            val cost = available(policy.enhancementCost(current), balances)
            val disabled = affordabilityReason(cost, rice)
            EquipmentActionSummary(cost, growth(slot, next), statDifference(growth(slot, current).stats, growth(slot, next).stats), disabled == null, disabled)
        } else null
        val nextGrade = EquipmentRules.nextGrade(current.grade)
        val promote = nextGrade?.let {
            val requiredStage = requireNotNull(policy.requiredPromotionStage(current.grade))
            val cost = available(policy.promotionCost(current.grade), balances)
            val chapterCleared = requiredStage in clearedStages
            val maxReached = current.enhancementLevel == EquipmentRules.MAX_ENHANCEMENT_LEVEL
            val disabled = when {
                !maxReached -> "EQUIPMENT_MAX_ENHANCEMENT_REQUIRED"
                !chapterCleared -> "EQUIPMENT_CHAPTER_NOT_CLEARED"
                else -> affordabilityReason(cost, rice)
            }
            EquipmentPromotionSummary(requiredStage, chapterCleared, maxReached, cost, growth(slot, current.copy(grade = it, enhancementLevel = 1)), disabled == null, disabled)
        }
        return EquipmentSlotSummary(slot, slot.label, true, growth(slot, current), null, enhance, promote, complete)
    }

    private fun statDifference(current: EquipmentStatSummary, next: EquipmentStatSummary): EquipmentStatSummary = EquipmentStatSummary(next.attack - current.attack, next.maxHp - current.maxHp, next.penetration - current.penetration)

    private fun growth(slot: EquipmentSlot, state: EquipmentSlotState?): EquipmentGrowthSummary {
        val q = state?.let(EquipmentRules::q) ?: 0
        return EquipmentGrowthSummary(
            state?.grade,
            state?.grade?.label,
            state?.enhancementLevel ?: 0,
            q,
            EquipmentRules.statSummary(slot, state),
        )
    }

    private fun available(cost: EquipmentCost, balances: Map<String, Long>): EquipmentCost =
        cost.copy(materials = cost.materials.map { it.copy(availableQuantity = balances[it.itemId] ?: 0) })

    private fun affordabilityReason(cost: EquipmentCost, rice: Long): String? = when {
        rice < cost.riceCost -> "INSUFFICIENT_RICE"
        cost.materials.any { it.availableQuantity < it.requiredQuantity } -> "INSUFFICIENT_MATERIALS"
        else -> null
    }

    private fun publishEquipmentEvent(eventType: String, accountId: UUID, commandId: UUID, state: EquipmentSlotState, cost: EquipmentCost, details: Map<String, Any?>) {
        events.publish(
            eventType = eventType,
            aggregateId = UUID.nameUUIDFromBytes("equipment\u0000$accountId\u0000${state.slot.name}".toByteArray(StandardCharsets.UTF_8)),
            occurredAt = java.time.Instant.now(),
            accountId = accountId,
            itemId = "equipment:${state.slot.name.lowercase()}:${state.grade.name.lowercase()}",
            quantity = (details["quantity"] as? Long),
            commandId = commandId,
            payload = mapOf(
                "equipmentSlot" to state.slot.name,
                "itemId" to "equipment:${state.slot.name.lowercase()}:${state.grade.name.lowercase()}",
                "consumedItems" to cost.materials.map { mapOf("itemId" to it.itemId, "quantity" to it.requiredQuantity) },
                "riceCost" to cost.riceCost,
            ) + details,
        )
    }

    private fun command(accountId: UUID, idempotencyKey: UUID, source: String, action: (EquipmentBalancePolicy) -> EquipmentCommandResult): EquipmentCommandResult {
        val fingerprint = sha256(source)
        repository.lockAccount(accountId)
        repository.command(accountId, idempotencyKey)?.let { existing ->
            require(existing.fingerprint == fingerprint) { "IDEMPOTENCY_KEY_REUSED" }
            return mapper.readValue(existing.resultJson, EquipmentCommandResult::class.java)
        }
        val policy = balanceCatalog.policy(repository.balanceVersion(accountId))
        val result = action(policy)
        repository.saveCommand(accountId, idempotencyKey, fingerprint, mapper.writeValueAsString(result))
        return result
    }


    private fun sha256(value: String): String = MessageDigest.getInstance("SHA-256")
        .digest(value.toByteArray(StandardCharsets.UTF_8)).joinToString("") { "%02x".format(it) }
}
