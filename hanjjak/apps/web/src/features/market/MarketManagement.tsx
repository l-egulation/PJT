import { useId, useState } from "react";
import riceIcon from "../equipment/assets-cozy-pixel/currency-rice-gold-256.png";
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
import skillbookIcon from "../../shared/assets/cozy-hud-v1/icons/reward-skillbook.png";
import gemIcon from "../../shared/assets/cozy-hud-v1/icons/bottom-gems.png";
import { MAX_MARKET_UNIT_PRICE, marketUnitPrice, numericInputValue, positiveInteger, type NumericFieldValue } from "./marketForm";
import type { MarketDelivery, MarketOrder, MarketSettlementMail, MarketTrade } from "./api";
import { formatMarketDateTime } from "./marketDateTime";

const MAX_ORDER_QUANTITY = Number.MAX_SAFE_INTEGER;
const ITEM_ART: Record<string, string> = {
  POTATO_M1: potatoFragment,
  POTATO_M2: potatoMini,
  POTATO_M3: potato,
  POTATO_M4: potatoGolden,
  POTATO_M5: potatoLegendary,
  SWEET_POTATO_M1: sweetFragment,
  SWEET_POTATO_M2: sweetMini,
  SWEET_POTATO_M3: sweetPotato,
  SWEET_POTATO_M4: sweetGolden,
  SWEET_POTATO_M5: sweetLegendary,
  CORN_M1: cornKernel,
  CORN_M2: cornMini,
  CORN_M3: corn,
  CORN_M4: cornGolden,
  CORN_M5: cornLegendary,
};

const ORDER_STATUS_LABELS: Record<string, string> = {
  ACTIVE: "대기 중",
  PARTIALLY_FILLED: "부분 체결",
  PARTIALLY_FILLED_CLOSED: "부분 체결 종료",
  FILLED: "전량 체결",
  CANCELLED: "취소됨",
  EXPIRED: "만료됨",
  RECOVERY_REVIEW: "복구 검토 중",
};

const DELIVERY_SOURCE_LABELS: Record<MarketDelivery["source"], string> = {
  BUY_FILL: "구매 체결",
  SELL_CANCEL_RETURN: "판매 주문 취소 반환",
  SELL_EXPIRE_RETURN: "판매 주문 만료 반환",
};

const MAIL_TYPE_LABELS: Record<string, string> = {
  MARKET_SETTLEMENT: "판매 정산",
  MARKET_SALE_SETTLEMENT: "판매 정산",
  MARKET_TRADE_SETTLEMENT: "거래소 정산",
};

type OrderUpdateRequest = { quantity?: number; limitUnitPrice?: number };
type Draft = { quantity: NumericFieldValue; price: NumericFieldValue };

function formatNumber(value: number) {
  return value.toLocaleString("ko-KR");
}


function orderStatusLabel(status: string) {
  return ORDER_STATUS_LABELS[status] ?? "상태 확인 중";
}

function itemArt(itemId: string) {
  if (ITEM_ART[itemId]) return ITEM_ART[itemId];
  if (itemId.startsWith("skillbook:")) return skillbookIcon;
  if (itemId.startsWith("gem:")) return gemIcon;
  return riceIcon;
}

function ItemArtwork({ itemId }: { itemId: string }) {
  return <span className="market-item-art"><img src={itemArt(itemId)} alt="" aria-hidden="true" /></span>;
}

function sideDetails(side: MarketOrder["side"] | MarketTrade["side"]) {
  if (side === "BUY") return { label: "구매", className: "buy" };
  if (side === "SELL") return { label: "판매", className: "sell" };
  return { label: "체결", className: "" };
}

function emptyState(title: string, message: string) {
  return <div className="market-empty-state"><img src={riceIcon} alt="" aria-hidden="true" /><h4>{title}</h4><p>{message}</p></div>;
}

export type OrderManagementProps = {
  active: MarketOrder[];
  closed: MarketOrder[];
  busy: boolean;
  onUpdate: (orderId: string, request: OrderUpdateRequest) => void;
  onCancel: (orderId: string) => void;
};

