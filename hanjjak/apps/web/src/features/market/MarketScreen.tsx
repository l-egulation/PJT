import { useEffect, useMemo, useRef, useState, type Dispatch, type SetStateAction } from "react";
import { createChart, HistogramSeries, LineSeries, ColorType, CrosshairMode, type HistogramData, type IChartApi, type ISeriesApi, type LineData, type MouseEventParams, type Time } from "lightweight-charts";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { authApi } from "../auth/api";
import { inventoryApi } from "../inventory/api";
import riceIcon from "../equipment/assets-cozy-pixel/currency-rice-gold-256.png";
import skillbookIcon from "../../shared/assets/cozy-hud-v1/icons/reward-skillbook.png";
import gemIcon from "../../shared/assets/cozy-hud-v1/icons/bottom-gems.png";
import potatoFragment from "../equipment/assets/material-ranks/potato-fragment-f.png";
import potatoMini from "../equipment/assets/material-ranks/potato-mini-d.png";
import potato from "../equipment/assets/material-ranks/potato-c.png";
import potatoGolden from "../equipment/assets/material-ranks/potato-golden-b.png";
import potatoLegendary from "../equipment/assets/material-ranks/potato-legendary-a.png";
import sweetFragment from "../equipment/assets/material-ranks/sweet-potato-fragment-f.png";
import sweetMini from "../equipment/assets/material-ranks/sweet-potato-mini-d.png";
import sweetPotato from "../equipment/assets/material-ranks/sweet-potato-c.png";
import sweetGolden from "../equipment/assets/material-ranks/sweet-potato-golden-b.png";
import sweetLegendary from "../equipment/assets/material-ranks/sweet-potato-legendary-a.png";
import cornKernel from "../equipment/assets/material-ranks/corn-kernel-f.png";
import cornMini from "../equipment/assets/material-ranks/corn-mini-d.png";
import corn from "../equipment/assets/material-ranks/corn-c.png";
import cornGolden from "../equipment/assets/material-ranks/corn-golden-b.png";
import cornLegendary from "../equipment/assets/material-ranks/corn-legendary-a.png";
import marketWindowIcon from "./market-window-icon.png";
import paperCloseIcon from "../character/assets-cozy-pixel/close-icon-32.png";
import { marketApi, type MarketDepthLevel, type MarketInstrument, type MarketOrderBook, type MarketOrderQuote, type MarketOrderSide, type MarketSummary, type MarketTimeInForce } from "./api";
import { DeliveryPanel, OrderManagement, SettlementPanel, TradeList } from "./MarketManagement";
import { marketErrorMessage } from "./marketErrors";
import { MAX_MARKET_UNIT_PRICE, marketUnitPrice, numericInputValue, positiveInteger, type NumericFieldValue } from "./marketForm";
import { valueText } from "../gems/GemManagement";
import { gemsApi } from "../gems/api";
import { PriceLevelGuide } from "./PriceLevelGuide";
import { GemGlyph } from "../gems/GemManagement";
import { gemIdentityFromItemId } from "../gems/gemIdentity";
import { SkillArtwork } from "../skills/SkillsScreen";
import "./MarketScreen.css";
import { MARKET_UI_STORAGE_KEY, marketCategoryForItemId, readMarketUiState } from "./marketUiState";
import { formatMarketDateTime } from "./marketDateTime";

export type MarketMode = "buy" | "sell" | "history";
type MarketView = "simple" | "book";
type MarketSection = "trade" | "orders" | "deliveries" | "settlements" | "history";
type CategoryId = "ALL" | "MATERIAL" | "SKILL_BOOK" | "GEM";
type MaterialFamily = "POTATO" | "SWEET_POTATO" | "CORN";
type MarketMessage = { kind: "success" | "error"; text: string };
type TradeConfirmation = { request: Parameters<typeof marketApi.createOrder>[0]; quote: MarketOrderQuote; idempotencyKey: string };
export type FilterState = { category: CategoryId; query: string };
export type PriceTrendPeriod = "12h" | "1d" | "2d";
const PRICE_TREND_PERIODS: ReadonlyArray<readonly [PriceTrendPeriod, string]> = [["12h", "12시간"], ["1d", "1일"], ["2d", "2일"]];
/* 기간 하나를 고르면 봉 간격이 따라온다. 짧게 볼수록 촘촘한 봉을 쓴다. */
const PRICE_TREND_RANGES = [
  { period: "12h", interval: "5m", label: "12시간" },
  { period: "1d", interval: "15m", label: "1일" },
  { period: "2d", interval: "30m", label: "2일" },
] as const;
const ORDER_BOOK_DEPTH = 20;

export function nextPriceTrendPeriod(period: PriceTrendPeriod): PriceTrendPeriod | null {
  const index = PRICE_TREND_PERIODS.findIndex(([value]) => value === period);
  return PRICE_TREND_PERIODS[index + 1]?.[0] ?? null;
}

const DEFAULT_FILTERS: FilterState = { category: "MATERIAL", query: "" };
/* 전체는 분류가 아니라 "분류를 걸지 않음"이다. 어떤 품목도 이 분류를 갖지 않는다. */
const CATEGORY_LABELS: Record<CategoryId, string> = { ALL: "전체", MATERIAL: "강화재료", SKILL_BOOK: "스킬북", GEM: "보석" };
const FAMILIES: Array<{ id: MaterialFamily; label: string }> = [{ id: "POTATO", label: "감자" }, { id: "SWEET_POTATO", label: "고구마" }, { id: "CORN", label: "옥수수" }];
const BOOK_GRADES: Record<string, string> = { normal: "노말", rare: "희귀", epic: "영웅", legendary: "전설" };
const MATERIAL_GRADES = ["F", "D", "C", "B", "A"];
const MATERIAL_ART: Record<string, string[]> = {
  POTATO: [potatoFragment, potatoMini, potato, potatoGolden, potatoLegendary],
  SWEET_POTATO: [sweetFragment, sweetMini, sweetPotato, sweetGolden, sweetLegendary],
  CORN: [cornKernel, cornMini, corn, cornGolden, cornLegendary],
};
const SECTIONS: Array<{ id: MarketSection; label: string }> = [
  { id: "trade", label: "거래" }, { id: "orders", label: "내 주문" },
  { id: "deliveries", label: "물품 수령함" }, { id: "settlements", label: "판매 정산" }, { id: "history", label: "체결 내역" },
];
export const MARKET_QUERY_KEYS = [["market-instruments"], ["market-order-book"], ["market-price-levels"], ["market-order-quote"], ["market-orders"], ["market-deliveries"], ["market-summary"], ["market-trades"], ["market-mails"], ["inventory"], ["gems"], ["auth", "session"]] as const;

export function maximumAffordableQuantity(levels: MarketDepthLevel[], maxUnitPrice: number, rice: number): number {
  let budget = rice, quantity = 0;
  for (const level of levels) {
    if (level.unitPrice > maxUnitPrice || budget < level.unitPrice) continue;
    const take = Math.max(0, Math.min(level.totalQuantity - level.myQuantity, Math.floor(budget / level.unitPrice)));
    quantity += take;
    budget -= take * level.unitPrice;
  }
  return quantity;
}

export function filterMarketItems(items: MarketInstrument[], filters: FilterState): MarketInstrument[] {
  const words = filters.query.trim().toLowerCase().split(/\s+/).filter(Boolean);
  return items.filter(item => {
    if (!words.length) return filters.category === "ALL" || item.category === filters.category;
    const searchable = `${item.displayName} ${item.canonicalKey} ${itemCategory(item)}`.toLowerCase();
    return words.every(word => searchable.includes(word));
  });
}
function formatPrice(value: number | null | undefined) { return value == null ? "—" : `${value.toLocaleString()}쌀`; }
export const MARKET_ACCENT_LINE = "#C8323F";
export const MARKET_RISE_COLOR = "#D84A3A";
export const MARKET_FALL_COLOR = "#3976C5";
export const MARKET_FLAT_COLOR = "#806956";
export function marketDirectionColor(open: number, close: number): string {
  return close > open ? MARKET_RISE_COLOR : close < open ? MARKET_FALL_COLOR : MARKET_FLAT_COLOR;
}
export function priceTrendCrosshairTimeText(time: Time): string {
  const date = typeof time === "number"
    ? new Date(time * 1_000)
    : typeof time === "string"
      ? new Date(`${time}T00:00:00`)
      : new Date(time.year, time.month - 1, time.day);
  return formatMarketDateTime(date);
}


