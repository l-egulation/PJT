package com.hanjjak.chat.application

import java.text.Normalizer

class ChatModeration(
    private val blockedTerms: Set<String> = DEFAULT_BLOCKED_TERMS,
) {
    data class Decision(val allowed: Boolean, val code: String = "ALLOW")

    fun check(value: String): Decision {
        val normalized = Normalizer.normalize(value, Normalizer.Form.NFKC)
            .lowercase()
            .replace(Regex("[\\s\\p{Punct}]+"), "")
        require(value.trim().isNotEmpty()) { "CHAT_EMPTY_BODY" }
        require(value.length <= 240) { "CHAT_BODY_TOO_LONG" }
        return if (blockedTerms.any(normalized::contains)) Decision(false, "BLOCKED_POLICY") else Decision(true)
    }

    companion object {
        val DEFAULT_BLOCKED_TERMS = setOf(
            "시발", "씨발", "ㅅㅂ", "병신", "개새끼", "좆", "보지", "자지", "sex", "nigger",
        )
    }
}
