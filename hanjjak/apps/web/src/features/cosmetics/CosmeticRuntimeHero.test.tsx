import { existsSync, readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { CosmeticRuntimeHero } from "./CosmeticRuntimeHero";
import { COSMETIC_RUNTIME_PACKAGES } from "./cosmeticRuntimePackages";

const publicFile = (url: string) => fileURLToPath(new URL(`../../../public${url}`, import.meta.url));

describe("매니페스트 캐릭터의 크기", () => {
  it("크기를 정해 주는 클래스를 항상 달고 나온다", () => {
    // 캔버스는 928x672 고유 크기로 그려진다. 이 클래스가 빠지면 상자를 뚫고 나온다.
    const html = renderToStaticMarkup(<CosmeticRuntimeHero
      runtimePackage={COSMETIC_RUNTIME_PACKAGES.angel}
      equipped={COSMETIC_RUNTIME_PACKAGES.angel.items}
      motion="rest" frame={0} alt="천사"
    />);
    expect(html).toContain("cosmetic-hero-canvas");
  });

  it("낱장 레이어와 같은 규칙으로 칸을 채운다", () => {
    const css = readFileSync(new URL("./CosmeticHero.css", import.meta.url), "utf8");
    const rule = css.slice(css.indexOf(".cosmetic-hero-canvas"));
    expect(rule).toContain(".cosmetic-hero-layers > img");
    expect(rule).toContain("object-position: 50% 100%");
  });
});

describe("매니페스트가 약속한 그림", () => {
  for (const [slug, runtimePackage] of Object.entries(COSMETIC_RUNTIME_PACKAGES)) {
    it(`${slug} 세트는 적어 둔 모션 시트를 모두 내보낸다`, () => {
      // 시트가 없으면 이미지 요청이 개발 서버의 index.html로 떨어져 캔버스가 빈 채로 남는다.
      const manifest = JSON.parse(readFileSync(publicFile(runtimePackage.manifestUrl), "utf8")) as {
        atlases: Record<string, { image: string; motionSheets?: Record<string, string> }>;
      };
      const missing = Object.values(manifest.atlases)
        .flatMap(atlas => [atlas.image, ...Object.values(atlas.motionSheets ?? {})])
        .filter(relative => !existsSync(publicFile(`${runtimePackage.baseUrl}/${relative}`)));
      expect(missing).toEqual([]);
    });
  }
});
