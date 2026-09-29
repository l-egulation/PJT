import { ApiError } from "@hanjjak/client-sdk";

const ERROR_LABELS: Record<string, string> = {
  ADMIN_AUTHENTICATION_REQUIRED: "운영 세션이 만료되었습니다. 다시 로그인해 주세요.",
  ADMIN_GITLAB_NOT_CONFIGURED: "GitLab 로그인이 아직 설정되지 않았습니다.",
  ADMIN_GITLAB_AUTHORIZATION_DENIED: "GitLab 로그인이 취소되었습니다.",
  ADMIN_GITLAB_STATE_INVALID: "로그인 요청이 만료되었습니다. 다시 시도해 주세요.",
  ADMIN_GITLAB_ACCESS_DENIED: "허용된 GitLab 계정이 아닙니다.",
  ADMIN_GITLAB_RESPONSE_INVALID: "GitLab 사용자 정보를 확인하지 못했습니다.",
  ADMIN_GITLAB_LOGIN_FAILED: "GitLab 로그인을 완료하지 못했습니다.",
  ADMIN_GITLAB_IDENTITY_CONFLICT: "GitLab 계정 연결 상태를 관리자에게 확인해 주세요.",
  ADMIN_PERMISSION_DENIED: "이 화면을 조회할 권한이 없습니다.",
  ADMIN_RATE_LIMITED: "로그인 시도가 너무 많습니다. 잠시 후 다시 시도해 주세요.",
  ADMIN_ORIGIN_FORBIDDEN: "허용되지 않은 운영 콘솔 주소입니다.",
  ADMIN_INVALID_DATE_RANGE: "조회 기간은 90일 이내로 선택해 주세요.",
  ADMIN_MARKET_REASON_REQUIRED: "운영 사유를 3자 이상 입력해 주세요.",
  ADMIN_MARKET_TOO_MANY_LISTINGS: "한 번에 만들 수 있는 매물 수를 초과했습니다.",
  ITEM_NOT_FOUND: "존재하지 않는 아이템입니다.",
  ITEM_NOT_TRADEABLE: "거래소에 등록할 수 없는 아이템입니다.",
  MARKET_ORDER_NOT_FOUND: "주문을 찾을 수 없습니다.",
  MARKET_ORDER_NOT_ACTIVE: "현재 조치할 수 없는 주문입니다.",
  SYSTEM_ORDER_NOT_PURCHASABLE: "시스템 주문은 시스템이 다시 구매할 수 없습니다.",
  INVALID_QUANTITY: "수량을 확인해 주세요.",
  INVALID_UNIT_PRICE: "단가는 10~999,999쌀 범위로 입력해 주세요.",
  ADMIN_USER_REASON_REQUIRED: "운영 사유를 3자 이상 입력해 주세요.",
  ADMIN_USER_CHANGE_REQUIRED: "현재 상태와 다른 값을 입력해 주세요.",
  ADMIN_ITEM_BELOW_RESERVED_QUANTITY: "거래소 예약 수량보다 적게 설정할 수 없습니다.",
  ADMIN_COSMETIC_BELOW_RESERVED_QUANTITY: "거래 예약 중인 치장 수량보다 적게 설정할 수 없습니다.",
  ADMIN_INSTANCE_ITEM_REQUIRES_GEM_COMMAND: "인스턴스 아이템은 보석 관리 기능을 사용해 주세요.",
  ADMIN_INSUFFICIENT_ADJUSTABLE_GEMS: "장착·판매 예약되지 않은 보석 수량이 부족합니다.",
  INVALID_LEVEL: "레벨은 1~500 범위여야 합니다.",
  INVALID_EXPERIENCE: "누적 경험치가 선택 레벨 범위와 맞지 않습니다.",
  INVALID_RICE_AMOUNT: "쌀 수량은 0 이상이어야 합니다.",
  INVALID_GEM_ADJUSTMENT: "보석 레벨과 조정 수량을 확인해 주세요.",
  INVALID_GEM_OPTION: "해당 레벨에서 사용할 수 없는 보석 옵션입니다.",
  INVALID_COSMETIC_QUANTITY: "치장 등록·미등록 수량을 확인해 주세요.",
  INVALID_EQUIPMENT_STATE: "장비 등급과 강화 단계를 확인해 주세요.",
  INVENTORY_CAPACITY_EXCEEDED: "인벤토리 200슬롯을 초과합니다.",
  COSMETIC_NOT_FOUND: "존재하지 않는 치장입니다.",
  STAGE_NOT_FOUND: "존재하지 않는 스테이지입니다.",
};

export function adminErrorMessage(error: unknown): string {
  if (error instanceof ApiError) return ERROR_LABELS[error.code] ?? `요청을 처리하지 못했습니다. (${error.code})`;
  return "운영 API에 연결할 수 없습니다. 서버 상태를 확인해 주세요.";
}

export function adminAuthErrorFromHash(hash: string): string {
  const code = new URLSearchParams(hash.replace(/^#/, "")).get("admin-auth-error");
  return code ? ERROR_LABELS[code] ?? "GitLab 로그인을 완료하지 못했습니다." : "";
}

export function defaultEconomyRange(now: Date): { from: string; to: string } {
  const to = now.toISOString().slice(0, 10);
  const from = new Date(now.getTime() - 6 * 86_400_000).toISOString().slice(0, 10);
  return { from, to };
}
