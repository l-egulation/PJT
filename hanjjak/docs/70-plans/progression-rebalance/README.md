---
doc_kind: plan
owner_domain: progression
authority_level: routing-only
---

# 진행·성장 리밸런싱 설계 진입점

배포 후 초반 반복 정체에 대한 사용자 피드백에서 출발한 차기 설계의 진입점이다. 2026-09-11 승인 구현 구조는 [진행·성장 리밸런싱 v1 설계](../../superpowers/specs/2026-09-11-progression-rebalance-v1-design.md)에 보존한다. 정책·수치·실행 상태는 아래 책임 문서에서만 정의하고 설계 문서에 복제하지 않는다.

| 주제 | 정본 |
| --- | --- |
| 제품 범위·추가 콘텐츠 일정 | [제품 범위의 차기 절](../mvp-release/requirements.md) |
| 진행·성장벽·성장 경험 | [스테이지 SSOT의 차기 절](../../30-domain/world/ssot.md) |
| 등급·강화·승급 비용 | [장비 SSOT의 차기 절](../../30-domain/items/equipment/ssot.md) |
| 재료 공급·지급 경계 | [아이템 SSOT의 차기 절](../../30-domain/items/ssot.md) |
| 최초 보상 수량과 지급 경계 | [최초 클리어 콘텐츠](../../60-content/stages/first-clear-rebalance.md) |
| 1장 반복 재료 공급 | [재료 드롭 콘텐츠](../../60-content/items/material-drops-mvp-v1.md) |
| 최초 스킬 해금 | [스킬 SSOT](../../30-domain/character/skills/ssot.md) |
| 보석 규칙 | [보석 SSOT](../../30-domain/gems/ssot.md) |
| 치장 규칙 | [치장 SSOT](../../30-domain/cosmetics/ssot.md) |
| 레벨·경험치 | [성장 SSOT](../../30-domain/progression/ssot.md) |

- [결정 상태와 전파](../../80-decisions/README.md)
- [A-05 실행 상태·완료 기준](../../wiki/06-delivery/tasks/A-release-scope/a-05-progression-rebalance.md)
- [산술 검증과 남은 검증](./verification.md)

## 적용 순서

`progression-rebalance-v1`은 1~4장·1~79레벨을 절대 수치 구현 범위로 사용한다. 정본 전파 뒤 콘텐츠 산출, 공급·소비와 전투 시뮬레이션, 구현, 기존 계정 소급 dry-run과 전환·인수 검증 순으로 진행한다. 기존 비용과 현재 배포 콘텐츠는 버전 구분으로 보존하며 문서 승인과 플레이 밸런스 검증을 동일하게 취급하지 않는다. 5~10장·80레벨 이후·4-10 신규 보상·레이드 보상·치장 밸런스는 이번 범위에서 제외한다.
