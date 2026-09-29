package com.hanjjak.admin.application

import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.stereotype.Service
import java.time.Instant
import java.util.UUID

@Service
class AdminAccountService(private val jdbc: JdbcClient) {
    data class AccountSummary(
        val accountId: UUID,
        val characterId: UUID,
        val email: String?,
        val nickname: String,
        val stateVersion: Long,
        val createdAt: Instant,
        val level: Int,
        val experience: Long,
        val rice: Long,
        val primaryMaterialType: String?,
        val currentStageId: String?,
        val repeatStageId: String?,
        val highestClearedStageId: String?,
        val inventoryStackCount: Long,
        val inventoryQuantity: Long,
        val equipmentSlotCount: Long,
        val skillCount: Long,
        val gemCount: Long,
        val cosmeticCount: Long,
        val activeGameSession: Boolean,
        val activeBattleSession: Boolean,
        val activeMarketOrderCount: Long,
        val unclaimedMailCount: Long,
    )

    data class CommandSummary(val commandId: UUID, val status: String, val expiresAt: Instant)
    data class WalletLedgerSummary(val ledgerId: UUID, val delta: Long, val balanceAfter: Long, val sourceType: String, val sourceId: UUID, val createdAt: Instant)
    data class BattleSessionSummary(val battleSessionId: UUID, val status: String, val stageId: String, val contentVersion: String, val startedAt: Instant, val closedAt: Instant?)
    data class AccountDetail(
        val account: AccountSummary,
        val commands: List<CommandSummary>,
        val walletLedger: List<WalletLedgerSummary>,
        val battles: List<BattleSessionSummary>,
    )

    fun search(query: String?, includeEmail: Boolean): List<AccountSummary> {
        val normalized = query?.trim().orEmpty()
        require(normalized.length <= 320) { "ADMIN_INVALID_ACCOUNT_QUERY" }
        val clauses = if (normalized.isBlank()) {
            emptyList()
        } else {
            buildList {
                add("position(lower(:query) in lower(a.id::text)) > 0")
                add("position(lower(:query) in lower(c.id::text)) > 0")
                add("position(lower(:query) in lower(c.nickname)) > 0")
                if (includeEmail) add("position(lower(:query) in lower(a.email)) > 0")
            }
        }
        val where = "a.deleted_at is null and (${clauses.joinToString(" or ").ifBlank { "true" }})"
        return jdbc.sql(
            """
            ${baseSelect(includeEmail)}
            where $where
            order by a.created_at desc, a.id desc
            """.trimIndent(),
        )
            .param("query", normalized)
            .query { result, _ -> result.accountSummary(includeEmail) }
            .list()
    }

    fun detail(accountId: UUID, includeEmail: Boolean): AccountDetail {
        val account = jdbc.sql("${baseSelect(includeEmail)} where a.id = :accountId")
            .param("accountId", accountId)
            .query { result, _ -> result.accountSummary(includeEmail) }
            .optional()
            .orElseThrow { IllegalArgumentException("ADMIN_ACCOUNT_NOT_FOUND") }
        val commands = jdbc.sql(
            """
            select command_id, status, expires_at
            from command_record
            where account_id = :accountId
            order by expires_at desc, command_id desc
            limit 20
            """.trimIndent(),
        ).param("accountId", accountId).query { result, _ ->
            CommandSummary(result.getObject("command_id", UUID::class.java), result.getString("status"), result.getTimestamp("expires_at").toInstant())
        }.list()
        val ledger = jdbc.sql(
            """
            select ledger_id, delta, balance_after, source_type, source_id, created_at
            from wallet_ledger
            where account_id = :accountId
            order by created_at desc, ledger_id desc
            limit 20
            """.trimIndent(),
        ).param("accountId", accountId).query { result, _ ->
            WalletLedgerSummary(
                result.getObject("ledger_id", UUID::class.java),
                result.getLong("delta"),
                result.getLong("balance_after"),
                result.getString("source_type"),
                result.getObject("source_id", UUID::class.java),
                result.getTimestamp("created_at").toInstant(),
            )
        }.list()
        val battles = jdbc.sql(
            """
            select id, status, stage_id, content_version, started_at, closed_at
            from battle_session
            where account_id = :accountId
            order by started_at desc, id desc
            limit 20
            """.trimIndent(),
        ).param("accountId", accountId).query { result, _ ->
            BattleSessionSummary(
                result.getObject("id", UUID::class.java),
                result.getString("status"),
                result.getString("stage_id"),
                result.getString("content_version"),
                result.getTimestamp("started_at").toInstant(),
                result.getTimestamp("closed_at")?.toInstant(),
            )
        }.list()
        return AccountDetail(account, commands, ledger, battles)
    }

    private fun baseSelect(includeEmail: Boolean): String = """
        select
          a.id as account_id,
          c.id as character_id,
          ${if (includeEmail) "a.email" else "null::varchar as email"},
          c.nickname,
          a.state_version,
          a.created_at,
          c.level,
          c.experience,
          coalesce(w.balance, c.rice) as rice,
          mp.material_type as primary_material_type,
          ars.current_stage_id,
          ars.repeat_stage_id,
          (select max(stage_id) from stage_progress sp where sp.account_id = a.id and sp.first_cleared_at is not null) as highest_cleared_stage_id,
          (select count(*) from inventory_stack i where i.account_id = a.id and i.quantity > 0) as inventory_stack_count,
          (select coalesce(sum(quantity), 0) from inventory_stack i where i.account_id = a.id) as inventory_quantity,
          (select count(*) from equipment_slot_state e where e.account_id = a.id) as equipment_slot_count,
          (select count(*) from skill_state s where s.account_id = a.id) as skill_count,
          (select count(*) from gem_instance g where g.account_id = a.id) as gem_count,
          (select count(*) from cosmetic_collection_state cs where cs.account_id = a.id and cs.registered_quantity > 0) as cosmetic_count,
          exists(select 1 from game_session gs where gs.account_id = a.id and gs.status = 'ACTIVE') as active_game_session,
          exists(select 1 from battle_session bs where bs.account_id = a.id and bs.status = 'ACTIVE') as active_battle_session,
          (select count(*) from market_order mo where mo.account_id = a.id and mo.status in ('ACTIVE','PARTIALLY_FILLED')) as active_market_order_count,
          (select count(*) from mail_message mm where mm.account_id = a.id and mm.claimed = false) as unclaimed_mail_count
        from account a
        join character c on c.account_id = a.id
        left join wallet_balance w on w.account_id = a.id
        left join material_preference mp on mp.account_id = a.id
        left join account_runtime_state ars on ars.account_id = a.id
    """.trimIndent()

    private fun java.sql.ResultSet.accountSummary(includeEmail: Boolean) = AccountSummary(
        getObject("account_id", UUID::class.java),
        getObject("character_id", UUID::class.java),
        if (includeEmail) getString("email") else null,
        getString("nickname"),
        getLong("state_version"),
        getTimestamp("created_at").toInstant(),
        getInt("level"),
        getLong("experience"),
        getLong("rice"),
        getString("primary_material_type"),
        getString("current_stage_id"),
        getString("repeat_stage_id"),
        getString("highest_cleared_stage_id"),
        getLong("inventory_stack_count"),
        getLong("inventory_quantity"),
        getLong("equipment_slot_count"),
        getLong("skill_count"),
        getLong("gem_count"),
        getLong("cosmetic_count"),
        getBoolean("active_game_session"),
        getBoolean("active_battle_session"),
        getLong("active_market_order_count"),
        getLong("unclaimed_mail_count"),
    )
}