/**
 * 품목 그림. 보석은 등급 모양과 옵션 색이 다르고, 스킬북은 어떤 스킬인지가 보여야
 * 한다. 예전에는 셋 다 같은 그림 한 장이었다.
 */
export function InstrumentIcon({ item }: { item: MarketInstrument }) {
  const gem = gemIdentityFromItemId(item.itemId);
  if (gem) return <GemGlyph level={gem.level} option={gem.option} />;
  if (item.category === "SKILL_BOOK") {
    const skillId = item.attributes.skillId ?? item.itemId.split(":")[1] ?? "";
    return <span className="market-skillbook-icon"><img src={skillbookIcon} alt="" /><SkillArtwork skillId={skillId} name={item.displayName} compact /></span>;
  }
  return <img src={itemIcon(item)} alt="" />;
}
function itemIcon(item: MarketInstrument) {
  const material = item.itemId.match(/^(POTATO|SWEET_POTATO|CORN)_M([1-5])$/);
  if (material) return MATERIAL_ART[material[1]][Number(material[2]) - 1];
  return item.category === "SKILL_BOOK" ? skillbookIcon : item.category === "GEM" ? gemIcon : marketWindowIcon;
}
function itemCategory(item: MarketInstrument) {
  const material = item.itemId.match(/_M([1-5])$/);
  if (material) return `강화재료 · ${MATERIAL_GRADES[Number(material[1]) - 1]}등급`;
  if (item.category === "GEM") return `보석 · ${itemVariant(item)}`;
  return CATEGORY_LABELS[item.category as CategoryId] ?? item.category;
}

function itemGroup(item: MarketInstrument) {
  if (item.category === "MATERIAL") {
    const family = FAMILIES.find(entry => item.itemId.startsWith(`${entry.id}_M`));
    return { id: family?.id ?? item.itemId, label: family?.label ?? item.displayName };
  }
  if (item.category === "SKILL_BOOK") {
    const grade = BOOK_GRADES[item.attributes.grade];
    const name = grade && item.displayName.startsWith(`${grade} `) ? item.displayName.slice(grade.length + 1) : item.displayName;
    return { id: item.attributes.skillId ?? item.itemId, label: name.replace(/ 비법서$/, "") };
  }
  return { id: item.attributes.option ?? item.itemId, label: item.displayName.replace(/^\d+레벨 /, "").replace(/ 보석$/, "") };
}

function itemVariant(item: MarketInstrument) {
  const material = item.itemId.match(/_M([1-5])$/);
  if (material) return `${MATERIAL_GRADES[Number(material[1]) - 1]}등급`;
  if (item.category === "SKILL_BOOK") return BOOK_GRADES[item.attributes.grade] ?? item.displayName;
  if (item.category === "GEM") {
    const option = item.attributes.option === "attack_speed" ? "HASTE" : item.attributes.option?.toUpperCase();
    const value = Number(item.attributes.value);
    if (option && item.attributes.value != null && Number.isFinite(value)) {
      return `Lv.${item.attributes.level} · ${valueText({ option, value })}`;
    }
  }
  return item.displayName;
}

function variantOrder(item: MarketInstrument) {
  if (item.category === "MATERIAL") return Number(item.itemId.match(/_M([1-5])$/)?.[1] ?? 0);
  if (item.category === "SKILL_BOOK") return Object.keys(BOOK_GRADES).indexOf(item.attributes.grade);
  return Number(item.attributes.level ?? 0);
}
function sectionCount(section: MarketSection, summary: MarketSummary | undefined) {
  if (!summary) return 0;
  if (section === "orders") return summary.activeOrders;
  if (section === "deliveries") return summary.claimableDeliveries;
  if (section === "settlements") return summary.claimableSettlements;
  return section === "history" ? summary.unread.find(item => item.stream === "FILLS")?.unreadCount ?? 0 : 0;
}
function EmptyState({ title, children }: { title: string; children: string }) {
  return <div className="market-empty-state"><img src={marketWindowIcon} alt="" /><h4>{title}</h4><p>{children}</p></div>;
}

export function MarketWindow({ open, onClose, initialMode = "buy" }: { open: boolean; onClose: () => void; initialMode?: MarketMode }) {
  const dialog = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    const element = dialog.current;
    if (!element) return;
    if (open && !element.open) element.showModal();
    if (!open && element.open) element.close();
  }, [open]);
  return <dialog ref={dialog} className="market-window" aria-labelledby="market-title"
    onCancel={event => { event.preventDefault(); onClose(); }}
    onMouseDown={event => { if (event.currentTarget === event.target) onClose(); }}>
    {open && <MarketScreen onClose={onClose} initialMode={initialMode} />}
  </dialog>;
}

