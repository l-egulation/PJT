package com.hanjjak.cosmetics.infrastructure

import com.hanjjak.cosmetics.application.CosmeticsRepository
import com.hanjjak.cosmetics.domain.CosmeticSlot
import com.hanjjak.cosmetics.domain.CosmeticState
import com.hanjjak.cosmetics.domain.EquipmentSlot
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.stereotype.Repository
import java.util.UUID

@Repository
class JdbcCosmeticsRepository(private val jdbc: JdbcClient) : CosmeticsRepository {
    override fun stateVersion(accountId: UUID): Long = jdbc.sql("select state_version from account where id=:account")
        .param("account", accountId).query(Long::class.java).single()
    override fun findStates(accountId: UUID): List<CosmeticState> = jdbc.sql("select account_id,cosmetic_id,registered_quantity,unregistered_quantity,reserved_quantity from cosmetic_collection_state where account_id=:account")
        .param("account", accountId).query(CosmeticState::class.java).list()


    override fun lockAccount(accountId: UUID) {
        jdbc.sql("select id from account where id=:account for update").param("account", accountId).query(UUID::class.java).single()
    }
    override fun findStateForUpdate(accountId: UUID, cosmeticId: String): CosmeticState? = jdbc.sql("select account_id,cosmetic_id,registered_quantity,unregistered_quantity,reserved_quantity from cosmetic_collection_state where account_id=:account and cosmetic_id=:cosmetic for update")
        .param("account", accountId).param("cosmetic", cosmeticId).query(CosmeticState::class.java).optional().orElse(null)

    override fun saveState(state: CosmeticState) {
        jdbc.sql("insert into cosmetic_collection_state(account_id,cosmetic_id,registered_quantity,unregistered_quantity,reserved_quantity) values (:account,:cosmetic,:registered,:unregistered,:reserved) on conflict(account_id,cosmetic_id) do update set registered_quantity=excluded.registered_quantity,unregistered_quantity=excluded.unregistered_quantity,reserved_quantity=excluded.reserved_quantity")
            .params(mapOf("account" to state.accountId,"cosmetic" to state.cosmeticId,"registered" to state.registeredQuantity,"unregistered" to state.unregisteredQuantity,"reserved" to state.reservedQuantity)).update()
    }

    override fun findEquipment(accountId: UUID): List<EquipmentSlot> = jdbc.sql("select account_id,slot,cosmetic_id from cosmetic_equipment where account_id=:account")
        .param("account", accountId).query { row, _ -> EquipmentSlot(row.getObject("account_id", UUID::class.java), CosmeticSlot.valueOf(row.getString("slot")), row.getString("cosmetic_id")) }.list()

    override fun saveEquipment(accountId: UUID, slot: CosmeticSlot, cosmeticId: String?) {
        jdbc.sql("insert into cosmetic_equipment(account_id,slot,cosmetic_id) values (:account,:slot,:cosmetic) on conflict(account_id,slot) do update set cosmetic_id=excluded.cosmetic_id")
            .param("account", accountId).param("slot", slot.name).param("cosmetic", cosmeticId).update()
    }
}
