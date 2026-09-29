package com.hanjjak.cosmetics.application

import java.util.Base64
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertNotEquals
import kotlin.test.assertTrue

class DrawEntropyTest {
    private val account = UUID.fromString("11111111-1111-1111-1111-111111111111")
    private val secret = ByteArray(32) { (it + 1).toByte() }
    private val nonce = ByteArray(32) { (it + 41).toByte() }

    private fun entropy(nonceValue: ByteArray = nonce): HmacDrawEntropy = HmacDrawEntropy(
        secret = secret,
        keyId = "test-key",
        nonceSource = { nonceValue.clone() },
    )

    @Test
    fun `same canonical inputs produce same token and random stream`() {
        val first = entropy().issue(account, "cosmetic-banner-01", 10, "cosmetics-v2-placeholder")
        val second = entropy().issue(account, "cosmetic-banner-01", 10, "cosmetics-v2-placeholder")

        assertEquals("HMAC-SHA256-V1", first.algorithmVersion)
        assertEquals("test-key", first.keyId)
        assertEquals(first.reproductionToken, second.reproductionToken)
        assertEquals(List(20) { first.random.nextInt(1_000_000) }, List(20) { second.random.nextInt(1_000_000) })
    }

    @Test
    fun `canonical field order and length encoding matches fixed HMAC vector`() {
        val issued = entropy().issue(account, "cosmetic-banner-01", 10, "cosmetics-v2-placeholder")
        assertEquals("U46gHyPgpj4xEaDLtMQNWDxpITZJntAPHxX6T4faFNo", issued.reproductionToken)
    }

    @Test
    fun `nextInt rejects an out of range first candidate before accepting next candidate`() {
        val token = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAADLk"
        assertEquals(764_997, DrawRandom.fromReproductionToken(token).nextInt(1_000_000))
    }

    @Test
    fun `each entropy input and server nonce changes reproduction token`() {
        val cases = listOf(
            entropy().issue(account, "cosmetic-banner-01", 10, "cosmetics-v2-placeholder"),
            entropy().issue(UUID.fromString("22222222-2222-2222-2222-222222222222"), "cosmetic-banner-01", 10, "cosmetics-v2-placeholder"),
            entropy().issue(account, "cosmetic-banner-02", 10, "cosmetics-v2-placeholder"),
            entropy().issue(account, "cosmetic-banner-01", 1, "cosmetics-v2-placeholder"),
            entropy().issue(account, "cosmetic-banner-01", 10, "cosmetics-v3-placeholder"),
            entropy(ByteArray(32) { (it + 99).toByte() }).issue(account, "cosmetic-banner-01", 10, "cosmetics-v2-placeholder"),
        )
        assertEquals(cases.size, cases.map { it.reproductionToken }.distinct().size)
    }

    @Test
    fun `random stream uses rejection sampling for awkward bounds`() {
        val token = Base64.getUrlEncoder().withoutPadding().encodeToString(ByteArray(32) { 0x7f })
        for (bound in listOf(3, 7, 1_000_000)) {
            val random = DrawRandom.fromReproductionToken(token)
            repeat(10_000) { assertTrue(random.nextInt(bound) in 0 until bound) }
        }
    }

    @Test
    fun `invalid server configuration is unavailable`() {
        assertFalse(HmacDrawEntropy(ByteArray(31), "test-key", { nonce.clone() }).isAvailable)
        assertFalse(HmacDrawEntropy(secret, " ", { nonce.clone() }).isAvailable)
    }

    @Test
    fun `entropy issue has no idempotency key parameter`() {
        val issue = DrawEntropy::class.java.methods.single { it.name == "issue" }
        assertEquals(4, issue.parameterCount)
        assertNotEquals(UUID::class.java, issue.parameterTypes.last())
    }
}
