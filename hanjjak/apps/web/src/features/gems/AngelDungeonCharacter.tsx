import { useEffect, useMemo, useRef, useState } from "react";
import {
  ANGEL_RUNTIME_BASE_URL,
  createEquipmentCatalog,
  frameIndexForMotion,
  resolveFrameLayers,
  type AngelMotion,
  type EquipmentManifest,
  type EquippedAppearance,
} from "./angelEquipment";
import { SkillVfxSprite, type SkillVfx } from "./skillVfx";

const manifestUrl = `${ANGEL_RUNTIME_BASE_URL}/angel-equipment-manifest.json`;
let manifestPromise: Promise<EquipmentManifest> | undefined;
const imagePromises = new Map<string, Promise<HTMLImageElement>>();

function loadManifest(): Promise<EquipmentManifest> {
  manifestPromise ??= fetch(manifestUrl).then((response) => {
    if (!response.ok) throw new Error(`천사 장비 매니페스트 로드 실패 (${response.status})`);
    return response.json() as Promise<EquipmentManifest>;
  }).then((manifest) => {
    if (manifest.coordinateSpace.frameWidth !== 928 || manifest.coordinateSpace.frameHeight !== 672 || manifest.coordinateSpace.autoCrop) {
      throw new Error("천사 장비 좌표계가 928×672 고정 캔버스 계약과 다릅니다.");
    }
    return manifest;
  });
  return manifestPromise;
}

function loadImage(url: string): Promise<HTMLImageElement> {
  const cached = imagePromises.get(url);
  if (cached) return cached;
  const loading = new Promise<HTMLImageElement>((resolve, reject) => {
    const image = new Image();
    image.decoding = "async";
    image.onload = () => resolve(image);
    image.onerror = () => reject(new Error(`천사 장비 이미지 로드 실패: ${url}`));
    image.src = url;
  });
  imagePromises.set(url, loading);
  return loading;
}

export function AngelDungeonCharacter({
  motion,
  motionStartedAt,
  equipped,
  frameOverride,
  hit = false,
  skillVfx = [],
}: {
  motion: AngelMotion;
  motionStartedAt: number;
  equipped: EquippedAppearance;
  frameOverride?: number;
  hit?: boolean;
  skillVfx?: SkillVfx[];
}) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [manifest, setManifest] = useState<EquipmentManifest>();
  const [ready, setReady] = useState(false);
  const [error, setError] = useState<string>();
  const reducedMotion = typeof window !== "undefined" && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  const [frameIndex, setFrameIndex] = useState(() => frameIndexForMotion(motion, Date.now(), motionStartedAt, reducedMotion));
  const visibleFrameIndex = frameOverride === undefined ? frameIndex : Math.max(0, Math.min(3, frameOverride - 1));
  const catalog = useMemo(() => manifest ? createEquipmentCatalog({ baseUrl: ANGEL_RUNTIME_BASE_URL, manifest }) : undefined, [manifest]);

  useEffect(() => {
    const updateFrame = () => setFrameIndex(frameIndexForMotion(motion, Date.now(), motionStartedAt, reducedMotion));
    updateFrame();
    if (reducedMotion) return;
    let requestId = 0;
    const animate = () => {
      updateFrame();
      requestId = window.requestAnimationFrame(animate);
    };
    requestId = window.requestAnimationFrame(animate);
    return () => window.cancelAnimationFrame(requestId);
  }, [motion, motionStartedAt, reducedMotion]);

  useEffect(() => {
    let cancelled = false;
    loadManifest().then((nextManifest) => {
      if (!cancelled) setManifest(nextManifest);
    }).catch((reason: unknown) => {
      if (!cancelled) setError(reason instanceof Error ? reason.message : "천사 장비를 불러오지 못했습니다.");
    });
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas || !manifest || !catalog) return;
    let cancelled = false;
    try {
      const layers = resolveFrameLayers(manifest.packageId, catalog, motion, visibleFrameIndex + 1, equipped);
      Promise.all([...new Set(layers.map((layer) => layer.imageUrl))].map(async (url) => [url, await loadImage(url)] as const)).then((loaded) => {
        if (cancelled) return;
        const context = canvas.getContext("2d");
        if (!context) return;
        const images = Object.fromEntries(loaded);
        context.imageSmoothingEnabled = false;
        context.clearRect(0, 0, 928, 672);
        for (const layer of layers) {
          const { x, y, width, height } = layer.frame;
          context.drawImage(images[layer.imageUrl], x, y, width, height, 0, 0, 928, 672);
        }
        setReady(true);
        setError(undefined);
      }).catch((reason: unknown) => {
        if (!cancelled) setError(reason instanceof Error ? reason.message : "천사 장비 합성에 실패했습니다.");
      });
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "천사 장비 합성에 실패했습니다.");
    }
    return () => { cancelled = true; };
  }, [catalog, equipped, manifest, motion, visibleFrameIndex]);

  const crownVfx = skillVfx.filter((effect) => effect.kind === "haste-caster" || effect.kind === "basic-amp-caster");
  return <div className={`angel-character-stage ${hit ? "is-hit" : ""}`} data-motion={motion} data-frame={visibleFrameIndex + 1}>
    {crownVfx.length > 0 && <div className="hero-crown-vfx" aria-hidden="true">
      {crownVfx.map((effect) => <SkillVfxSprite key={effect.kind} effect={effect} />)}
    </div>}
    <canvas ref={canvasRef} width={928} height={672} aria-label={`천사 장비 캐릭터 ${motion} ${visibleFrameIndex + 1}/4 프레임`} />
    {!ready && !error && <small className="angel-load-status">천사 장비 불러오는 중…</small>}
    {error && <small className="angel-load-status error" role="alert">{error}</small>}
  </div>;
}
