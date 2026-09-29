package com.hanjjak.chat.api

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.chat.application.*
import com.hanjjak.chat.domain.*
import com.hanjjak.chat.infrastructure.JdbcChatRepository
import com.hanjjak.events.application.DomainEventPublisher
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty
import jakarta.servlet.http.HttpSession
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.http.CacheControl
import org.springframework.http.ResponseEntity
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.web.bind.annotation.*
import java.time.Clock
import java.time.Instant
import java.util.UUID

@Configuration
class ChatConfiguration {
    @Bean fun chatRepository(jdbc: JdbcClient): ChatRepository = JdbcChatRepository(jdbc)
    @Bean fun chatModeration(): ChatModeration = ChatModeration()
    @Bean fun chatService(repository: ChatRepository, mapper: ObjectMapper, clock: Clock, events: DomainEventPublisher, moderation: ChatModeration) = ChatService(repository, mapper, clock, events, moderation)
}

@RestController
@RequestMapping("/api/v1/chat")
@ConditionalOnProperty(prefix = "hanjjak.chat", name = ["enabled"], havingValue = "true")
class ChatController(private val service: ChatService, private val repository: ChatRepository, private val clock: Clock) {
    data class Envelope<T>(val requestId: UUID, val serverTime: Instant, val stateVersion: Long, val data: T)
    @GetMapping("/messages") fun messages(@RequestParam(required = false) cursor: String?, session: HttpSession) = success(account(session), service.messages(account(session), cursor))
    @PostMapping("/messages") fun send(@RequestHeader("Idempotency-Key") key: UUID, @RequestBody body: ChatSendRequest, session: HttpSession) = success(account(session), service.sendMessage(account(session), key, body))
    @GetMapping("/board") fun board(@RequestParam(required = false) cursor: String?, session: HttpSession) = success(account(session), service.board(account(session), cursor))
    @PostMapping("/board") fun createPost(@RequestHeader("Idempotency-Key") key: UUID, @RequestBody body: ChatBoardPostRequest, session: HttpSession) = success(account(session), service.createPost(account(session), key, body))
    @PostMapping("/board/{postId}/close") fun closePost(@PathVariable postId: UUID, @RequestHeader("Idempotency-Key") key: UUID, session: HttpSession) = success(account(session), service.closePost(account(session), postId, key))
    @PostMapping("/{targetType}/{targetId}/reports") fun report(@PathVariable targetType: String, @PathVariable targetId: UUID, @RequestHeader("Idempotency-Key") key: UUID, @RequestBody body: ChatReportRequest, session: HttpSession) = success(account(session), service.report(account(session), key, when (targetType.uppercase()) { "MESSAGES" -> "MESSAGE"; "BOARD" -> "BOARD_POST"; else -> targetType.uppercase() }, targetId, body))
    @PostMapping("/blocks/{blockedAccountId}") fun block(@PathVariable blockedAccountId: UUID, @RequestHeader("Idempotency-Key") key: UUID, session: HttpSession) = success(account(session), service.block(account(session), blockedAccountId, key, true))
    @DeleteMapping("/blocks/{blockedAccountId}") fun unblock(@PathVariable blockedAccountId: UUID, @RequestHeader("Idempotency-Key") key: UUID, session: HttpSession) = success(account(session), service.block(account(session), blockedAccountId, key, false))
    @GetMapping("/blocks") fun blocks(session: HttpSession) = success(account(session), repository.listBlockedAccounts(account(session)))
    private fun account(session: HttpSession): UUID = session.getAttribute("accountId") as? UUID ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")
    private fun <T> success(accountId: UUID, data: T): ResponseEntity<Envelope<T>> = ResponseEntity.ok().cacheControl(CacheControl.noStore()).body(Envelope(UUID.randomUUID(), clock.instant(), service.stateVersion(accountId), data))
}
