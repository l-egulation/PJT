# 리플레이 서버 전송·저장

[분야 개요](README.md)

## 범위와 현재 구현

현재 데모의 수집 API base URL은 **`https://deviant-log.verte.kr`**이다. 공개 상태 확인 경로는 `https://deviant-log.verte.kr/healthz`이며, 클라이언트 빌드에는 `/v1`을 제외한 base URL을 포함한다.

계정 없는 데모를 위한 Go HTTP API + SQLite + 파일 저장 구현이다. 서버 코드는 [`backend/collector`](../../../backend/collector/README.md), player ID/클라이언트 연결 계획은 [player_id 수집](player-data-collection.md), 원본 형식은 [로컬 기록](replay-recording.md)을 따른다. 공급자·별도 worker·presigned URL·수집 토큰 발급을 요구하던 초기 확장 제안은 현재 구현 계약이 아니다.

한 프로세스/컨테이너와 하나의 영속 볼륨을 사용한다. 배포용 Dockerfile·Compose와 선택적 Cloudflare Tunnel 프로필을 제공한다. 터널의 도메인/route 생성과 운영 토큰 설정은 별도이며 [서버 실행 안내](../../../backend/collector/README.md#cloudflare-tunnel로-외부-노출)를 따른다. 웹 대시보드·자동 숙련도 점수·완성된 게임 화면 재생기는 포함하지 않는다.

## ID·설정·수집 경계

`player_id`는 자동 서버 전송 시작 시 생성해 PlayerPrefs에 유지하는 무작위 UUID다. 사용자 계정/하드웨어 식별자/인증 자격이 아니다. 웹 저장 영역과 바이너리 저장 영역이 다르면 서로 다른 플레이어로 취급한다. 정밀한 동일인 추적이나 복구 토큰은 추가하지 않는다.

`session_id`는 앱 실행마다 하나다. 마을 → 여러 런 → 귀환을 같은 session에 보관하고, 씬/전투 리셋/기록 재개는 별도 segment로 나눈다. run은 실제 런 구간에만 연결한다. session 시간/순번은 런이 바뀌어도 계속 증가한다.

기록은 첫 실행부터 기본 켜짐이며 서버 endpoint가 있으면 자동 전송한다. 별도의 서버 전송 동의는 두지 않는다. 이전에 명시적으로 중단한 기록 설정은 유지한다. 서버 endpoint가 없으면 로컬 기록만 수행한다. 자동 전송 대상인 새 조각에만 player/session/endpoint binding을 붙이며 이전 로컬 전용 파일에는 소급 적용하지 않는다. 기록 중단은 새 기록·진행 중 전송을 중단하고 로컬 큐를 삭제한다. 이미 저장된 데이터의 삭제는 관리자 API로 한다.

공개 쓰기 API는 UUID와 일치 관계·입력 크기·동시성·바이트 제한을 검사하지만 ID 소유권을 인증하지 않는다. ID를 아는 조작 클라이언트의 허위 업로드/상태 갱신을 방지하는 서비스가 아니다. 관리자 조회·다운로드·삭제는 별도의 서버 Bearer 토큰으로 보호한다. 게임에 이 토큰을 넣지 않는다. 수집기는 IP별·분당 요청 횟수를 제한하지 않으며 IP를 플레이 데이터/접근 로그에 저장하지 않는다.

## 저장 모델

등록 payload와 DB의 `consent_version=1`은 기존 수집 프로토콜 호환값이다. 기본 자동 수집도 이 wire version을 유지하며, 이 필드를 사용자의 명시적인 동의 행동을 증명하는 값으로 해석하지 않는다.

| 저장 위치 | 역할과 주요 필드 |
|---|---|
| `players` | player ID, 첫 서버 등록 시각 |
| `sessions` | player FK, 앱 session, build/platform, 클라이언트 시작 UTC, 마지막 수신 UTC, 만료, 기존 수집 계약 버전, 마지막 실제 ms/seq, ended/truncated |
| `segments` | session+segment PK, run ID 또는 빈 문자열, 씬 경로, content hash/UI 버전 |
| `chunks` | session+segment+index PK, SHA/이전 SHA, 실제 크기·시간/순번 범위·레코드 수·수신 시각 |
| `events` | session+seq PK, 원본 segment/index, 실제 ms/sim tick, kind, snapshot 여부, 해석용 JSON payload |
| 볼륨 `chunks/{session}/{segment}/{index}.replay.gz` | 기존 compact-v1 gzip 바이트 원본. 커서/이동 표본은 이곳에 보관 |

SQLite는 WAL·외래키/cascade를 사용한다. events는 `ui_activate`, 플레이어의 `player_command`, 플레이어가 가하거나 받은 `player_hit`, `run_state`, `scene_state`, `screen_state`, `weapon_state`, `growth_state`(공격 강화 단계), `economy_state`, `capture_gap`를 추출한다. 잉크/팔레트 상세 구성은 raw Growth 레코드에 남는다. 이동/커서 전체 표본을 SQL 행으로 복제하지 않는다.

payload의 `values`는 [schema 1 레지스트리](replay-recording.md#10-현재-구현-사용과-제한)의 논리 signed_values 순서이고, `resolved`는 조각별 사전 참조를 실제 콘텐츠/액션/UI 문자열로 해석한 `{필드인덱스:문자열}`이다. 값이 float bits인 필드는 비트 재해석 후 계산해야 한다. JSON 정수 자체를 피해량/시간으로 더하지 않는다.

각 조각의 시작 snapshot도 필요 상태로 저장하되 `is_snapshot=1`로 구분한다. 기본 사건 API는 이를 제외한다. snapshot을 무기 교체·성장 선택·실제 클릭 횟수로 중복 계산하지 않는다. 메뉴 클릭은 거래 성공 증거가 아니며 기존 로컬 기록에 없는 마을 거래 결과/성능 진단이 이 서버 구현으로 새로 생기지는 않는다.

관리자 session 조회의 조각 목록은 세션 내 `first_seq` 순서로 반환한다. 구간이 바뀌어도 한 타임라인으로 읽으며 업로드 도착 순서는 사용하지 않는다. 새 클라이언트의 session/segment는 UTC ms를 담은 UUIDv7이고 기존 UUIDv4도 계속 허용한다. DB/원본 형식과 기존 경로는 바꾸지 않는다.

조각 응답의 `segment_created_at`은 UUIDv7에서 읽은 ID 생성 UTC다(v4/해석 불가 시 생략). `recorded_from`, `recorded_to`는 session `started_at`에 각각 `start_ms`, `end_ms`를 더한 기록 UTC이며 이전 v4 기록에도 제공한다. 조회 시 계산하므로 원본/DB에 중복 시각 열을 저장하지 않는다. 시각은 클라이언트 제공 정보이고 UUID 시각으로 만료/권한을 결정하지 않는다. UUID 정렬은 보조이며 기존 전역 seq·경과 시간이 재생 순서의 기준이다.

## API와 업로드 처리

| 요청 | 계약 |
|---|---|
| `GET /healthz` | DB 연결 상태. 인증 불필요 |
| `POST /v1/sessions` | player/session/build/platform/started_at/consent_version 등록. 같은 불변 값은 재시도 성공, 다른 연결은 409 |
| `PUT /v1/sessions/{session}/chunks/{segment}/{index}` | raw gzip + X-Player-ID/X-Replay-SHA256. 압축·header·record·footer 검증 후 파일/DB 확정 |
| `PUT /v1/sessions/{session}/status` | 같은 player 헤더, last_ms/last_seq/ended/truncated 단조 병합 |
| `GET /admin/players` | 관리자용 최근 수신 플레이어 요약 |
| `GET /admin/players/{player}/sessions` | 관리자용 최근 세션 목록 |
| `GET /admin/sessions/{session}` | 메타데이터·조각·기록 완전성 |
| `GET /admin/sessions/{session}/events` | after_seq cursor로 주요 사건 조회 |
| `GET /admin/sessions/{session}/chunks/{segment}/{index}` | 관리자용 원본 다운로드 |
| `DELETE /admin/players/{player}` | 플레이어의 현재 DB/파일 삭제 |

업로드 body는 `application/octet-stream`이며 Content-Encoding을 설정하지 않는다. 같은 `(session,segment,index)`의 같은 해시는 중복 성공, 다른 해시는 409다. 서버는 제한된 gzip 해제/형식 검사를 마친 원본을 임시 파일에 쓰고 fsync·rename·상위 디렉터리 sync 뒤 DB 트랜잭션을 commit한다. 그 다음 `{"state":"committed","sha256":"…","duplicate":false}`를 반환한다.

파일 쓰기와 DB는 하나의 원자적 트랜잭션이 아니다. 파일 확정 뒤 DB commit 실패/프로세스 중단은 고아 파일을 남길 수 있으며 재시도 또는 한 시간 지난 고아 정리가 처리한다. 이미 DB에 있는 같은 조각의 파일이 유실/손상되면 동일 해시 재업로드로 파일을 복구한다. 외부에서 확정 파일을 수정하지 않는 단일 프로세스 운영을 전제로 한다.

클라이언트는 한 번에 한 파일을 전송하고, 정확한 committed SHA를 확인한 뒤 로컬 raw/receipt/binding을 삭제한다. 네트워크/5xx는 jitter를 둔 2초~5분 지수 backoff, 429는 Retry-After를 따른다. 그 외 영구 4xx는 원본을 유지한 채 upload-error 표식을 두고 자동 반복을 멈춘다. 별도 session 상태도 해시로 중복 억제해 다음 실행에 전송할 수 있다.

## 검증·한도

- 압축 4MiB, 해제 16MiB, header 64KiB, 레코드 256KiB, 총 레코드 65,537개 이하. gzip CRC/trailing data/중복 member, version/profile·길이·정수·사전 참조·기본 필드 수·Entity 유한 수·snapshot/footer/seq를 검사한다.
- 조각 분석 사건은 최대 16,384개·JSON payload 합계 8MiB다. 모든 게임 enum/빌드별 콘텐츠 허용 목록이나 실제 플레이 진위까지 증명하는 검증기는 아니다.
- API body/header와 파일 경로/UUID/index, header와 session/build의 연결, session 안 순번 범위 중복을 검사한다. 역순 업로드는 허용하되 연속성은 별도 평가한다.
- 기본 동시 수집 4개, player UTC 일별 raw 64MiB, 전체 raw+분석 추정량 2GiB, 등록 session 최대 100,000개다. IP별·분당 요청 횟수 제한은 없으며 같은 터널 IP를 공유해도 횟수로 차단하지 않는다. 동시 처리 초과는 `429 busy`, 용량 초과는 `429 storage_quota`로 구분한다. 보관/바이트 설정은 backend README를 따른다.
- JSON/파일/해시 검증을 통과해도 클라이언트 제공 데이터다. 보상·안티치트의 권위 있는 판단 근거로 쓰지 않는다.

용량 상한은 실제 디스크 block/WAL/인덱스/백업 전체의 강제 quota가 아니다. 디스크 여유·볼륨 모니터링을 따로 둔다. Cloudflare 등 외부 proxy의 요청 제한, HTTPS와 정확한 CORS origin, 관리자 토큰 설정도 backend 안내에 따른다.

## 완전성·종료·보관

`coverage_complete`는 session 순번 1부터 마지막 선언 seq까지의 연속성, 각 segment의 0부터 연속 index와 이전 SHA 체인을 검사한다. 클라이언트가 기록을 시작하기 전 행동이나 관찰 범위 밖 게임 상태까지 기록됐다는 뜻이 아니다. 로컬 기록 중간에 서버 전송을 켰으면 초기 seq가 없어서 부분 수집으로 보일 수 있다.

`state`는 종료 미관측 `unknown_end`, 종료 관측+누락 `partial`, 종료 관측+연속성+truncated 없음 `complete`, 유실/중단 선언 `truncated`다. 종료 미관측이라도 coverage는 따로 확인한다. 이 상태를 런의 사망/클리어/포기로 바꾸지 않는다. Web 탭 종료와 강제 종료에서는 마지막 조각/종료 상태가 없을 수 있다.

기본 보관은 클라이언트 session 시작 UTC+30일이며 재전송/조회/수신 시각으로 연장하지 않는다. 최초 등록 시 시작 시각은 서버 현재보다 7일 이내 과거·10분 이내 미래만 허용한다. 기기 시계가 크게 틀리면 원본은 로컬에 남기고 등록을 거부한다. 시작 UTC는 신뢰할 수 있는 신원/보안 증거가 아니다.

서버 시작과 매시간 만료 session/관계/원본 및 고아 파일을 정리한다. 서버가 멈춰 있는 동안의 물리 삭제는 보장하지 않는다. 관리자 상세/원본/사건 조회는 만료를 검사한다. 관리자 목록은 정리 사이에 만료 메타데이터가 보일 수 있다. 사용자 자체 삭제 인증이나 삭제 이후 재등록 차단은 구현하지 않는다. 관리자 삭제 전 클라이언트 업로드도 꺼야 미전송 큐가 다시 들어오지 않는다. 팀이 별도 보관한 분석 사본/다운로드/백업은 별도 파기 대상이다.

## 숙련도 분석에 쓰는 방법

`player_id → session 시작순 → run_state의 run ID/챕터/결과 → 주요 사건/원본`으로 묶는다. 동일 run이 챕터/segment를 지나도 run ID로 연결하고, 빌드·콘텐츠·장비/성장 조건을 함께 사용한다. 서로 다른 사람/난이도/빌드를 그대로 섞지 않는다.

첫 단계에서는 시도 순서별 도달 챕터·처치/시간, 명령의 Started/Rejected 결과, 피격 횟수와 HP/피해, 무기/성장 상태, 메뉴 체류·경로 재방문을 사후 계산할 수 있다. 입력만으로 공격 성공을 세거나 snapshot을 실제 선택 횟수로 세지 않는다. 반사/다중 타격/취소는 원본 execution/command 연결로 해석한다. 플레이어가 가한 Hit와 받은 Hit를 source/target으로 구분한다.

```sql
-- 같은 player의 실행 순서. session이 run 한 번이라는 뜻은 아니다.
SELECT player_id, id AS session_id, build_id, started_at,
       ROW_NUMBER() OVER (PARTITION BY player_id ORDER BY started_at, id) AS session_order
FROM sessions ORDER BY player_id, session_order;

-- snapshot을 제외한 실제 UI 활성화/플레이어 명령의 기초 집계.
SELECT s.player_id, e.session_id, e.kind, COUNT(*) AS events
FROM events e JOIN sessions s ON s.id=e.session_id
WHERE e.is_snapshot=0 AND e.kind IN ('ui_activate','player_command')
GROUP BY s.player_id,e.session_id,e.kind;
```

현재는 이 분석의 재료와 조회 API를 제공한다. 적 패턴별 교전 시간·정확한 회피/패링 기회·구역별 기준 시간·숙련도 점수/그래프를 자동 생성하는 분석기는 구현하지 않았다. 원본 관찰 가능 범위/기록 누락을 표시하고, 장비 강화 효과와 사용자 학습 효과를 분리한 지표는 별도로 정의한다.

## 검증 경계

Go 단위/API 통합 시험, 실제 C# 합성 파일의 디코딩, Unity ID/로컬 파일/commit 응답 및 폐기 가능한 Go 서버 대상 업로드 검사를 제공한다. 실행 방법은 backend README와 [테스트 안내](../testing/testing.md)를 따른다. 제공한 테스트와 실제 수행/통과 여부는 구별한다.

실제 Web Player의 IndexedDB 저장·새로고침/탭 종료, 제품 씬 전체 장시간 기록·실배포 네트워크, Docker 데몬 기반 이미지/volume 재시작 수락은 각 환경에서 별도로 수행한다. 외부 서버 운영/자동 숙련도 분석 완료로 해석하지 않는다.
