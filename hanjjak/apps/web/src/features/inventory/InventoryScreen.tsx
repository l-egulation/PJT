import { useEffect, useMemo, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { inventoryApi, type InventoryCategory, type InventoryItem, type InventorySort } from "./api";
import { gemsApi, type GemSummary } from "../gems/api";
import { gemIdentityFromItemId } from "../gems/gemIdentity";
import { GemGlyph, valueText } from "../gems/GemManagement";
import { LockIcon } from "./LockIcon";
import inventoryTitleIcon from "../../shared/assets/cozy-hud-v1/icons/bottom-items.png";
import closeIcon from "../../shared/assets/cozy-paper-v2/close-button.png";
import inventoryBookIcon from "../../shared/assets/cozy-hud-v1/icons/reward-skillbook.png";
import gemIcon from "../../shared/assets/cozy-hud-v1/icons/bottom-gems.png";
import gemBoxChestIcon from "./assets-gem-box-chest.png";
import potatoFragment from "../equipment/assets/material-ranks/potato-fragment-f.png";
import potatoMini from "../equipment/assets/material-ranks/potato-mini-d.png";
import potato from "../equipment/assets/material-ranks/potato-c.png";
import potatoGolden from "../equipment/assets/material-ranks/potato-golden-b.png";
import potatoLegendary from "../equipment/assets/material-ranks/potato-legendary-a.png";
import sweetPotatoFragment from "../equipment/assets/material-ranks/sweet-potato-fragment-f.png";
import sweetPotatoMini from "../equipment/assets/material-ranks/sweet-potato-mini-d.png";
import sweetPotato from "../equipment/assets/material-ranks/sweet-potato-c.png";
import sweetPotatoGolden from "../equipment/assets/material-ranks/sweet-potato-golden-b.png";
import sweetPotatoLegendary from "../equipment/assets/material-ranks/sweet-potato-legendary-a.png";
import cornKernel from "../equipment/assets/material-ranks/corn-kernel-f.png";
import cornMini from "../equipment/assets/material-ranks/corn-mini-d.png";
import corn from "../equipment/assets/material-ranks/corn-c.png";
import cornGolden from "../equipment/assets/material-ranks/corn-golden-b.png";
import cornLegendary from "../equipment/assets/material-ranks/corn-legendary-a.png";
import activeHeavyIcon from "../skills/assets-cozy-pixel/skill-active-heavy.png";
import activeDotIcon from "../skills/assets-cozy-pixel/skill-active-dot.png";
import activeHasteIcon from "../skills/assets-cozy-pixel/skill-active-haste.png";
import activeBasicAmpIcon from "../skills/assets-cozy-pixel/skill-active-basic-amp.png";
import passiveCriticalIcon from "../skills/assets-cozy-pixel/skill-passive-critical.png";
import passiveAllDamageIcon from "../skills/assets-cozy-pixel/skill-passive-all-damage.png";
import { NoticeToast } from "../../shared/NoticeToast";
import "./InventoryScreen.css";

export type InventoryDataSource = Pick<typeof inventoryApi, "list" | "detail">;

export function GemOptions({ item, gems }: { item: InventoryItem; gems: GemSummary[] }) {
  const gem = gems.find((entry) => entry.gemId === item.members[0]?.instanceId);
  return <div className="inventory-v2-detail-section"><h4>보석 옵션</h4>
    <p>{gem ? `Lv.${gem.level} ${gem.optionName} ${gem.value}` : "보석 정보 미확인"}</p>
  </div>;
}

const CATEGORY_LABELS: Record<InventoryCategory | "ALL", string> = {
  ALL: "전체",
  MATERIAL: "강화 재료",
  SKILL_BOOK: "스킬북",
  GEM: "보석",
  GEM_BOX: "보석함",
  COSMETIC_BOX: "치장 선택 상자",
};

const SORT_LABELS: Record<InventorySort, string> = {
  ACQUIRED_DESC: "최근 획득순",
  NAME_ASC: "이름 오름차순",
  NAME_DESC: "이름 내림차순",
  QUANTITY_DESC: "수량 많은순",
  QUANTITY_ASC: "수량 적은순",
};

const MATERIAL_ASSETS: Record<string, string> = {
  POTATO_M1: potatoFragment, POTATO_M2: potatoMini, POTATO_M3: potato, POTATO_M4: potatoGolden, POTATO_M5: potatoLegendary,
  SWEET_POTATO_M1: sweetPotatoFragment, SWEET_POTATO_M2: sweetPotatoMini, SWEET_POTATO_M3: sweetPotato, SWEET_POTATO_M4: sweetPotatoGolden, SWEET_POTATO_M5: sweetPotatoLegendary,
  CORN_M1: cornKernel, CORN_M2: cornMini, CORN_M3: corn, CORN_M4: cornGolden, CORN_M5: cornLegendary,
};

const SKILL_BOOK_BADGE_ASSETS: Record<string, string> = {
  active_heavy: activeHeavyIcon,
  active_dot: activeDotIcon,
  active_haste: activeHasteIcon,
  active_basic_amp: activeBasicAmpIcon,
  passive_critical: passiveCriticalIcon,
  passive_all_damage: passiveAllDamageIcon,
};
const SKILL_BOOK_GRADES: Record<string, { name: string; className: string }> = {
  normal: { name: "노말", className: "normal" },
  rare: { name: "희귀", className: "rare" },
  epic: { name: "영웅", className: "epic" },
  legendary: { name: "전설", className: "legendary" },
};

type SkillBookPresentation = { skillId: string; gradeName: string; gradeClass: string; displayName: string; badgeAsset: string | null };

export function inventorySkillBookPresentation(item: Pick<InventoryItem, "itemId" | "name" | "category">): SkillBookPresentation | null {
  if (item.category !== "SKILL_BOOK") return null;
  const [, skillId, gradeId] = item.itemId.split(":");
  const grade = SKILL_BOOK_GRADES[gradeId?.toLowerCase()] ?? { name: "등급 미확인", className: "locked" };
  const prefix = `${grade.name} `;
  return {
    skillId: skillId ?? "",
    gradeName: grade.name,
    gradeClass: grade.className,
    displayName: item.name.startsWith(prefix) ? item.name.slice(prefix.length) : item.name,
    badgeAsset: SKILL_BOOK_BADGE_ASSETS[skillId] ?? null,
  };
}

export type InventoryMaterialRank = "F" | "D" | "C" | "B" | "A";
const MATERIAL_RANKS: InventoryMaterialRank[] = ["F", "D", "C", "B", "A"];

export function inventoryMaterialRank(itemId: string): InventoryMaterialRank | null {
  const generation = Number(itemId.match(/_M([1-5])$/)?.[1]);
  return MATERIAL_RANKS[generation - 1] ?? null;
}

/* 보석 칸은 등급별 모양과 옵션 색을 그대로 쓴다. 규칙은 gemIdentity 가 갖고 있다. */
export const inventoryGemGlyph = gemIdentityFromItemId;

export function inventoryItemAsset(item: InventoryItem) {
  const materialAsset = MATERIAL_ASSETS[item.itemId];
  if (materialAsset) return materialAsset;
  if (item.category === "SKILL_BOOK") return inventoryBookIcon;
  if (item.category === "GEM_BOX") return gemBoxChestIcon;
  if (item.category === "GEM") return gemIcon;
  return item.icon || null;
}

function itemKey(item: InventoryItem) {
  return item.slotId ?? `${item.itemId}:${item.acquiredSequence}`;
}

function ItemIcon({ item, large = false }: { item: InventoryItem; large?: boolean }) {
  const localAsset = inventoryItemAsset(item);
  const [source, setSource] = useState(localAsset);
  const skillBook = inventorySkillBookPresentation(item);
  const gem = item.category === "GEM" ? inventoryGemGlyph(item.itemId) : null;

  useEffect(() => setSource(localAsset), [localAsset]);

  if (gem) return <span className={`inventory-v2-gem-icon ${large ? "is-large" : ""}`}><GemGlyph level={gem.level} option={gem.option} /></span>;
  if (skillBook && source) return <span className={`inventory-v2-skillbook-icon ${large ? "is-large" : ""}`} aria-hidden="true">
    <img className="inventory-v2-skillbook-base" src={source} alt="" />
    {skillBook.badgeAsset && <img className="inventory-v2-skillbook-badge" src={skillBook.badgeAsset} alt="" />}
  </span>;
  if (!source) return <span className={`inventory-v2-icon-fallback ${large ? "is-large" : ""}`} aria-hidden="true">{item.name.slice(0, 1)}</span>;
  return <img
    className={`inventory-v2-icon ${large ? "is-large" : ""}`}
    src={source}
    alt=""
    onError={() => setSource((current) => current === localAsset ? null : localAsset)}
  />;
}

export function InventoryWindow({ open, onClose, onSell, dataSource = inventoryApi }: { open: boolean; onClose: () => void; onSell?: (itemId: string) => void; dataSource?: InventoryDataSource }) {
  const dialog = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    const element = dialog.current;
    if (!element) return;
    if (open && !element.open) element.showModal();
    if (!open && element.open) element.close();
  }, [open]);

  return <dialog
    ref={dialog}
    className="inventory-window"
    aria-labelledby="inventory-title"
    onCancel={(event) => {
      event.preventDefault();
      onClose();
    }}
    onMouseDown={(event) => {
      if (event.currentTarget === event.target) onClose();
    }}
  >
    <InventoryScreen onClose={onClose} onSell={onSell} dataSource={dataSource} />
  </dialog>;
}

