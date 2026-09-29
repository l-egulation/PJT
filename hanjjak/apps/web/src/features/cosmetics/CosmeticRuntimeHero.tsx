import { useEffect, useMemo, useRef, useState } from "react";
import {
  createEquipmentCatalog, resolveFrameLayers,
  type EquipmentManifest, type EquippedAppearance,
} from "../gems/angelEquipment";
import type { HeroMotion } from "../battle/battleVisuals";
import type { CosmeticRuntimePackage } from "./cosmeticRuntimePackages";

const manifests = new Map<string, Promise<EquipmentManifest>>();

/** 매니페스트는 세트마다 한 번만 받아 둔다. 프레임이 바뀔 때마다 다시 받을 이유가 없다. */
function loadManifest(url: string): Promise<EquipmentManifest> {
  let pending = manifests.get(url);
  if (!pending) {
    pending = fetch(url).then(response => {
      if (!response.ok) throw new Error(`치장 매니페스트 로드 실패 (${response.status})`);
      return response.json() as Promise<EquipmentManifest>;
    }).then(manifest => {
      const space = manifest.coordinateSpace;
      if (space.frameWidth !== 928 || space.frameHeight !== 672 || space.autoCrop) {
        throw new Error("치장 좌표계가 928x672 고정 캔버스 계약과 다릅니다.");
      }
      return manifest;
    });
    manifests.set(url, pending);
  }
  return pending;
}

const images = new Map<string, Promise<HTMLImageElement>>();
function loadImage(url: string): Promise<HTMLImageElement> {
  let pending = images.get(url);
  if (!pending) {
    pending = new Promise((resolve, reject) => {
      const image = new Image();
      image.onload = () => resolve(image);
      image.onerror = () => reject(new Error(`치장 이미지 로드 실패: ${url}`));
      image.src = url;
    });
    images.set(url, pending);
  }
  return pending;
}

/**
 * 프레임마다 겹침 순서가 다른 세트를 그린다. 아트가 정해 둔 `drawOrderByFrame`을 그대로
 * 따라가므로, 천사의 공격 프레임에서 날개가 몸 앞으로 오는 것 같은 규칙이 유지된다.
 */
export function CosmeticRuntimeHero({ runtimePackage, equipped, motion, frame, className, alt }: {
  runtimePackage: CosmeticRuntimePackage;
  equipped: EquippedAppearance;
  motion: HeroMotion;
  frame: number;
  className?: string;
  alt: string;
}) {
  const canvas = useRef<HTMLCanvasElement>(null);
  const [manifest, setManifest] = useState<EquipmentManifest>();
  useEffect(() => {
    let cancelled = false;
    loadManifest(runtimePackage.manifestUrl).then(next => { if (!cancelled) setManifest(next); }).catch(() => undefined);
    return () => { cancelled = true; };
  }, [runtimePackage.manifestUrl]);
  const catalog = useMemo(
    () => (manifest ? createEquipmentCatalog({ baseUrl: runtimePackage.baseUrl, manifest }) : undefined),
    [manifest, runtimePackage.baseUrl],
  );
  useEffect(() => {
    const element = canvas.current;
    if (!element || !manifest || !catalog) return;
    let cancelled = false;
    try {
      const layers = resolveFrameLayers(manifest.packageId, catalog, motion, frame + 1, equipped);
      void Promise.all([...new Set(layers.map(layer => layer.imageUrl))].map(async url => [url, await loadImage(url)] as const))
        .then(loaded => {
          if (cancelled) return;
          const context = element.getContext("2d");
          if (!context) return;
          const byUrl = Object.fromEntries(loaded);
          context.imageSmoothingEnabled = false;
          context.clearRect(0, 0, 928, 672);
          for (const layer of layers) {
            const { x, y, width, height } = layer.frame;
            context.drawImage(byUrl[layer.imageUrl], x, y, width, height, 0, 0, 928, 672);
          }
        }).catch(() => undefined);
    } catch {
      // 프레임이 없는 세트는 조용히 비워 둔다. 부르는 쪽이 이미 모션 지원 여부를 가린다.
    }
    return () => { cancelled = true; };
  }, [manifest, catalog, motion, frame, equipped]);
  // 클래스를 항상 하나 달아 둔다. 캔버스는 928x672 고유 크기를 그대로 쓰는 탓에,
  // 크기를 정해 주는 규칙이 없는 자리에 놓이면 상자를 뚫고 나온다.
  return <canvas ref={canvas} className={`cosmetic-hero-canvas ${className ?? ""}`.trim()} width={928} height={672} role="img" aria-label={alt} />;
}
