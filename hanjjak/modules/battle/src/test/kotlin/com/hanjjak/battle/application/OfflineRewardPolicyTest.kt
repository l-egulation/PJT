package com.hanjjak.battle.application

import java.math.BigDecimal
import java.time.Duration
import java.time.Instant
import kotlin.test.Test
import kotlin.test.assertEquals

class OfflineRewardPolicyTest {
    private val start = Instant.parse("2026-09-12T00:00:00Z")

    @Test
    fun `production policy floors to whole minutes caps at eight hours and pays half rate`() {
        assertEquals(0, OfflineRewardPolicy.eligibleSeconds(start, start.plusSeconds(59)))
        assertEquals(60, OfflineRewardPolicy.eligibleSeconds(start, start.plusSeconds(60)))
        assertEquals(60, OfflineRewardPolicy.eligibleSeconds(start, start.plusSeconds(119)))
        assertEquals(120, OfflineRewardPolicy.eligibleSeconds(start, start.plusSeconds(120)))
        assertEquals(28_800, OfflineRewardPolicy.eligibleSeconds(start, start.plus(Duration.ofHours(12))))
        assertEquals(45, OfflineRewardPolicy.payout(BigDecimal("90"), 60))
        assertEquals(90, OfflineRewardPolicy.payout(BigDecimal("90"), 120))
    }

    @Test
    fun `session authority cutoff matches the first payable minute`() {
        assertEquals(60, OfflineRewardPolicy.BUCKET_SECONDS)
        assertEquals(0, OfflineRewardPolicy.eligibleSeconds(start, start.plusSeconds(59)))
        assertEquals(60, OfflineRewardPolicy.eligibleSeconds(start, start.plusSeconds(60)))
    }
    @Test
    fun `empty allowlist disables the test policy`() {
        val settings = OfflineRewardSettings()
        val account = java.util.UUID.fromString("00000000-0000-0000-0000-000000000001")
        assertEquals(false, settings.isTestAccount(account))
        assertEquals(0, settings.forAccount(account).gracePeriodSeconds)
        assertEquals(60, settings.forAccount(account).bucketSeconds)
        assertEquals(28_800, settings.forAccount(account).maxAccrualSeconds)
        assertEquals(BigDecimal("0.5"), settings.forAccount(account).rewardMultiplier)
    }

    @Test
    fun `allowlist applies test settings only to listed account`() {
        val account = java.util.UUID.fromString("00000000-0000-0000-0000-000000000001")
        val other = java.util.UUID.fromString("00000000-0000-0000-0000-000000000002")
        val settings = OfflineRewardSettings().apply {
            testAccountIds = account.toString()
            testRewardMultiplier = "0.25"
            testGracePeriodSeconds = "0"
            testBucketSeconds = "120"
            testAccrualCapSeconds = "600"
        }
        assertEquals(true, settings.isTestAccount(account))
        assertEquals(0, settings.forAccount(account).gracePeriodSeconds)
        assertEquals(120, settings.forAccount(account).bucketSeconds)
        assertEquals(600, settings.forAccount(account).maxAccrualSeconds)
        assertEquals(BigDecimal("0.25"), settings.forAccount(account).rewardMultiplier)
        assertEquals(0, settings.forAccount(other).gracePeriodSeconds)
        assertEquals(60, settings.forAccount(other).bucketSeconds)
        assertEquals(28_800, settings.forAccount(other).maxAccrualSeconds)
        assertEquals(BigDecimal("0.5"), settings.forAccount(other).rewardMultiplier)
    }
    @Test
    fun `global test mode applies policy to any account`() {
        val settings = OfflineRewardSettings().apply {
            testAllAccounts = true
            testRewardMultiplier = "0.5"
            testGracePeriodSeconds = "0"
            testBucketSeconds = "60"
            testAccrualCapSeconds = "28800"
        }
        val account = java.util.UUID.randomUUID()
        assertEquals(true, settings.isTestAccount(account))
        assertEquals(0, settings.forAccount(account).gracePeriodSeconds)
        assertEquals(60, settings.forAccount(account).bucketSeconds)
        assertEquals(28_800, settings.forAccount(account).maxAccrualSeconds)
        assertEquals(BigDecimal("0.5"), settings.forAccount(account).rewardMultiplier)
    }
}