export function MarketScreen({ onClose, initialMode = "buy", pollingEnabled = true }: { onClose?: () => void; initialMode?: MarketMode; pollingEnabled?: boolean } = {}) {
  const client = useQueryClient();
  const [initial] = useState(() => readMarketUiState(window.sessionStorage));
  const [view, setView] = useState<MarketView>("simple");
  const [section, setSection] = useState<MarketSection>(() => initialMode === "history" ? "history" : "trade");
  const [filters, setFilters] = useState<FilterState>(() => ({ category: marketCategoryForItemId(initial.selectedItemId).toUpperCase() as CategoryId, query: "" }));
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [side, setSide] = useState<MarketOrderSide>("BUY");
  const [timeInForce, setTimeInForce] = useState<MarketTimeInForce>("IOC");
  const [quantity, setQuantity] = useState<NumericFieldValue>(initialMode === "sell" ? 1 : "");
  const [unitPrice, setUnitPrice] = useState<NumericFieldValue>("");
  const [message, setMessage] = useState<MarketMessage | null>(null);
  const [confirmation, setConfirmation] = useState<TradeConfirmation | null>(null);
  const [checking, setChecking] = useState(false);
  const pollInterval = pollingEnabled ? 3_000 : false;
  const session = useQuery({ queryKey: ["auth", "session"], queryFn: authApi.session, retry: false, refetchInterval: pollInterval });
  const instruments = useQuery({ queryKey: ["market-instruments"], queryFn: marketApi.instruments, refetchInterval: pollInterval });
  const catalog = instruments.data ?? [];
  const selected = catalog.find(item => item.instrumentId === selectedId) ?? catalog.find(item => item.itemId === initial.selectedItemId) ?? catalog.find(item => item.itemId === "POTATO_M1") ?? catalog[0] ?? null;
  const book = useQuery({ queryKey: ["market-order-book", selected?.instrumentId], queryFn: () => marketApi.orderBook(selected!.instrumentId, ORDER_BOOK_DEPTH), enabled: Boolean(selected), refetchInterval: pollInterval });
  const inventory = useQuery({ queryKey: ["inventory", "market", selected?.itemId], queryFn: () => inventoryApi.detail(selected!.itemId), enabled: Boolean(selected), refetchInterval: pollInterval });
  const gems = useQuery({ queryKey: ["gems", "market"], queryFn: gemsApi.state, enabled: selected?.category === "GEM", refetchInterval: pollInterval });
  const saleGems = (gems.data?.gems ?? []).filter(gem => selected?.category === "GEM" && gem.level === Number(selected.attributes.level)
    && gem.option === (selected.attributes.option === "attack_speed" ? "HASTE" : selected.attributes.option?.toUpperCase())
    && gem.value === Number(selected.attributes.value) && !gem.locked && !gem.reservedForSale && gem.equippedPresets.length === 0);
  const availableQuantity = selected?.category === "GEM" ? gems.data ? saleGems.length : undefined : inventory.data?.availableQuantity;
  const quote = useQuery({ queryKey: ["market-order-quote", selected?.instrumentId, side, timeInForce, positiveInteger(quantity), marketUnitPrice(unitPrice)], queryFn: () => marketApi.quote(selected!.instrumentId, side, timeInForce, positiveInteger(quantity)!, marketUnitPrice(unitPrice)!), enabled: Boolean(selected) && positiveInteger(quantity) !== null && marketUnitPrice(unitPrice) !== null && !confirmation, refetchInterval: pollInterval, retry: false });
  const orders = useQuery({ queryKey: ["market-orders"], queryFn: () => marketApi.orders("ACTIVE"), refetchInterval: pollInterval });
  const closedOrders = useQuery({ queryKey: ["market-orders", "closed"], queryFn: () => marketApi.orders("CLOSED"), enabled: section === "orders", refetchInterval: pollInterval });
  const deliveries = useQuery({ queryKey: ["market-deliveries"], queryFn: () => marketApi.deliveries(true), refetchInterval: pollInterval });
  const summary = useQuery({ queryKey: ["market-summary"], queryFn: marketApi.summary, refetchInterval: pollInterval });
  const settlements = useQuery({ queryKey: ["market-mails"], queryFn: () => marketApi.settlementMails(), refetchInterval: pollInterval });
  const trades = useQuery({ queryKey: ["market-trades", selected?.instrumentId], queryFn: () => marketApi.trades([selected!.instrumentId], true), enabled: Boolean(selected) && section === "history", refetchInterval: pollInterval });
  useEffect(() => { if (selected && selected.instrumentId !== selectedId) setSelectedId(selected.instrumentId); }, [selected, selectedId]);
  useEffect(() => { if (selected) window.sessionStorage.setItem(MARKET_UI_STORAGE_KEY, JSON.stringify({ ...initial, selectedItemId: selected.itemId })); }, [initial, selected]);
  useEffect(() => { if (initialMode === "sell") { setSide("SELL"); setTimeInForce("GTC"); } }, [initialMode]);
  useEffect(() => { setConfirmation(null); }, [selected?.instrumentId, side, timeInForce, quantity, unitPrice, view]);
  useEffect(() => {
    setUnitPrice((side === "BUY" ? selected?.bestAskUnitPrice : selected?.bestBidUnitPrice) ?? "");
  }, [selected?.instrumentId, side]);
  const refresh = () => Promise.all(MARKET_QUERY_KEYS.map(key => client.invalidateQueries({ queryKey: key })));
  const confirmChange = (text: string) => { setMessage({ kind: "success", text }); return refresh(); };
  const create = useMutation({
    mutationFn: (confirmed: TradeConfirmation) => marketApi.createOrder(confirmed.request, confirmed.idempotencyKey),
    onSuccess: async ({ result }) => {
      setConfirmation(null);
      const unfilled = result.initialQuantity - result.filledQuantity;
      const remaining = unfilled ? ` · ${unfilled.toLocaleString()}개 ${result.timeInForce === "IOC" ? "거래되지 않고 종료" : "판매/구매 대기"}` : "";
      const prices = result.filledQuantity > 0 ? ` · 단가 ${formatPrice(result.lowestFilledUnitPrice)}~${formatPrice(result.highestFilledUnitPrice)} · 평균 ${formatPrice(result.weightedAverageUnitPrice)}` : "";
      setMessage({ kind: "success", text: `${result.side === "BUY" ? "구매" : "판매"} ${result.filledQuantity.toLocaleString()}개 완료${prices} · 거래 금액 ${formatPrice(result.totalPrice)}${remaining}` });
      await refresh();
    },
    onError: error => setMessage({ kind: "error", text: marketErrorMessage(error, "주문에 실패했습니다.") }),
  });
  const update = useMutation({ mutationFn: ({ orderId, request }: { orderId: string; request: { quantity?: number; limitUnitPrice?: number } }) => marketApi.updateOrder(orderId, request), onSuccess: () => confirmChange("주문을 수정했습니다."), onError: error => setMessage({ kind: "error", text: marketErrorMessage(error, "주문 수정에 실패했습니다.") }) });
  const cancel = useMutation({ mutationFn: marketApi.cancelOrder, onSuccess: () => confirmChange("주문을 취소했습니다. 반환된 쌀 또는 물품 수령함을 확인하세요."), onError: error => setMessage({ kind: "error", text: marketErrorMessage(error, "주문 취소에 실패했습니다.") }) });
  const claim = useMutation({ mutationFn: marketApi.claimDelivery, onSuccess: () => confirmChange("물품을 인벤토리로 수령했습니다."), onError: error => setMessage({ kind: "error", text: marketErrorMessage(error, "물품 수령에 실패했습니다.") }) });
  const claimAll = useMutation({ mutationFn: marketApi.claimAllDeliveries, onSuccess: () => confirmChange("수령 결과를 반영했습니다. 공간이 부족한 물품은 수령함에 남습니다."), onError: error => setMessage({ kind: "error", text: marketErrorMessage(error, "전체 수령에 실패했습니다.") }) });
  const claimSettlement = useMutation({ mutationFn: marketApi.claimSettlement, onSuccess: () => confirmChange("판매 정산금을 수령했습니다."), onError: error => setMessage({ kind: "error", text: marketErrorMessage(error, "정산 수령에 실패했습니다.") }) });
  const claimAllSettlements = useMutation({ mutationFn: marketApi.claimAllSettlements, onSuccess: () => confirmChange("판매 정산 결과를 반영했습니다."), onError: error => setMessage({ kind: "error", text: marketErrorMessage(error, "전체 정산 수령에 실패했습니다.") }) });
  const busy = create.isPending || update.isPending || cancel.isPending || claim.isPending || claimAll.isPending || claimSettlement.isPending || claimAllSettlements.isPending;
  const locked = busy || checking || confirmation !== null;
  const requestConfirmation = async () => {
    const parsedQuantity = positiveInteger(quantity), parsedPrice = marketUnitPrice(unitPrice);
    if (!selected || !parsedQuantity || !parsedPrice || locked) return;
    const request = { instrumentId: selected.instrumentId, side, timeInForce, quantity: parsedQuantity, limitUnitPrice: parsedPrice,
      ...(side === "SELL" && selected.category === "GEM" ? { instanceIds: saleGems.slice(0, parsedQuantity).map(gem => gem.gemId) } : {}) };
    setChecking(true);
    setMessage(null);
    try {
      const current = await marketApi.quote(request.instrumentId, side, timeInForce, parsedQuantity, parsedPrice);
      setConfirmation({ request, quote: current, idempotencyKey: crypto.randomUUID() });
    } catch (error) {
      setMessage({ kind: "error", text: marketErrorMessage(error, "최신 거래 정보를 불러오지 못했습니다.") });
    } finally { setChecking(false); }
  };
  const markSectionRead = async (next: MarketSection) => {
    setSection(next);
    setMessage(null);
    const stream = next === "history" ? "FILLS" : next === "deliveries" ? "DELIVERIES" : next === "settlements" ? "SETTLEMENTS" : null;
    if (!stream) return;
    const response = await (next === "history" ? trades.refetch() : next === "deliveries" ? deliveries.refetch() : settlements.refetch());
    if (response.isError || !response.data?.readThroughSequence) return;
    try {
      await marketApi.markRead(stream, response.data.readThroughSequence);
      await summary.refetch();
    } catch {
      setMessage({ kind: "error", text: "읽음 상태를 저장하지 못했습니다. 알림은 다음 조회 때 다시 표시될 수 있습니다." });
    }
  };
  const sectionQuery = section === "orders" ? orders : section === "deliveries" ? deliveries : section === "settlements" ? settlements : section === "history" ? trades : instruments;
  const sectionError = sectionQuery.error || (section === "orders" ? closedOrders.error : null);
  const sectionLoading = sectionQuery.isLoading || (section === "orders" && closedOrders.isLoading);

  return <section className="market-screen market-order-book-screen" aria-labelledby="market-title"><div className="market-paper">
    <header className="market-heading">
      <div className="market-title"><img src={marketWindowIcon} alt="" /><div><small>필요한 것을 사고, 남는 것을 나누는 곳</small><h2 id="market-title">거래소</h2></div></div>
      <div className="market-heading-actions">
        <div className="market-rice" aria-label="사용 가능 쌀"><img src={riceIcon} alt="" /><span>사용 가능 쌀</span><strong>{session.data?.account?.rice.toLocaleString() ?? "—"}</strong></div>
        <button className="market-refresh" aria-label="거래소 새로고침" title="새로고침" disabled={busy || sectionQuery.isFetching} onClick={() => void refresh()}><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true"><path d="M20 7v5h-5M4 17v-5h5" /><path d="M5.5 7a7.5 7.5 0 0 1 12-1L20 9M4 15l2.5 3a7.5 7.5 0 0 0 12-1" /></svg></button>
      </div>
      {/* 닫기는 줄 안에 끼지 않는다. 다른 창과 같은 자리에 서야 창을 옮겨 다녀도 손이 기억한다. */}
      {onClose && <button type="button" className="paper-close" aria-label="거래소 닫기" onClick={onClose}><img src={paperCloseIcon} alt="" aria-hidden="true" /></button>}
    </header>
    <nav className="market-section-tabs" aria-label="거래소 기능">{SECTIONS.map(entry => {
      const count = sectionCount(entry.id, summary.data);
      const stream = entry.id === "orders" ? "EXPIRATIONS" : entry.id === "deliveries" ? "DELIVERIES" : entry.id === "settlements" ? "SETTLEMENTS" : null;
      const unread = stream ? summary.data?.unread.find(item => item.stream === stream)?.unreadCount ?? 0 : 0;
      return <button key={entry.id} disabled={locked} className={section === entry.id ? "active" : ""} aria-current={section === entry.id ? "page" : undefined} onClick={() => markSectionRead(entry.id)}>{entry.label}{count > 0 && <span className="market-count">{count}</span>}{unread > 0 && <span className="market-unread-dot" aria-label={`읽지 않은 알림 ${unread}건`} />}</button>;
    })}</nav>
    {message && <p className={`market-result ${message.kind}`} role="status">{message.text}<button aria-label="알림 닫기" onClick={() => setMessage(null)}>×</button></p>}
    <div className="market-content">
      {sectionError ? <div className="market-inline-feedback" role="alert"><p>거래 정보를 불러오지 못했습니다.</p><button onClick={() => void refresh()}>다시 불러오기</button></div>
        : sectionLoading ? <div className="market-empty-state" role="status"><p>거래 정보를 불러오는 중입니다…</p></div>
        : <>
          {section === "trade" && <div className="market-unified-layout">
            <fieldset className="market-picker-lock" disabled={locked}><InstrumentPicker items={catalog} selected={selected} filters={filters} setFilters={setFilters} onSelect={setSelectedId} /></fieldset>
            <div className="market-trading-area">
              {selected ? <div className="market-trading-grid">
                <div className="market-market-data">
                  <section className="market-market-frame" aria-label="시장 정보">
                    <TradeModeControl side={side} view={view} locked={locked} setSide={setSide} setTimeInForce={setTimeInForce} setView={setView} />
                    {view === "simple"
                      ? <PriceLevelGuide instrument={selected} itemIconSrc={itemIcon(selected)} side={side} timeInForce={timeInForce} quantity={positiveInteger(quantity)} unitPrice={marketUnitPrice(unitPrice)} quoteRevision={(confirmation?.quote ?? quote.data)?.marketRevision} onPrice={setUnitPrice} disabled={locked} />
                      : book.error ? <div className="market-inline-feedback" role="alert">호가를 불러오지 못했습니다. <button onClick={() => void book.refetch()}>다시 조회</button></div> : book.isLoading ? <p role="status">시세를 확인하고 있습니다…</p> : <fieldset className="market-chart-lock" disabled={locked}><PriceTrendChart instrumentId={selected.instrumentId} /><OrderBookPanel key={selected.instrumentId} book={book.data} onPrice={setUnitPrice} /></fieldset>}
                  </section>
                </div>
                <TradeTicket key={`${selected.instrumentId}:${side}:${view}`} selected={selected} side={side} setSide={setSide} view={view} timeInForce={timeInForce} setTimeInForce={setTimeInForce} quantity={quantity} setQuantity={setQuantity} unitPrice={unitPrice} setUnitPrice={setUnitPrice} availableQuantity={availableQuantity} availableRice={session.data?.account?.rice} quote={confirmation?.quote ?? quote.data} quotePending={checking || (!confirmation && quote.isFetching)} quoteError={!confirmation && quote.error ? marketErrorMessage(quote.error, "견적을 불러오지 못했습니다.") : null} busy={busy} locked={locked} confirmation={confirmation} onCancel={() => setConfirmation(null)} onSubmit={() => confirmation ? create.mutate(confirmation) : void requestConfirmation()} />
              </div> : <EmptyState title="거래할 품목이 없어요">거래소 품목이 등록되면 이곳에서 거래할 수 있어요.</EmptyState>}
            </div>
          </div>}
          {section === "orders" && <OrderManagement active={orders.data?.items ?? []} closed={closedOrders.data?.items ?? []} busy={busy} onUpdate={(orderId, request) => update.mutate({ orderId, request })} onCancel={id => cancel.mutate(id)} />}
          {section === "deliveries" && <DeliveryPanel items={deliveries.data?.items ?? []} busy={busy} onClaim={id => claim.mutate(id)} onClaimAll={() => claimAll.mutate()} />}
          {section === "settlements" && <SettlementPanel items={settlements.data?.items ?? []} busy={busy} onClaim={id => claimSettlement.mutate(id)} onClaimAll={() => claimAllSettlements.mutate()} />}
          {section === "history" && <TradeList trades={trades.data?.items ?? []} selectedName={selected?.displayName} />}
        </>}
    </div>
    <footer className="market-footer"><small>{sectionQuery.isError ? "시세 연결을 확인해 주세요" : sectionQuery.isFetching ? "최신 정보를 확인하는 중" : "3초마다 자동 갱신"}</small><span>표시된 시세는 주문 시점에 달라질 수 있습니다.</span></footer>
  </div></section>;
}

