import type { Banner, Catalog } from "./api";
import riceIcon from "../equipment/assets-cozy-pixel/currency-rice-gold-v2.png";
import ticketIcon from "./assets-cozy-pixel/cosmetic-draw-ticket-v1.png";
import ticketHero from "./assets-cozy-pixel/cosmetic-ticket-hero-v1.png";
import { CosmeticMilestoneProgress } from "./CosmeticMilestoneProgress";

type GachaCommand = { kind: "claim"; bannerId: string; count: number };

const formatRice = (value: number) => value.toLocaleString();
const setName = (banner: Banner, catalog: Catalog) => catalog.sets.find((set) => set.setId === banner.setId)?.displayName ?? banner.displayName ?? `치장 배너 #${banner.bannerId.slice(-2)}`;

/** 제목 줄에 들어가는 보유 재화. 자릿수가 늘어도 제목을 밀지 않도록 헤더가 자리를 잡는다. */
export function CosmeticWalletBalance({ banner }: { banner: Banner }) {
  return <div className="cosmetic-gacha-balance" aria-label="보유 재화">
    <span aria-label={`뽑기권 ${banner.ticketBalance}개`}><img src={ticketIcon} alt="" /><strong>{formatRice(banner.ticketBalance)}</strong></span>
    <span aria-label={`쌀 ${formatRice(banner.riceBalance)}`}><img src={riceIcon} alt="" /><strong>{formatRice(banner.riceBalance)}</strong></span>
  </div>;
}

type Props = {
  banners: Banner[];
  catalog: Catalog;
  pending?: boolean;
  claimedBannerId?: string | null;
  onDraw: (banner: Banner, count: 1 | 10) => void;
  onOpenSelector?: (banner: Banner) => void;
  onCommand?: (command: GachaCommand) => void;
};

export function CosmeticGachaBoard({ banners, catalog, pending = false, claimedBannerId = null, onDraw, onOpenSelector, onCommand }: Props) {
  return <section className="cosmetic-banner-section" aria-label="치장 뽑기 배너">
    <div className="cosmetic-banner-grid">
      {banners.map((banner) => {
        return <article className="cosmetic-banner-card" key={banner.bannerId}>
          <h3 className="sr-only">{setName(banner, catalog)}</h3>
          <div className="cosmetic-ticket-hero" aria-hidden="true">
            <img src={ticketHero} alt="" />
          </div>
          <div className="cosmetic-cost-buttons">{([1, 10] as const).map((count) => {
            const cost = count === 1 ? banner.oneDraw : banner.tenDraw;
            const ricePrice = banner.singleRiceCost * count;
            return <button key={count} type="button" disabled={pending || !cost.executable} onClick={() => onDraw(banner, count)}>
              <strong>{count}회 뽑기</strong>
              <span className="cosmetic-draw-cost" aria-hidden="true">
                <span><img src={ticketIcon} alt="" /><b>{count}</b></span>
                <i>/</i>
                <span><img src={riceIcon} alt="" /><b>{formatRice(ricePrice)}</b></span>
              </span>
              <span className="sr-only">뽑기권 {count}개 또는 쌀 {formatRice(ricePrice)}</span>
            </button>;
          })}</div>
          <section className="cosmetic-milestone" aria-label={`${banner.bannerId} 마일스톤`}>
            <span className="sr-only">선택 치장</span>
            <CosmeticMilestoneProgress milestone={banner.milestone} pending={pending} claimed={claimedBannerId === banner.bannerId} onClaim={() => onCommand?.({ kind: "claim", bannerId: banner.bannerId, count: 1 })} onOpenSelector={() => onOpenSelector?.(banner)} />
          </section>
        </article>;
      })}
    </div>
  </section>;
}
