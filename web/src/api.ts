import type { ReportDefinition, ReportResponse, ReportSummary } from "./types";

async function json<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const body = await response.text();
    throw new Error(`${response.status} ${response.statusText}${body ? `: ${body}` : ""}`);
  }
  return (await response.json()) as T;
}

export const api = {
  listReports: (): Promise<ReportSummary[]> => fetch("/api/reports").then(json<ReportSummary[]>),

  getReport: (id: string): Promise<ReportResponse> =>
    fetch(`/api/reports/${id}`).then(json<ReportResponse>),

  createReport: (definition: ReportDefinition): Promise<ReportResponse> =>
    fetch("/api/reports", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(definition),
    }).then(json<ReportResponse>),

  updateReport: (
    id: string,
    definition: ReportDefinition,
    concurrencyToken?: string,
  ): Promise<ReportResponse> =>
    fetch(`/api/reports/${id}`, {
      method: "PUT",
      headers: {
        "Content-Type": "application/json",
        ...(concurrencyToken ? { "If-Match": `"${concurrencyToken}"` } : {}),
      },
      body: JSON.stringify(definition),
    }).then(json<ReportResponse>),

  deleteReport: (id: string): Promise<void> =>
    fetch(`/api/reports/${id}`, { method: "DELETE" }).then((r) => {
      if (!r.ok && r.status !== 404) throw new Error(`${r.status} ${r.statusText}`);
    }),
};
