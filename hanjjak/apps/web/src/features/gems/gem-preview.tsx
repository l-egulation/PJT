import React, { useEffect, useState } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { GemManagement, GemManagementWindow } from "./GemManagement";
import { GemBoxRewardDialog } from "../inventory/InventoryScreen";
import type { GemOption, GemState, GemSummary } from "./api";
import "../../styles.css";

const optionNames: Record<GemOption, string> = {
  FLAT_ATTACK: "고정 공격력",
  FLAT_HP: "고정 최대 HP",
  ATTACK_PERCENT: "공격력%",
  FLAT_PENETRATION: "고정 방어 관통",
  CRITICAL_CHANCE: "치명타 확률",
  HASTE: "공격속도",
};

const baseValues: Record<GemOption, number> = {
  FLAT_ATTACK: 60,
  FLAT_HP: 400,
  ATTACK_PERCENT: 150,
  FLAT_PENETRATION: 40,
  CRITICAL_CHANCE: 120,
  HASTE: 90,
};

let sequence = 0;
function gem(level: number, option: GemOption, extra: Partial<GemSummary> = {}): GemSummary {
  sequence += 1;
  return {
    gemId: `preview-gem-${String(sequence).padStart(2, "0")}`,
    slotId: `preview-slot-${String(sequence).padStart(2, "0")}`,
    level,
    option,
    optionName: optionNames[option],
    value: baseValues[option] * level,
    locked: false,
    reservedForSale: false,
    equippedPresets: [],
    ...extra,
  };
}

// 1레벨 6개 · 2레벨 6개 · 3레벨 4개 · 4레벨 2개 = 일괄합성 시 5레벨 1개
const gems: GemSummary[] = [
  gem(2, "FLAT_ATTACK"),
  gem(2, "FLAT_ATTACK"),
  gem(2, "FLAT_ATTACK"),
  gem(3, "FLAT_HP", { locked: true }),
  gem(1, "FLAT_ATTACK"),
  gem(1, "ATTACK_PERCENT"),
  gem(1, "CRITICAL_CHANCE"),
  gem(3, "HASTE", { equippedPresets: ["MAIN"] }),
  gem(1, "FLAT_HP"),
  gem(1, "FLAT_PENETRATION"),
  gem(2, "HASTE"),
  gem(1, "FLAT_ATTACK"),
  gem(2, "FLAT_HP"),
  gem(2, "CRITICAL_CHANCE"),
  gem(2, "ATTACK_PERCENT"),
  gem(3, "FLAT_ATTACK"),
  gem(3, "ATTACK_PERCENT"),
  gem(3, "FLAT_PENETRATION"),
  gem(3, "CRITICAL_CHANCE"),
  gem(4, "FLAT_ATTACK"),
  gem(4, "FLAT_HP"),
  gem(5, "FLAT_ATTACK", { reservedForSale: true }),
];

const previewState: GemState = {
  unlocked: true,
  tickets: 3,
  secondsUntilNextTicket: 5_400,
  todayBoss: "SURVIVAL",
  gemBoxQuantity: 26,
  gems,
  presets: { MAIN: gems.filter((item) => item.equippedPresets.length > 0) },
  lockedPresets: [],
  contentVersion: "preview",
};

const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: Number.POSITIVE_INFINITY } } });
queryClient.setQueryData(["gems"], previewState);
queryClient.setQueryData(["auth", "session"], { account: { rice: 42_000 } });

// 실제 창 크기(가방·장비 창과 같은 1320×790)를 그대로 배치한 뒤, 좁은 화면에서는 축소해서 전체를 한눈에 본다.
const FRAME_WIDTH = 1320;
const FRAME_HEIGHT = 790;

function PreviewStage() {
  const [scale, setScale] = useState(1);
  // ?w=1320&h=647 처럼 창 크기를 바꿔가며 눌린 곳이 없는지 본다.
  const params = new URLSearchParams(window.location.search);
  const width = Number(params.get("w")) || FRAME_WIDTH;
  const height = Number(params.get("h")) || FRAME_HEIGHT;
  useEffect(() => {
    // ?scale=0.6 처럼 적으면 그 배율로 고정한다. 좁은 칸에서 한 곳을 크게 볼 때 쓴다.
    const fixed = Number(new URLSearchParams(window.location.search).get("scale"));
    const fit = () => setScale(fixed > 0 ? fixed : Math.min(1, (window.innerWidth - 24) / width, (window.innerHeight - 24) / height));
    fit();
    window.addEventListener("resize", fit);
    return () => window.removeEventListener("resize", fit);
  }, [width, height]);
  // ?boxreward=1 이면 보석함 결과 알림만 띄운다.
  if (params.get("boxreward") === "1") {
    return <main className="gem-preview-stage" aria-label="보석함 결과 미리보기"><GemBoxRewardDialog gems={gems.slice(0, 9).concat(gems[0]!, gems[0]!)} onClose={() => undefined} /></main>;
  }
  // ?window=1 이면 실제 대화상자로 띄운다. 뒤 화면이 흐려지는지와 창 크기를 그대로 확인한다.
  if (new URLSearchParams(window.location.search).get("window") === "1") {
    return <main className="gem-preview-stage" aria-label="보석 창 미리보기">
      <GemManagementWindow open onClose={() => undefined} />
    </main>;
  }
  return <main className="gem-preview-stage" aria-label="보석 UI 미리보기">
    <div className="gem-preview-viewport" style={{ width: width * scale, height: height * scale }}>
      <div className="gem-preview-frame" style={{ width, height, transform: `scale(${scale})` }}>
        <GemManagement onClose={() => undefined} />
      </div>
    </div>
  </main>;
}

createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <PreviewStage />
    </QueryClientProvider>
  </React.StrictMode>,
);
