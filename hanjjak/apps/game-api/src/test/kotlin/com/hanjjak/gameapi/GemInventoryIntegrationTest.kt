package com.hanjjak.gameapi

import com.hanjjak.gems.application.GemService
import com.hanjjak.gems.domain.*
import com.hanjjak.inventory.application.InventoryRepository
import org.junit.jupiter.api.Assertions.*
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.jdbc.core.simple.JdbcClient
import java.util.UUID

@GameApiIntegrationTest
class GemInventoryIntegrationTest {
    @Autowired lateinit var jdbc: JdbcClient
    @Autowired lateinit var gems: GemService
    @Autowired lateinit var inventory: InventoryRepository
    @Autowired lateinit var queries: com.hanjjak.inventory.application.InventoryQueryService
    @Autowired lateinit var reservations: com.hanjjak.inventory.application.InventoryReservationService
    @Autowired lateinit var market: com.hanjjak.inventory.application.MarketInventoryService

    @Test
    fun `gem content unlocks only after the first clear of stage 1-5`() {
        val account = account(0, 0)
        jdbc.sql("insert into stage_progress(account_id,stage_id,unlocked,highest_clear_count,content_version) values (:account,'stage.01-05',true,0,'test')")
            .param("account", account).update()
        assertFalse(gems.state(account).unlocked)

        jdbc.sql("update stage_progress set first_cleared_at=now(),highest_clear_count=1 where account_id=:account and stage_id='stage.01-05'")
            .param("account", account).update()
        assertTrue(gems.state(account).unlocked)
        assertEquals(3, gems.state(account).tickets)
    }
    @Test
    fun `unlocked gem state refreshes accrued tickets without timestamp binding failure`() {
        val account = account(0, 0)
        unlock(account)
        gems.state(account)
        jdbc.sql("update gem_account_state set tickets=0,last_ticket_at=now() - interval '16 hours' where account_id=:account")
            .param("account", account).update()

        val refreshed = gems.state(account)

        assertTrue(refreshed.unlocked)
        assertEquals(3, refreshed.tickets)
        assertTrue(jdbc.sql("select date_trunc('hour',last_ticket_at)=last_ticket_at from gem_account_state where account_id=:account")
            .param("account", account).query(Boolean::class.java).single())
    }


    @Test
    fun `opening many identical gems uses capped groups and keeps individual identities`() {
        val account = account(100, 0)
        repeat(100) { gems.openBoxes(account, UUID.randomUUID(), 1) }
        assertEquals(100, count(account, "gem_instance"))
        assertEquals(100, count(account, "inventory_instance"))
        val expected = jdbc.sql("select sum((n-1)/99+1) from (select count(*) n from gem_instance where account_id=:a group by level,option,value) groups")
            .param("a", account).query(Int::class.java).single()
        assertEquals(expected, inventory.usedSlotCount(account))
    }

    @Test
    fun `all possible box outcomes fit existing gem groups at capacity`() {
        val account = account(2, 193)
        for (level in 1..3) for (option in listOf("FLAT_ATTACK", "FLAT_HP")) {
            val value = com.hanjjak.gems.domain.GemRules.fixedValue(level, com.hanjjak.gems.domain.GemOption.valueOf(option))
            jdbc.sql("insert into inventory_instance(instance_id,account_id,item_id,reserved_for_sale,acquired_sequence,stack_key,inventory_slot_id) values (gen_random_uuid(),:a,:item,false,300,:key,gen_random_uuid())")
                .params(mapOf("a" to account, "item" to "gem:$level:${option.lowercase()}", "key" to "$level:$option:$value")).update()
        }
        assertEquals(200, inventory.usedSlotCount(account))
        gems.openBoxes(account, UUID.randomUUID(), 1)
        assertEquals(200, inventory.usedSlotCount(account))
        assertEquals(1, boxQuantity(account))
    }

