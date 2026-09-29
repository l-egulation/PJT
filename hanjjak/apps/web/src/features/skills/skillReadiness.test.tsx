// @vitest-environment happy-dom
import { describe, expect, it } from "vitest";
import { skillReadiness } from "./SkillsScreen";
import type { SkillSummary } from "./api";

function skill(action: Partial<SkillSummary["action"]>): SkillSummary {
  return {
    skillId: "active_heavy", name: "한짝의 일격", active: true, unlocked: true,
    grade: "NORMAL", gradeName: "노말", level: 3, equippedSlot: null, effectText: "",
    action: {
      kind: "ENHANCE", targetGrade: "NORMAL", targetLevel: 4, books: [], riceCost: 100,
      successBasisPoints: 10_000, executable: false, disabledReason: null, ...action,
    },
  };
}

/* 카드 여섯 장을 하나씩 눌러 보지 않고도 무엇이 올라가는지 알아야 한다. */
describe("skillReadiness", () => {
  it("says what would happen when the skill can be raised right now", () => {
    expect(skillReadiness(skill({ executable: true }))).toEqual({ label: "강화 가능", tone: "ready" });
    expect(skillReadiness(skill({ kind: "PROMOTE", executable: true }))).toEqual({ label: "승급 가능", tone: "ready" });
    expect(skillReadiness(skill({ kind: "UNLOCK", executable: true }))).toEqual({ label: "해금 가능", tone: "ready" });
  });

  it("names the missing thing instead of just saying no", () => {
    expect(skillReadiness(skill({ disabledReason: "INSUFFICIENT_SKILLBOOK" }))).toEqual({ label: "스킬북 부족", tone: "short" });
    expect(skillReadiness(skill({ disabledReason: "INSUFFICIENT_RICE" }))).toEqual({ label: "쌀 부족", tone: "short" });
  });

  /* 까닭을 서버가 말해 주지 않아도 칸을 비워 두지 않는다. */
  it("falls back to a plain reason when the server gives none", () => {
    expect(skillReadiness(skill({}))).toEqual({ label: "조건 미달", tone: "short" });
  });

  it("reads as done, not blocked, once there is nowhere left to go", () => {
    expect(skillReadiness(skill({ kind: "COMPLETE", executable: false }))).toEqual({ label: "최대", tone: "done" });
  });
});
