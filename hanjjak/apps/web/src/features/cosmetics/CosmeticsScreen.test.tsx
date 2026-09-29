// @vitest-environment happy-dom
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderToStaticMarkup } from "react-dom/server";
import { afterEach, expect, it, vi } from "vitest";
import { cosmeticsApi, CosmeticsApiError, type DrawResponse } from "./api";
import { CosmeticsScreen } from "./CosmeticsScreen";
import { CosmeticDrawReveal } from "./CosmeticDrawReveal";
import { cosmeticDisplayName } from "./cosmetic-art";

afterEach(() => { document.body.innerHTML = ""; vi.restoreAllMocks(); });

const banner = { bannerId: "cosmetic-banner-01", setId: "cosmetic-set-11", displayName: null, singleRiceCost: 5000, ticketBalance: 0, riceBalance: 50_000, oneDraw: { ticketCost: 0, riceCost: 5000, executable: true }, tenDraw: { ticketCost: 0, riceCost: 50_000, executable: true }, milestone: { totalSuccessfulDraws: 200, claimedBoxCount: 0, claimableBoxCount: 1, drawsUntilNextBox: 0, boxItemId: "cosmetic-selector-box-01", ownedBoxQuantity: 1 } };
const secondBanner = { ...banner, bannerId: "cosmetic-banner-02", setId: "cosmetic-set-12", singleRiceCost: 7000, ticketBalance: 2, riceBalance: 70_000, oneDraw: { ticketCost: 2, riceCost: 7000, executable: true }, tenDraw: { ticketCost: 20, riceCost: 70_000, executable: true }, milestone: { ...banner.milestone, boxItemId: "cosmetic-selector-box-02" } };
const collection = { contentVersion: "v2", states: [{ cosmeticId: "cosmetic-061", displayName: null, imageUrl: null, grade: "LEGENDARY", slot: "HEAD", registeredQuantity: 1, unregisteredQuantity: 0, reservedQuantity: 0, availableUnregisteredQuantity: 0, cosmeticStar: 1, nextStarThreshold: 2, neededForNextStar: 1, canUpgrade: false, upgradeDisabledReason: "INSUFFICIENT_DUPLICATES" }], equipment: {}, uniqueRegisteredCount: 1, setStars: {}, setEffects: {}, totalEffects: [] };
const catalog = { contentVersion: "v2", cosmetics: Array.from({ length: 6 }, (_, index) => ({ cosmeticId: `cosmetic-${String(index + 61).padStart(3, "0")}`, displayName: null, imageUrl: null, grade: "LEGENDARY", slot: ["HEAD", "TOP", "BOTTOM", "GLOVES", "SHOES", "CAPE"][index], setId: "cosmetic-set-11" })), sets: [{ setId: "cosmetic-set-11", displayName: null, grade: "LEGENDARY", members: {}, effects: {} }] };
// 콘텐츠 표시명이 비어 있으면 이름은 `세트 이름 + 부위 이름`으로 만들어진다.
const nameOf = (cosmeticId: string) => cosmeticDisplayName(catalog.cosmetics.find((item) => item.cosmeticId === cosmeticId));

const drawResponse: DrawResponse = {
  bannerId: banner.bannerId,
  ticketCost: 1,
  riceCost: 5_000,
  results: [
    { cosmeticId: "cosmetic-061", grade: "LEGENDARY", isNew: true },
    { cosmeticId: "cosmetic-062", grade: "LEGENDARY", isNew: false },
  ],
  collection,
};
function clientWithData(options: { bannerStaleTime?: number } = {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: options.bannerStaleTime ?? Infinity }, mutations: { retry: false } } });
  client.setQueryData(["cosmetic-banners"], [banner]);
  client.setQueryData(["cosmetic-collection"], collection);
  client.setQueryData(["cosmetic-catalog"], catalog);
  return client;
}

function renderScreen(client = clientWithData()) {
  render(<QueryClientProvider client={client}><CosmeticsScreen /></QueryClientProvider>);
  return client;
}

const drawOnce = () => fireEvent.click(screen.getByRole("button", { name: /^1회 뽑기/ }));

