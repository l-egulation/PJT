---
doc_kind: reference
owner_domain: combat-system
authority_level: candidate
---

# Combat State Machine

현재 문서화된 전투 흐름을 시스템 관점에서 연결한다. 상태 이름과 전이의 확정 여부는 [Combat Domain SSOT](../../30-domain/combat/ssot.md)를 따른다.

```text
자동 이동 → 조우 → 자동 전투 → 결과·보상 → 다음 진행
                         └→ 실패·복귀
```

세부 전투 공식이나 콘텐츠별 수치는 이 문서에 정의하지 않는다.
