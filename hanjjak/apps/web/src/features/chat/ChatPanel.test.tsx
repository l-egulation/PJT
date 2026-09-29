// @vitest-environment happy-dom

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { renderToStaticMarkup } from "react-dom/server";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { afterEach, describe, expect, it, vi } from "vitest";
import { chatApi, type ChatMessage } from "./api";
import { ChatPanel } from "./ChatPanel";

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe("ChatPanel", () => {
  it("uses one simple chat composer instead of sales and purchase tabs", () => {
    const client = new QueryClient({ defaultOptions: { queries: { staleTime: Number.POSITIVE_INFINITY, retry: false } } });
    client.setQueryData(["chat", "messages"], { items: [], nextCursor: null, unreadCount: 0 });
    const html = renderToStaticMarkup(<QueryClientProvider client={client}><ChatPanel /></QueryClientProvider>);
    expect(html).toContain("광장");
    expect(html).toContain("채팅 입력");
    expect(html).toContain("메시지를 입력하세요");
    expect(html).not.toContain("거래 게시판");
    expect(html).not.toContain("판매");
    expect(html).not.toContain("구매");
  });

  it("collapses to the compact plaza button and opens the composer again", () => {
    const client = new QueryClient({ defaultOptions: { queries: { staleTime: Number.POSITIVE_INFINITY, retry: false } } });
    client.setQueryData(["chat", "messages"], { items: [], nextCursor: null, unreadCount: 3 });
    render(<QueryClientProvider client={client}><ChatPanel /></QueryClientProvider>);

    const collapse = screen.getByRole("button", { name: "채팅 접기" });
    expect(collapse.getAttribute("aria-expanded")).toBe("true");
    fireEvent.click(collapse);

    expect(screen.queryByRole("textbox", { name: "채팅 입력" })).toBeNull();
    const open = screen.getByRole("button", { name: /광장/ });
    expect(open.getAttribute("aria-expanded")).toBe("false");
    expect(open.textContent).toContain("3");

    fireEvent.click(open);
    expect(screen.getByRole("textbox", { name: "채팅 입력" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "채팅 접기" }).getAttribute("aria-expanded")).toBe("true");
  });

  it("updates the message cache from a WebSocket event and disables polling while connected", () => {
    const sockets: Array<{ onopen?: () => void; onclose?: () => void; onerror?: () => void; onmessage?: (event: MessageEvent) => void; close: () => void }> = [];
    vi.stubGlobal("WebSocket", class {
      onopen?: () => void;
      onclose?: () => void;
      onerror?: () => void;
      onmessage?: (event: MessageEvent) => void;
      close = vi.fn();
      constructor() { sockets.push(this); }
    });
    const client = new QueryClient({ defaultOptions: { queries: { staleTime: Number.POSITIVE_INFINITY, retry: false } } });
    client.setQueryData(["chat", "messages"], { items: [], nextCursor: null, unreadCount: 0 });
    render(<QueryClientProvider client={client}><ChatPanel /></QueryClientProvider>);
    sockets[0].onopen?.();
    sockets[0].onmessage?.(new MessageEvent("message", { data: JSON.stringify({ type: "message", message: { messageId: "m1", accountId: "a1", nickname: "한짝", body: "실시간", createdAt: "2026-09-11T00:00:00Z", eventId: "e1" } }) }));
    expect(client.getQueryData<{ items: ChatMessage[] }>(["chat", "messages"])?.items[0].body).toBe("실시간");
    expect(sockets[0].close).not.toHaveBeenCalled();
  });

  it("clears the composer before sending and exposes blocked-account management", () => {
    const client = new QueryClient({ defaultOptions: { queries: { staleTime: Number.POSITIVE_INFINITY, retry: false } } });
    client.setQueryData(["chat", "messages"], { items: [], nextCursor: null, unreadCount: 0 });
    vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response(JSON.stringify({ data: { items: [] } }), { status: 200, headers: { "Content-Type": "application/json" } }));
    render(<QueryClientProvider client={client}><ChatPanel /></QueryClientProvider>);
    const blockManagement = screen.getByRole("button", { name: /차단 관리/ });
    expect(blockManagement.getAttribute("aria-expanded")).toBe("false");
    fireEvent.click(blockManagement);
    expect(blockManagement.getAttribute("aria-expanded")).toBe("true");
    const input = screen.getByRole("textbox", { name: "채팅 입력" });
    fireEvent.change(input, { target: { value: "hello" } });
    fireEvent.click(screen.getByRole("button", { name: "채팅 보내기" }));
    expect((input as HTMLInputElement).value).toBe("");
  });

  it("does not show a send error when message submission succeeds", async () => {
    const client = new QueryClient({ defaultOptions: { queries: { staleTime: Number.POSITIVE_INFINITY, retry: false } } });
    client.setQueryData(["chat", "messages"], { items: [], nextCursor: null, unreadCount: 0 });
    vi.spyOn(chatApi, "send").mockResolvedValue({ commandId: "c1", idempotencyKey: "k1", status: "SUCCEEDED", result: { message: { messageId: "m1", accountId: "a1", nickname: "한짝", body: "hello", createdAt: "2026-09-11T00:00:00Z", eventId: "e1" }, filterStatus: "ALLOW" } });
    vi.spyOn(chatApi, "messages").mockRejectedValue(new Error("refresh failed"));
    render(<QueryClientProvider client={client}><ChatPanel /></QueryClientProvider>);
    fireEvent.change(screen.getByRole("textbox", { name: "채팅 입력" }), { target: { value: "hello" } });
    fireEvent.click(screen.getByRole("button", { name: "채팅 보내기" }));
    await waitFor(() => expect(screen.queryByText("채팅을 보내지 못했어요. 잠시 후 다시 시도해 주세요.")).toBeNull());
    expect(client.getQueryData<{ items: ChatMessage[] }>(["chat", "messages"])?.items[0].body).toBe("hello");
  });

  it("aligns the desktop panel with the bottom navigation baseline", () => {
    const css = readFileSync(resolve(process.cwd(), "src/features/chat/ChatPanel.css"), "utf8");
    expect(css).toContain("bottom: 14px");
    expect(css).toContain(".chat-panel { bottom: 8px; left: 8px; }");
  });
});
