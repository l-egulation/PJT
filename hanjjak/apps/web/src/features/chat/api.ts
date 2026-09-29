export type ChatPrimaryMaterial = "POTATO" | "SWEET_POTATO" | "CORN";
export type ChatMessage = { messageId: string; accountId: string; nickname: string; primaryMaterialType?: ChatPrimaryMaterial | null; body: string; createdAt: string; eventId: string };
export type ChatPage = { items: ChatMessage[]; nextCursor: string | null; unreadCount: number };
export type ChatBoardPost = { postId: string; accountId: string; nickname: string; intent: "SELL" | "BUY"; itemId: string; quantity: number; unitPrice: number | null; body: string; status: string; createdAt: string; expiresAt: string };
export type ChatBoardPage = { items: ChatBoardPost[]; nextCursor: string | null };
export type ChatCommand<T> = { commandId: string; idempotencyKey: string; status: string; result: T };

async function json<T>(response: Response): Promise<T> {
  const body = await response.json().catch(() => ({})) as { data?: T; code?: string; messageKey?: string };
  if (!response.ok) throw new Error(body.code || body.messageKey || `HTTP ${response.status}`);
  return body.data as T;
}
function commandHeaders(): HeadersInit { return { Accept: "application/json", "Content-Type": "application/json", "Idempotency-Key": crypto.randomUUID() }; }
function cursorQuery(cursor: string | null): string { return cursor ? `?cursor=${encodeURIComponent(cursor)}` : ""; }

export const chatApi = {
  messages: (cursor: string | null = null) => fetch(`/api/v1/chat/messages${cursorQuery(cursor)}`, { credentials: "include", headers: { Accept: "application/json" } }).then(json<ChatPage>),
  send: (body: string) => fetch("/api/v1/chat/messages", { method: "POST", credentials: "include", headers: commandHeaders(), body: JSON.stringify({ body }) }).then(json<ChatCommand<{ message: ChatMessage; filterStatus: string }>>),
  board: (cursor: string | null = null) => fetch(`/api/v1/chat/board${cursorQuery(cursor)}`, { credentials: "include", headers: { Accept: "application/json" } }).then(json<ChatBoardPage>),
  createPost: (request: { intent: "SELL" | "BUY"; itemId: string; quantity: number; unitPrice: number | null; body: string }) => fetch("/api/v1/chat/board", { method: "POST", credentials: "include", headers: commandHeaders(), body: JSON.stringify(request) }).then(json<ChatCommand<{ post: ChatBoardPost; filterStatus: string }>>),
  closePost: (postId: string) => fetch(`/api/v1/chat/board/${postId}/close`, { method: "POST", credentials: "include", headers: commandHeaders(), body: "{}" }).then(json<ChatCommand<{ postId: string; status: string }>>),
  report: (targetType: "messages" | "board", targetId: string, reason: string) => fetch(`/api/v1/chat/${targetType}/${targetId}/reports`, { method: "POST", credentials: "include", headers: commandHeaders(), body: JSON.stringify({ reason }) }).then(json<ChatCommand<{ reportId: string; status: string }>>),
  block: (accountId: string) => fetch(`/api/v1/chat/blocks/${accountId}`, { method: "POST", credentials: "include", headers: commandHeaders(), body: "{}" }).then(json<ChatCommand<{ accountId: string; blocked: boolean }>>),
  unblock: (accountId: string) => fetch(`/api/v1/chat/blocks/${accountId}`, { method: "DELETE", credentials: "include", headers: commandHeaders(), body: "{}" }).then(json<ChatCommand<{ accountId: string; blocked: boolean }>>),
  blocks: () => fetch("/api/v1/chat/blocks", { credentials: "include", headers: { Accept: "application/json" } }).then(json<{ accountId: string; nickname: string; createdAt: string }[]>),
};
