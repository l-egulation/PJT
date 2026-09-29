import type { AutoBattleClient } from "./autoBattleClient";

let activeClient: AutoBattleClient | null = null;

export function registerAutoBattleClient(client: AutoBattleClient | null): void {
  activeClient = client;
}

export async function abortActiveBattle(keepalive = false): Promise<void> {
  await activeClient?.stop(keepalive);
}

export async function restartActiveBattle(): Promise<boolean> {
  if (!activeClient?.active) return false;
  await activeClient.stop();
  await activeClient.start();
  return true;
}
