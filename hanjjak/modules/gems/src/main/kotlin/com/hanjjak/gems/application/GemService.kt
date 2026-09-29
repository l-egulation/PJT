package com.hanjjak.gems.application

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.gems.domain.*
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.transaction.annotation.Transactional
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.time.Clock
import java.util.UUID
import kotlin.math.abs

interface GemStatsProvider { fun combatStats(accountId: UUID, preset: GemPreset = GemPreset.MAIN): GemCombatStats }

open class GemService(private val jdbc: JdbcClient, private val mapper: ObjectMapper, private val clock: Clock, private val inventory: com.hanjjak.inventory.application.InventoryInstanceService, private val policy: GemDungeonPolicy = GemDungeonPolicy()) : GemStatsProvider {
    private data class StoredCommand(val fingerprint: String, val resultJson: String)

    @Transactional
    open fun state(accountId: UUID): GemState = buildState(accountId)

    @Transactional
    open fun lockSlot(accountId: UUID, key: UUID, slotId: UUID, locked: Boolean): GemState = command(accountId, key, "gem-slot-lock\u0000$slotId\u0000$locked", GemState::class.java) {
        val ids = jdbc.sql("select instance_id from inventory_instance where account_id=:account and inventory_slot_id=:slot and stack_key is not null for update")
            .params(mapOf("account" to accountId, "slot" to slotId)).query(UUID::class.java).list()
        require(ids.isNotEmpty()) { "GEM_NOT_FOUND" }
        jdbc.sql("update inventory_instance set locked=:locked where account_id=:account and inventory_slot_id=:slot")
            .params(mapOf("account" to accountId, "slot" to slotId, "locked" to locked)).update()
        jdbc.sql("update gem_instance set locked=:locked where account_id=:account and gem_id in (:ids)")
            .params(mapOf("account" to accountId, "ids" to ids, "locked" to locked)).update()
        incrementStateVersion(accountId)
        buildState(accountId)
    }

    @Transactional
    open fun openBoxes(accountId: UUID, idempotencyKey: UUID, quantity: Int): GemOpenResult = command(accountId, idempotencyKey, "gem\u0000open\u0000$quantity", GemOpenResult::class.java) {
        require(quantity in 1..100) { "INVALID_GEM_BOX_QUANTITY" }
        try {
            inventory.consumeStackForInstances(accountId, "GEM_BOX", quantity.toLong(), quantity, possibleGroups(1..3))
        } catch (error: IllegalArgumentException) {
            if (error.message == "INSUFFICIENT_AVAILABLE_QUANTITY") throw IllegalArgumentException("INSUFFICIENT_GEM_BOX", error)
            throw error
        }
        val granted = (0 until quantity).map { createGem(accountId, idempotencyKey, it) }.map { summarize(it, emptyMap(), emptySet()) }
        incrementStateVersion(accountId)
        GemOpenResult(granted, buildState(accountId))
    }

    @Transactional
    open fun updatePreset(accountId: UUID, idempotencyKey: UUID, preset: GemPreset, request: GemPresetRequest): GemPresetUpdateResult = command(accountId, idempotencyKey, "gem\u0000preset\u0000${preset.name}\u0000${request.gemIds.joinToString(",")}", GemPresetUpdateResult::class.java) {
        require(unlocked(accountId)) { "GEMS_LOCKED" }
        require(request.gemIds.size <= 6) { "GEM_PRESET_TOO_LARGE" }
        require(request.gemIds.distinct().size == request.gemIds.size) { "GEM_PRESET_DUPLICATE" }
        val owned = gems(accountId).associateBy { it.gemId }
        request.gemIds.forEach { require(owned.containsKey(it)) { "GEM_NOT_FOUND" } }
        require(preset !in lockedPresets(accountId)) { "GEM_PRESET_ACTIVE_CHALLENGE" }
        jdbc.sql("delete from gem_loadout where account_id=:account and preset=:preset").params(mapOf("account" to accountId, "preset" to preset.name)).update()
        request.gemIds.forEachIndexed { index, gemId -> jdbc.sql("insert into gem_loadout(account_id,preset,slot_index,gem_id) values (:account,:preset,:slot,:gem)").params(mapOf("account" to accountId, "preset" to preset.name, "slot" to index + 1, "gem" to gemId)).update() }
        val mainBattleRestarted = preset == GemPreset.MAIN && abortActiveMainBattle(accountId)
        incrementStateVersion(accountId)
        GemPresetUpdateResult(preset, mainBattleRestarted, buildState(accountId))
    }

