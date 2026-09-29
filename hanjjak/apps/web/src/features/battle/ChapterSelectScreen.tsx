import { useMemo, useState } from "react";
import { createPortal } from "react-dom";
import type { StageSummary } from "./api";
import { formatStageId } from "./stageLabel";
import {
  CHAPTERS,
  CHAPTER_SHOP_HEIGHT_PERCENT,
  CHAPTER_SHOP_WIDTH_PERCENT,
  chapterOfStage,
  chapterShopPlacement,
  type ChapterEntry,
} from "./chapterCatalog";
import arcadeBackground from "./assets-chapter-select-v1/arcade-background.png";
import badgeNormal from "./assets-chapter-select-v1/badges/chapter-badge-normal.png";
import badgeSelected from "./assets-chapter-select-v1/badges/chapter-badge-selected.png";
import badgeLocked from "./assets-chapter-select-v1/badges/chapter-badge-locked.png";
import badgeCleared from "./assets-chapter-select-v1/badges/chapter-badge-cleared.png";
import iconCheck from "./assets-chapter-select-v1/icons/icon-check.png";
import iconLock from "./assets-chapter-select-v1/icons/icon-lock.png";
import contentTitlePlaque from "./assets-content-picker-v1/content-title-plaque.png";
import closeButton from "../../shared/assets/cozy-paper-v2/close-button.png";
import contentMoveButton from "./assets-content-picker-v1/move-button.png";
import contentComingSoonButton from "./assets-content-picker-v1/coming-soon-button.png";
import "./ChapterSelectScreen.css";

export type ChapterState = {
  entry: ChapterEntry;
  stages: StageSummary[];
  unlocked: boolean;
  cleared: boolean;
  clearedStages: number;
};

/**
 * 챕터 하나가 열렸는지, 다 깼는지는 그 챕터에 속한 스테이지들이 말해 준다.
 * 설계서가 아직 없는 챕터는 스테이지가 하나도 없으므로 늘 잠겨 있다.
 */
export function chapterStates(stages: StageSummary[]): ChapterState[] {
  return CHAPTERS.map((entry) => {
    const owned = stages.filter((stage) => chapterOfStage(stage.stageId) === entry.chapter);
    const clearedStages = owned.filter((stage) => stage.clearCount > 0).length;
    return {
      entry,
      stages: owned,
      unlocked: owned.some((stage) => stage.unlocked),
      cleared: owned.length > 0 && clearedStages === owned.length,
      clearedStages,
    };
  });
}

function badgeArt(state: ChapterState, selected: boolean): string {
  if (!state.unlocked) return badgeLocked;
  if (selected) return badgeSelected;
  return state.cleared ? badgeCleared : badgeNormal;
}