export function InventoryScreen({ onClose, onSell, dataSource = inventoryApi }: { onClose?: () => void; onSell?: (itemId: string) => void; dataSource?: InventoryDataSource } = {}) {
  const queryClient = useQueryClient();
  const [category, setCategory] = useState<InventoryCategory | "ALL">("ALL");
  const [sort, setSort] = useState<InventorySort>("ACQUIRED_DESC");
  const [cursor, setCursor] = useState<string | null>(null);
  const [history, setHistory] = useState<Array<string | null>>([]);
  const [selectedItemId, setSelectedItemId] = useState<string | null>(null);
  const [selectedRowKey, setSelectedRowKey] = useState<string | null>(null);
  const inventory = useQuery({ queryKey: ["inventory", category, sort, cursor], queryFn: () => dataSource.list(category, sort, cursor) });
  const detail = useQuery({ queryKey: ["inventory-item", selectedItemId], queryFn: () => dataSource.detail(selectedItemId!), enabled: selectedItemId !== null });
  const selectedSlot = inventory.data?.items.find((item) => itemKey(item) === selectedRowKey);
  const detailMaterialRank = detail.data?.category === "MATERIAL" ? inventoryMaterialRank(detail.data.itemId) : null;
  const gems = useQuery({ queryKey: ["gems"], queryFn: gemsApi.state, enabled: selectedSlot?.category === "GEM" });
  const lock = useMutation({
    mutationFn: (item: InventoryItem) => gemsApi.lockSlot(item.slotId!.substring("instance:".length), !item.locked),
    onSuccess: async () => { await Promise.all([
      queryClient.invalidateQueries({ queryKey: ["inventory"] }),
      queryClient.invalidateQueries({ queryKey: ["gems"] }),
    ]); },
  });
  const [boxReward, setBoxReward] = useState<GemSummary[] | null>(null);
  const openGemBoxes = useMutation({
    mutationFn: (quantity: number) => gemsApi.openBoxes(quantity),
    onSuccess: async (result) => {
      queryClient.setQueryData(["gems"], result.state);
      setBoxReward(result.granted);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["inventory"] }),
        queryClient.invalidateQueries({ queryKey: ["inventory-item", "GEM_BOX"] }),
      ]);
    },
  });

  const capacityPercent = useMemo(() => {
    if (!inventory.data?.maxSlots) return 0;
    return Math.min(100, inventory.data.usedSlots / inventory.data.maxSlots * 100);
  }, [inventory.data]);

  useEffect(() => {
    if (!inventory.data) return;
    const currentExists = selectedRowKey && inventory.data.items.some((item) => itemKey(item) === selectedRowKey);
    if (currentExists) return;
    const firstItem = inventory.data.items[0];
    setSelectedItemId(firstItem?.itemId ?? null);
    setSelectedRowKey(firstItem ? itemKey(firstItem) : null);
  }, [inventory.data, selectedRowKey]);

  const resetCollection = () => {
    setCursor(null);
    setHistory([]);
    setSelectedItemId(null);
    setSelectedRowKey(null);
  };
  const changeCategory = (nextCategory: InventoryCategory | "ALL") => {
    resetCollection();
    setCategory(nextCategory);
  };
  const changeSort = (nextSort: InventorySort) => {
    resetCollection();
    setSort(nextSort);
  };

  const select = (item: InventoryItem) => {
    lock.reset();
    openGemBoxes.reset();
    setSelectedItemId(item.itemId);
    setSelectedRowKey(itemKey(item));
  };
  const next = () => {
    if (!inventory.data?.nextCursor) return;
    setHistory((current) => [...current, cursor]);
    setCursor(inventory.data.nextCursor);
    setSelectedItemId(null);
    setSelectedRowKey(null);
  };
  const previous = () => setHistory((current) => {
    const copy = [...current];
    setCursor(copy.pop() ?? null);
    setSelectedItemId(null);
    setSelectedRowKey(null);
    return copy;
  });

  return <section className="cozy-inventory inventory-screen" aria-labelledby="inventory-title">
    <div className="inventory-v2-paper">
      <header className="inventory-v2-heading">
        <div className="inventory-v2-title">
          <img src={inventoryTitleIcon} alt="" aria-hidden="true" />
          <h2 id="inventory-title">인벤토리</h2>
        </div>
        {inventory.data && <div className={`inventory-v2-capacity ${inventory.data.isFull ? "is-full" : ""}`} aria-label={`인벤토리 ${inventory.data.usedSlots}/${inventory.data.maxSlots} 슬롯`}>
          <div><span>가방 공간</span><strong>{inventory.data.usedSlots}<small> / {inventory.data.maxSlots}</small></strong></div>
          <div className="inventory-v2-capacity-track"><span style={{ width: `${capacityPercent}%` }} /></div>
        </div>}
        {onClose && <button type="button" className="paper-close" aria-label="인벤토리 닫기" onClick={onClose}><img src={closeIcon} alt="" aria-hidden="true" /></button>}
      </header>

      <div className="inventory-v2-toolbar">
        <div className="inventory-v2-tabs" role="tablist" aria-label="아이템 분류">
          {Object.entries(CATEGORY_LABELS).map(([value, label]) => <button key={value} role="tab" aria-selected={category === value} className={category === value ? "is-active" : ""} onClick={() => changeCategory(value as InventoryCategory | "ALL")}>
            {label}
          </button>)}
        </div>
        <label className="inventory-v2-sort"><span className="sr-only">정렬</span><select value={sort} onChange={(event) => changeSort(event.target.value as InventorySort)}>{Object.entries(SORT_LABELS).map(([value, label]) => <option value={value} key={value}>{label}</option>)}</select></label>
      </div>

      {inventory.isLoading && <div className="inventory-v2-message" aria-live="polite">인벤토리를 불러오는 중입니다.</div>}
      {inventory.error && !inventory.data && <div className="inventory-v2-message is-error" role="alert"><strong>인벤토리를 불러오지 못했습니다.</strong><button onClick={() => inventory.refetch()}>다시 시도</button></div>}
      {inventory.data && inventory.data.items.length === 0 && <div className="inventory-v2-message"><strong>아직 보유한 아이템이 없습니다.</strong><span>자동 파밍과 보석 던전에서 첫 아이템을 획득해 보세요.</span></div>}

      {inventory.data && inventory.data.items.length > 0 && <div className="inventory-v2-layout">
        <div className="inventory-v2-collection">
          <ul className="inventory-v2-grid" aria-label="보유 아이템">{inventory.data.items.map((item) => {
            const materialRank = item.category === "MATERIAL" ? inventoryMaterialRank(item.itemId) : null;
            const skillBook = inventorySkillBookPresentation(item);
            return <li key={itemKey(item)}>
              <button className={selectedRowKey === itemKey(item) ? "is-selected" : ""} onClick={() => select(item)} aria-pressed={selectedRowKey === itemKey(item)}>
                <span className="inventory-v2-art"><ItemIcon item={item} />{item.locked && <span className="inventory-v2-lock" role="img" aria-label="잠긴 칸"><LockIcon locked /></span>}</span>
                <strong title={item.name}>{skillBook?.displayName ?? item.name}</strong>
                <span className="inventory-v2-card-meta"><small className={materialRank ? `inventory-v2-grade is-grade-${materialRank.toLowerCase()}` : skillBook ? `inventory-v2-grade is-skill-grade-${skillBook.gradeClass}` : ""}>{materialRank ? `${materialRank}등급` : skillBook?.gradeName ?? CATEGORY_LABELS[item.category]}</small><b>{item.totalQuantity.toLocaleString()}개</b></span>
              </button>
            </li>;
          })}</ul>
          <nav className="inventory-v2-pagination" aria-label="인벤토리 페이지"><button aria-label="이전 쪽" disabled={history.length === 0} onClick={previous}><i className="cozy-chevron is-previous" aria-hidden="true" /></button><span>{history.length + 1}</span><button aria-label="다음 쪽" disabled={!inventory.data.nextCursor} onClick={next}><i className="cozy-chevron is-next" aria-hidden="true" /></button></nav>
        </div>

        <aside className="inventory-v2-detail" aria-live="polite">
          {!selectedItemId && <div className="inventory-v2-placeholder"><img src={inventoryBookIcon} alt="" /><strong>아이템을 선택해 주세요</strong></div>}
          {detail.isLoading && <p className="inventory-v2-detail-status">상세 정보를 불러오는 중입니다.</p>}
          {detail.error && <p className="inventory-v2-detail-status" role="alert">상세 정보를 불러오지 못했습니다.</p>}
          {detail.data && selectedSlot && (() => {
            const skillBook = inventorySkillBookPresentation(detail.data);
            return <>
            <div className="inventory-v2-detail-hero">
              <span className="inventory-v2-spark" aria-hidden="true">✦</span>
              {/* 자물쇠는 그림 바로 오른쪽 위에 붙는다. 그림과 한 덩어리로 묶어야
                  칸 크기가 바뀌어도 따라다닌다. */}
              <span className="inventory-v2-detail-art">
              <ItemIcon item={detail.data} large />
              {selectedSlot.category === "GEM" && <button
                type="button"
                className={`inventory-v2-gem-lock${selectedSlot.locked ? " is-locked" : ""}`}
                disabled={lock.isPending || !selectedSlot.slotId || !selectedSlot.members.length || !selectedSlot.members.every((member) => gems.data?.gems.some((gem) => gem.gemId === member.instanceId))}
                aria-pressed={!!selectedSlot.locked}
                aria-label={selectedSlot.locked ? "보석 잠금 해제" : "보석 잠금"}
                title={selectedSlot.locked ? "잠금 해제" : "보석 잠금"}
                onClick={() => lock.mutate(selectedSlot)}
              ><LockIcon locked={!!selectedSlot.locked} /></button>}
              </span>
              {/* 강화 재료에만 종류를 적어 스킬북은 이름 위가 비어 있었다. 어느 칸이든 같은 자리에 적는다. */}
              <span className="inventory-v2-detail-kind">{CATEGORY_LABELS[detail.data.category]}</span>
              <h3 className="inventory-v2-detail-name">{skillBook?.displayName ?? detail.data.name}</h3>
              <p className="inventory-v2-detail-tags">
                <mark>{detail.data.tradeable ? "거래 가능" : "계정 귀속"}</mark>
                {skillBook
                  ? <span className={`inventory-v2-grade is-skill-grade-${skillBook.gradeClass}`}>{skillBook.gradeName}</span>
                  : detailMaterialRank && <span className={`inventory-v2-grade is-grade-${detailMaterialRank.toLowerCase()}`}>{detailMaterialRank}등급</span>}
              </p>
            </div>
            <div className="inventory-v2-quantity">
              <span>보유 수량</span>
              <div className="inventory-v2-quantity-actions">
                <strong>{detail.data.totalQuantity.toLocaleString()}<small>개</small></strong>
                {detail.data.tradeable && onSell && <button
                  type="button"
                  className="inventory-v2-sell-shortcut"
                  onClick={() => onSell(detail.data!.itemId)}
                  aria-label={`${detail.data.name} 거래소에서 바로 판매`}
                >바로 판매</button>}
              </div>
            </div>
            <p className="inventory-v2-description">{detail.data.description}</p>
            {selectedSlot.category === "GEM" && <>
              <GemOptions item={selectedSlot} gems={gems.error ? [] : gems.data?.gems ?? []} />
              {lock.error && <NoticeToast message="잠금을 변경하지 못했습니다." tone="failure" onDone={() => lock.reset()} />}
            </>}
            {selectedSlot.category === "GEM_BOX" && <div className="inventory-v2-box-actions">
              <button className="inventory-v2-action" disabled={openGemBoxes.isPending || selectedSlot.availableQuantity < 1} onClick={() => openGemBoxes.mutate(1)}>보석함 1개 열기</button>
              <button className="inventory-v2-action is-secondary" disabled={openGemBoxes.isPending || selectedSlot.availableQuantity < 2} onClick={() => openGemBoxes.mutate(Math.min(selectedSlot.availableQuantity, 100))}>{selectedSlot.availableQuantity > 100 ? "보석함 최대 100개 열기" : "사용 가능 수량 모두 열기"}</button>
              {openGemBoxes.data && <p className="inventory-v2-box-result" role="status">보석 {openGemBoxes.data.granted.length}개를 획득했습니다.</p>}
              {openGemBoxes.error && <NoticeToast message="보석함을 열지 못했습니다." tone="failure" onDone={() => openGemBoxes.reset()} />}
            </div>}
            {/* 항상 펼쳐 두면 보유 수량과 옵션이 화면 밖으로 밀린다. 필요할 때만 연다. */}
            <div className="inventory-v2-detail-columns">
              <InventoryHint label="획득처" items={detail.data.acquisitionSources} />
              <InventoryHint label="사용처" items={detail.data.usages} />
            </div>
          </>;
          })()}
        </aside>
      </div>}
    </div>
    {boxReward && <GemBoxRewardDialog gems={boxReward} onClose={() => setBoxReward(null)} />}
  </section>;
}

