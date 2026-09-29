package com.hanjjak.gameapi

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.gameapi.character.CharacterStatsService
import com.hanjjak.gems.application.GemDungeonService
import com.hanjjak.gems.domain.GemDungeonSweepRequest
import com.hanjjak.gems.domain.GemPreset
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.http.MediaType
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.test.web.servlet.MockMvc
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post
import org.springframework.test.web.servlet.result.MockMvcResultMatchers.status
import java.sql.Timestamp
import java.time.Instant
import java.time.temporal.ChronoUnit
import java.util.UUID

@GameApiIntegrationTest
class GemDungeonIntegrationTest {
    @Autowired lateinit var mvc: MockMvc
    @Autowired lateinit var mapper: ObjectMapper
    @Autowired lateinit var jdbc: JdbcClient
    @Autowired lateinit var dungeons: GemDungeonService
    @Autowired lateinit var characterStats: CharacterStatsService
    @Autowired lateinit var policy: com.hanjjak.gems.domain.GemDungeonPolicy

    @Autowired lateinit var raidSnapshots: com.hanjjak.raid.application.RaidCombatSnapshotProvider
    @Test
    fun `dungeon stores the canonical preset snapshot without applying gems twice`() {
        val accountId = signup()
        unlock(accountId)
        jdbc.sql("insert into gem_dungeon_test_access(account_id) values (:account)").param("account", accountId).update()
        val attackGem = UUID.randomUUID()
        val hasteGem = UUID.randomUUID()
        jdbc.sql("insert into gem_instance(gem_id,account_id,level,option,value,locked,content_version) values (:attack,:account,1,'FLAT_ATTACK',50,false,'test'),(:haste,:account,7,'HASTE',1200,false,'test')")
            .params(mapOf("attack" to attackGem, "haste" to hasteGem, "account" to accountId)).update()
        jdbc.sql("insert into gem_loadout(account_id,preset,slot_index,gem_id) values (:account,'BERSERK',1,:attack),(:account,'BERSERK',2,:haste)")
            .params(mapOf("account" to accountId, "attack" to attackGem, "haste" to hasteGem)).update()

        val expected = characterStats.snapshot(accountId, GemPreset.BERSERK).calculation
        val challenge = dungeons.start(accountId, UUID.randomUUID(), GemPreset.BERSERK)
        val stored = mapper.readTree(jdbc.sql("select player_snapshot::text from gem_dungeon_challenge where challenge_id=:challenge")
            .param("challenge", challenge.challengeId).query(String::class.java).single())

        assertEquals(expected.fighter.attack, stored.at("/player/attack").asInt())
        assertEquals(expected.fighter.maxHp, stored.at("/player/maxHp").asInt())
        assertEquals(expected.skills.permanentHasteBasisPoints, stored.at("/skills/permanentHasteBasisPoints").asInt())
        assertEquals(expected.skills.hasteBasisPoints, stored.at("/skills/hasteBasisPoints").asInt())
    }

    @Test
    fun `dungeon freezes permanent equipment stats at entry`() {
        val accountId = signup()
        unlock(accountId)
        jdbc.sql("insert into gem_dungeon_test_access(account_id) values (:account)").param("account", accountId).update()
        val baseline = characterStats.snapshot(accountId, GemPreset.BERSERK).calculation.fighter.attack
        jdbc.sql("insert into equipment_slot_state(account_id,slot,grade,enhancement_level) values (:account,'WEAPON','NORMAL',10)")
            .param("account", accountId).update()
        val entryAttack = characterStats.snapshot(accountId, GemPreset.BERSERK).calculation.fighter.attack
        assertTrue(entryAttack > baseline)

        val challenge = dungeons.start(accountId, UUID.randomUUID(), GemPreset.BERSERK)
        jdbc.sql("update equipment_slot_state set enhancement_level=30 where account_id=:account and slot='WEAPON'")
            .param("account", accountId).update()

        val stored = mapper.readTree(jdbc.sql("select player_snapshot::text from gem_dungeon_challenge where challenge_id=:challenge")
            .param("challenge", challenge.challengeId).query(String::class.java).single())
        assertEquals(entryAttack, stored.at("/player/attack").asInt())
        assertTrue(characterStats.snapshot(accountId, GemPreset.BERSERK).calculation.fighter.attack > entryAttack)
    }

