# 캐릭터 창 구현 계획

> 실행: subagent-driven-development로 독립 UI·치장 조회 작업을 분담하고 루트에서 능력치·통합·검증을 수행한다.

**Goal:** 합의한 캐릭터 창을 기존 전투 화면과 서버에 연결한다.
**Architecture:** 기존 명령 재사용, 서버 능력치 출처 조회와 치장 카탈로그 추가, 단일 dialog 내부 탭.
**Tech Stack:** React, TanStack Query, Kotlin/Spring Boot, PostgreSQL, TypeSpec.
**Spec:** [승인 설계](./design.md)

## 제약

게임 규칙은 책임 SSOT를 따른다. 외형 아트가 없으면 명시적 대체 표시를 사용한다. 전체 전투 엔진 완성을 이 작업 완료와 혼동하지 않는다. 영구 소유 데이터와 기존 콘텐츠 ID를 임의 삭제·매핑하지 않는다.

## 작업 1: 서버 능력치

- [x] 순수 계산 테스트로 레벨·장비·보석·상시 효과, 반올림·상한·중복 제외를 검증한다.
- [x] apps/game-api의 character 패키지에 조회 모델과 공통 계산을 추가한다.
- [x] 기존 전투 provider가 같은 계산을 사용하고 일시 공속·상시 공속을 분리한다.
- [x] GET /api/v1/character/stats와 TypeSpec 계약을 추가한다.
- [x] Gradle 테스트로 계산과 치장 서비스·명령을 검증한다. 실제 HTTP·DB 인수는 K-22의 미검증 범위로 남긴다.

## 작업 2: 치장 조회·등록

- [x] modules/cosmetics에서 미획득·최저 성급·가용 재고·최대 성급 테스트를 먼저 추가한다.
- [x] 기존 collection에 실행 가능 사유·버전 정보를 추가하고 catalog API를 제공한다.
- [x] 등록·착용 멱등 명령과 계정 버전 처리를 확인한다.
- [x] 66종 번호형 임시 카탈로그, 등급별 성급 임계값과 generic 세트 효과로 clean cutover했다.

## 작업 3: 캐릭터 창

- [x] apps/web/src/features/character에서 API·창·능력치·치장·도감을 분리한다.
- [x] main.tsx의 기존 전투를 유지한 채 하단 캐릭터 버튼으로 창을 연다.
- [x] 창 내부 탭, 매번 능력치 시작, 포커스·Escape, 출처 tooltip, 미리보기 후 착용, 개별 성급 올리기를 연결한다.
- [x] 오류·로딩·잠금·빈 상태·재시도를 검증한다.

## 작업 4: 통합

- [x] 계약 생성·typecheck·웹 테스트·빌드·Gradle 테스트를 수행한다.
- [x] 실제 Chromium에서 테스트 응답 기반 사용자 시나리오를 검증한다 (K-22 증거 참조).
- [x] 번호형 fallback 정보 카드와 마일스톤·선택 상자 API/컴포넌트 테스트를 수행했다.
- [ ] 실제 DB·서버 인수 검증을 수행한다 (Docker 시작 오류로 미검증).
- [x] K-22 작업 및 K-21 증거·색인·기능 경로를 갱신한다.
- [x] diff와 문서 링크를 검토한다.
