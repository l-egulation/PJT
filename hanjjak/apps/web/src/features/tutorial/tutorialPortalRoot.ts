import { useEffect, useState } from "react";
import { TUTORIAL_MODAL_SCREEN_ROOTS, type TutorialRuntimeSurface } from "./tutorialRuntimeCatalog";

/*
 * 장비·스킬·보석 화면은 네이티브 `<dialog>` 를 `showModal()` 로 연다. 그렇게 열린 창은
 * 브라우저의 top layer 에 그려져서, 바깥에 둔 요소는 z-index 를 아무리 올려도 그 아래로
 * 깔리고 클릭도 받지 못한다. 안내 오버레이와 다시 보기 단추는 그 창 안에 심어야
 * 설명 대상 위에 보이고 눌린다. 화면이 응답을 받은 뒤 그려질 수 있으므로 자리가
 * 생길 때까지 지켜본다. 창이 아닌 화면에서는 null 을 돌려주고 평소 자리에 그린다.
 */
export function useTutorialPortalRoot(surface: TutorialRuntimeSurface | null): HTMLElement | null {
  const selector = surface ? TUTORIAL_MODAL_SCREEN_ROOTS[surface] ?? null : null;
  const [root, setRoot] = useState<HTMLElement | null>(null);

  useEffect(() => {
    if (!selector) {
      setRoot(null);
      return;
    }
    const find = () => setRoot((current) => {
      const next = document.querySelector<HTMLElement>(selector);
      return next === current ? current : next;
    });
    find();
    const observer = typeof MutationObserver === "undefined" ? null : new MutationObserver(find);
    observer?.observe(document.body, { childList: true, subtree: true });
    return () => observer?.disconnect();
  }, [selector]);

  return root;
}
