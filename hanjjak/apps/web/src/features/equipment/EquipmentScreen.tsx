import { useDeferredValue, useEffect, useRef, useState } from "react";
import { TopAlert } from "../../shared/TopAlert";
import { NoticeToast } from "../../shared/NoticeToast";
import type { PointerEvent as ReactPointerEvent, ReactNode } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { heroFrame } from "../battle/battleVisuals";
import { CosmeticHero, useOwnCosmeticAppearance } from "../cosmetics/CosmeticHero";
import { cosmeticArtUrl } from "../cosmetics/cosmetic-art";
import { MAX_MARKET_UNIT_PRICE, marketUnitPrice, type NumericFieldValue } from "../market/marketForm";
import { rankingApi } from "../ranking/api";
import accentRaysCoral from "../character/assets-cozy-pixel/accent-rays-coral.png";
import closeIcon from "../../shared/assets/cozy-paper-v2/close-button.png";
import sectionCheckIcon from "../character/assets-cozy-pixel/section-check-icon-32.png";
import navMarketIcon from "../../shared/assets/cozy-hud-v1/icons/nav-market.png";
import armorIcon from "./assets-cozy-pixel/equipment-armor-basic-v2.png";
import bootsIcon from "./assets-cozy-pixel/equipment-boots-basic-v2.png";
import capeIcon from "./assets-cozy-pixel/equipment-cape-basic-v2.png";
import glovesIcon from "./assets-cozy-pixel/equipment-gloves-basic-v2.png";
import helmetIcon from "./assets-cozy-pixel/equipment-helmet-basic-v2.png";
import loadoutCourtyard from "./assets-cozy-pixel/equipment-loadout-courtyard-v2.png";
import equipmentTitleIcon from "../character/assets-stats-v2/icon-attack.png";
import marketIcon from "./assets-cozy-pixel/market-stall-v2.png";
import potatoIcon from "./assets-cozy-pixel/material-potato-v2.png";
import riceGoldIcon from "./assets-cozy-pixel/currency-rice-gold-256.png";
import weaponIcon from "./assets-cozy-pixel/equipment-weapon-basic-v2.png";
import cornKernel from "./assets/material-ranks/corn-kernel-f.png";
import cornMini from "./assets/material-ranks/corn-mini-d.png";
import corn from "./assets/material-ranks/corn-c.png";
import cornGolden from "./assets/material-ranks/corn-golden-b.png";
import cornLegendary from "./assets/material-ranks/corn-legendary-a.png";
import potatoFragment from "./assets/material-ranks/potato-fragment-f.png";
import potatoMini from "./assets/material-ranks/potato-mini-d.png";
import potato from "./assets/material-ranks/potato-c.png";
import potatoGolden from "./assets/material-ranks/potato-golden-b.png";
import potatoLegendary from "./assets/material-ranks/potato-legendary-a.png";
import sweetPotatoFragment from "./assets/material-ranks/sweet-potato-fragment-f.png";
import sweetPotatoMini from "./assets/material-ranks/sweet-potato-mini-d.png";
import sweetPotato from "./assets/material-ranks/sweet-potato-c.png";
import sweetPotatoGolden from "./assets/material-ranks/sweet-potato-golden-b.png";
import sweetPotatoLegendary from "./assets/material-ranks/sweet-potato-legendary-a.png";
import { equipmentApi, isUncertainEquipmentCommandError, type EquipmentActionSummary, type EquipmentCommand, type EquipmentCost, type EquipmentMaterialCost, type EquipmentMaterialPurchaseCommand, type EquipmentSlot, type EquipmentSlotSummary, type EquipmentState, type EquipmentStatSummary } from "./api";

const SLOT_ORDER = ["WEAPON", "GLOVES", "ARMOR", "HELMET", "CAPE", "SHOES"] as const;
const characterRest = heroFrame("rest", 0);
const SLOT_ICONS: Record<EquipmentSlot, string> = { WEAPON: weaponIcon, GLOVES: glovesIcon, ARMOR: armorIcon, HELMET: helmetIcon, CAPE: capeIcon, SHOES: bootsIcon };

/* 장비 칸에는 그 부위에 입은 치장 그림을 보여준다. 없으면 기본 그림 그대로다. */
const COSMETIC_SLOT_FOR_EQUIPMENT: Record<EquipmentSlot, string> = { HELMET: "HEAD", ARMOR: "TOP", GLOVES: "GLOVES", SHOES: "SHOES", CAPE: "CAPE", WEAPON: "BOTTOM" };

type WornCosmetic = { art: string | null; name: string | null };

