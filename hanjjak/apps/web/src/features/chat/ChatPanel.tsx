import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState, type CSSProperties, type PointerEvent as ReactPointerEvent } from "react";
import { authApi } from "../auth/api";
import { chatApi, type ChatMessage, type ChatPage, type ChatPrimaryMaterial } from "./api";
import "./ChatPanel.css";

const WARNING = "욕설·성희롱·사기성 거래 유도는 금지됩니다.";
const CHAT_HEIGHT_KEY = "hanjjak.chat.height";
const CHAT_OPACITY_KEY = "hanjjak.chat.opacity";
const MIN_CHAT_HEIGHT = 240;
const DEFAULT_CHAT_HEIGHT = 330;

const MATERIAL_LABEL: Record<ChatPrimaryMaterial, string> = {
  POTATO: "감자 전문",
  SWEET_POTATO: "고구마 전문",
  CORN: "옥수수 전문",
};

function formatChatTime(createdAt: string): string {
  return new Intl.DateTimeFormat("ko-KR", {
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
    timeZone: "Asia/Seoul",
  }).format(new Date(createdAt));
}

function storedNumber(key: string, fallback: number, min: number, max: number): number {
  if (typeof window === "undefined") return fallback;
  const stored = window.localStorage.getItem(key);
  if (stored === null) return fallback;
  const value = Number(stored);
  return Number.isFinite(value) && value >= min && value <= max ? value : fallback;
}

function reportNotice(action: Promise<unknown>, onNotice: (text: string) => void, success: string) {
  void action.then(() => onNotice(success)).catch(() => onNotice("요청을 처리하지 못했습니다. 잠시 후 다시 시도해 주세요."));
}