    @Transactional(readOnly = true)
    open fun previewFusion(accountId: UUID, request: GemFusionPreviewRequest): GemFusionPreview = previewFusion(accountId, request, false)

    @Transactional
    open fun fuse(accountId: UUID, idempotencyKey: UUID, request: GemFusionExecuteRequest): GemFusionResult = command(accountId, idempotencyKey, "gem\u0000fusion\u0000${mapper.writeValueAsString(request)}", GemFusionResult::class.java) {
        if (request.mode == GemFusionMode.SAFE_BATCH && request.targetLevel != null) {
            fuseCascade(accountId, idempotencyKey, request)
        } else {
            val preview = previewSelectedFusion(accountId, request.mode, request.gemIds, true)
            inventory.checkExchange(accountId, preview.consumedGemIds, possibleGroups(preview.resultLevel..preview.resultLevel), preview.fusionCount)
            consumeInventoryBackedGems(accountId, preview.consumedGemIds)
            jdbc.sql("delete from gem_instance where account_id=:account and gem_id in (:gems)").params(mapOf("account" to accountId, "gems" to preview.consumedGemIds)).update()
            val granted = (0 until preview.fusionCount).map { createGem(accountId, idempotencyKey, it, preview.resultLevel) }.map { summarize(it, emptyMap(), emptySet()) }
            incrementStateVersion(accountId)
            GemFusionResult(preview.consumedGemIds, granted, buildState(accountId))
        }
    }

    private fun fuseCascade(accountId: UUID, idempotencyKey: UUID, request: GemFusionExecuteRequest): GemFusionResult {
        require(request.gemIds.isEmpty()) { "INVALID_GEM_FUSION_INPUT" }
        val targetLevel = request.targetLevel ?: throw IllegalArgumentException("INVALID_GEM_LEVEL")
        require(targetLevel in 2..7) { "INVALID_GEM_LEVEL" }
        val allowed = request.allowedOptions.toSet()
        require(allowed.isNotEmpty()) { "INVALID_GEM_FUSION_SELECTION" }
        require(unlocked(accountId)) { "GEMS_LOCKED" }
        val equipped = equippedGemIds(accountId)
        val reserved = reservedGemIds(accountId)
        val consumed = mutableListOf<UUID>()
        val generated = mutableListOf<GemInstance>()
        var creationIndex = 0
        for (level in 1 until targetLevel) {
            val generatedIds = generated.mapTo(mutableSetOf()) { it.gemId }
            val candidates = gems(accountId).filter { !it.locked && it.gemId !in equipped && it.gemId !in reserved && it.level == level && (it.option in allowed || it.gemId in generatedIds) }.sortedBy { it.gemId }
            val selected = candidates.take(candidates.size / 3 * 3)
            if (selected.isEmpty()) continue
            val selectedIds = selected.map { it.gemId }
            selectedIds.forEach { require(gem(accountId, it, true) != null) { "GEM_NOT_FOUND" } }
            inventory.checkExchange(accountId, selectedIds, possibleGroups(level + 1..level + 1), selected.size / 3)
            consumeInventoryBackedGems(accountId, selectedIds)
            jdbc.sql("delete from gem_instance where account_id=:account and gem_id in (:gems)").params(mapOf("account" to accountId, "gems" to selectedIds)).update()
            consumed += selectedIds.filterNot { it in generatedIds }
            generated.removeAll { it.gemId in selectedIds }
            repeat(selected.size / 3) { generated += createGem(accountId, idempotencyKey, creationIndex++, level + 1) }
        }
        require(consumed.isNotEmpty()) { "INVALID_GEM_FUSION_QUANTITY" }
        incrementStateVersion(accountId)
        val state = buildState(accountId)
        val grantedIds = generated.mapTo(mutableSetOf()) { it.gemId }
        return GemFusionResult(consumed, state.gems.filter { it.gemId in grantedIds }, state)
    }

