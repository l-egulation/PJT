import { useEffect, useRef, useState, type CSSProperties } from "react";
import { layout, prepare, type PreparedText } from "@chenglou/pretext";
import type { CatalogCosmetic, CharacterCollection, CharacterCommand, CosmeticCatalog } from "./api";
import { COSMETIC_ARMS_LAYER, COSMETIC_BASE_LAYER, COSMETIC_BEHIND_BODY_SLOTS, COSMETIC_DEFAULT_WEAPON_LAYER, COSMETIC_LAYER_ORDER, cosmeticArtUrl, cosmeticTopHidesArms, cosmeticTopOverArmsUrl, cosmeticWeaponBehindHands, cosmeticDisplayName, cosmeticLayerUrl, cosmeticSetArtUrl, cosmeticSetName } from "../cosmetics/cosmetic-art";
import characterBackdrop from "./assets-cozy-pixel/character-backdrop-coarse.png";
import characterShadow from "./assets-cozy-pixel/character-floor-shadow.png";
import armorFallback from "../equipment/assets-cozy-pixel/equipment-armor-basic-v2.png";
import bootsFallback from "../equipment/assets-cozy-pixel/equipment-boots-basic-v2.png";
import capeFallback from "../equipment/assets-cozy-pixel/equipment-cape-basic-v2.png";
import glovesFallback from "../equipment/assets-cozy-pixel/equipment-gloves-basic-v2.png";
import helmetFallback from "../equipment/assets-cozy-pixel/equipment-helmet-basic-v2.png";
import weaponFallback from "../equipment/assets-cozy-pixel/equipment-weapon-basic-v2.png";
import collectionBookIcon from "./assets-cozy-pixel/collection-v1/collection-book-icon.png";
import collectionEffectBoxIcon from "./assets-cozy-pixel/collection-v1/collection-effect-box-icon.png";
import collectionPieceIcon from "./assets-cozy-pixel/collection-v1/collection-piece-icon.png";
import collectionSignDecoration from "./assets-cozy-pixel/collection-v1/collection-sign-decoration.png";
import defenseShieldIcon from "./assets-cozy-pixel/collection-v1/defense-shield-icon.png";
import setEffectStarActive from "./assets-cozy-pixel/collection-v1/set-effect-star-active.png";
import setEffectStarLocked from "./assets-cozy-pixel/collection-v1/set-effect-star-locked.png";
import heartIcon from "./assets-cozy-pixel/heart-icon-128.png";
import swordIcon from "./assets-cozy-pixel/sword-icon-128.png";

export const SLOT_LABELS: Record<string, string> = { HEAD: "머리", TOP: "상의", GLOVES: "장갑", SHOES: "신발", CAPE: "망토", BOTTOM: "무기" };
/** 부위를 가리지 않는 보기. 실제 부위 이름과 부딪히지 않는 값을 쓴다. */
const ALL_SLOTS = "ALL";
const SLOT_POSITIONS: Record<string, string> = { HEAD: "left-top", TOP: "left-middle", GLOVES: "left-bottom", SHOES: "right-top", CAPE: "right-middle", BOTTOM: "right-bottom" };
const GRADE_LABELS: Record<string, string> = { NORMAL: "노말", RARE: "희귀", EPIC: "영웅", LEGENDARY: "전설" };
const SLOT_FALLBACKS: Record<string, string> = {
  HEAD: helmetFallback,
  TOP: armorFallback,
  BOTTOM: weaponFallback,
  GLOVES: glovesFallback,
  SHOES: bootsFallback,
  CAPE: capeFallback,
  WEAPON: weaponFallback,
};
// 아무것도 착용하지 않은 기본 모습은 기본 나무검을 든 자세다.
const DEFAULT_HERO = "/cosmetics/hero-default.png";
const STAT_LABELS: Record<string, string> = { attackPercent: "공격력", maxHpPercent: "최대 HP", defensePenetrationPercent: "방어 관통", basicAttackDamagePercent: "기본공격 피해", criticalChancePoint: "치명타 확률", attackSpeedPercent: "공격속도", buffDurationSeconds: "버프 지속시간" };
type CommandInput = Omit<Extract<CharacterCommand, { kind: "register" }>, "key"> | Omit<Extract<CharacterCommand, { kind: "equip" }>, "key">;
type Props = { collection: CharacterCollection; catalog: CosmeticCatalog; pending: boolean; send: (command: CommandInput) => void };

