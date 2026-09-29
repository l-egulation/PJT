import type { ReactNode } from "react";
import "./LoadingScene.css";

/**
 * 아직 화면을 그릴 수 없을 때 서는 자리. 세션을 확인하는 중이든, 나라를 고르기
 * 전이든, 다시 이어 붙는 중이든 같은 모습이어야 한다 — 빈 바탕에 글자 한 줄만
 * 뜨면 무엇이 잘못된 것처럼 보인다. 길드 배경 위에 종이 한 장을 놓고 그 안에서
 * 말한다.
 */
export function LoadingScene({ message = "한짝을 불러오는 중입니다.", action }: {
  message?: ReactNode;
  action?: ReactNode;
}) {
  return <main className="loading-scene">
    <section className="loading-scene-paper" role="status" aria-live="polite">
      <strong>{message}</strong>
      {action}
    </section>
  </main>;
}
