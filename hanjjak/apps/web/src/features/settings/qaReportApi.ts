export type QaReportPayload = {
  description: string;
  pageUrl: string;
  userAgent: string;
  viewportWidth: number;
  viewportHeight: number;
  imageDataUrl?: string;
};

export async function fileAsDataUrl(file: File): Promise<string> {
  if (!/^image\/(png|jpeg|webp)$/.test(file.type)) throw new Error("IMAGE_TYPE");
  if (file.size > 4 * 1024 * 1024) throw new Error("IMAGE_TOO_LARGE");
  return await new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(new Error("IMAGE_READ_FAILED"));
    reader.onload = () => resolve(String(reader.result));
    reader.readAsDataURL(file);
  });
}

export async function submitQaReport(payload: QaReportPayload): Promise<{ reportId: string; status: string }> {
  const response = await fetch("/api/v1/qa/reports", {
    method: "POST",
    credentials: "include",
    headers: { "Content-Type": "application/json", "Idempotency-Key": crypto.randomUUID() },
    body: JSON.stringify(payload),
  });
  const body = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(typeof body.code === "string" ? body.code : "QA_REPORT_FAILED");
  return body.data;
}
