import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { useQuery } from "@tanstack/react-query";
import { characterApi } from "../character/api";
import { equipmentApi } from "../equipment/api";
import { gemsApi } from "../gems/api";
import { skillsApi } from "../skills/api";
import { TutorialDialogue } from "./TutorialDialogue";
import {
  readCompletedTutorialIds,
  writeTutorialProgress,
  type TutorialProgressStatus,
} from "./tutorialProgress";
import { useTutorialPortalRoot } from "./tutorialPortalRoot";
import { TutorialScreenReplay } from "./TutorialScreenReplay";
import {
  TUTORIAL_RUNTIME_IDS,
  type TutorialRuntimeStep,
  type TutorialRuntimeSurface,
} from "./tutorialRuntimeCatalog";
import { selectNextTutorialChapter, tutorialTriggerQueries, type TutorialChapterStart } from "./tutorialTriggers";

/*
 * 조건이 맞아도 지금 이 순간이 안전한지는 화면이 안다. 다른 안내·보상·확인 창이 떠 있으면
 * 그 위에 또 덮지 않고 기다린다. 설계 근거: 점진형 온보딩 설계안 8절 "한 번에 오버레이는
 * 하나만 표시한다".
 * `[role="dialog"][aria-modal="true"]` 만 본다. 전투 화면의 획득 표시 설정은 늘 떠 있는
 * `aria-modal="false"` 판이라, 이것까지 세면 장이 영영 열리지 않는다.
 */
const BLOCKING_SELECTORS = [
  ".tutorial-layer",
  "dialog[open]",
  "[role=\"dialog\"][aria-modal=\"true\"]",
  ".offline-reward-panel",
  ".cozy-content-picker-backdrop",
];

/* 대상이 아직 그려지지 않았을 수 있다. 조금씩 다시 보되 영원히 기다리지는 않는다. */
const READINESS_INTERVAL_MS = 400;
const READINESS_TIMEOUT_MS = 20_000;
/* 사용자가 안내를 두고 다른 화면으로 나간 뒤에도 이 정도는 되돌아올 여유를 준다. */
const SURFACE_GRACE_MS = 900;

/*
 * 지금 설명하려는 화면 자체가 창인 경우가 있다. 장비·스킬·보석은 `<dialog open>` 으로
 * 열리므로, 그 창까지 방해물로 세면 정작 그 화면의 장이 영영 열리지 않는다. 설명 대상을
 * 품고 있는 창은 방해물이 아니다.
 */
function isPresentationSafe(screenRoot: HTMLElement | null): boolean {
  if (typeof document === "undefined") return false;
  return !BLOCKING_SELECTORS.some((selector) => [...document.querySelectorAll(selector)]
    .some((node) => !(screenRoot && node.contains(screenRoot))));
}

function isStepTargetReady(step: TutorialRuntimeStep | undefined): boolean {
  if (!step) return false;
  /* 대상이 늦게 그려질 수 있으니 첫 단계는 실제로 붙을 때까지 기다린다. 뒤 단계에서
     대상이 사라지면 TutorialDialogue 가 강조 없이 설명만 남기므로 화면이 잠기지 않는다. */
  return !step.target || document.querySelector(step.target) !== null;
}

type ProgressiveTutorialRuntimeProps = {
  accountId: string;
  /* 지금 열려 있는 화면. 자동 실행 대상이 아닌 화면에서는 null 을 준다. */
  surface: TutorialRuntimeSurface | null;
  /* 캐릭터 창·마이페이지처럼 화면 밖에서 이미 아는 차단 상태. */
  blocked?: boolean;
  onNavigate: (surface: TutorialRuntimeSurface) => void;
};

