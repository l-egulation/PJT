package com.hanjjak.inventory.api

import com.hanjjak.inventory.application.InventoryQueryService
import com.hanjjak.inventory.application.InventoryRepository
import com.hanjjak.inventory.domain.InventoryPage
import com.hanjjak.inventory.domain.InventoryStatus
import com.hanjjak.inventory.domain.ItemCategory
import com.hanjjak.inventory.domain.ItemSort
import jakarta.servlet.http.HttpSession
import org.springframework.http.CacheControl
import org.springframework.http.ResponseEntity
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.PathVariable
import org.springframework.web.bind.annotation.RequestMapping
import org.springframework.web.bind.annotation.RequestParam
import org.springframework.web.bind.annotation.RestController
import java.time.Clock
import java.time.Instant
import java.util.UUID

@RestController
@RequestMapping("/api/v1/inventory")
class InventoryController(
    private val queries: InventoryQueryService,
    private val repository: InventoryRepository,
    private val clock: Clock,
) {
    data class Envelope<T>(val requestId: UUID, val serverTime: Instant, val stateVersion: Long, val data: T)

    @GetMapping
    fun list(
        @RequestParam(required = false) category: String?,
        @RequestParam(defaultValue = "ACQUIRED_DESC") sort: String,
        @RequestParam(required = false) cursor: String?,
        @RequestParam(defaultValue = "40") limit: Int,
        session: HttpSession,
    ): ResponseEntity<Envelope<InventoryPage>> {
        val accountId = accountId(session)
        val parsedCategory = category?.let { parseEnum<ItemCategory>(it, "INVALID_CATEGORY") }
        val parsedSort = parseEnum<ItemSort>(sort, "INVALID_SORT")
        return success(accountId, queries.list(accountId, parsedCategory, parsedSort, cursor, limit))
    }

    @GetMapping("/items/{itemId}")
    fun detail(@PathVariable itemId: String, session: HttpSession): ResponseEntity<Envelope<Any>> {
        val accountId = accountId(session)
        return success(accountId, queries.detail(accountId, itemId))
    }

    @GetMapping("/status")
    fun status(session: HttpSession): ResponseEntity<Envelope<InventoryStatus>> {
        val accountId = accountId(session)
        return success(accountId, queries.status(accountId))
    }

    private fun accountId(session: HttpSession): UUID = session.getAttribute("accountId") as? UUID
        ?: throw IllegalArgumentException("AUTHENTICATION_REQUIRED")

    private inline fun <reified T : Enum<T>> parseEnum(value: String, code: String): T = enumValues<T>().firstOrNull { it.name.equals(value, ignoreCase = true) }
        ?: throw IllegalArgumentException(code)

    private fun <T> success(accountId: UUID, data: T): ResponseEntity<Envelope<T>> = ResponseEntity.ok()
        .cacheControl(CacheControl.noStore())
        .body(Envelope(UUID.randomUUID(), clock.instant(), repository.stateVersion(accountId), data))
}
