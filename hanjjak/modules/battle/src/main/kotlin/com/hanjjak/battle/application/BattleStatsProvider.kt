package com.hanjjak.battle.application

import com.hanjjak.sim.FighterStats
import java.util.UUID

interface BattleStatsProvider {
    fun forAccount(accountId: UUID): FighterStats
}
