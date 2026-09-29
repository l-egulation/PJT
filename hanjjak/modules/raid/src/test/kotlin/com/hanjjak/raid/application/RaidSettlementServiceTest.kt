package com.hanjjak.raid.application

import com.hanjjak.raid.domain.*
import java.lang.reflect.Proxy
import java.time.Instant
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertTrue

class RaidSettlementServiceTest {
    private val sessionId = RaidSessionId(UUID.fromString("00000000-0000-4000-8000-000000000001"))

    @Test
    fun `one settlement invocation drains every pending account page before ranks`() {
        val now = Instant.parse("2026-09-14T08:00:00Z")
        val pending = (1..5).map { UUID.nameUUIDFromBytes("account-$it".toByteArray()) }.toMutableSet()
        val pageCalls = mutableListOf<Int>()
        var session = RaidSession(
            sessionId,
            "raid-v1",
            "reward-v1",
            now.minusSeconds(1),
            RaidSessionStatus.SETTLING,
            cutoffAt = now.minusSeconds(1),
            settlementPhase = RaidSettlementPhase.FINALIZE_ACCOUNTS,
            settlementAccountCount = pending.size.toLong(),
        )
        val repository = Proxy.newProxyInstance(
            RaidRepository::class.java.classLoader,
            arrayOf(RaidRepository::class.java),
        ) { _, method, args ->
            when (method.name.substringBefore('-')) {
                "session", "lockSession" -> session
                "saveSession" -> { session = args[0] as RaidSession; Unit }
                "settlementAccounts" -> pending.sorted().take(args[2] as Int).also { pageCalls += it.size }
                "settlementAccountCount" -> 5L
                "finalizedSettlementAccountCount" -> 5L - pending.size
                else -> null
            }
        } as RaidRepository
        val rotation = object : RaidSessionRotationService(repository, RaidContentPort { error("unused") }) {
            override fun rotateSession(now: Instant) = session
        }
        val phases = object : RaidSettlementPhaseService(repository) {
            override fun freezeWorkset(sessionId: RaidSessionId) = Unit

            override fun advanceToRanks(sessionId: RaidSessionId): Boolean {
                if (pending.isNotEmpty()) return false
                session = session.copy(settlementPhase = RaidSettlementPhase.MATERIALIZE_RANKS)
                return true
            }
        }
        val accounts = object : RaidSettlementAccountService(
            repository,
            object : RaidRewardPort {
                override fun preflightLocked(accountId: UUID, bundle: RaidRewardBundle) = Unit
                override fun creditLocked(accountId: UUID, sourceId: UUID, bundle: RaidRewardBundle) = Unit
            },
            object : RaidMainBattlePort {
                override fun pauseLockedWithHandoff(accountId: UUID, commandId: UUID) = null
                override fun resumeLocked(accountId: UUID, commandId: UUID, handoff: RaidBattleHandoff) = null
            },
            com.hanjjak.events.application.NoopDomainEventPublisher,
            com.fasterxml.jackson.databind.ObjectMapper(),
        ) {
            override fun finalizeAccount(sessionId: RaidSessionId, accountId: UUID, now: Instant) {
                pending.remove(accountId)
            }
        }
        var ranked = false
        val ranks = object : RaidSettlementRankService(repository) {
            override fun materializeRanks(sessionId: RaidSessionId): List<RaidFinalRank> {
                ranked = true
                return emptyList()
            }
        }
        var completed = false
        val completion = object : RaidSettlementCompletionService(repository) {
            override fun complete(sessionId: RaidSessionId, now: Instant) {
                completed = true
                session = session.copy(status = RaidSessionStatus.SETTLED_SUCCESS, settlementPhase = RaidSettlementPhase.SETTLED, completedAt = now)
            }
        }

        RaidSettlementService(repository, RaidClock { now }, rotation, phases, accounts, ranks, completion, pageSize = 2)
            .settleOne(sessionId, now)

        assertTrue(pending.isEmpty(), "pending=$pending pageCalls=$pageCalls session=$session")
        assertEquals(RaidSettlementPhase.SETTLED, session.settlementPhase)
        assertTrue(ranked)
        assertTrue(completed)
    }

