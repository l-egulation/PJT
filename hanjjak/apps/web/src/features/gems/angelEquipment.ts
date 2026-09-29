import type { GemDungeonCombatEvent } from "./api";

export const ANGEL_RUNTIME_BASE_URL = "/assets/characters/angel/runtime-equipment-v1";

export const equipmentSlots = ["hat", "clothing", "cape", "glove", "shoe", "weapon"] as const;
export type AppearanceSlot = (typeof equipmentSlots)[number];
export type AngelMotion = "rest" | "run2" | "strike1" | "thrust1" | "death1";
export type DungeonMotionSelection = "auto" | AngelMotion;
export type EquippedAppearance = Record<AppearanceSlot, string | null>;

export const defaultAngelLoadout: EquippedAppearance = {
  hat: "angel-halo",
  clothing: "angel-white-robe",
  cape: "angel-wings",
  glove: "angel-wrist-rings",
  shoe: "angel-ankle-rings",
  weapon: "angel-feather-sword",
};

/** Every slot empty, so `resolveFrameLayers` draws only the base character. */
export const basicLoadout: EquippedAppearance = { hat: null, clothing: null, cape: null, glove: null, shoe: null, weapon: null };

export const angelWeaponIds = ["angel-feather-sword", "angel-halo-staff", "angel-light-spear"] as const;

export const motionFrameDurations: Record<AngelMotion, number> = {
  rest: 160,
  run2: 100,
  strike1: 120,
  thrust1: 120,
  death1: 180,
};

export type AtlasFrame = {
  motion: AngelMotion;
  frame: number;
  x: number;
  y: number;
  width: number;
  height: number;
};

export type EquipmentAtlas = {
  technical?: boolean;
  slot?: AppearanceSlot;
  variant?: string;
  sublayer?: "screen-left" | "screen-right";
  image: string;
  motionSheets?: Record<AngelMotion, string>;
};

export type EquipmentItem = {
  id: string;
  setId: string;
  slot: AppearanceSlot;
  atlas?: string;
  atlases?: string[];
};

export type EquipmentManifest = {
  schemaVersion: string;
  packageId: string;
  coordinateSpace: { origin: [number, number]; frameWidth: number; frameHeight: number; autoCrop: boolean };
  atlasGrid: { columns: number; rows: number; width: number; height: number; motionOrder: AngelMotion[]; frameOrder: number[] };
  frames: Record<string, AtlasFrame>;
  selectionContract: { mode: string; allowCrossSetMixing: boolean; slots: AppearanceSlot[] };
  atlases: Record<string, EquipmentAtlas>;
  items: EquipmentItem[];
  drawOrderByFrame: Record<string, string[]>;
  defaultLoadout: EquippedAppearance;
};

export type RegisteredEquipmentPackage = {
  baseUrl: string;
  manifest: EquipmentManifest;
};

export type RegisteredEquipmentItem = EquipmentItem & { packageId: string };
export type EquipmentCatalog = {
  packages: Record<string, RegisteredEquipmentPackage>;
  items: Record<string, RegisteredEquipmentItem>;
};

export type RuntimeLayer = {
  token: string;
  packageId: string;
  atlasId: string;
  imageUrl: string;
  frame: AtlasFrame;
};

export function createEquipmentCatalog(...packages: RegisteredEquipmentPackage[]): EquipmentCatalog {
  const catalog: EquipmentCatalog = { packages: {}, items: {} };
  for (const runtimePackage of packages) {
    const { manifest } = runtimePackage;
    catalog.packages[manifest.packageId] = runtimePackage;
    for (const item of manifest.items) catalog.items[item.id] = { ...item, packageId: manifest.packageId };
  }
  return catalog;
}

function atlasForToken(item: RegisteredEquipmentItem, token: string, runtimePackage: RegisteredEquipmentPackage): string {
  if (item.atlas) return item.atlas;
  const sublayer = token.split(".")[2];
  const atlasId = item.atlases?.find((candidate) => runtimePackage.manifest.atlases[candidate]?.sublayer === sublayer);
  if (!atlasId) throw new Error(`No ${sublayer ?? "default"} atlas for ${item.id}`);
  return atlasId;
}

export function resolveFrameLayers(
  basePackageId: string,
  catalog: EquipmentCatalog,
  motion: AngelMotion,
  frameNumber: number,
  equipped: EquippedAppearance,
): RuntimeLayer[] {
  const basePackage = catalog.packages[basePackageId];
  if (!basePackage) throw new Error(`Unknown base equipment package: ${basePackageId}`);
  const frameKey = `${motion}_${String(frameNumber).padStart(2, "0")}`;
  const drawOrder = basePackage.manifest.drawOrderByFrame[frameKey];
  if (!drawOrder) throw new Error(`Unknown equipment frame: ${frameKey}`);

  return drawOrder.flatMap((token): RuntimeLayer[] => {
    let runtimePackage = basePackage;
    let atlasId: string;

    if (token.startsWith("base.")) {
      atlasId = `base-${token.slice(5)}`;
    } else {
      const slot = token.split(".")[1] as AppearanceSlot;
      const itemId = equipped[slot];
      if (!itemId) return [];
      const item = catalog.items[itemId];
      if (!item || item.slot !== slot) throw new Error(`Invalid ${slot} item: ${itemId}`);
      runtimePackage = catalog.packages[item.packageId];
      atlasId = atlasForToken(item, token, runtimePackage);
    }

    const atlas = runtimePackage.manifest.atlases[atlasId];
    const frame = runtimePackage.manifest.frames[frameKey];
    if (!atlas || !frame) throw new Error(`Incompatible equipment layer: ${runtimePackage.manifest.packageId}/${atlasId}/${frameKey}`);
    const motionSheet = atlas.motionSheets?.[motion];
    return [{
      token,
      packageId: runtimePackage.manifest.packageId,
      atlasId,
      imageUrl: `${runtimePackage.baseUrl}/${motionSheet ?? atlas.image}`,
      frame: motionSheet ? { ...frame, y: 0 } : frame,
    }];
  });
}

export function frameIndexForMotion(motion: AngelMotion, now: number, startedAt: number, reducedMotion = false): number {
  if (reducedMotion) return motion === "death1" ? 3 : 0;
  const elapsed = Math.max(0, now - startedAt);
  const frame = Math.floor(elapsed / motionFrameDurations[motion]);
  return motion === "death1" ? Math.min(3, frame) : frame % 4;
}

export function dungeonMotionForEvent(event: GemDungeonCombatEvent | undefined, playedTicks: number): { motion: AngelMotion; startedAtTick: number } {
  if (!event) return { motion: "rest", startedAtTick: playedTicks };
  if (event.type === "DEFEAT") return { motion: "death1", startedAtTick: event.tick };
  const age = playedTicks - event.tick;
  if (event.type === "PLAYER_HIT") {
    if (age <= 4) return { motion: "strike1", startedAtTick: event.tick };
    if (age <= 8) return { motion: "run2", startedAtTick: event.tick + 5 };
  }
  if (event.type === "SKILL_CAST" && event.skillId === "active_dot" && age <= 4) {
    return { motion: "thrust1", startedAtTick: event.tick };
  }
  return { motion: "rest", startedAtTick: event.tick };
}
