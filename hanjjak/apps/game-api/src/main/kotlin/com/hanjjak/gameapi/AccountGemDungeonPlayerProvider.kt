package com.hanjjak.gameapi

import com.hanjjak.gameapi.character.CharacterStatsService
import com.hanjjak.gems.application.GemDungeonBaseSnapshot
import com.hanjjak.gems.application.GemDungeonPlayerProvider
import com.hanjjak.gems.domain.GemPreset
import org.springframework.stereotype.Component
import java.util.UUID

@Component
class AccountGemDungeonPlayerProvider(
    private val characterStats: CharacterStatsService,
) : GemDungeonPlayerProvider {
    override fun snapshot(accountId: UUID, preset: GemPreset): GemDungeonBaseSnapshot {
        val calculation = characterStats.snapshot(accountId, preset).calculation
        return GemDungeonBaseSnapshot(calculation.fighter, calculation.skills)
    }
}
