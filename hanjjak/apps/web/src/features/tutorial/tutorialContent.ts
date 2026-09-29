export type TutorialStep = {
  id: string;
  message: string;
  target?: string;
  targetLabel?: string;
};

export const ADVENTURER_TUTORIAL_STEPS: TutorialStep[] = [
  {
    id: "welcome",
    message: "반가워! 나는 네 모험을 도와줄 한짝이야. 먼저 전투 화면부터 같이 살펴보자!",
  },
  {
    id: "player-status",
    message: "여기서 내 레벨과 HP를 확인할 수 있어. 쓰러져도 잠시 뒤 다시 도전하니 걱정 마!",
    target: ".cozy-player-status",
    targetLabel: "캐릭터 상태",
  },
  {
    id: "stage-progress",
    message: "지금 도전 중인 스테이지와 남은 시간을 볼 수 있어. 전투는 자동으로 진행돼!",
    target: ".cozy-stage-progress",
    targetLabel: "스테이지 진행",
  },
  {
    id: "rewards",
    message: "전투에서 얻은 쌀과 재료는 여기에 차곡차곡 쌓여. 무엇을 얻었는지 언제든 펼쳐 볼 수 있어!",
    target: ".cozy-rewards",
    targetLabel: "획득 보상",
  },
  {
    id: "picture-in-picture",
    message: "다른 일을 하면서도 한짝을 보고 싶다면 화면 한켠 버튼을 눌러 봐. 작은 창으로 모험을 계속 지켜볼 수 있어!",
    target: ".picture-in-picture-shell",
    targetLabel: "화면 한켠",
  },
];

/*
 * 언제든 다시 열어 보는 안내서. 모험가의 장 대사를 그대로 쓰고, 첫 안내에서 빼 두었던
 * 화면 칸을 사이에 잇는다. 화면 한켠은 첫 안내와 같이 마지막 대사로 남긴다.
 */
export const BATTLE_GUIDE_TUTORIAL_STEPS: TutorialStep[] = [
  ...ADVENTURER_TUTORIAL_STEPS.filter((step) => step.id !== "picture-in-picture"),
  {
    id: "guide-stage-actions",
    message: "스테이지는 여기서 고르고, 자동 진행과 반복도 바꿀 수 있어. 어려우면 이긴 스테이지를 반복해 재료를 모으자!",
    target: ".cozy-stage-actions",
    targetLabel: "스테이지 선택",
  },
  {
    id: "guide-growth-nav",
    message: "아래 다섯 칸은 성장 메뉴야. 캐릭터, 장비, 아이템, 스킬, 보석을 여기서 돌보면 전투가 훨씬 수월해져!",
    target: ".cozy-growth-nav",
    targetLabel: "성장 메뉴",
  },
  {
    id: "guide-utility-nav",
    message: "위쪽은 마을 메뉴야. 치장 뽑기, 던전, 거래소, 랭킹, 메시지, 설정으로 이어져 있어.",
    target: ".cozy-utility-nav",
    targetLabel: "마을 메뉴",
  },
  {
    id: "guide-history",
    message: "지나간 전투의 승패와 얻은 것은 전투 기록에 남아. 어디서 막혔는지 확인할 때 열어 봐!",
    target: ".cozy-history",
    targetLabel: "전투 기록",
  },
  ...ADVENTURER_TUTORIAL_STEPS.filter((step) => step.id === "picture-in-picture"),
];
