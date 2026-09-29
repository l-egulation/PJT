# 성장 SSOT

---
doc_kind: ssot
owner_domain: progression
authority_level: applied
---

## 책임

경험치·레벨·쌀의 획득과 소비, 기본 성장과 투자형 성장의 구분을 소유한다. 장비 수치와 스킬 효과는 각 도메인을 참조한다.

## 진행 조건

백그라운드 탭에서는 렌더링 갱신과 브라우저 타이머에 의존하지 않고 서버 기준 경과시간 또는 서버 확정 결과로 메인 전투·성장·보상을 계속 진행한다. 오프라인 보상은 서버 UTC의 마지막 heartbeat·로그아웃 시각과 첫 재로그인 시각 차이를 사용한다. 1분 미만이면 지급하지 않고 1분 단위로 내림해 최대 480분까지 인정하며, 복귀 시점의 현재 스테이지를 1분 자동전투했을 때 얻는 쌀·경험치·주력 재료 기대량의 50%를 인정 분 수만큼 지급한다. 오프라인 작업은 스테이지 해금·랭킹·거래 자산을 만들지 않는다.

## 레벨

- 최대 레벨은 500이다.
- 레벨당 공격력 2, 최대 HP 20이 증가한다.
- 레벨은 방어 관통을 직접 올리지 않는다.
- 500레벨에서는 추가 경험치를 저장하지 않는다.

현재 레벨 $L$에서 다음 레벨까지 필요한 경험치는 다음과 같다.

$$
XP_{\mathrm{next}}(L)=1{,}000L.
$$

레벨 $L$ 도달 누적 경험치는 다음과 같다.

$$
XP_{\mathrm{total}}(L)=500L(L-1).
$$

한 번의 경험치 지급으로 여러 레벨 조건을 만족하면 500레벨 또는 경험치 부족까지 순서대로 레벨업한다. 레벨업으로 최대 HP가 20 증가하면 현재 HP도 20 증가해 손실 HP의 절대량을 유지하며 완전 회복은 일어나지 않는다.

## 차기 리밸런싱: 1~79레벨 목표시간

`progression-rebalance-v1`은 필요 경험치 `XP_next(L)=1,000L`, 누적 경험치와 레벨당 능력치 증가를 유지하고 1~79레벨의 목표 누적시간만 현재 버전의 1/3로 단축한다. 80레벨 이후 목표시간은 5~10장 설계에서 연속 곡선을 확정하기 전까지 이 버전의 출시 범위가 아니다. 실행 상태는 [A-05](../../wiki/06-delivery/tasks/A-release-scope/a-05-progression-rebalance.md)를 따른다.

$$
T_{\mathrm{target,next}}(L)
=2(L-1)+\frac{11}{90}(L-1)^2\quad\mathrm{min},\qquad 1\le L\le79.
$$

스테이지 글로벌 인덱스와 권장 레벨 `L_ref(I)=2I-1`, 기준 처치속도 50마리/분, 보스 경험치가 일반 몬스터의 5배인 관계는 유지한다. `progression-rebalance-v1`은 다음 식으로 1~79레벨 일반 몬스터 경험치를 역산하고, 전투 세션 시작 시 고정한 밸런스 버전을 완료까지 사용한다.

$$
\begin{aligned}
t_{\mathrm{level,next}}(L)
&=T_{\mathrm{target,next}}(L+1)-T_{\mathrm{target,next}}(L)
=2+\frac{11}{90}(2L-1),\\
XP_{\mathrm{normal,next}}(I)
&=\operatorname{round}\left(\frac{1{,}000L_{\mathrm{ref}}(I)}{50t_{\mathrm{level,next}}(L_{\mathrm{ref}}(I))}\right),\\
XP_{\mathrm{boss,next}}(I)&=5XP_{\mathrm{normal,next}}(I).
\end{aligned}
$$

| 도달점 | 기준 레벨 | P50 누적 유효 전투시간 | P90 상한 |
| --- | ---: | ---: | ---: |
| 1-10 | 19 | 75.6분 | 94.5분 |
| 2-10 | 39 | 4.21시간 | 5.26시간 |
| 3-10 | 59 | 8.79시간 | 10.98시간 |
| 4-10 | 79 | 14.99시간 | 18.74시간 |

P90 상한은 정상적인 거래소 매물과 정상 플레이를 전제로 P50의 1.25배다. 거래소 미사용과 시장 유동성 부족은 별도 관찰군으로 기록한다. 실제 적·공급·소비를 함께 시뮬레이션하기 전에는 시간표를 구현 완료 증거로 보지 않는다.

## 현재 버전 목표 시간과 몬스터 경험치

유효 전투시간 기준 레벨 $L$ 목표 누적시간은 다음과 같다.

$$
T_{\mathrm{target}}(L)=6(L-1)+\frac{11}{30}(L-1)^2\quad\mathrm{min}.
$$

스테이지 글로벌 인덱스 $I$의 권장 레벨은 $L_{\mathrm{ref}}(I)=2I-1$이다. 기준 처치속도 50마리/분에서 일반 몬스터 경험치는 목표 레벨업 시간으로 역산하고 보스는 일반의 5배를 지급한다.

$$
\begin{aligned}
t_{\mathrm{level}}(L)
&=T_{\mathrm{target}}(L+1)-T_{\mathrm{target}}(L)
=6+\frac{11}{30}(2L-1),\\
XP_{\mathrm{normal}}(I)
&=\operatorname{round}\left(\frac{1{,}000L_{\mathrm{ref}}(I)}{50t_{\mathrm{level}}(L_{\mathrm{ref}}(I))}\right),\\
XP_{\mathrm{boss}}(I)&=5XP_{\mathrm{normal}}(I).
\end{aligned}
$$

경험치는 처치 확정 시 즉시 지급하고 사망해도 유지한다. 피해량과 실패한 보스 도전에는 경험치를 지급하지 않는다.

## 쌀 획득

- 쌀은 처치 확정 시 즉시 지급하고 사망해도 유지한다.
- MVP working 기준에서 스테이지 글로벌 인덱스 $I$의 일반 몬스터 쌀은 $Rice_{\mathrm{normal}}(I)=I$다.
- MVP working 기준에서 보스 쌀은 일반 몬스터의 10배인 $Rice_{\mathrm{boss}}(I)=10I$다.
- 이 쌀 수치는 제작·스킬 비용표와 경제 지표 검증 전 임시 공급 기준이며, 밸런스 확정 상태가 아니다.

## 성장 역할

- 레벨은 반복 플레이의 기본 성장을 보장한다.
- 장비와 스킬은 상위 스테이지의 성장벽을 효율적으로 돌파하는 필수 기준 빌드를 구성한다.
- 쌀 소비처와 시장 규칙은 [거래소 SSOT](../economy/ssot.md)를 따른다.

관련 작업: [B 작업](../../wiki/06-delivery/tasks/B-account-storage/_index.md), [경제 원문](../../wiki/99-archive/notion/pages/design/economy-design.md)