export const cosmeticFallbackName = (id: string) => `치장 #${id.slice(-3)}`;
export const setFallbackName = (id: string) => `세트 #${id.slice(-2)}`;
const cosmeticName = (cosmetic?: CatalogCosmetic) => (cosmetic ? cosmeticDisplayName(cosmetic) : "콘텐츠 없음");
const effectValueText = (effect: { value: number; unit: string }) => (effect.unit === "SECONDS" ? `${effect.value}초` : `${effect.value / 100}%`);
const effectText = (effect: { statId: string; value: number; unit: string }) => `${STAT_LABELS[effect.statId] ?? effect.statId} ${effectValueText(effect)}`;

function dataSlotFor(uiSlot: string, catalog: CosmeticCatalog) {
  if (uiSlot !== "BOTTOM") return uiSlot;
  const hasBottom = catalog.cosmetics.some(item => item.slot === "BOTTOM");
  const hasPreviewWeapon = catalog.cosmetics.some(item => item.slot === "WEAPON");
  return !hasBottom && hasPreviewWeapon ? "WEAPON" : "BOTTOM";
}

function usePretextCostumeLayout(contentKey: string) {
  const root = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!document.fonts || typeof ResizeObserver === "undefined") return;
    let disposed = false;
    let observer: ResizeObserver | null = null;
    void document.fonts.ready.then(() => {
      if (disposed || !root.current) return;
      const prepared = new Map<HTMLElement, PreparedText>();
      root.current.querySelectorAll<HTMLElement>("[data-pretext]").forEach(element => {
        prepared.set(element, prepare(element.textContent ?? "", getComputedStyle(element).font, { wordBreak: "keep-all" }));
      });
      const relayout = () => prepared.forEach((text, element) => {
        const lineHeight = Number.parseFloat(getComputedStyle(element).lineHeight) || 18;
        element.style.minHeight = `${Math.ceil(layout(text, Math.max(element.clientWidth, 1), lineHeight).height)}px`;
      });
      observer = new ResizeObserver(relayout);
      observer.observe(root.current);
      relayout();
    });
    return () => { disposed = true; observer?.disconnect(); };
  }, [contentKey]);
  return root;
}

function CosmeticImage({ cosmetic, slot, compact = false }: { cosmetic?: CatalogCosmetic; slot?: string; compact?: boolean }) {
  const resolvedSlot = cosmetic?.slot ?? slot ?? "HEAD";
  const fallback = SLOT_FALLBACKS[resolvedSlot] ?? helmetFallback;
  const artUrl = cosmeticArtUrl(cosmetic);
  return <span className={`character-cosmetic-art${compact ? " is-compact" : ""}${artUrl ? " has-content-art" : " is-placeholder-art"}`}>
    <img className="character-cosmetic-image" src={artUrl ?? fallback} alt={cosmetic ? cosmeticName(cosmetic) : `${SLOT_LABELS[resolvedSlot] ?? "치장"} 이미지 준비 중`} />
  </span>;
}

