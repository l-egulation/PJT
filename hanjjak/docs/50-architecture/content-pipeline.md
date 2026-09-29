---
doc_kind: ssot
owner_domain: architecture
authority_level: applied
---

# Content Pipeline

정적 콘텐츠는 버전 단위로 검증·배포하고 런타임 코드와 분리한다. 콘텐츠 모델과 배치의 정본은 [Content](../60-content/README.md), 저장 권한과 배포 경계는 [Persistence](./persistence.md)와 [Operations](./operations.md)를 따른다.

## 계약과 생성 방향

- TypeSpec이 HTTP API와 콘텐츠 schema 정의의 유일한 편집 원본이다.
- TypeSpec에서 생성한 OpenAPI와 콘텐츠 JSON Schema는 검토·배포 가능한 계약 산출물로 저장소에 커밋한다. 생성 산출물을 직접 편집하지 않는다.
- TypeScript client와 Kotlin API interface·DTO는 빌드 시 생성하며 저장소에 커밋하지 않는다.
- Spring adapter는 생성된 API interface를 구현한다. 수작업 controller나 DTO로 같은 외부 계약을 중복 정의하지 않는다.
- 콘텐츠 validator는 생성된 JSON Schema뿐 아니라 manifest checksum, ID 참조, 콘텐츠 버전과 도메인별 전체 참조 무결성을 검사한다.
- CI는 TypeSpec을 다시 컴파일해 커밋된 OpenAPI·JSON Schema가 최신인지 검사하고 계약 breaking change를 별도 검증한다.

실제 콘텐츠 값은 [콘텐츠 구조와 버전](../60-content/schema.md) 및 각 콘텐츠 SSOT의 applied·working·unresolved 상태를 따른다. 미확정 값을 생성 도구가 임의 기본값으로 채우지 않는다.
