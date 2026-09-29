package com.hanjjak.gameapi.feedback

import jakarta.servlet.http.HttpSession
import org.springframework.http.CacheControl
import org.springframework.http.ResponseEntity
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RequestBody
import org.springframework.web.bind.annotation.RequestHeader
import org.springframework.web.bind.annotation.RestController
import java.time.Clock
import java.time.Instant
import java.util.Base64
import java.util.UUID

data class QaBugReportRequest(
    val description: String,
    val pageUrl: String,
    val userAgent: String,
    val viewportWidth: Int,
    val viewportHeight: Int,
    val imageDataUrl: String? = null,
)

data class QaBugReportResult(val reportId: UUID, val status: String)
data class QaBugReportEnvelope<T>(val requestId: UUID, val serverTime: Instant, val data: T)

@RestController
class QaBugReportController(private val jdbc: JdbcClient, private val clock: Clock) {
    @PostMapping("/api/v1/qa/reports")
    @Transactional
    fun create(
        @RequestHeader("Idempotency-Key") idempotencyKey: UUID,
        @RequestBody request: QaBugReportRequest,
        session: HttpSession,
    ): ResponseEntity<QaBugReportEnvelope<QaBugReportResult>> {
        val accountId = session.getAttribute("accountId") as? UUID ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")
        val description = request.description.trim()
        require(description.length in 1..2000) { "QA_REPORT_DESCRIPTION_REQUIRED" }
        require(request.pageUrl.length in 1..1000 && request.userAgent.length in 1..1000) { "QA_REPORT_CONTEXT_INVALID" }
        require(request.viewportWidth in 1..10000 && request.viewportHeight in 1..10000) { "QA_REPORT_VIEWPORT_INVALID" }
        val image = decodeQaReportImage(request.imageDataUrl)
        val existing = jdbc.sql("select report_id from qa_bug_report where reporter_account_id=:account and idempotency_key=:key")
            .params(mapOf("account" to accountId, "key" to idempotencyKey))
            .query(UUID::class.java).optional()
        val reportId = existing.orElseGet {
            val next = UUID.randomUUID()
            jdbc.sql("""
                insert into qa_bug_report(report_id,reporter_account_id,description,page_url,user_agent,viewport_width,viewport_height,image_media_type,image_data,idempotency_key,created_at)
                values (:report,:account,:description,:page,:agent,:width,:height,:media,:image,:key,:created)
            """.trimIndent()).params(mapOf(
                "report" to next, "account" to accountId, "description" to description,
                "page" to request.pageUrl, "agent" to request.userAgent,
                "width" to request.viewportWidth, "height" to request.viewportHeight,
                "media" to image?.first, "image" to image?.second,
                "key" to idempotencyKey, "created" to clock.instant(),
            )).update()
            next
        }
        return ResponseEntity.ok().cacheControl(CacheControl.noStore()).body(
            QaBugReportEnvelope(UUID.randomUUID(), clock.instant(), QaBugReportResult(reportId, "RECEIVED")),
        )
    }

}

private const val MAX_QA_REPORT_IMAGE_BYTES = 4 * 1024 * 1024
private val QA_REPORT_DATA_URL = Regex("^data:(image/(?:png|jpeg|webp));base64,(.+)$", RegexOption.DOT_MATCHES_ALL)

internal fun decodeQaReportImage(dataUrl: String?): Pair<String, ByteArray>? {
    if (dataUrl.isNullOrBlank()) return null
    val match = QA_REPORT_DATA_URL.matchEntire(dataUrl) ?: throw IllegalArgumentException("QA_REPORT_IMAGE_INVALID")
    val bytes = try { Base64.getDecoder().decode(match.groupValues[2]) }
    catch (_: IllegalArgumentException) { throw IllegalArgumentException("QA_REPORT_IMAGE_INVALID") }
    require(bytes.isNotEmpty() && bytes.size <= MAX_QA_REPORT_IMAGE_BYTES) { "QA_REPORT_IMAGE_TOO_LARGE" }
    return match.groupValues[1] to bytes
}
