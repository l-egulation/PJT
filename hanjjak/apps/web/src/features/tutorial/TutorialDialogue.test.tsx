// @vitest-environment happy-dom
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { TutorialDialogue } from "./TutorialDialogue";
import type { TutorialStep } from "./tutorialContent";

const STEPS: TutorialStep[] = [
  { id: "welcome", message: "첫 번째 안내" },
  { id: "status", message: "두 번째 안내", target: ".target", targetLabel: "대상" },
];

afterEach(cleanup);

it("advances by click and completes after the last dialogue", () => {
  const onComplete = vi.fn();
  render(<TutorialDialogue chapterLabel="모험가의 장" steps={STEPS} onComplete={onComplete} onDismiss={vi.fn()} />);

  const firstAdvance = screen.getByRole("button", { name: /첫 번째 안내/ });
  expect(firstAdvance.textContent).toContain("다음");
  fireEvent.click(firstAdvance);
  expect(screen.getByText("두 번째 안내")).toBeTruthy();
  expect(screen.getByRole("button", { name: /두 번째 안내/ }).textContent).toContain("완료");

  fireEvent.click(screen.getByRole("button", { name: /두 번째 안내/ }));
  expect(onComplete).toHaveBeenCalledOnce();
});

it("keeps the chapter label out of the dialogue and shows the complete frame bounds", () => {
  render(<TutorialDialogue chapterLabel="모험가의 장" steps={STEPS} onComplete={vi.fn()} onDismiss={vi.fn()} />);

  expect((screen.getByText("모험가의 장") as HTMLElement).hidden).toBe(true);
  expect(document.querySelector(".tutorial-dialogue-frame")?.getAttribute("viewBox")).toBe("30 120 2112 460");
});

it("advances with keyboard and dismisses with escape", () => {
  const onDismiss = vi.fn();
  render(<TutorialDialogue chapterLabel="모험가의 장" steps={STEPS} onComplete={vi.fn()} onDismiss={onDismiss} />);

  fireEvent.keyDown(window, { key: " " });
  expect(screen.getByText("두 번째 안내")).toBeTruthy();
  fireEvent.keyDown(window, { key: "Escape" });
  expect(onDismiss).toHaveBeenCalledOnce();
});

it("marks the current target when it is visible", () => {
  const target = document.createElement("section");
  target.className = "target";
  target.getBoundingClientRect = () => ({
    x: 80,
    y: 600,
    top: 600,
    left: 80,
    right: 280,
    bottom: 700,
    width: 200,
    height: 100,
    toJSON: () => undefined,
  });
  document.body.append(target);
  render(<TutorialDialogue chapterLabel="모험가의 장" steps={STEPS} onComplete={vi.fn()} onDismiss={vi.fn()} />);

  fireEvent.click(screen.getByRole("button", { name: /첫 번째 안내/ }));

  expect(screen.getByText("대상")).toBeTruthy();
  expect(document.querySelector(".tutorial-spotlight")).toBeTruthy();
  target.remove();
});

it("switches the character sprite frame with the dialogue step", () => {
  render(<TutorialDialogue
    chapterLabel="모험가의 장"
    characterSrc="/tutorial-poses.png"
    characterSprite={{ columns: 3, rows: 2, frames: [0, 4] }}
    steps={STEPS}
    onComplete={vi.fn()}
    onDismiss={vi.fn()}
  />);

  const character = document.querySelector<HTMLElement>(".tutorial-character-sprite");
  expect(character?.style.getPropertyValue("--tutorial-sprite-column")).toBe("0");
  expect(character?.style.getPropertyValue("--tutorial-sprite-row")).toBe("0");

  fireEvent.click(screen.getByRole("button", { name: /첫 번째 안내/ }));

  expect(character?.style.getPropertyValue("--tutorial-sprite-column")).toBe("1");
  expect(character?.style.getPropertyValue("--tutorial-sprite-row")).toBe("1");
});