function wornCosmetic(slot: EquipmentSlot, equipment?: Record<string, string | null>, catalog?: { cosmetics: Array<{ cosmeticId: string; displayName?: string | null; imageUrl?: string | null; setId?: string; slot?: string }> }): WornCosmetic | null {
  const uiSlot = COSMETIC_SLOT_FOR_EQUIPMENT[slot];
  const id = equipment?.[uiSlot] ?? (uiSlot === "BOTTOM" ? equipment?.WEAPON : null);
  const cosmetic = id ? catalog?.cosmetics.find(item => item.cosmeticId === id) : undefined;
  return cosmetic ? { art: cosmeticArtUrl(cosmetic), name: cosmetic.displayName ?? null } : null;
}
type MaterialKind = "potato" | "sweet_potato" | "corn";
const MATERIAL_RANKS = [
  { id: "M1", rank: "F", label: "한 조각" },
  { id: "M2", rank: "D", label: "미니" },
  { id: "M3", rank: "C", label: "기본" },
  { id: "M4", rank: "B", label: "황금" },
  { id: "M5", rank: "A", label: "전설" },
] as const;
type MaterialRank = (typeof MATERIAL_RANKS)[number]["id"];
const MATERIAL_RANK_ICONS: Record<MaterialKind, Record<MaterialRank, string>> = {
  potato: { M1: potatoFragment, M2: potatoMini, M3: potato, M4: potatoGolden, M5: potatoLegendary },
  sweet_potato: { M1: sweetPotatoFragment, M2: sweetPotatoMini, M3: sweetPotato, M4: sweetPotatoGolden, M5: sweetPotatoLegendary },
  corn: { M1: cornKernel, M2: cornMini, M3: corn, M4: cornGolden, M5: cornLegendary },
};
const MATERIAL_DISPLAY_NAMES: Record<MaterialKind, Record<MaterialRank, string>> = {
  potato: { M1: "감자 한 조각", M2: "미니 감자", M3: "감자", M4: "황금 감자", M5: "전설 감자" },
  sweet_potato: { M1: "고구마 한 조각", M2: "미니 고구마", M3: "고구마", M4: "황금 고구마", M5: "전설 고구마" },
  corn: { M1: "옥수수 한 알", M2: "미니 옥수수", M3: "옥수수", M4: "황금 옥수수", M5: "전설 옥수수" },
};

const ERROR_LABELS: Record<string, string> = {
  EQUIPMENT_ALREADY_UNLOCKED: "이미 해금한 부위입니다.",
  EQUIPMENT_NOT_UNLOCKED: "먼저 부위를 해금해 주세요.",
  EQUIPMENT_MAX_ENHANCEMENT: "현재 등급은 +30강이 최대입니다.",
  EQUIPMENT_MAX_ENHANCEMENT_REQUIRED: "현재 등급 +30강이 필요합니다.",
  EQUIPMENT_CHAPTER_NOT_CLEARED: "필요한 챕터를 먼저 클리어해 주세요.",
  EQUIPMENT_MAX_GRADE: "최종 등급에 도달했습니다.",
  INSUFFICIENT_RICE: "쌀이 부족합니다.",
  INSUFFICIENT_MATERIALS: "재료가 부족합니다.",
  MARKET_NO_FILL: "구매 가능한 매도 주문이 없습니다.",
  MARKET_ORDER_NOT_ACTIVE: "현재 구매할 수 없는 주문입니다.",
  INVALID_UNIT_PRICE: "최대 단가는 10~999,999쌀 사이의 정수로 입력해 주세요.",
};

function primaryStat(stats: EquipmentStatSummary): { label: string; value: number } {
  if (stats.attack !== 0) return { label: "공격력", value: stats.attack };
  if (stats.maxHp !== 0) return { label: "최대 HP", value: stats.maxHp };
  return { label: "방어 관통", value: stats.penetration };
}

function statIncrease(action: EquipmentActionSummary | null): number {
  if (!action) return 0;
  return action.statIncrease.attack || action.statIncrease.maxHp || action.statIncrease.penetration;
}

function materialKind(material: EquipmentMaterialCost): MaterialKind | null {
  const id = material.itemId.toLowerCase();
  if (id.includes("sweet")) return "sweet_potato";
  if (id.includes("potato")) return "potato";
  if (id.includes("corn")) return "corn";
  return null;
}

function materialRank(itemId: string): MaterialRank {
  return (itemId.match(/M[1-5]$/)?.[0] ?? "M1") as MaterialRank;
}

