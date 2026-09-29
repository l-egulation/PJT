import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { TutorialDialogue } from "./TutorialDialogue";
import { useTutorialPortalRoot } from "./tutorialPortalRoot";
import {
  chapterForSurface,
  chapterStepsForSurface,
  type TutorialRuntimeSurface,
} from "./tutorialRuntimeCatalog";
import "./TutorialScreenReplay.css";

type TutorialScreenReplayProps = {
  surface: TutorialRuntimeSurface | null;
};

/*
 * 자동 안내는 한 계정에서 한 번만 열린다. 잘못 눌러 닫았거나 조건이 지나간 뒤에 다시
 * 보고 싶을 수 있으므로, 화면마다 그 화면의 안내를 다시 여는 단추를 왼쪽 아래에 둔다.
 * 다시 보기는 자동 실행 완료 상태를 읽지도 쓰지도 않는다. 전투 화면은 이미
 * `TutorialLauncher` 가 같은 일을 하므로 여기서는 제외한다.
 */
export function TutorialScreenReplay({ surface }: TutorialScreenReplayProps) {
  const chapter = chapterForSurface(surface);
  const root = useTutorialPortalRoot(surface);
  const [open, setOpen] = useState(false);
  const trigger = useRef<HTMLButtonElement>(null);

  /* 화면을 옮기면 열려 있던 다시 보기는 설명할 대상을 잃는다. 같이 닫는다. */
  useEffect(() => setOpen(false), [surface]);

  /* 전투 화면은 `TutorialLauncher` 가 같은 자리를 이미 쓴다. */
  if (!surface || surface === "battle" || !chapter) return null;
  const steps = chapterStepsForSurface(chapter, surface);
  if (steps.length === 0) return null;

  const close = () => {
    setOpen(false);
    trigger.current?.focus();
  };

  return <>
    {createPortal(<button
      ref={trigger}
      type="button"
      className="tutorial-replay-pin"
      data-surface={surface}
      aria-haspopup="dialog"
      aria-expanded={open}
      onClick={() => setOpen(true)}
    >
      <span className="tutorial-replay-pin-face" aria-hidden="true" />
      <span>{chapter.label} 다시 보기</span>
    </button>, root ?? document.body)}
    {/* 안내도 같은 창 안에 심는다. 밖에 두면 top layer 창 아래로 깔려 보이지 않는다. */}
    {open && createPortal(<TutorialDialogue
      key={`${chapter.id}:${surface}`}
      chapterLabel={chapter.label}
      characterSrc={chapter.characterSrc}
      characterAlt={chapter.characterAlt}
      characterSprite={{ columns: 3, rows: 2, frames: [0, 1, 2, 5] }}
      steps={steps}
      dismissLabel="닫기"
      dismissOnBackdrop
      onComplete={close}
      onDismiss={close}
    />, root ?? document.body)}
  </>;
}
