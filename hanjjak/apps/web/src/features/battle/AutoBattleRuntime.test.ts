import { QueryClient } from "@tanstack/react-query";
import { expect, it, vi } from "vitest";
import { invalidateBattleCompletionQueries } from "./AutoBattleRuntime";

it("invalidates the pending reward inbox after battle completion", async () => {
  const client = new QueryClient();
  const invalidate = vi.spyOn(client, "invalidateQueries");

  await invalidateBattleCompletionQueries(client);

  expect(invalidate).toHaveBeenCalledWith({ queryKey: ["first-clear-rewards"] });
  expect(invalidate).toHaveBeenCalledWith({ queryKey: ["stages"] });
  expect(invalidate).toHaveBeenCalledWith({ queryKey: ["inventory"] });
});
