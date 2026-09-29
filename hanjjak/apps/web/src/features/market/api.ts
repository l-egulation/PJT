export type MarketOrderSide = "BUY" | "SELL";
export type MarketTimeInForce = "GTC" | "IOC";
export type MarketOrderScope = "ACTIVE" | "CLOSED" | "RECOVERY_REVIEW";
export type MarketInstrumentStatus = "ACTIVE" | "CANCELLING" | "INACTIVE";
export type MarketUnreadStream = "FILLS" | "DELIVERIES" | "SETTLEMENTS" | "EXPIRATIONS";
export type MarketInstrument = { instrumentId: string; canonicalKey: string; itemId: string; displayName: string; category: string; attributes: Record<string, string>; status: MarketInstrumentStatus; bestBidUnitPrice: number | null; bestAskUnitPrice: number | null; lastTradeUnitPrice: number | null; marketRevision: number };
export type MarketDepthLevel = { unitPrice: number; totalQuantity: number; cumulativeQuantity: number; myQuantity: number };
export type MarketPriceLevel = MarketDepthLevel & { cumulativeOtherQuantity: number };
export type MarketPriceLevels = { instrumentId: string; side: MarketOrderSide; marketRevision: number; items: MarketPriceLevel[]; nextUnitPrice: number | null; totalLevels: number };
export type MarketRecentTrade = { tradeId: string; quantity: number; unitPrice: number; totalPrice: number; filledAt: string };
export type MarketCandle = { openedAt: string; open: number; high: number; low: number; close: number; quantity: number; totalPrice: number };
export type MarketCandles = { instrumentId: string; interval: string; from: string; to: string; marketRevision: number; items: MarketCandle[] };
export type MarketOrderBook = { instrumentId: string; marketRevision: number; bids: MarketDepthLevel[]; asks: MarketDepthLevel[]; bestBidUnitPrice: number | null; bestAskUnitPrice: number | null; spread: number | null; recentTrades: MarketRecentTrade[] };
export type MarketOrderQuote = { instrumentId: string; side: MarketOrderSide; timeInForce: MarketTimeInForce; requestedQuantity: number; limitUnitPrice: number; expectedFilledQuantity: number; expectedRemainingQuantity: number; lowestFilledUnitPrice: number | null; highestFilledUnitPrice: number | null; weightedAverageUnitPrice: number | null; totalPrice: number; expectedFee: number; expectedSettlementAmount: number; availableRice: number; availableQuantity: number; marketRevision: number };
export type MarketOrderFill = { tradeId: string; makerOrderId: string; takerOrderId: string; quantity: number; unitPrice: number; totalPrice: number; fee: number; settlementAmount: number };
export type MarketOrderResult = { orderId: string; instrumentId: string; side: MarketOrderSide; timeInForce: MarketTimeInForce; initialQuantity: number; filledQuantity: number; remainingQuantity: number; limitUnitPrice: number; reservedRice: number; status: string; priorityAt: string; expiresAt: string | null; totalPrice: number; lowestFilledUnitPrice: number | null; highestFilledUnitPrice: number | null; weightedAverageUnitPrice: number | null; marketRevision: number; fills: MarketOrderFill[] };
export type MarketOrder = { orderId: string; instrumentId: string; itemId: string; displayName: string; side: MarketOrderSide; timeInForce: MarketTimeInForce; initialQuantity: number; filledQuantity: number; remainingQuantity: number; limitUnitPrice: number; reservedRice: number; status: string; priorityAt: string; createdAt: string; updatedAt: string; expiresAt: string | null };
export type MarketDelivery = { deliveryId: string; instrumentId: string; orderId: string; tradeId: string | null; source: "BUY_FILL" | "SELL_CANCEL_RETURN" | "SELL_EXPIRE_RETURN"; itemId: string; instanceIds: string[]; quantity: number; displayName: string; createdAt: string; claimedAt: string | null };
export type MarketTrade = { tradeId: string; instrumentId: string; itemId: string; displayName: string; side: MarketOrderSide | null; quantity: number; unitPrice: number; totalPrice: number; fee: number | null; settlementAmount: number | null; filledAt: string };
export type MarketUnreadState = { stream: MarketUnreadStream; latestSequence: number; readSequence: number; unreadCount: number };
export type MarketSummary = { activeOrders: number; closedOrders: number; recoveryReviewOrders: number; claimableDeliveries: number; claimableSettlements: number; unread: MarketUnreadState[] };
export type MarketPage<T> = { items: T[]; nextCursor: string | null; totalItems: number; readThroughSequence: number };
export type CommandResult<T> = { commandId: string; idempotencyKey: string; status: string; result: T };
export type MarketSettlementMail = { mailId: string; type: string; riceAmount: number; claimed: boolean; createdAt: string; claimedAt: string | null };

type Envelope<T> = { data: T };
type ErrorEnvelope = { code?: string; messageKey?: string };

async function json<T>(response: Response): Promise<T> {
  const body = await response.json().catch(() => ({})) as Envelope<T> & ErrorEnvelope;
  if (!response.ok) throw Object.assign(new Error(body.code || body.messageKey || `HTTP ${response.status}`), body);
  return body.data;
}
function commandHeaders(idempotencyKey: string = crypto.randomUUID()): HeadersInit { return { Accept: "application/json", "Content-Type": "application/json", "Idempotency-Key": idempotencyKey }; }
function query(params: Record<string, string | number | boolean | null | undefined>): string { const search = new URLSearchParams(); Object.entries(params).forEach(([key, value]) => { if (value !== null && value !== undefined && value !== "") search.set(key, String(value)); }); const value = search.toString(); return value ? `?${value}` : ""; }
function instrumentsQuery(ids: string[], cursor: string | null = null): string { const search = new URLSearchParams(); ids.forEach(id => search.append("instrumentId", id)); if (cursor) search.set("cursor", cursor); const value = search.toString(); return value ? `?${value}` : ""; }

