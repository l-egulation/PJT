---
doc_kind: reference
owner_domain: combat-system
authority_level: applied
---

# Combat System

전투 도메인 규칙을 자동 이동·조우·전투·결과 흐름으로 실행하는 논리 시스템이다. 전투 규칙과 미확정 공식은 [Combat Domain SSOT](../../30-domain/combat/ssot.md)가 소유한다.

## 책임 경계

- 소유: 전투 흐름의 실행, 전투 결과 전달, 실패 후 복귀 요청
- 소유하지 않음: UI 표현, 콘텐츠 정의, 보상·거래 권한, AI의 전략 결정

- [State Machine](./state-machine.md)
- [Events](./events.md)
- [Flows](./flows.md)
