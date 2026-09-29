import { useEffect } from "react";
import { useBattleRuntimeStore } from "../battle/runtimeStore";
import { fetchRuntimeState } from "./api";
import { runtimeCheckpoints } from "./checkpoints";
import { gameSessionClient } from "./gameSessionClient";
import { abortActiveBattle } from "../battle/autoBattleControl";

let activeLifecycleCount = 0;
let pendingFinalCleanup: number | undefined;

const RECOVERY_DELAY_MS = 1_000;

export function RuntimeLifecycle() {
  useEffect(() => {
    activeLifecycleCount += 1;
    clearTimeout(pendingFinalCleanup);
    pendingFinalCleanup = undefined;
    let disposed = false;
    let recoveryTimer: number | undefined;
    let connecting = false;

    const setDisconnected = () => {
      useBattleRuntimeStore.getState().setConnection(false);
    };
    const scheduleRecovery = () => {
      if (disposed || recoveryTimer !== undefined || !navigator.onLine) return;
      recoveryTimer = window.setTimeout(() => {
        recoveryTimer = undefined;
        void connect();
      }, RECOVERY_DELAY_MS);
    };
    const connect = async () => {
      if (connecting) return;
      connecting = true;
      try {
        const snapshot = await fetchRuntimeState();
        if (disposed) return;
        await useBattleRuntimeStore.getState().restore(snapshot, runtimeCheckpoints);
        if (disposed) return;
        const session = await gameSessionClient.open(snapshot.accountId);
        if (disposed) return;
        useBattleRuntimeStore.getState().setConnection(true, session.gameSessionId);
      } catch {
        if (disposed) return;
        gameSessionClient.suspend();
        setDisconnected();
        scheduleRecovery();
      } finally {
        connecting = false;
      }
    };
    const handleOffline = () => {
      void abortActiveBattle(true);
      gameSessionClient.suspend();
      setDisconnected();
    };
    const handleOnline = () => void connect();
    const handlePageHide = (event: PageTransitionEvent) => {
      void abortActiveBattle(true);
      gameSessionClient.suspend();
      setDisconnected();
    };
    const handlePageShow = (event: PageTransitionEvent) => {
      if (!event.persisted) return;
      gameSessionClient.suspend();
      void connect();
    };

    gameSessionClient.setSessionLostHandler(() => {
      setDisconnected();
      scheduleRecovery();
    });
    window.addEventListener("offline", handleOffline);
    window.addEventListener("online", handleOnline);
    window.addEventListener("pagehide", handlePageHide);
    window.addEventListener("pageshow", handlePageShow);
    void connect();

    return () => {
      disposed = true;
      clearTimeout(recoveryTimer);
      gameSessionClient.setSessionLostHandler(null);
      window.removeEventListener("offline", handleOffline);
      window.removeEventListener("online", handleOnline);
      window.removeEventListener("pagehide", handlePageHide);
      window.removeEventListener("pageshow", handlePageShow);
      activeLifecycleCount -= 1;
      pendingFinalCleanup = window.setTimeout(() => {
        if (activeLifecycleCount > 0) return;
        void abortActiveBattle(true);
        gameSessionClient.suspend();
        useBattleRuntimeStore.getState().setConnection(false);
      });
    };
  }, []);

  return null;
}