    @Test
    fun `material transfer crossing limit rejects atomically for single and batch`() {
        val seller = account(0, 0)
        val buyer = account(0, 199)
        reservations.grantStackOrReject(seller, "POTATO_M1", 10)
        market.reserveForSale(seller, "POTATO_M1", 10)
        reservations.grantStackOrReject(buyer, "POTATO_M1", 999)
        assertThrows(IllegalArgumentException::class.java) { market.transferReserved(seller, buyer, "POTATO_M1", 1) }
        assertThrows(IllegalArgumentException::class.java) {
            market.transferReserveds(listOf(com.hanjjak.inventory.application.MarketInventoryService.ReservedTransfer(seller, buyer, "POTATO_M1", 1)))
        }
        assertEquals(0, inventory.lockStack(seller, "POTATO_M1")!!.quantity)
        assertEquals(0, inventory.lockStack(seller, "POTATO_M1")!!.reservedQuantity)
        assertEquals(999, inventory.lockStack(buyer, "POTATO_M1")!!.quantity)
    }

    @Test
    fun `combine at capacity rejects when consuming a partial group cannot release a slot`() {
        val account = account(4, 0)
        unlock(account)
        repeat(4) { gems.openBoxes(account, UUID(0, 0x10000L + it), 1) }
        jdbc.sql("update gem_instance set level=1,option='FLAT_ATTACK',value=3,locked=false where account_id=:a").param("a", account).update()
        jdbc.sql("update inventory_instance set item_id='gem:1:flat_attack',stack_key='1:FLAT_ATTACK:3' where account_id=:a").param("a", account).update()
        jdbc.sql("insert into inventory_instance(instance_id,account_id,item_id,reserved_for_sale,acquired_sequence) select gen_random_uuid(),:a,'gem:1:flat_attack',false,n from generate_series(1,199) n").param("a", account).update()
        val ids = jdbc.sql("select gem_id from gem_instance where account_id=:a limit 3").param("a", account).query(UUID::class.java).list()
        val version = inventory.stateVersion(account)
        assertThrows(IllegalArgumentException::class.java) { gems.fuse(account, UUID.randomUUID(), GemFusionExecuteRequest(GemFusionMode.MANUAL, ids)) }
        assertEquals(4, count(account, "gem_instance"))
        assertEquals(version, inventory.stateVersion(account))
    }

    @Test
    fun `query returns same type gems individually and detail keeps type totals`() {
        val account = account(0, 3)
        val page = queries.list(account, com.hanjjak.inventory.domain.ItemCategory.GEM, com.hanjjak.inventory.domain.ItemSort.NAME_ASC, null, 10)
        assertEquals(3, page.items.size)
        assertEquals(3, page.items.map { it.instanceId }.distinct().size)
        assertTrue(page.items.all { it.instanceId != null && it.totalQuantity == 1L })
        assertEquals(3, queries.detail(account, "gem:1:flat_attack").totalQuantity)
    }

    @Test
    fun `active stack right retains consumed box slot and grants into it at capacity`() {
        val account = account(1, 198)
        val challenge = UUID.randomUUID()
        jdbc.sql("""insert into gem_dungeon_challenge(challenge_id,account_id,boss_type,stage,kst_date,status,seed,content_version,preset_snapshot,player_snapshot,battle_result,reward_gem_boxes,started_at,minimum_complete_at,expires_at)
            values (:c,:a,'SURVIVAL',1,current_date,'ACTIVE',1,'test','{}','{}','{}',10,now(),now(),now()+interval '1 hour')""")
            .params(mapOf("c" to challenge, "a" to account)).update()
        jdbc.sql("insert into inventory_capacity_reservation values (gen_random_uuid(),:c,:a,'GEM_BOX','STACK_RIGHT','ACTIVE',now(),null)")
            .params(mapOf("c" to challenge, "a" to account)).update()
        gems.openBoxes(account, UUID.randomUUID(), 1)
        assertEquals(200, inventory.usedSlotCount(account))
        assertEquals(200, queries.status(account).usedSlots)
        assertThrows(IllegalArgumentException::class.java) { reservations.grantStackOrReject(account, "POTATO_M1", 1) }
        reservations.grantStackOrReject(account, "GEM_BOX", 10)
        assertEquals(200, inventory.usedSlotCount(account))
        assertEquals(10, boxQuantity(account))
    }

