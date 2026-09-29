---
doc_kind: reference
owner_domain: architecture
authority_level: proposed
---

# Runtime Overview

현재 제안된 런타임 배치는 다음 문서로 나뉜다.

```text
Player
  ↓
Game Client
  ↓
Server API / Runtime Modules
  ↓
Persistence and Outbox
```

- 게임 규칙 실행: [Gameplay Systems](../40-systems/README.md)
- 클라이언트 표현: [Client](./client.md)
- 서버 권한과 거래: [Server](./server.md)
- 저장·복원: [Persistence](./persistence.md)
- 배포·운영: [Operations](./operations.md)

초기 배포 경계와 서비스 분리는 운영 근거가 생길 때 별도 결정으로 확정한다.