export const marketApi = {
  priceLevels: (instrumentId: string, side: MarketOrderSide, afterUnitPrice: number | null = null, limit = 10) => fetch(`/api/v1/market/instruments/${instrumentId}/price-levels${query({ side, afterUnitPrice, limit })}`, { credentials: "include", headers: { Accept: "application/json" } }).then(json<MarketPriceLevels>),
  instruments: () => fetch("/api/v1/market/instruments", { credentials: "include", headers: { Accept: "application/json" } }).then(json<MarketInstrument[]>),
  candles: (instrumentId: string, interval: "5m" | "15m" | "30m" = "5m", period: "12h" | "1d" | "2d" = "12h") => fetch(`/api/v1/market/instruments/${instrumentId}/candles${query({ interval, period })}`, { credentials: "include", headers: { Accept: "application/json" } }).then(json<MarketCandles>),
  orderBook: (instrumentId: string, levels = 5) => fetch(`/api/v1/market/order-books/${instrumentId}?levels=${levels}`, { credentials: "include", headers: { Accept: "application/json" } }).then(json<MarketOrderBook>),
  quote: (instrumentId: string, side: MarketOrderSide, timeInForce: MarketTimeInForce, quantity: number, limitUnitPrice: number) => fetch(`/api/v1/market/instruments/${instrumentId}/order-quote${query({ side, timeInForce, quantity, limitUnitPrice })}`, { credentials: "include", headers: { Accept: "application/json" } }).then(json<MarketOrderQuote>),
  createOrder: (request: { instrumentId: string; side: MarketOrderSide; timeInForce: MarketTimeInForce; quantity: number; limitUnitPrice: number; instanceIds?: string[] }, idempotencyKey: string = crypto.randomUUID()) => fetch("/api/v1/market/orders", { method: "POST", credentials: "include", headers: commandHeaders(idempotencyKey), body: JSON.stringify(request) }).then(json<CommandResult<MarketOrderResult>>),
  orders: (scope: MarketOrderScope, side: MarketOrderSide | null = null, instrumentId: string | null = null, cursor: string | null = null) => fetch(`/api/v1/market/orders/me${query({ scope, side, instrumentId, cursor })}`, { credentials: "include", headers: { Accept: "application/json" } }).then(json<MarketPage<MarketOrder>>),
  updateOrder: (orderId: string, request: { quantity?: number; limitUnitPrice?: number }) => fetch(`/api/v1/market/orders/${orderId}`, { method: "PATCH", credentials: "include", headers: commandHeaders(), body: JSON.stringify(request) }).then(json<CommandResult<MarketOrderResult>>),
  cancelOrder: (orderId: string) => fetch(`/api/v1/market/orders/${orderId}/cancel`, { method: "POST", credentials: "include", headers: commandHeaders(), body: "{}" }).then(json<CommandResult<unknown>>),
  deliveries: (claimable: boolean | null = null, cursor: string | null = null) => fetch(`/api/v1/market/deliveries/me${query({ claimable, cursor })}`, { credentials: "include", headers: { Accept: "application/json" } }).then(json<MarketPage<MarketDelivery>>),
  claimDelivery: (deliveryId: string) => fetch(`/api/v1/market/deliveries/${deliveryId}/claim`, { method: "POST", credentials: "include", headers: commandHeaders(), body: "{}" }).then(json<CommandResult<unknown>>),
  claimAllDeliveries: () => fetch("/api/v1/market/deliveries/claim-all", { method: "POST", credentials: "include", headers: commandHeaders(), body: "{}" }).then(json<CommandResult<unknown>>),
  settlementMails: (cursor: string | null = null) => fetch(`/api/v1/mails${query({ claimable: true, cursor })}`, { credentials: "include", headers: { Accept: "application/json" } }).then(json<MarketPage<MarketSettlementMail>>),
  claimSettlement: (mailId: string) => fetch(`/api/v1/mails/${mailId}/claim`, { method: "POST", credentials: "include", headers: commandHeaders(), body: "{}" }).then(json<CommandResult<unknown>>),
  claimAllSettlements: () => fetch("/api/v1/mails/claim-all", { method: "POST", credentials: "include", headers: commandHeaders(), body: "{}" }).then(json<CommandResult<unknown>>),
  summary: () => fetch("/api/v1/market/summary/me", { credentials: "include", headers: { Accept: "application/json" } }).then(json<MarketSummary>),
  markRead: (stream: MarketUnreadStream, readThroughSequence: number) => fetch(`/api/v1/market/unread-cursors/${stream}`, { method: "POST", credentials: "include", headers: commandHeaders(), body: JSON.stringify({ readThroughSequence }) }).then(json<unknown>),
  trades: (instrumentIds: string[], mine: boolean, cursor: string | null = null) => fetch(`/api/v1/market/trades${mine ? "/me" : ""}${instrumentsQuery(instrumentIds, cursor)}`, { credentials: "include", headers: { Accept: "application/json" } }).then(json<MarketPage<MarketTrade>>),
};