export function ChapterSelectScreen({ stages, selectedStageId, onSelectStage, onClose }: {
  stages: StageSummary[];
  selectedStageId: string;
  onSelectStage: (stageId: string) => void;
  onClose: () => void;
}) {
  const states = useMemo(() => chapterStates(stages), [stages]);
  const [picked, setPicked] = useState(() => chapterOfStage(selectedStageId) || 1);
  /* 가게를 고르는 화면과 그 안의 스테이지를 고르는 화면은 같은 창의 두 쪽이다. */
  const [stageListOpen, setStageListOpen] = useState(false);
  const current = states.find((state) => state.entry.chapter === picked) ?? states[0];

  /*
   * 전투 화면의 스테이지 묶음에는 transform 이 걸려 있어, 그 안에서 position:fixed
   * 를 쓰면 화면이 아니라 그 묶음 크기에 갇힌다. 몸통에 직접 띄운다.
   */
  return createPortal(<div className="chapter-select-backdrop" onMouseDown={(event) => event.target === event.currentTarget && onClose()}>
    <section className="chapter-select" role="dialog" aria-modal="true" aria-labelledby="chapter-select-title">
      <div className="chapter-select-stage" style={{ backgroundImage: `url(${arcadeBackground})` }}>
        <header className="chapter-select-topbar">
          <h2 id="chapter-select-title" style={{ backgroundImage: `url(${contentTitlePlaque})` }}>챕터 선택</h2>
          <button type="button" className="chapter-select-close" aria-label="챕터 선택 닫기" onClick={onClose}>
            <img src={closeButton} alt="" aria-hidden="true" />
          </button>
        </header>

        <ul className="chapter-select-shops">{states.map((state) => {
          const { chapter, name } = state.entry;
          const selected = chapter === picked;
          const placement = chapterShopPlacement(chapter);
          return <li
            key={chapter}
            className={`chapter-shop${selected ? " is-selected" : ""}${state.unlocked ? "" : " is-locked"}`}
            style={{
              left: `${placement.left}%`,
              top: `${placement.top}%`,
              width: `${CHAPTER_SHOP_WIDTH_PERCENT}%`,
              height: `${CHAPTER_SHOP_HEIGHT_PERCENT}%`,
            }}
          >
            <button
              type="button"
              aria-pressed={selected}
              aria-label={`${chapter}챕터 ${name}${state.unlocked ? "" : " 잠김"}`}
              onClick={() => setPicked(chapter)}
            >
              <img className="chapter-shop-art" src={selected ? state.entry.selectedArt : state.entry.art} alt="" />
            </button>
            <span className="chapter-shop-badge" aria-hidden="true">
              <img src={badgeArt(state, selected)} alt="" />
              <b>{chapter}</b>
            </span>
            {!state.unlocked && <img className="chapter-shop-mark is-lock" src={iconLock} alt="" aria-hidden="true" />}
            {state.cleared && <img className="chapter-shop-mark is-check" src={iconCheck} alt="" aria-hidden="true" />}
          </li>;
        })}</ul>

        <footer className="chapter-select-footer">
          <img className="chapter-select-footer-art" src={current.entry.art} alt="" aria-hidden="true" />
          <p className="chapter-select-footer-name">
            <small>CHAPTER {current.entry.chapter}</small>
            <strong>{current.entry.name}</strong>
          </p>
          <p className="chapter-select-footer-level">
            <small>권장 레벨</small>
            <strong>{current.entry.levelRange ?? "준비 중"}</strong>
          </p>
          <button
            type="button"
            className="chapter-select-enter"
            style={{ backgroundImage: `url(${current.entry.comingSoon ? contentComingSoonButton : contentMoveButton})` }}
            disabled={!current.unlocked}
            onClick={() => setStageListOpen(true)}
          >{current.entry.comingSoon ? "준비 중" : "챕터 이동"}</button>
          <span className="chapter-select-footer-count">{current.entry.chapter} / {CHAPTERS.length}</span>
        </footer>

        {stageListOpen && <div className="chapter-stage-sheet" role="dialog" aria-label={`${current.entry.name} 스테이지 선택`}>
          <header>
            <strong>CHAPTER {current.entry.chapter} · {current.entry.name}</strong>
            <button type="button" aria-label="스테이지 목록 닫기" onClick={() => setStageListOpen(false)}>×</button>
          </header>
          <ul>{current.stages.map((stage) => <li key={stage.stageId}>
            <button
              type="button"
              className={stage.stageId === selectedStageId ? "is-current" : undefined}
              disabled={!stage.unlocked}
              aria-label={`${formatStageId(stage.stageId)} ${stage.unlocked ? `클리어 ${stage.clearCount}회` : "잠김"}`}
              onClick={() => { onSelectStage(stage.stageId); onClose(); }}
            >
              <b>{formatStageId(stage.stageId)}</b>
              <small>{stage.unlocked ? `클리어 ${stage.clearCount}회` : "잠김"}</small>
            </button>
          </li>)}</ul>
        </div>}
      </div>
    </section>
  </div>, document.body);
}
