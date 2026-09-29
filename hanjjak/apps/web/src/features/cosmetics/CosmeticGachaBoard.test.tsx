// @vitest-environment happy-dom
import { fireEvent, render, screen, within } from "@testing-library/react";
import { expect, it, vi } from "vitest";
import type { Banner, Catalog } from "./api";
import { CosmeticGachaBoard, CosmeticWalletBalance } from "./CosmeticGachaBoard";

const banner: Banner = {
  bannerId: "banner-01", setId: "set-01", displayName: null, singleRiceCost: 5000,
  ticketBalance: 4, riceBalance: 50_000,
  oneDraw: { ticketCost: 1, riceCost: 5000, executable: true },
  tenDraw: { ticketCost: 10, riceCost: 50_000, executable: false },
  milestone: { totalSuccessfulDraws: 7, claimedBoxCount: 0, claimableBoxCount: 0, drawsUntilNextBox: 193, boxItemId: "box-01", ownedBoxQuantity: 0 },
};
const catalog: Catalog = { contentVersion: "v2", cosmetics: [], sets: [] };

it("renders server balances in the wallet row", () => {
  render(<CosmeticWalletBalance banner={banner} />);
  const wallet = screen.getByLabelText("보유 재화");
  expect(within(wallet).getByText("4", { selector: "strong" })).toBeTruthy();
  expect(within(wallet).getByText("50,000", { selector: "strong" })).toBeTruthy();
});

it("renders executable draw estimates without a probability action", () => {
  const onDraw = vi.fn();
  render(<CosmeticGachaBoard banners={[banner]} catalog={catalog} onDraw={onDraw} />);
  expect(screen.getByRole("button", { name: /1회/ })).not.toHaveProperty("disabled", true);
  expect(screen.getByRole("button", { name: /10회/ })).toHaveProperty("disabled", true);
  fireEvent.click(screen.getByRole("button", { name: /1회/ }));
  expect(onDraw).toHaveBeenCalledWith(banner, 1);
  expect(screen.queryByRole("button", { name: /확률/ })).toBeNull();
});

it("keeps the selector box locked until the milestone is reached", () => {
  const onDraw = vi.fn();
  const onOpenSelector = vi.fn();
  const almostReady = { ...banner, milestone: { ...banner.milestone, totalSuccessfulDraws: 199, drawsUntilNextBox: 1 } };
  render(<CosmeticGachaBoard banners={[almostReady]} catalog={catalog} onDraw={onDraw} onOpenSelector={onOpenSelector} />);

  // 상자가 모이기 전에는 누를 것이 없다. 뽑기는 위의 뽑기 버튼으로만 한다.
  const box = screen.getByRole("button", { name: "선택 상자까지 1회 남음" });
  expect((box as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(box);
  expect(onDraw).not.toHaveBeenCalled();
  expect(onOpenSelector).not.toHaveBeenCalled();
});

it("opens the selector once a box is owned", () => {
  const onOpenSelector = vi.fn();
  const withBox = { ...banner, milestone: { ...banner.milestone, drawsUntilNextBox: 200, ownedBoxQuantity: 1 } };
  render(<CosmeticGachaBoard banners={[withBox]} catalog={catalog} onDraw={vi.fn()} onOpenSelector={onOpenSelector} />);

  fireEvent.click(screen.getByRole("button", { name: "선택 상자 열고 치장 선택하기" }));
  expect(onOpenSelector).toHaveBeenCalledWith(withBox);
});
