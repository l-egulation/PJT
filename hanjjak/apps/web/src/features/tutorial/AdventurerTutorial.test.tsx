// @vitest-environment happy-dom
import { act, cleanup, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { AdventurerTutorial } from "./AdventurerTutorial";
import { ADVENTURER_TUTORIAL_ID, tutorialProgressKey } from "./tutorialProgress";

beforeEach(() => {
  vi.useFakeTimers();
  window.localStorage.clear();
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  window.localStorage.clear();
});

it("opens once on the active battle screen", () => {
  render(<AdventurerTutorial accountId="account-a" active />);
  act(() => vi.advanceTimersByTime(450));

  expect(screen.getByRole("dialog")).toBeTruthy();
  expect(screen.getByText("모험가의 장")).toBeTruthy();
  expect(screen.getByRole("img", { name: "모험가 복장을 입은 튜토리얼 안내자 한짝" })).toBeTruthy();
});

it("does not open when this account already finished it", () => {
  window.localStorage.setItem(tutorialProgressKey("account-a", ADVENTURER_TUTORIAL_ID), JSON.stringify({
    status: "COMPLETED",
    updatedAt: "2026-09-13T00:00:00.000Z",
  }));

  render(<AdventurerTutorial accountId="account-a" active />);
  act(() => vi.advanceTimersByTime(1_000));

  expect(screen.queryByRole("dialog")).toBeNull();
});
