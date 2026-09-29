// @vitest-environment happy-dom
import { renderToStaticMarkup } from "react-dom/server";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { CostumePanel, GalleryPanel } from "./CosmeticPanels";
import type { CharacterCollection, CosmeticCatalog } from "./api";

const catalog: CosmeticCatalog = {
  contentVersion: "cosmetics-v2-placeholder",
  cosmetics: [{ cosmeticId: "cosmetic-999", displayName: null, imageUrl: null, grade: "NORMAL", slot: "HEAD", setId: "cosmetic-set-99" }],
  sets: [{ setId: "cosmetic-set-99", displayName: null, grade: "NORMAL", members: { HEAD: "cosmetic-999" }, effects: { "1": [{ statId: "attackPercent", value: 20, unit: "BASIS_POINTS" }] } }],
};
const collection: CharacterCollection = {
  contentVersion: catalog.contentVersion,
  states: [{ cosmeticId: "cosmetic-999", displayName: null, imageUrl: null, grade: "NORMAL", slot: "HEAD", registeredQuantity: 1, unregisteredQuantity: 0, reservedQuantity: 0, availableUnregisteredQuantity: 0, cosmeticStar: 1, nextStarThreshold: 3, neededForNextStar: 2, canUpgrade: false, upgradeDisabledReason: "INSUFFICIENT_DUPLICATES" }],
  equipment: { HEAD: "cosmetic-999" }, uniqueRegisteredCount: 1, setStars: { "cosmetic-set-99": 1 },
  setEffects: { "cosmetic-set-99": [{ statId: "attackPercent", value: 20, unit: "BASIS_POINTS" }] },
  totalEffects: [{ statId: "attackPercent", value: 20, unit: "BASIS_POINTS" }],
};

it("uses numbered fallback names and explicit image placeholders", () => {
  const costume = renderToStaticMarkup(<CostumePanel collection={collection} catalog={catalog} pending={false} send={() => {}} sendBatch={() => {}} onGacha={() => {}} />);
  expect(costume).toContain("치장 #999");
  expect(costume).toContain("이미지 준비 중");
  expect(costume).toContain("cosmetic-999");
  const gallery = renderToStaticMarkup(<GalleryPanel collection={collection} catalog={catalog} pending={false} send={() => {}} />);
  expect(gallery).toContain("세트 #99");
  expect(gallery).toContain("공격력");
  expect(gallery).toContain("0.2%");
});

afterEach(cleanup);

it("keeps preview reset in the stage and starts the closet with slot tabs", () => {
  const sendBatch = vi.fn();
  const { container } = render(<CostumePanel collection={collection} catalog={catalog} pending={false} send={() => {}} sendBatch={sendBatch} onGacha={() => {}} />);

  fireEvent.click(screen.getByRole("button", { name: "초기화" }));
  expect(sendBatch).toHaveBeenCalledWith([{ kind: "equip", slot: "HEAD", cosmeticId: null }]);
  expect(screen.queryByRole("button", { name: "치장 뽑기" })).toBeNull();
  expect(screen.getByRole("tablist", { name: "치장 부위" })).toBeTruthy();
  expect(screen.getByRole("tab", { name: "머리" }).getAttribute("aria-selected")).toBe("true");
  expect(screen.getByRole("tab", { name: "무기" })).toBeTruthy();
  expect(screen.queryByRole("tab", { name: "하의" })).toBeNull();
  expect(screen.queryByText("치장 보관함")).toBeNull();
  expect(container.querySelectorAll(".costume-owned-blank")).toHaveLength(0);
});

it("resets every equipped data slot and disables reset when nothing is equipped", () => {
  const sendBatch = vi.fn();
  const weapon = { cosmeticId: "cosmetic-998", displayName: "테스트 무기", imageUrl: null, grade: "NORMAL" as const, slot: "WEAPON", setId: "cosmetic-set-99" };
  const multiCatalog: CosmeticCatalog = { ...catalog, cosmetics: [...catalog.cosmetics, weapon] };
  const multiCollection: CharacterCollection = {
    ...collection,
    states: [...collection.states, { ...collection.states[0], cosmeticId: weapon.cosmeticId, displayName: weapon.displayName, slot: weapon.slot }],
    equipment: { HEAD: "cosmetic-999", WEAPON: "cosmetic-998" },
  };
  const { rerender } = render(<CostumePanel collection={multiCollection} catalog={multiCatalog} pending={false} send={() => {}} sendBatch={sendBatch} onGacha={() => {}} />);

  fireEvent.click(screen.getByRole("button", { name: "초기화" }));
  expect(sendBatch).toHaveBeenCalledWith([
    { kind: "equip", slot: "HEAD", cosmeticId: null },
    { kind: "equip", slot: "WEAPON", cosmeticId: null },
  ]);

  rerender(<CostumePanel collection={{ ...multiCollection, equipment: {} }} catalog={multiCatalog} pending={false} send={() => {}} sendBatch={sendBatch} onGacha={() => {}} />);
  expect((screen.getByRole("button", { name: "초기화" }) as HTMLButtonElement).disabled).toBe(true);
});

