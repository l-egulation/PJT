// @vitest-environment happy-dom

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ChatPanel } from "./ChatPanel";

const messages = [
  { messageId: "mine", accountId: "account-me", nickname: "내닉네임", primaryMaterialType: "POTATO" as const, body: "내 메시지", createdAt: "2026-09-11T19:00:00+09:00", eventId: "event-mine" },
  { messageId: "other", accountId: "account-other", nickname: "아주긴상대방닉네임", primaryMaterialType: "SWEET_POTATO" as const, body: "줄바꿈되어야 하는 아주 긴 상대방 메시지", createdAt: "2026-09-11T19:01:00+09:00", eventId: "event-other" },
];

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  window.localStorage.clear();
});

function renderChat() {
  vi.stubGlobal("WebSocket", class {
    close() {}
  });
  const client = new QueryClient({ defaultOptions: { queries: { staleTime: Number.POSITIVE_INFINITY, retry: false } } });
  client.setQueryData(["auth", "session"], { authenticated: true, account: { accountId: "account-me" } });
  client.setQueryData(["chat", "messages"], { items: messages, nextCursor: null, unreadCount: 0 });
  return render(<QueryClientProvider client={client}><ChatPanel /></QueryClientProvider>);
}

describe("ChatPanel interaction regressions", () => {
  it("opens report and block actions only from another player's nickname context menu", () => {
    renderChat();
    fireEvent.contextMenu(screen.getByText("내닉네임"));
    expect(screen.queryByRole("button", { name: "신고" })).toBeNull();

    fireEvent.contextMenu(screen.getByText("아주긴상대방닉네임"));
    expect(screen.getByRole("button", { name: "신고" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "차단" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: /메시지 메뉴/ })).toBeNull();
  });

  it("persists the user-selected paper opacity and exposes a height resize separator", () => {
    renderChat();
    const opacity = screen.getByRole("slider", { name: "채팅창 배경 투명도" });
    fireEvent.change(opacity, { target: { value: "57" } });
    expect(window.localStorage.getItem("hanjjak.chat.opacity")).toBe("57");
    expect(screen.getByRole("separator", { name: "채팅창 높이 조절" })).toBeTruthy();
  });

  it("shows 24-hour timestamps and distinguishes professions by nickname color", () => {
    const { container } = renderChat();
    expect(screen.getByText("19:00")).toBeTruthy();
    expect(container.querySelector(".material-potato strong")?.textContent).toBe("내닉네임");
    expect(container.querySelector(".material-sweet-potato strong")?.textContent).toBe("아주긴상대방닉네임");
  });
});

describe("chat follows the newest message", () => {
  /*
   * 목록은 서른 개에서 잘린다. 개수만 보고 따라가면 서른 개를 채운 뒤로
   * 새 글이 와도 다시 내려가지 않으므로, 현재 하단 고정 상태를 검증한다.
   */
  it("scrolls down when a new message arrives while already at the bottom", async () => {
    const client = new QueryClient({ defaultOptions: { queries: { staleTime: Number.POSITIVE_INFINITY, retry: false } } });
    vi.stubGlobal("WebSocket", class { close() {} });
    const capped = Array.from({ length: 30 }, (_, index) => ({
      messageId: `m${index}`, accountId: "account-other", nickname: "상대", primaryMaterialType: null,
      body: `메시지 ${index}`, createdAt: "2026-09-11T19:00:00+09:00", eventId: `e${index}`,
    }));
    client.setQueryData(["auth", "session"], { authenticated: true, account: { accountId: "account-me" } });
    client.setQueryData(["chat", "messages"], { items: capped, nextCursor: null, unreadCount: 0 });
    render(<QueryClientProvider client={client}><ChatPanel /></QueryClientProvider>);

    const list = document.querySelector<HTMLOListElement>(".chat-message-list")!;
    Object.defineProperty(list, "scrollHeight", { value: 900, configurable: true });
    Object.defineProperty(list, "clientHeight", { value: 300, configurable: true });
    list.scrollTop = 600;
    fireEvent.scroll(list);

    const next = [{ ...capped[0], messageId: "newest", body: "새 메시지" }, ...capped.slice(0, 29)];
    client.setQueryData(["chat", "messages"], { items: next, nextCursor: null, unreadCount: 0 });
    await waitFor(() => expect(list.scrollTop).toBe(900));

    expect(document.querySelectorAll(".chat-message-list li")).toHaveLength(30);
    expect(list.scrollTop).toBe(900);
  });

  it("keeps the reader position when a new message arrives away from the bottom", async () => {
    const client = new QueryClient({ defaultOptions: { queries: { staleTime: Number.POSITIVE_INFINITY, retry: false } } });
    vi.stubGlobal("WebSocket", class { close() {} });
    const capped = Array.from({ length: 30 }, (_, index) => ({
      messageId: `m${index}`, accountId: "account-other", nickname: "상대", primaryMaterialType: null,
      body: `메시지 ${index}`, createdAt: "2026-09-11T19:00:00+09:00", eventId: `e${index}`,
    }));
    client.setQueryData(["auth", "session"], { authenticated: true, account: { accountId: "account-me" } });
    client.setQueryData(["chat", "messages"], { items: capped, nextCursor: null, unreadCount: 0 });
    render(<QueryClientProvider client={client}><ChatPanel /></QueryClientProvider>);

    const list = document.querySelector<HTMLOListElement>(".chat-message-list")!;
    Object.defineProperty(list, "scrollHeight", { value: 900, configurable: true });
    Object.defineProperty(list, "clientHeight", { value: 300, configurable: true });
    list.scrollTop = 240;
    fireEvent.scroll(list);

    const next = [{ ...capped[0], messageId: "newest", accountId: "account-me", body: "내 새 메시지" }, ...capped.slice(0, 29)];
    client.setQueryData(["chat", "messages"], { items: next, nextCursor: null, unreadCount: 0 });
    await waitFor(() => expect(list.scrollTop).toBe(240));

    expect(list.scrollTop).toBe(240);
  });
});
