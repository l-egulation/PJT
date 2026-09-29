import type { MarketOrder, MarketTrade } from "./api";

/*
 * The trade feed does not carry an order id yet, so a closed order's real
 * fills are reconstructed from my own trades: same instrument, same side and
 * inside the window the order was alive.  `exact` tells the screen whether the
 * reconstruction adds up to the order's filled quantity; when it does not we
 * show the order summary instead of pretending the breakdown is complete.
 */
export type OrderFill = {
  tradeId: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
  settlementAmount: number | null;
  filledAt: string;
};
export type OrderFillBreakdown = { fills: OrderFill[]; exact: boolean };

const MATCH_TOLERANCE_MS = 2_000;
const NATIVE_COUNTS = ["", "한", "두", "세", "네", "다섯", "여섯", "일곱", "여덟", "아홉", "열"];

function time(value: string): number {
  const parsed = new Date(value).getTime();
  return Number.isNaN(parsed) ? 0 : parsed;
}

export function reconstructOrderFills(order: MarketOrder, trades: MarketTrade[]): OrderFillBreakdown {
  if (order.filledQuantity <= 0) return { fills: [], exact: true };
  const from = time(order.createdAt) - MATCH_TOLERANCE_MS;
  const until = time(order.updatedAt) + MATCH_TOLERANCE_MS;
  const fills: OrderFill[] = [];
  let matched = 0;
  for (const trade of trades
    .filter(trade => trade.instrumentId === order.instrumentId && trade.side === order.side && time(trade.filledAt) >= from && time(trade.filledAt) <= until)
    .sort((a, b) => time(a.filledAt) - time(b.filledAt))) {
    if (matched + trade.quantity > order.filledQuantity) break;
    fills.push({ tradeId: trade.tradeId, quantity: trade.quantity, unitPrice: trade.unitPrice, totalPrice: trade.totalPrice, settlementAmount: trade.settlementAmount, filledAt: trade.filledAt });
    matched += trade.quantity;
  }
  return { fills, exact: matched === order.filledQuantity };
}

export function fillSummaryText(breakdown: OrderFillBreakdown, side: MarketOrder["side"]): string {
  const verb = side === "BUY" ? "구매" : "판매";
  /*
   * 맞춰 낸 기록이 주문 수량과 딱 떨어지지 않을 때가 있다. 오래된 거래는 기록이
   * 남아 있는 범위를 넘어간다. 영영 "불러오는 중"으로 두지 않고 찾은 만큼만 보여준다.
   */
  if (!breakdown.exact) {
    return breakdown.fills.length === 0
      ? "이 거래의 기록은 남아 있는 범위를 넘어갔어요."
      : `찾은 기록 ${breakdown.fills.length}건만 보여줘요. 나머지는 기록이 남아 있지 않아요.`;
  }
  if (breakdown.fills.length === 0) return "거래된 수량이 없어요.";
  if (breakdown.fills.length === 1) return `한 번에 ${verb}됐어요.`;
  const word = NATIVE_COUNTS[breakdown.fills.length];
  const count = word ? `${word} 번` : `${breakdown.fills.length}번`;
  return `${count}에 나누어 ${verb}됐어요.`;
}
