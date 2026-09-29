import { useEffect, useRef } from "react";
import "./NoticeToast.css";

/**
 * 누를 것이 없는 알림은 확인 단추를 두지 않는다. 화면 가운데에 잠깐 떴다가 스스로 사라지고,
 * 그동안에도 아래 화면은 그대로 누를 수 있다.
 */
export function NoticeToast({ message, tone = "neutral", duration = 1600, onDone }: {
  message: string;
  tone?: "neutral" | "success" | "failure";
  duration?: number;
  onDone: () => void;
}) {
  // onDone 은 부르는 쪽에서 대개 즉석 함수로 넘어온다. 그걸 의존성에 두면 화면이 다시
  // 그려질 때마다 시계가 처음으로 돌아가, 자주 갱신되는 화면에서는 영영 사라지지 않는다.
  const done = useRef(onDone);
  done.current = onDone;
  useEffect(() => {
    const timer = window.setTimeout(() => done.current(), duration);
    return () => window.clearTimeout(timer);
  }, [message, duration]);
  /* 사라지는 그림과 치우는 시각을 같은 값으로 묶는다. */
  return <div className={`notice-toast notice-toast-${tone}`} style={{ animationDuration: `${duration}ms` }} role="status">{message}</div>;
}
