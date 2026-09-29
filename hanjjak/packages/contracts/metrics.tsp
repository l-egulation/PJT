using TypeSpec.Http;

namespace Hanjjak;

model EconomyMetricRefreshRequest {
  from: string;
  to: string;
}

model EconomyDailyMetric {
  metricDate: string;
  itemFamily: string;
  generation: int32 | null;
  itemId: string;
  listedQuantity: int64;
  cancelledQuantity: int64;
  tradeCount: int64;
  tradedQuantity: int64;
  tradeAmount: int64;
  feeAmount: int64;
  settlementAmount: int64;
  droppedQuantity: int64;
  consumedQuantity: int64;
  riceGenerated: int64;
  riceConsumed: int64;
}

@route("/api/v1/metrics/economy")
interface EconomyMetrics {
  @post @route("/daily/refresh") refreshDaily(@body request: EconomyMetricRefreshRequest): SuccessEnvelope<EconomyDailyMetric[]> | ErrorEnvelope;
  @get @route("/daily") listDaily(@query from: string, @query to: string): SuccessEnvelope<EconomyDailyMetric[]> | ErrorEnvelope;
}