it("renders a concise milestone row and leaves the collection to the character tab", () => {
  const html = renderToStaticMarkup(<QueryClientProvider client={clientWithData()}><CosmeticsScreen /></QueryClientProvider>);
  expect(html).toContain("치장 배너 #01");
  expect(html).toContain("선택 상자 진행도 200 / 200");
  expect(html).toContain("선택 상자 받고 치장 선택하기");
  expect(html).toContain("선택 치장");
  // 도감·등록·착용은 이 화면에 없다. 보유 치장 카드가 새어 나오면 안 된다.
  expect(html).not.toContain(nameOf("cosmetic-061"));
  expect(html).not.toContain("도감과 외형");
  expect(html).not.toContain("<select");
});

it("renders an ordered result grid from the server response", () => {
  const html = renderToStaticMarkup(<CosmeticDrawReveal
    draw={{
      bannerId: banner.bannerId,
      ticketCost: 0,
      riceCost: 50_000,
      results: [
        { cosmeticId: "cosmetic-062", grade: "LEGENDARY", isNew: true },
        { cosmeticId: "cosmetic-061", grade: "LEGENDARY", isNew: false },
      ],
      collection,
    }}
    catalog={catalog}
  />);
  expect(html).not.toContain("획득 결과");
  expect(html).toContain("NEW");
  expect(html).not.toContain("중복");
  expect(html.indexOf(nameOf("cosmetic-062"))).toBeLessThan(html.indexOf(nameOf("cosmetic-061")));
});

it("draws immediately without a confirmation step and preserves server results", async () => {
  const draw = vi.spyOn(cosmeticsApi, "draw").mockResolvedValue(drawResponse);
  // 명령이 끝나면 배너를 다시 읽으므로, 조회도 함께 세워 둔다.
  vi.spyOn(cosmeticsApi, "banners").mockResolvedValue([banner]);
  renderScreen();
  drawOnce();
  await waitFor(() => expect(draw).toHaveBeenCalledWith(banner.bannerId, 1, expect.any(String)));
  expect(screen.queryByRole("dialog", { name: "뽑기 전 확인" })).toBeNull();
  expect((await screen.findAllByText(nameOf("cosmetic-061"))).length).toBeGreaterThan(0);
  expect(screen.getByText("NEW")).toBeTruthy();
  expect(screen.queryByText("중복")).toBeNull();
  expect(screen.getByRole("button", { name: /다시 1회 뽑기/ })).toBeTruthy();
  expect(screen.queryByRole("button", { name: /^1회 뽑기/ })).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "돌아가기" }));
  expect(screen.getByRole("button", { name: /^1회 뽑기/ })).toBeTruthy();
  expect(draw).toHaveBeenCalledTimes(1);
});

it("claims a ready selector box from the draw result screen", async () => {
  vi.spyOn(cosmeticsApi, "draw").mockResolvedValue(drawResponse);
  const claim = vi.spyOn(cosmeticsApi, "claimMilestone").mockResolvedValue({ remainingClaimableCount: 0 });
  renderScreen();

  drawOnce();
  await screen.findByLabelText("치장 뽑기 결과");
  fireEvent.click(screen.getByRole("button", { name: "선택 상자 받고 치장 선택하기" }));

  await waitFor(() => expect(claim).toHaveBeenCalledWith(banner.bannerId, 1, expect.any(String)));
  expect(await screen.findByRole("heading", { name: "전설 세트 선택" })).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "스시야 선택" }));
  expect(screen.getByRole("heading", { name: "받을 치장 선택" })).toBeTruthy();
  expect(screen.getByRole("button", { name: new RegExp(nameOf("cosmetic-066")) })).toBeTruthy();
});

