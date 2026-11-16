# 2스테이지 보스 모션·크기 기준

승인한 프레임과 사용자 크기 설정을 모은 검토용 리소스다. Unity 적용은 별도다.

- `preview/index.html`: 기본 자세와 모션 16종의 나란히 보기·크기 조절.
- `preview/assets/`: 승인된 보정 프레임의 바이트 보존본.
- `runtime/`: 사용자 배율을 nearest-neighbor로 반영한 별도 출력. `runtime/motions.json`의 파일 경로는 이 에셋 그룹 루트를 기준으로 한다.
- `source/stage2-boss-motion-sizes.json`: 사용자가 내보낸 크기 설정.
- `source/aether/sources.json`: 새 쓰러짐·일어남·시전·붙잡힘 시트 원본과 승인 프레임의 대응. 기존 착지·로즈 쓰러짐 원본은 `../rose-blue-transition-motions-20261105/source/`에 보존된다.

리소스 루트에서 `python -m http.server 8155 --bind 127.0.0.1`을 실행하고 `http://127.0.0.1:8155/monsters/stage-02/boss-motion-size-review-20261106/preview/`로 연다. 로컬 file:// 대신 HTTP를 사용한다.
