---
doc_kind: reference
owner_domain: game-loop
authority_level: candidate
---

# Meta Loop

현재 한짝의 장기 경험을 설명하기 위한 후보 흐름이다. 별도의 run 기반 규칙으로 확정된 문서는 아니며, 미확정 사항은 [결정 로그](../80-decisions/README.md)에서 관리한다.

```text
세션 시작
  ↓
자동 진행과 보상 축적
  ↓
성장·장비·스킬·전문·거래 선택
  ↓
체크포인트 저장 또는 브라우저 수명주기 정책에 따른 복원
  ↓
다음 세션
```

세션과 브라우저 수명주기 중단·복귀의 입력·권한은 [Player Domain](../30-domain/player/ssot.md)이 소유한다.
