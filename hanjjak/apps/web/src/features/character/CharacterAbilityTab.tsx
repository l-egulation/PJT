import { experienceProgress } from "../battle/battlePresentation";
import type { CSSProperties, ReactNode } from "react";
import { CosmeticHero } from "../cosmetics/CosmeticHero";
import type { Catalog } from "../cosmetics/api";
import attackIcon from "./assets-stats-v2/icon-attack.png";
import attackSpeedIcon from "./assets-stats-v2/icon-attack-speed.png";
import basicAttackIcon from "./assets-stats-v2/icon-basic-attack.png";
import buffDurationIcon from "./assets-stats-v2/icon-buff-duration.png";
import criticalIcon from "./assets-stats-v2/icon-critical.png";
import damageUpIcon from "./assets-stats-v2/icon-damage-up.png";
import hpIcon from "./assets-stats-v2/icon-hp.png";
import penetrationIcon from "./assets-stats-v2/icon-penetration.png";
import panelAttack from "./assets-stats-v2/panel-attack-sage.png";
import panelHp from "./assets-stats-v2/panel-hp-coral.png";
import panelPenetration from "./assets-stats-v2/panel-penetration-mustard.png";
import panelProfile from "./assets-stats-v2/panel-profile-blue.png";
import combatPowerPlaque from "./assets-stats-v2/combat-power-plaque.png";
import expFill from "./assets-stats-v2/exp-fill.png";
import expTrack from "./assets-stats-v2/exp-track.png";
import restCharacter from "./assets-stats-v2/character-rest-192.png";
import type { CharacterStat, CharacterStats } from "./api";

type CharacterAppearance = {
  equipment?: Record<string, string | null>;
  catalog?: Pick<Catalog, "cosmetics" | "sets">;
};

export function formatCharacterStat(value: number, unit: string) {
  const number = unit === "BASIS_POINTS" || unit === "PERCENTAGE_POINTS" ? value / 100 : value;
  const suffix = unit === "BASIS_POINTS" ? "%" : unit === "PERCENTAGE_POINTS" ? "%p" : unit === "SECONDS" ? "초" : "";
  return `${number.toLocaleString("ko-KR", { maximumFractionDigits: 4 })}${suffix}`;
}

function statValue(stat: CharacterStat | undefined, fallback = "—") {
  return stat ? formatCharacterStat(stat.total, stat.unit) : fallback;
}

function AbilityHero({ equipment, catalog }: CharacterAppearance) {
  return <CosmeticHero
    className="ability-fold-hero"
    equipment={equipment}
    catalog={catalog}
    alt="착용한 치장을 입은 한짝 캐릭터"
    fallback={<img className="ability-fold-hero" src={restCharacter} alt="나무검을 든 한짝 캐릭터" />}
  />;
}

function DetailRow({ icon, label, stat, fallback }: { icon: string; label: string; stat?: CharacterStat; fallback?: string }) {
  return <div className="ability-fold-detail">
    <img src={icon} alt="" aria-hidden="true" />
    <span>{label}</span>
    <strong>{statValue(stat, fallback)}</strong>
  </div>;
}

function StatPanel({ tone, art, icon, title, main, children }: {
  tone: "hp" | "attack" | "penetration";
  art: string;
  icon: string;
  title: string;
  main?: CharacterStat;
  children: ReactNode;
}) {
  /* 머리띠에는 그림처럼 흰 아이콘만 둔다. 이름과 값은 바로 아래 본문이 갖고 있어
     같은 글자를 두 번 읽히지 않도록 칸 이름만 aria-label 로 남긴다. */
  return <article className={`ability-fold-panel is-${tone}`} aria-label={title} style={{ "--ability-panel-art": `url(${art})` } as CSSProperties}>
    <header><img src={icon} alt="" aria-hidden="true" /></header>
    <div className="ability-fold-main"><img src={icon} alt="" aria-hidden="true" /><span>{title}</span><strong>{statValue(main)}</strong></div>
    <div className="ability-fold-details">{children}</div>
  </article>;
}

function ProfilePanel({ data, equipment, catalog, combatPower }: { data: CharacterStats; combatPower?: number | null } & CharacterAppearance) {
  const xp = experienceProgress(data.level, data.experience);
  const power = combatPower ?? data.combatPower ?? data.stats.reduce((total, stat) => total + Math.max(0, stat.total), 0);
  return <article className="ability-fold-panel is-profile" style={{ "--ability-panel-art": `url(${panelProfile})` } as CSSProperties}>
    <header><span>{data.nickname}</span></header>
    <div className="ability-fold-stage"><AbilityHero equipment={equipment} catalog={catalog} /></div>
    <strong className="ability-fold-level">LV.{data.level.toLocaleString()}</strong>
    {/* 글자를 막대 옆에 두면 막대가 그만큼 짧아져 칸 오른쪽에서 잘린다.
        이름과 숫자는 위 한 줄로 올리고 막대는 칸 폭을 그대로 쓴다. */}
    <div className="ability-fold-exp">
      <b>EXP</b>
      <small>{xp.current.toLocaleString()} / {xp.required.toLocaleString()}</small>
      <div className="ability-fold-exp-track" role="progressbar" aria-label="현재 레벨 경험치" aria-valuemin={0} aria-valuemax={Math.max(1, xp.required)} aria-valuenow={xp.current} style={{ "--ability-exp-track": `url(${expTrack})` } as CSSProperties}>
        <i style={{ width: `${xp.percent}%`, backgroundImage: `url(${expFill})` }} />
      </div>
    </div>
    {/* 그림과 숫자면 충분하다. 이름은 읽어 주는 쪽에만 남긴다. */}
    <div className="ability-fold-power" style={{ backgroundImage: `url(${combatPowerPlaque})` }} aria-label={`전투력 ${power.toLocaleString()}`}><strong>{power.toLocaleString()}</strong></div>
  </article>;
}

export function CharacterAbilityTab({ data, equipment, catalog, combatPower }: { data: CharacterStats; combatPower?: number | null } & CharacterAppearance) {
  const byId = new Map(data.stats.map((stat) => [stat.statId, stat]));
  return <section className="ability-fold-grid" aria-label="캐릭터 능력치">
    <ProfilePanel data={data} equipment={equipment} catalog={catalog} combatPower={combatPower} />
    <StatPanel tone="hp" art={panelHp} icon={hpIcon} title="최대 HP" main={byId.get("maxHp")}>
      {/* 이동속도는 쓰지 않는 값이라 뺀다. 아랫자리는 비워 둔다. */}
      <DetailRow icon={buffDurationIcon} label="버프 지속시간" stat={byId.get("buffDuration")} fallback="0%" />
    </StatPanel>
    <StatPanel tone="attack" art={panelAttack} icon={attackIcon} title="공격력" main={byId.get("attack")}>
      <DetailRow icon={criticalIcon} label="치명타 확률" stat={byId.get("criticalChance")} fallback="0%" />
      <DetailRow icon={basicAttackIcon} label="기본공격 피해" stat={byId.get("basicAttackDamage")} fallback="0%" />
    </StatPanel>
    <StatPanel tone="penetration" art={panelPenetration} icon={penetrationIcon} title="관통력" main={byId.get("penetration")}>
      <DetailRow icon={damageUpIcon} label="피해량 증가" stat={byId.get("allDamage")} fallback="0%" />
      <DetailRow icon={attackSpeedIcon} label="공격속도" stat={byId.get("attackSpeed")} fallback="0%" />
    </StatPanel>
  </section>;
}
