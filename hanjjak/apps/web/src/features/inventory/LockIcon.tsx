/** 가방과 보석 창이 같은 자물쇠 그림을 쓴다. 두 화면이 서로를 불러오면 고리가 생겨서 여기로 뺐다. */
export function LockIcon({ locked }: { locked: boolean }) {
  return <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" aria-hidden="true">
    <rect x="5" y="10.5" width="14" height="10" rx="2.5" fill="currentColor" stroke="none" />
    <path d={locked ? "M8.5 10.5V7.5a3.5 3.5 0 0 1 7 0v3" : "M8.5 10.5V7.5a3.5 3.5 0 0 1 6.8-1.2"} />
  </svg>;
}
