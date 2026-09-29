# Game Domain

게임 규칙과 게임 개념을 소유하는 계층이다. 각 책임 문서의 규칙·수치·상태 전이는 하나의 SSOT에서만 정의한다.

| 게임 개념 | 규칙 정본 | 기능 경로 |
| --- | --- | --- |
| Player | [Player SSOT](player/ssot.md) | [Player features](player/features/_index.md) |
| Character | [Character SSOT](character/ssot.md) | [Character features](character/features/_index.md) |
| Combat | [Combat SSOT](./combat/ssot.md) | [Combat features](./combat/features/_index.md) |
| World | [World SSOT](world/ssot.md) | [World features](world/features/_index.md) |
| Items | [Items SSOT](./items/ssot.md) | [Items features](./items/features/_index.md) |
| Gems | [Gems SSOT](./gems/ssot.md) | [Gems features](./gems/features/_index.md) |
| Raid | [Raid SSOT](./raid/ssot.md) | [Raid features](./raid/features/_index.md) |
| Cosmetics | [Cosmetics SSOT](./cosmetics/ssot.md) | [Cosmetics features](./cosmetics/features/_index.md) |
| Quest | 아직 정본 없음 | [Quest placeholder](quest/README.md) |
| Progression | [Progression SSOT](./progression/ssot.md) | [Progression features](./progression/features/_index.md) |
| Economy | [Economy SSOT](economy/ssot.md) | [Economy features](economy/features/_index.md) |
| Social | 아직 정본 없음 | [Social placeholder](social/README.md) |

세부 책임은 Character의 [Skills](character/skills/ssot.md), Items의 [Equipment](./items/equipment/ssot.md), [Gems](./gems/ssot.md), Player의 [UX](player/ux/ssot.md)로 분리해 보존한다.