    @Test
    fun `KST boundary is exactly 17 30 and next interval is deterministic`() {
        val before = Instant.parse("2026-09-14T08:29:59.999Z")
        val boundary = Instant.parse("2026-09-14T08:30:00Z")
        val after = Instant.parse("2026-09-14T08:30:00.001Z")

        assertEquals(Instant.parse("2026-09-14T08:30:00Z"), RaidSettlementService.settlementAt(before))
        assertEquals(Instant.parse("2026-09-15T08:30:00Z"), RaidSettlementService.settlementAt(boundary))
        assertEquals(Instant.parse("2026-09-15T08:30:00Z"), RaidSettlementService.settlementAt(after))
        assertEquals(
            Instant.parse("2026-09-15T08:30:00Z"),
            RaidSettlementService.nextSettlementAt(boundary),
        )
    }

    @Test
    fun `competitive ranks sort all three comparison keys and skip after ties`() {
        val accounts = listOf(
            contribution("00000000-0000-4000-8000-000000000010", 100, 500, 300),
            contribution("00000000-0000-4000-8000-000000000011", 100, 500, 300),
            contribution("00000000-0000-4000-8000-000000000012", 100, 500, 250),
            contribution("00000000-0000-4000-8000-000000000013", 100, 400, 999),
            contribution("00000000-0000-4000-8000-000000000014", 90, 900, 900),
        )

        val ranks = RaidSettlementService.computeRanks(sessionId, accounts)

        assertEquals(listOf(1, 1, 3, 4, 5), ranks.map { it.competitiveRank })
        assertEquals(
            listOf(accounts[0].accountId, accounts[1].accountId, accounts[2].accountId, accounts[3].accountId, accounts[4].accountId),
            ranks.map { it.accountId },
        )
    }

    @Test
    fun `rank 100 tie fanout includes every tie and cursor does not skip equal keys`() {
        val accounts = (1..102).map { number ->
            val id = UUID.nameUUIDFromBytes("account-$number".toByteArray())
            contribution(id, if (number <= 99) 100 else 99, if (number <= 99) (1_000 + number).toLong() else number.toLong(), if (number <= 99) (1_000 + number).toLong() else number.toLong())
        } + listOf(
            contribution(UUID.nameUUIDFromBytes("tie-99".toByteArray()), 100, 1, 1),
            contribution(UUID.nameUUIDFromBytes("tie-98".toByteArray()), 100, 1, 1),
        )
        val ranks = RaidSettlementService.computeRanks(sessionId, accounts)
        val public = RaidSettlementService.publicRanks(ranks, limit = 100)

        assertTrue(public.items.size > 100)
        assertTrue(public.items.all { it.competitiveRank <= 100 })
        assertEquals(1, public.items.first().competitiveRank)
        assertEquals(100, public.items.last().competitiveRank)

        val page = RaidSettlementService.publicRanks(ranks, cursor = RaidRankCursor.from(public.items[98], ranks), limit = 10)
        assertTrue(page.items.isNotEmpty())
        assertTrue(page.items.all { it.competitiveRank == 100 })
    }

    @Test
    fun `caller outside public list is returned separately and cursor carries every ordering key`() {
        val accounts = (1..105).map { number ->
            contribution(UUID.nameUUIDFromBytes("account-$number".toByteArray()), number.toLong(), number.toLong(), number.toLong())
        }
        val ranks = RaidSettlementService.computeRanks(sessionId, accounts)
        val caller = accounts.first().accountId
        val view = RaidSettlementService.publicRanks(ranks, callerAccountId = caller, limit = 10)

        assertTrue(view.items.none { it.accountId == caller })
        assertEquals(caller, view.caller?.accountId)
        val last = view.items.last()
        val cursor = RaidRankCursor.from(last, ranks)
        assertEquals(cursor, RaidRankCursor.decode(cursor.encode()))
    }

    @Test
    fun `phase order is explicit and restartable`() {
        assertEquals(
            listOf(
                RaidSettlementPhase.ROTATE_SESSION,
                RaidSettlementPhase.FREEZE_WORKSET,
                RaidSettlementPhase.FINALIZE_ACCOUNTS,
                RaidSettlementPhase.MATERIALIZE_RANKS,
                RaidSettlementPhase.SETTLED,
            ),
            RaidSettlementPhase.entries,
        )
    }

    private fun contribution(id: String, seal: Long, damage: Long, highest: Long) =
        contribution(UUID.fromString(id), seal, damage, highest)

    private fun contribution(id: UUID, seal: Long, damage: Long, highest: Long) =
        RaidContribution(sessionId, id, seal, damage, confirmedAttempts = 1, highestDamage = highest)
}