it("reveals a selector box grant as a single card with only a confirm action", async () => {
  const chosen = { ...collection.states[0], cosmeticId: "cosmetic-066", slot: "CAPE", registeredQuantity: 0, unregisteredQuantity: 1 };
  vi.spyOn(cosmeticsApi, "draw").mockResolvedValue(drawResponse);
  vi.spyOn(cosmeticsApi, "claimMilestone").mockResolvedValue({ remainingClaimableCount: 0 });
  const openBox = vi.spyOn(cosmeticsApi, "openSelectorBox").mockResolvedValue({ ...collection, states: [chosen] });
  vi.spyOn(cosmeticsApi, "banners").mockResolvedValue([banner]);
  renderScreen();

  drawOnce();
  await screen.findByLabelText("치장 뽑기 결과");
  fireEvent.click(screen.getByRole("button", { name: "선택 상자 받고 치장 선택하기" }));
  await screen.findByRole("heading", { name: "전설 세트 선택" });
  fireEvent.click(screen.getByRole("button", { name: "스시야 선택" }));
  fireEvent.click(screen.getByRole("button", { name: new RegExp(nameOf("cosmetic-066")) }));
  fireEvent.click(screen.getByRole("button", { name: "확정하기" }));

  await waitFor(() => expect(openBox).toHaveBeenCalledWith(banner.milestone.boxItemId, "cosmetic-066", expect.any(String)));
  expect((await screen.findByLabelText("획득한 치장 1개")).children).toHaveLength(1);
  expect(screen.getByText(nameOf("cosmetic-066"))).toBeTruthy();
  // 상자로 받은 치장은 다시 뽑을 대상이 아니다. 확인 한 번으로 끝난다.
  expect(screen.queryByRole("button", { name: /다시 1회 뽑기/ })).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "확인" }));
  expect(screen.getByRole("button", { name: /^1회 뽑기/ })).toBeTruthy();
});

it("loads the headline banner's own detail for the odds dialog", async () => {
  const detail = vi.spyOn(cosmeticsApi, "detail").mockImplementation(async (bannerId) => ({
    banner: bannerId === secondBanner.bannerId ? secondBanner : banner,
    gradeProbabilityMillionths: { LEGENDARY: bannerId === secondBanner.bannerId ? 222_222 : 111_111 },
    pool: [],
  }));
  const client = clientWithData();
  client.setQueryData(["cosmetic-banners"], [banner, secondBanner]);
  renderScreen(client);
  fireEvent.click(screen.getByRole("button", { name: "등급별 확률" }));

  const dialog = await screen.findByRole("dialog", { name: "치장 뽑기 확률" });
  expect(detail).toHaveBeenCalledWith(banner.bannerId);
  expect(await within(dialog).findByText(/111,111/)).toBeTruthy();
  expect(within(dialog).queryByText(/222,222/)).toBeNull();
});

it("hides draw controls and the odds dialog when a cached banner query refetch fails", async () => {
  const client = clientWithData();
  vi.spyOn(cosmeticsApi, "detail").mockResolvedValue({ banner, gradeProbabilityMillionths: { LEGENDARY: 111_111 }, pool: [] });
  renderScreen(client);
  fireEvent.click(screen.getByRole("button", { name: "등급별 확률" }));
  expect(await screen.findByRole("dialog", { name: "치장 뽑기 확률" })).toBeTruthy();

  vi.spyOn(cosmeticsApi, "banners").mockRejectedValue(new Error("GACHA_CONTENT_UNAVAILABLE"));
  await client.refetchQueries({ queryKey: ["cosmetic-banners"] });

  expect(await screen.findByText("현재 뽑기를 이용할 수 없습니다.")).toBeTruthy();
  expect(screen.queryByRole("button", { name: /^1회 뽑기/ })).toBeNull();
  expect(screen.queryByRole("dialog", { name: "치장 뽑기 확률" })).toBeNull();
  expect(screen.getByText("도감·등록·착용은 캐릭터 창의 치장 탭에서 계속 이용할 수 있습니다.")).toBeTruthy();
});

it("serves the board from cached banners without calling a failing endpoint", () => {
  const banners = vi.spyOn(cosmeticsApi, "banners").mockRejectedValue(new Error("GACHA_CONTENT_UNAVAILABLE"));
  renderScreen();
  expect(screen.getByRole("button", { name: /^1회 뽑기/ })).toBeTruthy();
  expect(screen.queryByText("현재 뽑기를 이용할 수 없습니다.")).toBeNull();
  expect(banners).not.toHaveBeenCalled();
});

