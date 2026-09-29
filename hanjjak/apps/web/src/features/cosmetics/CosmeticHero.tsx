import type { ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { cosmeticsApi, type Catalog, type Collection } from "./api";
import { composeCosmeticHero } from "./cosmetic-art";
import "./CosmeticHero.css";

/**
 * 착용한 치장을 입은 한짝. 아무것도 걸치지 않았거나 아직 목록을 못 받았으면 fallback을
 * 그린다. 이 조합을 치장 탭만 쓰고 나머지 화면이 고정 그림을 쓰던 탓에 같은 캐릭터가
 * 화면마다 다르게 보였다.
 */
export function CosmeticHero({ equipment, catalog, className = "", alt, fallback = null }: {
  equipment?: Record<string, string | null | undefined>;
  catalog?: Pick<Catalog, "cosmetics" | "sets">;
  className?: string;
  alt: string;
  fallback?: ReactNode;
}) {
  const composed = composeCosmeticHero(equipment, catalog);
  if (!composed) return <>{fallback}</>;
  if ("flat" in composed) return <img className={className} src={composed.flat} alt={alt} draggable={false} />;
  return <span className={`cosmetic-hero-layers ${className}`} role="img" aria-label={alt}>
    {composed.layers.map(layer => <img key={layer} src={layer} alt="" draggable={false} />)}
  </span>;
}

/** 랭킹이 내려주는 소문자 부위 이름을 치장 카탈로그의 슬롯 이름으로 맞춘다. */
export function cosmeticEquipmentFromAppearance(appearance?: Record<string, string | null>): Record<string, string | null> | undefined {
  if (!appearance) return undefined;
  return Object.fromEntries(Object.entries(appearance).map(([slot, id]) => [slot.toUpperCase(), id]));
}

/**
 * 로그인한 본인의 치장 착용 상태. 화면마다 같은 두 질의를 쓰므로 캐시를 그대로 공유한다.
 */
export function useOwnCosmeticAppearance(enabled = true) {
  const collection = useQuery({ queryKey: ["cosmetic-collection"], queryFn: cosmeticsApi.collection, enabled, retry: false, staleTime: 30_000 });
  const catalog = useQuery({ queryKey: ["cosmetic-catalog"], queryFn: cosmeticsApi.catalog, enabled, retry: false, staleTime: Number.POSITIVE_INFINITY });
  return { equipment: (collection.data as Collection | undefined)?.equipment, catalog: catalog.data };
}
