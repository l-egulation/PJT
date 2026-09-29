import { useCallback, useEffect, useLayoutEffect, useRef, useState, type CSSProperties } from "react";
import adventurerPoses from "./assets/adventurer-poses.png";
import dialogueFrame from "./assets/tutorial-dialogue-frame.png";
import type { TutorialStep } from "./tutorialContent";
import "./TutorialDialogue.css";

type SpotlightRect = {
  top: number;
  left: number;
  width: number;
  height: number;
};

type TutorialDialogueProps = {
  chapterLabel: string;
  characterAlt?: string;
  characterSrc?: string;
  characterSprite?: {
    columns: number;
    rows: number;
    frames: number[];
  };
  steps: TutorialStep[];
  isTargetAction?: (step: TutorialStep, index: number) => boolean;
  /* 첫 안내는 건너뛰기, 다시 보기는 닫기다. 하는 일은 같고 이름만 다르다. */
  dismissLabel?: string;
  /* 다른 창과 같은 규칙으로 바깥을 눌러 닫는다. 첫 안내는 실수로 닫히지 않게 꺼 둔다. */
  dismissOnBackdrop?: boolean;
  onStepChange?: (step: TutorialStep, index: number) => void;
  onComplete: () => void;
  onDismiss: () => void;
};

const TARGET_PADDING = 10;
/* 말풍선 칸의 높이·여백. CSS 의 --tutorial-dialogue-wrap-height 와 같은 값이다. */
const WRAP_HEIGHT = 270;
const WRAP_GAP = 18;

/*
 * 말풍선을 어디에 둘지 고른다. 예전에는 강조한 칸의 "윗변"만 보고 아래 절반이면
 * 위로 올렸다. 그래서 위에서 시작해 아래까지 길게 뻗은 칸(자동 사용 슬롯, 던전 입장문)
 * 은 올라가지도 않고, 아래에 놓인 말풍선이 그 아래쪽을 덮어 버렸다.
 * 이제 실제로 겹치는지를 보고, 위에 자리가 없으면 옆으로 비킨다.
 */
function wrapPlacement(spotlight: SpotlightRect | null, wrapHeight: number): string {
  if (!spotlight || typeof window === "undefined") return "";
  const bottomTop = window.innerHeight - 34 - wrapHeight;
  if (spotlight.top + spotlight.height <= bottomTop) return "";
  /*
   * 위에 자리가 없으면 아래에 그대로 둔다. 좁혀서 옆에 세워 봤더니 말풍선 안이
   * 무너져 — 대사가 사라지고 단추가 화면 밖으로 나갔다 — 넓이를 지키는 편이 낫다.
   */
  return spotlight.top - WRAP_GAP >= wrapHeight + 12 ? "is-raised" : "";
}

const DEFAULT_ADVENTURER_SPRITE = {
  columns: 3,
  rows: 2,
  frames: [0, 1, 2, 5],
};

function targetRect(selector?: string): SpotlightRect | null {
  if (!selector) return null;
  const target = document.querySelector<HTMLElement>(selector);
  if (!target) return null;
  const rect = target.getBoundingClientRect();
  if (rect.width <= 0 || rect.height <= 0) return null;
  return {
    top: Math.max(8, rect.top - TARGET_PADDING),
    left: Math.max(8, rect.left - TARGET_PADDING),
    width: Math.min(window.innerWidth - 16, rect.width + TARGET_PADDING * 2),
    height: Math.min(window.innerHeight - 16, rect.height + TARGET_PADDING * 2),
  };
}