it("wears a costume on the first click and takes it off when it is clicked again", () => {
  const send = vi.fn();
  const secondCatalog: CosmeticCatalog = {
    ...catalog,
    cosmetics: [...catalog.cosmetics, { cosmeticId: "cosmetic-002", displayName: "둘째 모자", imageUrl: "/second.png", grade: "LEGENDARY", slot: "HEAD", setId: "cosmetic-set-99" }],
  };
  const secondCollection: CharacterCollection = {
    ...collection,
    states: [...collection.states, { cosmeticId: "cosmetic-002", displayName: "둘째 모자", imageUrl: "/second.png", grade: "LEGENDARY", slot: "HEAD", registeredQuantity: 1, unregisteredQuantity: 0, reservedQuantity: 0, availableUnregisteredQuantity: 0, cosmeticStar: 3, nextStarThreshold: 4, neededForNextStar: 3, canUpgrade: false, upgradeDisabledReason: "INSUFFICIENT_DUPLICATES" }],
  };
  render(<CostumePanel collection={secondCollection} catalog={secondCatalog} pending={false} send={send} sendBatch={() => {}} onGacha={() => {}} />);

  fireEvent.click(screen.getByRole("button", { name: "둘째 모자 입기" }));
  expect(send).toHaveBeenCalledWith({ kind: "equip", slot: "HEAD", cosmeticId: "cosmetic-002" });

  fireEvent.click(screen.getByRole("button", { name: "치장 #999 벗기" }));
  expect(send).toHaveBeenLastCalledWith({ kind: "equip", slot: "HEAD", cosmeticId: null });

  expect(screen.queryByRole("button", { name: "착용" })).toBeNull();
  expect(screen.queryByRole("button", { name: "취소" })).toBeNull();
  expect(screen.queryByRole("button", { name: "해제" })).toBeNull();
});

it("assembles the gallery into set, member, and set-effect regions", () => {
  render(<GalleryPanel collection={collection} catalog={catalog} pending={false} send={() => {}} />);

  expect(screen.getByRole("region", { name: "세트 목록" })).toBeTruthy();
  expect(screen.getByRole("region", { name: "선택 세트 상세" })).toBeTruthy();
  expect(screen.getByRole("region", { name: "세트 효과" })).toBeTruthy();
  expect(screen.getByRole("tablist", { name: "도감 세트 필터" })).toBeTruthy();
  expect(screen.getByText("공격력")).toBeTruthy();

  fireEvent.click(screen.getByRole("tab", { name: "미보유" }));
  expect(screen.getByText("해당하는 세트가 없습니다.")).toBeTruthy();
});

it("keeps the member registration command in the redesigned gallery", () => {
  const send = vi.fn();
  const upgradeCollection: CharacterCollection = {
    ...collection,
    states: collection.states.map(state => ({ ...state, unregisteredQuantity: 2, availableUnregisteredQuantity: 2, canUpgrade: true, upgradeDisabledReason: null })),
  };
  render(<GalleryPanel collection={upgradeCollection} catalog={catalog} pending={false} send={send} />);

  // 버튼에 "가진 여분 / 다음 성급에 드는 개수"를 적는다. 누적 수치를 읽을 필요가 없다.
  const upgrade = screen.getByRole("button", { name: /치장 #999 여분 2개를 넣어 2성/ });
  expect(upgrade.textContent).toBe("2 / 2");
  fireEvent.click(upgrade);

  expect(send).toHaveBeenCalledWith({ kind: "register", cosmeticId: "cosmetic-999" });
});

it("shows spare copies against the next star cost instead of a running total", () => {
  const send = vi.fn();
  const shortCollection: CharacterCollection = {
    ...collection,
    states: collection.states.map(state => ({ ...state, unregisteredQuantity: 1, availableUnregisteredQuantity: 1, canUpgrade: false, upgradeDisabledReason: "INSUFFICIENT_DUPLICATES" })),
  };
  render(<GalleryPanel collection={shortCollection} catalog={catalog} pending={false} send={send} />);

  // 다음 성급까지 2개가 필요한데 여분이 1개이므로 1개가 모자란다.
  const upgrade = screen.getByRole("button", { name: /치장 #999 2성까지 2개가 필요한데 여분이 1개/ });
  expect(upgrade.textContent).toBe("1 / 2");
  expect((upgrade as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(upgrade);
  expect(send).not.toHaveBeenCalled();
});
