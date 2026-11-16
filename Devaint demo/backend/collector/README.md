# Deviant 플레이 데이터 수집 서버

Go 표준 HTTP API, SQLite, 압축 리플레이 파일로 구성한 단일 인스턴스 서버다. 게임에 로그인/계정 시스템을 추가하지 않고 로컬에 저장한 무작위 `player_id`로 여러 실행을 연결한다. ID는 인증 비밀이 아니다. 게임에 관리자 토큰을 배포하지 않는다.

수집 계약은 [서버 명세](../../docs/development/telemetry/replay-server-storage.md), 클라이언트 연결/계획은 [player_id 수집](../../docs/development/telemetry/player-data-collection.md)을 따른다. 대시보드·숙련도 자동 점수·계정 간 동일인 판별은 포함하지 않는다.

## Docker 실행

Go 모듈의 최소 버전은 `go.mod`, 이미지 빌드 버전은 Dockerfile에 고정한다. Docker Engine과 Compose가 필요하다.

```sh
cd backend/collector
cp config.example.env .env
# .env의 ADMIN_TOKEN을 운영용 비밀 값(24자 이상)으로 설정한다.
# Web 배포 주소의 origin을 CORS_ORIGINS에 정확히 설정한다.
docker compose --env-file .env config --quiet
docker compose --env-file .env up -d --build
docker compose ps
curl --fail http://127.0.0.1:8080/healthz
```

`.env`는 Git 제외다. 운영 토큰을 게임 설정/리플레이/URL에 넣지 않는다. 단독 실행은 `ADMIN_TOKEN_FILE`도 지원하며 두 방식을 동시에 설정하면 시작을 거부한다. 기본 Compose는 `.env`의 토큰을 사용한다.

기본 포트는 호스트 loopback에만 노출한다. 외부 배포는 아래 Cloudflare Tunnel 프로필 또는 별도 HTTPS reverse proxy를 사용한다. `/v1/` 요청 본문은 4MiB까지 허용하고 gzip 파일 바이트를 그대로 전달한다. 요청 본문을 자동 해제/재압축하면 SHA가 달라진다. 서버 자체 TLS 인증서 발급·도메인·외부 배포는 이 구성에 포함하지 않는다.

| 설정 | 기본/의미 |
|---|---|
| `ADMIN_TOKEN` / `ADMIN_TOKEN_FILE` | 필수. 관리자 조회·다운로드·삭제 전용 |
| `DATA_DIR` | 직접 실행은 `./data`, 이미지에서는 `/data` |
| `HTTP_ADDR` | `:8080`. 이미지 healthcheck는 8080 기준 |
| `CORS_ORIGINS` | 쉼표로 나눈 Web origin 허용 목록. 경로/마지막 `/` 제외. 빈 값이면 브라우저 Origin 요청 거부 |
| `BIND_ADDRESS`, `PORT` | Compose의 호스트 주소/포트, `127.0.0.1`, `8080` |
| `MAX_STORAGE_BYTES` | 2GiB. raw + 분석 payload/행 오버헤드 추정량의 수집 상한 |
| `PLAYER_DAILY_BYTES` | 플레이어별 UTC 날짜 기준 raw 수신 64MiB |
| `RETENTION_DAYS` | 세션 시작 UTC 기준 30일, 1~365일 |

수집기에는 IP별·분당 요청 횟수 제한이 없다. 같은 Cloudflare Tunnel이나 reverse proxy를 거쳐도 요청 수를 합산해 차단하지 않으며 방문자 IP 헤더를 신뢰할 필요도 없다. 기존 `.env`의 `REQUESTS_PER_MINUTE`는 더 이상 사용하지 않으므로 삭제할 수 있다. 동시 처리 4개 제한에 걸리면 `429 busy`와 `Retry-After: 5`, 저장 용량 한도에 걸리면 `429 storage_quota`와 `Retry-After: 3600`을 반환한다. CORS는 브라우저 정책이며 네이티브/조작 클라이언트 인증을 대신하지 않는다.

`collector-data` named volume에 SQLite/WAL과 `chunks/{session}/{segment}/{index}.replay.gz`를 저장한다. Compose 재시작/down 뒤에도 유지된다. `down -v`는 데이터를 삭제하므로 보관이 필요하면 사용하지 않는다. 컨테이너는 non-root/read-only root filesystem으로 실행한다.