export function ProgressiveTutorialRuntime({ accountId, surface, blocked = false, onNavigate }: ProgressiveTutorialRuntimeProps) {
  /* 첫 그림 전에 읽는다. 나중에 읽으면 이미 다 끝낸 계정도 조건 조회를 한 번 더 부른다. */
  const [completed, setCompleted] = useState<ReadonlySet<string>>(
    () => readCompletedTutorialIds(window.localStorage, accountId, TUTORIAL_RUNTIME_IDS),
  );
  const [active, setActive] = useState<TutorialChapterStart | null>(null);
  const [expectedSurface, setExpectedSurface] = useState<TutorialRuntimeSurface | null>(null);
  const loadedAccount = useRef(accountId);

  useEffect(() => {
    if (loadedAccount.current === accountId) return;
    loadedAccount.current = accountId;
    setActive(null);
    setExpectedSurface(null);
    setCompleted(readCompletedTutorialIds(window.localStorage, accountId, TUTORIAL_RUNTIME_IDS));
  }, [accountId]);

  const wanted = useMemo(() => tutorialTriggerQueries(completed), [completed]);
  const enabled = Boolean(accountId);

  /*
   * 전투 화면은 이미 보석·스킬·캐릭터 상태를 받아 온다. 같은 키를 써서 응답을 나눠 쓰고,
   * 아직 끝나지 않은 장에 필요한 것만 켠다. 모두 끝낸 계정은 아무것도 더 부르지 않는다.
   */
  const equipment = useQuery({ queryKey: ["equipment"], queryFn: equipmentApi.state, retry: false, enabled: enabled && wanted.equipment, staleTime: 30_000 });
  const skills = useQuery({ queryKey: ["skills"], queryFn: skillsApi.state, retry: false, enabled: enabled && wanted.skills, staleTime: 30_000 });
  const gems = useQuery({ queryKey: ["gems"], queryFn: gemsApi.state, retry: false, enabled: enabled && wanted.gems, staleTime: 30_000 });
  const characterStats = useQuery({ queryKey: ["character-stats"], queryFn: characterApi.stats, retry: false, enabled: enabled && wanted.cosmetics, staleTime: 30_000 });

  const portalRoot = useTutorialPortalRoot(surface);

  const candidate = useMemo(() => {
    if (!enabled || active) return null;
    return selectNextTutorialChapter({
      surface,
      equipment: equipment.data,
      skills: skills.data,
      gems: gems.data,
      cosmeticsUnlocked: characterStats.data?.cosmeticsUnlocked,
    }, completed);
  }, [active, characterStats.data, completed, enabled, equipment.data, gems.data, skills.data, surface]);

  /* 조건이 맞은 뒤에도 화면이 조용해지고 대상이 그려질 때까지 기다렸다가 연다. */
  useEffect(() => {
    if (!candidate || blocked) return;
    let waited = 0;
    const timer = window.setInterval(() => {
      /* 다른 창이 떠 있는 동안은 시간을 세지 않는다. 오래 걸리는 안내 하나가 이 장을
         영영 삼켜 버리면 안 된다. 시간 제한은 끝내 나타나지 않는 대상에만 건다. */
      if (!isPresentationSafe(portalRoot)) return;
      waited += READINESS_INTERVAL_MS;
      if (waited > READINESS_TIMEOUT_MS) {
        window.clearInterval(timer);
        return;
      }
      const firstStep = candidate.chapter.steps[candidate.startIndex];
      if (!isStepTargetReady(firstStep)) return;
      window.clearInterval(timer);
      setExpectedSurface(firstStep.surface);
      setActive(candidate);
    }, READINESS_INTERVAL_MS);
    return () => window.clearInterval(timer);
  }, [blocked, candidate, portalRoot]);

  /*
   * 안내 오버레이가 뒤쪽 조작을 막지만, 브라우저 뒤로 가기나 화면 한켠처럼 밖에서 화면이
   * 바뀌는 길은 남아 있다. 그때는 조용히 닫기만 하고 완료로 적지 않는다. 조건이 그대로면
   * 다음에 그 화면으로 돌아왔을 때 다시 안내한다.
   */
  useEffect(() => {
    if (!active || !expectedSurface || surface === expectedSurface) return;
    const timer = window.setTimeout(() => {
      setActive(null);
      setExpectedSurface(null);
    }, SURFACE_GRACE_MS);
    return () => window.clearTimeout(timer);
  }, [active, expectedSurface, surface]);

  const finish = useCallback((tutorialId: string, status: TutorialProgressStatus) => {
    writeTutorialProgress(window.localStorage, accountId, tutorialId, status);
    setCompleted((current) => new Set([...current, tutorialId]));
    setActive(null);
    setExpectedSurface(null);
  }, [accountId]);

  /* 자동 안내가 열려 있는 동안에는 다시 보기 단추가 안내에 가려 눌리지 않는다. */
  const replay = <TutorialScreenReplay surface={surface} />;
  if (!active) return replay;
  const dialogue = <TutorialDialogue
      key={`${accountId}:${active.chapter.tutorialId}`}
      chapterLabel={active.chapter.label}
      characterSrc={active.chapter.characterSrc}
      characterAlt={active.chapter.characterAlt}
      characterSprite={{ columns: 3, rows: 2, frames: [0, 1, 2, 5] }}
      steps={active.chapter.steps.slice(active.startIndex)}
      isTargetAction={(step) => (step as TutorialRuntimeStep).advanceMode === "target"}
      onStepChange={(step) => {
        const next = (step as TutorialRuntimeStep).surface;
        setExpectedSurface(next);
        onNavigate(next);
      }}
      onComplete={() => finish(active.chapter.tutorialId, "COMPLETED")}
      onDismiss={() => finish(active.chapter.tutorialId, "DISMISSED")}
    />;
  return <>
    {replay}
    {/* `showModal()` 로 연 화면은 top layer 다. 안내를 그 밖에 두면 설명 대상 아래로 깔린다. */}
    {portalRoot ? createPortal(dialogue, portalRoot) : dialogue}
  </>;
}