    @Test
    fun `opening last box at capacity replaces its slot and replay does not duplicate`() {
        val account = account(1, 199)
        val key = UUID.randomUUID()
        gems.openBoxes(account, key, 1)
        gems.openBoxes(account, key, 1)
        assertEquals(200, inventory.usedSlotCount(account))
        assertEquals(1, count(account, "gem_instance"))
        assertEquals(0, boxQuantity(account))
    }

    @Test
    fun `insufficient net capacity rejects before consumption and command recording`() {
        val account = account(2, 199)
        val version = inventory.stateVersion(account)
        val error = assertThrows(IllegalArgumentException::class.java) { gems.openBoxes(account, UUID.randomUUID(), 2) }
        assertEquals("INVENTORY_CAPACITY_EXCEEDED", error.message)
        assertEquals(2, boxQuantity(account))
        assertEquals(0, count(account, "gem_instance"))
        assertEquals(0, count(account, "gem_command_record"))
        assertEquals(version, inventory.stateVersion(account))
    }

    @Test
    fun `combine consumes three matching inventory instances and grants one`() {
        val account = account(3, 0)
        unlock(account)
        gems.openBoxes(account, UUID(0, 0), 3)
        val ids = jdbc.sql("select gem_id from gem_instance where account_id=:account").param("account", account).query(UUID::class.java).list()
        jdbc.sql("update gem_instance set level=1,option='FLAT_ATTACK',locked=false where account_id=:account").param("account", account).update()
        jdbc.sql("update inventory_instance set item_id='gem:1:flat_attack' where account_id=:account").param("account", account).update()
        gems.fuse(account, UUID.randomUUID(), GemFusionExecuteRequest(GemFusionMode.MANUAL, ids))
        assertEquals(1, inventory.usedSlotCount(account))
        assertEquals(1, count(account, "gem_instance"))
        assertEquals(1, count(account, "inventory_instance"))
    }

    @Test
    fun `combine retires imported orphan gem rows without instance not available`() {
        val account = account(0, 0)
        unlock(account)
        val ids = List(3) { UUID.randomUUID() }
        ids.forEach { id ->
            jdbc.sql("insert into gem_instance(gem_id,account_id,level,option,value,locked,content_version,created_at) values (:gem,:account,1,'FLAT_ATTACK',3,false,'test',now())")
                .params(mapOf("gem" to id, "account" to account)).update()
        }

        val result = gems.fuse(account, UUID.randomUUID(), GemFusionExecuteRequest(GemFusionMode.SAFE_BATCH, ids))

        assertEquals(1, result.granted.size)
        assertEquals(1, count(account, "gem_instance"))
        assertEquals(1, count(account, "inventory_instance"))
    }

    @Test
    fun `slot lock covers all members and replay preserves other slots`() {
        val account = account(5, 0)
        repeat(3) { gems.openBoxes(account, UUID(0, 0x10000L + it), 1) }
        val before = inventory.instances(account)
        val slot = before.first().inventorySlotId!!
        assertEquals(1, before.map { it.inventorySlotId }.distinct().size)
        val key = UUID.randomUUID()
        gems.lockSlot(account, key, slot, true)
        val version = inventory.stateVersion(account)
        gems.lockSlot(account, key, slot, true)
        assertEquals(version, inventory.stateVersion(account))
        assertTrue(inventory.instances(account).all { it.locked })
        assertTrue(gems.state(account).gems.all { it.locked })
        assertThrows(IllegalArgumentException::class.java) {
            gems.fuse(account, UUID.randomUUID(), GemFusionExecuteRequest(GemFusionMode.MANUAL, before.map { it.instanceId }))
        }
        gems.openBoxes(account, UUID(0, 0x10003L), 1)
        assertEquals(2, inventory.instances(account).map { it.inventorySlotId }.distinct().size)
        assertEquals(1, inventory.instances(account).count { !it.locked })
        val other = account(0, 0)
        assertThrows(IllegalArgumentException::class.java) { gems.lockSlot(other, UUID.randomUUID(), slot, false) }
        gems.lockSlot(account, UUID.randomUUID(), slot, false)
        assertTrue(inventory.instances(account).none { it.locked })
        assertEquals(2, inventory.instances(account).map { it.inventorySlotId }.distinct().size)
    }

