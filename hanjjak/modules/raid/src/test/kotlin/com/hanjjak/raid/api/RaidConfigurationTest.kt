package com.hanjjak.raid.api

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.events.application.DomainEventPublisher
import com.hanjjak.raid.application.RaidAttemptCompletionService
import com.hanjjak.raid.application.RaidClock
import com.hanjjak.raid.application.RaidCombatSnapshotProvider
import com.hanjjak.raid.application.RaidContentPort
import com.hanjjak.raid.application.RaidMainBattlePort
import com.hanjjak.raid.application.RaidProgressPort
import com.hanjjak.raid.application.RaidQueryPort
import com.hanjjak.raid.application.RaidRepository
import com.hanjjak.raid.application.RaidRewardPort
import com.hanjjak.raid.application.RaidService
import com.hanjjak.raid.application.RaidClaimsView
import com.hanjjak.raid.application.RaidRankingApiView
import com.hanjjak.raid.domain.RaidSession
import com.hanjjak.raid.domain.RaidSessionId
import java.lang.reflect.Proxy
import java.time.Instant
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertSame

class RaidConfigurationTest {
    @Test
    fun `configuration passes composition root query port into raid service`() {
        val queryPort = object : RaidQueryPort {
            override fun stateVersion(accountId: UUID) = 42L
            override fun sealedContribution(sessionId: RaidSessionId) = 0L
            override fun ranking(accountId: UUID, session: RaidSession, cursor: String?, limit: Int) =
                RaidRankingApiView(session.sessionId.value, false, null, 0L, emptyList(), null, 0L)
            override fun claims(accountId: UUID, cursor: String?) = RaidClaimsView(emptyList(), 0L, 0L, null)
        }
        val repository = proxy(RaidRepository::class.java)
        val mainBattle = proxy(RaidMainBattlePort::class.java)
        val service = RaidConfiguration().raidService(
            repository = repository,
            clock = RaidClock { Instant.EPOCH },
            snapshotProvider = proxy(RaidCombatSnapshotProvider::class.java),
            contentPort = proxy(RaidContentPort::class.java),
            progress = proxy(RaidProgressPort::class.java),
            mainBattle = mainBattle,
            completion = RaidAttemptCompletionService(repository, mainBattle, ObjectMapper()),
            mapper = ObjectMapper(),
            rewardPort = proxy(RaidRewardPort::class.java),
            events = proxy(DomainEventPublisher::class.java),
            queries = queryPort,
        )

        val field = RaidService::class.java.getDeclaredField("queries").apply { isAccessible = true }
        assertSame(queryPort, field.get(service))
    }

    @Suppress("UNCHECKED_CAST")
    private fun <T> proxy(type: Class<T>): T = Proxy.newProxyInstance(type.classLoader, arrayOf(type)) { _, method, _ ->
        when (method.returnType) {
            Boolean::class.javaPrimitiveType -> false
            Int::class.javaPrimitiveType -> 0
            Long::class.javaPrimitiveType -> 0L
            else -> null
        }
    } as T
}