it("renders the 1-5 unlock notice without gacha or management controls when cosmetics are locked", async () => {
  vi.spyOn(cosmeticsApi, "collection").mockRejectedValue(new CosmeticsApiError(403, "COSMETICS_LOCKED"));
  vi.spyOn(cosmeticsApi, "catalog").mockRejectedValue(new CosmeticsApiError(403, "COSMETICS_LOCKED"));
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: Infinity } } });
  client.setQueryData(["cosmetic-banners"], []);
  renderScreen(client);

  expect(await screen.findByText(/1-5/)).toBeTruthy();
  expect(screen.queryByRole("button", { name: /뽑기/ })).toBeNull();
  expect(screen.queryByText("도감과 외형")).toBeNull();
});

it("retries an uncertain command with the exact same request", async () => {
  const draw = vi.spyOn(cosmeticsApi, "draw").mockRejectedValueOnce(new TypeError("network lost")).mockResolvedValueOnce(drawResponse);
  vi.spyOn(crypto, "randomUUID").mockReturnValue("same-request-key" as ReturnType<typeof crypto.randomUUID>);
  renderScreen();
  drawOnce();
  fireEvent.click(await screen.findByRole("button", { name: "같은 요청 다시 확인" }));
  await waitFor(() => expect(draw).toHaveBeenCalledTimes(2));
  expect(draw.mock.calls[0]).toEqual(draw.mock.calls[1]);
  expect((await screen.findAllByText(nameOf("cosmetic-061"))).length).toBeGreaterThan(0);
});

