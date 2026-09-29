import React from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { SkillWindow } from "./SkillsScreen";
import type { SkillActionSummary, SkillState } from "./api";
import "../../styles.css";

function action(kind: "ENHANCE" | "PROMOTE", riceCost: number, bookCost: number): SkillActionSummary {
  return {
    kind,
    targetGrade: kind === "PROMOTE" ? "RARE" : null,
    targetLevel: kind === "PROMOTE" ? 1 : 2,
    books: [{ itemId: "SKILL_BOOK", displayName: "스킬 비법서", requiredQuantity: bookCost, availableQuantity: 38 }],
    riceCost,
    successBasisPoints: kind === "PROMOTE" ? 10_000 : 7_000,
    executable: true,
    disabledReason: null,
  };
}

const previewState: SkillState = {
  riceBalance: 61_238,
  activeLoadout: ["active_haste", "active_dot", "active_basic_amp"],
  skills: [
    { skillId: "active_heavy", name: "한짝의 일격", active: true, unlocked: true, grade: "NORMAL", gradeName: "노말", level: 10, equippedSlot: null, effectText: "강한 일격으로 공격력의 455% 피해", action: action("PROMOTE", 1_000, 4) },
    { skillId: "active_dot", name: "마! 쫄이나", active: true, unlocked: true, grade: "NORMAL", gradeName: "노말", level: 7, equippedSlot: 2, effectText: "5초 동안 총 865% 지속 피해", action: action("ENHANCE", 300, 2) },
    { skillId: "active_haste", name: "잘게 더 잘게!", active: true, unlocked: true, grade: "NORMAL", gradeName: "노말", level: 7, equippedSlot: 1, effectText: "8초 동안 공격 속도 18% 증가", action: action("ENHANCE", 210, 2) },
    { skillId: "active_basic_amp", name: "화력 최대로", active: true, unlocked: true, grade: "NORMAL", gradeName: "노말", level: 7, equippedSlot: 3, effectText: "10초 동안 기본 공격 피해 24% 증가", action: action("ENHANCE", 210, 2) },
    { skillId: "passive_critical", name: "회심의 간", active: false, unlocked: true, grade: "NORMAL", gradeName: "노말", level: 7, equippedSlot: null, effectText: "치명타 확률 7%, 치명타 피해 14% 증가", action: action("ENHANCE", 210, 2) },
    { skillId: "passive_all_damage", name: "오늘의 특선", active: false, unlocked: true, grade: "NORMAL", gradeName: "노말", level: 7, equippedSlot: null, effectText: "모든 피해 12% 증가", action: action("ENHANCE", 210, 2) },
  ],
};

const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: Number.POSITIVE_INFINITY } } });
queryClient.setQueryData(["skills"], previewState);

createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <main className="skills-preview-stage" aria-label="스킬 UI 미리보기">
        <SkillWindow open onClose={() => undefined} />
      </main>
    </QueryClientProvider>
  </React.StrictMode>,
);
