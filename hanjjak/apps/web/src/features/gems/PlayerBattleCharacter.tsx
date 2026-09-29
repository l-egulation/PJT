import { useEffect, useState } from "react";
import type { Catalog, Collection } from "../cosmetics/api";
import { CosmeticHero } from "../cosmetics/CosmeticHero";
import { composeCosmeticHero, composeCosmeticHeroMotion, cosmeticSlugsBySlot, preloadCosmeticHeroMotion } from "../cosmetics/cosmetic-art";
import { runtimeLoadoutFor } from "../cosmetics/cosmeticRuntimePackages";
import { CosmeticRuntimeHero } from "../cosmetics/CosmeticRuntimeHero";
import { heroFrame, heroMotionFrame, preloadHeroCombatFrames, type HeroMotion } from "../battle/battleVisuals";
import { SkillVfxSprite, type SkillVfx } from "./skillVfx";

export function equippedCosmeticIds(appearance?: Pick<Collection, "equipment">): string[] {
  return Object.values(appearance?.equipment ?? {}).filter((id): id is string => Boolean(id)).sort();
}

export function MasterHero({ motion, actionEventId, skillVfx }: { motion: HeroMotion; actionEventId?: string; skillVfx: SkillVfx[] }) {
  const [frame, setFrame] = useState(0);
  const crownVfx = skillVfx.filter((effect) => effect.kind === "haste-caster" || effect.kind === "basic-amp-caster");
  useEffect(() => {
    preloadHeroCombatFrames();
  }, []);
  useEffect(() => {
    const startedAt = performance.now();
    setFrame(0);
    let requestId = 0;
    const animate = (now: number) => {
      const next = heroMotionFrame(motion, now - startedAt);
      setFrame(current => current === next ? current : next);
      requestId = requestAnimationFrame(animate);
    };
    requestId = requestAnimationFrame(animate);
    return () => cancelAnimationFrame(requestId);
  }, [motion, actionEventId]);
  return <div className={`master-hero motion-${motion}`} data-frame={frame + 1} data-action={actionEventId}>
    <div className="hero-shadow" aria-hidden="true" />
    {crownVfx.length > 0 && <div className="hero-crown-vfx" aria-hidden="true">
      {crownVfx.map((effect) => <SkillVfxSprite key={effect.kind} effect={effect} />)}
    </div>}
    <img src={heroFrame(motion, frame)} alt="기본 나무검을 든 마스터 젓가락" draggable={false} />
  </div>;
}

/** 아트가 프레임마다 겹침 순서를 정해 둔 세트. 매니페스트를 그대로 따라 그린다. */
function CosmeticManifestHero({ runtimePackage, equipped, motion, actionEventId, hit, skillVfx }: {
  runtimePackage: ReturnType<typeof runtimeLoadoutFor> extends null ? never : NonNullable<ReturnType<typeof runtimeLoadoutFor>>["runtimePackage"];
  equipped: NonNullable<ReturnType<typeof runtimeLoadoutFor>>["equipped"];
  motion: HeroMotion;
  actionEventId?: string;
  hit: boolean;
  skillVfx: SkillVfx[];
}) {
  const [frame, setFrame] = useState(0);
  useEffect(() => {
    const startedAt = performance.now();
    setFrame(0);
    let requestId = 0;
    const animate = (now: number) => {
      setFrame(heroMotionFrame(motion, now - startedAt));
      requestId = requestAnimationFrame(animate);
    };
    requestId = requestAnimationFrame(animate);
    return () => cancelAnimationFrame(requestId);
  }, [motion, actionEventId]);
  const crownVfx = skillVfx.filter((effect) => effect.kind === "haste-caster" || effect.kind === "basic-amp-caster");
  return <div className={`dungeon-current-character ${hit ? "is-hit" : ""}`} data-frame={frame + 1}>
    {crownVfx.length > 0 && <div className="hero-crown-vfx" aria-hidden="true">{crownVfx.map((effect) => <SkillVfxSprite key={effect.kind} effect={effect} />)}</div>}
    <CosmeticRuntimeHero runtimePackage={runtimePackage} equipped={equipped} motion={motion} frame={frame} alt="착용한 치장을 입은 한짝" />
  </div>;
}

