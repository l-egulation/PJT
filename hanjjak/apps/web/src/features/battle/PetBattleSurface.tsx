import { useMemo, useState } from "react";
import { useQueries, useQuery } from "@tanstack/react-query";
import { authApi } from "../auth/api";
import { inventoryApi } from "../inventory/api";
import { useBattleRuntimeStore } from "./runtimeStore";
import { formatStageId } from "./stageLabel";
import { DEFAULT_PIP_TRACKED_ITEM_IDS, PIP_TRACKED_ITEMS, type TrackedItem } from "../picture-in-picture/trackedItems";
import { usePictureInPictureSessionStore } from "../picture-in-picture/pictureInPictureSessionStore";
import frameArt from "../picture-in-picture/assets/pip-frame-layout-a.png";
import chefSheet from "../picture-in-picture/assets/chef-strike-12frame.png";
import foeSheet from "../picture-in-picture/assets/riceball-hit-12frame.png";
import "./PetBattleSurface.css";

const MAX_TRACKED_ITEMS = 4;
const STORAGE_KEY = "hanjjak.pip.tracked-items.v1";

function readTrackedItems(): string[] {
  try {
    const parsed = JSON.parse(window.localStorage.getItem(STORAGE_KEY) ?? "null");
    if (!Array.isArray(parsed)) return DEFAULT_PIP_TRACKED_ITEM_IDS;
    const allowed = new Set(PIP_TRACKED_ITEMS.map((item) => item.itemId));
    const valid = parsed.filter((itemId): itemId is string => typeof itemId === "string" && allowed.has(itemId));
    return [...new Set(valid)].slice(0, MAX_TRACKED_ITEMS);
  } catch {
    return DEFAULT_PIP_TRACKED_ITEM_IDS;
  }
}

function gradeClass(grade: string): string {
  return `grade-${grade === "노말" ? "normal" : grade === "영웅" ? "epic" : grade === "희귀" ? "rare" : grade === "전설" ? "legendary" : grade.toLowerCase()}`;
}

function TrackedSlot({ item, gained, owned }: { item?: TrackedItem; gained: number; owned: number | null }) {
  if (!item) return <div className="pip-ledger-empty">빈 칸</div>;
  return <div className="pip-ledger-slot">
    <img src={item.icon} alt="" aria-hidden="true" />
    <span className="pip-ledger-item-copy">
      <span className="pip-ledger-item-name"><i className={`pip-grade ${gradeClass(item.grade)}`}>{item.grade}</i>{item.name}</span>
      <span className="pip-ledger-item-value">+{gained.toLocaleString()}<small>{owned == null ? "보유 확인 중" : `보유 ${owned.toLocaleString()}`}</small></span>
    </span>
  </div>;
}

