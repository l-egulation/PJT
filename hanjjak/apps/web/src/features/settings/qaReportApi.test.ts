// @vitest-environment happy-dom
import { afterEach, expect, it, vi } from "vitest";
import { submitQaReport } from "./qaReportApi";

afterEach(() => vi.restoreAllMocks());

it("submits text, viewport context and an optional screenshot with an idempotency key", async () => {
  const fetch = vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response(JSON.stringify({ data: { reportId: "report-1", status: "RECEIVED" } }), {
    status: 200,
    headers: { "Content-Type": "application/json" },
  }));
  vi.spyOn(crypto, "randomUUID").mockReturnValue("11111111-1111-4111-8111-111111111111");

  await submitQaReport({
    description: "보석 창이 열리지 않아요",
    pageUrl: "https://game.example/battle",
    userAgent: "qa-browser",
    viewportWidth: 1280,
    viewportHeight: 720,
    imageDataUrl: "data:image/png;base64,AQID",
  });

  expect(fetch).toHaveBeenCalledWith("/api/v1/qa/reports", expect.objectContaining({
    method: "POST",
    credentials: "include",
    headers: expect.objectContaining({ "Idempotency-Key": "11111111-1111-4111-8111-111111111111" }),
  }));
  expect(JSON.parse(String(fetch.mock.calls[0]?.[1]?.body))).toMatchObject({
    description: "보석 창이 열리지 않아요",
    viewportWidth: 1280,
    viewportHeight: 720,
    imageDataUrl: "data:image/png;base64,AQID",
  });
});