/** 치장 조합을 모션 프레임에 맞춰 돌린다. 기본 젓가락과 같은 간격을 쓴다. */
function CosmeticMotionHero({ equipment, catalog, motion, actionEventId, hit, skillVfx }: {
  equipment: Record<string, string | null>;
  catalog: Pick<Catalog, "cosmetics" | "sets">;
  motion: HeroMotion;
  actionEventId?: string;
  hit: boolean;
  skillVfx: SkillVfx[];
}) {
  const [frame, setFrame] = useState(0);
  useEffect(() => {
    /* 지금 모션만 받으면 다음 모션의 첫 프레임에서 또 깨진다. 쓰는 모션을 다 받아 둔다. */
    for (const preloaded of ["rest", "run2", "strike1", "thrust1", "death1"] as const) {
      preloadCosmeticHeroMotion(equipment, catalog, preloaded);
    }
  }, [equipment, catalog, motion]);
  useEffect(() => {
    const startedAt = performance.now();
    setFrame(0);
    let requestId = 0;
    const animate = (now: number) => {
      /* Only re-render on a frame change; sixty state updates a second was
         redrawing every layer for nothing. */
      const next = heroMotionFrame(motion, now - startedAt);
      setFrame(current => current === next ? current : next);
      requestId = requestAnimationFrame(animate);
    };
    requestId = requestAnimationFrame(animate);
    return () => cancelAnimationFrame(requestId);
  }, [motion, actionEventId]);
  const layers = composeCosmeticHeroMotion(equipment, catalog, motion, frame);
  if (!layers) return null;
  const crownVfx = skillVfx.filter((effect) => effect.kind === "haste-caster" || effect.kind === "basic-amp-caster");
  return <div className={`dungeon-current-character ${hit ? "is-hit" : ""}`} data-frame={frame + 1}>
    {crownVfx.length > 0 && <div className="hero-crown-vfx" aria-hidden="true">{crownVfx.map((effect) => <SkillVfxSprite key={effect.kind} effect={effect} />)}</div>}
    <span className="cosmetic-hero-layers" role="img" aria-label="착용한 치장을 입은 한짝">
      {/* Keyed by position, never by url: keying by url tears down and rebuilds
          every layer each frame, which is the blink. */}
      {layers.map((layer, index) => <img key={index} src={layer} alt="" draggable={false} />)}
    </span>
  </div>;
}

/**
 * 전투 중인 한짝. 치장을 걸쳤으면 치장 탭과 같은 레이어 조합으로 그리고, 아무것도
 * 걸치지 않았으면 프레임이 있는 기본 젓가락을 움직인다. 치장 아트는 대기 자세 한 장뿐이라
 * 조합을 쓰는 동안에는 모션이 멈춘다. 예전에는 네 세트만 이름으로 알아보고 나머지는
 * 치장을 입어도 기본 젓가락이 나왔다.
 */
export function PlayerBattleCharacter({ appearance, catalog, motion, actionEventId, hit = false, skillVfx }: {
  appearance?: Pick<Collection, "equipment">;
  catalog?: Pick<Catalog, "cosmetics" | "sets">;
  motion: HeroMotion;
  actionEventId?: string;
  hit?: boolean;
  skillVfx: SkillVfx[];
}) {
  const composed = composeCosmeticHero(appearance?.equipment, catalog);
  if (!composed) return <MasterHero motion={motion} actionEventId={actionEventId} skillVfx={skillVfx} />;
  // 아트가 프레임별 겹침 순서를 준 세트가 먼저다. 낱장 규칙으로는 그 순서를 지킬 수 없다.
  const runtime = runtimeLoadoutFor(cosmeticSlugsBySlot(appearance?.equipment, catalog) ?? {});
  if (runtime) {
    return <CosmeticManifestHero runtimePackage={runtime.runtimePackage} equipped={runtime.equipped} motion={motion} actionEventId={actionEventId} hit={hit} skillVfx={skillVfx} />;
  }
  // 부위별 모션 아트가 갖춰진 세트는 움직인다. 아직 아닌 세트는 아래의 멈춘 조합으로 간다.
  if (appearance?.equipment && catalog && composeCosmeticHeroMotion(appearance.equipment, catalog, motion, 0)) {
    return <CosmeticMotionHero equipment={appearance.equipment} catalog={catalog} motion={motion} actionEventId={actionEventId} hit={hit} skillVfx={skillVfx} />;
  }

  const crownVfx = skillVfx.filter((effect) => effect.kind === "haste-caster" || effect.kind === "basic-amp-caster");
  return <div className={`dungeon-current-character ${hit ? "is-hit" : ""}`} data-equipped-cosmetics={equippedCosmeticIds(appearance).join(",")}>
    {crownVfx.length > 0 && <div className="hero-crown-vfx" aria-hidden="true">{crownVfx.map((effect) => <SkillVfxSprite key={effect.kind} effect={effect} />)}</div>}
    <CosmeticHero equipment={appearance?.equipment} catalog={catalog} alt="착용한 치장을 입은 한짝" />
  </div>;
}