export function OrderManagement({ active, closed, busy, onUpdate, onCancel }: OrderManagementProps) {
  const headingId = useId();
  const [tab, setTab] = useState<"active" | "closed">("active");
  const [editingId, setEditingId] = useState<string | null>(null);
  const [drafts, setDrafts] = useState<Record<string, Draft>>({});
  const [errors, setErrors] = useState<Record<string, string>>({});
  const orders = tab === "active" ? active : closed;

  function startEditing(order: MarketOrder) {
    setEditingId(order.orderId);
    setDrafts(current => ({ ...current, [order.orderId]: { quantity: order.remainingQuantity, price: order.limitUnitPrice } }));
    setErrors(current => ({ ...current, [order.orderId]: "" }));
  }

  function updateDraft(order: MarketOrder, key: keyof Draft, value: NumericFieldValue) {
    setDrafts(current => ({ ...current, [order.orderId]: { ...(current[order.orderId] ?? { quantity: order.remainingQuantity, price: order.limitUnitPrice }), [key]: value } }));
  }

  function saveEditing(order: MarketOrder) {
    const draft = drafts[order.orderId] ?? { quantity: order.remainingQuantity, price: order.limitUnitPrice };
    const quantity = positiveInteger(draft.quantity);
    const price = marketUnitPrice(draft.price);
    if (!quantity || !price) {
      setErrors(current => ({ ...current, [order.orderId]: "수량은 1개 이상, 가격은 10쌀 이상 입력해 주세요." }));
      return;
    }
    const request: OrderUpdateRequest = {};
    if (quantity !== order.remainingQuantity) request.quantity = quantity;
    if (price !== order.limitUnitPrice) request.limitUnitPrice = price;
    setErrors(current => ({ ...current, [order.orderId]: "" }));
    setEditingId(null);
    if (Object.keys(request).length > 0) onUpdate(order.orderId, request);
  }

  return <section className="market-management" aria-labelledby={headingId}>
    <header className="market-management-heading"><div><h3 id={headingId}>내 주문</h3><p>진행 중인 주문은 가격과 미체결 수량을 조정할 수 있어요.</p></div></header>
    <nav className="market-management-tabs" aria-label="주문 상태 선택">
      <button type="button" aria-pressed={tab === "active"} className={tab === "active" ? "active" : ""} onClick={() => { setTab("active"); setEditingId(null); }}>진행 중 <span className="market-count">{active.length}</span></button>
      <button type="button" aria-pressed={tab === "closed"} className={tab === "closed" ? "active" : ""} onClick={() => { setTab("closed"); setEditingId(null); }}>최근 종료 <span className="market-count">{closed.length}</span></button>
    </nav>
    <div className="market-order-rows" aria-label={tab === "active" ? "진행 중인 주문 목록" : "최근 종료된 주문 목록"}>
      {orders.length === 0 && (tab === "active" ? emptyState("진행 중인 주문이 없어요", "거래할 품목을 골라 첫 주문을 등록해 보세요.") : emptyState("최근 종료된 주문이 없어요", "체결되거나 종료된 주문이 여기에 표시됩니다."))}
      {orders.map(order => {
        const editing = editingId === order.orderId;
        const draft = drafts[order.orderId] ?? { quantity: order.remainingQuantity, price: order.limitUnitPrice };
        const side = sideDetails(order.side);
        return <article className={`market-record-card${editing ? " editing" : ""}`} key={order.orderId}>
          <div className="market-record-heading"><strong><ItemArtwork itemId={order.itemId} />{order.displayName}</strong><span className={`market-side ${side.className}`}>{side.label}</span><span className="market-status">{orderStatusLabel(order.status)}</span></div>
          <div className="market-record-meta"><span>등록 {formatMarketDateTime(order.createdAt)}</span><span>{order.expiresAt ? `만료 ${formatMarketDateTime(order.expiresAt)}` : "즉시 거래"}</span></div>
          {editing ? <>
            <div className="market-record-fields">
              <label><span>미체결 수량</span><div className="market-numeric-field"><input aria-label={`${order.displayName} 미체결 수량`} disabled={busy} inputMode="numeric" value={draft.quantity} onChange={event => updateDraft(order, "quantity", numericInputValue(event.currentTarget.value, MAX_ORDER_QUANTITY))} /><small>개</small></div><small>체결 {formatNumber(order.filledQuantity)}개 · 미체결 주문의 수량</small></label>
              <label><span>개당 가격</span><div className="market-numeric-field"><input aria-label={`${order.displayName} 개당 가격`} disabled={busy} inputMode="numeric" value={draft.price} onChange={event => updateDraft(order, "price", numericInputValue(event.currentTarget.value, MAX_MARKET_UNIT_PRICE))} /><small>쌀</small></div></label>
            </div>
            <p className="market-record-note">미체결 수량을 줄이면 현재 우선순위를 유지합니다. 수량을 늘리거나 가격을 바꾸면 주문 우선순위와 만료 시각이 새로 설정됩니다.</p>
            {errors[order.orderId] && <p className="market-inline-feedback" role="alert">{errors[order.orderId]}</p>}
            <div className="market-record-actions"><button type="button" className="primary" disabled={busy} onClick={() => saveEditing(order)}>저장</button><button type="button" disabled={busy} onClick={() => setEditingId(null)}>취소</button></div>
          </> : <>
            <div className="market-record-fields"><span><small>체결 / 잔량</small><strong>{formatNumber(order.filledQuantity)} / {formatNumber(order.remainingQuantity)}개</strong></span><span><small>개당 가격</small><strong>{formatNumber(order.limitUnitPrice)}쌀</strong></span>{order.side === "BUY" && <span><small>주문에 보관 중인 쌀</small><strong>{formatNumber(order.reservedRice)}쌀</strong></span>}</div>
            {tab === "active" && <div className="market-record-actions"><button type="button" className="primary" disabled={busy} onClick={() => startEditing(order)}>주문 수정</button><button type="button" className="danger" disabled={busy} onClick={() => onCancel(order.orderId)}>주문 취소</button></div>}
          </>}
        </article>;
      })}
    </div>
  </section>;
}

