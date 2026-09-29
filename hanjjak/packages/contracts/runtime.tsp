using TypeSpec.Http;
namespace Hanjjak;

model RuntimeStateSnapshot {
  accountId: Uuid;
  stateVersion: int64;
  contentVersion: string;
  currentStageId: string;
  idleMode: "AUTO_PROGRESS" | "REPEAT_STAGE";
  repeatStageId: string | null;
  maxHp: int32;
}

@route("/api/v1/runtime-state")
interface RuntimeState {
  @get read(): SuccessEnvelope<RuntimeStateSnapshot> | ErrorEnvelope;
}
