# Skill VFX runtime assets

Generated from the approved visual drafts. Every runtime PNG is a transparent horizontal sprite sheet.

- `active-heavy-target-8f.png`: approved `active_heavy` target impact, 8 frames
- `active-dot-target-8f.png`: approved `active_dot` simmering target mark, 8 frames
- `haste-caster-8f.png`: 8 × 256×256
- `active-basic-amp-caster-8f.png`: approved `active_basic_amp` caster buff, 8 × 512×512 cells; the narrow concept strip is repacked without aspect-ratio distortion

All four active-skill runtime mappings use `background-size: 800% 100%`. The heavy impact plays once on the current target. The DOT mark follows the current DOT target. The two caster buffs loop at the shared character crown anchor and blink during their final three seconds.

Legacy draft sheets remain only as source references and are not mapped by the runtime. Do not attach these effects to weapon layers.
