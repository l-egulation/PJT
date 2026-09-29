package com.hanjjak.chat.application

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith

class ChatModerationTest {
    private val moderation = ChatModeration()

    @Test
    fun `blocks normalized profanity before persistence`() {
        assertEquals("BLOCKED_POLICY", moderation.check("씨 발").code)
        assertEquals(false, moderation.check("씨 발").allowed)
    }

    @Test
    fun `allows ordinary marketplace text`() {
        assertEquals("ALLOW", moderation.check("감자 재료 10개 판매합니다").code)
    }

    @Test
    fun `rejects empty and oversized messages`() {
        assertFailsWith<IllegalArgumentException> { moderation.check(" ") }
        assertFailsWith<IllegalArgumentException> { moderation.check("a".repeat(241)) }
    }
}
