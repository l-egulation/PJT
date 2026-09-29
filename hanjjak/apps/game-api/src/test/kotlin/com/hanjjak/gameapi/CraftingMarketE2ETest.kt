package com.hanjjak.gameapi

import com.fasterxml.jackson.databind.JsonNode
import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.market.application.OrderBookService
import jakarta.servlet.http.Cookie
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.BeforeEach
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.http.MediaType
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.test.web.servlet.MockMvc
import org.springframework.test.web.servlet.MvcResult
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post
import org.springframework.test.web.servlet.result.MockMvcResultMatchers.status
import java.util.UUID

@GameApiIntegrationTest
class CraftingMarketE2ETest {
    @Autowired lateinit var mvc: MockMvc
    @Autowired lateinit var mapper: ObjectMapper
    @Autowired lateinit var jdbc: JdbcClient
    @Autowired lateinit var orders: OrderBookService

    @BeforeEach
    fun synchronizeMarketCatalog() {
        orders.synchronizeCatalog()
    }


    @Test
    fun `unified sell GTC and buy IOC settle at maker price`() {
        val seller = AccountSession().apply { signup("seller-${UUID.randomUUID()}@test.local", "seller"); grantItem("POTATO_M1", 5) }
        val buyer = AccountSession().apply { signup("buyer-${UUID.randomUUID()}@test.local", "buyer"); setWallet(1_000) }
        val instrumentId = instrumentId("POTATO_M1")
        val sell = seller.postJson("/api/v1/market/orders", mapOf("instrumentId" to instrumentId, "side" to "SELL", "quantity" to 5, "limitUnitPrice" to 90, "timeInForce" to "GTC"))
        assertEquals("ACTIVE", sell.at("/data/result/status").asText())
        val buy = buyer.postJson("/api/v1/market/orders", mapOf("instrumentId" to instrumentId, "side" to "BUY", "quantity" to 3, "limitUnitPrice" to 100, "timeInForce" to "IOC"))
        assertEquals(3, buy.at("/data/result/filledQuantity").asLong())
        assertEquals(90, buy.at("/data/result/fills/0/unitPrice").asLong())
        assertEquals(730, wallet(buyer.accountId))
        assertEquals(3, quantity(buyer.accountId, "POTATO_M1"))
        assertEquals(243, seller.postJson("/api/v1/mails/claim-all", emptyMap<String, String>()).at("/data/result/totalRiceAmount").asLong())
    }

    @Test
    fun `resting BUY GTC creates claimable delivery and unread sequence`() {
        val seller = AccountSession().apply { signup("reserve-seller-${UUID.randomUUID()}@test.local", "seller"); grantItem("POTATO_M2", 3) }
        val buyer = AccountSession().apply { signup("reserve-buyer-${UUID.randomUUID()}@test.local", "buyer"); setWallet(1_000) }
        val instrumentId = instrumentId("POTATO_M2")
        buyer.postJson("/api/v1/market/orders", mapOf("instrumentId" to instrumentId, "side" to "BUY", "quantity" to 3, "limitUnitPrice" to 100, "timeInForce" to "GTC"))
        assertEquals(700, wallet(buyer.accountId))
        seller.postJson("/api/v1/market/orders", mapOf("instrumentId" to instrumentId, "side" to "SELL", "quantity" to 3, "limitUnitPrice" to 80, "timeInForce" to "IOC"))
        val deliveries = buyer.getJson("/api/v1/market/deliveries/me?claimable=true")
        assertEquals(1, deliveries.at("/data/items").size())
        assertTrue(deliveries.at("/data/readThroughSequence").asLong() > 0)
        val deliveryId = deliveries.at("/data/items/0/deliveryId").asText()
        buyer.postJson("/api/v1/market/deliveries/$deliveryId/claim", emptyMap<String, String>())
        assertEquals(3, quantity(buyer.accountId, "POTATO_M2"))
        assertEquals(700, wallet(buyer.accountId))
    }

