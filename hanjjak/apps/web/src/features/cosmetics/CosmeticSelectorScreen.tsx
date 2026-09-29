import { useState, type CSSProperties } from "react";
import type { Banner, Catalog } from "./api";
import paperCloseButton from "../../shared/assets/cozy-paper-v2/close-button.png";
import { cosmeticArtUrl, cosmeticDisplayName, cosmeticSetArtUrl, cosmeticSetName } from "./cosmetic-art";
import characterRest from "../character/assets-cozy-pixel/character-rest-192.png";

const gradeLabels: Record<string, string> = { NORMAL: "노말", RARE: "희귀", EPIC: "영웅", LEGENDARY: "전설" };
const slotLabels: Record<string, string> = { HEAD: "머리", TOP: "상의", BOTTOM: "무기", GLOVES: "장갑", SHOES: "신발", CAPE: "망토", WEAPON: "무기" };
const slotOrder = ["HEAD", "TOP", "BOTTOM", "GLOVES", "SHOES", "CAPE", "WEAPON"];
const setName = (id: string, displayName: string | null) => cosmeticSetName({ setId: id, displayName }) ?? `전설 세트 #${id.slice(-2)}`;
const setColors = [
  ["#6f9e58", "#d9554d"],
  ["#6878a9", "#d8b657"],
  ["#a45b49", "#e4a640"],
  ["#75a8b7", "#d9eef0"],
];

type Props = {
  banner: Banner;
  catalog: Catalog;
  pending?: boolean;
  onBack: () => void;
  onSelect: (cosmeticId: string) => void;
};

export function CosmeticSelectorScreen({ banner, catalog, pending = false, onBack, onSelect }: Props) {
  const legendaryCosmetics = catalog.cosmetics.filter((item) => item.grade === "LEGENDARY");
  const legendarySets = catalog.sets.filter((set) => set.grade === "LEGENDARY" && legendaryCosmetics.some((item) => item.setId === set.setId));
  const [chosenSetId, setChosenSetId] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState("");
  const selectedSetId = chosenSetId;
  const selectedSet = legendarySets.find((set) => set.setId === selectedSetId) ?? null;
  const choices = legendaryCosmetics
    .filter((item) => item.setId === selectedSetId)
    .sort((left, right) => slotOrder.indexOf(left.slot) - slotOrder.indexOf(right.slot));

  const chooseSet = (setId: string) => {
    setChosenSetId(setId);
    setSelectedId("");
  };

  const returnToSets = () => {
    setChosenSetId(null);
    setSelectedId("");
  };

  return <section className={`cosmetic-screen cosmetic-selector-screen ${selectedSet ? "is-part-list" : "is-set-list"}`} aria-labelledby="cosmetic-selector-title">
    <header className="cosmetic-selector-heading">
      <h2 id="cosmetic-selector-title" className="sr-only">{selectedSet ? "받을 치장 선택" : "전설 세트 선택"}</h2>
      <span className="sr-only">선택 상자 {banner.milestone.ownedBoxQuantity}개 보유</span>
      {/* 글자 ×는 이 화면의 글꼴에 없다. 다른 창과 같은 그림을 쓴다. */}
      <button type="button" className="cosmetic-selector-close" onClick={onBack} aria-label="선택 화면 닫기">
        <img src={paperCloseButton} alt="" aria-hidden="true" />
      </button>
    </header>
    {!selectedSet ? <div className="cosmetic-selector-set-grid" aria-label="선택 가능한 전설 세트">
      {legendarySets.map((set, setIndex) => {
        const [accent, accentLight] = setColors[setIndex % setColors.length];
        const setArtUrl = cosmeticSetArtUrl(set);
        return <button type="button" className="cosmetic-selector-set-card" data-set-id={set.setId} style={{ "--set-accent": accent, "--set-accent-light": accentLight } as CSSProperties} key={set.setId} onClick={() => chooseSet(set.setId)} aria-label={`${setName(set.setId, set.displayName)} 선택`}>
          <span className="cosmetic-selector-set-look" aria-hidden="true">
            <img className={setArtUrl ? "is-supplied-preview" : undefined} src={setArtUrl ?? characterRest} alt="" />
            {!setArtUrl && <><i className="cosmetic-selector-look-hat" /><i className="cosmetic-selector-look-outfit" /></>}
          </span>
          <span className="cosmetic-selector-set-meta" aria-hidden="true">{setName(set.setId, set.displayName)}</span>
        </button>;
      })}
      </div> : <div className="cosmetic-selector-detail">
      <div className="cosmetic-selector-detail-heading">
        <strong>{setName(selectedSet.setId, selectedSet.displayName)}</strong>
      </div>
      <div className="cosmetic-selector-grid" aria-label={`${setName(selectedSet.setId, selectedSet.displayName)} 부위별 치장`}>
        {choices.map((item) => {
          const artUrl = cosmeticArtUrl(item);
          return <button type="button" className={`cosmetic-selector-card grade-${item.grade.toLowerCase()}${selectedId === item.cosmeticId ? " is-selected" : ""}`} key={item.cosmeticId} onClick={() => setSelectedId(item.cosmeticId)} aria-label={`${cosmeticDisplayName(item)} ${slotLabels[item.slot] ?? item.slot} ${gradeLabels[item.grade] ?? item.grade}`} aria-pressed={selectedId === item.cosmeticId}>
            <strong className="cosmetic-result-name">{cosmeticDisplayName(item)}</strong>
            <span className="cosmetic-art-placeholder">{artUrl ? <img src={artUrl} alt="" /> : <span className="cosmetic-art-empty"><b aria-hidden="true">✦</b><small>이미지 준비 중</small></span>}</span>
            <span className="cosmetic-result-grade">{gradeLabels[item.grade] ?? item.grade}</span>
          </button>;
        })}
      </div>
    </div>}
    {selectedSet && <footer className="cosmetic-selector-footer">
      <button type="button" className="is-secondary" onClick={returnToSets}>다시 선택하기</button>
      <button type="button" disabled={pending || !selectedId || banner.milestone.ownedBoxQuantity < 1} onClick={() => onSelect(selectedId)}>확정하기</button>
    </footer>}
  </section>;
}