    @Test
    fun `safe batch preview excludes locked equipped and listed gems then fuses atomically`() {
        val account = account(9, 0)
        unlock(account)
        repeat(9) { gems.openBoxes(account, UUID(0, 0x20000L + it), 1) }
        val all = jdbc.sql("select gem_id from gem_instance where account_id=:account order by gem_id").param("account", account).query(UUID::class.java).list()
        jdbc.sql("update gem_instance set level=2,option='FLAT_ATTACK',value=7,locked=false where account_id=:account").param("account", account).update()
        jdbc.sql("update inventory_instance set item_id='gem:2:flat_attack',stack_key='2:FLAT_ATTACK:7',locked=false where account_id=:account").param("account", account).update()
        jdbc.sql("update gem_instance set locked=true where gem_id=:gem").param("gem", all[0]).update()
        jdbc.sql("update inventory_instance set locked=true where instance_id=:gem").param("gem", all[0]).update()
        jdbc.sql("update inventory_instance set reserved_for_sale=true where instance_id=:gem").param("gem", all[1]).update()
        jdbc.sql("insert into gem_loadout(account_id,preset,slot_index,gem_id) values (:account,'SURVIVAL',1,:gem)").params(mapOf("account" to account, "gem" to all[2])).update()

        val preview = gems.previewFusion(account, GemFusionPreviewRequest(GemFusionMode.SAFE_BATCH, 2, selections = listOf(GemFusionSelection(GemOption.FLAT_ATTACK, 6))))
        assertEquals(6, preview.consumedGemIds.size)
        assertTrue(preview.consumedGemIds.none { it in all.take(3) })
        assertEquals(2, preview.fusionCount)
        val result = gems.fuse(account, UUID.randomUUID(), GemFusionExecuteRequest(GemFusionMode.SAFE_BATCH, preview.consumedGemIds))
        assertEquals(2, result.granted.size)
        assertEquals(5, count(account, "gem_instance"))
    }

    @Test
    fun `safe batch cascade commits every level in one transaction`() {
        val account = account(5, 0)
        unlock(account)
        repeat(5) { gems.openBoxes(account, UUID(0, 0x21000L + it), 1) }
        val all = jdbc.sql("select gem_id from gem_instance where account_id=:account order by gem_id").param("account", account).query(UUID::class.java).list()
        jdbc.sql("update gem_instance set level=1,option='FLAT_ATTACK',value=3,locked=false where account_id=:account").param("account", account).update()
        jdbc.sql("update inventory_instance set item_id='gem:1:flat_attack',stack_key='1:FLAT_ATTACK:3',locked=false where account_id=:account").param("account", account).update()
        jdbc.sql("update gem_instance set level=2,option='FLAT_HP',value=7 where gem_id in (:ids)").param("ids", all.take(2)).update()
        jdbc.sql("update inventory_instance set item_id='gem:2:flat_hp',stack_key='2:FLAT_HP:7' where instance_id in (:ids)").param("ids", all.take(2)).update()

        val result = gems.fuse(account, UUID.randomUUID(), GemFusionExecuteRequest(GemFusionMode.SAFE_BATCH, targetLevel = 3, allowedOptions = listOf(GemOption.FLAT_ATTACK, GemOption.FLAT_HP)))

        assertEquals(5, result.consumedGemIds.size)
        assertEquals(1, result.granted.size)
        assertEquals(3, result.granted.single().level)
        assertEquals(1, count(account, "gem_instance"))
        assertEquals(1, count(account, "inventory_instance"))
    }