export function CostumePanel({ collection, catalog, pending, send, sendBatch }: Props & { onGacha: () => void; sendBatch: (commands: CommandInput[]) => void }) {
  const [slot, setSlot] = useState("HEAD");
  const [draft, setDraft] = useState<Record<string, string | null>>({});
  const cosmetics = new Map(catalog.cosmetics.map(item => [item.cosmeticId, item]));
  /* 부위를 하나씩 오가지 않고 가진 것을 한눈에 보고 싶을 때가 있다. 그때는
     부위를 가리지 않고 모두 내놓고, 입고 벗기는 그 치장이 속한 부위로 한다. */
  const showingAll = slot === ALL_SLOTS;
  const activeDataSlot = dataSlotFor(showingAll ? "HEAD" : slot, catalog);
  const owned = collection.states.filter(item => item.registeredQuantity > 0 && (showingAll || item.slot === activeDataSlot));
  const selected = Object.hasOwn(draft, activeDataSlot) ? draft[activeDataSlot] : collection.equipment[activeDataSlot];
  const selectedState = collection.states.find(item => item.cosmeticId === selected);
  const selectedCosmetic = cosmetics.get(selected ?? "");
  // 화면에 보이는 부위 차례를 따른다. 겹쳐 그리는 차례(COSMETIC_LAYER_ORDER)는 무기가
  // 장갑보다 앞이어야 해서 다르고, 명령 순서가 거기에 끌려다닐 이유는 없다.
  const equipmentSlots = [...new Set(Object.keys(SLOT_LABELS).map(uiSlot => dataSlotFor(uiSlot, catalog)))];
  const resetCommands: CommandInput[] = equipmentSlots
    .filter(dataSlot => Boolean(collection.equipment[dataSlot]))
    .map(dataSlot => ({ kind: "equip", slot: dataSlot, cosmeticId: null }));
  // 착용(또는 미리보기) 중인 부위를 레이어로 겹쳐 조합 모습을 만든다.
  const wornEntries = COSMETIC_LAYER_ORDER.map(uiSlot => {
    const dataSlot = dataSlotFor(uiSlot, catalog);
    const id = Object.hasOwn(draft, dataSlot) ? draft[dataSlot] : collection.equipment[dataSlot];
    const item = id ? cosmetics.get(id) : undefined;
    return item ? { uiSlot, item } : undefined;
  }).filter((entry): entry is { uiSlot: (typeof COSMETIC_LAYER_ORDER)[number]; item: CatalogCosmetic } => Boolean(entry));
  const wornCosmetics = wornEntries.map(entry => entry.item);
  const wornLayers = wornCosmetics.map(item => cosmeticLayerUrl(item));
  // 완성 이미지는 실제로 그 모습일 때만 쓴다.
  // 여섯 부위를 한 세트로 채웠는데 그 세트에 레이어가 없을 때만 전신 이미지를 쓴다.
  // 예전에는 레이어가 하나라도 없으면 고른 부위의 세트 전신 이미지로 물러섰다.
  // 그래서 스시야 모자 하나만 걸쳐도 스시야 한 벌을 다 입은 것처럼 보였다.
  const wornSetIds = new Set(wornCosmetics.map(item => item.setId));
  const fullSet = wornCosmetics.length === COSMETIC_LAYER_ORDER.length && wornSetIds.size === 1
    ? catalog.sets.find(item => item.setId === wornCosmetics[0].setId)
    : undefined;
  const flatHero = fullSet && wornLayers.some(layer => !layer) ? cosmeticSetArtUrl(fullSet) : null;
  const previewHero = flatHero ?? DEFAULT_HERO;
  // 레이어가 있는 부위만 겹친다. 레이어가 없는 부위는 원본에 그림이 없어 빠진다.
  const layered = wornEntries
    .map(entry => ({ ...entry, layer: cosmeticLayerUrl(entry.item) }))
    .filter((entry): entry is typeof entry & { layer: string } => Boolean(entry.layer));
  // 민소매 상의는 맨몸의 팔을 덮으므로 그 바로 뒤에 공통 팔 레이어를 다시 올린다.
  // 상의가 몸통과 소매로 나뉜 세트는 소매를 그 팔 위에 한 번 더 얹는다.
  const behindBody = (entry: (typeof layered)[number]) =>
    COSMETIC_BEHIND_BODY_SLOTS.has(entry.uiSlot) || cosmeticWeaponBehindHands(entry.item);
  const front = layered
    .filter(entry => !behindBody(entry))
    .flatMap(entry => (cosmeticTopHidesArms(entry.item)
      ? [entry.layer, COSMETIC_ARMS_LAYER, cosmeticTopOverArmsUrl(entry.item)].filter((layer): layer is string => Boolean(layer))
      : [entry.layer]));
  // 무기 치장을 끼지 않았으면 기본 나무검을 쥐여 준다. 아무것도 없으면 빈손이 된다.
  const holdsWeapon = wornEntries.some(entry => entry.uiSlot === "BOTTOM");
  const composedLayers = !flatHero && layered.length > 0
    ? [
      ...layered.filter(behindBody).map(entry => entry.layer),
      COSMETIC_BASE_LAYER,
      ...front,
      ...(holdsWeapon ? [] : [COSMETIC_DEFAULT_WEAPON_LAYER]),
    ]
    : null;
  const layoutRoot = usePretextCostumeLayout(`${slot}:${owned.map(item => item.cosmeticId).join(",")}`);
  return <div ref={layoutRoot} className="character-two-columns costume-layout">
    <section className="costume-preview-pane" aria-label="치장 미리보기">
      <div className="costume-character-stage" aria-label="치장 부위 선택">
        <img className="costume-character-stage__backdrop" src={characterBackdrop} alt="" />
        <img className="costume-character-stage__shadow" src={characterShadow} alt="" />
        {composedLayers
          ? <span className="costume-character-stage__hero costume-hero-layers" role="img" aria-label="선택한 치장을 미리 보는 한짝 캐릭터">
            {composedLayers.map(layer => <img key={layer} src={layer} alt="" />)}
          </span>
          : <img className="costume-character-stage__hero" src={previewHero} alt="선택한 치장을 미리 보는 한짝 캐릭터" />}
        <button
          type="button"
          className="costume-preview-reset"
          disabled={pending || resetCommands.length === 0}
          onClick={() => {
            setDraft(Object.fromEntries(equipmentSlots.map(dataSlot => [dataSlot, null])));
            sendBatch(resetCommands);
          }}
        >초기화</button>
        {Object.entries(SLOT_LABELS).map(([id, label]) => {
          const dataSlot = dataSlotFor(id, catalog);
          const current = collection.equipment[dataSlot];
          const preview = Object.hasOwn(draft, dataSlot) ? draft[dataSlot] : current;
          const previewCosmetic = cosmetics.get(preview ?? "");
          const previewStar = collection.states.find(item => item.cosmeticId === preview)?.cosmeticStar ?? 0;
          const changed = preview !== current;
          const itemName = previewCosmetic ? cosmeticName(previewCosmetic) : "비어 있음";
          return <button
            type="button"
            className={`costume-slot-card costume-slot-${SLOT_POSITIONS[id] ?? "left-middle"}`}
            key={id}
            aria-label={`${label} ${itemName}${changed ? " 미리보기" : preview ? " 착용 중" : " 선택"}`}
            aria-pressed={slot === id}
            title={itemName}
            onClick={() => setSlot(id)}
          >
            {slot === id && <span className="costume-slot-selection" aria-hidden="true"><i /></span>}
            <span className={`cosmetic-star-dot${previewStar > 0 ? " is-earned" : ""}`} aria-label={`${previewStar}성`}>{previewStar}</span>
            <strong>{label}</strong>
            <CosmeticImage cosmetic={previewCosmetic} slot={id} compact />
            <span className="costume-slot-state">{changed ? "미리보기" : preview ? "착용 중" : "비어 있음"}</span>
          </button>;
        })}
      </div>
    </section>
    <section className="costume-closet-pane" aria-label="보유 치장">
      <div className="character-slot-filter" role="tablist" aria-label="치장 부위">
        <button type="button" role="tab" aria-selected={showingAll} onClick={() => setSlot(ALL_SLOTS)}>전체</button>
        {Object.entries(SLOT_LABELS).map(([id, label]) => <button type="button" role="tab" key={id} aria-selected={slot === id} onClick={() => setSlot(id)}>{label}</button>)}
      </div>
      {/* 고르는 순간 바로 입는다. 이미 입고 있는 것을 다시 누르면 벗는다.
          예전에는 아래쪽 막대의 착용 단추를 한 번 더 눌러야 끝났다. */}
      <ul className="character-owned costume-owned-grid">{owned.map(item => {
        const cosmetic = cosmetics.get(item.cosmeticId);
        const itemSlot = showingAll ? item.slot : activeDataSlot;
        const worn = collection.equipment[itemSlot] === item.cosmeticId;
        return <li key={item.cosmeticId}><button
          type="button"
          data-cosmetic-id={item.cosmeticId}
          aria-pressed={worn}
          aria-label={`${cosmeticName(cosmetic)} ${worn ? "벗기" : "입기"}`}
          disabled={pending}
          onClick={() => {
            const next = worn ? null : item.cosmeticId;
            setDraft(current => ({ ...current, [itemSlot]: next }));
            send({ kind: "equip", slot: itemSlot, cosmeticId: next });
          }}
        >
          <CosmeticImage cosmetic={cosmetic} slot={itemSlot} />
          <span className={`costume-grade grade-${item.grade.toLowerCase()}`}>{GRADE_LABELS[item.grade] ?? item.grade}</span>
          <span className={`cosmetic-star-dot${item.cosmeticStar > 0 ? " is-earned" : ""}`} aria-label={`${item.cosmeticStar}성`}>{item.cosmeticStar}</span>
          {/* 승급에 쓴 개수는 등록분으로 넘어가므로, 아직 쓰지 않은 중복만 센다. */}
          <span className="costume-card-count" aria-label={`남은 중복 ${item.availableUnregisteredQuantity}개`}>×{item.availableUnregisteredQuantity}</span>
          <strong className="costume-card-name" data-pretext>{cosmeticName(cosmetic)}</strong>
          <span className="costume-card-meta"><b>{item.cosmeticStar}성</b><small>등록 {item.registeredQuantity}</small></span>
          {worn && <i className="costume-selected-mark" aria-hidden="true" />}
        </button></li>;
      })}
      </ul>
      {owned.length === 0 && <div className="costume-empty-state"><CosmeticImage slot={activeDataSlot} /><strong>아직 보유한 {showingAll ? "" : `${SLOT_LABELS[slot]} `}치장이 없어요</strong><span>치장 뽑기에서 새로운 외형을 만나 보세요.</span></div>}
    </section>
  </div>;
}