상한은 논리 수집량 제한이며 실제 디스크 사용량의 강제 제한이 아니다. SQLite 인덱스/WAL/free pages·고아 파일·백업에는 여유 공간이 필요하다. DB에서 삭제한 페이지가 즉시 OS에 반환되는 것도 아니다. 볼륨 용량을 따로 관측한다. SQLite 쓰기와 파일 정합성을 단일 프로세스가 조정하므로 replicas를 늘리거나 같은 볼륨에 여러 서버를 연결하지 않는다.

## Cloudflare Tunnel로 외부 노출

선택적인 `tunnel` Compose 프로필은 고정 버전/digest의 공식 `cloudflare/cloudflared` 컨테이너를 추가한다. 기본 `docker compose up -d`는 수집 서버만 실행하므로 기존 로컬 실행에는 터널 토큰이 필요 없다. cloudflared는 수집 서버의 healthcheck가 통과한 뒤 시작하며 같은 Compose 네트워크의 `collector:8080`으로 접속한다. host 네트워크나 Docker 소켓을 마운트하지 않는다.

Cloudflare 계정에 등록된 도메인과 remotely-managed Tunnel을 준비한다. [공식 설정 안내](https://developers.cloudflare.com/tunnel/get-started/)에 따라 Dashboard에서 Tunnel을 생성하고 Docker 커넥터용 **Tunnel token**을 발급한다. 일반 Cloudflare API 토큰·계정 키·이 서버의 ADMIN_TOKEN과는 별개다. 발급한 값을 Git 제외 `.env`의 `CLOUDFLARE_TUNNEL_TOKEN`에 저장한다. 토큰을 게임 JSON이나 Web 정적 파일에 넣지 않는다.

현재 데모 수집 서버의 공개 주소는 `https://deviant-log.verte.kr`, 게임 페이지의 배포 주소는 `https://deviant.verte.kr`이다. 같은 `.env`의 `CORS_ORIGINS`에는 게임 Web 페이지의 origin을 설정한다. 별도 Pages 미리보기 주소에서도 업로드하려면 해당 origin을 쉼표로 추가한다.

```dotenv
CORS_ORIGINS=https://deviant.verte.kr
```

Cloudflare Dashboard의 해당 Tunnel에 **Published application** route를 추가한다.

- 현재 데모 공개 hostname: `deviant-log.verte.kr`
- Service type: **HTTP**
- Service URL: **`http://collector:8080`**

이 주소는 cloudflared 컨테이너가 접속하는 내부 서비스다. 컨테이너 자신의 localhost나 호스트 포트 설정(`PORT`)을 넣지 않는다. public hostname은 사용자의 실제 도메인으로 지정하며 DNS/route가 자동으로 만들어졌는지 확인한다. TLS는 공개 Cloudflare endpoint에서 처리하고 Docker 네트워크 내부 수집 API는 HTTP로 사용한다.

```sh
cd backend/collector
docker compose --env-file .env --profile tunnel config --quiet
docker compose --env-file .env --profile tunnel up -d --build
docker compose ps
docker compose logs --tail=50 cloudflared
curl --fail https://deviant-log.verte.kr/healthz
```

현재 데모 공개 hostname은 `deviant-log.verte.kr`이다. 다른 환경에서는 해당 hostname과 게임 빌드 설정을 함께 변경한다. cloudflared 로그의 연결 등록과 Dashboard의 Healthy, 공개 health 응답을 함께 확인한다. 컨테이너가 Up인 것만으로 터널 연결 성공을 판단하지 않는다. profile을 켤 때는 유효한 토큰이 필수이며 빈 토큰으로 외부 터널을 만들지 못한다. 토큰을 바꿨으면 같은 up 명령으로 컨테이너를 재생성한다.

서버의 80/443 포트를 인터넷에 직접 열 필요는 없다. cloudflared가 Cloudflare에 outbound 연결할 수 있어야 하며 제한된 방화벽에서는 [공식 연결 요구 사항](https://developers.cloudflare.com/tunnel/get-started/)의 포트 7844를 확인한다. 로컬 관리용 수집 포트는 기본 `127.0.0.1:8080`으로 유지한다.

게임 빌드의 `ReplayCollector.json`에는 공개 `https://deviant-log.verte.kr`을 넣고 다시 빌드한다. 도메인/토큰을 Compose에 넣는 것만으로 이미 배포한 게임의 서버 주소가 바뀌지는 않는다. 수집 API에 브라우저 대화형 Access 로그인/챌린지를 적용하면 UnityWebRequest 업로드가 막힐 수 있다. 관리자 `/admin/` API는 기존 Bearer 인증을 계속 요구한다.

수집기는 터널 IP를 기준으로 요청 횟수를 제한하지 않는다. Cloudflare 대시보드의 Rate Limiting 규칙은 수집기와 별도 설정이다. `429`가 발생하면 응답 본문의 `busy`·`storage_quota`로 수집기의 동시 처리·용량 제한을 구분한다. 전역 바이트/플레이어 quota는 계속 적용된다. 서버 코드 변경을 적용하려면 같은 `docker compose --env-file .env --profile tunnel up -d --build` 명령으로 수집기를 다시 빌드·시작한다.

터널만 멈추려면 `docker compose --profile tunnel stop cloudflared`, 전체를 멈추려면 `docker compose --profile tunnel stop`을 실행한다. 기존 리플레이를 유지하려면 볼륨을 삭제하지 않는다. 프로필 없이 up을 다시 실행해도 이미 실행 중인 cloudflared가 자동 종료되지는 않는다.

## Unity 빌드 연결

`unity/Assets/Deviant/Replay/Unity/Resources/ReplayCollector.json`:

```json
{"base_url":"https://deviant-log.verte.kr"}
```

현재 기본값은 데모 수집 서버 `https://deviant-log.verte.kr`이다. 빈 문자열로 바꾸면 서버 전송 옵션이 없는 로컬 전용 빌드가 된다. 운영은 HTTPS, 개발은 loopback HTTP만 허용한다. Web 빌드의 페이지 origin을 서버 CORS 목록에 넣는다. 첫 실행부터 기록과 서버 전송을 자동 시작한다. 별도 업로드 동의 버튼은 없다. 제품 화면에 기록 버튼·상태 패널·설정 창을 표시하지 않고 백그라운드에서 처리한다. 기존에 명시적으로 기록을 중단한 설정은 유지한다.

UUID는 자동 서버 전송 시작 시 PlayerPrefs에 생성/유지한다. Web은 같은 origin/브라우저 저장 영역, 바이너리는 같은 OS 사용자/회사·제품 저장 영역에서 재사용한다. 저장 영역 삭제·비공개 브라우징·재설치 환경 변화는 새 플레이어가 되어도 허용한다. 디바이스 지문이나 Web/바이너리 동일인 연결은 하지 않는다.

서버 주소 없이 만든 과거 로컬 조각에는 업로드 binding이 없다. 과거 로컬 기록을 소급 업로드하지 않으며, 과거 전송 대기 파일은 원래 player/session/endpoint를 유지한다. 기록 중단/삭제는 전송을 중단하고 로컬 큐를 지운다. 이미 전송된 서버 데이터는 관리자 삭제 API를 사용한다.

## API 사용

등록 JSON의 `player_id`, `session_id`는 UUID N 형식이다. 서버는 API 경로/등록의 일반 UUID 표기도 정규화하지만 raw header는 기존 코덱의 소문자 N 형식을 요구한다.

```json
{
  "player_id":"11111111111141118111111111111111",
  "session_id":"22222222222242228222222222222222",
  "build_id":"demo-build-id",
  "platform":"web",
  "started_at":"2026-11-09T00:00:00Z",
  "consent_version":1
}
```

이 예시 ID/시각은 형식 설명용이다. 실제 요청은 현재 실행의 UUID/UTC를 사용한다. `POST /v1/sessions`는 위 JSON, `PUT /v1/sessions/{session}/chunks/{segment}/{index}`는 raw gzip 바이트와 `X-Player-ID`, `X-Replay-SHA256`를 받는다. `Content-Type: application/octet-stream`이며 `Content-Encoding`은 설정하지 않는다.

성공 응답은 `{"state":"committed","sha256":"…","duplicate":false}`다. 같은 ID/해시 재전송은 중복 성공, 같은 조각의 다른 바이트는 409다. 파일과 DB 저장 완료 뒤 응답한다. 클라이언트는 정확히 일치하는 committed SHA를 확인한 뒤 큐 파일을 지운다.

4xx 영구 오류가 난 파일은 `.upload-error`와 함께 로컬에 남긴다. 원인을 해결한 뒤 개발자가 해당 표식을 제거하면 같은 파일로 재시도할 수 있다. 일반 사용자의 새 동의로 과거 로컬 전용 파일을 다시 binding하지 않는다.

`PUT /v1/sessions/{session}/status`는 같은 player 헤더와 `{"last_ms":1000,"last_seq":42,"ended":true,"truncated":false}`를 받는다. 상태는 단조 병합하며 뒤늦은 업데이트가 종료/유실을 취소하지 않는다. 종료 시 HTTP 성공을 보장하지 않으며, 다음 실행에서 남은 큐/상태를 재전송한다.

관리자 API에는 `Authorization: Bearer <ADMIN_TOKEN>`이 필요하다. 토큰을 직접 명령에 붙여 shell history에 남기지 않는다. 이미 환경에 주입된 경우 다음처럼 조회한다.

```sh
curl --fail -H "Authorization: Bearer $ADMIN_TOKEN" http://127.0.0.1:8080/admin/players
```

- `GET /admin/players?limit=200`: 최근 수신 플레이어 요약.
- `GET /admin/players/{player}/sessions?limit=200`: 시작 시각순 세션과 완전성.
- `GET /admin/sessions/{session}`: 전역 seq 순의 조각·해시·씬/run 연결과 종료/완전성. 조각에 `recorded_from/to` UTC를 제공하고, UUIDv7 구간에는 `segment_created_at` UTC도 제공한다. 기존 UUIDv4 파일은 그대로 지원한다.
- `GET /admin/sessions/{session}/events?after_seq=0&limit=1000`: 주요 사건. `next_after_seq`를 다음 cursor로 사용. snapshot은 기본 제외, `snapshots=1`로 포함.
- `GET /admin/sessions/{session}/chunks/{segment}/{index}`: 원본 다운로드.
- `DELETE /admin/players/{player}`: DB 관계와 원본 제거. 자체 백업은 별도 삭제 정책을 적용한다.

플레이어/세션 목록은 최근 최대 200개이며 목록 cursor는 아직 없다. 장기 전체 분석은 서버를 멈추고 SQLite+chunks를 일관되게 복사하거나 SQLite backup 절차로 별도 분석 사본을 만든다. 실행 중 DB 파일만 복사하면 WAL의 변경을 놓칠 수 있다. 라이브 DB를 외부에 노출하지 않는다.

## 테스트

```sh
go test -race ./...
go vet ./...
# 선택: 기존 C# 기록기로 만든 합성 조각과 상호운용 확인
REPLAY_FIXTURE_DIR=/absolute/path/to/synthetic/recordings go test -run TestOptionalProductionClientFixtures -v
```

Go 테스트는 임시 SQLite와 합성 데이터로 형식/CRC/해시/용량·중복/충돌·동시 요청·재시작·만료·삭제·CORS·관리자 권한을 검사한다. 실제 플레이 기록을 fixture로 커밋하지 않는다.

Unity `Deviant.Replay.Tests`에는 ID 유지/전송 binding/철회/확정 응답 검사가 있다. 폐기 가능한 서버를 loopback에 실행하고 `DEVIANT_COLLECTOR_TEST_URL=http://127.0.0.1:18087`을 Unity 테스트 프로세스에 설정하면 실제 클라이언트 업로드·세션 상태 동기화 통합 검사도 실행한다. 이 검사는 무작위 합성 플레이어 1개를 서버에 남기므로 운영 서버를 대상으로 실행하지 않는다. Unity는 6000.6.5f1을 사용한다.

Web의 IndexedDB 영속성·탭 종료·백그라운드 전송과 실제 Windows/Linux Player 네트워크 수락 검사는 별도로 수행한다. Web에서 종료 콜백/마지막 업로드 성공을 보장하지 않는다.
