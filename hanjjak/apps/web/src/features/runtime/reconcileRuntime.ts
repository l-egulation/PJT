import type { ServerRuntimeSnapshot } from "./api";
import type { RuntimeCheckpointStore } from "../../shared/persistence/runtimeCheckpointDb";

export type RestoredRuntime = {
  accountId: string;
  stateVersion: number;
  contentVersion: string;
  selectedStageId: string;
  idleMode: "AUTO_PROGRESS" | "REPEAT_STAGE";
  repeatStageId: string | null;
  hp: number;
  maxHp: number;
  defeatedNormals: number;
  residenceSessionId: string;
};

export async function restoreRuntime(
  server: ServerRuntimeSnapshot,
  checkpoints: RuntimeCheckpointStore,
): Promise<RestoredRuntime> {
  const checkpoint = await checkpoints.load(server.accountId).catch(() => null);
  if (checkpoint && checkpoint.contentVersion !== server.contentVersion) {
    await checkpoints.clearRuntime(server.accountId).catch(() => undefined);
  }
  return {
    accountId: server.accountId,
    stateVersion: server.stateVersion,
    contentVersion: server.contentVersion,
    selectedStageId: server.currentStageId,
    idleMode: server.idleMode,
    repeatStageId: server.repeatStageId,
    hp: server.maxHp,
    maxHp: server.maxHp,
    defeatedNormals: 0,
    residenceSessionId: crypto.randomUUID(),
  };
}
