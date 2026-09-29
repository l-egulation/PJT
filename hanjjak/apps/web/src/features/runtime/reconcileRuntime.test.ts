import { describe, expect, it } from "vitest";
import type { RuntimeCheckpoint, RuntimeCheckpointStore } from "../../shared/persistence/runtimeCheckpointDb";
import { restoreRuntime } from "./reconcileRuntime";

class MemoryCheckpointStore implements RuntimeCheckpointStore {
  checkpoint: RuntimeCheckpoint | null = null;
  async load(): Promise<RuntimeCheckpoint | null> { return this.checkpoint; }
  async save(checkpoint: RuntimeCheckpoint): Promise<void> { this.checkpoint = checkpoint; }
  async clearRuntime(): Promise<void> { this.checkpoint = null; }
}

describe("runtime restore", () => {
  it("uses the server idle mode and stage at full hp and zero progress", async () => {
    const checkpoints = new MemoryCheckpointStore();
    checkpoints.checkpoint = {
      accountId: "account-1",
      schemaVersion: 1,
      contentVersion: "enemy-v1-applied",
      idleMode: "REPEAT_STAGE",
      stageId: "stage.01-01",
      hp: 1,
      defeatedNormals: 19,
      savedAt: "2026-09-01T00:00:00Z",
    };

    const restored = await restoreRuntime({
      accountId: "account-1",
      stateVersion: 7,
      contentVersion: "enemy-v1-applied",
      currentStageId: "stage.02-03",
      idleMode: "AUTO_PROGRESS",
      repeatStageId: null,
      maxHp: 1240,
    }, checkpoints);

    expect(restored).toMatchObject({ idleMode: "AUTO_PROGRESS", repeatStageId: null, selectedStageId: "stage.02-03", hp: 1240, maxHp: 1240, defeatedNormals: 0 });
  });

  it("drops an incompatible checkpoint without importing its combat state", async () => {
    const checkpoints = new MemoryCheckpointStore();
    checkpoints.checkpoint = {
      accountId: "account-1",
      schemaVersion: 1,
      contentVersion: "old",
      idleMode: "REPEAT_STAGE",
      stageId: "stage.04-10",
      hp: 1,
      defeatedNormals: 19,
      savedAt: "2026-09-01T00:00:00Z",
    };

    const restored = await restoreRuntime({
      accountId: "account-1",
      stateVersion: 8,
      contentVersion: "enemy-v1-applied",
      currentStageId: "stage.01-05",
      idleMode: "REPEAT_STAGE",
      repeatStageId: "stage.01-04",
      maxHp: 900,
    }, checkpoints);

    expect(checkpoints.checkpoint).toBeNull();
    expect(restored).toMatchObject({ idleMode: "REPEAT_STAGE", repeatStageId: "stage.01-04", selectedStageId: "stage.01-05", hp: 900, defeatedNormals: 0 });
  });
});
