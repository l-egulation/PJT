package com.hanjjak.chat.api

import com.fasterxml.jackson.databind.JsonNode
import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.chat.application.ChatRepository
import com.hanjjak.chat.domain.ChatMessage
import org.springframework.scheduling.annotation.Scheduled
import org.springframework.context.annotation.Configuration
import org.springframework.web.socket.TextMessage
import org.springframework.web.socket.CloseStatus
import org.springframework.web.socket.WebSocketHandler
import org.springframework.web.socket.WebSocketSession
import org.springframework.web.socket.config.annotation.EnableWebSocket
import org.springframework.web.socket.config.annotation.WebSocketConfigurer
import org.springframework.web.socket.config.annotation.WebSocketHandlerRegistry
import org.springframework.web.socket.handler.TextWebSocketHandler
import org.springframework.web.socket.server.support.HttpSessionHandshakeInterceptor
import java.time.Clock
import java.time.Instant
import java.util.UUID
import java.util.concurrent.ConcurrentHashMap

@Configuration
@EnableWebSocket
class ChatWebSocketConfiguration(
    private val chatWebSocketHandler: ChatWebSocketHandler,
    @org.springframework.beans.factory.annotation.Value("\${hanjjak.api-security.allowed-origins:http://localhost:5173,http://127.0.0.1:5173}")
    private val allowedOrigins: List<String>,
) : WebSocketConfigurer {
    override fun registerWebSocketHandlers(registry: WebSocketHandlerRegistry) {
        registry.addHandler(chatWebSocketHandler, "/ws/chat")
            .addInterceptors(ChatSessionHandshakeInterceptor())
            .setAllowedOrigins(*allowedOrigins.toTypedArray())
    }
}

private class ChatSessionHandshakeInterceptor : HttpSessionHandshakeInterceptor() {
    override fun beforeHandshake(
        request: org.springframework.http.server.ServerHttpRequest,
        response: org.springframework.http.server.ServerHttpResponse,
        wsHandler: WebSocketHandler,
        attributes: MutableMap<String, Any>,
    ): Boolean {
        val session = (request as? org.springframework.http.server.ServletServerHttpRequest)?.servletRequest?.getSession(false)
        val accountId = session?.getAttribute("accountId") as? UUID ?: return false
        attributes["accountId"] = accountId
        return true
    }
}

@org.springframework.stereotype.Component
class ChatWebSocketHandler(
    private val objectMapper: ObjectMapper,
    private val repository: ChatRepository,
) : TextWebSocketHandler() {
    private val sessions = ConcurrentHashMap<String, WebSocketSession>()

    override fun afterConnectionEstablished(session: WebSocketSession) {
        if (session.attributes["accountId"] !is UUID) {
            session.close(CloseStatus.POLICY_VIOLATION)
            return
        }
        sessions[session.id] = session
        session.sendMessage(TextMessage(objectMapper.writeValueAsString(mapOf("type" to "ready"))))
    }

    override fun afterConnectionClosed(session: WebSocketSession, status: CloseStatus) {
        sessions.remove(session.id)
    }

    override fun handleTransportError(session: WebSocketSession, exception: Throwable) {
        sessions.remove(session.id)
        if (session.isOpen) session.close(CloseStatus.SERVER_ERROR)
    }

    fun broadcast(message: ChatMessage) {
        val payload = TextMessage(objectMapper.writeValueAsString(mapOf("type" to "message", "message" to message)))
        sessions.values.toList().forEach { session ->
            val accountId = session.attributes["accountId"] as? UUID
            if (accountId == null || repository.isBlocked(accountId, message.accountId)) return@forEach
            runCatching { if (session.isOpen) session.sendMessage(payload) }
                .onFailure { sessions.remove(session.id) }
        }
    }
}

@org.springframework.stereotype.Component
class ChatOutboxWebSocketRelay(
    private val jdbc: org.springframework.jdbc.core.simple.JdbcClient,
    private val objectMapper: ObjectMapper,
    private val repository: ChatRepository,
    private val handler: ChatWebSocketHandler,
    private val clock: Clock,
) {
    @Volatile
    private var cursor: Instant = clock.instant()
    private val delivered = java.util.Collections.newSetFromMap(ConcurrentHashMap<UUID, Boolean>())

    @Scheduled(fixedDelayString = "\${hanjjak.chat.websocket-relay-delay-ms:500}")
    fun relay() {
        val pollStarted = clock.instant()
        val rows = jdbc.sql("select event_id,payload from outbox_event where event_type='CHAT_MESSAGE_CREATED' and created_at >= :cursor order by created_at asc,event_id asc limit 500")
            .param("cursor", java.sql.Timestamp.from(cursor))
            .query { row, _ -> row.getObject("event_id", UUID::class.java) to row.getString("payload") }
            .list()
        cursor = pollStarted
        rows.forEach { (eventId, payload) ->
            if (!delivered.add(eventId)) return@forEach
            val messageId = readMessageId(payload) ?: return@forEach
            repository.findMessage(messageId)?.let(handler::broadcast)
        }
        if (delivered.size > 4096) delivered.clear()
    }

    private fun readMessageId(payload: String): UUID? = runCatching {
        val node: JsonNode = objectMapper.readTree(payload).path("payload").path("messageId")
        UUID.fromString(node.asText())
    }.getOrNull()
}