function TradeModeControl({ side, view, locked, setSide, setTimeInForce, setView }: {
  side: MarketOrderSide;
  view: MarketView;
  locked: boolean;
  setSide: (side: MarketOrderSide) => void;
  setTimeInForce: (timeInForce: MarketTimeInForce) => void;
  setView: (view: MarketView) => void;
}) {
  const chooseSide = (next: MarketOrderSide) => {
    setSide(next);
    if (view === "simple") setTimeInForce(next === "BUY" ? "IOC" : "GTC");
  };
  return <div className="market-entry-modes market-side-toggle market-price-guide-side-toggle" role="group" aria-label="거래 방향과 보기">
    <button type="button" disabled={locked} className={side === "BUY" ? "active buy" : ""} aria-pressed={side === "BUY"} onClick={() => chooseSide("BUY")}>구매</button>
    <button type="button" disabled={locked} className={side === "SELL" ? "active sell" : ""} aria-pressed={side === "SELL"} onClick={() => chooseSide("SELL")}>판매</button>
    <label className="market-book-check"><input type="checkbox" disabled={locked} checked={view === "book"} onChange={event => setView(event.currentTarget.checked ? "book" : "simple")} /><span>호가 기반 거래</span></label>
  </div>;
}

/*
 * 접힌 줄은 "무엇 안에 무엇이 몇 개"를 보여 준다. 왼쪽은 묶음 이름, 오른쪽 숫자는
 * 그 안에 든 갈래 수다. 숫자만 덩그러니 있어 뜻을 알 수 없어서 머리글을 붙였다.
 */
const PICKER_COLUMNS: Record<CategoryId, [group: string, variants: string]> = {
  ALL: ["품목", "종류"],
  MATERIAL: ["계열", "등급"],
  SKILL_BOOK: ["스킬", "등급"],
  GEM: ["옵션", "레벨"],
};

/** 품목 하나가 어느 묶음에 드는지. 왼쪽에서 계열을 고르면 가운데도 이 열쇠로 좁힌다. */
export function marketGroupKey(item: MarketInstrument): string {
  return `${item.category}:${itemGroup(item).id}`;
}