export function equipmentMaterialDisplayName(material: Pick<EquipmentMaterialCost, "itemId" | "displayName">): string {
  const kind = materialKind({ ...material, requiredQuantity: 0, availableQuantity: 0 });
  return kind ? MATERIAL_DISPLAY_NAMES[kind][materialRank(material.itemId)] : material.displayName;
}

function activeCost(summary: EquipmentSlotSummary): EquipmentCost | null {
  if (!summary.unlocked) return summary.unlock?.cost ?? null;
  if (summary.promote?.maxEnhancementReached) return summary.promote.cost;
  return summary.enhance?.cost ?? summary.promote?.cost ?? null;
}

export function shortagePurchaseQuantity(material: EquipmentMaterialCost): number {
  return Math.max(material.requiredQuantity - material.availableQuantity, 0);
}

type EquipmentPreviewCommandResult = { state: EquipmentState; powerIncrease: number };
type EquipmentPreviewCommand = (command: EquipmentCommand, currentState: EquipmentState) => EquipmentPreviewCommandResult;

export function EquipmentWindow({ open, onClose, previewData, previewCommand }: { open: boolean; onClose: () => void; previewData?: EquipmentState; previewCommand?: EquipmentPreviewCommand }) {
  const dialog = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    const element = dialog.current;
    if (!element) return;
    if (open && !element.open) element.showModal();
    if (!open && element.open) element.close();
  }, [open]);

  return <dialog
    ref={dialog}
    className="equipment-window"
    aria-labelledby="equipment-title"
    onCancel={event => {
      event.preventDefault();
      onClose();
    }}
    onMouseDown={event => {
      if (event.currentTarget === event.target) onClose();
    }}
  >
    <EquipmentScreen onClose={onClose} previewData={previewData} previewCommand={previewCommand} />
  </dialog>;
}

function Resource({ icon, label, value, short = false, muted = false }: { icon: string; label: string; value: number; short?: boolean; muted?: boolean }) {
  return <span className={`equipment-resource ${short ? "short" : ""} ${muted ? "muted" : ""}`} title={`${label} ${value.toLocaleString()}`}>
    <img src={icon} alt="" aria-hidden="true" /><span className="sr-only">{label}</span><strong>{value.toLocaleString()}</strong>
  </span>;
}

function actionButtonLabel(action: { executable: boolean; disabledReason: string | null }): string {
  return action.executable ? "실행" : ERROR_LABELS[action.disabledReason ?? ""] ?? "조건 미충족";
}

function EquipmentSlotCard({ summary, selected, cosmetic, onSelect }: { summary: EquipmentSlotSummary; selected: boolean; cosmetic?: WornCosmetic | null; onSelect: () => void }) {
  const accessibleName = `${summary.slotName} ${summary.current.gradeName ?? "미해금"} +${summary.current.enhancementLevel}${selected ? " 선택됨" : ""}`;
  return <button type="button" className={`equipment-slot-card equipment-slot-${summary.slot.toLowerCase()} ${selected ? "selected" : ""} ${summary.growthComplete ? "complete" : ""}`} aria-label={accessibleName} aria-pressed={selected} onClick={onSelect}>
    {selected && <span className="equipment-slot-selection" aria-hidden="true"><i /></span>}
    <strong>{summary.slotName}</strong>
    <img className={cosmetic?.art ? "is-cosmetic" : undefined} src={cosmetic?.art ?? SLOT_ICONS[summary.slot]} alt="" aria-hidden="true" />
    <span><span className={`equipment-grade equipment-grade-${summary.current.grade?.toLowerCase() ?? "locked"}`}>{summary.current.gradeName ?? "미해금"}</span><b>+{summary.current.enhancementLevel}</b></span>
  </button>;
}

function EquipmentMaterialRow({ summary, material, busy, onPurchase }: { summary: EquipmentSlotSummary; material: EquipmentMaterialCost; busy: boolean; onPurchase: (slot: EquipmentSlotSummary, material: EquipmentMaterialCost) => void }) {
  const kind = materialKind(material);
  const rank = materialRank(material.itemId);
  const rankName = MATERIAL_RANKS.find(entry => entry.id === rank)?.rank ?? rank;
  const short = material.availableQuantity < material.requiredQuantity;
  return <div className={`equipment-material-row ${short ? "short" : "complete"}`} role="listitem">
    <img src={kind ? MATERIAL_RANK_ICONS[kind][rank] : potatoIcon} alt="" aria-hidden="true" />
    <div className="equipment-material-name"><strong>{equipmentMaterialDisplayName(material)}</strong><span>{rankName}</span></div>
    <div className="equipment-material-quantity"><strong>{material.availableQuantity.toLocaleString()}</strong><span>/ {material.requiredQuantity.toLocaleString()}</span></div>
    {short ? <button type="button" className="equipment-material-market" aria-label={`${equipmentMaterialDisplayName(material)} 거래소에서 구매`} title="거래소 구매" disabled={busy} onClick={() => onPurchase(summary, material)}><img src={navMarketIcon} alt="" aria-hidden="true" /></button> : <span className="equipment-material-check" aria-label="재료 충족">✓</span>}
  </div>;
}

