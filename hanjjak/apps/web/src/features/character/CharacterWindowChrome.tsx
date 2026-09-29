import type { RefObject } from "react";
import type { CharacterStats } from "./api";
import accentRaysCoral from "./assets-cozy-pixel/accent-rays-coral.png";
import closeIcon from "../../shared/assets/cozy-paper-v2/close-button.png";
import chopstickHead from "../../shared/assets/cozy-hud-v1/icons/chopstick-head.png";
import tabStatsDefault from "./assets-stats-v2/tab-stats-default.png";
import tabStatsSelected from "./assets-stats-v2/tab-stats-selected.png";
import tabCosmeticsDefault from "./assets-stats-v2/tab-cosmetics-default.png";
import tabCosmeticsSelected from "./assets-stats-v2/tab-cosmetics-selected.png";
import tabGalleryDefault from "./assets-stats-v2/tab-gallery-default.png";
import tabGallerySelected from "./assets-stats-v2/tab-gallery-selected.png";

export type CharacterTab = "stats" | "cosmetics" | "gallery";

const TABS: Array<[CharacterTab, string]> = [
  ["stats", "능력치"],
  ["cosmetics", "치장"],
  ["gallery", "도감"],
];

const TAB_ART: Record<CharacterTab, { default: string; selected: string }> = {
  stats: { default: tabStatsDefault, selected: tabStatsSelected },
  cosmetics: { default: tabCosmeticsDefault, selected: tabCosmeticsSelected },
  gallery: { default: tabGalleryDefault, selected: tabGallerySelected },
};

type HeaderProps = {
  activeTab: CharacterTab;
  closeButton: RefObject<HTMLButtonElement | null>;
  data?: CharacterStats;
  onClose: () => void;
  onTabChange: (tab: CharacterTab) => void;
};

export function CharacterWindowHeader({ activeTab, closeButton, data, onClose, onTabChange }: HeaderProps) {
  return <header className="character-header">
    <div className="character-heading">
      <img className="character-heading__sprout" src={chopstickHead} alt="" />
      <h2 id="character-title">캐릭터</h2>
      <img className="character-heading__accent" src={accentRaysCoral} alt="" />
    </div>
    <div role="tablist" aria-label="캐릭터 정보" className="character-tabs">
      {TABS.map(([id, label], index) => {
        const locked = Boolean(data && !data.cosmeticsUnlocked && id !== "stats");
        return <button
        key={id}
        role="tab"
        id={`character-tab-${id}`}
        aria-controls={`character-panel-${id}`}
        aria-label={locked ? `${label} (잠김)` : label}
        aria-selected={activeTab === id}
        tabIndex={activeTab === id ? 0 : -1}
        onClick={() => onTabChange(id)}
        onKeyDown={(event) => {
          const next = event.key === "ArrowRight" ? (index + 1) % TABS.length
            : event.key === "ArrowLeft" ? (index + TABS.length - 1) % TABS.length
              : event.key === "Home" ? 0 : event.key === "End" ? TABS.length - 1 : -1;
          if (next < 0) return;
          event.preventDefault();
          const nextTab = TABS[next][0];
          onTabChange(nextTab);
          document.getElementById(`character-tab-${nextTab}`)?.focus();
        }}
      >
        <img src={activeTab === id ? TAB_ART[id].selected : TAB_ART[id].default} alt="" aria-hidden="true" />
        <span>{label}</span>
      </button>;
      })}
    </div>
    <button ref={closeButton} className="character-close paper-close" type="button" onClick={onClose} aria-label="캐릭터 창 닫기"><img src={closeIcon} alt="" aria-hidden="true" /></button>
  </header>;
}

export function CharacterLoadPanel({ kind, onRetry }: { kind: "loading" | "error"; onRetry?: () => void }) {
  return <section className={`character-load-panel character-load-panel--${kind}`} aria-live="polite" role={kind === "error" ? "alert" : "status"}>
    <span className="character-load-panel__stamp" aria-hidden="true">한</span>
    <div>
      <strong>{kind === "loading" ? "캐릭터 기록을 펼치는 중" : "캐릭터 기록을 열지 못했습니다"}</strong>
      <p>{kind === "loading" ? "성장 기록과 착용 정보를 정리하고 있습니다." : "잠시 뒤 다시 시도해 주세요. 계속 실패하면 서버의 DB 적용 상태를 확인해야 합니다."}</p>
    </div>
    {kind === "error" && onRetry && <button type="button" onClick={onRetry}>다시 불러오기</button>}
  </section>;
}
