using TypeSpec.Http;
namespace Hanjjak;

union GameSessionStatus {
  ACTIVE: "ACTIVE",
  REPLACED: "REPLACED",
  CLOSED: "CLOSED",
  EXPIRED: "EXPIRED"
}

model GameSessionState {
  gameSessionId: Uuid;
  status: GameSessionStatus;
  heartbeatIntervalSeconds: int32;
  expiresAfterSeconds: int32;
}

@route("/api/v1/game-sessions")
interface GameSessions {
  @post open(@header("Idempotency-Key") key: Uuid): SuccessEnvelope<GameSessionState> | ErrorEnvelope;
  @post @route("/{gameSessionId}/heartbeat") heartbeat(@path gameSessionId: Uuid, @header("Idempotency-Key") key: Uuid): SuccessEnvelope<GameSessionState> | ErrorEnvelope;
  @post @route("/{gameSessionId}/close") close(@path gameSessionId: Uuid, @header("Idempotency-Key") key: Uuid): SuccessEnvelope<GameSessionState> | ErrorEnvelope;
}