export function InstrumentPicker({ items, selected, filters, setFilters, onSelect, onFocusGroup, focusedIds }: {
  items: MarketInstrument[]; selected: MarketInstrument | null; filters: FilterState; setFilters: Dispatch<SetStateAction<FilterState>>; onSelect: (id: string) => void;
  /** 계열 줄을 펼치거나 접을 때 알린다. 펼친 계열만 보여 주는 화면이 이것을 듣는다. */
  onFocusGroup?: (groupKey: string | null) => void;
  /*
   * 여럿을 한꺼번에 골라 둘 수 있는 화면이 쓴다. 주면 눌린 표시가 이 묶음을 따르고,
   * 주지 않으면 예전처럼 거래 중인 하나만 눌린 것으로 보인다.
   */
  focusedIds?: ReadonlySet<string>;
}) {
  const searching = Boolean(filters.query.trim());
  const [expanded, setExpanded] = useState<Record<string, boolean>>({});
  const searchInput = useRef<HTMLInputElement>(null);
  const list = useRef<HTMLDivElement>(null);
  const visible = useMemo(() => filterMarketItems(items, filters), [items, filters]);
  const groups = useMemo(() => {
    const grouped = new Map<string, { key: string; category: string; label: string; items: MarketInstrument[] }>();
    for (const item of visible) {
      const group = itemGroup(item), key = marketGroupKey(item);
      const existing = grouped.get(key);
      if (existing) existing.items.push(item);
      else grouped.set(key, { key, category: item.category, label: group.label, items: [item] });
    }
    for (const group of grouped.values()) group.items.sort((a, b) => variantOrder(a) - variantOrder(b)
      || Number(a.attributes.value ?? 0) - Number(b.attributes.value ?? 0) || a.instrumentId.localeCompare(b.instrumentId));
    return [...grouped.values()].sort((a, b) => {
      const categoryOrder = Object.keys(CATEGORY_LABELS).indexOf(a.category) - Object.keys(CATEGORY_LABELS).indexOf(b.category);
      if (categoryOrder) return categoryOrder;
      if (a.category === "MATERIAL") return FAMILIES.findIndex(f => a.key === `MATERIAL:${f.id}`) - FAMILIES.findIndex(f => b.key === `MATERIAL:${f.id}`);
      return a.label.localeCompare(b.label, "ko");
    });
  }, [visible]);
  useEffect(() => { if (list.current) list.current.scrollTop = 0; }, [filters.category, filters.query]);
  const revealSelected = () => {
    if (!selected) return;
    setFilters({ category: selected.category as CategoryId, query: "" });
    setExpanded(current => ({ ...current, [`${selected.category}:${itemGroup(selected).id}`]: true }));
    requestAnimationFrame(() => list.current?.querySelector('[aria-pressed="true"]')?.scrollIntoView({ block: "nearest" }));
  };
  return <aside className="market-instrument-picker" aria-label="거래 품목">
    <div className="market-picker-heading"><h3>품목 찾기</h3><small aria-live="polite">{searching ? "검색 " : ""}{visible.length}종</small></div>
    <div className="market-search"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true"><circle cx="10" cy="10" r="6" /><path d="m15 15 5 5" /></svg><input ref={searchInput} aria-label="거래 품목 검색" placeholder="전체 품목 검색" value={filters.query} onChange={event => setFilters(current => ({ ...current, query: event.target.value }))} />{filters.query && <button type="button" aria-label="검색어 지우기" onClick={() => { setFilters(current => ({ ...current, query: "" })); searchInput.current?.focus(); }}>×</button>}</div>
    <div className="market-category-tabs" role="group" aria-label="품목 분류">{(Object.keys(CATEGORY_LABELS) as CategoryId[]).map(category => <button key={category} aria-pressed={!searching && filters.category === category} onClick={() => setFilters({ category, query: "" })}>{CATEGORY_LABELS[category]}</button>)}</div>
    {/* 픽셀 글꼴에 › 글리프가 없어 "계열 › 등급"이 "계열  등급"으로 보였다.
        설명 문장 대신 줄과 같은 칸에 맞춘 머리글로 적는다. */}
    <div className="market-picker-hint">{searching
      ? <span>전체 분류에서 찾은 품목</span>
      : <><span>{PICKER_COLUMNS[filters.category][0]}</span><small>{PICKER_COLUMNS[filters.category][1]}</small></>}</div>
    <div className="market-instrument-list" ref={list}>
      {groups.map((group, index) => {
        const containsSelected = group.items.some(item => item.instrumentId === selected?.instrumentId);
        const groupArtwork = group.items.find(item => item.instrumentId === selected?.instrumentId) ?? group.items[0];
        const open = searching || (expanded[group.key] ?? (containsSelected || index === 0));
        const id = `market-group-${group.key.replace(/[^a-zA-Z0-9_-]/g, "-")}`;
        return <section className={`market-instrument-group${containsSelected ? " has-selection" : ""}`} key={group.key}>
          <button type="button" className="market-group-toggle" aria-expanded={open} aria-controls={id} disabled={searching} onClick={() => { setExpanded(current => ({ ...current, [group.key]: !open })); onFocusGroup?.(open ? null : group.key); }}>
            <span className="market-group-art"><InstrumentIcon item={groupArtwork} /></span>
            <span className="market-group-label"><strong>{group.label}</strong>{searching && <small>{CATEGORY_LABELS[group.category as CategoryId]}</small>}</span>
            <small className="market-group-count">{group.items.length}</small>
            <span className="market-group-chevron" aria-hidden="true">{open ? "−" : "+"}</span>
          </button>
          <div id={id} className={`market-variant-grid ${group.category.toLowerCase()}`} hidden={!open}>
            {group.items.map(item => <button type="button" key={item.instrumentId} data-grade={item.attributes.grade ?? undefined} className={item.instrumentId === selected?.instrumentId ? "is-trading" : undefined} aria-pressed={focusedIds ? focusedIds.has(item.instrumentId) : item.instrumentId === selected?.instrumentId} aria-label={`${item.displayName} · ${itemVariant(item)}`} title={`${item.displayName} · ${itemVariant(item)}`} onClick={() => onSelect(item.instrumentId)}>
              {group.category !== "SKILL_BOOK" && <InstrumentIcon item={item} />}<span>{itemVariant(item)}</span>
            </button>)}
          </div>
        </section>;
      })}
      {!groups.length && <div className="market-picker-empty"><strong>{searching ? "검색 결과가 없어요" : "등록된 품목이 없어요"}</strong><p>{searching ? "이름·등급·레벨로 다시 찾아보세요. 선택한 주문 품목은 유지됩니다." : "다른 분류를 선택해 보세요."}</p></div>}
    </div>
  </aside>;
}


