package com.hanjjak.gameapi

import com.hanjjak.cosmetics.application.DrawEntropy
import com.hanjjak.cosmetics.application.HmacDrawEntropy
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import java.util.Base64

@Configuration
class CosmeticGachaEntropyConfiguration {
    @Bean
    fun cosmeticGachaEntropy(
        @org.springframework.beans.factory.annotation.Value("\${hanjjak.cosmetics.gacha-hmac-secret:}") encodedSecret: String,
        @org.springframework.beans.factory.annotation.Value("\${hanjjak.cosmetics.gacha-hmac-key-id:}") keyId: String,
    ): DrawEntropy {
        val secret = try {
            if (encodedSecret.isBlank()) null else Base64.getDecoder().decode(encodedSecret)
        } catch (_: IllegalArgumentException) {
            null
        }
        return if (secret == null || secret.size < 32 || keyId.isBlank()) HmacDrawEntropy.unavailable() else HmacDrawEntropy(secret, keyId)
    }
}
