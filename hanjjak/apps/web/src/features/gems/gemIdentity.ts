import type { GemOption } from "./api";

/*
 * 보석 아이디는 `gem:<레벨>:<옵션>` 꼴이다. 레벨과 옵션이 아이디에 들어 있어서
 * 그림(등급별 모양·옵션별 색)을 그리는 데 따로 물어볼 필요가 없다. 인벤토리와
 * 거래소가 같은 규칙을 봐야 하므로 여기 한 곳에 둔다.
 */
const GEM_OPTION_IDS: Record<string, GemOption> = {
  flat_attack: "FLAT_ATTACK", flat_hp: "FLAT_HP", attack_percent: "ATTACK_PERCENT",
  flat_penetration: "FLAT_PENETRATION", critical_chance: "CRITICAL_CHANCE", attack_speed: "HASTE",
};

export type GemIdentity = { level: number; option: GemOption };

export function gemIdentityFromItemId(itemId: string): GemIdentity | null {
  const parsed = /^gem:(\d+):([a-z_]+)$/.exec(itemId);
  const option = parsed && GEM_OPTION_IDS[parsed[2]];
  return option ? { level: Number(parsed[1]), option } : null;
}
