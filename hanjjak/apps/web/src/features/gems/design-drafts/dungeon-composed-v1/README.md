# Dungeon composed v1 mockups

Status: `design-draft`, not wired to runtime.

Two desktop mockups compose the approved `assets-cozy-pixel-v2` pack into the intended screens:

- `dungeon-selection-composed-v1.png`: daily three-dungeon selection on the open recipe book. Udon is today's active survival dungeon; the other two entrances show their next opening day.
- `dungeon-battle-udon-composed-v1.png`: Udon survival battle with player/boss HP, compact stage timer, three skills, battle log, survival progress, reward marker, and quit action.

## Locked composition decisions

- No website navigation is included in the game viewport mockup.
- The battle timer shows only stage, mechanic type, and remaining time; the large objective sentence is omitted.
- The battle log stays compact at bottom left rather than occupying a right-side rail.
- Mutable labels, timers, HP, cooldowns, tickets, days, rewards, and progress should be HTML/CSS in implementation.
- The selection screen uses the three boss labels supplied by the user: Udon Monster, Quail Egg Jangjorim Monster, and Acorn Jelly Monster.

## Generation prompts

Built-in image generation was used on 2026-09-12 with the approved book scene, boss key art, battle background, timer shell, skill-card shell, and user-selected mockups as references.

Selection prompt summary: compose a 16:9 high-fidelity pixel-art game UI with three bookmark-shaped dungeon entrances on the left book page and the selected daily boss, entry state, progress, reward, sweep, and challenge CTA on the right page. Preserve supplied assets and avoid ornate fantasy decoration.

Battle prompt summary: compose a 16:9 Udon survival battle with HP panels at the upper corners, compact stage/timer panel at top center, player and boss in a spacious arena, three skills at bottom center, compact battle log at bottom left, progress/reward along the bottom, and quit at bottom right. Omit the large objective sentence and the right-side event rail.
