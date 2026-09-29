import { useRef, useState } from "react";
import { createPortal } from "react-dom";
import { TutorialDialogue } from "./TutorialDialogue";
import { BATTLE_GUIDE_TUTORIAL_STEPS } from "./tutorialContent";
import "./TutorialLauncher.css";

/*
 * 첫 안내를 건너뛰었거나 이미 끝낸 모험가도 게임 방법을 다시 볼 수 있어야 한다.
 * 전투 기록 아래의 왼쪽 자리에서 같은 종이 단추로 안내서를 연다.
 */
export function TutorialLauncher() {
  const [open, setOpen] = useState(false);
  const trigger = useRef<HTMLButtonElement>(null);

  const close = () => {
    setOpen(false);
    trigger.current?.focus();
  };

  return <div className={`cozy-tutorial-launcher ${open ? "is-open" : ""}`}>
    <button
      ref={trigger}
      type="button"
      className="cozy-tutorial-trigger cozy-paper"
      aria-haspopup="dialog"
      aria-expanded={open}
      onClick={() => setOpen(true)}
    >
      <span className="cozy-tutorial-guide" aria-hidden="true" />
      <span>튜토리얼 보기</span>
      <b aria-hidden="true"><i className="cozy-chevron is-next" /></b>
    </button>
    {/* HUD 는 zoom 으로 줄고 채팅은 더 위에 뜬다. 안내서는 그 바깥, 화면 전체에 깔아야 한다. */}
    {open && createPortal(<TutorialDialogue
      chapterLabel="게임 방법 안내"
      steps={BATTLE_GUIDE_TUTORIAL_STEPS}
      dismissLabel="닫기"
      dismissOnBackdrop
      onComplete={close}
      onDismiss={close}
    />, document.body)}
  </div>;
}