/** 손을 얹거나 글쇠로 옮겨 오면 위쪽으로 펼친다. 아래로 펼치면 종이 톱니에 잘린다. */
function InventoryHint({ label, items }: { label: string; items: string[] }) {
  return <div className="inventory-v2-detail-section" tabIndex={0} role="group" aria-label={label}>
    <span className="inventory-v2-detail-section-label">{label}<i aria-hidden="true" /></span>
    <ul className="inventory-v2-detail-pop">{items.length
      ? items.map((entry) => <li key={entry}>{entry}</li>)
      : <li>알려진 곳이 없습니다.</li>}</ul>
  </div>;
}

/** 보석함을 열면 무엇이 나왔는지 바로 알 수 있어야 한다. 같은 보석은 묶어서 세고
 *  높은 레벨부터 보여준다. 100개를 한 번에 열어도 목록이 감당되도록. */
export function GemBoxRewardDialog({ gems, onClose }: { gems: GemSummary[]; onClose: () => void }) {
  const rows = useMemo(() => {
    const grouped = new Map<string, { gem: GemSummary; count: number }>();
    for (const gem of gems) {
      const key = `${gem.level}:${gem.option}:${gem.value}`;
      const found = grouped.get(key);
      if (found) found.count += 1; else grouped.set(key, { gem, count: 1 });
    }
    return [...grouped.values()].sort((left, right) => (right.gem.level ?? 0) - (left.gem.level ?? 0) || (left.gem.optionName ?? "").localeCompare(right.gem.optionName ?? ""));
  }, [gems]);

  useEffect(() => {
    const close = (event: KeyboardEvent) => { if (event.key === "Escape") onClose(); };
    window.addEventListener("keydown", close);
    return () => window.removeEventListener("keydown", close);
  }, [onClose]);

  return <div className="gem-box-reward-backdrop" onMouseDown={(event) => { if (event.currentTarget === event.target) onClose(); }}>
    <section className="gem-box-reward" role="dialog" aria-modal="true" aria-labelledby="gem-box-reward-title">
      <header>
        <img src={gemBoxChestIcon} alt="" aria-hidden="true" />
        <h3 id="gem-box-reward-title">보석 {gems.length}개를 얻었습니다</h3>
      </header>
      <ul>{rows.map(({ gem, count }) => <li key={`${gem.level}:${gem.option}:${gem.value}`}>
        <GemGlyph gem={gem} />
        <b>Lv.{gem.level}</b>
        <span>{gem.optionName ?? "보석"}</span>
        <small>{gem.option ? valueText(gem) : ""}</small>
        <em>{count > 1 ? `x${count}` : ""}</em>
      </li>)}</ul>
      <button type="button" autoFocus onClick={onClose}>확인</button>
    </section>
  </div>;
}
