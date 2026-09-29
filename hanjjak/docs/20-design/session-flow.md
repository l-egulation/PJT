---
doc_kind: reference
owner_domain: player-experience
authority_level: candidate
---

# Session Flow

현재 플레이 세션을 설명하는 후보 흐름이다. 세부 상태 전이는 각 도메인과 시스템의 정본을 따른다.

```text
웹 진입
  ↓
계정·체크포인트·단일 실행 세션 확인
  ↓
자동 진행
  ↓
선택적 관리 개입
  ↓
체크포인트 저장
  ↓
백그라운드 진행 또는 브라우저 수명주기 중단·재실행
```

- 저장·복원: [Player Domain](../30-domain/player/ssot.md)
- 화면과 표시: [Player UX](../30-domain/player/ux/README.md)
- 자동 진행: [Combat System](../40-systems/combat-system/README.md)
- 인수 흐름: [MVP Acceptance](../70-plans/mvp-release/acceptance.md)