function DraggableRequirementList({ children }: { children: ReactNode }) {
  const drag = useRef({ active: false, didDrag: false, pointerId: -1, startY: 0, startScrollTop: 0 });

  const endDrag = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (!drag.current.active || drag.current.pointerId !== event.pointerId) return;
    drag.current.active = false;
    event.currentTarget.classList.remove("dragging");
    if (event.currentTarget.hasPointerCapture(event.pointerId)) event.currentTarget.releasePointerCapture(event.pointerId);
    window.setTimeout(() => { drag.current.didDrag = false; }, 0);
  };

  return <div
    className="equipment-requirement-list"
    role="list"
    tabIndex={0}
    aria-label="강화 필요 재료. 항목이 많으면 위아래로 드래그할 수 있습니다."
    onPointerDown={event => {
      if (event.pointerType !== "mouse" || event.button !== 0 || (event.target as HTMLElement).closest("button")) return;
      drag.current = { active: true, didDrag: false, pointerId: event.pointerId, startY: event.clientY, startScrollTop: event.currentTarget.scrollTop };
      event.currentTarget.setPointerCapture(event.pointerId);
      event.currentTarget.classList.add("dragging");
    }}
    onPointerMove={event => {
      if (!drag.current.active || drag.current.pointerId !== event.pointerId) return;
      const distance = event.clientY - drag.current.startY;
      if (Math.abs(distance) < 3) return;
      drag.current.didDrag = true;
      event.currentTarget.scrollTop = drag.current.startScrollTop - distance;
      event.preventDefault();
    }}
    onPointerUp={endDrag}
    onPointerCancel={endDrag}
    onClickCapture={event => {
      if (!drag.current.didDrag) return;
      event.preventDefault();
      event.stopPropagation();
      drag.current.didDrag = false;
    }}
  >{children}</div>;
}

function EquipmentDetailPanel({ summary, riceBalance, busy, onCommand, onPurchase }: { summary: EquipmentSlotSummary; riceBalance: number; busy: boolean; onCommand: (command: Omit<EquipmentCommand, "key">) => void; onPurchase: (slot: EquipmentSlotSummary, material: EquipmentMaterialCost) => void }) {
  const stat = primaryStat(summary.current.stats);
  const promotionReady = Boolean(summary.promote?.maxEnhancementReached);
  const action = !summary.unlocked ? summary.unlock : promotionReady ? summary.promote : summary.enhance;
  const cost = activeCost(summary);
  const commandKind: EquipmentCommand["kind"] = !summary.unlocked ? "unlock" : promotionReady ? "promote" : "enhance";
  const mainLabel = !summary.unlocked ? "제작" : promotionReady ? "승급" : "강화";
  const nextIncrease = action ? "statIncrease" in action ? statIncrease(action) : primaryStat(action.result.stats).value - stat.value : null;
  const nextStat = action ? primaryStat(action.result.stats) : null;
  return <article className="equipment-detail-panel" aria-label={`${summary.slotName} 상세`}>
    <div className="equipment-detail-main">
      <div className="equipment-detail-body">
        <div className="equipment-upgrade-preview">
          <section><span>현재 상태</span><strong>{summary.current.gradeName ?? "미해금"} +{summary.current.enhancementLevel}</strong><small>{stat.label} +{stat.value.toLocaleString()}</small></section>
          <i aria-hidden="true">&gt;</i>
          <section className="next"><span>{promotionReady ? "다음 승급" : "다음 강화"}</span>{action ? <><strong>{action.result.gradeName ?? "미해금"} +{action.result.enhancementLevel}</strong><small>{nextStat?.label} +{nextStat?.value.toLocaleString()}{nextIncrease !== null && nextIncrease > 0 && <span className="equipment-stat-delta" aria-label={`증가 ${nextIncrease.toLocaleString()}`}> (<i aria-hidden="true" />+{nextIncrease.toLocaleString()})</span>}</small></> : <strong>성장 완료</strong>}</section>
        </div>
        <section className="equipment-material-section">
          <div className="equipment-material-heading">
            <img className="equipment-material-heading-check" src={sectionCheckIcon} alt="" aria-hidden="true" />
            <h4>필요 재료</h4>
            <img className="equipment-material-heading-accent" src={accentRaysCoral} alt="" aria-hidden="true" />
          </div>
          {cost ? <DraggableRequirementList>
            {cost.materials.map(material => <EquipmentMaterialRow key={material.itemId} summary={summary} material={material} busy={busy} onPurchase={onPurchase} />)}
          </DraggableRequirementList> : <p className="equipment-material-complete">필요한 강화 재료가 없습니다.</p>}
        </section>
      </div>
    </div>
    <footer className="equipment-detail-actions">
      {cost ? <div className={`equipment-action-rice ${riceBalance < cost.riceCost ? "short" : ""}`} aria-label={`필요 쌀. 현재 보유 ${riceBalance.toLocaleString()}, 필요 ${cost.riceCost.toLocaleString()}`}><img src={riceGoldIcon} alt="" aria-hidden="true" /><span>필요 쌀</span><strong>{riceBalance.toLocaleString()} <i>/</i> {cost.riceCost.toLocaleString()}</strong></div> : <small>{action && !action.executable ? actionButtonLabel(action) : "최대 강화에 도달했습니다."}</small>}
      <button type="button" className={promotionReady ? "equipment-promote-button" : "equipment-enhance-button"} disabled={!action?.executable || busy} title={action && !action.executable ? actionButtonLabel(action) : undefined} onClick={() => onCommand({ kind: commandKind, slot: summary.slot })}>{summary.growthComplete ? "최대" : mainLabel}</button>
    </footer>
  </article>;
}

