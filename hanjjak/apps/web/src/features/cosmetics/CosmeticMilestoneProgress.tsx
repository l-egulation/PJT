import type { Milestone } from "./api";
import selectorBoxIcon from "./assets-cozy-pixel/cosmetic-selector-box-v1.png";

type Props = {
  milestone: Milestone;
  pending?: boolean;
  claimed?: boolean;
  onClaim?: () => void;
  onOpenSelector?: () => void;
};

export function CosmeticMilestoneProgress({ milestone, pending = false, claimed = false, onClaim, onOpenSelector }: Props) {
  const ready = milestone.claimableBoxCount > 0;
  const progress = ready ? 200 : Math.max(0, Math.min(200, 200 - milestone.drawsUntilNextBox));
  const hasBox = milestone.ownedBoxQuantity > 0;
  // 상자가 모이기 전에는 누를 것이 없다. 뽑기는 위의 뽑기 버튼으로만 한다.
  const action = ready ? onClaim : hasBox ? onOpenSelector : undefined;
  const actionLabel = ready ? "선택 상자 받고 치장 선택하기" : hasBox ? "선택 상자 열고 치장 선택하기" : `선택 상자까지 ${milestone.drawsUntilNextBox}회 남음`;

  return <div className={`cosmetic-milestone-progress${ready ? " is-ready" : ""}${claimed ? " is-claimed" : ""}`}>
    <div className="cosmetic-milestone-track">
      <div><strong>선택 상자</strong><span>{progress} / 200</span></div>
      <progress max={200} value={progress} aria-label={`선택 상자 진행도 ${progress} / 200`} />
    </div>
    <button type="button" disabled={pending || !action} aria-label={actionLabel} onClick={action}>
      <img src={selectorBoxIcon} alt="" aria-hidden="true" />
      <span>{ready ? "받기" : hasBox ? `${milestone.ownedBoxQuantity}개` : `${milestone.drawsUntilNextBox}회`}</span>
    </button>
    {claimed && <strong className="cosmetic-milestone-received" role="status">선택 상자 획득!</strong>}
  </div>;
}
