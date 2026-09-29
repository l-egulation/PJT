import { PlazaIcon } from "./PlazaIcon";
import { itemArt, riceIcon } from "./marketArt";
import type { MarketDelivery, MarketSettlementMail } from "./api";

const SOURCE_LABELS: Record<MarketDelivery["source"], string> = {
  BUY_FILL: "구매한 물품",
  SELL_CANCEL_RETURN: "취소한 판매 물품",
  SELL_EXPIRE_RETURN: "기간이 끝난 판매 물품",
};

export type MarketInboxProps = {
  deliveries: MarketDelivery[];
  settlements: MarketSettlementMail[];
  loading: boolean;
  busy: boolean;
  onClaimDelivery: (deliveryId: string) => void;
  onClaimSettlement: (mailId: string) => void;
  onClaimAll: () => void;
};

export function MarketInbox({ deliveries, settlements, loading, busy, onClaimDelivery, onClaimSettlement, onClaimAll }: MarketInboxProps) {
  const items = deliveries.filter(delivery => !delivery.claimedAt);
  const mails = settlements.filter(mail => !mail.claimed);
  const totalRice = mails.reduce((sum, mail) => sum + mail.riceAmount, 0);

  if (loading) return <p className="market-plaza-empty" role="status">받을 것을 확인하는 중이에요…</p>;
  return <div className="market-plaza-inbox">
    <header className="market-plaza-inbox-summary">
      <p><PlazaIcon name="parcel" /><span>받을 물품</span><strong>{items.length}건</strong></p>
      <i aria-hidden="true" />
      <p><img src={riceIcon} alt="" /><span>받을 쌀</span><strong>{totalRice.toLocaleString()}쌀</strong></p>
      <button type="button" className="market-plaza-cta" disabled={busy || (items.length === 0 && mails.length === 0)} onClick={onClaimAll}><PlazaIcon name="claim" />모두 받기</button>
    </header>
    <div className="market-plaza-inbox-columns">
      <section aria-labelledby="market-plaza-inbox-items">
        <header><PlazaIcon name="parcel" /><h3 id="market-plaza-inbox-items">받을 물품</h3></header>
        <div className="market-plaza-scroll">
          {items.length === 0 && <p className="market-plaza-empty small">받을 물품이 없어요.</p>}
          {items.map(delivery => <article key={delivery.deliveryId} className="market-plaza-inbox-row">
            <img src={itemArt(delivery.itemId)} alt="" />
            <div>
              <strong>{delivery.displayName}</strong>
              <p><span>수량 {delivery.quantity.toLocaleString()}개</span><em>{SOURCE_LABELS[delivery.source]}</em></p>
            </div>
            <button type="button" className="market-plaza-row-button claim" disabled={busy} onClick={() => onClaimDelivery(delivery.deliveryId)}><PlazaIcon name="claim" />받기</button>
          </article>)}
        </div>
      </section>
      <section aria-labelledby="market-plaza-inbox-rice">
        <header><img src={riceIcon} alt="" /><h3 id="market-plaza-inbox-rice">받을 쌀</h3><small></small></header>
        <div className="market-plaza-scroll">
          {mails.length === 0 && <p className="market-plaza-empty small">받을 쌀이 없어요.</p>}
          {mails.map(mail => <article key={mail.mailId} className="market-plaza-inbox-row">
            <img src={riceIcon} alt="" />
            <div>
              <strong>판매 정산</strong>
              <p><span>{mail.riceAmount.toLocaleString()}쌀</span></p>
            </div>
            <button type="button" className="market-plaza-row-button claim" disabled={busy} onClick={() => onClaimSettlement(mail.mailId)}><PlazaIcon name="claim" />받기</button>
          </article>)}
        </div>
      </section>
    </div>
  </div>;
}
