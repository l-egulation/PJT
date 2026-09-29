import type { CSSProperties } from "react";
import type { Banner, Catalog, DrawResponse } from "./api";
import { CosmeticMilestoneProgress } from "./CosmeticMilestoneProgress";
import { cosmeticArtUrl, cosmeticDisplayName } from "./cosmetic-art";
import newRibbon from "./assets-cozy-pixel/cosmetic-new-ribbon-v1.png";

const gradeLabels: Record<string, string> = { NORMAL: "노말", RARE: "희귀", EPIC: "영웅", LEGENDARY: "전설" };
type Props = {
  draw: DrawResponse;
  catalog: Catalog;
  banner?: Banner;
  pending?: boolean;
  milestoneClaimed?: boolean;
  onRedraw?: (count: 1 | 10) => void;
  onContinue?: () => void;
  continueLabel?: string;
  onClaimMilestone?: () => void;
  onOpenSelector?: () => void;
};

export function CosmeticDrawReveal({ draw, catalog, banner, pending = false, milestoneClaimed = false, onRedraw, onContinue, continueLabel = "돌아가기", onClaimMilestone, onOpenSelector }: Props) {
  const byId = new Map(catalog.cosmetics.map((item) => [item.cosmeticId, item]));
  const drawCount = draw.results.length === 10 ? 10 : 1;
  const redrawCost = drawCount === 10 ? banner?.tenDraw : banner?.oneDraw;
  return <section className={`cosmetic-result-panel cosmetic-reveal-stage${draw.results.length === 1 ? " is-single" : ""}`} aria-live="polite" aria-label="치장 뽑기 결과">
    <span className="cosmetic-reveal-aura" aria-hidden="true" />
    <ol className="cosmetic-result-grid" aria-label={`획득한 치장 ${draw.results.length}개`}>
      {draw.results.map((result, index) => {
        const item = byId.get(result.cosmeticId);
        const itemName = cosmeticDisplayName(item ?? { cosmeticId: result.cosmeticId });
        const artUrl = cosmeticArtUrl(item);
        return <li className={`cosmetic-result-card grade-${result.grade.toLowerCase()}`} style={{ "--reveal-index": index } as CSSProperties} key={`${result.cosmeticId}-${index}`}>
          <strong className="cosmetic-result-name">{itemName}</strong>
          <div className="cosmetic-art-placeholder">
            {artUrl ? <img src={artUrl} alt="" /> : "이미지 준비 중"}
          </div>
          {/* 아이콘을 담는 영역은 넘침을 잘라내므로, NEW 스티커는 카드 직속으로 둔다. */}
          {result.isNew && <mark className="is-new"><img src={newRibbon} alt="" aria-hidden="true" /><span className="sr-only">NEW</span></mark>}
          <span className="cosmetic-result-grade">{gradeLabels[result.grade] ?? result.grade}</span>
        </li>;
      })}
    </ol>
    {banner && <CosmeticMilestoneProgress milestone={banner.milestone} pending={pending} claimed={milestoneClaimed} onClaim={onClaimMilestone} onOpenSelector={onOpenSelector} />}
    {(onRedraw || onContinue) && <footer className="cosmetic-reveal-footer">
      {onRedraw && <button type="button" className="is-primary" disabled={pending || !redrawCost?.executable} onClick={() => onRedraw(drawCount)}><strong>다시 {drawCount}회 뽑기</strong>{banner && <small>뽑기권 {banner.ticketBalance}장 · 쌀 {banner.riceBalance.toLocaleString()}</small>}</button>}
      {onContinue && <button type="button" onClick={onContinue}>{continueLabel}</button>}
    </footer>}
  </section>;
}
