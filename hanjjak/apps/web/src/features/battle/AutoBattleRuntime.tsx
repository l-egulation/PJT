import { useEffect, useRef } from "react";
import { useQueryClient, type QueryClient } from "@tanstack/react-query";
import { registerAutoBattleClient } from "./autoBattleControl";
import { AutoBattleClient } from "./autoBattleClient";
import { useBattleRuntimeStore } from "./runtimeStore";
import { runtimeCheckpoints } from "../runtime/checkpoints";
import { gameSessionClient } from "../runtime/gameSessionClient";
import { usePictureInPictureSessionStore } from "../picture-in-picture/pictureInPictureSessionStore";

const BATTLE_COMPLETION_QUERY_KEYS = [
  ["stages"],
  ["battle-history"],
  ["inventory"],
  ["auth", "session"],
  ["character-stats"],
  ["gems"],
  ["cosmetic-collection"],
  ["first-clear-rewards"],
] as const;

export function invalidateBattleCompletionQueries(queryClient: Pick<QueryClient, "invalidateQueries">): void {
  BATTLE_COMPLETION_QUERY_KEYS.forEach((queryKey) => {
    void queryClient.invalidateQueries({ queryKey });
  });
}

export function AutoBattleRuntime() {
  const queryClient = useQueryClient();
  const clientRef = useRef<AutoBattleClient | null>(null);
  const connected = useBattleRuntimeStore((state) => state.connected);
  const gameSessionId = useBattleRuntimeStore((state) => state.gameSessionId);
  const selectedStageId = useBattleRuntimeStore((state) => state.selectedStageId);
  const battleError = useBattleRuntimeStore((state) => state.battleError);

  useEffect(() => {
    const client = new AutoBattleClient({
      gameSessionId: () => useBattleRuntimeStore.getState().gameSessionId,
      stageId: () => useBattleRuntimeStore.getState().selectedStageId,
      onStarted: (session) => useBattleRuntimeStore.getState().startBattleSession(session),
      onProgress: (checkpoint) => useBattleRuntimeStore.getState().updateBattleProgress(checkpoint),
      onEvents: (events) => useBattleRuntimeStore.getState().applyBattleEvents(events),
      onSettled: (settlements) => {
        useBattleRuntimeStore.getState().applyBattleSettlements(settlements);
        usePictureInPictureSessionStore.getState().applySettlements(settlements);
        void queryClient.invalidateQueries({ queryKey: ["inventory"] });
      },
      onCompleted: (result, retryAt) => {
        useBattleRuntimeStore.getState().completeBattleSession(result, retryAt, runtimeCheckpoints);
        usePictureInPictureSessionStore.getState().applySettlements(result.settlements);
        usePictureInPictureSessionStore.getState().applyCompletion(result);
        invalidateBattleCompletionQueries(queryClient);
      },
      onStopped: () => useBattleRuntimeStore.getState().stopBattleSession(),
      onError: (error) => {
        useBattleRuntimeStore.getState().failBattleSession(error.message);
        if (error.message === "GAME_SESSION_NOT_ACTIVE") gameSessionClient.invalidate();
      },
    });
    clientRef.current = client;
    registerAutoBattleClient(client);
    return () => {
      registerAutoBattleClient(null);
      clientRef.current = null;
      void client.stop(true);
    };
  }, [queryClient]);

  useEffect(() => {
    const client = clientRef.current;
    if (!client) return;
    if (connected && gameSessionId && !battleError) void client.start();
    else void client.stop(true);
  }, [connected, gameSessionId, battleError]);

  useEffect(() => {
    const client = clientRef.current;
    if (!client?.currentStageId || client.currentStageId === selectedStageId) return;
    void client.stop().then(() => client.start());
  }, [selectedStageId]);

  return null;
}
