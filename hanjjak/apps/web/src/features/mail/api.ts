export type MailMessage = {
  mailId: string;
  type: string;
  riceAmount: number;
  claimed: boolean;
  createdAt: string;
  claimedAt: string | null;
};
export type MailPage = { items: MailMessage[]; nextCursor: string | null; totalItems: number };
export type MailClaimResult = { mailId: string; riceAmount: number; claimedAt: string; walletBalance: number };
export type MailClaimAllResult = { claimedMailIds: string[]; totalRiceAmount: number; walletBalance: number };
type Envelope<T> = { data: T };
type CommandResult<T> = { commandId: string; idempotencyKey: string; status: string; result: T };
type ErrorEnvelope = { code?: string; messageKey?: string };

export class MailApiError extends Error {
  constructor(public status: number, public code: string) { super(code); }
}

async function json<T>(response: Response): Promise<T> {
  const body = await response.json().catch(() => ({})) as Envelope<T> & ErrorEnvelope;
  if (!response.ok) throw new MailApiError(response.status, body.code || body.messageKey || `HTTP_${response.status}`);
  return body.data;
}

/* 받기는 두 번 눌러도 한 번만 들어가야 한다. 서버가 키로 같은 명령을 알아본다. */
function commandHeaders(key: string): HeadersInit {
  return { Accept: "application/json", "Content-Type": "application/json", "Idempotency-Key": key };
}

export const mailApi = {
  list: (claimable?: boolean, cursor?: string | null) => {
    const search = new URLSearchParams();
    if (claimable !== undefined) search.set("claimable", String(claimable));
    if (cursor) search.set("cursor", cursor);
    const query = search.toString();
    return fetch(`/api/v1/mails${query ? `?${query}` : ""}`, { credentials: "include", headers: { Accept: "application/json" } }).then(json<MailPage>);
  },
  claim: (mailId: string, key: string) => fetch(`/api/v1/mails/${mailId}/claim`, {
    method: "POST", credentials: "include", headers: commandHeaders(key),
  }).then(json<CommandResult<MailClaimResult>>),
  claimAll: (key: string) => fetch("/api/v1/mails/claim-all", {
    method: "POST", credentials: "include", headers: commandHeaders(key),
  }).then(json<CommandResult<MailClaimAllResult>>),
};

const TYPE_LABELS: Record<string, string> = {
  MARKET_SETTLEMENT: "거래소 판매 정산",
};

/** 메일 종류를 사람 말로. 모르는 종류가 와도 빈 줄을 남기지 않는다. */
export function mailTypeLabel(type: string): string {
  return TYPE_LABELS[type] ?? "보관함 지급";
}

/** 받은 날짜는 "몇 월 며칠"이면 충분하다. 초까지 적으면 읽을 것만 늘어난다. */
export function mailDateLabel(isoDate: string): string {
  const date = new Date(isoDate);
  if (Number.isNaN(date.getTime())) return "";
  return `${date.getMonth() + 1}월 ${date.getDate()}일`;
}