const SET_STAR_STEPS = [1, 2, 3, 4, 5] as const;
// 다음 성급까지 모은 개수. 등록분에 아직 등록하지 않은 중복을 더하고 기준치에서 멈춘다.
// 이 값이 기준치와 같아질 때 서버가 성급 올리기를 허용한다.
type MemberState = { registeredQuantity: number; availableUnregisteredQuantity: number; cosmeticStar: number; neededForNextStar: number | null };

// 성급 올리기 버튼에 적을 말. 가진 여분과 다음 성급에 드는 개수를 나란히 적는다.
// 서버가 주는 neededForNextStar는 다음 기준까지 더 등록해야 하는 개수다.
function upgradeAction(state?: MemberState): { label: string; hint: string; ready: boolean; counts?: [number, number] } {
  if (!state || state.registeredQuantity === 0) return { label: "미획득", hint: "아직 뽑지 않은 부위입니다.", ready: false };
  const needed = state.neededForNextStar;
  if (needed == null) return { label: "최대 성급", hint: "더 올릴 성급이 없습니다.", ready: false };
  const have = state.availableUnregisteredQuantity;
  const nextStar = state.cosmeticStar + 1;
  return {
    label: `${have} / ${needed}`,
    counts: [have, needed],
    hint: have >= needed
      ? `여분 ${needed}개를 넣어 ${nextStar}성으로 올립니다.`
      : `${nextStar}성까지 ${needed}개가 필요한데 여분이 ${have}개입니다.`,
    ready: have >= needed,
  };
}
// 중앙 제목은 "<세트 이름> 세트"로 읽는다. 콘텐츠 표시명이 이미 "세트"로 끝나면 덧붙이지 않는다.
const setTitle = (name: string) => (name.endsWith("세트") ? name : `${name} 세트`);
type GalleryFilter = "all" | "complete" | "missing";

