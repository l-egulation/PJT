import { renderToStaticMarkup } from "react-dom/server";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { expect, it } from "vitest";
import { CharacterWindow } from "./CharacterWindow";

it("renders ability stats by default and a locked tab without exposing cosmetic inventory", () => {
  const client = new QueryClient();
  client.setQueryData(["character-stats"], { nickname: "용사", level: 1, experience: 0, cosmeticsUnlocked: false, contentVersion: "v1", stats: [] });
  const render = (initialTab?: "stats" | "cosmetics") => renderToStaticMarkup(<QueryClientProvider client={client}><CharacterWindow open initialTab={initialTab} onClose={() => {}} onGacha={() => {}} /></QueryClientProvider>);
  const html = render();
  expect(html).toContain('aria-selected="true"');
  expect(html).toContain('aria-label="캐릭터 능력치"');
  expect(render("cosmetics")).toContain("1-5 최초 클리어");
  expect(client.getQueryData(["cosmetic-collection"])).toBeUndefined();
  client.clear();
});