    private fun consumeInventoryBackedGems(accountId: UUID, gemIds: List<UUID>) {
        val rows = jdbc.sql("select instance_id,reserved_for_sale,locked from inventory_instance where account_id=:account and instance_id in (:ids) for update")
            .params(mapOf("account" to accountId, "ids" to gemIds))
            .query { row, _ -> Triple(row.getObject("instance_id", UUID::class.java), row.getBoolean("reserved_for_sale"), row.getBoolean("locked")) }
            .list()
        require(rows.none { it.second || it.third }) { "GEM_NOT_FUSION_SAFE" }
        // V23 backfilled historic gems, but imported/local verification accounts can still contain
        // a gem_instance without its inventory mirror. The gem row is already ownership-locked by
        // previewSelectedFusion, so consume every present mirror and retire the orphan atomically.
        val inventoryIds = rows.map { it.first }
        if (inventoryIds.isNotEmpty()) inventory.consume(accountId, inventoryIds)
    }

    @Transactional(readOnly = true)
    override fun combatStats(accountId: UUID, preset: GemPreset): GemCombatStats {
        val equipped = presetGems(accountId, preset)
        val flatAttack = equipped.filter { it.option == GemOption.FLAT_ATTACK }.sumOf { it.value }
        val flatHp = equipped.filter { it.option == GemOption.FLAT_HP }.sumOf { it.value }
        return GemCombatStats(
            flatAttack = flatAttack,
            flatHp = flatHp,
            attackPercentBasisPoints = equipped.filter { it.option == GemOption.ATTACK_PERCENT }.maxOfOrNull { it.value } ?: 0,
            flatPenetration = equipped.filter { it.option == GemOption.FLAT_PENETRATION }.maxOfOrNull { it.value } ?: 0,
            criticalChanceBasisPoints = equipped.filter { it.option == GemOption.CRITICAL_CHANCE }.maxOfOrNull { it.value } ?: 0,
            hasteBasisPoints = equipped.filter { it.option == GemOption.HASTE }.maxOfOrNull { it.value } ?: 0,
        )
    }

    @Transactional(readOnly = true)
    open fun equipped(accountId: UUID, preset: GemPreset): List<GemInstance> = presetGems(accountId, preset)

    @Transactional(readOnly = true)
    open fun equippedMain(accountId: UUID): List<GemInstance> = equipped(accountId, GemPreset.MAIN)

    private fun buildState(accountId: UUID): GemState {
        if (unlocked(accountId)) refreshTickets(accountId)
        val equipped = equippedPresets(accountId)
        val reserved = reservedGemIds(accountId)
        val slots = gemSlotIds(accountId)
        val summaries = gems(accountId).map { summarize(it, equipped, reserved, slots) }
        val byId = summaries.associateBy { it.gemId }
        val presets = GemPreset.entries.associateWith { preset -> presetGemIds(accountId, preset).mapNotNull { byId[it] } }
        return GemState(unlocked(accountId), if (unlocked(accountId)) tickets(accountId) else 0, secondsUntilNextTicket(accountId), todayBoss(), gemBoxQuantity(accountId), summaries, presets, lockedPresets(accountId))
    }

