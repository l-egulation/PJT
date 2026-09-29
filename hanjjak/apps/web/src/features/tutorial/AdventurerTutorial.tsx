import { useCallback, useEffect, useState } from "react";
import { TutorialDialogue } from "./TutorialDialogue";
import { ADVENTURER_TUTORIAL_STEPS } from "./tutorialContent";
import { ADVENTURER_TUTORIAL_ID, readTutorialProgress, writeTutorialProgress } from "./tutorialProgress";

type AdventurerTutorialProps = {
  accountId: string;
  active: boolean;
};

export function AdventurerTutorial({ accountId, active }: AdventurerTutorialProps) {
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    if (!active || !accountId) {
      setVisible(false);
      return;
    }
    if (readTutorialProgress(window.localStorage, accountId, ADVENTURER_TUTORIAL_ID)) {
      setVisible(false);
      return;
    }
    const timer = window.setTimeout(() => setVisible(true), 450);
    return () => window.clearTimeout(timer);
  }, [accountId, active]);

  const finish = useCallback((status: "COMPLETED" | "DISMISSED") => {
    writeTutorialProgress(window.localStorage, accountId, ADVENTURER_TUTORIAL_ID, status);
    setVisible(false);
  }, [accountId]);

  if (!visible) return null;
  return <TutorialDialogue
    chapterLabel="모험가의 장"
    steps={ADVENTURER_TUTORIAL_STEPS}
    onComplete={() => finish("COMPLETED")}
    onDismiss={() => finish("DISMISSED")}
  />;
}
