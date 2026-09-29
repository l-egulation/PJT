# Gem dungeon cozy pixel v2 assets

Status: `asset-ready`, not wired to runtime.

This is the production-oriented asset pack selected from the dungeon design exploration. It keeps the approved book and food-monster direction, while using the quieter battle backgrounds and simpler paper UI requested in the final review.

## Included assets

| Area | Assets | Runtime use |
| --- | --- | --- |
| Dungeon selection | Open-book scene, three vertical bookmark panels | Render names, daily open/locked state, weekday, tickets, recharge time, stage, reward, and CTA as HTML/CSS over the blank surfaces. |
| Battle scene | Three 1672 × 941 backgrounds | Select by the current daily boss. |
| Bosses | Three transparent key arts plus three 4 × 5 motion sheets | Key art is suitable for selection/result compositions; motion sheets are for battle animation. |
| Battle UI | Timer shell, skill-card shell, victory and defeat badge shells | Render dungeon/stage, seconds, cooldown, result text, and values in HTML/CSS. |
| Icons | Ticket, gem chest, broom, survival, berserk, armored | Six transparent 512 × 512 files; the original 3 × 2 sheet is also retained. |
| Effects | Three transparent 4 × 2 attack sheets | Eight frames per boss; see `effects/attack-sheets.json`. |

## Boss sprite contract

- Sheet: 1120 × 1400 pixels.
- Cell: 280 × 280 pixels.
- Grid: 4 columns × 5 rows.
- Rows: `idle`, `move`, `attack`, `hit`, `defeat`.
- Playback order: left to right within each row.
- Transparent background.
- Run motion and anchor-point QA in the actual battle viewport before treating the animations as final.

## Reuse instead of generating more

The following pieces already exist elsewhere in the project and should be composed with this pack:

- Player and boss HP containers: `shared/assets/cozy-hud-v1/paper/status-panel-shell-9slice.png` plus CSS progress fills.
- Battle log and progress: `shared/assets/cozy-hud-v1/paper/battle-log-panel-shell-9slice.png`, `stage-progress-shell-9slice.png`, and `controls/progress-fill-coral-9slice.png`.
- Challenge/quit buttons: `shared/assets/onboarding-cozy-pixel/primary-button-shell-9slice.png` and `secondary-button-shell-9slice.png`.
- Player skill icons: `features/skills/assets-cozy-pixel/`.

This avoids baking mutable text, values, cooldowns, HP, daily state, or localized copy into images.

## Content mapping

The SSOT daily rotation maps mechanic types in this order: survival → berserk → armored. The user-selected presentation maps them to:

- Udon Monster → survival.
- Quail Egg Jangjorim Monster → berserk.
- Acorn Jelly Monster → armored.

Names and presentation are asset labels only until the working content document is reconciled with the SSOT.

Generated and normalized on 2026-09-11. Source design drafts remain under `design-drafts/`; the rejected ornate direction remains archived separately for traceability.
