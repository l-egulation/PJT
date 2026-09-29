package com.hanjjak.sim

import java.nio.file.Files
import java.nio.file.Path
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertTrue

class RaidGoldenFixtureTest {
    @Test
    fun `golden fixture matches authoritative raid simulation`() {
        val result = RaidSimulator.simulate(
            RaidCombatInput(
                contentVersion = "raid-golden-v1",
                seed = 1,
                player = FighterStats(100, 1_000_000, 20),
                skills = SkillProfile(activeOrder = emptyList()),
                bossInitialAttack = 0,
                bossInitialDefense = 100,
                maxTicks = 60,
            ),
        )
        val text = Files.readString(Path.of("src/test/resources/golden/raid-combat-v1.json"))

        assertEquals(60, result.elapsedTicks)
        assertEquals(1_000_000, result.remainingPlayerHp)
        assertTrue(text.contains("\"totalDamage\": ${result.totalDamage}"), "authoritative totalDamage=${result.totalDamage}")
        assertTrue(text.contains("\"sequence\": ${result.events.last().sequence}"))
        assertTrue(text.contains("\"logicalTick\": ${result.events.last().logicalTick}"))
        assertTrue(text.contains("\"type\": \"${result.events.last().type}\""))
    }
}
