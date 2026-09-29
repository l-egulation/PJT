package com.hanjjak.gameapi.ranking

import com.hanjjak.account.application.AuthenticationService
import com.hanjjak.account.application.MaterialPreferenceService
import com.hanjjak.account.domain.MaterialType
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNotNull
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.transaction.annotation.Transactional
import java.util.UUID

@com.hanjjak.gameapi.GameApiIntegrationTest
@Transactional
class CombatPowerRankingIntegrationTest {
    @Autowired private lateinit var authentication: AuthenticationService
    @Autowired private lateinit var preferences: MaterialPreferenceService
    @Autowired private lateinit var rankings: CombatPowerRankingService
    @Autowired private lateinit var jdbc: JdbcClient

    @Test
    fun `ranking contains selected specialization and server calculated power`() {
        val signup = authentication.signup("ranking-${UUID.randomUUID()}@example.com", "password123", "옥수수랭커")
        preferences.select(signup.account.id, UUID.randomUUID(), MaterialType.CORN)

        val view = rankings.snapshot(signup.account.id, 20)
        val corn = view.specializations.first { it.materialType == MaterialType.CORN }

        assertEquals("옥수수 전문", corn.displayName)
        assertEquals("combat-power-v2", view.formulaVersion)
        assertNotNull(view.myEntry)
        assertEquals("옥수수랭커", view.myEntry!!.nickname)
        assertEquals(1, view.myEntry!!.level)
        assertEquals("옥수수 전문", view.myEntry!!.displayName)
        assertEquals(RankingAppearance(null, null, null, null, null, null), view.myEntry!!.appearance)
        assertEquals(1, view.myEntry!!.overallRank)
        assertEquals("옥수수랭커", view.overallTop.first().nickname)
        assertEquals(view.sourceStateVersion, signup.account.stateVersion + 1)
    }

    @Test
    fun `my overall rank remains available outside the overall top ten`() {
        val target = authentication.signup("ranking-target-${UUID.randomUUID()}@example.com", "password123", "통합순위대상")
        preferences.select(target.account.id, UUID.randomUUID(), MaterialType.CORN)
        repeat(10) { index ->
            val competitor = authentication.signup("ranking-top-$index-${UUID.randomUUID()}@example.com", "password123", "상위랭커$index")
            preferences.select(competitor.account.id, UUID.randomUUID(), MaterialType.entries[index % MaterialType.entries.size])
            jdbc.sql("update character set level=2 where account_id=:account")
                .param("account", competitor.account.id)
                .update()
        }

        val view = rankings.snapshot(target.account.id, 20)

        assertEquals(10, view.overallTop.size)
        assertTrue(view.myEntry!!.overallRank > 10)
        assertTrue(view.overallTop.none { it.nickname == "통합순위대상" })
    }

    @Test
    fun `deleted account is excluded from ranking`() {
        val deleted = authentication.signup("ranking-deleted-${UUID.randomUUID()}@example.com", "password123", "탈퇴랭커")
        preferences.select(deleted.account.id, UUID.randomUUID(), MaterialType.POTATO)
        jdbc.sql("update account set deleted_at=now() where id=:account")
            .param("account", deleted.account.id)
            .update()

        val view = rankings.snapshot(deleted.account.id, 20)

        assertEquals(null, view.myEntry)
        assertTrue(view.overallTop.none { it.nickname == "탈퇴랭커" })
        assertTrue(view.specializations.all { ranking -> ranking.entries.none { it.nickname == "탈퇴랭커" } })
    }

}