export function PetBattleSurface() {
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [notice, setNotice] = useState("");
  const [selectedItemIds, setSelectedItemIds] = useState(readTrackedItems);
  const session = useQuery({ queryKey: ["auth", "session"], queryFn: authApi.session, retry: false });
  const selectedStageId = useBattleRuntimeStore((state) => state.selectedStageId);
  const battleSession = useBattleRuntimeStore((state) => state.battleSession);
  const running = useBattleRuntimeStore((state) => state.running);
  const connected = useBattleRuntimeStore((state) => state.connected);
  const settledProgression = useBattleRuntimeStore((state) => state.settledProgression);
  const latestCycle = useBattleRuntimeStore((state) => state.latestCycle);
  const latestChapter = useBattleRuntimeStore((state) => state.latestChapter);
  const clearCount = usePictureInPictureSessionStore((state) => state.clearCount);
  const gainedByItemId = usePictureInPictureSessionStore((state) => state.gainedByItemId);
  const currentStageId = battleSession?.stageId ?? latestChapter?.stoppedStageId ?? selectedStageId;
  const progression = settledProgression ?? latestChapter?.progression ?? latestCycle?.progression ?? null;
  const rice = progression?.riceBalance ?? session.data?.account?.rice ?? 0;
  const fighting = Boolean(battleSession) || running !== null;
  const selectedItems = useMemo(() => selectedItemIds.map((itemId) => PIP_TRACKED_ITEMS.find((item) => item.itemId === itemId)).filter((item): item is TrackedItem => Boolean(item)), [selectedItemIds]);
  const holdings = useQueries({ queries: selectedItems.map((item) => ({ queryKey: ["inventory", "item", item.itemId], queryFn: () => inventoryApi.detail(item.itemId), retry: false, staleTime: 5_000 })) });

  const updateSelection = (itemId: string, checked: boolean) => {
    setSelectedItemIds((current) => {
      if (checked && current.length >= MAX_TRACKED_ITEMS) {
        setNotice("최대 4개까지만 표시할 수 있어요. 먼저 하나를 해제해 주세요.");
        return current;
      }
      const next = checked ? [...current, itemId] : current.filter((id) => id !== itemId);
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
      setNotice(next.length === MAX_TRACKED_ITEMS ? "4개를 모두 선택했어요." : "");
      return next;
    });
  };

  return <div className="pip-battle-stage">
    <article className="pip-battle-surface" aria-label="한짝 한켠 전투창" style={{ backgroundImage: `url(${frameArt})` }}>
      <div className="pip-battle-window">
        <div className="pip-fighter pip-chef" role="img" aria-label="요리사 공격 동작" style={{ backgroundImage: `url(${chefSheet})` }} />
        <div className="pip-fighter pip-foe" role="img" aria-label="주먹밥 몬스터 피격 동작" style={{ backgroundImage: `url(${foeSheet})` }} />
        <p className="pip-battle-badge"><i aria-hidden="true" /><b>{fighting ? "전투 진행 중" : connected ? "전투 준비 중" : "연결 복구 중"}</b><span>스테이지 {formatStageId(currentStageId)}</span></p>
      </div>
      <button className="pip-settings-sign" type="button" aria-label="획득 표시 설정 열기" aria-expanded={settingsOpen} onClick={() => setSettingsOpen(true)}><span aria-hidden="true">⚙</span>설정</button>
      <div className="pip-ledger">
        <div className="pip-ledger-summary"><div><small>보유 쌀</small><strong>{rice.toLocaleString()}</strong></div><div className="pip-clear-count"><small>한켠 이후 클리어</small><strong>{clearCount.toLocaleString()}<em>회</em></strong></div></div>
        <div className="pip-ledger-tracked">{Array.from({ length: MAX_TRACKED_ITEMS }, (_, index) => <TrackedSlot key={selectedItems[index]?.itemId ?? `empty-${index}`} item={selectedItems[index]} gained={selectedItems[index] ? gainedByItemId[selectedItems[index].itemId] ?? 0 : 0} owned={holdings[index]?.data?.totalQuantity ?? null} />)}</div>
      </div>
      <section className={`pip-settings-panel ${settingsOpen ? "is-open" : ""}`} role="dialog" aria-label="획득 표시 설정" aria-modal="false">
        <header><div><h3>획득 표시 설정</h3><span>{selectedItemIds.length} / {MAX_TRACKED_ITEMS} 선택</span></div><button type="button" aria-label="설정 닫기" onClick={() => setSettingsOpen(false)}>×</button></header>
        <div className="pip-settings-scroll">
          {(["강화 재료", "스킬북"] as const).map((group) => <div key={group} className="pip-settings-group"><h4>{group === "강화 재료" ? "강화 재료 · F / D / C / B / A" : "스킬북 · 노말 / 희귀 / 영웅 / 전설"}</h4>{PIP_TRACKED_ITEMS.filter((item) => item.group === group).map((item) => { const checked = selectedItemIds.includes(item.itemId); const disabled = !checked && selectedItemIds.length >= MAX_TRACKED_ITEMS; return <label key={item.itemId} className={disabled ? "is-disabled" : ""}><input type="checkbox" checked={checked} aria-disabled={disabled} onChange={(event) => updateSelection(item.itemId, event.currentTarget.checked)} /><i className={`pip-grade ${gradeClass(item.grade)}`}>{item.grade}</i><span>{item.name}</span></label>; })}</div>)}
          <p className="pip-settings-notice" role="status">{notice}</p>
        </div>
      </section>
    </article>
  </div>;
}
