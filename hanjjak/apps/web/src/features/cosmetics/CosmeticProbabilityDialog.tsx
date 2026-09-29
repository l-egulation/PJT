import type { BannerDetail, PoolItem } from "./api";
import { cosmeticDisplayName } from "./cosmetic-art";

const gradeLabels: Record<string, string> = { NORMAL: "노말", RARE: "희귀", EPIC: "영웅", LEGENDARY: "전설" };
const slotLabels: Record<string, string> = { HEAD: "머리", TOP: "상의", BOTTOM: "무기", GLOVES: "장갑", SHOES: "신발", CAPE: "망토" };

function gradeName(grade: string) {
  return gradeLabels[grade] ?? grade;
}

type Props = { detail?: BannerDetail; error?: boolean; onRetry?: () => void; onClose: () => void };

export function CosmeticProbabilityDialog({ detail, error = false, onRetry, onClose }: Props) {
  return <div className="cosmetic-dialog-backdrop" role="presentation">
    <section className="cosmetic-dialog cosmetic-probability-dialog" role="dialog" aria-modal="true" aria-labelledby="cosmetic-probability-title">
      <p className="cosmetic-eyebrow">등장 확률 안내</p>
      <h3 id="cosmetic-probability-title">치장 뽑기 확률</h3>
      {error && !detail ? <div className="cosmetic-error" role="alert"><strong>확률 정보를 불러오지 못했습니다.</strong><p>잠시 후 다시 시도해 주세요.</p><button type="button" onClick={onRetry}>다시 시도</button></div> : detail && <>
        <p>서버가 제공한 확률과 풀 정보를 그대로 표시합니다.</p>
        <section className="cosmetic-probability-panel" aria-label="등급별 서버 확률">
          <h4>등급별 확률</h4>
          {Object.entries(detail.gradeProbabilityMillionths).map(([grade, value]) => <p key={grade}><span>{gradeName(grade)}</span><strong>{value.toLocaleString()} / 1,000,000</strong><small>{(value / 10_000).toFixed(2)}%</small></p>)}
        </section>
        <section className="cosmetic-pool-panel" aria-label="뽑기 풀">
          <h4>뽑기 풀</h4>
          <ul>
            {detail.pool.map((item: PoolItem) => <li key={item.cosmeticId}>
              <div><strong>{cosmeticDisplayName(item)}</strong><small>{gradeName(item.grade)} · {slotLabels[item.slot] ?? item.slot}</small></div>
              <span>{item.probabilityNumerator.toLocaleString()} / {item.probabilityDenominator.toLocaleString()}</span>
              <strong>{item.probabilityDisplay}</strong>
            </li>)}
          </ul>
        </section>
      </>}
      <div className="cosmetic-dialog-actions"><button type="button" className="is-primary" onClick={onClose}>닫기</button></div>
    </section>
  </div>;
}
