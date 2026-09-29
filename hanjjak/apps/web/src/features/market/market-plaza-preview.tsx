/*
 * A backendless harness for the market plaza draft, in the same spirit as
 * `battle-hud-preview`: it answers the market endpoints with fixtures so the
 * three tabs can be reviewed without a running game API.  Open
 * `/market-plaza-preview.html?tab=ledger` to land on a specific tab.
 */
import React from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MarketPlaza } from "./MarketPlaza";
import { marketPlazaFixtures, marketPlazaFixtureResponse } from "./marketPlazaFixtures";
import "../../styles.css";

export function installMarketFixtureFetch(): void {
  const original = window.fetch.bind(window);
  window.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = typeof input === "string" ? input : input instanceof URL ? input.toString() : input.url;
    const fixture = marketPlazaFixtureResponse(url, init?.method ?? "GET");
    if (fixture === undefined) return original(input as RequestInfo, init);
    return new Response(JSON.stringify({ data: fixture }), { status: 200, headers: { "Content-Type": "application/json" } });
  }) as typeof window.fetch;
}

function MarketPlazaPreview() {
  const initialMode = new URLSearchParams(window.location.search).get("mode") === "sell" ? "sell" as const : "buy" as const;
  /* Stands in for the battle stage the market normally opens over, so the
     blurred backdrop has something to blur. */
  return <main className="game-shell market-plaza-preview-stage">
    <MarketPlaza initialMode={initialMode} onClose={() => undefined} />
  </main>;
}

if (typeof document !== "undefined" && document.getElementById("root")) {
  installMarketFixtureFetch();
  window.sessionStorage.setItem("hanjjak.market-ui", JSON.stringify({ selectedItemId: marketPlazaFixtures.instruments[0].itemId }));
  createRoot(document.getElementById("root")!).render(
    <React.StrictMode>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <MarketPlazaPreview />
      </QueryClientProvider>
    </React.StrictMode>,
  );
}