    @Test
    fun `consuming below ticket cap preserves the current recharge boundary`() {
        val accountId = signup()
        unlock(accountId)
        val boss = dungeons.today(accountId).boss
        jdbc.sql("insert into gem_dungeon_progress(account_id,boss_type,highest_cleared_stage) values (:account,:boss,1)")
            .params(mapOf("account" to accountId, "boss" to boss.name)).update()
        val rechargeStartedAt = policy.ticketSlotStartedAt(Instant.now()).truncatedTo(ChronoUnit.MICROS)
        jdbc.sql("update gem_account_state set tickets=2,last_ticket_at=:started where account_id=:account")
            .params(mapOf("started" to Timestamp.from(rechargeStartedAt), "account" to accountId)).update()

        dungeons.sweep(accountId, UUID.randomUUID(), GemDungeonSweepRequest(1))

        val stored = jdbc.sql("select tickets,last_ticket_at from gem_account_state where account_id=:account")
            .param("account", accountId).query { row, _ -> row.getInt("tickets") to row.getTimestamp("last_ticket_at").toInstant() }.single()
        assertEquals(1, stored.first)
        assertEquals(rechargeStartedAt, stored.second)
    }

    @Test
    fun `ticket recharge accrues on hourly boundaries and stops at three`() {
        val accountId = signup()
        assertEquals(3_600, policy.ticketRegenSeconds)
        assertEquals(3, policy.ticketMaxStock)
        unlock(accountId)
        dungeons.today(accountId)
        val rechargeStartedAt = Instant.now().minusSeconds(4 * 60 * 60L).truncatedTo(ChronoUnit.MICROS)
        jdbc.sql("update gem_account_state set tickets=2,last_ticket_at=:started where account_id=:account")
            .params(mapOf("started" to Timestamp.from(rechargeStartedAt), "account" to accountId)).update()

        dungeons.today(accountId)

        val tickets = jdbc.sql("select tickets from gem_account_state where account_id=:account")
            .param("account", accountId).query(Int::class.java).single()
        assertEquals(3, tickets)
    }

    @Test
    fun `consuming from full ticket stock anchors recharge to the current hour`() {
        val accountId = signup()
        unlock(accountId)
        val boss = dungeons.today(accountId).boss
        jdbc.sql("insert into gem_dungeon_progress(account_id,boss_type,highest_cleared_stage) values (:account,:boss,1)")
            .params(mapOf("account" to accountId, "boss" to boss.name)).update()
        val staleRechargeStart = Instant.now().minusSeconds(2 * 60 * 60L).truncatedTo(ChronoUnit.MICROS)
        jdbc.sql("update gem_account_state set tickets=3,last_ticket_at=:started where account_id=:account")
            .params(mapOf("started" to Timestamp.from(staleRechargeStart), "account" to accountId)).update()
        val rechargeBoundary = policy.ticketSlotStartedAt(Instant.now())

        dungeons.sweep(accountId, UUID.randomUUID(), GemDungeonSweepRequest(1))

        val stored = jdbc.sql("select tickets,last_ticket_at from gem_account_state where account_id=:account")
            .param("account", accountId).query { row, _ -> row.getInt("tickets") to row.getTimestamp("last_ticket_at").toInstant() }.single()
        assertEquals(2, stored.first)
        assertEquals(rechargeBoundary, stored.second)
    }
    @Test
    fun `sweep count is limited to one through three`() {
        val accountId = signup()
        unlock(accountId)
        val boss = dungeons.today(accountId).boss
        jdbc.sql("insert into gem_dungeon_progress(account_id,boss_type,highest_cleared_stage) values (:account,:boss,1)")
            .params(mapOf("account" to accountId, "boss" to boss.name)).update()

        for (invalidCount in listOf(0, 4)) {
            val error = org.junit.jupiter.api.assertThrows<IllegalArgumentException> {
                dungeons.sweep(accountId, UUID.randomUUID(), GemDungeonSweepRequest(invalidCount))
            }
            assertEquals("INVALID_GEM_DUNGEON_SWEEP_COUNT", error.message)
        }
        assertEquals(3, jdbc.sql("select tickets from gem_account_state where account_id=:account")
            .param("account", accountId).query(Int::class.java).single())
    }


    @Test
    fun `abort releases reward capacity without consuming tickets and replay stays unchanged`() {
        val accountId = signup()
        unlock(accountId)
        val challenge = dungeons.start(accountId, UUID.randomUUID())
        assertReservation(challenge.challengeId, "ACTIVE")
        val key = UUID.randomUUID()

        dungeons.abort(accountId, challenge.challengeId, key)
        dungeons.abort(accountId, challenge.challengeId, key)
        dungeons.complete(accountId, challenge.challengeId, UUID.randomUUID())

        assertReservation(challenge.challengeId, "RELEASED")
        assertEquals("ABORTED", jdbc.sql("select status from gem_dungeon_challenge where challenge_id=:id")
            .param("id", challenge.challengeId).query(String::class.java).single())
        assertNoRewardOrTicketConsumption(accountId)
        val next = dungeons.start(accountId, UUID.randomUUID())
        assertReservation(next.challengeId, "ACTIVE")
    }

