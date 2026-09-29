package com.hanjjak.battle.application

import jakarta.annotation.PostConstruct
import org.springframework.boot.context.properties.ConfigurationProperties
import java.math.BigDecimal
import java.util.UUID

@ConfigurationProperties("hanjjak.offline-reward")
class OfflineRewardSettings {
    var testAllAccounts: Boolean = false
    var testAccountIds: String = ""
    var testRewardMultiplier: String = ""
    var testGracePeriodSeconds: String = ""
    var testBucketSeconds: String = ""
    var testAccrualCapSeconds: String = ""

    data class Policy(
        val gracePeriodSeconds: Long,
        val bucketSeconds: Long,
        val maxAccrualSeconds: Long,
        val rewardMultiplier: BigDecimal,
    )

    @PostConstruct
    fun validate() {
        val ids = parsedTestAccountIds()
        if (!testAllAccounts && ids.isEmpty()) return
        requiredLong(testGracePeriodSeconds, "OFFLINE_REWARD_TEST_GRACE_PERIOD_SECONDS")
        requiredPositiveLong(testBucketSeconds, "OFFLINE_REWARD_TEST_BUCKET_SECONDS")
        requiredLong(testAccrualCapSeconds, "OFFLINE_REWARD_TEST_ACCRUAL_CAP_SECONDS")
        requiredDecimal(testRewardMultiplier, "OFFLINE_REWARD_TEST_REWARD_MULTIPLIER")
    }

    fun forAccount(accountId: UUID): Policy = if (isTestAccount(accountId)) {
        Policy(
            gracePeriodSeconds = requiredLong(testGracePeriodSeconds, "OFFLINE_REWARD_TEST_GRACE_PERIOD_SECONDS"),
            bucketSeconds = requiredPositiveLong(testBucketSeconds, "OFFLINE_REWARD_TEST_BUCKET_SECONDS"),
            maxAccrualSeconds = requiredLong(testAccrualCapSeconds, "OFFLINE_REWARD_TEST_ACCRUAL_CAP_SECONDS"),
            rewardMultiplier = requiredDecimal(testRewardMultiplier, "OFFLINE_REWARD_TEST_REWARD_MULTIPLIER"),
        )
    } else {
        Policy(OfflineRewardPolicy.GRACE_PERIOD_SECONDS, OfflineRewardPolicy.BUCKET_SECONDS, OfflineRewardPolicy.MAX_ACCRUAL_SECONDS, OfflineRewardPolicy.REWARD_MULTIPLIER)
    }

    fun isTestAccount(accountId: UUID): Boolean = testAllAccounts || accountId in parsedTestAccountIds()

    private fun parsedTestAccountIds(): Set<UUID> = testAccountIds.split(',')
        .asSequence()
        .map(String::trim)
        .filter(String::isNotEmpty)
        .map { value -> runCatching { UUID.fromString(value) }.getOrElse { error("Invalid offline reward test account id: $value") } }
        .toSet()

    private fun requiredLong(value: String, name: String): Long = value.trim().toLongOrNull()
        ?.also { require(it >= 0) { "$name must be non-negative" } }
        ?: error("$name is required for an enabled offline reward test mode")

    private fun requiredPositiveLong(value: String, name: String): Long = requiredLong(value, name)
        .also { require(it > 0) { "$name must be positive" } }

    private fun requiredDecimal(value: String, name: String): BigDecimal = value.trim().toBigDecimalOrNull()
        ?.also { require(it >= BigDecimal.ZERO) { "$name must be non-negative" } }
        ?: error("$name is required for an enabled offline reward test mode")
}
