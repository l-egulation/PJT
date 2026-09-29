// @vitest-environment happy-dom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, expect, it } from "vitest";
import { TutorialLauncher } from "./TutorialLauncher";
import { BATTLE_GUIDE_TUTORIAL_STEPS } from "./tutorialContent";

afterEach(cleanup);

it("opens the guide from the battle screen and closes it back to the trigger", () => {
  render(<TutorialLauncher />);

  const trigger = screen.getByRole("button", { name: "튜토리얼 보기" });
  expect(trigger.getAttribute("aria-haspopup")).toBe("dialog");
  expect(trigger.getAttribute("aria-expanded")).toBe("false");
  expect(screen.queryByRole("dialog")).toBeNull();

  fireEvent.click(trigger);

  expect(screen.getByRole("dialog")).toBeTruthy();
  expect(trigger.getAttribute("aria-expanded")).toBe("true");
  expect(screen.getByText(BATTLE_GUIDE_TUTORIAL_STEPS[0].message)).toBeTruthy();

  fireEvent.keyDown(window, { key: "Escape" });

  expect(screen.queryByRole("dialog")).toBeNull();
  expect(document.activeElement).toBe(trigger);
});

it("walks the whole guide and finishes on the last step", () => {
  render(<TutorialLauncher />);
  fireEvent.click(screen.getByRole("button", { name: "튜토리얼 보기" }));

  BATTLE_GUIDE_TUTORIAL_STEPS.slice(0, -1).forEach(() => fireEvent.keyDown(window, { key: "ArrowRight" }));

  const last = BATTLE_GUIDE_TUTORIAL_STEPS[BATTLE_GUIDE_TUTORIAL_STEPS.length - 1];
  expect(screen.getByRole("button", { name: new RegExp(`${last.message} 튜토리얼 완료`) })).toBeTruthy();

  fireEvent.keyDown(window, { key: "ArrowRight" });

  expect(screen.queryByRole("dialog")).toBeNull();
});

it("closes when the darkened background is pressed", () => {
  render(<TutorialLauncher />);
  fireEvent.click(screen.getByRole("button", { name: "튜토리얼 보기" }));

  fireEvent.mouseDown(document.querySelector(".tutorial-dim")!);

  expect(screen.queryByRole("dialog")).toBeNull();
});