export function ChatPanel() {
  const client = useQueryClient();
  const [open, setOpen] = useState(true);
  const [body, setBody] = useState("");
  const [notice, setNotice] = useState<string | null>(null);
  const [activeMenuId, setActiveMenuId] = useState<string | null>(null);
  const [panelHeight, setPanelHeight] = useState(() => storedNumber(CHAT_HEIGHT_KEY, DEFAULT_CHAT_HEIGHT, MIN_CHAT_HEIGHT, 900));
  const [paperOpacity, setPaperOpacity] = useState(() => storedNumber(CHAT_OPACITY_KEY, 92, 30, 100));
  const [showBlocks, setShowBlocks] = useState(false);
  const [socketReady, setSocketReady] = useState(false);
  const session = useQuery({ queryKey: ["auth", "session"], queryFn: authApi.session, retry: false });
  const messages = useQuery({ queryKey: ["chat", "messages"], queryFn: () => chatApi.messages(), refetchInterval: socketReady ? false : open ? 5_000 : false, retry: false });
  const blocked = useQuery({ queryKey: ["chat", "blocks"], queryFn: () => chatApi.blocks(), enabled: showBlocks, retry: false });
  const inputRef = useRef<HTMLInputElement>(null);
  const panelRef = useRef<HTMLElement>(null);
  const resizeRef = useRef<{ pointerId: number; startY: number; startHeight: number; maxHeight: number } | null>(null);

  function maximumPanelHeight(): number {
    const panel = panelRef.current;
    if (!panel) return DEFAULT_CHAT_HEIGHT;
    const panelRect = panel.getBoundingClientRect();
    const renderedScale = panel.offsetHeight > 0 ? panelRect.height / panel.offsetHeight : 1;
    const historyBottom = document.querySelector<HTMLElement>(".cozy-history-trigger")?.getBoundingClientRect().bottom ?? 176;
    return Math.max(MIN_CHAT_HEIGHT, Math.floor((panelRect.bottom - historyBottom - 8) / renderedScale));
  }

  function startResize(event: ReactPointerEvent<HTMLDivElement>) {
    event.preventDefault();
    event.currentTarget.setPointerCapture(event.pointerId);
    resizeRef.current = { pointerId: event.pointerId, startY: event.clientY, startHeight: panelHeight, maxHeight: maximumPanelHeight() };
  }

  function resizePanel(event: ReactPointerEvent<HTMLDivElement>) {
    const drag = resizeRef.current;
    if (!drag || drag.pointerId !== event.pointerId) return;
    const renderedScale = panelRef.current ? panelRef.current.getBoundingClientRect().height / panelRef.current.offsetHeight : 1;
    setPanelHeight(Math.min(drag.maxHeight, Math.max(MIN_CHAT_HEIGHT, drag.startHeight + (drag.startY - event.clientY) / renderedScale)));
  }

  function finishResize(event: ReactPointerEvent<HTMLDivElement>) {
    if (resizeRef.current?.pointerId !== event.pointerId) return;
    event.currentTarget.releasePointerCapture(event.pointerId);
    resizeRef.current = null;
  }

  useEffect(() => {
    if (typeof WebSocket === "undefined") return;
    let stopped = false;
    let retryTimer: ReturnType<typeof globalThis.setTimeout> | undefined;
    let socket: WebSocket | undefined;
    let retryDelay = 1_000;

    const connect = () => {
      if (stopped) return;
      const protocol = window.location.protocol === "https:" ? "wss:" : "ws:";
      socket = new WebSocket(`${protocol}//${window.location.host}/ws/chat`);
      socket.onopen = () => { retryDelay = 1_000; setSocketReady(true); };
      socket.onclose = () => {
        setSocketReady(false);
        if (!stopped) {
          retryTimer = setTimeout(connect, retryDelay);
          retryDelay = Math.min(retryDelay * 2, 30_000);
        }
      };
      socket.onerror = () => setSocketReady(false);
      socket.onmessage = (event) => {
        let payload: { type?: string; message?: ChatMessage };
        try { payload = JSON.parse(event.data) as { type?: string; message?: ChatMessage }; } catch { return; }
        if (payload.type !== "message" || !payload.message) return;
        client.setQueryData<ChatPage>(["chat", "messages"], (current) => {
          const items = [payload.message!, ...(current?.items ?? [])].filter((item, index, all) => all.findIndex((candidate) => candidate.messageId === item.messageId) === index);
          return { items: items.slice(0, 30), nextCursor: current?.nextCursor ?? null, unreadCount: current?.unreadCount ?? 0 };
        });
      };
    };
    connect();
    return () => {
      stopped = true;
      if (retryTimer) clearTimeout(retryTimer);
      socket?.close();
      setSocketReady(false);
    };
  }, [client]);

  useEffect(() => { if (open) inputRef.current?.focus(); }, [open]);
  useEffect(() => { window.localStorage.setItem(CHAT_HEIGHT_KEY, String(Math.round(panelHeight))); }, [panelHeight]);
  useEffect(() => { window.localStorage.setItem(CHAT_OPACITY_KEY, String(paperOpacity)); }, [paperOpacity]);
  useEffect(() => {
    const clampHeight = () => setPanelHeight((current) => Math.min(current, maximumPanelHeight()));
    window.addEventListener("resize", clampHeight);
    return () => window.removeEventListener("resize", clampHeight);
  }, []);

  async function send() {
    const value = body.trim();
    if (!value) return;
    setNotice(null);
    setBody("");
    try {
      const result = await chatApi.send(value);
      const message = result.result.message;
      client.setQueryData<ChatPage>(["chat", "messages"], (current) => {
        const items = [message, ...(current?.items ?? [])].filter((item, index, all) => all.findIndex((candidate) => candidate.messageId === item.messageId) === index);
        return { items: items.slice(0, 30), nextCursor: current?.nextCursor ?? null, unreadCount: current?.unreadCount ?? 0 };
      });
      void client.invalidateQueries({ queryKey: ["chat", "messages"] }).catch(() => undefined);
    } catch (error) {
      const code = error instanceof Error ? (error as Error & { code?: string }).code : undefined;
      setNotice(code === "BLOCKED_POLICY" ? "보낼 수 없는 표현이 포함되어 있어요." : code === "CHAT_BANNED" ? "채팅 이용이 제한된 계정입니다." : "채팅을 보내지 못했어요. 잠시 후 다시 시도해 주세요.");
    }
  }

  if (!open) return <aside className="chat-panel is-closed" aria-label="채팅">
    <button type="button" className="chat-open" onClick={() => setOpen(true)} aria-expanded={false}>
      <span className="chat-bubble-icon" aria-hidden="true"><i /><i /><i /></span>
      <span>광장</span>
      {(messages.data?.unreadCount ?? 0) > 0 && <b>{messages.data?.unreadCount}</b>}
    </button>
  </aside>;

  return <aside
    ref={panelRef}
    className="chat-panel is-open"
    aria-label="채팅"
    style={{ height: `${panelHeight}px`, "--chat-paper-opacity": paperOpacity / 100 } as CSSProperties}
  >
    <div
      className="chat-resize-handle"
      role="separator"
      aria-label="채팅창 높이 조절"
      aria-orientation="horizontal"
      tabIndex={0}
      onPointerDown={startResize}
      onPointerMove={resizePanel}
      onPointerUp={finishResize}
      onPointerCancel={finishResize}
      onKeyDown={(event) => {
        if (event.key !== "ArrowUp" && event.key !== "ArrowDown") return;
        event.preventDefault();
        setPanelHeight((current) => Math.min(maximumPanelHeight(), Math.max(MIN_CHAT_HEIGHT, current + (event.key === "ArrowUp" ? 16 : -16))));
      }}
    ><span /></div>
    <header className="chat-panel-header">
      <div className="chat-panel-title">
        <span className="chat-bubble-icon" aria-hidden="true"><i /><i /><i /></span>
        <h2>광장</h2>
        <button type="button" className="chat-info" aria-label="채팅 이용 안내" onClick={() => setNotice(WARNING)}>i</button>
      </div>
      <div className="chat-header-actions">
        <label className="chat-opacity-control">
          <span>배경</span>
          <input aria-label="채팅창 배경 투명도" type="range" min="30" max="100" step="1" value={paperOpacity} onChange={(event) => setPaperOpacity(Number(event.target.value))} />
        </label>
        <button type="button" className="chat-collapse" onClick={() => { setOpen(false); setActiveMenuId(null); }} aria-label="채팅 접기" aria-expanded={true}>−</button>
      </div>
    </header>
    {notice && <p className="chat-notice" role="status">{notice}<button type="button" aria-label="안내 닫기" onClick={() => setNotice(null)}>×</button></p>}
    <button
      type="button"
      className="chat-blocks-toggle"
      aria-expanded={showBlocks}
      aria-controls="chat-blocks-list"
      onClick={() => setShowBlocks((value) => !value)}
    ><span aria-hidden="true">☰</span> 차단 관리 <i aria-hidden="true">{showBlocks ? "▴" : "▾"}</i></button>
    {showBlocks && <div id="chat-blocks-list" className="chat-blocks" aria-label="차단 목록">{blocked.data?.length ? blocked.data.map((item) => <div key={item.accountId}><span>{item.nickname}</span><button type="button" onClick={() => void chatApi.unblock(item.accountId).then(() => client.invalidateQueries({ queryKey: ["chat", "blocks"] }))}>차단 해제</button></div>) : <span>차단한 사용자가 없습니다.</span>}</div>}
    <ChatMessages
      items={messages.data?.items ?? []}
      loading={messages.isLoading}
      ownAccountId={session.data?.account?.accountId ?? null}
      activeMenuId={activeMenuId}
      onOpenMenu={setActiveMenuId}
      onNotice={(text) => { setNotice(text); setActiveMenuId(null); }}
    />
    <div className="chat-composer">
      <div className="chat-input-shell">
        <input ref={inputRef} value={body} maxLength={240} placeholder="메시지를 입력하세요" aria-label="채팅 입력" onChange={(event) => setBody(event.target.value)} onKeyDown={(event) => { if (event.key === "Enter") void send(); }} />
        <span aria-hidden="true">{body.length} / 240</span>
      </div>
      <button type="button" className="chat-send" aria-label="채팅 보내기" disabled={!body.trim()} onClick={() => void send()}><span className="sr-only">보내기</span></button>
    </div>
  </aside>;
}