    @Test
    fun `price pages retain global other liquidity and explicit terminal cursor`() {
        val mine = AccountSession().apply { signup("depth-mine-${UUID.randomUUID()}@test.local", "mine"); grantItem("POTATO_M3", 2) }
        val other = AccountSession().apply { signup("depth-other-${UUID.randomUUID()}@test.local", "other"); grantItem("POTATO_M3", 7) }
        val instrument = instrumentId("POTATO_M3")
        mine.postJson("/api/v1/market/orders", mapOf("instrumentId" to instrument, "side" to "SELL", "quantity" to 2, "limitUnitPrice" to 100, "timeInForce" to "GTC"))
        other.postJson("/api/v1/market/orders", mapOf("instrumentId" to instrument, "side" to "SELL", "quantity" to 3, "limitUnitPrice" to 110, "timeInForce" to "GTC"))
        other.postJson("/api/v1/market/orders", mapOf("instrumentId" to instrument, "side" to "SELL", "quantity" to 4, "limitUnitPrice" to 120, "timeInForce" to "GTC"))
        val path = "/api/v1/market/instruments/$instrument/price-levels?side=SELL&limit=2"
        val first = mine.getJson(path).path("data")
        assertEquals(listOf(100L, 110L), first.path("items").map { it.path("unitPrice").asLong() })
        assertEquals(listOf(0L, 3L), first.path("items").map { it.path("cumulativeOtherQuantity").asLong() })
        assertEquals(2L, first.at("/items/0/myQuantity").asLong())
        assertEquals(110L, first.path("nextUnitPrice").asLong())
        val last = mine.getJson("$path&afterUnitPrice=${first.path("nextUnitPrice").asLong()}").path("data")
        assertEquals(listOf(120L), last.path("items").map { it.path("unitPrice").asLong() })
        assertEquals(9L, last.at("/items/0/cumulativeQuantity").asLong())
        assertEquals(7L, last.at("/items/0/cumulativeOtherQuantity").asLong())
        assertEquals(first.path("marketRevision"), last.path("marketRevision"))
        assertTrue(last.path("nextUnitPrice").isNull)
    }

    private fun instrumentId(itemId: String): String = jdbc.sql("select instrument_id from market_instrument where item_id=:item and status='ACTIVE'").param("item", itemId).query(UUID::class.java).single().toString()
    private fun wallet(accountId: UUID) = jdbc.sql("select balance from wallet_balance where account_id=:account").param("account", accountId).query(Long::class.java).single()
    private fun quantity(accountId: UUID, itemId: String) = jdbc.sql("select quantity from inventory_stack where account_id=:account and item_id=:item").params(mapOf("account" to accountId, "item" to itemId)).query(Long::class.java).optional().orElse(0)

    private inner class AccountSession {
        lateinit var accountId: UUID
        private val cookies = mutableListOf<Cookie>()
        fun signup(email: String, nickname: String) {
            accountId = UUID.fromString(postJson("/api/v1/auth/signup", mapOf("email" to email, "password" to "password123", "nickname" to nickname), false).at("/data/accountId").asText())
            postJson("/api/v1/material-preference", mapOf("materialType" to "POTATO"))
        }
        fun setWallet(balance: Long) { jdbc.sql("update wallet_balance set balance=:balance where account_id=:account").params(mapOf("balance" to balance, "account" to accountId)).update() }
        fun grantItem(itemId: String, quantity: Long) { jdbc.sql("insert into inventory_stack(account_id,item_id,quantity,reserved_quantity,acquired_sequence) values (:account,:item,:quantity,0,1) on conflict(account_id,item_id) do update set quantity=inventory_stack.quantity+excluded.quantity").params(mapOf("account" to accountId, "item" to itemId, "quantity" to quantity)).update() }
        fun postJson(path: String, body: Any, authenticated: Boolean = true): JsonNode {
            val request = post(path).header("Idempotency-Key", UUID.randomUUID().toString()).header("Origin", "http://localhost:5173").contentType(MediaType.APPLICATION_JSON).accept(MediaType.APPLICATION_JSON).content(mapper.writeValueAsString(body))
            if (cookies.isNotEmpty()) request.cookie(*cookies.toTypedArray())
            if (authenticated) request.sessionAttr("accountId", accountId)
            val result = mvc.perform(request).andExpect(status().isOk).andReturn()
            captureCookies(result)
            return mapper.readTree(result.response.contentAsString)
        }
        fun getJson(path: String): JsonNode { val request = get(path).sessionAttr("accountId", accountId).accept(MediaType.APPLICATION_JSON); if (cookies.isNotEmpty()) request.cookie(*cookies.toTypedArray()); return mapper.readTree(mvc.perform(request).andExpect(status().isOk).andReturn().response.contentAsString) }
        private fun captureCookies(result: MvcResult) { result.response.cookies.forEach { cookie -> cookies.removeIf { it.name == cookie.name }; cookies += cookie } }
    }

}
