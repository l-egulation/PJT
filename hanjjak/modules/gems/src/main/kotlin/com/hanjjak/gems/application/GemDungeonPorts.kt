package com.hanjjak.gems.application

import com.hanjjak.gems.domain.GemPreset
import com.hanjjak.sim.FighterStats
import com.hanjjak.sim.SkillProfile
import java.util.UUID

data class GemDungeonBaseSnapshot(val player: FighterStats, val skills: SkillProfile)

interface GemDungeonPlayerProvider {
    fun snapshot(accountId: UUID, preset: GemPreset): GemDungeonBaseSnapshot
}
