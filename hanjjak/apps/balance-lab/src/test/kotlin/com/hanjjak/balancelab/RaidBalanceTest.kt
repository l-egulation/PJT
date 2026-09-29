package com.hanjjak.balancelab

import java.io.File
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertTrue
class RaidBalanceTest {
    @Test
    fun `candidate bounds are exact positive search ranges`() {
        assertEquals(1, RAID_INITIAL_ATTACK_MIN)
        assertEquals(20, RAID_INITIAL_ATTACK_MAX)
        assertEquals(1, RAID_INITIAL_DEFENSE_MIN)
        assertEquals(10, RAID_INITIAL_DEFENSE_MAX)
        assertTrue(RAID_INITIAL_ATTACK_MIN > 0 && RAID_INITIAL_DEFENSE_MIN > 0)
    }
    @Test
    fun `derived raid content meets survival grade and deficiency gates`() {
        val report = deriveRaidBalance()
        val thresholds = report.content.gradeDamageThresholds.values.toList()

        assertTrue(report.reference.bSuccessCount >= 900)
        assertTrue(report.reference.medianSurvivalTicks in 550..650)
        assertTrue(thresholds.zipWithNext().all { (left, right) -> right > left })
        assertEquals(roundHalfUp(report.content.gradeDamageThresholds.getValue("B") * 35.0 / 100.0), report.content.gradeDamageThresholds.getValue("D"))
        assertEquals(roundHalfUp(report.content.gradeDamageThresholds.getValue("B") * 450.0 / 100.0), report.content.gradeDamageThresholds.getValue("SSS"))
        assertTrue(report.attackDeficient.earlyDamage < report.reference.earlyDamage)
        assertTrue(report.hpDeficient.medianSurvivalTicks < report.reference.medianSurvivalTicks)
        assertTrue(report.penetrationDeficient.lateDamage < report.reference.lateDamage)
    }

    @Test
    fun `raid derivation is reproducible`() {
        assertEquals(deriveRaidBalance(), deriveRaidBalance())
    }
    @Test
    fun `distribution report covers one thousand seeds for every named build`() {
        val report = deriveRaidBalance()
        val builds = listOf(report.reference, report.attackDeficient, report.hpDeficient, report.penetrationDeficient)

        builds.forEach { metrics ->
            assertTrue(metrics.sampleCount >= 1_000)
            assertTrue(metrics.p10Damage <= metrics.p50Damage && metrics.p50Damage <= metrics.p90Damage)
            assertTrue(metrics.p10SurvivalTicks <= metrics.p50SurvivalTicks && metrics.p50SurvivalTicks <= metrics.p90SurvivalTicks)
            assertEquals(metrics.sampleCount, metrics.gradeDistribution.values.sum())
            assertTrue(metrics.fiveMinuteCapCount in 0..metrics.sampleCount)
        }
    }

    @Test
    fun `population projection includes required sizes and participation floor`() {
        val report = deriveRaidBalance()
        assertEquals(setOf(36, 72, 144, 1_000), report.populationProjections.map { it.activeParticipants }.toSet())

        report.populationProjections.forEach { projection ->
            assertEquals(RaidRewardProjection(15, 9, 15_000), projection.threeParticipationConfirmations)
            assertTrue(projection.serverDay.cosmeticTickets >= projection.activeParticipants * 15)
            assertTrue(projection.serverDay.gemBoxes >= projection.activeParticipants * 9)
            assertTrue(projection.serverDay.rice >= projection.activeParticipants * 15_000)
            assertTrue(projection.sealContribution >= 0)
            assertTrue(projection.autoClaimBacklog >= 0)
        }
    }
    @Test
    fun `reference loader accepts the fixed raid contract`() {
        val root = repositoryRootForTest()
        val build = loadReferenceBuild(root.resolve("apps/balance-lab/src/main/resources/raid-reference-build-v1.json"), root)

        assertEquals("raid-mvp-v1-working", build.contentVersion)
        assertEquals(1L, build.seedStart)
        assertEquals(1_000L, build.seedEnd)
        assertEquals(600, build.targetMedianSurvivalTicks)
        assertEquals(550, build.survivalMin)
        assertEquals(650, build.survivalMax)
    }

    @Test
    fun `reference loader rejects drift from the fixed raid contract`() {
        val root = repositoryRootForTest()
        val source = root.resolve("apps/balance-lab/src/main/resources/raid-reference-build-v1.json").readText()
        val mutations = listOf(
            "\"contentVersion\": \"raid-mvp-v1-working\"" to "\"contentVersion\": \"raid-mvp-v2-working\"",
            "\"seedStart\": 1" to "\"seedStart\": 2",
            "\"seedEnd\": 1000" to "\"seedEnd\": 999",
            "\"targetMedianSurvivalTicks\": 600" to "\"targetMedianSurvivalTicks\": 601",
            "\"min\": 550" to "\"min\": 551",
            "\"max\": 650" to "\"max\": 649",
        )

        for ((from, to) in mutations) {
            assertTrue(source.contains(from), "fixture mutation did not find $from")
            val fixture = kotlin.io.path.createTempFile(prefix = "raid-reference-", suffix = ".json").toFile()
            try {
                fixture.writeText(source.replace(from, to))
                assertFailsWith<IllegalArgumentException> { loadReferenceBuild(fixture, root) }
            } finally {
                fixture.delete()
            }
        }
    }

    private fun repositoryRootForTest(): File {
        val current = File(System.getProperty("user.dir")).canonicalFile
        return generateSequence(current) { it.parentFile }
            .first { it.resolve("packages/game-content").isDirectory }
    }
}
