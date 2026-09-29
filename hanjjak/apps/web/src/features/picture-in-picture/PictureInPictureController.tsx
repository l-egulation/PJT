import { useCallback, useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { PetBattleSurface } from "../battle/PetBattleSurface";
import pictureInPictureButton from "../battle/assets-utility-nav-v1/picture-in-picture-button.png";
import { getPictureInPictureAvailability, openPictureInPictureDocument } from "./documentPictureInPicture";
import { usePictureInPictureSessionStore } from "./pictureInPictureSessionStore";

type ControllerState = "inline" | "opening" | "pip" | "failed" | "unsupported";

function errorMessage(error: unknown): string {
  if (error instanceof DOMException && error.name === "NotAllowedError") return "브라우저에서 화면 한켠 보기를 허용하지 않았습니다.";
  return "화면 한켠 보기를 열지 못했습니다. 메인 화면에서 계속 표시합니다.";
}

export function PictureInPictureController({ visible = true }: { visible?: boolean }) {
  const availability = getPictureInPictureAvailability();
  const [state, setState] = useState<ControllerState>(availability.supported ? "inline" : "unsupported");
  const [message, setMessage] = useState(availability.supported ? "메인 화면에 표시 중입니다." : availability.reason === "insecure" ? "HTTPS 환경에서 화면 한켠 보기를 사용할 수 있습니다." : "이 브라우저에서는 화면 한켠 보기를 지원하지 않습니다.");
  const [portalTarget, setPortalTarget] = useState<HTMLElement | null>(null);
  const pipWindowRef = useRef<Window | null>(null);
  const mountedRef = useRef(true);

  const restoreInline = useCallback(() => {
    pipWindowRef.current = null;
    usePictureInPictureSessionStore.getState().stop();
    if (!mountedRef.current) return;
    setPortalTarget(null);
    setState("inline");
    setMessage("화면 한켠 보기를 닫고 메인 화면으로 돌아왔습니다.");
  }, []);

  const close = useCallback(() => {
    const pipWindow = pipWindowRef.current;
    if (pipWindow && !pipWindow.closed) pipWindow.close();
    restoreInline();
  }, [restoreInline]);

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
      usePictureInPictureSessionStore.getState().stop();
      const pipWindow = pipWindowRef.current;
      pipWindowRef.current = null;
      if (pipWindow && !pipWindow.closed) pipWindow.close();
    };
  }, []);

  const open = async () => {
    if (!availability.supported || state === "opening" || state === "pip") return;
    setState("opening");
    setMessage("화면 한켠 보기를 여는 중입니다.");
    try {
      const pip = await openPictureInPictureDocument();
      if (!mountedRef.current) {
        pip.window.close();
        return;
      }
      pipWindowRef.current = pip.window;
      usePictureInPictureSessionStore.getState().start();
      pip.window.addEventListener("pagehide", restoreInline, { once: true });
      setPortalTarget(pip.mountNode);
      setState("pip");
      setMessage("화면 한켠 보기에서 한짝이 실행 중입니다.");
    } catch (error) {
      if (!mountedRef.current) return;
      setPortalTarget(null);
      setState("failed");
      setMessage(errorMessage(error));
    }
  };

  const actionLabel = state === "pip"
    ? "메인 화면으로 돌아오기"
    : state === "opening"
      ? "화면 한켠 보기 여는 중"
      : state === "failed"
        ? "화면 한켠 보기 다시 열기"
        : state === "unsupported"
          ? "화면 한켠 보기 미지원"
          : "화면 한켠에서 보기";
  return <section className={`picture-in-picture-shell picture-in-picture-compact ${state === "pip" ? "active" : ""} ${visible ? "" : "is-hidden"}`} aria-labelledby="picture-in-picture-title">
    <div className="picture-in-picture-toolbar">
      <h2 id="picture-in-picture-title" className="sr-only">화면 한켠의 한짝</h2>
      {state === "pip"
        ? <button type="button" onClick={close} aria-label={actionLabel} title={message}><img src={pictureInPictureButton} alt="" /></button>
        : <button type="button" onClick={open} disabled={state === "opening" || state === "unsupported"} aria-label={actionLabel} title={message}><img src={pictureInPictureButton} alt="" /></button>}
    </div>
    <p className={`picture-in-picture-message ${state === "failed" ? "error" : ""}`} role={state === "failed" ? "alert" : "status"}>{message}</p>
    {portalTarget ? createPortal(<PetBattleSurface />, portalTarget) : null}
  </section>;
}
