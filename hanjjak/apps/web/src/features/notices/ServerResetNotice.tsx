import { useEffect, useState } from "react";
import mascot from "../battle/assets-notices-v1/sad-chopstick-mascot.png";
import closeIcon from "../../shared/assets/cozy-paper-v2/close-button.png";
import "./ServerResetNotice.css";

const STORAGE_KEY = "hanjjak-server-reset-notice-seen-at";

/**
 * 안내는 접속할 때마다 한 번 보여 준다. 판이 갈리는 일을 못 보고 지나치면 곤란한
 * 안내라, 이번 접속에서 닫았을 때만 접어 둔다. 창을 닫았다가 다시 들어오면 새 접속
 * 이므로 다시 뜬다. 시크릿 창처럼 저장소를 못 쓰는 곳에서는 읽기가 던지는데, 그때는
 * 안내를 못 본 것으로 보고 띄우는 쪽을 고른다.
 */
export function serverResetNoticeDue(storage: Pick<Storage, "getItem">): boolean {
  try {
    return storage.getItem(STORAGE_KEY) === null;
  } catch {
    return true;
  }
}

export function markServerResetNoticeSeen(storage: Pick<Storage, "setItem">, now = Date.now()): void {
  try {
    storage.setItem(STORAGE_KEY, String(now));
  } catch {
    /* 저장을 못 해도 이번 화면은 닫혀야 한다. 다음 접속에 다시 뜰 뿐이다. */
  }
}

export function ServerResetNotice({ openLabel = "9월 15일", forceOpen = false }: { openLabel?: string; forceOpen?: boolean }) {
  /* 이번 접속에서 아직 닫지 않았으면 띄운다. 접속이 끝나면 표시도 함께 사라진다. */
  const [open, setOpen] = useState(() => forceOpen || serverResetNoticeDue(globalThis.sessionStorage));

  useEffect(() => {
    if (!open) return;
    const closeOnEscape = (event: KeyboardEvent) => event.key === "Escape" && dismiss();
    globalThis.addEventListener("keydown", closeOnEscape);
    return () => globalThis.removeEventListener("keydown", closeOnEscape);
  });

  if (!open) return null;

  function dismiss() {
    markServerResetNoticeSeen(globalThis.sessionStorage);
    setOpen(false);
  }

  return <div className="server-reset-notice-backdrop" onMouseDown={(event) => event.target === event.currentTarget && dismiss()}>
    <section className="server-reset-notice" role="dialog" aria-modal="true" aria-labelledby="server-reset-notice-title">
      <button type="button" className="server-reset-notice-close" onClick={dismiss} aria-label="안내 닫기">
        <img src={closeIcon} alt="" aria-hidden="true" />
      </button>
      <img className="server-reset-notice-mascot" src={mascot} alt="" aria-hidden="true" />
      <div className="server-reset-notice-copy">
        <h2 id="server-reset-notice-title">정식 서버 오픈 안내</h2>
        <p className="server-reset-notice-badge">{openLabel} 정식 오픈</p>
        <p className="server-reset-notice-lead">정식 서버가 시작되면</p>
        <p className="server-reset-notice-point">현재 플레이 데이터가 초기화됩니다.</p>
        <p className="server-reset-notice-thanks">지금까지 함께해 주셔서 감사합니다.</p>
      </div>
      <button type="button" className="server-reset-notice-cta" onClick={dismiss}>확인했어요</button>
    </section>
  </div>;
}