    @Test
    fun `expired challenge releases capacity and late completion cannot grant rewards`() {
        val accountId = signup()
        unlock(accountId)
        val challenge = dungeons.start(accountId, UUID.randomUUID())
        jdbc.sql("update gem_dungeon_challenge set started_at=:started,minimum_complete_at=:minimum,expires_at=:expires where challenge_id=:id")
            .params(mapOf("started" to Timestamp.from(Instant.now().minusSeconds(90)),
                "minimum" to Timestamp.from(Instant.now().minusSeconds(60)),
                "expires" to Timestamp.from(Instant.now().minusSeconds(30)), "id" to challenge.challengeId)).update()

        dungeons.today(accountId)
        dungeons.complete(accountId, challenge.challengeId, UUID.randomUUID())

        assertReservation(challenge.challengeId, "RELEASED")
        assertEquals("EXPIRED", jdbc.sql("select status from gem_dungeon_challenge where challenge_id=:id")
            .param("id", challenge.challengeId).query(String::class.java).single())
        assertNoRewardOrTicketConsumption(accountId)
    }

    @Test
    fun `raid snapshot preserves unlocked skill ids and active loadout`() {
        val accountId = signup()
        jdbc.sql("insert into skill_state(account_id,skill_id,grade,level,failure_bonus_basis_points) values (:account,'active_heavy','NORMAL',1,0),(:account,'passive_critical','NORMAL',1,0)")
            .param("account", accountId).update()
        jdbc.sql("insert into skill_loadout(account_id,slot_index,skill_id) values (:account,1,'active_heavy')")
            .param("account", accountId).update()

        jdbc.sql("update character set cosmetics_unlocked=true where account_id=:account").param("account", accountId).update()
        (1..6).forEach { index ->
            jdbc.sql("insert into cosmetic_collection_state(account_id,cosmetic_id,registered_quantity,unregistered_quantity,reserved_quantity) values (:account,:cosmetic,1,0,0)")
                .params(mapOf("account" to accountId, "cosmetic" to "cosmetic-${index.toString().padStart(3, '0')}")).update()
        }

        val snapshot = raidSnapshots.snapshot(accountId)

        assertEquals(listOf("active_heavy", "passive_critical"), snapshot.skillIds)
        assertEquals(listOf("active_heavy"), snapshot.activeSkillLoadout)
        assertEquals("MAIN", snapshot.mainGemPreset)
        assertEquals(0, snapshot.defense)
        assertEquals(listOf("cosmetic-set-01"), snapshot.cosmeticEffectIds)
    }

    private fun assertReservation(challengeId: UUID, expected: String) {
        assertEquals(expected, jdbc.sql("select status from inventory_capacity_reservation where challenge_id=:id")
            .param("id", challengeId).query(String::class.java).single())
    }

    private fun assertNoRewardOrTicketConsumption(accountId: UUID) {
        assertEquals(3, jdbc.sql("select tickets from gem_account_state where account_id=:account")
            .param("account", accountId).query(Int::class.java).single())
        assertEquals(0L, jdbc.sql("select coalesce(sum(quantity),0) from inventory_stack where account_id=:account and item_id='GEM_BOX'")
            .param("account", accountId).query(Long::class.java).single())
        assertEquals(0L, jdbc.sql("select count(*) from gem_dungeon_first_clear where account_id=:account")
            .param("account", accountId).query(Long::class.java).single())
    }

    private fun signup(): UUID {
        val response = mvc.perform(post("/api/v1/auth/signup")
            .header("Idempotency-Key", UUID.randomUUID().toString())
            .header("Origin", "http://localhost:5173")
            .contentType(MediaType.APPLICATION_JSON)
            .content(mapper.writeValueAsString(mapOf("email" to "dungeon-${UUID.randomUUID()}@test.local", "password" to "password123", "nickname" to "dungeon-test"))))
            .andExpect(status().isOk).andReturn().response.contentAsString
        return UUID.fromString(mapper.readTree(response).at("/data/accountId").asText())
    }

    private fun unlock(accountId: UUID) {
        jdbc.sql("insert into stage_progress(account_id,stage_id,unlocked,first_cleared_at,highest_clear_count,content_version) values (:account,'stage.01-05',true,now(),1,'test') on conflict(account_id,stage_id) do update set unlocked=true,first_cleared_at=coalesce(stage_progress.first_cleared_at,excluded.first_cleared_at),highest_clear_count=greatest(stage_progress.highest_clear_count,1)")
            .param("account", accountId).update()
    }

}
