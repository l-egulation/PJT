import type { ReactNode } from "react";
import "./TopAlert.css";

/**
 * 서버가 준 문제는 화면 맨 위 한 줄로만 알린다.
 *
 * 예전에는 창 안에 줄을 끼워 넣었다. 그러면 그만큼 아래 내용이 밀려, 누르려던 단추가
 * 손가락 아래에서 움직였다. 자리를 화면에 고정해 두면 어떤 창이 열려 있든 같은 곳에
 * 뜨고, 창 안 배치는 아무 영향을 받지 않는다.
 *
 * 창이 `<dialog>` 인 화면에서는 이 줄도 그 창 안에 두어야 최상위 층에서 함께 그려진다.
 */
export function TopAlert({ message, action, tone = "error" }: {
  message: ReactNode;
  action?: ReactNode;
  tone?: "error" | "warning";
}) {
  return <div className={`top-alert top-alert-${tone}`} role="alert">
    <p>{message}</p>
    {action}
  </div>;
}
