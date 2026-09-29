package com.hanjjak.battle.infrastructure

import com.fasterxml.jackson.core.type.TypeReference
import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.battle.application.FirstClearRewardRepository
import com.hanjjak.inventory.domain.ItemReward
import org.springframework.jdbc.core.simple.JdbcClient
import java.time.Instant
import java.time.OffsetDateTime
import java.util.UUID

class JdbcFirstClearRewardRepository(
    private val jdbc: JdbcClient,
    private val mapper: ObjectMapper,
) : FirstClearRewardRepository {
    override fun lockAccount(accountId: UUID) {
        jdbc.sql("select id from account where id=:account for update").param("account", accountId).query(UUID::class.java).single()
    }
    override fun findByVersion(accountId: UUID, stageId: String, rewardVersion: String): FirstClearRewardRepository.RewardRow? = jdbc.sql(
        "select reward_id,account_id,stage_id,reward_version,source_id,rice_granted,unlocked_skill_id,item_status,items_json::text,required_slots,claimed_at from stage_first_clear_reward where account_id=:account and stage_id=:stage and reward_version=:version",
    ).params(mapOf("account" to accountId, "stage" to stageId, "version" to rewardVersion)).query { row, _ -> rowToReward(row) }.optional().orElse(null)

    override fun insert(row: FirstClearRewardRepository.RewardRow): Boolean = jdbc.sql(
        "insert into stage_first_clear_reward(reward_id,account_id,stage_id,reward_version,source_id,rice_granted,unlocked_skill_id,item_status,items_json,required_slots,claimed_at,created_at) values (:reward,:account,:stage,:version,:source,:rice,:skill,:status,cast(:items as jsonb),:slots,:claimed,now()) on conflict(account_id,stage_id,reward_version) do nothing",
    ).params(
        mapOf(
            "reward" to row.rewardId,
            "account" to row.accountId,
            "stage" to row.stageId,
            "version" to row.rewardVersion,
            "source" to row.sourceId,
            "rice" to row.riceGranted,
            "skill" to row.unlockedSkillId,
            "status" to row.itemStatus,
            "items" to mapper.writeValueAsString(row.items),
            "slots" to row.requiredSlots,
            "claimed" to row.claimedAt?.let { OffsetDateTime.ofInstant(it, java.time.ZoneOffset.UTC) },
        ),
    ).update() == 1

    override fun lockReward(accountId: UUID, rewardId: UUID): FirstClearRewardRepository.RewardRow? = jdbc.sql(
        "select reward_id,account_id,stage_id,reward_version,source_id,rice_granted,unlocked_skill_id,item_status,items_json::text,required_slots,claimed_at from stage_first_clear_reward where account_id=:account and reward_id=:reward for update",
    ).params(mapOf("account" to accountId, "reward" to rewardId)).query { row, _ -> rowToReward(row) }.optional().orElse(null)

    override fun pending(accountId: UUID): List<FirstClearRewardRepository.RewardRow> = jdbc.sql(
        "select reward_id,account_id,stage_id,reward_version,source_id,rice_granted,unlocked_skill_id,item_status,items_json::text,required_slots,claimed_at from stage_first_clear_reward where account_id=:account and item_status='PENDING' order by created_at,reward_id",
    ).param("account", accountId).query { row, _ -> rowToReward(row) }.list()

    override fun markClaimed(rewardId: UUID, claimedAt: Instant) {
        jdbc.sql("update stage_first_clear_reward set item_status='CLAIMED',claimed_at=:claimed where reward_id=:reward")
            .params(mapOf("reward" to rewardId, "claimed" to OffsetDateTime.ofInstant(claimedAt, java.time.ZoneOffset.UTC))).update()
    }

    override fun claimCommand(accountId: UUID, idempotencyKey: UUID): FirstClearRewardRepository.ClaimCommand? = jdbc.sql(
        "select fingerprint,result_json::text from first_clear_reward_claim_command where account_id=:account and idempotency_key=:key",
    ).params(mapOf("account" to accountId, "key" to idempotencyKey)).query { row, _ -> FirstClearRewardRepository.ClaimCommand(row.getString("fingerprint"), row.getString("result_json")) }.optional().orElse(null)

    override fun saveClaimCommand(accountId: UUID, idempotencyKey: UUID, rewardId: UUID, fingerprint: String, resultJson: String) {
        jdbc.sql("insert into first_clear_reward_claim_command(account_id,idempotency_key,reward_id,fingerprint,result_json,created_at) values (:account,:key,:reward,:fingerprint,cast(:result as jsonb),now())")
            .params(mapOf("account" to accountId, "key" to idempotencyKey, "reward" to rewardId, "fingerprint" to fingerprint, "result" to resultJson)).update()
    }

    private fun rowToReward(row: java.sql.ResultSet): FirstClearRewardRepository.RewardRow = FirstClearRewardRepository.RewardRow(
        rewardId = row.getObject("reward_id", UUID::class.java),
        accountId = row.getObject("account_id", UUID::class.java),
        stageId = row.getString("stage_id"),
        rewardVersion = row.getString("reward_version"),
        sourceId = row.getObject("source_id", UUID::class.java),
        riceGranted = row.getLong("rice_granted"),
        unlockedSkillId = row.getString("unlocked_skill_id"),
        itemStatus = row.getString("item_status"),
        items = mapper.readValue(row.getString("items_json"), object : TypeReference<List<ItemReward>>() {}),
        requiredSlots = row.getInt("required_slots"),
        claimedAt = row.getObject("claimed_at", OffsetDateTime::class.java)?.toInstant(),
    )
}
