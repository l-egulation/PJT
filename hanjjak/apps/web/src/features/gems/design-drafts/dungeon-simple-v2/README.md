# Dungeon simple v2 design draft

Status: `promoted-to-asset-pack`, not wired to runtime.

This pass keeps the approved monster/book direction and regenerates the remaining dungeon assets with the user's finalized captures as strict visual references.

## Direction lock

- Cute, compact 2D pixel art with large visible pixel clusters.
- Simple silhouettes and only 2-3 shading tones per material.
- Quiet, low-contrast environments so combatants remain dominant.
- Minimal paper UI: thin outlines, almost no brass, rivets, leaves, or fantasy ornament.
- Copy, numbers, timers, HP, cooldowns, open/locked state, and progress remain HTML/CSS.

## Contents

- `dungeon-selection-book-v1-kept.png`: accepted blank open-book scene.
- `bosses/`: accepted boss direction plus the simplified Udon comparison.
- `backgrounds/`: simplified battle arenas for Udon, quail-egg jangjorim, and acorn jelly.
- `ui/`: simplified bookmarks, timer shell, skill-card shell, and a 3 x 2 icon sheet.

## Runtime readiness

The files in this folder remain visual-selection drafts. Cleaned transparent PNGs, normalized sprite grids, split icons, attack effects, result badges, and machine-readable metadata were promoted to `../../assets-cozy-pixel-v2/`. Responsive composition and runtime motion QA are still required during integration.

Generated with the built-in image generation tool on 2026-09-11.