export type DeliveryPanelProps = {
  items: MarketDelivery[];
  busy: boolean;
  onClaim: (deliveryId: string) => void;
  onClaimAll: () => void;
};

function deliverySource(source: MarketDelivery["source"]) {
  return DELIVERY_SOURCE_LABELS[source] ?? "거래소 반환";
}

export function DeliveryPanel({ items, busy, onClaim, onClaimAll }: DeliveryPanelProps) {
  const headingId = useId();
  const claimable = items.filter(item => !item.claimedAt).length;
  return <section className="market-management" aria-labelledby={headingId}>
    <header className="market-management-heading"><div><h3 id={headingId}>물품 수령함</h3><p>거래소에 보관 중인 물품을 인벤토리로 가져옵니다. 미수령 {claimable}건</p></div><button type="button" className="primary" disabled={busy || claimable === 0} onClick={onClaimAll}>전체 수령{claimable > 0 ? ` (${claimable})` : ""}</button></header>
    <div className="market-delivery-rows" aria-label="물품 수령 목록">
      {items.length === 0 && emptyState("수령할 물품이 없어요", "지정가 구매 체결 물품과 판매 주문의 반환 물품이 이곳에 보관됩니다.")}
      {items.length > 0 && claimable === 0 && <p className="market-record-note">모든 물품을 수령했습니다.</p>}
      {items.map(delivery => <article className="market-record-card" key={delivery.deliveryId}>
        <div className="market-record-heading"><strong><ItemArtwork itemId={delivery.itemId} />{delivery.displayName}</strong><span className="market-status">{delivery.claimedAt ? "수령 완료" : "수령 가능"}</span></div>
        <div className="market-record-meta"><span>출처 {deliverySource(delivery.source)}</span><span>도착 {formatMarketDateTime(delivery.createdAt)}</span>{delivery.claimedAt && <span>수령 {formatMarketDateTime(delivery.claimedAt)}</span>}</div>
        <div className="market-record-fields"><span><small>수령 수량</small><strong>{formatNumber(delivery.quantity)}개</strong></span></div>
        <div className="market-record-actions">{delivery.claimedAt ? <span className="market-status">수령 완료</span> : <button type="button" className="primary" disabled={busy} onClick={() => onClaim(delivery.deliveryId)}>수령하기</button>}</div>
      </article>)}
    </div>
  </section>;
}

