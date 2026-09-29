import { useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { restartActiveBattle } from "../battle/autoBattleControl";
import { authApi } from "../auth/api";
import { gemsApi, type GemFusionMode, type GemFusionResult, type GemOption, type GemPreset, type GemPresetUpdateResult, type GemState, type GemSummary } from "./api";
import { LockIcon } from "../inventory/LockIcon";
import { GemBoxRewardDialog } from "../inventory/InventoryScreen";
import closeIcon from "../../shared/assets/cozy-paper-v2/close-button.png";
import chestIcon from "./assets-cozy-pixel-v2/icons/icon-gem-chest.png";
import survivalIcon from "./assets-cozy-pixel-v2/icons/icon-survival.png";
import berserkIcon from "./assets-cozy-pixel-v2/icons/icon-berserk.png";
import armoredIcon from "./assets-cozy-pixel-v2/icons/icon-armored.png";
import raidIcon from "./assets-cozy-pixel-v2/icons/icon-entry-ticket.png";
import swordIcon from "../character/assets-cozy-pixel/sword-icon-32.png";
import gemsTitleIcon from "../../shared/assets/cozy-hud-v1/icons/bottom-gems.png";
import heartIcon from "../character/assets-cozy-pixel/heart-icon-32.png";
import targetIcon from "../character/assets-cozy-pixel/target-icon-32.png";
import "./gem-management.css";
import { NoticeToast } from "../../shared/NoticeToast";
import gemLevel12 from "./assets/gem-level-1-2.png";
import gemLevel34 from "./assets/gem-level-3-4.png";
import gemLevel56 from "./assets/gem-level-5-6.png";
import gemLevel7 from "./assets/gem-level-7.png";

export const presetNames: Record<GemPreset, string> = { MAIN: "메인", SURVIVAL: "생존형", BERSERK: "폭주형", ARMORED: "장갑형" };
const serverPresets: GemPreset[] = ["MAIN", "SURVIVAL", "BERSERK", "ARMORED"];
const options: GemOption[] = ["FLAT_ATTACK", "FLAT_HP", "ATTACK_PERCENT", "FLAT_PENETRATION", "CRITICAL_CHANCE", "HASTE"];
const optionNames: Record<GemOption, string> = { FLAT_ATTACK: "고정 공격력", FLAT_HP: "고정 최대 HP", ATTACK_PERCENT: "공격력%", FLAT_PENETRATION: "고정 방어 관통", CRITICAL_CHANCE: "치명타 확률", HASTE: "공격속도" };
const MAX_GEM_LEVEL = 7;
const MAX_CUSTOM_PRESETS = 10;
const DEFAULT_CUSTOM_PRESETS = 3;
const PRESET_UNLOCK_COST = 5_000;
const PRESET_STORAGE_KEY = "hanjjak.gem-presets.v3";
const PRESET_SLOTS = 6;
const INVENTORY_COLUMNS = 4;
const INVENTORY_MIN_CELLS = 12;

export type GemSortDirection = "asc" | "desc";
export type GemFilter = "all" | "attack" | "survival" | "special";
type CombatContext = GemPreset | "RAID";
type StoredPreset = { name: string; gemIds: string[] };
type StoredPresetState = {
  unlockedCount: number;
  activeIndex: number;
  riceSpent: number;
  presets: StoredPreset[];
  assignments: Record<CombatContext, number | null>;
};

const contextLabels: Record<CombatContext, string> = { MAIN: "메인 전투", SURVIVAL: "생존형 던전", BERSERK: "폭주형 던전", ARMORED: "장갑형 던전", RAID: "레이드" };
const optionIcons: Record<GemOption, string> = { FLAT_ATTACK: swordIcon, ATTACK_PERCENT: swordIcon, FLAT_PENETRATION: swordIcon, FLAT_HP: heartIcon, CRITICAL_CHANCE: targetIcon, HASTE: targetIcon };
const combatContexts: CombatContext[] = ["MAIN", "SURVIVAL", "BERSERK", "ARMORED", "RAID"];
const filterLabels: Array<[GemFilter, string]> = [["all", "전체"], ["attack", "공격"], ["survival", "체력"], ["special", "특수"]];

export function sortGemsByLevel(gems: GemSummary[], direction: GemSortDirection) {
  return [...gems].sort((left, right) => direction === "desc"
    ? right.level - left.level || left.gemId.localeCompare(right.gemId)
    : left.level - right.level || left.gemId.localeCompare(right.gemId));
}

const gemShapeForLevel = (level: number) => level >= 7 ? gemLevel7 : level >= 5 ? gemLevel56 : level >= 3 ? gemLevel34 : gemLevel12;

/** 합성 결과는 서버가 옵션을 새로 뽑으므로 무엇이 나올지 미리 알 수 없다.
 *  silhouette은 레벨 모양만 그림자로 보여주고 가운데에 물음표를 얹는다. */
export function GemGlyph({ mystery = false, silhouette = false, gem, level = 1, option = "FLAT_ATTACK" }: { mystery?: boolean; silhouette?: boolean; gem?: Pick<GemSummary, "level" | "option">; level?: number; option?: GemOption }) {
  const resolvedLevel = gem?.level ?? level;
  const resolvedOption = gem?.option ?? option;
  const tint = silhouette ? "silhouette" : `gem-option-${resolvedOption.toLowerCase().replaceAll("_", "-")}`;
  return <span className={`gem-glyph gem-level-${resolvedLevel} ${tint}${mystery ? " mystery" : ""}`} aria-hidden="true">
    {!mystery && <img src={gemShapeForLevel(resolvedLevel)} alt="" />}
    <i>{mystery || silhouette ? "?" : ""}</i>
  </span>;
}

function defaultStoredPresets(data: GemState): StoredPresetState {
  const fallbackSources: GemPreset[] = ["MAIN", "SURVIVAL", "BERSERK"];
  return {
    unlockedCount: DEFAULT_CUSTOM_PRESETS,
    activeIndex: 0,
    riceSpent: 0,
    presets: Array.from({ length: MAX_CUSTOM_PRESETS }, (_, index) => ({
      name: `프리셋 ${index + 1}`,
      gemIds: fallbackSources[index] ? data.presets[fallbackSources[index]!] ?.map((gem) => gem.gemId) ?? [] : [],
    })),
    assignments: { MAIN: 0, SURVIVAL: 1, BERSERK: 2, ARMORED: 2, RAID: null },
  };
}

function readStoredPresets(data: GemState): StoredPresetState {
  const fallback = defaultStoredPresets(data);
  try {
    const parsed = JSON.parse(window.localStorage.getItem(PRESET_STORAGE_KEY) ?? "null") as Partial<StoredPresetState> | null;
    if (!parsed || !Array.isArray(parsed.presets)) return fallback;
    const unlockedCount = Math.min(MAX_CUSTOM_PRESETS, Math.max(DEFAULT_CUSTOM_PRESETS, Number(parsed.unlockedCount) || DEFAULT_CUSTOM_PRESETS));
    const assignments = { ...fallback.assignments };
    for (const context of Object.keys(assignments) as CombatContext[]) {
      const candidate = parsed.assignments?.[context];
      assignments[context] = typeof candidate === "number" && candidate >= 0 && candidate < unlockedCount ? candidate : null;
    }
    return {
      unlockedCount,
      activeIndex: Math.min(unlockedCount - 1, Math.max(0, Number(parsed.activeIndex) || 0)),
      riceSpent: Math.max(0, Number(parsed.riceSpent) || 0),
      assignments,
      presets: fallback.presets.map((item, index) => ({
        name: typeof parsed.presets?.[index]?.name === "string" && parsed.presets[index]!.name.trim() ? parsed.presets[index]!.name.slice(0, 12) : item.name,
        gemIds: Array.isArray(parsed.presets?.[index]?.gemIds) ? parsed.presets[index]!.gemIds.filter((id): id is string => typeof id === "string").slice(0, PRESET_SLOTS) : item.gemIds,
      })),
    };
  } catch {
    return fallback;
  }
}

export function valueText(gem: { option: string; value: number }) {
  return gem.option.includes("PERCENT") || gem.option === "CRITICAL_CHANCE" || gem.option === "HASTE" ? `${gem.value / 100}%` : `+${gem.value.toLocaleString()}`;
}

function totalText(option: GemOption, total: number) {
  return option.includes("PERCENT") || option === "CRITICAL_CHANCE" || option === "HASTE" ? `${total / 100}%` : `+${total.toLocaleString()}`;
}

function eligibleForFusion(gem: GemSummary) {
  return gem.level < MAX_GEM_LEVEL && !gem.locked && !gem.reservedForSale && gem.equippedPresets.length === 0;
}

function filterGem(gem: GemSummary, filter: GemFilter) {
  if (filter === "all") return true;
  if (filter === "survival") return gem.option === "FLAT_HP";
  if (filter === "special") return gem.option === "CRITICAL_CHANCE" || gem.option === "HASTE";
  return gem.option === "FLAT_ATTACK" || gem.option === "ATTACK_PERCENT" || gem.option === "FLAT_PENETRATION";
}

function sameIds(left: string[], right: string[]) {
  return left.length === right.length && left.every((id, index) => id === right[index]);
}

export function selectSafeBatch(gems: GemSummary[], level: number, quantities: Partial<Record<GemOption, number>>): string[] {
  return options.flatMap((option) => gems.filter((gem) => eligibleForFusion(gem) && gem.level === level && gem.option === option)
    .sort((left, right) => left.gemId.localeCompare(right.gemId))
    .slice(0, quantities[option] ?? 0)
    .map((gem) => gem.gemId));
}

export function calculateBatchPlan(gems: GemSummary[], targetLevel: number, allowedOptions: ReadonlySet<GemOption>) {
  let carry = 0;
  let fusionCount = 0;
  let sourceCount = 0;
  for (let level = 1; level < targetLevel; level += 1) {
    const sources = gems.filter((gem) => eligibleForFusion(gem) && gem.level === level && allowedOptions.has(gem.option)).length;
    sourceCount += sources;
    const available = sources + carry;
    const fusions = Math.floor(available / 3);
    fusionCount += fusions;
    carry = fusions;
  }
  return { sourceCount, fusionCount, targetResults: carry };
}

/** 일괄 합성은 한 단계(N레벨 → N+1레벨)씩 따로 센다. 단계별로 지금 가진 보석만 쓰고,
 *  방금 만들어진 보석은 다음 단계로 넘기지 않는다. 화면의 "보유 / 예상"이 그 숫자다. */
export type BatchStep = { level: number; held: number; fusions: number; consumed: number; produced: number; gemIds: string[] };

export function buildBatchSteps(gems: GemSummary[], allowedOptions: ReadonlySet<GemOption>, targetLevel: number = MAX_GEM_LEVEL): BatchStep[] {
  /* 한 단계에서 나온 보석은 다음 단계의 재료가 된다. 서버의 fuseCascade 와 같은 셈이다. */
  let carried = 0;
  return Array.from({ length: Math.max(0, Math.min(MAX_GEM_LEVEL, targetLevel) - 1) }, (_, index) => {
    const level = index + 1;
    const pool = gems.filter((gem) => eligibleForFusion(gem) && gem.level === level && allowedOptions.has(gem.option)).sort((left, right) => left.gemId.localeCompare(right.gemId));
    const held = pool.length + carried;
    const fusions = Math.floor(held / 3);
    carried = fusions;
    return { level, held, fusions, consumed: fusions * 3, produced: fusions, gemIds: pool.slice(0, Math.min(pool.length, fusions * 3)).map((gem) => gem.gemId) };
  });
}

function GemTile({ gem, marker, blocked, focused, onSelect }: {
  gem: GemSummary;
  marker: { kind: "check" | "order"; order?: number } | null;
  blocked: boolean;
  focused: boolean;
  onSelect: () => void;
}) {
  const flags = [gem.locked ? "잠금" : null, gem.reservedForSale ? "판매 중" : null, gem.equippedPresets.length > 0 ? "장착 중" : null].filter(Boolean).join(" ");
  return <button type="button" data-gem-id={gem.gemId} className={`gem-tile${marker ? " selected" : ""}${blocked ? " blocked" : ""}${focused ? " focused" : ""}`} aria-pressed={marker !== null} onClick={onSelect} aria-label={`${gem.level}레벨 ${gem.optionName}${flags ? ` ${flags}` : ""}`}>
    {marker && <span className={`gem-tile-marker ${marker.kind}`} aria-hidden="true">{marker.kind === "order" ? marker.order : "✓"}</span>}
    <GemGlyph gem={gem} />
    <span className="gem-tile-level">Lv.{gem.level}</span>
  </button>;
}

function GemInventoryPanel({ data, busy, markerOf, isBlocked, onGemClick, statusOf, onToggleLock, lockBusy }: {
  data: GemState;
  busy: boolean;
  markerOf: (gem: GemSummary) => { kind: "check" | "order"; order?: number } | null;
  isBlocked: (gem: GemSummary) => boolean;
  onGemClick: (gem: GemSummary) => void;
  statusOf: (gem: GemSummary) => { label: string; tone: "ok" | "no" };
  onToggleLock?: (gem: GemSummary) => void;
  lockBusy?: boolean;
}) {
  const [filter, setFilter] = useState<GemFilter>("all");
  const [focusedId, setFocusedId] = useState<string | null>(null);
  const gems = useMemo(() => sortGemsByLevel(data.gems.filter((gem) => filterGem(gem, filter)), "desc"), [data.gems, filter]);
  const focused = gems.find((gem) => gem.gemId === focusedId) ?? gems[0];
  const status = focused ? statusOf(focused) : null;
  // 보석이 적어도 칸 크기는 그대로 두고, 남는 자리는 빈 네모로 채운다.
  const blankCells = Math.max(0, Math.max(INVENTORY_MIN_CELLS, Math.ceil(gems.length / INVENTORY_COLUMNS) * INVENTORY_COLUMNS) - gems.length);

  return <aside className="gem-paper-panel gem-inventory-panel">
    <header className="gem-panel-heading"><h3>보유 보석</h3></header>
    <div className="gem-filter" aria-label="보석 필터">{filterLabels.map(([value, label]) => <button type="button" key={value} className={filter === value ? "active" : ""} aria-pressed={filter === value} onClick={() => setFilter(value)}>{label}</button>)}</div>
    <div className="gem-tile-grid" aria-label="보유 보석 목록">
      {gems.map((gem) => <GemTile key={gem.gemId} gem={gem} marker={markerOf(gem)} blocked={isBlocked(gem)} focused={focused?.gemId === gem.gemId} onSelect={() => { setFocusedId(gem.gemId); if (!busy && !isBlocked(gem)) onGemClick(gem); }} />)}
      {Array.from({ length: blankCells }, (_, index) => <div key={`blank-${index}`} className="gem-tile blank" aria-hidden="true" />)}
    </div>
    <footer className="gem-inventory-detail">{focused ? <>
      <GemGlyph gem={focused} />
      <div className="gem-detail-copy">
        <strong>{focused.level}레벨 {focused.optionName}</strong>
        <b>{valueText(focused)}</b>
        {status && <div className="gem-detail-chips"><span className={`gem-detail-chip ${status.tone}`}>{status.tone === "ok" ? "✓" : "✕"} {status.label}</span></div>}
      </div>
      {onToggleLock && <button type="button" className={`gem-lock-button${focused.locked ? " is-locked" : ""}`} disabled={lockBusy} aria-pressed={focused.locked} aria-label={focused.locked ? "보석 잠금 해제" : "보석 잠금"} title={focused.locked ? "잠금 해제" : "보석 잠금"} onClick={() => onToggleLock(focused)}><LockIcon locked={focused.locked} /></button>}
    </> : <p className="gem-detail-hint">보석을 선택하면 정보가 보입니다.</p>}</footer>
  </aside>;
}

export function PresetEditor({ data, busy, riceBalance = 0, onSave, onToggleLock, lockBusy }: {
  data: GemState;
  busy: boolean;
  riceBalance?: number;
  onSave: (presetTypes: GemPreset[], gemIds: string[]) => void | Promise<unknown>;
  onToggleLock?: (gem: GemSummary) => void;
  lockBusy?: boolean;
}) {
  const [stored, setStored] = useState<StoredPresetState>(() => readStoredPresets(data));
  const [activeIndex, setActiveIndex] = useState(() => Math.min(stored.activeIndex, stored.unlockedCount - 1));
  const [editingIndex, setEditingIndex] = useState<number | null>(null);
  const [unlockIndex, setUnlockIndex] = useState<number | null>(null);
  const savedIds = stored.presets[activeIndex]?.gemIds ?? [];
  const [draftIds, setDraftIds] = useState<string[]>(savedIds);
  const selected = useMemo(() => new Set(draftIds), [draftIds]);
  const selectedGems = draftIds.map((id) => data.gems.find((gem) => gem.gemId === id)).filter((gem): gem is GemSummary => gem !== undefined);
  const assignedServerPresets = serverPresets.filter((context) => stored.assignments[context] === activeIndex);
  const presetLocked = assignedServerPresets.some((context) => data.lockedPresets.includes(context));
  const appliedEffects = options.map((option) => ({ option, total: selectedGems.filter((gem) => gem.option === option).reduce((sum, gem) => sum + gem.value, 0) })).filter((item) => item.total !== 0);
  const dirty = !sameIds(draftIds, savedIds);
  const nextSlot = selectedGems.length < PRESET_SLOTS ? selectedGems.length : null;

  useEffect(() => setDraftIds(savedIds), [activeIndex, savedIds]);
  useEffect(() => window.localStorage.setItem(PRESET_STORAGE_KEY, JSON.stringify({ ...stored, activeIndex })), [activeIndex, stored]);

  const toggleGem = (gem: GemSummary) => {
    if (presetLocked || busy) return;
    setDraftIds((current) => selected.has(gem.gemId) ? current.filter((id) => id !== gem.gemId) : current.length < PRESET_SLOTS ? [...current, gem.gemId] : current);
  };
  const updateName = (index: number, name: string) => setStored((current) => ({ ...current, presets: current.presets.map((preset, presetIndex) => presetIndex === index ? { ...preset, name: name.slice(0, 12) } : preset) }));
  const toggleAssignment = (context: CombatContext) => setStored((current) => ({ ...current, assignments: { ...current.assignments, [context]: current.assignments[context] === activeIndex ? null : activeIndex } }));
  const unlockPreset = () => {
    if (unlockIndex === null || stored.unlockedCount >= MAX_CUSTOM_PRESETS || riceBalance - stored.riceSpent < PRESET_UNLOCK_COST) return;
    setStored((current) => ({ ...current, unlockedCount: current.unlockedCount + 1, riceSpent: current.riceSpent + PRESET_UNLOCK_COST }));
    setActiveIndex(unlockIndex);
    setUnlockIndex(null);
  };
  const save = () => {
    setStored((current) => ({ ...current, activeIndex, presets: current.presets.map((preset, index) => index === activeIndex ? { ...preset, gemIds: draftIds } : preset) }));
    void onSave(assignedServerPresets, draftIds);
  };

  const activeName = stored.presets[activeIndex]?.name ?? `프리셋 ${activeIndex + 1}`;

  return <section className="gem-preset-page">
    <article className="gem-paper-panel gem-loadout-panel">
      <div className="gem-preset-tabs" role="tablist" aria-label="보석 프리셋">
        {stored.presets.slice(0, stored.unlockedCount).map((preset, index) => <div key={index} className={`gem-preset-tab-shell${activeIndex === index ? " active" : ""}`}>
          {editingIndex === index
            ? <input autoFocus aria-label={`프리셋 ${index + 1} 이름`} value={preset.name} maxLength={5} onChange={(event) => updateName(index, event.target.value)} onBlur={() => setEditingIndex(null)} onKeyDown={(event) => { if (event.key === "Enter") setEditingIndex(null); }} />
            : <button type="button" role="tab" aria-selected={activeIndex === index} onDoubleClick={() => setEditingIndex(index)} onClick={() => { setActiveIndex(index); setEditingIndex(null); }}>{preset.name}</button>}
          {activeIndex === index && editingIndex !== index && <button type="button" className="gem-preset-edit" aria-label={`${preset.name} 이름 수정`} onClick={() => setEditingIndex(index)}>✎</button>}
        </div>)}
        {stored.unlockedCount < MAX_CUSTOM_PRESETS && <div className="gem-preset-tab-shell unlock">
          <button type="button" className="gem-preset-plus" aria-label={`프리셋 ${stored.unlockedCount + 1} 열기`} onClick={() => setUnlockIndex(stored.unlockedCount)}><span aria-hidden="true" /></button>
          {unlockIndex === stored.unlockedCount && <div className="gem-unlock-confirm" role="dialog" aria-label="프리셋 열기 확인"><p>프리셋을 열기 위해 <strong>5,000 쌀</strong>이 필요합니다.<br />여시겠습니까?</p><div><button type="button" disabled={busy || riceBalance - stored.riceSpent < PRESET_UNLOCK_COST} onClick={unlockPreset}>예</button><button type="button" onClick={() => setUnlockIndex(null)}>아니오</button></div></div>}
        </div>}
      </div>

      <header className="gem-panel-heading"><h3>{activeName}</h3><strong>{selected.size} / {PRESET_SLOTS}</strong></header>
      {presetLocked && <p className="gem-warning" role="status">던전 도전이 끝날 때까지 이 프리셋을 변경할 수 없습니다.</p>}

      <div className="gem-slot-zone" aria-label={`${activeName} 프리셋 슬롯`}>{Array.from({ length: PRESET_SLOTS }, (_, index) => {
        const gem = selectedGems[index];
        const next = nextSlot === index;
        return <button type="button" key={index} className={`gem-slot-card${gem ? " filled" : " empty"}${next ? " next" : ""}`} disabled={!gem || busy || presetLocked} onClick={() => gem && toggleGem(gem)} aria-label={gem ? `${index + 1}번 슬롯 ${gem.level}레벨 ${gem.optionName} 해제` : `${index + 1}번 빈 슬롯`}>
          <em>{index + 1}</em>
          {gem ? <><GemGlyph gem={gem} /><span className="gem-tile-level">Lv.{gem.level}</span><small>{gem.optionName}</small></> : <><span className="gem-slot-plus" aria-hidden="true">＋</span>{next && <small className="gem-slot-next">다음 장착</small>}</>}
        </button>;
      })}</div>

      <div className="gem-context-assignments" aria-label="프리셋 전투 연결">{combatContexts.map((context) => {
        const active = stored.assignments[context] === activeIndex;
        return <button type="button" key={context} className={active ? "active" : ""} aria-pressed={active} onClick={() => toggleAssignment(context)}>{contextLabels[context]}</button>;
      })}</div>

      <footer className="gem-applied-effects">
        <strong>현재 적용 효과</strong>
        <div>{appliedEffects.length ? appliedEffects.map(({ option, total }) => <span key={option}><img src={optionIcons[option]} alt="" /><small>{optionNames[option]}</small><b>{totalText(option, total)}</b></span>) : <span className="empty">선택된 효과 없음</span>}</div>
        <button type="button" className="gem-save-button" disabled={busy || presetLocked || (!dirty && assignedServerPresets.length === 0)} onClick={save}>프리셋 저장</button>
      </footer>
    </article>

    <GemInventoryPanel
      data={data}
      busy={busy}
      markerOf={(gem) => selected.has(gem.gemId) ? { kind: "check" } : null}
      isBlocked={(gem) => presetLocked || (!selected.has(gem.gemId) && selected.size >= PRESET_SLOTS)}
      onGemClick={toggleGem}
      statusOf={(gem) => selected.has(gem.gemId) ? { label: "장착 중", tone: "ok" } : presetLocked ? { label: "변경 잠김", tone: "no" } : selected.size >= PRESET_SLOTS ? { label: "슬롯 가득 참", tone: "no" } : { label: "장착 가능", tone: "ok" }}
      onToggleLock={onToggleLock}
      lockBusy={lockBusy}
    />
  </section>;
}

function BatchResultCluster({ level, count }: { level: number; count: number }) {
  const shown = Math.min(count, 12);
  // 한 줄이 어중간하게 넘치지 않도록 정사각형에 가깝게 줄을 나눈다.
  const columns = Math.min(4, shown <= 3 ? Math.max(1, shown) : Math.ceil(Math.sqrt(shown)));
  return <div className="gem-batch-cluster">
    <div className="gem-batch-cluster-gems" style={{ gridTemplateColumns: `repeat(${columns}, auto)` }}>{Array.from({ length: shown }, (_, index) => <GemGlyph key={index} level={level} />)}{count > shown && <em>+{count - shown}</em>}</div>
    <span className="gem-tile-level">Lv.{level} x {count}개</span>
  </div>;
}

export function FusionPanel({ data, busy, onFuse, onToggleLock, lockBusy }: {
  data: GemState;
  busy: boolean;
  onFuse: (mode: GemFusionMode, gemIds: string[], options?: { targetLevel: number; allowedOptions: GemOption[] }) => Promise<GemFusionResult>;
  onToggleLock?: (gem: GemSummary) => void;
  lockBusy?: boolean;
}) {
  const [mode, setMode] = useState<GemFusionMode>("MANUAL");
  const [manualSlots, setManualSlots] = useState<Array<string | null>>([null, null, null]);
  const [activeSlot, setActiveSlot] = useState(0);
  const [allowedOptions, setAllowedOptions] = useState<Set<GemOption>>(() => new Set(options));
  /* 어디까지 올릴지 하나만 고른다. 2를 고르면 1→2 까지, 4를 고르면 1→4 까지 이어 간다. */
  const [targetLevel, setTargetLevel] = useState(MAX_GEM_LEVEL);
  const [phase, setPhase] = useState<"idle" | "merging" | "revealed">("idle");
  const [reward, setReward] = useState<GemSummary[]>([]);
  const selectedIds = useMemo(() => new Set(manualSlots.filter((id): id is string => id !== null)), [manualSlots]);
  const firstSelected = data.gems.find((gem) => gem.gemId === manualSlots.find((id) => id !== null));
  const equationGems = manualSlots.map((id) => data.gems.find((gem) => gem.gemId === id));
  const resultLevel = firstSelected ? firstSelected.level + 1 : null;

  const draftSteps = useMemo(() => buildBatchSteps(data.gems, allowedOptions, targetLevel), [allowedOptions, data.gems, targetLevel]);
  /* 고른 것이 곧 적용이다. 따로 누를 단추를 두지 않는다. */
  const appliedSteps = useMemo(() => draftSteps.filter((step) => step.fusions > 0), [draftSteps]);
  const appliedConsumed = appliedSteps.reduce((sum, step) => sum + step.consumed, 0);
  const appliedProduced = appliedSteps.reduce((sum, step) => sum + step.produced, 0);
  const batchSelectedIds = useMemo(() => new Set(appliedSteps.flatMap((step) => step.gemIds)), [appliedSteps]);

  useLayoutEffect(() => {
    if (reward.length === 0) return;
    const closeReward = (event: KeyboardEvent) => { if (event.key === "Escape") setReward([]); };
    window.addEventListener("keydown", closeReward);
    return () => window.removeEventListener("keydown", closeReward);
  }, [reward.length]);

  const toggleManualGem = (gem: GemSummary) => {
    const selectedIndex = manualSlots.indexOf(gem.gemId);
    if (selectedIndex >= 0) {
      setManualSlots((current) => current.map((id, index) => index === selectedIndex ? null : id));
      setActiveSlot(selectedIndex);
      return;
    }
    const target = manualSlots[activeSlot] === null ? activeSlot : manualSlots.findIndex((id) => id === null);
    if (target < 0) return;
    setManualSlots((current) => current.map((id, index) => index === target ? gem.gemId : id));
    setActiveSlot(Math.min(2, target + 1));
  };
  const manualBlocked = (gem: GemSummary) => !eligibleForFusion(gem) || (firstSelected !== undefined && firstSelected.level !== gem.level) || (!selectedIds.has(gem.gemId) && selectedIds.size >= 3);
  const toggleAllowedOption = (option: GemOption) => setAllowedOptions((current) => {
    const next = new Set(current);
    if (next.has(option)) next.delete(option); else next.add(option);
    return next;
  });
  const runManualFusion = async () => {
    const gemIds = manualSlots.filter((id): id is string => id !== null);
    if (gemIds.length !== 3) return;
    setPhase("merging"); setReward([]);
    try {
      const [result] = await Promise.all([onFuse("MANUAL", gemIds), new Promise((resolve) => window.setTimeout(resolve, 650))]);
      setReward(result.granted); setManualSlots([null, null, null]); setActiveSlot(0); setPhase("revealed");
      window.setTimeout(() => setPhase("idle"), 2400);
    } catch { setPhase("idle"); }
  };
  /** 목표 레벨까지 한 번에 맡긴다. 중간에 나온 보석까지 이어 쓰는 셈은 서버가 한다. */
  const runBatchFusion = async () => {
    if (appliedSteps.length === 0) return;
    setPhase("merging"); setReward([]);
    try {
      const result = await onFuse("SAFE_BATCH", [], { targetLevel, allowedOptions: [...allowedOptions] });
      await new Promise((resolve) => window.setTimeout(resolve, 350));
      setReward(result.granted); setPhase("revealed");
      window.setTimeout(() => setPhase("idle"), 2400);
    } catch { setPhase("idle"); }
  };

  return <section className={`gem-preset-page gem-fusion-layout${mode === "SAFE_BATCH" ? " batch" : ""}`}>
    <article className="gem-paper-panel gem-fusion-panel">
      {/* 일괄 합성은 아래 탭이 곧 제목이라, 같은 말을 두 번 적으면 칸만 좁아진다. */}
      {mode === "MANUAL" && <header className="gem-panel-heading"><h3>보석 합성</h3></header>}
      <div className="gem-fusion-modes" role="tablist" aria-label="합성 방식">
        <button type="button" role="tab" aria-selected={mode === "MANUAL"} className={mode === "MANUAL" ? "active" : ""} onClick={() => setMode("MANUAL")}>직접 합성</button>
        <button type="button" role="tab" aria-selected={mode === "SAFE_BATCH"} className={mode === "SAFE_BATCH" ? "active" : ""} onClick={() => setMode("SAFE_BATCH")}>일괄 합성</button>
      </div>

      {mode === "MANUAL" ? <>
        <div className={`gem-fusion-equation ${phase}`} aria-label="보석 더하기 보석 더하기 보석은 결과 보석">
          {Array.from({ length: 3 }, (_, index) => <button type="button" key={index} className={`gem-fusion-ingredient${activeSlot === index ? " active" : ""}`} onClick={() => { if (manualSlots[index]) setManualSlots((current) => current.map((id, slot) => slot === index ? null : id)); setActiveSlot(index); }} aria-label={`${index + 1}번 재료 칸`}>
            <span className="gem-tile-marker order" aria-hidden="true">{index + 1}</span>
            {equationGems[index] ? <><GemGlyph gem={equationGems[index]} /><span className="gem-tile-level">Lv.{equationGems[index]!.level}</span></> : <em>＋</em>}
          </button>).flatMap((item, index) => index < 2 ? [item, <b key={`plus-${index}`} className="gem-fusion-operator" aria-hidden="true">+</b>] : [item])}
          <b className="gem-fusion-operator equals" aria-hidden="true">=</b>
          <div className="gem-fusion-result">
            <GemGlyph mystery={reward.length === 0 && resultLevel === null} silhouette={reward.length === 0 && resultLevel !== null} gem={reward[0]} level={resultLevel ?? 1} />
            <span className="gem-tile-level">{reward[0] ? `Lv.${reward[0].level}` : resultLevel ? `Lv.${resultLevel}` : "결과"}</span>
          </div>
        </div>
        <p className="gem-fusion-hint">같은 레벨 보석 3개</p>
        <button type="button" className="gem-fuse-button" disabled={busy || phase === "merging" || selectedIds.size !== 3} onClick={() => void runManualFusion()}>{phase === "merging" ? "합성 중…" : "합성"}</button>
      </> : <>
        <section className="gem-batch-box">
          <h4>어디까지 올릴까요</h4>
          {/* 고른 레벨까지 1레벨부터 차곡차곡 올라간다. 중간에 나온 보석도 그대로 재료가 된다. */}
          <div className="gem-batch-target" role="radiogroup" aria-label="목표 레벨">{Array.from({ length: MAX_GEM_LEVEL - 1 }, (_, index) => index + 2).map((level) => <button
            key={level}
            type="button"
            role="radio"
            aria-checked={targetLevel === level}
            className={targetLevel === level ? "active" : ""}
            onClick={() => setTargetLevel(level)}
          >Lv.{level}</button>)}</div>
          <div className="gem-batch-steps">{draftSteps.map((step) => {
            const possible = step.fusions > 0;
            return <div key={step.level} className={`gem-batch-step${possible ? "" : " disabled"}`}>
              <b>Lv.{step.level} <i className="gem-arrow" aria-hidden="true" /> Lv.{step.level + 1}</b>
              <span className="gem-batch-step-flow" aria-hidden="true"><span><GemGlyph level={step.level} /><span className="gem-tile-level">Lv.{step.level}</span></span><i className="gem-arrow" /><span><GemGlyph level={step.level + 1} /><span className="gem-tile-level">Lv.{step.level + 1}</span></span></span>
              <span className="gem-batch-step-count"><small>모이는 재료</small><b>{step.held}개</b></span>
              {possible ? <span className="gem-batch-step-count result"><small>예상</small><b>{step.produced}개</b></span> : <span className="gem-batch-step-short">재료 부족</span>}
            </div>;
          })}</div>
        </section>
        <section className="gem-batch-box">
          <h4>허용할 보석 종류</h4>
          <div className="gem-batch-options-grid">{options.map((option) => <label key={option}><input type="checkbox" checked={allowedOptions.has(option)} onChange={() => toggleAllowedOption(option)} /><span>{optionNames[option]}</span></label>)}</div>
        </section>
      </>}

      {reward.length > 0 && <div className="gem-reward-popover" role="dialog" aria-modal="true" aria-labelledby="gem-reward-title"><div><span>짜잔!</span><GemGlyph gem={reward[0]} /><h4 id="gem-reward-title">{reward[0]!.level}레벨 {reward[0]!.optionName} 보석을 획득하였습니다!{reward.length > 1 ? ` 외 ${reward.length - 1}개` : ""}</h4><button type="button" autoFocus onClick={() => setReward([])}>확인</button></div></div>}
    </article>

    {mode === "MANUAL"
      ? <GemInventoryPanel
        data={data}
        busy={busy}
        markerOf={(gem) => { const index = manualSlots.indexOf(gem.gemId); return index >= 0 ? { kind: "order", order: index + 1 } : null; }}
        isBlocked={manualBlocked}
        onGemClick={toggleManualGem}
        statusOf={(gem) => !eligibleForFusion(gem)
          ? { label: gem.level >= MAX_GEM_LEVEL ? "최고 레벨" : gem.locked ? "잠금 상태" : gem.reservedForSale ? "판매 중" : "장착 중", tone: "no" }
          : firstSelected && firstSelected.level !== gem.level && !selectedIds.has(gem.gemId) ? { label: "레벨 불일치", tone: "no" } : { label: "합성 가능", tone: "ok" }}
        onToggleLock={onToggleLock}
        lockBusy={lockBusy}
      />
      : <aside className="gem-paper-panel gem-batch-preview">
        <header className="gem-panel-heading"><h3>예상 결과</h3></header>
        <div className="gem-batch-preview-list">{appliedSteps.length === 0
          ? <p className="equipment-empty">합성할 수 있는 보석이 없습니다.</p>
          : appliedSteps.map((step) => <section key={step.level} className="gem-batch-preview-step">
            <span className="gem-batch-step-title">Lv.{step.level} <i className="gem-arrow" aria-hidden="true" /> Lv.{step.level + 1}</span>
            <div className="gem-batch-preview-flow"><BatchResultCluster level={step.level} count={step.consumed} /><i className="gem-arrow big" aria-hidden="true" /><BatchResultCluster level={step.level + 1} count={step.produced} /></div>
          </section>)}
        </div>
        <div className="gem-batch-totals"><span><small>소모 보석</small><b>{appliedConsumed}개</b></span><span><small>획득 보석</small><b>{appliedProduced}개</b></span></div>
        <p className="gem-batch-warning">⚠ 잠금 및 장착 중인 보석은 제외됩니다</p>
        <div className="gem-batch-actions">
          <button type="button" className="gem-batch-cancel" onClick={() => setMode("MANUAL")}>취소</button>
          <button type="button" className="gem-fuse-button" disabled={busy || phase === "merging" || appliedSteps.length === 0 || batchSelectedIds.size === 0} onClick={() => void runBatchFusion()}>{phase === "merging" ? "일괄 합성 중…" : "일괄 합성"}</button>
        </div>
      </aside>}
  </section>;
}

export function GemManagement({ onClose, previewFuse }: {
  onClose?: () => void;
  previewFuse?: (mode: GemFusionMode, gemIds: string[], options?: { targetLevel: number; allowedOptions: GemOption[] }) => Promise<GemFusionResult>;
}) {
  const queryClient = useQueryClient();
  const [message, setMessage] = useState<string | null>(null);
  const [activePage, setActivePage] = useState<"preset" | "fusion">("preset");
  const state = useQuery({ queryKey: ["gems"], queryFn: gemsApi.state });
  const session = queryClient.getQueryData<Awaited<ReturnType<typeof authApi.session>>>(["auth", "session"]);
  const preset = useMutation({ mutationFn: async ({ types, gemIds }: { types: GemPreset[]; gemIds: string[] }) => {
    const results: GemPresetUpdateResult[] = [];
    for (const type of types) results.push(await gemsApi.updatePreset(type, gemIds));
    return results;
  }, onSuccess: async (results) => {
    const last = results.at(-1);
    if (last) queryClient.setQueryData<GemState>(["gems"], last.state);
    const restartedByServer = results.some((result) => result.mainBattleRestarted);
    const restarted = results.some((result) => result.preset === "MAIN") ? await restartActiveBattle() || restartedByServer : restartedByServer;
    setMessage(restarted ? "프리셋을 저장하고 현재 스테이지를 0/20부터 다시 시작했습니다." : "프리셋과 전투 연결을 저장했습니다.");
  }, onError: (error) => setMessage(error instanceof Error ? error.message : "프리셋 저장 실패") });
  const fusion = useMutation({ mutationFn: ({ mode, gemIds, options }: { mode: GemFusionMode; gemIds: string[]; options?: { targetLevel: number; allowedOptions: GemOption[] } }) => previewFuse ? previewFuse(mode, gemIds, options) : gemsApi.fuse(mode, gemIds, options), onSuccess: (result) => { queryClient.setQueryData<GemState>(["gems"], result.state); }, onError: (error) => setMessage(error instanceof Error ? error.message : "보석 합성 실패") });
  const lock = useMutation({ mutationFn: (gem: GemSummary) => gemsApi.lockSlot(gem.slotId!, !gem.locked), onSuccess: (next) => queryClient.setQueryData<GemState>(["gems"], next), onError: (error) => setMessage(error instanceof Error ? error.message : "보석 잠금 변경 실패") });
  // 보석함을 열려고 인벤토리까지 다녀오지 않아도 되게, 세어 놓은 자리에서 바로 연다.
  const [boxReward, setBoxReward] = useState<GemSummary[] | null>(null);
  const openBoxes = useMutation({
    mutationFn: (quantity: number) => gemsApi.openBoxes(quantity),
    onSuccess: async (result) => {
      queryClient.setQueryData<GemState>(["gems"], result.state);
      setBoxReward(result.granted);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["inventory"] }),
        queryClient.invalidateQueries({ queryKey: ["inventory-item", "GEM_BOX"] }),
      ]);
    },
    onError: (error) => setMessage(error instanceof Error ? error.message : "보석함을 열지 못했습니다."),
  });
  const data = state.data;
  const busy = preset.isPending || fusion.isPending;
  // 잠금은 보석이 아니라 가방 칸을 잠근다. 서버가 칸 번호를 같이 내려줄 때만 단추를 보인다.
  const toggleLock = data?.gems.some((gem) => gem.slotId) ? (gem: GemSummary) => { if (gem.slotId) lock.mutate(gem); } : undefined;
  const fusionCount = data ? [...new Set(data.gems.filter(eligibleForFusion).map((gem) => gem.level))].reduce((count, level) => count + Math.floor(data.gems.filter((gem) => eligibleForFusion(gem) && gem.level === level).length / 3), 0) : 0;
  return <section className="gems-screen gem-management-screen" aria-labelledby="gems-title"><div className="gem-management-window">
    <header className="gems-heading">
      <div className="gems-title-copy"><img src={gemsTitleIcon} alt="" aria-hidden="true" /><h2 id="gems-title">보석</h2></div>
      <nav className="gem-management-tabs" role="tablist" aria-label="보석 메뉴">
        <button type="button" role="tab" aria-selected={activePage === "preset"} className={activePage === "preset" ? "active" : ""} onClick={() => setActivePage("preset")}>프리셋</button>
        <button type="button" role="tab" aria-selected={activePage === "fusion"} className={activePage === "fusion" ? "active" : ""} onClick={() => setActivePage("fusion")}>합성{fusionCount > 0 && <span>{fusionCount}</span>}</button>
      </nav>
      {data && <div className="gem-ticket" aria-label={`보유 보석함 ${data.gemBoxQuantity.toLocaleString()}개`}>
        <small>보유 보석함</small><img src={chestIcon} alt="" /><strong>{data.gemBoxQuantity.toLocaleString()}</strong>
        <button type="button" className="gem-ticket-open" disabled={openBoxes.isPending || data.gemBoxQuantity < 1} onClick={() => openBoxes.mutate(Math.min(data.gemBoxQuantity, 100))}>{openBoxes.isPending ? "여는 중" : "열기"}</button>
      </div>}
      {onClose && <button type="button" className="paper-close" aria-label="보석 창 닫기" onClick={onClose}><img src={closeIcon} alt="" aria-hidden="true" /></button>}
    </header>
    {state.isLoading && <div className="domain-message">보석 상태를 불러오는 중입니다.</div>}{state.error && <div className="domain-message error" role="alert"><strong>보석 상태를 불러오지 못했습니다.</strong><button onClick={() => state.refetch()}>다시 시도</button></div>}{message && <NoticeToast message={message} onDone={() => setMessage(null)} />}
    {data && !data.unlocked && <div className="domain-message"><strong>보석 콘텐츠 잠김</strong><span>메인 스테이지 1-5 최초 클리어 후 입장권 3장과 보석 프리셋이 열립니다.</span></div>}
    {data?.unlocked && activePage === "preset" && <PresetEditor data={data} busy={busy} riceBalance={session?.account?.rice ?? 0} onSave={(types, gemIds) => previewFuse
      ? setMessage("연습 프리셋을 저장했습니다. 튜토리얼에서는 실제 전투 설정을 변경하지 않습니다.")
      : types.length > 0
        ? preset.mutateAsync({ types, gemIds })
        : setMessage("프리셋을 로컬에 저장했습니다. 전투 연결을 선택하면 자동 적용됩니다.")} onToggleLock={toggleLock} lockBusy={lock.isPending} />}
    {data?.unlocked && activePage === "fusion" && <FusionPanel data={data} busy={busy} onFuse={(mode, gemIds, options) => fusion.mutateAsync({ mode, gemIds, options })} onToggleLock={toggleLock} lockBusy={lock.isPending} />}
    {boxReward && <GemBoxRewardDialog gems={boxReward} onClose={() => setBoxReward(null)} />}
  </div></section>;
}

export function GemManagementWindow({ open, onClose }: { open: boolean; onClose: () => void }) {
  const dialog = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    const element = dialog.current;
    if (!element) return;
    if (open && !element.open) element.showModal();
    if (!open && element.open) element.close();
  }, [open]);
  return <dialog ref={dialog} className="gem-management-dialog" aria-labelledby="gems-title" onCancel={(event) => { event.preventDefault(); onClose(); }} onMouseDown={(event) => { if (event.currentTarget === event.target) onClose(); }}><GemManagement onClose={onClose} /></dialog>;
}