    private fun unlocked(accountId: UUID): Boolean = jdbc.sql("select count(*) from stage_progress where account_id=:account and stage_id='stage.01-05' and first_cleared_at is not null").param("account", accountId).query(Long::class.java).single() > 0
    private fun ensureAccount(accountId: UUID) { jdbc.sql("insert into gem_account_state(account_id,tickets,last_ticket_at) values (:account,:grant,now()) on conflict(account_id) do nothing").params(mapOf("account" to accountId, "grant" to policy.ticketInitialGrant)).update() }
    private fun refreshTickets(accountId: UUID) {
        ensureAccount(accountId)
        val row = jdbc.sql("select tickets,last_ticket_at from gem_account_state where account_id=:account for update").param("account", accountId).query { rs, _ -> rs.getInt("tickets") to rs.getTimestamp("last_ticket_at").toInstant() }.single()
        val now = clock.instant()
        val currentTickets = row.first.coerceAtMost(policy.ticketMaxStock)
        val gained = (policy.ticketSlotIndex(now) - policy.ticketSlotIndex(row.second)).coerceAtLeast(0).toInt()
        val nextTickets = if (currentTickets >= policy.ticketMaxStock) currentTickets else (currentTickets + gained).coerceAtMost(policy.ticketMaxStock)
        if (nextTickets == row.first && gained <= 0) return
        val nextAt = if (gained > 0) policy.ticketSlotStartedAt(now) else row.second
        jdbc.sql("update gem_account_state set tickets=:tickets,last_ticket_at=:time where account_id=:account").params(mapOf("tickets" to nextTickets, "time" to java.sql.Timestamp.from(nextAt), "account" to accountId)).update()
    }
    private fun tickets(accountId: UUID): Int = jdbc.sql("select tickets from gem_account_state where account_id=:account").param("account", accountId).query(Int::class.java).optional().orElse(0)
    private fun secondsUntilNextTicket(accountId: UUID): Long = jdbc.sql("select tickets from gem_account_state where account_id=:account").param("account", accountId).query(Int::class.java).optional().map { if (it >= policy.ticketMaxStock) 0L else policy.secondsUntilNextTicketSlot(clock.instant()) }.orElse(0)
    private fun todayBoss(): GemPreset = GemRules.bossForSlot(policy.slotIndex(clock.instant()))
    private fun gemBoxQuantity(accountId: UUID): Long = jdbc.sql("select quantity-reserved_quantity from inventory_stack where account_id=:account and item_id='GEM_BOX'").param("account", accountId).query(Long::class.java).optional().orElse(0)
    private fun gems(accountId: UUID): List<GemInstance> = jdbc.sql("select gem_id,account_id,level,option,value,locked from gem_instance where account_id=:account order by created_at desc,gem_id desc").param("account", accountId).query(::mapGem).list()
    private fun gem(accountId: UUID, gemId: UUID, lock: Boolean): GemInstance? = jdbc.sql("select gem_id,account_id,level,option,value,locked from gem_instance where account_id=:account and gem_id=:gem${if (lock) " for update" else ""}").params(mapOf("account" to accountId, "gem" to gemId)).query(::mapGem).optional().orElse(null)
    private fun presetGems(accountId: UUID, preset: GemPreset): List<GemInstance> = presetGemIds(accountId, preset).mapNotNull { gem(accountId, it, false) }
    private fun presetGemIds(accountId: UUID, preset: GemPreset): List<UUID> = jdbc.sql("select gem_id from gem_loadout where account_id=:account and preset=:preset order by slot_index").params(mapOf("account" to accountId, "preset" to preset.name)).query(UUID::class.java).list()
    private fun equippedGemIds(accountId: UUID): Set<UUID> = jdbc.sql("select gem_id from gem_loadout where account_id=:account").param("account", accountId).query(UUID::class.java).list().toSet()
    private fun equippedPresets(accountId: UUID): Map<UUID, List<GemPreset>> = jdbc.sql("select gem_id,preset from gem_loadout where account_id=:account").param("account", accountId).query { row, _ -> row.getObject("gem_id", UUID::class.java) to GemPreset.valueOf(row.getString("preset")) }.list().groupBy({ it.first }, { it.second })
    private fun reservedGemIds(accountId: UUID): Set<UUID> = jdbc.sql("select instance_id from inventory_instance where account_id=:account and reserved_for_sale=true").param("account", accountId).query(UUID::class.java).list().toSet()
    private fun lockedPresets(accountId: UUID): Set<GemPreset> = jdbc.sql("select boss_type from gem_dungeon_challenge where account_id=:account and status='ACTIVE'").param("account", accountId).query(String::class.java).list().mapTo(mutableSetOf(), GemPreset::valueOf)
    private fun abortActiveMainBattle(accountId: UUID): Boolean = jdbc.sql("update battle_session set status='ABORTED',closed_at=:now where account_id=:account and status='ACTIVE'").params(mapOf("now" to java.sql.Timestamp.from(clock.instant()), "account" to accountId)).update() > 0

