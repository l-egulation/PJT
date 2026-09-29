package com.hanjjak.gems.domain

import java.util.UUID
import java.time.Instant

const val GEM_CONTENT_VERSION = "gem-v1-draft"

enum class GemOption(val label: String) { FLAT_ATTACK("고정 공격력"), FLAT_HP("고정 최대 HP"), ATTACK_PERCENT("공격력%"), FLAT_PENETRATION("고정 방어 관통"), CRITICAL_CHANCE("치명타 확률"), HASTE("공격속도") }
enum class GemPreset(val label: String) { MAIN("메인"), SURVIVAL("생존형"), BERSERK("폭주형"), ARMORED("장갑형") }

data class GemInstance(val gemId: UUID, val accountId: UUID, val level: Int, val option: GemOption, val value: Int, val locked: Boolean)
/** slotId는 이 보석이 들어 있는 가방 칸이다. 잠금은 보석이 아니라 칸에 걸리므로 화면이 이 값을 쓴다. */
data class GemSummary(val gemId: UUID, val level: Int, val option: GemOption, val optionName: String, val value: Int, val locked: Boolean, val reservedForSale: Boolean = false, val equippedPresets: List<GemPreset> = emptyList(), val slotId: UUID? = null)
data class GemState(val unlocked: Boolean, val tickets: Int, val secondsUntilNextTicket: Long, val todayBoss: GemPreset, val gemBoxQuantity: Long, val gems: List<GemSummary>, val presets: Map<GemPreset, List<GemSummary>>, val lockedPresets: Set<GemPreset> = emptySet(), val contentVersion: String = GEM_CONTENT_VERSION)
data class GemOpenRequest(val quantity: Int = 1)
data class GemPresetRequest(val gemIds: List<UUID>)
data class GemPresetUpdateResult(val preset: GemPreset, val mainBattleRestarted: Boolean, val state: GemState)
enum class GemFusionMode { MANUAL, SAFE_BATCH }
data class GemFusionSelection(val option: GemOption, val quantity: Int)
data class GemFusionPreviewRequest(val mode: GemFusionMode, val level: Int? = null, val gemIds: List<UUID> = emptyList(), val selections: List<GemFusionSelection> = emptyList())
data class GemFusionExecuteRequest(
    val mode: GemFusionMode,
    val gemIds: List<UUID> = emptyList(),
    val targetLevel: Int? = null,
    val allowedOptions: List<GemOption> = emptyList(),
)
data class GemFusionConsumption(val option: GemOption, val optionName: String, val quantity: Int)
data class GemFusionPreview(val consumedGemIds: List<UUID>, val consumption: List<GemFusionConsumption>, val fusionCount: Int, val inputLevel: Int, val resultLevel: Int)
data class GemOpenResult(val granted: List<GemSummary>, val state: GemState)
data class GemFusionResult(val consumedGemIds: List<UUID>, val granted: List<GemSummary>, val state: GemState)
data class GemCombatStats(val flatAttack: Int = 0, val flatHp: Int = 0, val attackPercentBasisPoints: Int = 0, val flatPenetration: Int = 0, val criticalChanceBasisPoints: Int = 0, val hasteBasisPoints: Int = 0)
enum class GemDungeonChallengeStatus { ACTIVE, SUCCEEDED, FAILED, ABORTED, EXPIRED }
data class GemDungeonProgress(val boss: GemPreset, val highestClearedStage: Int)
data class GemDungeonToday(
    val boss: GemPreset,
    val tickets: Int,
    val secondsUntilNextTicket: Long,
    val progress: List<GemDungeonProgress>,
    val nextChallengeStage: Int?,
    val sweepStage: Int?,
    val activeChallenge: GemDungeonChallengeView?,
    val testBossSelectionEnabled: Boolean = false,
)
data class GemDungeonChallengeView(
    val challengeId: UUID,
    val boss: GemPreset,
    val stage: Int,
    val status: GemDungeonChallengeStatus,
    val minimumCompleteAt: Instant,
    val expiresAt: Instant,
    val rewardGemBoxes: Long,
    val contentVersion: String,
    val battle: com.hanjjak.sim.DungeonCombatResult,
)
data class GemDungeonCompletion(val challenge: GemDungeonChallengeView, val state: GemState)
data class GemDungeonStartRequest(val boss: GemPreset? = null)
data class GemDungeonSweepRequest(val count: Int = 1)
data class GemDungeonSweepResult(val boss: GemPreset, val stage: Int, val count: Int, val rewardGemBoxes: Long, val state: GemState)

object GemRules {
    fun fixedValue(level: Int, option: GemOption): Int = when (option) {
        GemOption.FLAT_ATTACK -> listOf(0, 3, 7, 15, 32, 70, 150, 320)[level]
        GemOption.FLAT_HP -> listOf(0, 30, 70, 150, 320, 700, 1500, 3200)[level]
        GemOption.ATTACK_PERCENT -> if (level == 6) 800 else 1500
        GemOption.FLAT_PENETRATION -> if (level == 6) 60 else 120
        GemOption.CRITICAL_CHANCE -> 1200
        GemOption.HASTE -> 1200
    }
    fun optionsFor(level: Int): List<Pair<GemOption, Int>> = when (level) {
        in 1..5 -> listOf(GemOption.FLAT_ATTACK to 5000, GemOption.FLAT_HP to 5000)
        6 -> listOf(GemOption.FLAT_ATTACK to 3500, GemOption.FLAT_HP to 3500, GemOption.ATTACK_PERCENT to 1500, GemOption.FLAT_PENETRATION to 1500)
        7 -> listOf(GemOption.FLAT_ATTACK to 3500, GemOption.FLAT_HP to 3500, GemOption.ATTACK_PERCENT to 750, GemOption.FLAT_PENETRATION to 750, GemOption.CRITICAL_CHANCE to 750, GemOption.HASTE to 750)
        else -> throw IllegalArgumentException("INVALID_GEM_LEVEL")
    }
    fun autoLocked(option: GemOption): Boolean = option !in setOf(GemOption.FLAT_ATTACK, GemOption.FLAT_HP)
    fun boxLevel(roll: Int): Int = when {
        roll < 8500 -> 1
        roll < 9500 -> 2
        else -> 3
    }

    /** Fixed 생존형 → 폭주형 → 장갑형 cycle over the rotation slot index supplied by [GemDungeonPolicy]. */
    fun bossForSlot(slotIndex: Long): GemPreset =
        listOf(GemPreset.SURVIVAL, GemPreset.BERSERK, GemPreset.ARMORED)[Math.floorMod(slotIndex, 3L).toInt()]

    fun todayBoss(epochDay: Long): GemPreset = bossForSlot(epochDay)
}
