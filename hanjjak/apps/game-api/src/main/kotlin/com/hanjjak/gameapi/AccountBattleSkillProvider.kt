package com.hanjjak.gameapi

import com.hanjjak.battle.application.BattleSkillProvider
import com.hanjjak.gameapi.character.CharacterStatsService
import com.hanjjak.sim.SkillProfile
import org.springframework.stereotype.Component
import java.util.UUID

@Component
class AccountBattleSkillProvider(private val characterStats: CharacterStatsService) : BattleSkillProvider {
    override fun forAccount(accountId: UUID): SkillProfile = characterStats.snapshot(accountId).calculation.skills
}