function galleryEffectIcon(statId: string) {
  if (statId.toLowerCase().includes("hp")) return heartIcon;
  if (statId.toLowerCase().includes("defense")) return defenseShieldIcon;
  return swordIcon;
}

export function GalleryPanel({ collection, catalog, pending, send }: Props) {
  const [selectedId, setSelectedId] = useState(catalog.sets[0]?.setId ?? "");
  const [filter, setFilter] = useState<GalleryFilter>("all");
  const setProgress = catalog.sets.map(set => {
    const memberIds = Object.values(set.members).filter(Boolean);
    const registeredCount = memberIds.filter(id => collection.states.some(state => state.cosmeticId === id && state.registeredQuantity > 0)).length;
    return { set, memberCount: memberIds.length, registeredCount, complete: memberIds.length > 0 && registeredCount === memberIds.length };
  });
  const visibleSets = setProgress.filter(item => filter === "all" || (filter === "complete" ? item.complete : !item.complete));
  const selectedProgress = visibleSets.find(item => item.set.setId === selectedId) ?? visibleSets[0] ?? setProgress[0];
  if (!selectedProgress) return <p>등록된 세트가 없습니다.</p>;
  const selected = selectedProgress.set;
  const setStar = collection.setStars[selected.setId] ?? 0;
  const selectedMembers = Object.entries(SLOT_LABELS).map(([slot, label]) => {
    const dataSlot = slot === "BOTTOM" && !selected.members.BOTTOM && selected.members.WEAPON ? "WEAPON" : slot;
    const id = selected.members[dataSlot];
    const cosmetic = catalog.cosmetics.find(item => item.cosmeticId === id);
    const state = collection.states.find(item => item.cosmeticId === id);
    return { slot, label, dataSlot, id, cosmetic, state };
  });
  const currentSetEffects = collection.setEffects[selected.setId] ?? [];
  const nextEffect = Object.entries(selected.effects)
    .map(([star, effects]) => ({ star: Number(star), effects }))
    .filter(item => item.star > setStar)
    .sort((left, right) => left.star - right.star)[0];
  const completedSets = setProgress.filter(item => item.complete).length;
  const nextByStat = new Map((nextEffect?.effects ?? []).map(effect => [effect.statId, effect]));
  const mergedEffects = [
    ...currentSetEffects.map(effect => ({ statId: effect.statId, current: effect, next: nextByStat.get(effect.statId) })),
    ...(nextEffect?.effects ?? []).filter(effect => !currentSetEffects.some(item => item.statId === effect.statId)).map(effect => ({ statId: effect.statId, current: undefined, next: effect })),
  ];

  return <div className="character-two-columns character-gallery">
    <section className="gallery-set-panel" aria-label="세트 목록">
      <header className="gallery-panel-title">
        <img src={collectionBookIcon} alt="" />
        <h3>세트 목록</h3>
        <span><small>전체</small><b>{collection.uniqueRegisteredCount}</b> / {catalog.cosmetics.length}</span>
      </header>
      <div className="gallery-set-filters" role="tablist" aria-label="도감 세트 필터">
        {([["전체", "all"], ["완성", "complete"], ["미보유", "missing"]] as const).map(([label, id]) => <button
          type="button"
          role="tab"
          key={id}
          aria-selected={filter === id}
          onClick={() => setFilter(id)}
        >{label}</button>)}
      </div>
      <div className="gallery-set-list">
        {visibleSets.map(({ set, memberCount, registeredCount }) => {
          const itemStar = collection.setStars[set.setId] ?? 0;
          return <button type="button" className={`grade-${set.grade.toLowerCase()}`} key={set.setId} aria-pressed={selected.setId === set.setId} onClick={() => setSelectedId(set.setId)}>
            <span className="gallery-set-thumb"><img src={cosmeticSetArtUrl(set) ?? collectionBookIcon} alt="" /></span>
            <span className="gallery-set-copy">
              <strong>{cosmeticSetName(set) ?? setFallbackName(set.setId)}<i className={`gallery-set-grade grade-${set.grade.toLowerCase()}`}>{GRADE_LABELS[set.grade] ?? set.grade}</i></strong>
              <span className="gallery-set-meta"><span><img src={collectionPieceIcon} alt="" />{registeredCount} / {memberCount}</span><span><img src={itemStar > 0 ? setEffectStarActive : setEffectStarLocked} alt="" />{itemStar}성</span></span>
            </span>
            <i aria-hidden="true">›</i>
          </button>;
        })}
        {visibleSets.length === 0 && <p className="gallery-empty-list">해당하는 세트가 없습니다.</p>}
      </div>
    </section>

    <section className="gallery-detail-panel" aria-label="선택 세트 상세">
      <header className="gallery-set-title">
        <h3>{setTitle(cosmeticSetName(selected) ?? setFallbackName(selected.setId))}</h3>
      </header>
      <ul className="gallery-member-grid">{selectedMembers.map(({ slot, label, dataSlot, id, cosmetic, state }) => {
        const star = state?.cosmeticStar ?? 0;
        const action = upgradeAction(state);
        return <li key={slot} className={(state?.registeredQuantity ?? 0) > 0 ? "" : "is-missing"} data-cosmetic-id={id}>
          <strong>{label}</strong>
          <span className={`cosmetic-star-dot${star > 0 ? " is-earned" : ""}`} aria-hidden="true">{star}</span>
          <span className="gallery-member-art"><CosmeticImage cosmetic={cosmetic} slot={dataSlot} /></span>
          <button
            type="button"
            className={`gallery-member-upgrade${action.ready ? " is-ready" : ""}`}
            disabled={pending || !id || !state?.canUpgrade}
            title={action.hint}
            aria-label={`${cosmeticName(cosmetic)} ${action.hint}`}
            onClick={() => id && send({ kind: "register", cosmeticId: id })}
          >{action.counts && <img src={collectionPieceIcon} alt="" />}{action.label}</button>
        </li>;
      })}</ul>
      <section className="gallery-set-effects" aria-label="선택 세트 성급">
        <div className="gallery-star-track" style={{ "--gallery-progress": Math.max(0, Math.min(setStar - 1, 4)) } as CSSProperties}>{SET_STAR_STEPS.map(star => <span key={star} className={star <= setStar ? "is-active" : ""}>
          <img src={star <= setStar ? setEffectStarActive : setEffectStarLocked} alt="" />
          <b>{star}성</b>
        </span>)}</div>
      </section>
    </section>

    <section className="gallery-total-panel" aria-label="세트 효과">
      <header className="gallery-panel-title">
        <img src={collectionEffectBoxIcon} alt="" />
        <h3>세트 효과</h3>
      </header>
      {mergedEffects.length > 0
        ? <ul className="gallery-total-effects">
          {mergedEffects.map(({ statId, current, next }) => <li key={statId} className={current ? "" : "is-locked"}>
            <img src={galleryEffectIcon(statId)} alt="" />
            <strong>{STAT_LABELS[statId] ?? statId}</strong>
            <span>{current ? effectValueText(current) : "—"}</span>
            {current && next && <i aria-hidden="true">›</i>}
            {next && <b aria-label={`${nextEffect?.star}성 달성 시 ${effectValueText(next)}`}><img src={setEffectStarLocked} alt="" />{effectValueText(next)}</b>}
          </li>)}
        </ul>
        : <p className="gallery-no-effects">세트를 완성하면 1성 효과가 켜집니다.</p>}
      <div className="gallery-complete-count"><span>완성 세트</span><strong>{completedSets} / {catalog.sets.length}</strong></div>
      <img className="gallery-sign-decoration" src={collectionSignDecoration} alt="도감 완성 장식" />
    </section>
  </div>;
}