export type SettlementPanelProps = {
  items: MarketSettlementMail[];
  busy: boolean;
  onClaim: (mailId: string) => void;
  onClaimAll: () => void;
};

function settlementTypeLabel(type: string) {
  const normalized = type.toUpperCase();
  if (MAIL_TYPE_LABELS[normalized]) return MAIL_TYPE_LABELS[normalized];
  return normalized.includes("MARKET") ? "거래소 판매 정산" : "판매 정산";
}

export function SettlementPanel({ items, busy, onClaim, onClaimAll }: SettlementPanelProps) {
  const headingId = useId();
  const claimable = items.filter(item => !item.claimed).length;
  return <section className="market-management" aria-labelledby={headingId}>
    <header className="market-management-heading"><div><h3 id={headingId}>판매 정산</h3><p>판매가 체결되면 쌀이 이곳에 도착해요. 미수령 {claimable}건</p></div><button type="button" className="primary" disabled={busy || claimable === 0} onClick={onClaimAll}>모두 수령{claimable > 0 ? ` (${claimable})` : ""}</button></header>
    <div className="market-delivery-rows settlement" aria-label="판매 정산 기록">
      {items.length === 0 && emptyState("받을 정산이 없어요", "판매가 체결되면 정산 금액이 여기에 도착합니다.")}
      {items.length > 0 && claimable === 0 && <p className="market-record-note">모든 정산을 수령했습니다.</p>}
      {items.map(mail => <article className="market-record-card" key={mail.mailId}>
        <div className="market-record-heading"><strong><img className="market-record-icon" src={riceIcon} alt="" aria-hidden="true" />{settlementTypeLabel(mail.type)}</strong><span className="market-status">{mail.claimed ? "수령 완료" : "수령 가능"}</span></div>
        <div className="market-record-meta"><span>발생 {formatMarketDateTime(mail.createdAt)}</span>{mail.claimedAt && <span>수령 {formatMarketDateTime(mail.claimedAt)}</span>}</div>
        <div className="market-record-fields"><span><small>정산 금액</small><strong>{formatNumber(mail.riceAmount)}쌀</strong></span><span><small>상태</small><strong>{mail.claimed ? "수령 완료" : "수령 가능"}</strong></span></div>
        <div className="market-record-actions">{mail.claimed ? <span className="market-status">수령 완료</span> : <button type="button" className="primary" disabled={busy} onClick={() => onClaim(mail.mailId)}>수령하기</button>}</div>
      </article>)}
    </div>
  </section>;
}

export type TradeListProps = {
  trades: MarketTrade[];
  selectedName?: string;
};

export function TradeList({ trades, selectedName }: TradeListProps) {
  const headingId = useId();
  return <section className="market-management" aria-labelledby={headingId}>
    <header className="market-management-heading"><div><h3 id={headingId}>체결 내역</h3><p>{selectedName ? `내 거래 · ${selectedName} · 다른 품목은 거래 탭에서 선택하세요.` : "최근 체결된 내 거래를 확인하세요."}</p></div></header>
    <div className="market-trade-rows" aria-label="체결 내역 목록">
      <div className="market-trade-head" aria-hidden="true"><span>품목</span><span>구분</span><span>수량</span><span>개당 가격</span><span>거래 금액</span><span>시각</span></div>
      {trades.length === 0 && emptyState("거래 기록이 없어요", selectedName ? `${selectedName}의 체결 기록이 아직 없습니다.` : "주문이 체결되면 이곳에 기록됩니다.")}
      {trades.map(trade => {
        const side = sideDetails(trade.side);
        return <article className="market-record-card market-trade-row" key={trade.tradeId}>
          <span className="market-order-item"><ItemArtwork itemId={trade.itemId} /><strong>{trade.displayName}</strong></span>
          <span className={`market-side ${side.className}`}>{side.label}</span>
          <span><small>수량</small><strong>{formatNumber(trade.quantity)}개</strong></span>
          <span><small>개당 가격</small><strong>{formatNumber(trade.unitPrice)}쌀</strong></span>
          <span><small>거래 금액</small><strong>{formatNumber(trade.totalPrice)}쌀</strong></span>
          <time dateTime={trade.filledAt}>{formatMarketDateTime(trade.filledAt)}</time>
        </article>;
      })}
    </div>
  </section>;
}
