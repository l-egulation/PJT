package com.hanjjak.battle.application

import com.hanjjak.sim.FighterStats
import java.time.Instant
import java.util.UUID

interface BattleHistoryRecorder {
    fun dungeonEntered(
        accountId: UUID,
        sourceId: UUID,
        dungeonId: String,
        contentVersion: String,
        player: FighterStats,
        occurredAt: Instant,
    )

    fun dungeonReturned(
        accountId: UUID,
        sourceId: UUID,
        dungeonId: String,
        resultCode: String,
        occurredAt: Instant,
    )
}