export function TutorialDialogue({
  chapterLabel,
  characterAlt = "모험가 복장을 입은 튜토리얼 안내자 한짝",
  characterSrc = adventurerPoses,
  characterSprite = DEFAULT_ADVENTURER_SPRITE,
  steps,
  isTargetAction,
  dismissLabel = "건너뛰기",
  dismissOnBackdrop = false,
  onStepChange,
  onComplete,
  onDismiss,
}: TutorialDialogueProps) {
  const [stepIndex, setStepIndex] = useState(0);
  const step = steps[stepIndex];
  const [spotlight, setSpotlight] = useState<SpotlightRect | null>(() => targetRect(steps[0]?.target));
  const layerElement = useRef<HTMLDivElement>(null);
  const advanceButton = useRef<HTMLButtonElement>(null);
  /*
   * 말풍선의 실제 높이를 잰다. 예전에는 270px 로 못 박아 두고 그 값으로 위치를 정했는데,
   * 창이 좁으면 말풍선이 훨씬 높아져 — 위로 올린 자리가 화면 밖으로 넘어가 단추를
   * 누를 수 없었다.
   */
  const wrapElement = useRef<HTMLDivElement>(null);
  const [wrapHeight, setWrapHeight] = useState(WRAP_HEIGHT);
  const targetActionButton = useRef<HTMLButtonElement>(null);
  const requiresTargetAction = Boolean(step && isTargetAction?.(step, stepIndex));

  const advance = useCallback(() => {
    if (stepIndex >= steps.length - 1) {
      onComplete();
      return;
    }
    const nextIndex = stepIndex + 1;
    onStepChange?.(steps[nextIndex], nextIndex);
    setStepIndex(nextIndex);
  }, [onComplete, onStepChange, stepIndex, steps]);

  const back = useCallback(() => {
    if (stepIndex <= 0) return;
    const previousIndex = stepIndex - 1;
    onStepChange?.(steps[previousIndex], previousIndex);
    setStepIndex(previousIndex);
  }, [onStepChange, stepIndex, steps]);

  const activateTarget = useCallback(() => {
    if (!step?.target) return;
    const target = document.querySelector<HTMLElement>(step.target);
    if (!target) return;
    target.click();
    advance();
  }, [advance, step]);

  useLayoutEffect(() => {
    const observer = typeof ResizeObserver === "undefined" ? null : new ResizeObserver(update);
    const mutationObserver = typeof MutationObserver === "undefined" ? null : new MutationObserver(update);
    let observedTarget: Element | null = null;
    function update() {
      const target = step?.target ? document.querySelector(step.target) : null;
      if (target !== observedTarget) {
        observer?.disconnect();
        if (target) observer?.observe(target);
        observedTarget = target;
      }
      const next = targetRect(step?.target);
      setSpotlight((current) => current?.top === next?.top
        && current?.left === next?.left
        && current?.width === next?.width
        && current?.height === next?.height ? current : next);
      if (target) mutationObserver?.disconnect();
    }
    mutationObserver?.observe(document.body, { childList: true, subtree: true });
    const initialTarget = step?.target ? document.querySelector<HTMLElement>(step.target) : null;
    const initialRect = initialTarget?.getBoundingClientRect();
    if (requiresTargetAction && initialTarget && initialRect
      && (initialRect.top < 8 || initialRect.bottom > window.innerHeight - 8)) {
      initialTarget.scrollIntoView?.({ block: "center", inline: "nearest" });
    }
    update();
    window.addEventListener("resize", update);
    window.addEventListener("scroll", update, true);
    return () => {
      window.removeEventListener("resize", update);
      window.removeEventListener("scroll", update, true);
      observer?.disconnect();
      mutationObserver?.disconnect();
    };
  }, [requiresTargetAction, step?.target]);

  useLayoutEffect(() => {
    const element = wrapElement.current;
    if (!element) return;
    const measure = () => setWrapHeight((current) => {
      const next = Math.round(element.getBoundingClientRect().height);
      return next > 0 && next !== current ? next : current;
    });
    measure();
    const observer = typeof ResizeObserver === "undefined" ? null : new ResizeObserver(measure);
    observer?.observe(element);
    window.addEventListener("resize", measure);
    return () => { observer?.disconnect(); window.removeEventListener("resize", measure); };
  }, [stepIndex]);

  useEffect(() => {
    if (requiresTargetAction) targetActionButton.current?.focus();
    else advanceButton.current?.focus();
  }, [requiresTargetAction, stepIndex, spotlight]);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        onDismiss();
        return;
      }
      if (event.key === "ArrowLeft") {
        event.preventDefault();
        back();
        return;
      }
      if (event.key === "ArrowRight") {
        event.preventDefault();
        /* 직접 눌러야 넘어가는 단계는 화살표로 건너뛰지 못하게 둔다. */
        if (!requiresTargetAction) advance();
        return;
      }
      /* 안내가 열려 있는 동안 탭이 뒤쪽 화면으로 새어 나가지 않게 가둔다. */
      if (event.key === "Tab") {
        const layer = layerElement.current;
        if (!layer) return;
        const focusable = [...layer.querySelectorAll<HTMLElement>("button:not(:disabled)")];
        if (focusable.length === 0) return;
        const edge = event.shiftKey ? focusable[0] : focusable[focusable.length - 1];
        const wrapTo = event.shiftKey ? focusable[focusable.length - 1] : focusable[0];
        if (document.activeElement === edge || !layer.contains(document.activeElement)) {
          event.preventDefault();
          wrapTo.focus();
        }
        return;
      }
      if (event.key === "Enter" || event.key === " ") {
        event.preventDefault();
        if (requiresTargetAction) activateTarget();
        else advance();
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [activateTarget, advance, back, onDismiss, requiresTargetAction]);

  if (!step || steps.length === 0) return null;

  /* 단계가 자세보다 많으면 처음 자세로 되돌아온다. 예전에는 남는 단계가 모두 첫 칸에 멈췄다. */
  const characterFrame = characterSprite?.frames[stepIndex % Math.max(1, characterSprite.frames.length)] ?? 0;
  const characterColumn = characterSprite ? characterFrame % characterSprite.columns : 0;
  const characterRow = characterSprite ? Math.floor(characterFrame / characterSprite.columns) : 0;
  const characterStyle = characterSprite ? {
    "--tutorial-sprite-columns": characterSprite.columns,
    "--tutorial-sprite-rows": characterSprite.rows,
    "--tutorial-sprite-column": characterColumn,
    "--tutorial-sprite-row": characterRow,
  } as CSSProperties : undefined;

  const spotlightStyle = spotlight ? {
    "--tutorial-wrap-height": `${wrapHeight}px`,
    "--tutorial-target-top": `${spotlight.top}px`,
    "--tutorial-target-left": `${spotlight.left}px`,
    "--tutorial-target-width": `${spotlight.width}px`,
    "--tutorial-target-height": `${spotlight.height}px`,
  } as CSSProperties : undefined;

  return <div
    ref={layerElement}
    className="tutorial-layer"
    data-tutorial-chapter={chapterLabel}
    role="dialog"
    aria-modal="true"
    aria-labelledby="tutorial-speaker"
    aria-describedby="tutorial-message"
    onMouseDown={dismissOnBackdrop
      ? (event) => {
        const target = event.target as HTMLElement;
        if (target === event.currentTarget || target.classList.contains("tutorial-dim")) onDismiss();
      }
      : undefined}
  >
    {spotlight
      ? <div className="tutorial-spotlight" style={spotlightStyle} aria-hidden="true" />
      : <div className="tutorial-dim" aria-hidden="true" />}
    {spotlight && step.targetLabel && <span className="tutorial-target-label" style={spotlightStyle}>{step.targetLabel}</span>}
    {requiresTargetAction && <button
      ref={targetActionButton}
      type="button"
      className="tutorial-target-action"
      style={spotlightStyle}
      aria-label={`${step.targetLabel ?? "강조된 항목"} 직접 선택`}
      onClick={activateTarget}
    />}
    <div
      ref={wrapElement}
      className={`tutorial-dialogue-wrap ${wrapPlacement(spotlight, wrapHeight)}`}
      style={spotlightStyle}
    >
      {characterSprite
        ? <div className="tutorial-character tutorial-character-sprite" style={characterStyle}>
          <img src={characterSrc} alt={characterAlt} />
        </div>
        : <img className="tutorial-character tutorial-character-single" src={characterSrc} alt={characterAlt} />}
      <section className="tutorial-dialogue">
        <svg className="tutorial-dialogue-frame" viewBox="30 120 2112 460" preserveAspectRatio="none" aria-hidden="true" focusable="false">
          <image href={dialogueFrame} x="0" y="0" width="2172" height="724" />
        </svg>
        <span hidden>{chapterLabel}</span>
        <strong className="tutorial-nameplate" id="tutorial-speaker">한짝</strong>
        {requiresTargetAction
          ? <div className="tutorial-advance tutorial-target-instruction">
            <span id="tutorial-message">{step.message}</span>
          </div>
          : <button
            ref={advanceButton}
            type="button"
            className="tutorial-advance"
            onClick={advance}
            aria-label={`${step.message} ${stepIndex === steps.length - 1 ? "튜토리얼 완료" : "다음 대사"}`}
          >
            <span id="tutorial-message">{step.message}</span>
            <span className="tutorial-next-label" aria-hidden="true">
              {stepIndex === steps.length - 1 ? "완료" : "다음"}<i />
            </span>
          </button>}
        {stepIndex > 0 && <button
          type="button"
          className="tutorial-previous"
          onClick={back}
          aria-label={`이전 대사로 돌아가기, 전체 ${steps.length}단계 중 ${stepIndex}단계`}
        >
          <i aria-hidden="true" />이전
        </button>}
        <div className="tutorial-progress" aria-label={`전체 ${steps.length}단계 중 ${stepIndex + 1}단계`}>
          {steps.map((item, index) => <i key={item.id} className={index === stepIndex ? "active" : ""} />)}
        </div>
        <button type="button" className="tutorial-skip" onClick={onDismiss}>{dismissLabel}</button>
      </section>
    </div>
  </div>;
}
