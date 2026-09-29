package com.hanjjak.raid.domain

/** Immutable, validator-approved raid grading and reward tables. */
object RaidRules {
    val personalRewards: Map<RaidGrade, RaidRewardBundle> = linkedMapOf(
        RaidGrade.PARTICIPATION to RaidRewardBundle(5, 3, 5_000),
        RaidGrade.D to RaidRewardBundle(6, 3, 5_750),
        RaidGrade.C to RaidRewardBundle(7, 4, 6_500),
        RaidGrade.B to RaidRewardBundle(7, 4, 7_250),
        RaidGrade.A to RaidRewardBundle(8, 5, 8_000),
        RaidGrade.S to RaidRewardBundle(9, 5, 8_750),
        RaidGrade.SS to RaidRewardBundle(10, 6, 9_500),
        RaidGrade.SSS to RaidRewardBundle(10, 6, 10_000),
    ).toMap()

    private val successRankRewards = linkedMapOf(
        RankBand.FIRST to RaidRewardBundle(11, 7, 11_000),
        RankBand.SECOND_TO_TENTH to RaidRewardBundle(10, 6, 10_000),
        RankBand.ELEVENTH_TO_HUNDREDTH to RaidRewardBundle(9, 5, 9_000),
        RankBand.REST to RaidRewardBundle(8, 5, 8_000),
    ).toMap()

    private val failureRankRewards = linkedMapOf(
        RankBand.FIRST to RaidRewardBundle(8, 5, 7_700),
        RankBand.SECOND_TO_TENTH to RaidRewardBundle(7, 4, 7_000),
        RankBand.ELEVENTH_TO_HUNDREDTH to RaidRewardBundle(6, 4, 6_300),
        RankBand.REST to RaidRewardBundle(6, 4, 5_600),
    ).toMap()

    enum class RankBand { FIRST, SECOND_TO_TENTH, ELEVENTH_TO_HUNDREDTH, REST }

    fun personalReward(grade: RaidGrade): RaidRewardBundle = personalRewards.getValue(grade)

    fun rankReward(success: Boolean, rank: Int): RaidRewardBundle {
        require(rank >= 1) { "INVALID_RANK" }
        val band = when (rank) {
            1 -> RankBand.FIRST
            in 2..10 -> RankBand.SECOND_TO_TENTH
            in 11..100 -> RankBand.ELEVENTH_TO_HUNDREDTH
            else -> RankBand.REST
        }
        return (if (success) successRankRewards else failureRankRewards).getValue(band)
    }

    fun grade(damage: Long, thresholds: Map<String, Long>): RaidGrade {
        require(damage >= 0) { "INVALID_DAMAGE" }
        if (damage == 0L) return RaidGrade.PARTICIPATION
        return RaidGrade.entries.drop(1).lastOrNull { damage >= (thresholds[it.name] ?: Long.MAX_VALUE) }
            ?: RaidGrade.PARTICIPATION
    }

    fun contribution(grade: RaidGrade, contributions: Map<String, Int>): Long {
        val value = contributions[grade.name] ?: 0
        require(value >= 0) { "INVALID_CONTRIBUTION" }
        return value.toLong()
    }

    fun validateTables(thresholds: Map<String, Long>, contributions: Map<String, Int>) {
        val approvedContributions = linkedMapOf(
            "PARTICIPATION" to 0, "D" to 100, "C" to 167, "B" to 233,
            "A" to 300, "S" to 367, "SS" to 433, "SSS" to 500,
        )
        require(contributions == approvedContributions) { "RAID_CONTRIBUTION_TABLE_INVALID" }
        val b = thresholds["B"] ?: throw IllegalArgumentException("RAID_GRADE_TABLE_INVALID")
        val multipliers = linkedMapOf("D" to 0.35, "C" to 0.65, "B" to 1.0, "A" to 1.5, "S" to 2.2, "SS" to 3.2, "SSS" to 4.5)
        val expected = multipliers.mapValues { (_, multiplier) -> kotlin.math.floor(b * multiplier + 0.5).toLong() }
        require(thresholds == expected) { "RAID_GRADE_TABLE_INVALID" }
    }
}