    @Test
    fun `active dungeon locks only its boss preset and main update aborts active battle`() {
        val account = account(1, 0)
        unlock(account)
        val gem = gems.openBoxes(account, UUID(0, 0x30000L), 1).granted.single().gemId
        val challenge = UUID.randomUUID()
        jdbc.sql("""insert into gem_dungeon_challenge(challenge_id,account_id,boss_type,stage,kst_date,status,seed,content_version,preset_snapshot,player_snapshot,battle_result,reward_gem_boxes,started_at,minimum_complete_at,expires_at)
            values (:challenge,:account,'SURVIVAL',1,current_date,'ACTIVE',1,'test','{}','{}','{}',0,now(),now(),now()+interval '1 hour')""").params(mapOf("challenge" to challenge, "account" to account)).update()
        assertEquals(setOf(GemPreset.SURVIVAL), gems.state(account).lockedPresets)
        assertThrows(IllegalArgumentException::class.java) { gems.updatePreset(account, UUID.randomUUID(), GemPreset.SURVIVAL, GemPresetRequest(listOf(gem))) }

        val gameSession = UUID.randomUUID()
        jdbc.sql("insert into game_session(id,account_id,status,created_at,last_heartbeat_at) values (:session,:account,'ACTIVE',now(),now())").params(mapOf("session" to gameSession, "account" to account)).update()
        jdbc.sql("insert into battle_session(id,account_id,game_session_id,token_hash,status,stage_id,content_version,seed,input_json,started_at,last_heartbeat_at,completable_at) values (gen_random_uuid(),:account,:gameSession,'token','ACTIVE','stage.03-01','test',1,'{}',now(),now(),now()+interval '1 minute')").params(mapOf("account" to account, "gameSession" to gameSession)).update()
        val result = gems.updatePreset(account, UUID.randomUUID(), GemPreset.MAIN, GemPresetRequest(listOf(gem)))
        assertTrue(result.mainBattleRestarted)
        assertEquals("ABORTED", jdbc.sql("select status from battle_session where account_id=:account").param("account", account).query(String::class.java).single())
    }

    @Test
    fun `market deposit frees slots and failed recovery leaves inventory unchanged`() {
        val seller = account(0, 199)
        reservations.grantStackOrReject(seller, "POTATO_M1", 999)
        assertEquals(200, inventory.usedSlotCount(seller))
        market.reserveForSale(seller, "POTATO_M1", 999)
        assertEquals(199, inventory.usedSlotCount(seller))
        reservations.grantStackOrReject(seller, "GEM_BOX", 1)
        val version = inventory.stateVersion(seller)
        assertThrows(IllegalArgumentException::class.java) { market.releaseReservation(seller, "POTATO_M1", 999) }
        assertEquals(0, inventory.lockStack(seller, "POTATO_M1")!!.quantity)
        assertEquals(version, inventory.stateVersion(seller))
        val buyer = account(0, 0)
        market.transferReserved(seller, buyer, "POTATO_M1", 10)
        assertEquals(10, inventory.lockStack(buyer, "POTATO_M1")!!.quantity)
        assertEquals(0, inventory.lockStack(seller, "POTATO_M1")!!.quantity)
    }

    private fun account(boxes: Int, occupied: Int): UUID {
        val account = UUID.randomUUID()
        jdbc.sql("insert into account(id,email,password_hash,state_version,created_at) values (:a,:email,'test',1,now())")
            .params(mapOf("a" to account, "email" to "$account@test.local")).update()
        jdbc.sql("insert into inventory_stack values (:a,'GEM_BOX',:q,0,1)").params(mapOf("a" to account,"q" to boxes)).update()
        if (occupied > 0) jdbc.sql("insert into inventory_instance select gen_random_uuid(),:a,'gem:1:flat_attack',false,n+1 from generate_series(1,:n) n")
            .params(mapOf("a" to account,"n" to occupied)).update()
        return account
    }
    private fun unlock(account: UUID) {
        jdbc.sql("insert into stage_progress(account_id,stage_id,unlocked,first_cleared_at,highest_clear_count,content_version) values (:account,'stage.01-05',true,now(),1,'test') on conflict(account_id,stage_id) do update set unlocked=true,first_cleared_at=coalesce(stage_progress.first_cleared_at,excluded.first_cleared_at),highest_clear_count=greatest(stage_progress.highest_clear_count,1)")
            .param("account", account).update()
    }
    private fun count(account: UUID, table: String): Long = jdbc.sql("select count(*) from $table where account_id=:a").param("a",account).query(Long::class.java).single()
    private fun boxQuantity(account: UUID): Long = jdbc.sql("select quantity from inventory_stack where account_id=:a and item_id='GEM_BOX'").param("a",account).query(Long::class.java).single()

}
