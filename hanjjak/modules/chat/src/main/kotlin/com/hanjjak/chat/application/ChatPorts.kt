package com.hanjjak.chat.application

import com.hanjjak.chat.domain.*
import java.time.Instant
import java.util.UUID

interface ChatRepository {
    data class StoredCommand(val commandId: UUID, val fingerprint: String, val resultJson: String)
    data class AccountView(val nickname: String, val primaryMaterialType: String?)
    fun stateVersion(accountId: UUID): Long
    fun account(accountId: UUID): AccountView
    fun command(accountId: UUID, idempotencyKey: UUID): StoredCommand?
    fun saveCommand(commandId: UUID, accountId: UUID, idempotencyKey: UUID, fingerprint: String, resultJson: String)
    fun isChatBanned(accountId: UUID, now: Instant): Boolean
    fun listMessages(accountId: UUID, cursor: Instant?, limit: Int): List<ChatMessage>
    fun findMessage(messageId: UUID): ChatMessage?
    fun createMessage(message: ChatMessage)
    fun listBoard(accountId: UUID, cursor: Instant?, limit: Int): List<ChatBoardPost>
    fun expireBoards(now: Instant)
    fun createBoard(post: ChatBoardPost)
    fun closeBoard(accountId: UUID, postId: UUID, now: Instant): ChatBoardPost?
    fun messageExists(messageId: UUID): Boolean
    fun boardExists(postId: UUID): Boolean
    fun createReport(reportId: UUID, reporter: UUID, targetType: String, targetId: UUID, reason: String, now: Instant): Boolean
    fun block(accountId: UUID, blockedAccountId: UUID)
    fun unblock(accountId: UUID, blockedAccountId: UUID)
    fun isBlocked(accountId: UUID, blockedAccountId: UUID): Boolean
    fun listReports(limit: Int): List<ChatReportView>
    fun findBoard(postId: UUID): ChatBoardPost?
    fun moderateMessage(messageId: UUID): Boolean
    fun moderateBoard(postId: UUID): Boolean
    fun banAccount(accountId: UUID, until: Instant?, reason: String)
    fun unbanAccount(accountId: UUID)
    fun listBlockedAccounts(accountId: UUID): List<ChatBlockedAccountView>
}

data class ChatReportView(val reportId: UUID, val reporterAccountId: UUID, val targetType: String, val targetId: UUID, val reason: String, val createdAt: Instant, val targetBody: String?, val targetAccountId: UUID?, val targetNickname: String?)
data class ChatBlockedAccountView(val accountId: UUID, val nickname: String, val createdAt: Instant)
