// @vitest-environment happy-dom
import { fireEvent, render, screen } from "@testing-library/react";
import { expect, it, vi } from "vitest";
import type { Banner, Catalog } from "./api";
import { CosmeticSelectorScreen } from "./CosmeticSelectorScreen";

const banner: Banner = {
  bannerId: "banner-1",
  setId: "set-1",
  displayName: "첫 번째 세트",
  singleRiceCost: 5_000,
  ticketBalance: 0,
  riceBalance: 0,
  oneDraw: { ticketCost: 0, riceCost: 5_000, executable: false },
  tenDraw: { ticketCost: 0, riceCost: 50_000, executable: false },
  milestone: { totalSuccessfulDraws: 200, claimedBoxCount: 1, claimableBoxCount: 0, drawsUntilNextBox: 200, boxItemId: "box-1", ownedBoxQuantity: 1 },
};

const catalog: Catalog = {
  contentVersion: "test",
  cosmetics: [
    { cosmeticId: "legend-1-head", displayName: "첫 모자", imageUrl: null, grade: "LEGENDARY", slot: "HEAD", setId: "set-1" },
    { cosmeticId: "legend-1-cape", displayName: "첫 망토", imageUrl: null, grade: "LEGENDARY", slot: "CAPE", setId: "set-1" },
    { cosmeticId: "legend-2-head", displayName: "둘째 모자", imageUrl: null, grade: "LEGENDARY", slot: "HEAD", setId: "set-2" },
    { cosmeticId: "rare-1", displayName: "희귀 모자", imageUrl: null, grade: "RARE", slot: "HEAD", setId: "set-3" },
  ],
  sets: [
    { setId: "set-1", displayName: "첫 번째 세트", grade: "LEGENDARY", members: {}, effects: {} },
    { setId: "set-2", displayName: "두 번째 세트", grade: "LEGENDARY", members: {}, effects: {} },
    { setId: "set-3", displayName: "희귀 세트", grade: "RARE", members: {}, effects: {} },
  ],
};

it("lists every legendary set before narrowing the choice to one set's parts", () => {
  const onSelect = vi.fn();
  render(<CosmeticSelectorScreen banner={banner} catalog={catalog} onBack={() => {}} onSelect={onSelect} />);

  expect(screen.getByRole("button", { name: "첫 번째 세트 선택" })).toBeTruthy();
  expect(screen.getByRole("button", { name: "두 번째 세트 선택" })).toBeTruthy();
  expect(screen.queryByRole("button", { name: "희귀 세트 선택" })).toBeNull();

  fireEvent.click(screen.getByRole("button", { name: "두 번째 세트 선택" }));
  expect(screen.getByRole("heading", { name: "받을 치장 선택" })).toBeTruthy();
  expect(screen.getByRole("button", { name: /둘째 모자/ })).toBeTruthy();
  expect(screen.queryByRole("button", { name: /첫 모자/ })).toBeNull();

  fireEvent.click(screen.getByRole("button", { name: /둘째 모자/ }));
  fireEvent.click(screen.getByRole("button", { name: "확정하기" }));
  expect(onSelect).toHaveBeenCalledWith("legend-2-head");
});