it("blocks every other command until an uncertain draw is confirmed", async () => {
  const draw = vi.spyOn(cosmeticsApi, "draw").mockRejectedValue(new TypeError("network lost"));
  const claim = vi.spyOn(cosmeticsApi, "claimMilestone").mockResolvedValue({ remainingClaimableCount: 0 });
  renderScreen();
  drawOnce();
  await screen.findByRole("button", { name: "같은 요청 다시 확인" });

  const claimButton = screen.getByRole("button", { name: "선택 상자 받고 치장 선택하기" });
  expect((claimButton as HTMLButtonElement).disabled).toBe(true);
  expect((screen.getByRole("button", { name: /^1회 뽑기/ }) as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(claimButton);
  drawOnce();
  expect(claim).not.toHaveBeenCalled();
  expect(draw).toHaveBeenCalledTimes(1);
});

it("keeps a stable gacha outage from re-sending the draw", async () => {
  const draw = vi.spyOn(cosmeticsApi, "draw").mockRejectedValue(new CosmeticsApiError(503, "GACHA_CONTENT_UNAVAILABLE"));
  renderScreen();
  drawOnce();

  expect(await screen.findByText("현재 뽑기를 이용할 수 없습니다.")).toBeTruthy();
  expect(screen.queryByRole("button", { name: /^1회 뽑기/ })).toBeNull();
  const retry = screen.getByRole("button", { name: "같은 요청 다시 확인" });
  expect((retry as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(retry);
  expect(draw).toHaveBeenCalledTimes(1);
});

it("gives each command its own idempotency key", async () => {
  const draw = vi.spyOn(cosmeticsApi, "draw").mockResolvedValue(drawResponse);
  const claim = vi.spyOn(cosmeticsApi, "claimMilestone").mockResolvedValue({ remainingClaimableCount: 0 });
  renderScreen();
  drawOnce();
  await screen.findByLabelText("치장 뽑기 결과");
  fireEvent.click(screen.getByRole("button", { name: "선택 상자 받고 치장 선택하기" }));

  await waitFor(() => expect(claim).toHaveBeenCalledTimes(1));
  expect(claim.mock.calls[0][2]).not.toBe(draw.mock.calls[0][2]);
});

it("sends only one command for synchronous actionable clicks", async () => {
  const claimGate = Promise.withResolvers<{ remainingClaimableCount: number }>();
  const claim = vi.spyOn(cosmeticsApi, "claimMilestone").mockImplementation(() => claimGate.promise);
  const client = clientWithData();
  client.setQueryData(["cosmetic-banners"], [banner, secondBanner]);
  renderScreen(client);

  const claimButtons = screen.getAllByRole("button", { name: "선택 상자 받고 치장 선택하기" });
  fireEvent.click(claimButtons[0]);
  fireEvent.click(claimButtons[1]);

  await waitFor(() => expect(claim).toHaveBeenCalledTimes(1));
  claimGate.resolve({ remainingClaimableCount: 0 });
  await screen.findByRole("heading", { name: "전설 세트 선택" });
});

it("refetches authoritative cosmetics state before closing deterministic errors", async () => {
  const claim = vi.spyOn(cosmeticsApi, "claimMilestone").mockRejectedValue(new CosmeticsApiError(409, "COSMETIC_STATE_CONFLICT"));
  const client = clientWithData();
  const refetch = vi.spyOn(client, "refetchQueries");
  renderScreen(client);

  fireEvent.click(screen.getByRole("button", { name: "선택 상자 받고 치장 선택하기" }));
  await waitFor(() => expect(claim).toHaveBeenCalledTimes(1));
  await screen.findByText("치장 상태가 변경되었습니다. 최신 상태를 확인해 주세요.");
  fireEvent.click(screen.getByRole("button", { name: "오류 닫기" }));

  await waitFor(() => expect(refetch).toHaveBeenCalledTimes(4));
  expect(refetch).toHaveBeenCalledWith({ queryKey: ["cosmetic-banners"] });
  expect(refetch).toHaveBeenCalledWith({ queryKey: ["cosmetic-collection"] });
  expect(refetch).toHaveBeenCalledWith({ queryKey: ["cosmetic-catalog"] });
  expect(refetch).toHaveBeenCalledWith({ queryKey: ["character-stats"] });
  await waitFor(() => expect(screen.queryByText("치장 상태가 변경되었습니다. 최신 상태를 확인해 주세요.")).toBeNull());
});

it("blocks all commands while a deterministic error is open and during close refetch", async () => {
  const claim = vi.spyOn(cosmeticsApi, "claimMilestone").mockRejectedValue(new CosmeticsApiError(409, "COSMETIC_STATE_CONFLICT"));
  const draw = vi.spyOn(cosmeticsApi, "draw").mockResolvedValue(drawResponse);
  const client = clientWithData();
  const refetchGate = Promise.withResolvers<void>();
  vi.spyOn(client, "refetchQueries").mockImplementation((filters) => {
    if (filters?.queryKey?.[0] === "cosmetic-collection") return refetchGate.promise;
    return Promise.resolve();
  });
  renderScreen(client);

  fireEvent.click(screen.getByRole("button", { name: "선택 상자 받고 치장 선택하기" }));
  await waitFor(() => expect(claim).toHaveBeenCalledTimes(1));
  await screen.findByText("치장 상태가 변경되었습니다. 최신 상태를 확인해 주세요.");

  const drawButton = screen.getByRole("button", { name: /^1회 뽑기/ });
  expect((drawButton as HTMLButtonElement).disabled).toBe(true);
  expect((screen.getByRole("button", { name: "선택 상자 받고 치장 선택하기" }) as HTMLButtonElement).disabled).toBe(true);

  fireEvent.click(screen.getByRole("button", { name: "오류 닫기" }));
  fireEvent.click(drawButton);
  expect(draw).not.toHaveBeenCalled();
  expect((drawButton as HTMLButtonElement).disabled).toBe(true);

  refetchGate.resolve();
  await waitFor(() => expect((screen.getByRole("button", { name: /^1회 뽑기/ }) as HTMLButtonElement).disabled).toBe(false));
  drawOnce();
  await waitFor(() => expect(draw).toHaveBeenCalledTimes(1));
});

it("offers a shortcut from the draw result straight to dressing up", async () => {
  vi.spyOn(cosmeticsApi, "draw").mockResolvedValue(drawResponse);
  vi.spyOn(cosmeticsApi, "banners").mockResolvedValue([banner]);
  const onClose = vi.fn();
  render(<QueryClientProvider client={clientWithData()}><CosmeticsScreen onClose={onClose} /></QueryClientProvider>);
  drawOnce();
  await screen.findByLabelText("치장 뽑기 결과");
  // 갓 뽑은 것을 바로 입어 보러 가는 길이 결과 화면에 있어야 한다.
  fireEvent.click(screen.getByRole("button", { name: "치장하러 가기" }));
  expect(onClose).toHaveBeenCalled();
});
