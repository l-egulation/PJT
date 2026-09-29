package com.hanjjak.battle.application

import com.hanjjak.sim.CombatSimulator
import com.hanjjak.sim.CycleInput
import com.hanjjak.sim.CycleResult

class RunBattleCycle {
    fun execute(input: CycleInput): CycleResult = CombatSimulator.simulate(input)
}
