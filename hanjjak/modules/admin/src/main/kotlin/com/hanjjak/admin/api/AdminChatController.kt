package com.hanjjak.admin.api

import com.hanjjak.admin.domain.AdminPermission
import com.hanjjak.admin.infrastructure.AdminAuditRepository
import com.hanjjak.chat.application.ChatRepository
import com.hanjjak.chat.application.ChatReportView
import jakarta.servlet.http.HttpServletRequest
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty
import org.springframework.http.CacheControl
import org.springframework.http.ResponseEntity
import org.springframework.web.bind.annotation.*
import java.time.Clock
import java.time.Instant
import java.util.UUID

@RestController
@RequestMapping("/api/admin/v1/chat")
@ConditionalOnProperty(prefix = "hanjjak.chat", name = ["enabled"], havingValue = "true")
class AdminChatController(private val chat: ChatRepository, private val audit: AdminAuditRepository, private val clock: Clock) {
    data class Envelope<T>(val requestId: UUID, val serverTime: Instant, val stateVersion: Long, val data: T)
    data class ModerationResult(val targetId: UUID, val status: String)

    @GetMapping("/reports")
    fun reports(@RequestParam(defaultValue = "50") limit: Int, request: HttpServletRequest): ResponseEntity<Envelope<List<ChatReportView>>> = audited(request, "CHAT_READ_REPORTS", "CHAT_REPORT") { chat.listReports(limit) }

    @PostMapping("/messages/{messageId}/delete")
    fun deleteMessage(@PathVariable messageId: UUID, request: HttpServletRequest): ResponseEntity<Envelope<ModerationResult>> = moderate(request, "CHAT_DELETE_MESSAGE", "CHAT_MESSAGE", messageId) { chat.moderateMessage(messageId) }

    @PostMapping("/board/{postId}/delete")
    fun deleteBoard(@PathVariable postId: UUID, request: HttpServletRequest): ResponseEntity<Envelope<ModerationResult>> = moderate(request, "CHAT_DELETE_BOARD", "CHAT_BOARD_POST", postId) { chat.moderateBoard(postId) }

    @PostMapping("/accounts/{accountId}/ban")
    fun ban(@PathVariable accountId: UUID, @RequestParam(required = false) reason: String?, request: HttpServletRequest): ResponseEntity<Envelope<ModerationResult>> = moderate(request, "CHAT_BAN_ACCOUNT", "ACCOUNT", accountId) { chat.banAccount(accountId, null, reason ?: "ADMIN_MODERATION"); true }

    @DeleteMapping("/accounts/{accountId}/ban")
    fun unban(@PathVariable accountId: UUID, request: HttpServletRequest): ResponseEntity<Envelope<ModerationResult>> = moderate(request, "CHAT_UNBAN_ACCOUNT", "ACCOUNT", accountId) { chat.unbanAccount(accountId); true }

    private fun <T> audited(request: HttpServletRequest, action: String, target: String, operation: () -> T): ResponseEntity<Envelope<T>> {
        val principal = principal(request); require(AdminPermission.CHAT_MODERATE in principal.permissions) { "ADMIN_PERMISSION_DENIED" }
        return try { val result = operation(); record(request, action, target, null, "SUCCEEDED"); success(requestId(request), result) } catch (error: RuntimeException) { record(request, action, target, null, "FAILED"); throw error }
    }
    private fun moderate(request: HttpServletRequest, action: String, target: String, id: UUID, operation: () -> Boolean): ResponseEntity<Envelope<ModerationResult>> {
        val changed = audited(request, action, target, operation)
        return success(requestId(request), ModerationResult(id, if (changed.body?.data == true) "DELETED" else "NOT_FOUND"))
    }
    private fun record(request: HttpServletRequest, action: String, target: String, id: String?, outcome: String) { val p = principal(request); audit.record(AdminAuditRepository.Record(clock.instant(), p.operatorId, p.username, action, target, id, outcome, requestId(request), request.remoteAddr, mutation = request.method != "GET")) }
    private fun principal(request: HttpServletRequest) = request.getAttribute(AdminAccessFilter.PRINCIPAL_ATTRIBUTE) as? com.hanjjak.admin.domain.AdminPrincipal ?: throw IllegalArgumentException("ADMIN_AUTHENTICATION_REQUIRED")
    private fun requestId(request: HttpServletRequest) = request.getAttribute(AdminAccessFilter.REQUEST_ID_ATTRIBUTE) as? UUID ?: UUID.randomUUID()
    private fun <T> success(id: UUID, data: T) = ResponseEntity.ok().cacheControl(CacheControl.noStore()).body(Envelope(id, clock.instant(), 0, data))
}
