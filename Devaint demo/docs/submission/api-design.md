# Deviant API 설계 문서

| 항목 | 내용 |
|---|---|
| 기준 | 2026-11-16, `9bfde2398` |
| 대상 | Go HTTP 플레이 데이터 수집 서버의 실제 구현 API |
| 구현 원본 | [http.go](../../backend/collector/http.go), [store.go](../../backend/collector/store.go), [replay.go](../../backend/collector/replay.go) |
| 연계 | [요구사항 FR-19~21](requirements.md), [ERD](erd.md) |

## 1. 서비스 범위

플레이 기록의 등록·업로드·조회·다운로드·삭제를 제공한다.

| 구분 | 주소 |
|---|---|
| 데모 base URL | `https://deviant-log.verte.kr` |
| 수집 API | `/v1/` |
| 관리자 API | `/admin/` |

## 2. 공통 규약

- JSON 요청·응답은 UTF-8이다. JSON 입력 본문은 8KiB 이하이며 엄격한 디코딩을 적용한다.
- player/session/segment는 UUID다. API 입력의 일반 UUID 표기를 정규화하되 raw 리플레이 header는 소문자 N 형식을 따른다. 기존 UUIDv4와 새 session/segment UUIDv7을 지원한다.
- UTC 시각은 RFC3339 계열 문자열, 경과시간은 밀리초 정수, seq는 session 전체의 증가 순번이다.
- 공개 쓰기 API의 `X-Player-ID`는 연결 확인용이다.
- `/admin/`에는 `Authorization: Bearer <ADMIN_TOKEN>`이 필요하다. 서버 토큰은 24자 이상이며 클라이언트에 포함하지 않는다.
- CORS는 설정한 정확한 origin만 허용한다. 허용 origin의 OPTIONS는 204다.
- 공통 응답에 `Cache-Control: no-store`, `X-Content-Type-Options: nosniff`를 적용한다.

## 3. 엔드포인트 목록

| ID | 메서드·경로 | 인증·연결 확인 | 성공 |
|---|---|---|---|
| API-01 | `GET /healthz` | 없음 | 200, DB 연결 상태 |
| API-02 | `POST /v1/sessions` | 없음, body의 player/session | 200, 등록 |
| API-03 | `PUT /v1/sessions/{session}/chunks/{segment}/{index}` | X-Player-ID, X-Replay-SHA256 | 200, 확정 SHA |
| API-04 | `PUT /v1/sessions/{session}/status` | X-Player-ID | 200, 상태 병합 |
| API-05 | `GET /admin/players` | 관리자 Bearer | 200, 배열 |
| API-06 | `GET /admin/players/{player}/sessions` | 관리자 Bearer | 200, 배열 |
| API-07 | `GET /admin/sessions/{session}` | 관리자 Bearer | 200, 세션 상세 |
| API-08 | `GET /admin/sessions/{session}/events` | 관리자 Bearer | 200, 사건·커서 |
| API-09 | `GET /admin/sessions/{session}/chunks/{segment}/{index}` | 관리자 Bearer | 200, raw 다운로드 |
| API-10 | `DELETE /admin/players/{player}` | 관리자 Bearer | 204, 본문 없음 |

## 4. 공개 수집 API

### API-01 상태 확인

`GET /healthz` → `{"status":"ok"}`. DB ping 실패 시 503 `database_unavailable`을 반환한다.

### API-02 세션 등록

`Content-Type: application/json` 요청:

```json
{
  "player_id": "11111111111141118111111111111111",
  "session_id": "22222222222242228222222222222222",
  "build_id": "demo-build-id",
  "platform": "web",
  "started_at": "2026-11-16T00:00:00Z",
  "consent_version": 1
}
```

위 UUID와 시각은 형식 예시다. 실제 요청에는 실행 시점 값을 사용한다.

| 필드 | 타입·검증 |
|---|---|
| player_id, session_id | string, 유효 UUID |
| build_id | string, `^[A-Za-z0-9._-]{1,128}$` 형식 |
| platform | `web`, `windows`, `linux`, `osx`, `editor` 중 하나 |
| started_at | string, 최초 등록 시 서버 현재의 과거 7일~미래 10분 이내 |
| consent_version | integer, 수집 프로토콜 호환값 1 |

응답은 `{"state":"registered"}`. 같은 session의 player/build/platform/시작 시각이 같으면 재등록에 성공한다. 불변 연결이 달라지면 409, 만료면 410이다. 세션 등록은 앱 실행 단위이며 마을·여러 런이 같은 session에 포함될 수 있다.

### API-03 조각 업로드

`index`는 0~100000 정수다. 요청 헤더:

```http
Content-Type: application/octet-stream
X-Player-ID: 11111111111141118111111111111111
X-Replay-SHA256: <압축 원본 바이트의 SHA-256 소문자 64자리>
```

본문은 compact-v1 gzip 원본이며 `Content-Encoding`은 생략한다.

해시·파일 형식·세션 연결을 검사한다. 경로와 파일의 식별자가 다르면 409 `header_path_conflict`를 반환한다.

