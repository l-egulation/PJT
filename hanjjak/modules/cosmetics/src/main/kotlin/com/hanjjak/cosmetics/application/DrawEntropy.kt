package com.hanjjak.cosmetics.application

import java.io.ByteArrayOutputStream
import java.nio.ByteBuffer
import java.nio.charset.StandardCharsets
import java.security.SecureRandom
import java.util.Base64
import java.util.UUID
import javax.crypto.Mac
import javax.crypto.spec.SecretKeySpec

interface DrawEntropy {
    val isAvailable: Boolean
    fun issue(accountId: UUID, bannerId: String, count: Int, contentVersion: String): Issued

    data class Issued(
        val algorithmVersion: String,
        val keyId: String,
        val reproductionToken: String,
        val random: DrawRandom,
    )
}

class HmacDrawEntropy(
    private val secret: ByteArray?,
    private val keyId: String?,
    private val nonceSource: () -> ByteArray = { ByteArray(NONCE_BYTES).also(SecureRandom()::nextBytes) },
) : DrawEntropy {
    override val isAvailable: Boolean = secret != null && secret.size >= MIN_SECRET_BYTES && !keyId.isNullOrBlank()

    override fun issue(accountId: UUID, bannerId: String, count: Int, contentVersion: String): DrawEntropy.Issued {
        check(isAvailable) { "GACHA_CONTENT_UNAVAILABLE" }
        val nonce = nonceSource()
        require(nonce.size == NONCE_BYTES) { "draw nonce must be 32 bytes" }
        val nonceText = Base64.getUrlEncoder().withoutPadding().encodeToString(nonce)
        val message = canonicalFields(ALGORITHM_VERSION, accountId.toString(), bannerId, count.toString(), contentVersion, nonceText)
        val tokenBytes = hmac(secret!!, message)
        return DrawEntropy.Issued(ALGORITHM_VERSION, keyId!!, Base64.getUrlEncoder().withoutPadding().encodeToString(tokenBytes), DrawRandom(tokenBytes))
    }

    companion object {
        const val ALGORITHM_VERSION = "HMAC-SHA256-V1"
        private const val MIN_SECRET_BYTES = 32
        private const val NONCE_BYTES = 32

        fun unavailable(): HmacDrawEntropy = HmacDrawEntropy(null, null)

        private fun canonicalFields(vararg values: String): ByteArray {
            val output = ByteArrayOutputStream()
            values.forEach { value ->
                val bytes = value.toByteArray(StandardCharsets.UTF_8)
                output.write(ByteBuffer.allocate(4).putInt(bytes.size).array())
                output.write(bytes)
            }
            return output.toByteArray()
        }

        private fun hmac(secret: ByteArray, message: ByteArray): ByteArray {
            val mac = Mac.getInstance("HmacSHA256")
            mac.init(SecretKeySpec(secret, "HmacSHA256"))
            return mac.doFinal(message)
        }
    }
}

class DrawRandom(token: ByteArray) {
    private val token = token.clone().also { require(it.size == 32) { "reproduction token must decode to 32 bytes" } }
    private var counter = 0L
    private var block = ByteArray(0)
    private var offset = 0

    fun nextInt(bound: Int): Int {
        require(bound > 0) { "bound must be positive" }
        val upperBound = 1L shl 32
        val limit = upperBound - upperBound % bound
        while (true) {
            val value = nextUnsignedInt()
            if (value < limit) return (value % bound).toInt()
        }
    }

    private fun nextUnsignedInt(): Long {
        val bytes = ByteArray(4) { nextByte() }
        return ((bytes[0].toLong() and 0xff) shl 24) or
            ((bytes[1].toLong() and 0xff) shl 16) or
            ((bytes[2].toLong() and 0xff) shl 8) or
            (bytes[3].toLong() and 0xff)
    }

    private fun nextByte(): Byte {
        if (offset == block.size) {
            val counterBytes = ByteBuffer.allocate(8).putLong(counter++).array()
            val mac = Mac.getInstance("HmacSHA256")
            mac.init(SecretKeySpec(token, "HmacSHA256"))
            block = mac.doFinal(counterBytes)
            offset = 0
        }
        return block[offset++]
    }

    companion object {
        fun fromReproductionToken(token: String): DrawRandom {
            val bytes = try { Base64.getUrlDecoder().decode(token) } catch (_: IllegalArgumentException) { throw IllegalArgumentException("invalid reproduction token") }
            return DrawRandom(bytes)
        }
    }
}
