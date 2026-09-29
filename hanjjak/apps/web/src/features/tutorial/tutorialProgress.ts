export const ADVENTURER_TUTORIAL_ID = "adventurer-intro-v1";

export type TutorialProgressStatus = "COMPLETED" | "DISMISSED";

type StoredTutorialProgress = {
  status: TutorialProgressStatus;
  updatedAt: string;
};

const STORAGE_PREFIX = "hanjjak.tutorial-progress";

export function tutorialProgressKey(accountId: string, tutorialId: string): string {
  return `${STORAGE_PREFIX}:${accountId}:${tutorialId}`;
}

export function readTutorialProgress(
  storage: Pick<Storage, "getItem">,
  accountId: string,
  tutorialId: string,
): TutorialProgressStatus | null {
  try {
    const raw = storage.getItem(tutorialProgressKey(accountId, tutorialId));
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Partial<StoredTutorialProgress>;
    return parsed.status === "COMPLETED" || parsed.status === "DISMISSED" ? parsed.status : null;
  } catch {
    return null;
  }
}

export function writeTutorialProgress(
  storage: Pick<Storage, "setItem">,
  accountId: string,
  tutorialId: string,
  status: TutorialProgressStatus,
): void {
  try {
    storage.setItem(tutorialProgressKey(accountId, tutorialId), JSON.stringify({
      status,
      updatedAt: new Date().toISOString(),
    } satisfies StoredTutorialProgress));
  } catch {
    // 저장소가 차단된 환경에서도 가이드를 닫는 동작 자체는 막지 않는다.
  }
}

/*
 * 자동 실행 대상 장은 여러 개다. 하나씩 물어보면 화면이 바뀔 때마다 저장소를 여러 번
 * 읽게 되므로, 한 번에 끝난 장만 모아 둔다.
 */
export function readCompletedTutorialIds(
  storage: Pick<Storage, "getItem">,
  accountId: string,
  tutorialIds: readonly string[],
): ReadonlySet<string> {
  const completed = new Set<string>();
  if (!accountId) return completed;
  for (const tutorialId of tutorialIds) {
    if (readTutorialProgress(storage, accountId, tutorialId)) completed.add(tutorialId);
  }
  return completed;
}