export function PriceTrendChart({ instrumentId }: { instrumentId: string }) {
  type TrendInterval = "5m" | "15m" | "30m";
  const [interval, setInterval] = useState<TrendInterval>("15m");
  const [period, setPeriod] = useState<PriceTrendPeriod>("12h");
  const [hover, setHover] = useState<string | null>(null);
  const hostRef = useRef<HTMLDivElement>(null);
  const chartRef = useRef<IChartApi | null>(null);
  const lineSeriesRef = useRef<ISeriesApi<"Line"> | null>(null);
  const volumeSeriesRef = useRef<ISeriesApi<"Histogram"> | null>(null);
  const candles = useQuery({ queryKey: ["market-candles", instrumentId, interval, period], queryFn: () => marketApi.candles(instrumentId, interval, period), refetchInterval: 30_000 });
  const items = candles.data?.items ?? [];
  const fallbackPeriod = candles.isSuccess && items.length === 0 ? nextPriceTrendPeriod(period) : null;
  useEffect(() => {
    if (!fallbackPeriod) return;
    setHover(null);
    setPeriod(fallbackPeriod);
  }, [fallbackPeriod]);
  const data = useMemo(() => {
    const response = candles.data;
    if (!response) return null;
    const toTime = (value: string) => Math.floor(Date.parse(value) / 1_000) as Time;
    return {
      items: response.items,
      line: response.items.map(item => ({ time: toTime(item.openedAt), value: item.close } satisfies LineData<Time>)),
      volume: response.items.map(item => ({ time: toTime(item.openedAt), value: item.quantity, color: `${marketDirectionColor(item.open, item.close)}59` } satisfies HistogramData<Time>)),
      emptyRange: [{ time: toTime(response.from) }, { time: toTime(response.to) }],
      latest: response.items.at(-1) ?? null,
    };
  }, [candles.data]);
  const chartHostReady = Boolean(data && !fallbackPeriod);
  useEffect(() => {
    const host = hostRef.current;
    if (!host || !chartHostReady) return;
    host.replaceChildren();
    const chart = createChart(host, { autoSize: true, layout: { background: { type: ColorType.Solid, color: "transparent" }, textColor: "#715b49", fontSize: 12 }, localization: { timeFormatter: priceTrendCrosshairTimeText }, grid: { vertLines: { color: "rgba(118,81,57,.12)" }, horzLines: { color: "rgba(118,81,57,.16)" } }, timeScale: { timeVisible: true, secondsVisible: false, borderColor: "rgba(118,81,57,.35)" }, rightPriceScale: { borderColor: "rgba(118,81,57,.35)" }, crosshair: { mode: CrosshairMode.Normal } });
    chartRef.current = chart;
    /* 캔들은 이 게임 이용자에게 읽히지 않는다. 값이 오르내린 선 하나만 남긴다. */
    lineSeriesRef.current = chart.addSeries(LineSeries, { color: MARKET_ACCENT_LINE, lineWidth: 3, crosshairMarkerVisible: true, crosshairMarkerRadius: 5, priceLineVisible: true, lastValueVisible: true });
    volumeSeriesRef.current = chart.addSeries(HistogramSeries, { priceFormat: { type: "volume" }, priceScaleId: "volume", lastValueVisible: false, priceLineVisible: false });
    chart.priceScale("volume").applyOptions({ scaleMargins: { top: .78, bottom: 0 } });
    return () => {
      chart.remove();
      host.replaceChildren();
      chartRef.current = null;
      lineSeriesRef.current = null;
      volumeSeriesRef.current = null;
    };
  }, [chartHostReady]);
  useEffect(() => {
    const chart = chartRef.current, lineSeries = lineSeriesRef.current, volumeSeries = volumeSeriesRef.current;
    if (!chart || !lineSeries || !volumeSeries || !data || fallbackPeriod) return;
    lineSeries.setData(data.line.length ? data.line : data.emptyRange);
    volumeSeries.setData(data.volume);
    chart.timeScale().fitContent();
    const handleCrosshair = (param: MouseEventParams<Time>) => {
      if (!param.time) { setHover(null); return; }
      const time = Number(param.time);
      const item = data.items.find(row => Math.floor(Date.parse(row.openedAt) / 1_000) === time);
      setHover(item ? `${item.openedAt}|${item.open}|${item.high}|${item.low}|${item.close}|${item.quantity}` : null);
    };
    chart.subscribeCrosshairMove(handleCrosshair);
    return () => chart.unsubscribeCrosshairMove(handleCrosshair);
  }, [data, fallbackPeriod, chartHostReady]);
  const hovered = hover?.split("|");
  const empty = candles.isSuccess && !fallbackPeriod && items.length === 0;
  return <section className="market-price-trend" aria-label="가격 추이">
    <header className="market-price-trend-heading">
      <h4>가격 추이</h4>
      {/* 봉 간격과 기간을 따로 고르게 하면 여섯 칸이 된다. 기간 하나만 고르고
          봉 간격은 거기 맞춰 따라간다. */}
      <div className="market-price-trend-period-tabs" role="group" aria-label="가격 추이 표시 기간">{PRICE_TREND_RANGES.map(range => <button
        key={range.period} type="button" aria-pressed={period === range.period}
        onClick={() => { setHover(null); setPeriod(range.period); setInterval(range.interval); }}
      >{range.label}</button>)}</div>
    </header>
    {candles.isPending || fallbackPeriod ? <p className="market-price-trend-status">{fallbackPeriod ? "체결 내역을 더 찾아보는 중입니다." : "가격 추이를 불러오는 중입니다."}</p>
      : candles.error ? <p className="market-price-trend-status">가격 추이를 불러오지 못했습니다.</p>
        : data ? <>
          {data.latest ? <div className="market-price-trend-summary"><strong>{data.latest.close.toLocaleString()}쌀</strong><span>고가 {Math.max(...data.items.map(item => item.high)).toLocaleString()}</span><span>저가 {Math.min(...data.items.map(item => item.low)).toLocaleString()}</span><span>거래량 {data.items.reduce((sum, item) => sum + item.quantity, 0).toLocaleString()}개</span></div> : <div className="market-price-trend-summary"><span>최근 2일간 체결 없음</span></div>}
          <div className="market-price-trend-chart-shell"><div ref={hostRef} className="market-price-trend-chart" />{empty && <p className="market-price-trend-empty">최근 2일간 체결 내역이 없습니다.</p>}</div>
          {/* 마우스를 올릴 때만 이 줄이 생기면 그래프 높이가 바뀌고, 그 바람에 다시
              마우스가 벗어나며 화면이 떨었다. 자리는 늘 잡아 두고 글자만 바꾼다. */}
          <p className="market-price-trend-hover-summary">{hovered
            ? `${formatMarketDateTime(hovered[0])} · ${Number(hovered[4]).toLocaleString()}쌀 · ${Number(hovered[5]).toLocaleString()}개 거래`
            : ""}</p>
        </> : null}
  </section>;
}

export function marketDepthChart(book?: MarketOrderBook) {
  const bids = (book?.bids ?? []).map(level => ({ ...level, side: "buy" as const, label: "매수", key: `buy:${level.unitPrice}` })).sort((a, b) => a.unitPrice - b.unitPrice);
  const asks = (book?.asks ?? []).map(level => ({ ...level, side: "sell" as const, label: "매도", key: `sell:${level.unitPrice}` })).sort((a, b) => a.unitPrice - b.unitPrice);
  const levels = [...bids, ...asks];
  const minimumPrice = levels.length ? Math.min(...levels.map(level => level.unitPrice)) : 0;
  const maximumPrice = levels.length ? Math.max(...levels.map(level => level.unitPrice)) : 0;
  const midpoint = bids.length && asks.length ? (bids.at(-1)!.unitPrice + asks[0].unitPrice) / 2 : null;
  const center = midpoint ?? (minimumPrice + maximumPrice) / 2;
  const distance = Math.max(1, center - minimumPrice, maximumPrice - center);
  const priceStep = 10 ** Math.floor(Math.log10(distance));
  const radius = Math.ceil(distance * 1.08 / priceStep) * priceStep;
  const priceFrom = Math.max(0, center - radius);
  const priceTo = center < radius ? Math.ceil((center + radius) / (priceStep * 4)) * priceStep * 4 : center + radius;
  const priceX = (price: number) => (price - priceFrom) / (priceTo - priceFrom) * 100;
  const maximumQuantity = Math.max(1, ...levels.map(level => level.cumulativeQuantity));
  const magnitude = 10 ** Math.floor(Math.log10(maximumQuantity / 4));
  const quantityStep = Math.max(1, Math.ceil(([1, 2, 2.5, 5, 10].find(step => step * magnitude >= maximumQuantity / 4) ?? 10) * magnitude));
  const quantityCeiling = Math.ceil(maximumQuantity / quantityStep) * quantityStep;
  const quantityY = (quantity: number) => 100 - quantity / quantityCeiling * 92;
  const point = (level: typeof levels[number]) => ({ ...level, x: priceX(level.unitPrice), y: quantityY(level.cumulativeQuantity) });
  const bidPoints = bids.map(point), askPoints = asks.map(point);
  const bestBid = bidPoints.at(-1), bestAsk = askPoints[0];
  // Bids count orders at or above a price; asks count orders at or below it.
  const bidFrom = bidPoints.length === ORDER_BOOK_DEPTH ? bidPoints[0].x : 0;
  const askTo = askPoints.length === ORDER_BOOK_DEPTH ? askPoints.at(-1)!.x : 100;
  // Do not extend truncated depth into prices whose remaining orders were not read.
  const bidLine = bidPoints.length ? `M${bidFrom},${bidPoints[0].y} H${bidPoints[0].x} ${bidPoints.slice(1).map(entry => `V${entry.y} H${entry.x}`).join(" ")} V100` : "";
  const askLine = bestAsk ? `M${bestAsk.x},100 V${bestAsk.y} ${askPoints.slice(1).map(entry => `H${entry.x} V${entry.y}`).join(" ")} H${askTo}` : "";
  return {
    levels: [...bidPoints, ...askPoints],
    bids: bidPoints,
    asks: askPoints,
    bestBid,
    bestAsk,
    bidLine,
    askLine,
    bidArea: bidLine ? `${bidLine} H${bidFrom} Z` : "",
    askArea: askLine ? `${askLine} V100 Z` : "",
    spread: bestBid && bestAsk ? { left: bestBid.x, width: Math.max(0, bestAsk.x - bestBid.x), price: bestAsk.unitPrice - bestBid.unitPrice } : null,
    midpoint,
    midpointX: midpoint === null ? null : priceX(midpoint),
    centered: midpoint !== null && priceFrom === center - radius,
    priceFrom,
    priceTo,
    bidFrom,
    askTo,
    quantityCeiling,
    quantityTicks: Array.from({ length: quantityCeiling / quantityStep + 1 }, (_, index) => ({ value: index * quantityStep, y: quantityY(index * quantityStep) })),
    priceTicks: levels.length ? Array.from({ length: 5 }, (_, index) => {
      const value = priceFrom + (priceTo - priceFrom) * index / 4;
      return { value, x: priceX(value) };
    }) : [],
  };
}