```json
{"state":"committed","sha256":"<요청과 일치하는 SHA-256>","duplicate":false}
```

저장 완료 시 `committed`와 SHA-256을 반환한다. 동일 기록 재전송은 `duplicate:true`, 같은 키의 다른 해시는 409다.

| 한도 | 값 |
|---|---|
| 압축 / 해제 바이트 | 4MiB / 16MiB |
| header / 단일 레코드 | 64KiB / 256KiB |
| 조각 레코드 수 | 최대 65,537 |
| 추출 사건 / JSON payload 합계 | 최대 16,384 / 8MiB |
| 동시 수집 요청 | 4개 |
| 기본 player 일일 raw / 전체 논리 저장량 | 64MiB / 2GiB |
| 등록 session 수 | 최대 100,000 |

### API-04 세션 상태

같은 player 헤더와 JSON을 보낸다.

```json
{"last_ms":1000,"last_seq":42,"ended":true,"truncated":false}
```

`last_ms/last_seq`는 음수가 아닌 정수, `ended/truncated`는 boolean이다. 응답은 `{"state":"updated"}`. 최신 경과시간·순번과 종료·중단 상태를 유지한다.

## 5. 관리자 조회·삭제

### API-05 플레이어 목록

`?limit=200`. 최근 수신 기준 목록이며 기본·최대 200개다. 각 객체는 `player_id`, `sessions`, `chunks`, `bytes`, `last_received_at`을 포함한다. 목록 cursor는 없다.

### API-06 플레이어 세션 목록

`?limit=200`. 시작 시각 내림차순의 세션 배열이다. 아래 sessionView를 사용하되 `chunks`는 생략한다. limit가 없거나 잘못된 범위이면 최대값을 사용한다.

### API-07 세션 상세

| 필드 | 타입·의미 |
|---|---|
| session_id, player_id, build_id, platform | string, 연결 식별 |
| started_at, updated_at, expires_at | UTC string |
| last_ms, last_seq | integer, 마지막 선언값 |
| ended, truncated | boolean |
| state | `unknown_end`, `partial`, `complete`, `truncated` |
| coverage_complete | boolean, 관측된 session 순번·구간 index·이전 SHA 연속성 |
| chunks | 조각이 있을 때 배열. session 전체 first_seq 순 |

조각 객체는 `segment_id`, `chunk_index`, `sha256`, `previous_sha256`, `bytes`, `start_ms`, `end_ms`, `first_seq`, `last_seq`, `scene`, `run_id`, `recorded_from`, `recorded_to`를 포함한다. UUIDv7 구간에는 `segment_created_at`을 추가한다. 만료 세션 상세는 410이다.

### API-08 사건 조회

`?after_seq=0&limit=1000&snapshots=1`을 지원한다. `after_seq`는 음수가 아닌 정수이며 기본 0, limit 기본·최대 1000이다. snapshots는 정확히 `1`일 때만 상태 snapshot을 포함한다.

```json
{
  "events": [
    {"seq":42,"time_ms":1000,"sim_tick":60,"kind":"ui_activate","snapshot":false,"payload":{}}
  ],
  "next_after_seq":42
}
```

`payload`는 사건 상세 값이다. `next_after_seq`로 다음 목록을 조회하고 빈 배열이면 종료한다.

### API-09 원본 다운로드

`application/octet-stream`, `Content-Disposition: attachment; filename="recording.replay.gz"`로 원본 gzip을 반환한다. 세션·조각 또는 파일이 없으면 404, 만료 세션이면 410이다.

### API-10 플레이어 삭제

플레이어와 관련 세션·기록·원본 파일을 삭제한다.

## 6. 오류·재시도

핸들러의 오류 본문은 `{"error":"코드"}`다.

| HTTP | 대표 코드 | 처리 |
|---|---|---|
| 400 | invalid_json, invalid_request, invalid_id, invalid_player_id, invalid_chunk_index, invalid_sha256, sha256_mismatch, invalid_replay, invalid_cursor | 입력·원본 확인 |
| 401 | admin_required | 관리자 자격 확인 |
| 403 | origin_not_allowed | CORS 설정 확인 |
| 404 | not_found | 대상 확인 |
| 409 | identity_or_chunk_conflict, header_path_conflict | ID·원본 연결 확인 |
| 410 | session_expired | 자동 재전송 중단 |
| 413 | body_too_large | 본문 크기 확인 |
| 415 | send_gzip_as_octet_stream | Content-Encoding 제거 |
| 429 | busy | Retry-After: 5 |
| 429 | storage_quota | Retry-After: 3600 |
| 500 / 503 | storage_error / database_unavailable | 서버 복구·재시도 |

네트워크 오류·5xx는 2초~5분 간격으로 재시도하고 429는 Retry-After를 따른다. 영구 4xx는 자동 전송을 중단하고 오류 파일을 보존한다.

## 7. 관련 검증

[테스트 계획·결과서](test-plan-results.md)의 AT-01·02에 서버 API 검사 결과를 정리한다.
