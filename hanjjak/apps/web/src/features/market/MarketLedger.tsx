import { useMemo, useState } from "react";
import { PlazaIcon, type PlazaIconName } from "./PlazaIcon";
import { itemArt } from "./marketArt";
import { formatMarketDateTime } from "./marketDateTime";
import { fillSummaryText, reconstructOrderFills } from "./marketFills";
import { MAX_MARKET_UNIT_PRICE, marketUnitPrice, numericInputValue, positiveInteger, type NumericFieldValue } from "./marketForm";
import type { MarketOrder, MarketTrade } from "./api";

type PeriodId = "7d" | "30d" | "all";
type Draft = { quantity: NumericFieldValue; price: NumericFieldValue };
type StatusChip = { label: string; icon: PlazaIconName; tone: "waiting" | "partial" | "done" | "cancelled" | "expired" };

const PERIODS: Array<{ id: PeriodId; label: string; days: number | null }> = [
  { id: "7d", label: "최근 7일", days: 7 }, { id: "30d", label: "최근 30일", days: 30 }, { id: "all", label: "전체", days: null },
];
const STATUS_CHIPS: Record<string, StatusChip> = {
  ACTIVE: { label: "기다리는 중", icon: "clock", tone: "waiting" },
  PARTIALLY_FILLED: { label: "일부 거래됨", icon: "half", tone: "partial" },
  PARTIALLY_FILLED_CLOSED: { label: "일부만 거래됨", icon: "half", tone: "partial" },
  FILLED: { label: "모두 거래됨", icon: "check", tone: "done" },
  CANCELLED: { label: "취소함", icon: "cross", tone: "cancelled" },
  EXPIRED: { label: "기간 끝남", icon: "hourglass", tone: "expired" },
  RECOVERY_REVIEW: { label: "확인 중", icon: "hourglass", tone: "expired" },
};
const MAX_ORDER_QUANTITY = 9_999;

function statusChip(status: string): StatusChip {
  return STATUS_CHIPS[status] ?? { label: "상태 확인 중", icon: "hourglass", tone: "expired" };
}
function sideChip(side: MarketOrder["side"]) {
  return side === "BUY" ? { label: "구매", icon: "cart" as PlazaIconName, tone: "buy" } : { label: "판매", icon: "bag" as PlazaIconName, tone: "sell" };
}
function dateText(value: string): string {
  return formatMarketDateTime(value).slice(0, 10).replace(/-/g, ". ");
}
function timeText(value: string): string {
  return formatMarketDateTime(value).slice(11);
}
function withinPeriod(order: MarketOrder, days: number | null): boolean {
  if (days === null) return true;
  const updated = new Date(order.updatedAt).getTime();
  return Number.isNaN(updated) || updated >= Date.now() - days * 24 * 60 * 60 * 1_000;
}

function OrderIdentity({ order }: { order: MarketOrder }) {
  const side = sideChip(order.side);
  const status = statusChip(order.status);
  return <div className="market-plaza-order-identity">
    <p className="market-plaza-order-name">
      <strong>{order.displayName}</strong>
      <span className={`market-plaza-chip side ${side.tone}`}><PlazaIcon name={side.icon} />{side.label}</span>
    </p>
    <span className={`market-plaza-chip status ${status.tone}`}><PlazaIcon name={status.icon} />{status.label}</span>
  </div>;
}

export type MarketLedgerProps = {
  active: MarketOrder[];
  closed: MarketOrder[];
  trades: MarketTrade[];
  loading: boolean;
  busy: boolean;
  onUpdate: (orderId: string, request: { quantity?: number; limitUnitPrice?: number }) => void;
  onCancel: (orderId: string) => void;
};

export function MarketLedger({ active, closed, trades, loading, busy, onUpdate, onCancel }: MarketLedgerProps) {
  const [period, setPeriod] = useState<PeriodId>("30d");
  const [openOrderId, setOpenOrderId] = useState<string | null>(null);
  const days = PERIODS.find(entry => entry.id === period)?.days ?? null;
  const past = useMemo(() => closed.filter(order => withinPeriod(order, days)), [closed, days]);
  const openOrder = past.find(order => order.orderId === openOrderId) ?? null;

  if (loading) return <p className="market-plaza-empty" role="status">내 거래를 불러오는 중이에요…</p>;
  return <div className="market-plaza-ledger">
    <section className="market-plaza-ledger-column" aria-labelledby="market-plaza-active-heading">
      <header className="market-plaza-ledger-heading">
        <PlazaIcon name="clock" /><h3 id="market-plaza-active-heading">진행 중 거래</h3>
        {active.length > 0 && <em>{active.length}건</em>}
      </header>
      <div className="market-plaza-scroll">
        {active.length === 0 && <p className="market-plaza-empty small">진행 중인 거래가 없어요. 거래하기에서 첫 거래를 넣어 보세요.</p>}
        {active.map(order => <ActiveOrderCard key={order.orderId} order={order} busy={busy} onUpdate={onUpdate} onCancel={onCancel} />)}
      </div>
    </section>
    <section className="market-plaza-ledger-column" aria-labelledby="market-plaza-past-heading">
      <header className="market-plaza-ledger-heading">
        <PlazaIcon name="parcel" /><h3 id="market-plaza-past-heading">지난 거래</h3>
        <label className="market-plaza-period">
          <PlazaIcon name="calendar" />
          <select aria-label="지난 거래 기간" value={period} onChange={event => { setPeriod(event.currentTarget.value as PeriodId); setOpenOrderId(null); }}>
            {PERIODS.map(entry => <option key={entry.id} value={entry.id}>{entry.label}</option>)}
          </select>
        </label>
      </header>
      <div className="market-plaza-scroll">
        {past.length === 0 && <p className="market-plaza-empty small">이 기간에 끝난 거래가 없어요.</p>}
        {past.map(order => {
          const breakdown = reconstructOrderFills(order, trades);
          const total = breakdown.exact ? breakdown.fills.reduce((sum, fill) => sum + fill.totalPrice, 0) : null;
          const open = order.orderId === openOrderId;
          return <button key={order.orderId} type="button" className={`market-plaza-past-row${open ? " open" : ""}`} aria-expanded={open} onClick={() => setOpenOrderId(open ? null : order.orderId)}>
            <img src={itemArt(order.itemId)} alt="" />
            <OrderIdentity order={order} />
            <dl className="market-plaza-order-figures">
              <div><dt>수량</dt><dd>{order.initialQuantity.toLocaleString()}개</dd></div>
              <div><dt>총 금액</dt><dd>{total == null ? "—" : `${total.toLocaleString()} 쌀`}</dd></div>
            </dl>
            <time dateTime={order.updatedAt}>{dateText(order.updatedAt)}</time>
            <PlazaIcon name="chevron" className={open ? "is-open" : ""} />
          </button>;
        })}
      </div>
      {openOrder && <FillBreakdown order={openOrder} trades={trades} />}
    </section>
  </div>;
}