const depthQuantityFormat = new Intl.NumberFormat("ko-KR", { notation: "compact", maximumFractionDigits: 1 });

export function OrderBookPanel({ book, onPrice }: { book?: MarketOrderBook; onPrice: (price: number) => void }) {
  const [inspected, setInspected] = useState<string | null>(null);
  const chart = useMemo(() => marketDepthChart(book), [book]);
  const active = chart.levels.find(level => level.key === inspected);
  const detail = active ?? chart.bestAsk ?? chart.bestBid;
  const pointerLevel = (clientX: number, plot: HTMLElement) => {
    if (plot.closest("fieldset")?.disabled) return null;
    const bounds = plot.getBoundingClientRect();
    const x = (clientX - bounds.left) / bounds.width * 100;
    const hitRadius = Math.min(4 / bounds.width * 100, (chart.spread?.width ?? Infinity) / 4);
    const levels = chart.bestBid && x <= chart.bestBid.x + hitRadius && x >= chart.bidFrom - hitRadius ? chart.bids
      : chart.bestAsk && x >= chart.bestAsk.x - hitRadius && x <= chart.askTo + hitRadius ? chart.asks : [];
    let nearest: typeof chart.levels[number] | null = null, distance = Infinity;
    for (const level of levels) {
      const candidate = Math.abs(x - level.x);
      if (candidate < distance) { nearest = level; distance = candidate; }
    }
    return nearest;
  };
  return <section className="market-depth" aria-label="호가 차트">
    <header className="market-depth-heading">
      <h4>호가 차트</h4><div className="market-depth-legend"><span className="buy">매수</span><span className="sell">매도</span><span className="own">내 주문</span></div>
    </header>
    <div className="market-depth-quotes">
      <div className="buy"><span>최고 매수</span><strong>{chart.bestBid ? formatPrice(chart.bestBid.unitPrice) : "대기 주문 없음"}</strong></div>
      <div><span>스프레드</span><strong>{chart.spread ? formatPrice(chart.spread.price) : "—"}</strong></div>
      <div className="sell"><span>최저 매도</span><strong>{chart.bestAsk ? formatPrice(chart.bestAsk.unitPrice) : "대기 주문 없음"}</strong></div>
    </div>
    <div className="market-depth-unit"><span>누적 수량 (개)</span><span>{chart.midpoint !== null ? `중간가격 ${formatPrice(chart.midpoint)}${chart.centered ? "" : " · 가격 하한으로 범위 조정"}` : "중간가격 없음"}</span></div>
    <div className="market-depth-chart" role="group" aria-label="가격별 누적 호가 · 호가 선택으로 주문 가격 입력">
      <div className="market-depth-quantity-axis" aria-hidden="true">{chart.quantityTicks.map(tick => <span key={tick.value} style={{ top: `${tick.y}%` }}>{depthQuantityFormat.format(tick.value)}</span>)}</div>
      <div className="market-depth-plot"
        onPointerMove={event => { if (event.pointerType !== "touch") setInspected(pointerLevel(event.clientX, event.currentTarget)?.key ?? null); }}
        onPointerLeave={event => { if (!event.currentTarget.contains(document.activeElement)) setInspected(null); }}
        onBlur={event => { if (!event.currentTarget.contains(event.relatedTarget)) setInspected(null); }}
        onClick={event => {
          if (event.detail === 0) return;
          const selected = pointerLevel(event.clientX, event.currentTarget);
          if (selected) { setInspected(selected.key); onPrice(selected.unitPrice); }
        }}>
        <svg viewBox="0 0 100 100" preserveAspectRatio="none" aria-hidden="true">
          {chart.spread && <rect className="market-depth-spread" x={chart.spread.left} y="0" width={chart.spread.width} height="100" />}
          {chart.midpointX !== null && <line className="market-depth-midpoint" x1={chart.midpointX} x2={chart.midpointX} y1="0" y2="100" />}
          {chart.bidFrom > 0 && <line className="market-depth-boundary" x1={chart.bidFrom} x2={chart.bidFrom} y1="0" y2="100" />}
          {chart.askTo < 100 && <line className="market-depth-boundary" x1={chart.askTo} x2={chart.askTo} y1="0" y2="100" />}
          {chart.quantityTicks.map(tick => <line key={tick.value} className="market-depth-grid" x1="0" x2="100" y1={tick.y} y2={tick.y} />)}
          {chart.priceTicks.map(tick => <line key={tick.value} className="market-depth-grid vertical" x1={tick.x} x2={tick.x} y1="0" y2="100" />)}
          {chart.bidArea && <path className="market-depth-area buy" d={chart.bidArea} />}
          {chart.askArea && <path className="market-depth-area sell" d={chart.askArea} />}
          {chart.bidLine && <path className="market-depth-line buy" d={chart.bidLine} />}
          {chart.askLine && <path className="market-depth-line sell" d={chart.askLine} />}
          {active && <path className="market-depth-crosshair" d={`M${active.x},0 V100 M0,${active.y} H100`} />}
        </svg>
        {chart.levels.map(level => <button type="button" key={level.key} className={`market-depth-point ${level.side}${level.myQuantity > 0 ? " own" : ""}${active?.key === level.key ? " inspected" : ""}`}
          style={{ left: `${level.x}%`, top: `${level.y}%` }}
          aria-label={`${level.label} ${level.unitPrice.toLocaleString()}쌀, 잔량 ${level.totalQuantity.toLocaleString()}개, 누적 ${level.cumulativeQuantity.toLocaleString()}개, 내 주문 ${level.myQuantity.toLocaleString()}개. 가격 입력`}
          onFocus={() => setInspected(level.key)} onClick={event => {
            event.stopPropagation(); setInspected(level.key); onPrice(level.unitPrice);
          }}><span aria-hidden="true" /></button>)}
        {!detail && <div className="market-depth-empty"><strong>아직 대기 중인 주문이 없어요</strong><span>주문이 등록되면 호가 차트에 표시됩니다.</span></div>}
      </div>
      <div className="market-depth-price-axis" aria-hidden="true">{chart.priceTicks.map(tick => <span key={tick.value} style={{ left: `${tick.x}%` }}>{tick.value.toLocaleString()}</span>)}</div>
    </div>
    <div className="market-depth-axis">가격 (쌀){chart.centered ? " · 중간가격 기준 좌우 동일 범위" : ""}</div>
    {detail && <div className="market-depth-detail" aria-live="polite">
      <div className={detail.side}><span>{detail.label} 호가</span><strong>{formatPrice(detail.unitPrice)}</strong></div>
      <div><span>잔량</span><strong>{detail.totalQuantity.toLocaleString()}<small>개</small></strong></div>
      <div><span>누적 수량</span><strong>{detail.cumulativeQuantity.toLocaleString()}<small>개</small></strong></div>
      <div><span>내 주문</span><strong>{detail.myQuantity.toLocaleString()}<small>개</small></strong></div>
    </div>}
    <div className="market-depth-coverage"><span>매수 {chart.bids.length} · 매도 {chart.asks.length} 가격대</span><span>방향별 최대 {ORDER_BOOK_DEPTH}단계 조회</span></div>
    {(chart.bidFrom > 0 || chart.askTo < 100) && <p className="market-depth-limit">조회 경계 밖에 주문이 더 있을 수 있습니다. 누적량은 조회 범위 기준입니다.</p>}
    <p className="market-depth-hint">차트에서 호가 탐색 · 클릭 또는 키보드 Enter로 가격만 입력</p>
  </section>;
}

function RecentTrades({ book }: { book?: MarketOrderBook }) {
  return <section className="market-recent-trades"><header><h4>최근 체결</h4><small>시장 전체 · 최근 10건</small></header>
    {book?.recentTrades.length ? <><div className="market-recent-head"><span>체결 단가</span><span>수량</span><span>시각</span></div>{book.recentTrades.slice(0, 10).map(trade => <div className="market-recent-row" key={trade.tradeId}><strong>{formatPrice(trade.unitPrice)}</strong><span>{trade.quantity.toLocaleString()}개</span><time dateTime={trade.filledAt}>{formatMarketDateTime(trade.filledAt)}</time></div>)}</> : <p className="market-trading-note">아직 체결된 거래가 없어요.<br /><b>주문이 체결되면 최근 흐름을 확인할 수 있어요.</b></p>}
  </section>;
}

