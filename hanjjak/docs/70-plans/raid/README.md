---
doc_kind: reference
owner_domain: raid
authority_level: planning-reference
---

# 레이드 기획 검토

2026-09-10부터 2026-09-13까지 사용자와 진행한 레이드 기획의 검토 진입점이다. 규칙·수치·상태 전이와 남은 콘텐츠 산출은 [레이드 SSOT](../../30-domain/raid/ssot.md) 한 곳에서 관리하고, 합의 근거와 검증 기준은 [일일 공동 봉인 레이드 승인 설계](../../superpowers/specs/2026-09-13-daily-seal-raid-design.md)에 보존한다. 이 계획은 구현이나 출시를 승인하는 문서가 아니다.

## 기획 의도

개인 성장 결과를 전체 서버의 일일 공동 봉인 성과로 연결한다. 짧게 참여하는 사용자와 더 나은 전투 결과를 탐색하는 사용자 모두 자신의 참여 강도를 선택할 수 있고, 목표를 조기에 달성해도 KST 17:30 정산까지 늦은 참여와 순위 경쟁을 보장하는 경험을 목표로 한다. 기존 던전과의 규칙 비교는 [보석 SSOT](../../30-domain/gems/ssot.md)를 참조한다.

## 후속 검토

- 승인된 운영·등급·보상 규칙으로 보스 초기 공격력·방어력, 기준 빌드와 B 절대 피해 커트라인을 시뮬레이션한다.
- 같은 투자 규모의 공격·생존·관통 세팅을 비교하여 특정 성장축이 항상 우월해지는지 검토한다.
- 참여만 하루 3회 확정해도 계정당 치장권 15장·보석함 9개·쌀 15,000이 공급되는 기준을 포함해 전체 경제 영향을 검증한다.
- 구현 요청 시 전용 task를 생성하고 [작업 색인](../../wiki/06-delivery/tasks/_index.md)과 [기능 경로](../../30-domain/raid/features/shared-boss.md)에 연결한다. 현재 문서화 작업으로 기존 구현 task의 완료 상태를 바꾸지 않는다.

## 관련 정본

- [레이드 도메인 진입점](../../30-domain/raid/README.md)
- [전투 SSOT](../../30-domain/combat/ssot.md)
- [치장 SSOT](../../30-domain/cosmetics/ssot.md)
- [보석 SSOT](../../30-domain/gems/ssot.md)
- [성장 SSOT](../../30-domain/progression/ssot.md)
- [현재 클라이언트 노출 정책](../../30-domain/player/ux/ssot.md)
- [결정 로그](../../80-decisions/README.md)
