import type { ReactNode } from "react";
import "./NoticeDialog.css";

/** 화면을 밀어내지 않는 공용 알림 창. 장비·스킬·전투 연결이 모두 이 한 가지 모양을 쓴다. */
export function NoticeDialog({ title = "알림", message, detail, actions }: {
  title?: string;
  message: ReactNode;
  detail?: ReactNode;
  actions: ReactNode;
}) {
  return <div className="notice-dialog-backdrop" role="presentation">
    <section className="notice-dialog" role="dialog" aria-modal="true" aria-label={title}>
      <h3>{title}</h3>
      <p>{message}</p>
      {detail != null && <p className="notice-dialog-detail">{detail}</p>}
      <div className="notice-dialog-actions">{actions}</div>
    </section>
  </div>;
}
