export function formatStageId(stageId: string): string {
  return stageId.replace("stage.", "").replace(/^0/, "").replace("-0", "-");
}
