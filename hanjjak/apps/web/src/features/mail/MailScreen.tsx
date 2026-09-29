import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { mailApi, mailDateLabel, mailTypeLabel, type MailMessage } from "./api";
import { TopAlert } from "../../shared/TopAlert";
import mailTitleIcon from "../battle/assets-utility-nav-v1/notification-message-button.png";
import riceIcon from "../equipment/assets-cozy-pixel/currency-rice-gold-256.png";
import closeIcon from "../../shared/assets/cozy-paper-v2/close-button.png";
import "./MailScreen.css";

export type MailDataSource = Pick<typeof mailApi, "list" | "claim" | "claimAll">;
export type MailTab = "notice" | "mail";

/*
 * 거래소 판매 정산은 거래소의 받기 칸이 맡는다. 같은 우편을 두 곳에서 보여 주면
 * 한쪽에서 받은 것이 다른 쪽에서 소리 없이 사라져 무슨 일이 일어났는지 알 수 없다.
 * 이 함에는 운영자가 보내는 것만 담는다.
 */
export const MARKET_MAIL_TYPES = new Set(["MARKET_SETTLEMENT"]);
export function giftMailOnly(items: MailMessage[]): MailMessage[] {
  return items.filter((mail) => !MARKET_MAIL_TYPES.has(mail.type));
}

/** 아직 받지 않은 것이 위로 온다. 받은 것은 기록으로 남아 아래에 쌓인다. */
export function sortMailForReading(items: MailMessage[]): MailMessage[] {
  return [...items].sort((left, right) => {
    if (left.claimed !== right.claimed) return left.claimed ? 1 : -1;
    return right.createdAt.localeCompare(left.createdAt);
  });
}

export function claimableRice(items: MailMessage[]): number {
  return items.filter((mail) => !mail.claimed).reduce((total, mail) => total + mail.riceAmount, 0);
}

/*
 * 알림과 메시지는 성격이 다르다. 알림은 읽고 마는 것, 메시지는 받아 가는 것이다.
 * 지금 서버가 보내는 것은 받아 갈 쌀뿐이라 알림 쪽은 비어 있지만, 자리는 먼저
 * 잡아 둔다 — 나중에 알림이 붙을 때 화면을 다시 짜지 않아도 되게.
 */
const TABS: Array<{ id: MailTab; label: string; tone: "red" | "blue" }> = [
  { id: "notice", label: "알림", tone: "red" },
  { id: "mail", label: "메시지", tone: "blue" },
];

export function MailScreen({ dataSource = mailApi, initialTab = "notice", onClose }: {
  dataSource?: MailDataSource;
  initialTab?: MailTab;
  onClose?: () => void;
}) {
  const client = useQueryClient();
  const [tab, setTab] = useState<MailTab>(initialTab);
  const [failure, setFailure] = useState<string | null>(null);
  const mails = useQuery({ queryKey: ["mails"], queryFn: () => dataSource.list(), retry: false });

  const refresh = () => Promise.all([
    client.invalidateQueries({ queryKey: ["mails"] }),
    client.invalidateQueries({ queryKey: ["auth", "session"] }),
  ]);
  const fail = (fallback: string) => () => setFailure(fallback);
  const claim = useMutation({
    mutationFn: (mailId: string) => dataSource.claim(mailId, crypto.randomUUID()),
    onSuccess: () => { setFailure(null); void refresh(); },
    onError: fail("우편을 받지 못했어요."),
  });
  const claimAll = useMutation({
    mutationFn: () => dataSource.claimAll(crypto.randomUUID()),
    onSuccess: () => { setFailure(null); void refresh(); },
    onError: fail("우편을 모두 받지 못했어요."),
  });

  const items = sortMailForReading(giftMailOnly(mails.data?.items ?? []));
  const waiting = items.filter((mail) => !mail.claimed);
  const busy = claim.isPending || claimAll.isPending;
  const onMail = tab === "mail";

  return <section className="mail-screen" aria-labelledby="mail-title">
    {failure && <TopAlert message={failure} action={<button type="button" onClick={() => { setFailure(null); void mails.refetch(); }}>다시 확인</button>} />}

    <div className="mail-paper">
      <aside className="mail-sidebar" aria-label="메시지함 메뉴">
        <nav>{TABS.map((entry) => <button
          key={entry.id}
          type="button"
          className={`mail-tab-${entry.tone}${tab === entry.id ? " is-active" : ""}`}
          aria-current={tab === entry.id ? "page" : undefined}
          onClick={() => setTab(entry.id)}
        >{entry.label}{entry.id === "mail" && waiting.length > 0 && <em>{waiting.length}</em>}</button>)}</nav>
      </aside>

      <header className="mail-heading">
        <h2 id="mail-title"><img src={mailTitleIcon} alt="" aria-hidden="true" /><span>{onMail ? "메시지" : "알림"}</span></h2>
        {onMail && <>
          <p className="mail-summary">
            <img src={riceIcon} alt="" aria-hidden="true" />
            <span>받을 쌀</span>
            <strong>{claimableRice(items).toLocaleString()}</strong>
          </p>
          <button
            type="button"
            className="mail-claim-all"
            disabled={busy || waiting.length === 0}
            onClick={() => claimAll.mutate()}
          >{claimAll.isPending ? "받는 중…" : "모두 받기"}</button>
        </>}
        {onClose && <button type="button" className="mail-close" aria-label="메시지함 닫기" onClick={onClose}>
          <img src={closeIcon} alt="" aria-hidden="true" />
        </button>}
      </header>

      <div className="mail-body">
        {!onMail && <p className="mail-empty">아직 도착한 알림이 없어요.</p>}
        {onMail && mails.isLoading && <p className="mail-empty" role="status">우편을 불러오는 중입니다.</p>}
        {onMail && mails.error && <p className="mail-empty is-error" role="alert">우편을 불러오지 못했습니다.<button type="button" onClick={() => void mails.refetch()}>다시 불러오기</button></p>}
        {onMail && mails.data && items.length === 0 && <p className="mail-empty">아직 도착한 우편이 없어요.</p>}
        {onMail && items.length > 0 && <ul className="mail-list">{items.map((mail) => <li key={mail.mailId} className={mail.claimed ? "is-claimed" : undefined}>
          <img src={riceIcon} alt="" aria-hidden="true" />
          <span className="mail-row-copy">
            <strong>{mailTypeLabel(mail.type)}</strong>
            <small>{mailDateLabel(mail.createdAt)}</small>
          </span>
          <b className="mail-row-amount">{mail.riceAmount.toLocaleString()}</b>
          {mail.claimed
            ? <span className="mail-row-done">받음</span>
            : <button type="button" disabled={busy} aria-label={`${mailTypeLabel(mail.type)} ${mail.riceAmount.toLocaleString()}쌀 받기`} onClick={() => claim.mutate(mail.mailId)}>받기</button>}
        </li>)}</ul>}
      </div>
    </div>
  </section>;
}