function ActiveOrderCard({ order, busy, onUpdate, onCancel }: { order: MarketOrder; busy: boolean; onUpdate: MarketLedgerProps["onUpdate"]; onCancel: MarketLedgerProps["onCancel"] }) {
  const [draft, setDraft] = useState<Draft | null>(null);
  const [error, setError] = useState("");

  const save = () => {
    if (!draft) return;
    const quantity = positiveInteger(draft.quantity), price = marketUnitPrice(draft.price);
    if (!quantity || !price) {
      setError("수량은 1개부터, 가격은 10쌀부터 넣을 수 있어요.");
      return;
    }
    const request: { quantity?: number; limitUnitPrice?: number } = {};
    if (quantity !== order.remainingQuantity) request.quantity = quantity;
    if (price !== order.limitUnitPrice) request.limitUnitPrice = price;
    setError("");
    setDraft(null);
    if (Object.keys(request).length > 0) onUpdate(order.orderId, request);
  };

  return <article className={`market-plaza-active-card${draft ? " editing" : ""}`}>
    <img src={itemArt(order.itemId)} alt="" />
    <OrderIdentity order={order} />
    {draft ? <>
      <div className="market-plaza-edit-fields">
        <label><span>남은 수량</span><input inputMode="numeric" aria-label={`${order.displayName} 남은 수량`} disabled={busy} value={draft.quantity} onChange={event => setDraft({ ...draft, quantity: numericInputValue(event.currentTarget.value, MAX_ORDER_QUANTITY) })} /></label>
        <label><span>개당 쌀</span><input inputMode="numeric" aria-label={`${order.displayName} 개당 가격`} disabled={busy} value={draft.price} onChange={event => setDraft({ ...draft, price: numericInputValue(event.currentTarget.value, MAX_MARKET_UNIT_PRICE) })} /></label>
      </div>
      <div className="market-plaza-order-actions">
        <button type="button" className="market-plaza-row-button save" disabled={busy} onClick={save}>저장</button>
        <button type="button" className="market-plaza-row-button ghost" disabled={busy} onClick={() => { setDraft(null); setError(""); }}>그만두기</button>
      </div>
      {error && <p className="market-plaza-ticket-error" role="alert">{error}</p>}
    </> : <>
      <dl className="market-plaza-order-figures">
        <div><dt>수량</dt><dd>{order.initialQuantity.toLocaleString()}개</dd></div>
        <div><dt>남은 수량</dt><dd>{order.remainingQuantity.toLocaleString()}개</dd></div>
        <div><dt>개당</dt><dd>{order.limitUnitPrice.toLocaleString()} 쌀</dd></div>
      </dl>
      <div className="market-plaza-order-actions">
        <button type="button" className="market-plaza-row-button edit" disabled={busy} onClick={() => setDraft({ quantity: order.remainingQuantity, price: order.limitUnitPrice })}><PlazaIcon name="pencil" />수정</button>
        <button type="button" className="market-plaza-row-button cancel" disabled={busy} onClick={() => onCancel(order.orderId)}><PlazaIcon name="trash" />취소</button>
      </div>
    </>}
  </article>;
}

function FillBreakdown({ order, trades }: { order: MarketOrder; trades: MarketTrade[] }) {
  const breakdown = reconstructOrderFills(order, trades);
  return <section className="market-plaza-fills" aria-label={`${order.displayName} 실제로 거래된 내용`}>
    <header><h4>실제로 거래된 내용</h4><i aria-hidden="true" /><p>{fillSummaryText(breakdown, order.side)}</p></header>
    {breakdown.fills.length === 0
      ? <p className="market-plaza-empty small">{breakdown.exact ? "거래된 수량 없이 끝난 거래예요." : "이 거래의 체결 기록은 남아 있는 범위를 넘어갔어요."}</p>
      : <ul>{breakdown.fills.map(fill => <li key={fill.tradeId}>
        <span><small>수량</small><strong>{fill.quantity.toLocaleString()}개</strong></span>
        <span><small>개당</small><strong>{fill.unitPrice.toLocaleString()} 쌀</strong></span>
        <span><small>금액</small><strong>{fill.totalPrice.toLocaleString()} 쌀</strong></span>
        <time dateTime={fill.filledAt}>{timeText(fill.filledAt)}</time>
      </li>)}</ul>}
  </section>;
}