function ChatMessages({ items, loading, ownAccountId, activeMenuId, onOpenMenu, onNotice }: { items: ChatMessage[]; loading: boolean; ownAccountId: string | null; activeMenuId: string | null; onOpenMenu: (messageId: string | null) => void; onNotice: (text: string) => void }) {
  const listRef = useRef<HTMLOListElement>(null);
  const followLatestRef = useRef(true);
  /*
   * 사용자가 목록을 읽기 위해 위로 올렸다면 새 메시지가 와도 현재 위치를
   * 유지한다. 목록이 이미 하단에 붙어 있을 때만 최신 메시지를 따라간다.
   */
  const latestMessageId = items[0]?.messageId ?? null;

  useEffect(() => {
    if (!followLatestRef.current) return;
    const list = listRef.current;
    if (list) list.scrollTop = list.scrollHeight;
  }, [latestMessageId, loading]);

  if (loading) return <p className="chat-state" role="status">채팅을 불러오는 중…</p>;
  if (items.length === 0) return <p className="chat-state">첫 메시지를 남겨보세요.</p>;
  return <ol ref={listRef} className="chat-message-list" onScroll={(event) => {
    const list = event.currentTarget;
    followLatestRef.current = list.scrollHeight - list.scrollTop - list.clientHeight < 20;
  }}>{[...items].reverse().map((item) => <li key={item.messageId} className={item.primaryMaterialType ? `material-${item.primaryMaterialType.toLowerCase().replace("_", "-")}` : undefined}>
    <strong
      className={item.accountId === ownAccountId ? "is-own" : undefined}
      aria-label={item.primaryMaterialType ? `${item.nickname}, ${MATERIAL_LABEL[item.primaryMaterialType]}` : item.nickname}
      onContextMenu={(event) => {
        event.preventDefault();
        if (item.accountId === ownAccountId) {
          onOpenMenu(null);
          return;
        }
        onOpenMenu(item.messageId);
      }}
    >{item.nickname}</strong>
    <p>{item.body}</p>
    <time dateTime={item.createdAt}>{formatChatTime(item.createdAt)}</time>
    {activeMenuId === item.messageId && <div className="chat-item-actions">
      <button type="button" onClick={() => reportNotice(chatApi.report("messages", item.messageId, "OTHER"), onNotice, "신고를 접수했어요.")}>신고</button>
      <button type="button" onClick={() => reportNotice(chatApi.block(item.accountId), onNotice, "차단했어요. 내 화면에서 숨깁니다.")}>차단</button>
    </div>}
  </li>)}</ol>;
}
