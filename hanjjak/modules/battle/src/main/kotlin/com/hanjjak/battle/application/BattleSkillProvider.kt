package com.hanjjak.battle.application

import com.hanjjak.sim.SkillProfile
import java.util.UUID

interface BattleSkillProvider {
    fun forAccount(accountId: UUID): SkillProfile
}

class EmptyBattleSkillProvider : BattleSkillProvider {
    override fun forAccount(accountId: UUID): SkillProfile = SkillProfile()
}
