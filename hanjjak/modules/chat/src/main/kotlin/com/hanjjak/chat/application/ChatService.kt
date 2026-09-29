package com.hanjjak.chat.application

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.chat.domain.*
import com.hanjjak.events.application.DomainEventPublisher
import org.springframework.transaction.annotation.Transactional
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.time.Clock
import java.time.Instant
import java.time.temporal.ChronoUnit
import java.util.Base64
import java.util.UUID

internal fun visibleChatMessages(rows: List<ChatMessage>, accountId: UUID, isBlocked: (UUID) -> Boolean): List<ChatMessage> =
    rows.filterNot { it.accountId != accountId && isBlocked(it.accountId) }

open class ChatService(
    private val repository: ChatRepository,
    private val mapper: ObjectMapper,
    private val clock: Clock,
    private val events: DomainEventPublisher,
    private val moderation: ChatModeration = ChatModeration(),
) {
    companion object { private const val PAGE_SIZE = 30 }

    @Transactional(readOnly = true)
    open fun stateVersion(accountId: UUID): Long = repository.stateVersion(accountId)

    @Transactional
    open fun messages(accountId: UUID, cursor: String?): ChatPage {
        val rows = repository.listMessages(accountId, decodeCursor(cursor), PAGE_SIZE + 1)
        val visible = visibleChatMessages(rows, accountId) { repository.isBlocked(accountId, it) }
        return ChatPage(visible.take(PAGE_SIZE), visible.drop(PAGE_SIZE).firstOrNull()?.createdAt?.toString(), 0)
    }

    @Transactional
    open fun sendMessage(accountId: UUID, idempotencyKey: UUID, request: ChatSendRequest): ChatCommandResult<ChatSendResult> {
        require(!repository.isChatBanned(accountId, clock.instant())) { "CHAT_BANNED" }
        val decision = moderation.check(request.body)
        require(decision.allowed) { decision.code }
        val fingerprint = fingerprint("message", request.body)
        existing(accountId, idempotencyKey, fingerprint, ChatSendResult::class.java)?.let { return it }
        val account = repository.account(accountId)
        val now = clock.instant()
        val message = ChatMessage(UUID.randomUUID(), accountId, account.nickname, account.primaryMaterialType, request.body.trim(), now, UUID.randomUUID())
        repository.createMessage(message)
        val commandId = UUID.randomUUID()
        events.publish("CHAT_MESSAGE_CREATED", message.eventId, now, accountId = accountId, payload = mapOf("messageId" to message.messageId))
        val result = ChatSendResult(message)
        repository.saveCommand(commandId, accountId, idempotencyKey, fingerprint, mapper.writeValueAsString(result))
        return ChatCommandResult(commandId, idempotencyKey, result = result)
    }

    @Transactional
    open fun board(accountId: UUID, cursor: String?): ChatBoardPage {
        repository.expireBoards(clock.instant())
        val rows = repository.listBoard(accountId, decodeCursor(cursor), PAGE_SIZE + 1)
        val visible = rows.filterNot { it.accountId != accountId && repository.isBlocked(accountId, it.accountId) }
        return ChatBoardPage(visible.take(PAGE_SIZE), visible.drop(PAGE_SIZE).firstOrNull()?.createdAt?.toString())
    }

    @Transactional
    open fun createPost(accountId: UUID, idempotencyKey: UUID, request: ChatBoardPostRequest): ChatCommandResult<ChatBoardPostResult> {
        require(request.itemId.isNotBlank()) { "CHAT_ITEM_REQUIRED" }
        require(request.quantity in 1..999) { "CHAT_INVALID_QUANTITY" }
        require(request.intent == ChatIntent.BUY || request.unitPrice != null) { "CHAT_UNIT_PRICE_REQUIRED" }
        request.unitPrice?.let { require(it in 10..999990) { "CHAT_INVALID_UNIT_PRICE" } }
        val decision = moderation.check(request.body)
        require(decision.allowed) { decision.code }
        val fingerprint = fingerprint("board", request.intent, request.itemId, request.quantity, request.unitPrice, request.body)
        existing(accountId, idempotencyKey, fingerprint, ChatBoardPostResult::class.java)?.let { return it }
        val account = repository.account(accountId)
        val now = clock.instant()
        val post = ChatBoardPost(UUID.randomUUID(), accountId, account.nickname, request.intent, request.itemId.trim(), request.quantity, request.unitPrice, request.body.trim(), ChatPostStatus.ACTIVE, now, now.plus(24, ChronoUnit.HOURS))
        repository.createBoard(post)
        val commandId = UUID.randomUUID()
        events.publish("CHAT_BOARD_POST_CREATED", post.postId, now, accountId = accountId, itemId = post.itemId, quantity = post.quantity, unitPrice = post.unitPrice, commandId = commandId)
        val result = ChatBoardPostResult(post)
        repository.saveCommand(commandId, accountId, idempotencyKey, fingerprint, mapper.writeValueAsString(result))
        return ChatCommandResult(commandId, idempotencyKey, result = result)
    }

    @Transactional
    open fun closePost(accountId: UUID, postId: UUID, idempotencyKey: UUID): ChatCommandResult<ChatBoardCloseResult> {
        val fingerprint = fingerprint("close", postId)
        existing(accountId, idempotencyKey, fingerprint, ChatBoardCloseResult::class.java)?.let { return it }
        val post = repository.closeBoard(accountId, postId, clock.instant()) ?: throw IllegalArgumentException("CHAT_POST_NOT_FOUND")
        val commandId = UUID.randomUUID()
        val result = ChatBoardCloseResult(post.postId, post.status.name)
        repository.saveCommand(commandId, accountId, idempotencyKey, fingerprint, mapper.writeValueAsString(result))
        return ChatCommandResult(commandId, idempotencyKey, result = result)
    }

    @Transactional
    open fun report(accountId: UUID, idempotencyKey: UUID, targetType: String, targetId: UUID, request: ChatReportRequest): ChatCommandResult<ChatReportResult> {
        require(request.reason.length in 1..64) { "CHAT_REPORT_REASON_REQUIRED" }
        val fingerprint = fingerprint("report", targetType, targetId, request.reason)
        existing(accountId, idempotencyKey, fingerprint, ChatReportResult::class.java)?.let { return it }
        require(if (targetType == "MESSAGE") repository.messageExists(targetId) else repository.boardExists(targetId)) { "CHAT_TARGET_NOT_FOUND" }
        val commandId = UUID.randomUUID()
        val reportId = UUID.randomUUID()
        repository.createReport(reportId, accountId, targetType, targetId, request.reason, clock.instant())
        val result = ChatReportResult(reportId, "RECEIVED")
        repository.saveCommand(commandId, accountId, idempotencyKey, fingerprint, mapper.writeValueAsString(result))
        return ChatCommandResult(commandId, idempotencyKey, result = result)
    }

    @Transactional
    open fun block(accountId: UUID, blockedAccountId: UUID, idempotencyKey: UUID, blocked: Boolean): ChatCommandResult<ChatBlockResult> {
        require(accountId != blockedAccountId) { "CHAT_SELF_BLOCK" }
        val fingerprint = fingerprint(if (blocked) "block" else "unblock", blockedAccountId)
        existing(accountId, idempotencyKey, fingerprint, ChatBlockResult::class.java)?.let { return it }
        if (blocked) repository.block(accountId, blockedAccountId) else repository.unblock(accountId, blockedAccountId)
        val commandId = UUID.randomUUID()
        val result = ChatBlockResult(blockedAccountId, blocked)
        repository.saveCommand(commandId, accountId, idempotencyKey, fingerprint, mapper.writeValueAsString(result))
        return ChatCommandResult(commandId, idempotencyKey, result = result)
    }

    private fun <T> existing(accountId: UUID, key: UUID, fingerprint: String, type: Class<T>): ChatCommandResult<T>? {
        val existing = repository.command(accountId, key) ?: return null
        require(existing.fingerprint == fingerprint) { "IDEMPOTENCY_KEY_REUSED" }
        return ChatCommandResult(existing.commandId, key, result = mapper.readValue(existing.resultJson, type))
    }

    private fun fingerprint(vararg values: Any?): String = sha256(values.joinToString("\u0000"))
    private fun sha256(value: String): String = MessageDigest.getInstance("SHA-256").digest(value.toByteArray(StandardCharsets.UTF_8)).joinToString("") { "%02x".format(it) }
    private fun decodeCursor(cursor: String?): Instant? = cursor?.let { runCatching { Instant.parse(String(Base64.getUrlDecoder().decode(it), StandardCharsets.UTF_8)) }.getOrElse { throw IllegalArgumentException("INVALID_CURSOR") } }
}