    private fun previewFusion(accountId: UUID, request: GemFusionPreviewRequest, lock: Boolean): GemFusionPreview {
        require(unlocked(accountId)) { "GEMS_LOCKED" }
        val equipped = equippedGemIds(accountId)
        val reserved = reservedGemIds(accountId)
        val eligible = gems(accountId).filter { !it.locked && it.gemId !in equipped && it.gemId !in reserved && it.level < 7 }
        val selected = when (request.mode) {
            GemFusionMode.MANUAL -> {
                require(request.gemIds.size == 3 && request.gemIds.distinct().size == 3) { "INVALID_GEM_FUSION_INPUT" }
                val eligibleById = eligible.associateBy { it.gemId }
                request.gemIds.map { eligibleById[it] ?: throw IllegalArgumentException("GEM_NOT_FUSION_SAFE") }
            }
            GemFusionMode.SAFE_BATCH -> {
                require(request.gemIds.isEmpty()) { "INVALID_GEM_FUSION_INPUT" }
                val level = request.level ?: throw IllegalArgumentException("INVALID_GEM_LEVEL")
                require(level in 1..6) { "GEM_MAX_LEVEL" }
                require(request.selections.isNotEmpty() && request.selections.map { it.option }.distinct().size == request.selections.size && request.selections.all { it.quantity >= 0 }) { "INVALID_GEM_FUSION_SELECTION" }
                val wanted = request.selections.filter { it.quantity > 0 }
                require(wanted.isNotEmpty() && wanted.sumOf { it.quantity } % 3 == 0) { "INVALID_GEM_FUSION_QUANTITY" }
                val pool = eligible.filter { it.level == level }.groupBy { it.option }
                wanted.flatMap { selection ->
                    val candidates = pool[selection.option].orEmpty().sortedBy { it.gemId }
                    require(candidates.size >= selection.quantity) { "INSUFFICIENT_SAFE_GEMS" }
                    candidates.take(selection.quantity)
                }
            }
        }
        require(selected.map { it.level }.distinct().size == 1) { "GEM_LEVEL_MISMATCH" }
        require(selected.first().level < 7) { "GEM_MAX_LEVEL" }
        val consumedIds = selected.map { it.gemId }
        if (lock) consumedIds.forEach { require(gem(accountId, it, true) != null) { "GEM_NOT_FOUND" } }
        val consumption = selected.groupingBy { it.option }.eachCount().entries.sortedBy { it.key.ordinal }.map { GemFusionConsumption(it.key, it.key.label, it.value) }
        return GemFusionPreview(consumedIds, consumption, selected.size / 3, selected.first().level, selected.first().level + 1)
    }
    private fun previewSelectedFusion(accountId: UUID, mode: GemFusionMode, gemIds: List<UUID>, lock: Boolean): GemFusionPreview {
        require(unlocked(accountId)) { "GEMS_LOCKED" }
        require(gemIds.size >= 3 && gemIds.size % 3 == 0 && gemIds.distinct().size == gemIds.size) { "INVALID_GEM_FUSION_INPUT" }
        require(mode == GemFusionMode.SAFE_BATCH || gemIds.size == 3) { "INVALID_GEM_FUSION_INPUT" }
        val equipped = equippedGemIds(accountId)
        val reserved = reservedGemIds(accountId)
        val selected = gemIds.map { id -> gem(accountId, id, lock) ?: throw IllegalArgumentException("GEM_NOT_FOUND") }
        require(selected.none { it.locked || it.gemId in equipped || it.gemId in reserved }) { "GEM_NOT_FUSION_SAFE" }
        require(selected.map { it.level }.distinct().size == 1) { "GEM_LEVEL_MISMATCH" }
        require(selected.first().level < 7) { "GEM_MAX_LEVEL" }
        val consumption = selected.groupingBy { it.option }.eachCount().entries.sortedBy { it.key.ordinal }.map { GemFusionConsumption(it.key, it.key.label, it.value) }
        return GemFusionPreview(gemIds, consumption, selected.size / 3, selected.first().level, selected.first().level + 1)
    }
    private fun createGem(accountId: UUID, key: UUID, index: Int, forcedLevel: Int? = null): GemInstance {
        val seed = abs((key.mostSignificantBits xor key.leastSignificantBits xor index.toLong() * 1103515245L) % 10_000).toInt()
        val level = forcedLevel ?: GemRules.boxLevel(seed)
        val option = weighted(seed, GemRules.optionsFor(level))
        val gem = GemInstance(UUID.randomUUID(), accountId, level, option, GemRules.fixedValue(level, option), GemRules.autoLocked(option))
        inventory.grant(accountId, gem.gemId, "gem:${gem.level}:${gem.option.name.lowercase(java.util.Locale.ROOT)}", com.hanjjak.inventory.domain.InventorySlots.gemKey(gem.level, gem.option.name, gem.value), gem.locked)
        jdbc.sql("insert into gem_instance(gem_id,account_id,level,option,value,locked,content_version,created_at) values (:gem,:account,:level,:option,:value,:locked,:version,now())").params(mapOf("gem" to gem.gemId, "account" to accountId, "level" to gem.level, "option" to gem.option.name, "value" to gem.value, "locked" to gem.locked, "version" to GEM_CONTENT_VERSION)).update()
        return gem
    }
    private fun summarize(gem: GemInstance, equipped: Map<UUID, List<GemPreset>>, reserved: Set<UUID>, slots: Map<UUID, UUID> = emptyMap()): GemSummary = GemSummary(gem.gemId, gem.level, gem.option, gem.option.label, gem.value, gem.locked, gem.gemId in reserved, equipped[gem.gemId] ?: emptyList(), slots[gem.gemId])
    /** 잠금은 가방 칸에 걸린다. 화면에서 잠금을 누르려면 보석마다 칸 번호를 알아야 한다. */
    private fun gemSlotIds(accountId: UUID): Map<UUID, UUID> = jdbc.sql("select instance_id,inventory_slot_id from inventory_instance where account_id=:account and inventory_slot_id is not null").param("account", accountId).query { row, _ -> row.getObject("instance_id", UUID::class.java) to row.getObject("inventory_slot_id", UUID::class.java) }.list().toMap()
    private fun possibleGroups(levels: IntRange) = levels.flatMap { level ->
        GemRules.optionsFor(level).map { (option, _) ->
            com.hanjjak.inventory.application.InventoryInstanceService.GemGroup(
                "gem:$level:${option.name.lowercase(java.util.Locale.ROOT)}",
                com.hanjjak.inventory.domain.InventorySlots.gemKey(level, option.name, GemRules.fixedValue(level, option)),
                GemRules.autoLocked(option),
            )
        }
    }
    private fun weighted(seed: Int, values: List<Pair<GemOption, Int>>): GemOption { var cursor = seed; for ((value, weight) in values) { if (cursor < weight) return value; cursor -= weight }; return values.last().first }
    private fun mapGem(row: java.sql.ResultSet, ignored: Int): GemInstance = GemInstance(row.getObject("gem_id", UUID::class.java), row.getObject("account_id", UUID::class.java), row.getInt("level"), GemOption.valueOf(row.getString("option")), row.getInt("value"), row.getBoolean("locked"))
    private fun incrementStateVersion(accountId: UUID): Long = jdbc.sql("update account set state_version=state_version+1 where id=:account returning state_version").param("account", accountId).query(Long::class.java).single()
    private fun <T> command(accountId: UUID, key: UUID, source: String, type: Class<T>, action: () -> T): T {
        val fingerprint = sha256(source)
        jdbc.sql("select id from account where id=:account for update").param("account", accountId).query(UUID::class.java).optional().orElseThrow { IllegalArgumentException("AUTHENTICATION_REQUIRED") }
        commandRow(accountId, key)?.let { existing -> require(existing.fingerprint == fingerprint) { "IDEMPOTENCY_KEY_REUSED" }; return mapper.readValue(existing.resultJson, type) }
        val result = action()
        jdbc.sql("insert into gem_command_record(command_id,account_id,idempotency_key,fingerprint,result_json) values (:command,:account,:key,:fingerprint,cast(:result as jsonb))").params(mapOf("command" to UUID.randomUUID(), "account" to accountId, "key" to key, "fingerprint" to fingerprint, "result" to mapper.writeValueAsString(result))).update()
        return result
    }
    private fun commandRow(accountId: UUID, key: UUID): StoredCommand? = jdbc.sql("select fingerprint,result_json::text from gem_command_record where account_id=:account and idempotency_key=:key").params(mapOf("account" to accountId, "key" to key)).query { row, _ -> StoredCommand(row.getString("fingerprint"), row.getString("result_json")) }.optional().orElse(null)
    private fun sha256(value: String): String = MessageDigest.getInstance("SHA-256").digest(value.toByteArray(StandardCharsets.UTF_8)).joinToString("") { "%02x".format(it) }
}
