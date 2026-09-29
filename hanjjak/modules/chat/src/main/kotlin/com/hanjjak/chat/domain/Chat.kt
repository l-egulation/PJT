package com.hanjjak.chat.domain

import java.time.Instant
import java.util.UUID

enum class ChatIntent { SELL, BUY }
enum class ChatPostStatus { ACTIVE, CLOSED, EXPIRED, DELETED }

data class ChatMessage(
    val messageId: UUID,
    val accountId: UUID,
    val nickname: String,
    val primaryMaterialType: String? = null,
    val body: String,
    val createdAt: Instant,
    val eventId: UUID,
)

data class ChatBoardPost(
    val postId: UUID,
    val accountId: UUID,
    val nickname: String,
    val intent: ChatIntent,
    val itemId: String,
    val quantity: Long,
    val unitPrice: Long?,
    val body: String,
    val status: ChatPostStatus,
    val createdAt: Instant,
    val expiresAt: Instant,
)

data class ChatPage(val items: List<ChatMessage>, val nextCursor: String?, val unreadCount: Int)
data class ChatBoardPage(val items: List<ChatBoardPost>, val nextCursor: String?)
data class ChatSendRequest(val body: String)
data class ChatSendResult(val message: ChatMessage, val filterStatus: String = "ALLOW")
data class ChatReportRequest(val reason: String)
data class ChatReportResult(val reportId: UUID, val status: String)
data class ChatBlockResult(val accountId: UUID, val blocked: Boolean)
data class ChatBoardPostRequest(val intent: ChatIntent, val itemId: String, val quantity: Long, val unitPrice: Long?, val body: String)
data class ChatBoardPostResult(val post: ChatBoardPost, val filterStatus: String = "ALLOW")
data class ChatBoardCloseResult(val postId: UUID, val status: String)
data class ChatCommandResult<T>(val commandId: UUID, val idempotencyKey: UUID, val status: String = "SUCCEEDED", val result: T)
