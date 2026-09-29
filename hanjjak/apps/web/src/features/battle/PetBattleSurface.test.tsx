// @vitest-environment happy-dom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, expect, it } from "vitest";
import { PetBattleSurface } from "./PetBattleSurface";
import { useBattleRuntimeStore } from "./runtimeStore";
import { usePictureInPictureSessionStore } from "../picture-in-picture/pictureInPictureSessionStore";
import { DEFAULT_PIP_TRACKED_ITEM_IDS } from "../picture-in-picture/trackedItems";

const account = { accountId: "account-1", characterId: "character-1", email: "test@example.com", nickname: "한짝", level: 1, experience: 0, rice: 12_345 };

function renderSurface() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: Number.POSITIVE_INFINITY } } });
  client.setQueryData(["auth", "session"], { authenticated: true, account });
  for (const itemId of DEFAULT_PIP_TRACKED_ITEM_IDS) client.setQueryData(["inventory", "item", itemId], { itemId, totalQuantity: 10 });
  useBattleRuntimeStore.setState({ connected: true, selectedStageId: "stage.02-06" });
  usePictureInPictureSessionStore.getState().start();
  render(<QueryClientProvider client={client}><PetBattleSurface /></QueryClientProvider>);
}

afterEach(() => {
  cleanup();
  window.localStorage.clear();
  useBattleRuntimeStore.getState().reset();
  usePictureInPictureSessionStore.getState().stop();
});

it("shows the live account and stage data without level or experience", () => {
  renderSurface();
  expect(screen.getByText("12,345")).toBeTruthy();
  expect(screen.getByText("스테이지 2-6")).toBeTruthy();
  expect(screen.queryByText("레벨")).toBeNull();
  expect(screen.queryByText("경험치")).toBeNull();
});

it("keeps the selection at four and explains a fifth selection", () => {
  renderSurface();
  fireEvent.click(screen.getByRole("button", { name: "획득 표시 설정 열기" }));
  expect(screen.getByText("4 / 4 선택")).toBeTruthy();
  const fifth = screen.getByRole("checkbox", { name: /미니 감자/ }) as HTMLInputElement;
  fireEvent.click(fifth);
  expect(fifth.checked).toBe(false);
  expect(screen.getByText(/최대 4개까지만/)).toBeTruthy();
  expect(screen.getByText("4 / 4 선택")).toBeTruthy();
});
