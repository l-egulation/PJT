package com.hanjjak.gameapi

import com.hanjjak.battle.application.BattleStatsProvider
import com.hanjjak.gameapi.character.CharacterStatsService
import com.hanjjak.sim.FighterStats
import org.springframework.stereotype.Component
import java.util.UUID

@Component
class AccountBattleStatsProvider(private val characterStats: CharacterStatsService) : BattleStatsProvider {
    override fun forAccount(accountId: UUID): FighterStats = characterStats.snapshot(accountId).calculation.fighter
}