function EquipmentFeedbackDialog({ message, onConfirm, onRetry, retryLabel, busy = false }: { message: string; onConfirm: () => void; onRetry?: () => void; retryLabel?: string; busy?: boolean }) {
  if (!onRetry) return <NoticeToast message={message} onDone={onConfirm} duration={2200} />;
  return <TopAlert message={message} action={<>
    <button type="button" disabled={busy} onClick={onRetry}>{retryLabel ?? "같은 요청 다시 확인"}</button>
    <button type="button" onClick={onConfirm}>확인</button>
  </>} />;
}

export function EquipmentScreen({ onClose, previewData, previewCommand }: { onClose?: () => void; previewData?: EquipmentState; previewCommand?: EquipmentPreviewCommand }) {
  const queryClient = useQueryClient();
  const [message, setMessage] = useState<string | null>(null);
  const [lastCommand, setLastCommand] = useState<EquipmentCommand | null>(null);
  const [purchaseTarget, setPurchaseTarget] = useState<{ slot: EquipmentSlotSummary; material: EquipmentMaterialCost; quantity: number } | null>(null);
  const [maxUnitPrice, setMaxUnitPrice] = useState<NumericFieldValue>("");
  const [lastPurchase, setLastPurchase] = useState<EquipmentMaterialPurchaseCommand | null>(null);
  const [selectedSlot, setSelectedSlot] = useState<EquipmentSlot>("WEAPON");
  const [powerIncrease, setPowerIncrease] = useState<number | null>(null);
  const state = useQuery({
    queryKey: ["equipment"],
    queryFn: equipmentApi.state,
    enabled: previewData === undefined,
    initialData: previewData,
    retry: false,
  });
  const marketInstruments = useQuery({ queryKey: ["market-instruments"], queryFn: equipmentApi.marketInstruments, retry: false });
  const { equipment: cosmeticEquipment, catalog: cosmeticCatalog } = useOwnCosmeticAppearance(previewData === undefined);
  const purchaseInstrument = purchaseTarget ? marketInstruments.data?.find(instrument => instrument.itemId === purchaseTarget.material.itemId) ?? null : null;
  const deferredPurchaseQuantity = useDeferredValue(purchaseTarget?.quantity ?? 0);
  const quote = useQuery({ queryKey: ["equipment-market-quote", purchaseInstrument?.instrumentId, deferredPurchaseQuantity, marketUnitPrice(maxUnitPrice)], queryFn: () => equipmentApi.purchaseQuote(purchaseInstrument!.instrumentId, deferredPurchaseQuantity, marketUnitPrice(maxUnitPrice)!), enabled: purchaseInstrument !== null && deferredPurchaseQuantity > 0 && marketUnitPrice(maxUnitPrice) !== null, retry: false, placeholderData: previous => previous });
  useEffect(() => {
    const lowestPrice = purchaseInstrument?.bestAskUnitPrice;
    if (lowestPrice == null) return;
    setMaxUnitPrice(current => current === "" ? Math.min(lowestPrice, MAX_MARKET_UNIT_PRICE) : current);
  }, [purchaseInstrument?.bestAskUnitPrice, purchaseTarget?.material.itemId]);

  const action = useMutation({
    mutationFn: async (command: EquipmentCommand) => {
      if (previewData && previewCommand) {
        const currentState = queryClient.getQueryData<EquipmentState>(["equipment"]) ?? previewData;
        const previewResult = previewCommand(command, currentState);
        const slot = previewResult.state.slots.find(summary => summary.slot === command.slot);
        if (!slot) throw new Error("PREVIEW_EQUIPMENT_SLOT_NOT_FOUND");
        return { result: { slot, state: previewResult.state }, beforePower: 0, afterPower: previewResult.powerIncrease };
      }
      const beforePower = await rankingApi.combatPower(1).then(view => view.myEntry?.combatPower ?? null).catch(() => null);
      const result = await equipmentApi.command(command);
      const afterPower = await rankingApi.combatPower(1).then(view => view.myEntry?.combatPower ?? null).catch(() => null);
      return { result, beforePower, afterPower };
    }, retry: false,
    onSuccess: async ({ result, beforePower, afterPower }) => {
      setMessage(null);
      if (beforePower !== null && afterPower !== null && afterPower > beforePower) setPowerIncrease(afterPower - beforePower);
      queryClient.setQueryData<EquipmentState>(["equipment"], result.state);
      /* 이어서 누를 수 있어야 한다. 곁다리 갱신까지 기다리면 한 번 누를 때마다
           서버를 네 번 다녀오는 동안 단추가 잠겼다. 갱신은 뒤에서 따라오게 둔다. */
      void Promise.all([queryClient.invalidateQueries({ queryKey: ["inventory"] }), queryClient.invalidateQueries({ queryKey: ["auth", "session"] }), queryClient.invalidateQueries({ queryKey: ["character-stats"] })]);
    },
    onError: error => {
      if (!isUncertainEquipmentCommandError(error)) void state.refetch();
      setMessage(isUncertainEquipmentCommandError(error) ? "변경 결과를 확인하지 못했습니다. 같은 요청으로 다시 확인해 주세요." : ERROR_LABELS[error instanceof Error ? error.message : ""] ?? "장비 명령을 처리하지 못했습니다.");
    },
  });
  useEffect(() => {
    if (powerIncrease === null) return;
    const timeout = window.setTimeout(() => setPowerIncrease(null), 2_200);
    return () => window.clearTimeout(timeout);
  }, [powerIncrease]);
  const purchase = useMutation({
    mutationFn: (command: EquipmentMaterialPurchaseCommand) => equipmentApi.purchaseMaterial(command), retry: false,
    onSuccess: async ({ result }) => {
      const partial = result.remainingQuantity > 0 ? `, ${result.remainingQuantity.toLocaleString()}개는 아직 부족합니다` : "";
      setMessage(`${purchaseTarget ? equipmentMaterialDisplayName(purchaseTarget.material) : "재료"} ${result.filledQuantity.toLocaleString()}개를 ${result.totalPrice.toLocaleString()}쌀에 구매했습니다${partial}.`);
      setPurchaseTarget(null);
      await Promise.all([state.refetch(), queryClient.invalidateQueries({ queryKey: ["equipment-market-quote"] }), queryClient.invalidateQueries({ queryKey: ["inventory"] }), queryClient.invalidateQueries({ queryKey: ["market-materials"] }), queryClient.invalidateQueries({ queryKey: ["auth", "session"] })]);
    },
    onError: error => setMessage(isUncertainEquipmentCommandError(error) ? "구매 결과를 확인하지 못했습니다. 같은 요청으로 다시 확인해 주세요." : ERROR_LABELS[error instanceof Error ? error.message : ""] ?? "재료 구매를 처리하지 못했습니다."),
  });

  const openPurchase = (slot: EquipmentSlotSummary, material: EquipmentMaterialCost) => {
    const instrument = marketInstruments.data?.find(entry => entry.itemId === material.itemId) ?? null;
    // 살 것이 없으면 빈 구매 창 대신 가운데 알림으로 끝낸다. 예전에는 창이 내용 없이 떠서
    // 아무것도 뜨지 않은 것처럼 보였다.
    if (marketInstruments.data !== undefined && (instrument === null || instrument.bestAskUnitPrice == null)) {
      setPurchaseTarget(null); setLastPurchase(null);
      setMessage(equipmentMaterialDisplayName(material) + "은(는) 지금 거래소에 올라온 매물이 없습니다.");
      return;
    }
    setMaxUnitPrice("");
    setPurchaseTarget({ slot, material, quantity: shortagePurchaseQuantity(material) });
    setLastPurchase(null); setMessage(null);
  };
  const confirmPurchase = () => {
    if (!purchaseTarget || purchase.isPending || (purchase.isError && isUncertainEquipmentCommandError(purchase.error))) return;
    const price = marketUnitPrice(maxUnitPrice);
    const lowestPrice = purchaseInstrument?.bestAskUnitPrice;
    if (price === null || lowestPrice == null || price < lowestPrice) return;
    const command = { instrumentId: purchaseInstrument!.instrumentId, quantity: purchaseTarget.quantity, maxUnitPrice: price, key: crypto.randomUUID() };
    setLastPurchase(command); purchase.mutate(command);
  };
  const updatePurchaseQuantity = (quantity: number) => {
    const nextQuantity = Math.max(Math.trunc(quantity) || 1, 1);
    setPurchaseTarget(current => current ? { ...current, quantity: nextQuantity } : current);
  };
  const send = (base: Omit<EquipmentCommand, "key">) => {
    if (action.isPending || (action.isError && isUncertainEquipmentCommandError(action.error))) return;
    const command = { ...base, key: crypto.randomUUID() } as EquipmentCommand;
    setPowerIncrease(null);
    setLastCommand(command); action.mutate(command);
  };

  const data = state.data;
  const orderedSlots = data ? SLOT_ORDER.map(slot => data.slots.find(item => item.slot === slot)).filter((summary): summary is EquipmentSlotSummary => Boolean(summary)) : [];
  const selectedSummary = orderedSlots.find(summary => summary.slot === selectedSlot) ?? orderedSlots[0] ?? null;
  const validMaxUnitPrice = marketUnitPrice(maxUnitPrice);
  const lowestPurchasableUnitPrice = purchaseInstrument?.bestAskUnitPrice ?? null;
  const canPurchase = purchaseTarget !== null && purchaseInstrument !== null && lowestPurchasableUnitPrice !== null && validMaxUnitPrice !== null && validMaxUnitPrice >= lowestPurchasableUnitPrice;
  const targetKind = purchaseTarget ? materialKind(purchaseTarget.material) : null;
  const targetIcon = purchaseTarget && targetKind ? MATERIAL_RANK_ICONS[targetKind][materialRank(purchaseTarget.material.itemId)] : potatoIcon;
  const shortageQuantity = purchaseTarget ? shortagePurchaseQuantity(purchaseTarget.material) : 0;
  const estimatedPurchaseTotal = purchaseTarget && lowestPurchasableUnitPrice !== null ? purchaseTarget.quantity * lowestPurchasableUnitPrice : 0;

  return <section className="equipment-screen" aria-labelledby="equipment-title">
    <div className="equipment-paper">
      <header className="equipment-heading">
        <div className="equipment-title"><img src={equipmentTitleIcon} alt="" aria-hidden="true" /><h2 id="equipment-title">장비</h2></div>
        {onClose && <button type="button" className="paper-close" aria-label="장비 화면 닫기" onClick={onClose}><img src={closeIcon} alt="" aria-hidden="true" /></button>}
      </header>
      {state.isLoading && <div className="equipment-message" aria-live="polite">장비 상태를 불러오는 중입니다.</div>}
      {state.error && !data && <div className="equipment-message error" role="alert"><strong>장비 상태를 불러오지 못했습니다.</strong><button onClick={() => state.refetch()}>다시 시도</button></div>}
      {powerIncrease !== null && <output className="equipment-power-toast" aria-live="polite" aria-label={`전투력 ${powerIncrease.toLocaleString()} 상승`}><span>전투력</span><strong>+{powerIncrease.toLocaleString()}</strong><i aria-hidden="true" /></output>}
      {data && selectedSummary && <div className="equipment-layout">
        <div className="equipment-loadout-stage" aria-label="장비 부위 선택"><img className="equipment-loadout-backdrop" src={loadoutCourtyard} alt="" aria-hidden="true" /><CosmeticHero className="equipment-loadout-hero" equipment={cosmeticEquipment} catalog={cosmeticCatalog} alt="착용한 치장을 입은 젓가락 캐릭터" fallback={<img className="equipment-loadout-hero" src={characterRest} alt="기본 나무검을 든 젓가락 캐릭터" />} />{orderedSlots.map(summary => <EquipmentSlotCard key={summary.slot} summary={summary} selected={summary.slot === selectedSummary.slot} cosmetic={wornCosmetic(summary.slot, cosmeticEquipment, cosmeticCatalog)} onSelect={() => setSelectedSlot(summary.slot)} />)}</div>
        <EquipmentDetailPanel summary={selectedSummary} riceBalance={data.riceBalance} busy={action.isPending || purchase.isPending} onCommand={send} onPurchase={openPurchase} />
      </div>}
    </div>

    {purchaseTarget && <div className="equipment-market-backdrop" role="presentation" onMouseDown={event => { if (event.currentTarget === event.target) setPurchaseTarget(null); }}>
      <section className="equipment-purchase-confirm" role="dialog" aria-modal="true" aria-labelledby="equipment-purchase-title">
        <button type="button" className="equipment-modal-close" aria-label="거래소 닫기" disabled={purchase.isPending} onClick={() => setPurchaseTarget(null)}>×</button>
        <header className="equipment-purchase-heading"><img src={marketIcon} alt="" aria-hidden="true" /><h3 id="equipment-purchase-title">거래소</h3></header>
        <div className="equipment-purchase-item"><img src={targetIcon} alt="" aria-hidden="true" /><strong>{equipmentMaterialDisplayName(purchaseTarget.material)}</strong></div>
        {quote.isLoading && <p className="equipment-quote-message">현재 거래소 가격을 확인하는 중입니다.</p>}
        {quote.error && <p className="unmet" role="alert">구매 견적을 불러오지 못했습니다. <button type="button" onClick={() => quote.refetch()}>다시 조회</button></p>}
        {quote.data && <>
          <dl className="equipment-purchase-quote"><div><dt>현재 보유</dt><dd>{purchaseTarget.material.availableQuantity.toLocaleString()}</dd></div><div><dt>필요 수량</dt><dd>{purchaseTarget.material.requiredQuantity.toLocaleString()}</dd></div><div><dt>부족 수량</dt><dd className="shortage">{Math.max(purchaseTarget.material.requiredQuantity - purchaseTarget.material.availableQuantity, 0).toLocaleString()}</dd></div></dl>
          <div className="equipment-current-price"><img src={riceGoldIcon} alt="" aria-hidden="true" /><span>현재 최저가</span><strong>{(quote.data.lowestFilledUnitPrice ?? 0).toLocaleString()} 쌀</strong></div>
          <div className="equipment-purchase-quantity"><strong>구매 수량</strong><div className="equipment-quantity-stepper"><button type="button" aria-label="구매 수량 줄이기" onClick={() => updatePurchaseQuantity(purchaseTarget.quantity - 1)}>−</button><input aria-label="구매 수량" type="text" inputMode="numeric" pattern="[0-9]*" value={purchaseTarget.quantity} onChange={event => updatePurchaseQuantity(Number(event.target.value.replace(/[^0-9]/g, "")))} onFocus={event => event.currentTarget.select()} /><button type="button" aria-label="구매 수량 늘리기" onClick={() => updatePurchaseQuantity(purchaseTarget.quantity + 1)}>+</button></div><button type="button" className="equipment-fill-shortage" onClick={() => updatePurchaseQuantity(shortageQuantity)}>부족분 채우기</button></div>
          {quote.data.lowestFilledUnitPrice == null && <p className="unmet">구매할 수 있는 다른 사용자의 매도 주문이 없습니다.</p>}
          {validMaxUnitPrice !== null && quote.data.lowestFilledUnitPrice != null && validMaxUnitPrice < quote.data.lowestFilledUnitPrice && <p className="unmet">현재 최저가보다 낮은 금액입니다.</p>}
          <p className="equipment-max-spend"><span>총 구매 금액</span><strong><img src={riceGoldIcon} alt="" aria-hidden="true" />{estimatedPurchaseTotal.toLocaleString()}</strong></p>
          <div className="equipment-purchase-actions"><button type="button" className="equipment-purchase-cancel" disabled={purchase.isPending} onClick={() => setPurchaseTarget(null)}>취소</button><button type="button" className="equipment-purchase-button" disabled={!canPurchase || purchase.isPending} onClick={confirmPurchase}>구매</button></div>
        </>}
      </section>
    </div>}
    {message && <EquipmentFeedbackDialog
      message={message}
      onConfirm={() => setMessage(null)}
      onRetry={action.isError && isUncertainEquipmentCommandError(action.error) && lastCommand ? () => action.mutate(lastCommand) : purchase.isError && isUncertainEquipmentCommandError(purchase.error) && lastPurchase ? () => purchase.mutate(lastPurchase) : undefined}
      retryLabel={purchase.isError && isUncertainEquipmentCommandError(purchase.error) ? "같은 구매 다시 확인" : undefined}
      busy={action.isPending || purchase.isPending}
    />}
  </section>;
}