it("lets a target-action step activate the highlighted control before advancing", () => {
  const targetTop = window.innerHeight * .8;
  const target = document.createElement("button");
  target.className = "interactive-target";
  target.textContent = "장비";
  target.getBoundingClientRect = () => ({
    x: 80,
    y: targetTop,
    top: targetTop,
    left: 80,
    right: 280,
    bottom: targetTop + 100,
    width: 200,
    height: 100,
    toJSON: () => undefined,
  });
  const onTargetClick = vi.fn();
  target.addEventListener("click", onTargetClick);
  document.body.append(target);

  render(<TutorialDialogue
    chapterLabel="대장장이의 장"
    steps={[
      { id: "open-equipment", message: "장비 버튼을 눌러 봐.", target: ".interactive-target", targetLabel: "장비" },
      { id: "equipment-opened", message: "장비 창이 열렸어." },
    ]}
    isTargetAction={(step) => step.id === "open-equipment"}
    onComplete={vi.fn()}
    onDismiss={vi.fn()}
  />);

  expect(screen.queryByRole("button", { name: /장비 버튼을 눌러 봐/ })).toBeNull();
  expect(screen.queryByText("표시된 버튼을 직접 눌러 줘!")).toBeNull();
  const dialogueWrap = document.querySelector<HTMLElement>(".tutorial-dialogue-wrap");
  expect(dialogueWrap?.classList.contains("is-raised")).toBe(true);
  expect(dialogueWrap?.style.getPropertyValue("--tutorial-target-top")).toBe(`${targetTop - 10}px`);
  fireEvent.click(screen.getByRole("button", { name: "장비 직접 선택" }));

  expect(onTargetClick).toHaveBeenCalledOnce();
  expect(screen.getByText("장비 창이 열렸어.")).toBeTruthy();
  target.remove();
});

it("steps back to the previous dialogue and hides the control on the first step", () => {
  render(<TutorialDialogue chapterLabel="모험가의 장" steps={STEPS} onComplete={vi.fn()} onDismiss={vi.fn()} />);

  expect(screen.queryByRole("button", { name: /이전 대사로 돌아가기/ })).toBeNull();

  fireEvent.click(screen.getByRole("button", { name: /첫 번째 안내/ }));
  const previous = screen.getByRole("button", { name: /이전 대사로 돌아가기/ });
  fireEvent.click(previous);

  expect(screen.getByText("첫 번째 안내")).toBeTruthy();
  expect(screen.queryByRole("button", { name: /이전 대사로 돌아가기/ })).toBeNull();
});

it("moves between dialogues with the arrow keys", () => {
  render(<TutorialDialogue chapterLabel="모험가의 장" steps={STEPS} onComplete={vi.fn()} onDismiss={vi.fn()} />);

  fireEvent.keyDown(window, { key: "ArrowRight" });
  expect(screen.getByText("두 번째 안내")).toBeTruthy();

  fireEvent.keyDown(window, { key: "ArrowLeft" });
  expect(screen.getByText("첫 번째 안내")).toBeTruthy();
});

it("keeps the dismissal label and the background press opt-in", () => {
  const onDismiss = vi.fn();
  const { rerender } = render(<TutorialDialogue chapterLabel="모험가의 장" steps={STEPS} onComplete={vi.fn()} onDismiss={onDismiss} />);

  expect(screen.getByRole("button", { name: "건너뛰기" })).toBeTruthy();
  fireEvent.mouseDown(document.querySelector(".tutorial-dim")!);
  expect(onDismiss).not.toHaveBeenCalled();

  rerender(<TutorialDialogue chapterLabel="모험가의 장" steps={STEPS} dismissLabel="닫기" dismissOnBackdrop onComplete={vi.fn()} onDismiss={onDismiss} />);

  expect(screen.getByRole("button", { name: "닫기" })).toBeTruthy();
  fireEvent.mouseDown(document.querySelector(".tutorial-dim")!);
  expect(onDismiss).toHaveBeenCalledOnce();
});

it("keeps a target label on one line inside the screen when the target hugs the right edge", () => {
  const styles = readFileSync(resolve(process.cwd(), "src/features/tutorial/TutorialDialogue.css"), "utf8");
  const start = styles.indexOf(".tutorial-target-label {");
  const rule = styles.slice(start, styles.indexOf("}", start));

  expect(rule).toContain("white-space: nowrap");
  /* 이름표를 강조 칸 왼쪽에 못 박으면 화면 오른쪽 끝 단추에서 두 줄로 접혔다. */
  expect(rule).not.toContain("left: calc(var(--tutorial-target-left) + 20px)");
  expect(rule.replace(/\s+/g, " ")).toContain("translate: clamp( calc(16px - var(--tutorial-target-left)), 20px, calc(100dvw - 16px - var(--tutorial-target-left) - 100%) ) 0");
});
