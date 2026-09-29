import { describe, expect, it, vi } from "vitest";
import { chatApi } from "./api";

describe("chat api", () => {
  it("sends messages with an idempotency key", async () => {
    const fetcher = vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response(JSON.stringify({ data: { result: { messageId: "m1" } } }), { status: 200, headers: { "Content-Type": "application/json" } }));
    await chatApi.send("hello");
    expect(fetcher.mock.calls[0][0]).toBe("/api/v1/chat/messages");
    expect((fetcher.mock.calls[0][1]?.headers as Record<string, string>)["Idempotency-Key"]).toBeTruthy();
    fetcher.mockRestore();
  });

  it("uses separate board and report endpoints", async () => {
    const fetcher = vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response(JSON.stringify({ data: { items: [] } }), { status: 200, headers: { "Content-Type": "application/json" } }));
    await chatApi.board();
    await chatApi.report("board", "p1", "SPAM");
    expect(fetcher.mock.calls[0][0]).toBe("/api/v1/chat/board");
    expect(fetcher.mock.calls[1][0]).toBe("/api/v1/chat/board/p1/reports");
    fetcher.mockRestore();
  });
});
