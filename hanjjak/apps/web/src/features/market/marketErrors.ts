const ERROR_MESSAGES: Record<string, string> = {
  INSUFFICIENT_RICE: "쌀이 부족합니다.",
  MARKET_NO_FILL: "현재 가격 조건으로 체결할 주문이 없습니다.",
  SELF_CROSS_NOT_ALLOWED: "내 매수 주문과 가격이 겹쳐 매도할 수 없습니다.",
  MARKET_ACTIVE_ORDER_LIMIT: "활성 주문은 최대 30개까지 등록할 수 있습니다.",
  MARKET_MUTATION_RATE_LIMITED: "주문 변경이 너무 잦습니다. 잠시 후 다시 시도하세요.",
  MARKET_INSTRUMENT_INACTIVE: "현재 거래가 중단된 품목입니다.",
  INVENTORY_CAPACITY_EXCEEDED: "인벤토리 공간이 부족합니다.",
  LISTING_SOLD_OUT: "구매 가능한 매물이 없습니다.",
};

/*
 * 서버가 남긴 말을 그대로 띄우면 "HTTP 403" 같은 것이 화면에 뜬다. 그걸 읽고 무엇을
 * 해야 하는지 아는 사람은 만든 사람뿐이다. 아는 까닭은 우리말로 바꿔 적고, 모르는
 * 것은 그 자리에서 무엇을 하려다 실패했는지 말해 주는 문장으로 대신한다.
 */
export function marketErrorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof Error)) return fallback;
  const known = ERROR_MESSAGES[error.message.toUpperCase()];
  if (known) return known;
  return /[가-힣]/.test(error.message) ? error.message : fallback;
}
