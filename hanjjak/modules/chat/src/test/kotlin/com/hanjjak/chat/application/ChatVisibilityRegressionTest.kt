package com.hanjjak.chat.application

import com.hanjjak.chat.domain.ChatMessage
import java.time.Instant
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals

class ChatVisibilityRegressionTest {
    @Test
    fun `keeps own messages and removes only blocked players`() {
        val me = UUID.randomUUID()
        val allowed = UUID.randomUUID()
        val blocked = UUID.randomUUID()
        val rows = listOf(
            message(me, "mine"),
            message(allowed, "allowed"),
            message(blocked, "blocked"),
        )

        val visible = visibleChatMessages(rows, me) { it == blocked }

        assertEquals(listOf("mine", "allowed"), visible.map(ChatMessage::body))
    }

    private fun message(accountId: UUID, body: String) = ChatMessage(
        messageId = UUID.randomUUID(),
        accountId = accountId,
        nickname = body,
        body = body,
        createdAt = Instant.parse("2026-09-11T00:00:00Z"),
        eventId = UUID.randomUUID(),
    )
}