export function TradeTicket({ selected, side, setSide, view, timeInForce, setTimeInForce, quantity, setQuantity, unitPrice, setUnitPrice, availableQuantity, availableRice, quote, quotePending, quoteError, busy, locked, confirmation, onCancel, onSubmit }: {
  selected: MarketInstrument; side: MarketOrderSide; setSide: (side: MarketOrderSide) => void; view: MarketView; timeInForce: MarketTimeInForce; setTimeInForce: (tif: MarketTimeInForce) => void;
  quantity: NumericFieldValue; setQuantity: Dispatch<SetStateAction<NumericFieldValue>>; unitPrice: NumericFieldValue; setUnitPrice: Dispatch<SetStateAction<NumericFieldValue>>;
  availableQuantity?: number; availableRice?: number; quote?: MarketOrderQuote; quotePending: boolean; quoteError: string | null; busy: boolean; locked: boolean;
  confirmation: TradeConfirmation | null; onCancel: () => void; onSubmit: () => void;
}) {
  const parsedQuantity = positiveInteger(quantity), parsedPrice = marketUnitPrice(unitPrice);
  const valid = parsedQuantity !== null && parsedPrice !== null && selected.status === "ACTIVE";
  const ceiling = valid && Number.isSafeInteger(parsedQuantity * parsedPrice) ? parsedQuantity * parsedPrice : null;
  const buying = side === "BUY", awaiting = timeInForce === "GTC", simple = view === "simple";
  const action = simple ? buying ? "구매하기" : "판매하기" : `${awaiting ? "지정가" : "즉시"} ${buying ? "매수" : "매도"}`;
  const priceLabel = buying ? "개당 최대 허용 가격" : "개당 판매 가격";
  const quantityLabel = simple ? buying ? "구매 수량" : "판매 수량" : "주문 수량";
  const assetError = buying
    ? availableRice == null ? "사용 가능 쌀을 확인하고 있어요." : (awaiting ? ceiling ?? 0 : quote?.totalPrice ?? 0) > availableRice ? "현재 견적에 필요한 쌀이 부족해요. 수량이나 가격 한도를 조정해 주세요." : null
    : availableQuantity == null ? "판매 가능한 재고를 확인하고 있어요." : parsedQuantity !== null && parsedQuantity > availableQuantity ? "판매 가능한 수량보다 많아요. 수량을 조정해 주세요." : null;
  const noFill = !awaiting && quote?.expectedFilledQuantity === 0;
  const canSubmit = !busy && (confirmation !== null || (valid && ceiling !== null && Boolean(quote) && !quotePending && !quoteError && !assetError && !noFill));
  return <aside className={`market-order-ticket ${side.toLowerCase()}${simple ? " market-simple-ticket" : ""}`} aria-label={simple ? "구매·판매 내용" : "주문서"}>
    <header className="market-ticket-heading"><small>{confirmation ? "최종 확인" : simple ? buying ? "구매 내용" : "판매 내용" : "주문서"}</small><h3>{selected.displayName}</h3></header>
    {selected.category === "GEM" && <p className="market-ticket-note">{itemVariant(selected)} · 잠금·장착·판매 중인 보석 제외</p>}
    {!confirmation && !simple && <div className="market-tif-toggle"><button className={!awaiting ? "active" : ""} aria-pressed={!awaiting} disabled={locked} onClick={() => setTimeInForce("IOC")}><strong>즉시 거래</strong><small>가능한 수량만</small></button><button className={awaiting ? "active" : ""} aria-pressed={awaiting} disabled={locked} onClick={() => setTimeInForce("GTC")}><strong>지정가 주문</strong><small>최대 8시간 대기</small></button></div>}
    <label className="market-field"><span>{quantityLabel}</span><span className="market-numeric-field"><input aria-label={quantityLabel} value={quantity} inputMode="numeric" disabled={locked} onChange={event => setQuantity(numericInputValue(event.target.value, Number.MAX_SAFE_INTEGER))} /><small>개</small><button type="button" className="market-numeric-clear" aria-label={`${quantityLabel} 초기화`} disabled={locked} onClick={() => setQuantity("")}>×</button></span></label>
    {!confirmation && <div className="market-quantity-shortcuts">{[10, 100, 500].map(value => <button key={value} disabled={locked} onClick={() => setQuantity(current => Math.min(Number.MAX_SAFE_INTEGER, (positiveInteger(current) ?? 0) + value))}>{value}개</button>)}{!buying && <button disabled={locked || !availableQuantity} onClick={() => setQuantity(availableQuantity ?? "")}>전량</button>}</div>}
    <label className="market-field"><span>{priceLabel}</span><span className="market-numeric-field market-price-field"><input aria-label={priceLabel} value={unitPrice} placeholder="가격 입력" inputMode="numeric" disabled={locked} onChange={event => setUnitPrice(numericInputValue(event.target.value, MAX_MARKET_UNIT_PRICE))} /><small>쌀</small><button type="button" className="market-numeric-clear" aria-label={`${priceLabel} 초기화`} disabled={locked} onClick={() => setUnitPrice("")}>×</button></span></label>
    {quotePending && <p className="market-inline-feedback" role="status">최신 견적을 확인하고 있어요…</p>}
    <dl className="market-quote">
      <div><dt>{simple ? buying ? "시장 기준 예상 구매" : "시장 기준 예상 판매" : "예상 체결"}</dt><dd>{quote?.expectedFilledQuantity.toLocaleString() ?? "—"}개</dd></div>
      <div><dt>{awaiting ? "대기할 수량" : buying ? "구매되지 않는 수량" : "판매되지 않는 수량"}</dt><dd>{quote?.expectedRemainingQuantity.toLocaleString() ?? "—"}개</dd></div>
      <div><dt>예상 평균 단가</dt><dd>{quote?.weightedAverageUnitPrice == null ? "—" : `${quote.weightedAverageUnitPrice.toLocaleString("ko-KR", { maximumFractionDigits: 2 })}쌀`}</dd></div>
      {quote?.lowestFilledUnitPrice != null && <div><dt>예상 가격 범위</dt><dd>{formatPrice(quote.lowestFilledUnitPrice)} ~ {formatPrice(quote.highestFilledUnitPrice)}</dd></div>}
      {!buying && <><div><dt>예상 판매금액</dt><dd>{formatPrice(quote?.totalPrice)}</dd></div><div><dt>판매 수수료</dt><dd>{formatPrice(quote?.expectedFee)}</dd></div></>}
    </dl>
    <div className="market-quote-total"><span>{buying ? "예상 결제액" : "예상 받을 쌀"}</span><strong>{formatPrice(buying ? quote?.totalPrice : quote?.expectedSettlementAmount)}</strong></div>
    {awaiting && !buying && <p className="market-ticket-note">등록 물품은 거래소에서 보관하고, 취소나 만료된 물품은 물품 수령함에 보관됩니다.</p>}
    {confirmation && <p className="market-confirmation-note" role="status">위 수량과 가격으로 {buying ? "구매" : "판매"}합니다. 표시된 예상 금액은 확정 금액이 아닙니다.</p>}
    {!valid && <p className="market-inline-feedback">{selected.status !== "ACTIVE" ? "거래 중단된 품목입니다." : "수량과 10~999,999쌀의 가격을 입력해 주세요."}</p>}
    {assetError && <p className="market-inline-feedback" role="status">{assetError}</p>}
    {noFill && <p className="market-inline-feedback">지금 이 가격 조건으로 거래할 상대 물량이 없어요.</p>}
    {confirmation && <p className="market-confirmation-note" role="status">위 수량과 가격 한도로 {buying ? "구매" : awaiting ? "판매 등록" : "판매"}합니다. 표시된 예상 금액은 확정 금액이 아닙니다.</p>}
    {confirmation && <button className="market-confirm-cancel" disabled={busy} onClick={onCancel}>돌아가서 수정</button>}
    <button className={`market-submit ${side.toLowerCase()}`} disabled={!canSubmit} onClick={onSubmit}>{busy ? "처리 중…" : confirmation ? action : "거래 내용 확인"}</button>
  </aside>;
}
