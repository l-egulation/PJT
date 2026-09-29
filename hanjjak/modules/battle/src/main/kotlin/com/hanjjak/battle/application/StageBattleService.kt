package com.hanjjak.battle.application

import com.hanjjak.sim.CycleInput
import com.hanjjak.sim.CycleResult
import com.hanjjak.sim.EnemyStats
import com.hanjjak.sim.FighterStats
import com.hanjjak.sim.SkillProfile
import com.hanjjak.stage.application.StageCatalog
import com.hanjjak.stage.domain.BossType

class StageBattleService(private val stages: StageCatalog, private val runner: RunBattleCycle = RunBattleCycle()) {
    fun input(stageId: String, player: FighterStats, seed: Long, contentVersion: String, skills: SkillProfile = SkillProfile()): CycleInput {
        val stage = stages.require(contentVersion, stageId)
        val strikes = if (stage.bossType == BossType.HP_CHECK) stage.scheduledStrikes else emptyMap()
        return CycleInput(
            contentVersion,
            seed,
            player,
            EnemyStats(stage.normalHp, stage.normalAttack, stage.enemyDefense),
            EnemyStats(stage.bossHp, stage.bossAttack, stage.enemyDefense),
            skills,
            normalCount = if (stage.bossOnly || stage.id.number == 10) 0 else 20,
            bossTimeLimitTicks = stage.timeLimitTicks,
            scheduledStrikes = strikes,
        )
    }

    fun run(stageId: String, player: FighterStats, seed: Long, contentVersion: String, skills: SkillProfile = SkillProfile()): CycleResult =
        runner.execute(input(stageId, player, seed, contentVersion, skills))

    fun lastPlayableIndex(contentVersion: String): Int = stages.lastPlayableIndex(contentVersion)
}
