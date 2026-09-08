import type {
  DataField,
  DataSourceDefinition,
  ReportDefinition,
  ReportResponse,
  ReportSummary,
} from "./types";

async function json<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const body = await response.text();
    throw new Error(`${response.status} ${response.statusText}${body ? `: ${body}` : ""}`);
  }
  return (await response.json()) as T;
}

const jsonHeaders = { "Content-Type": "application/json" };

export const api = {
  listReports: (): Promise<ReportSummary[]> => fetch("/api/reports").then(json<ReportSummary[]>),

  getReport: (id: string): Promise<ReportResponse> =>
    fetch(`/api/reports/${id}`).then(json<ReportResponse>),

  createReport: (definition: ReportDefinition): Promise<ReportResponse> =>
    fetch("/api/reports", { method: "POST", headers: jsonHeaders, body: JSON.stringify(definition) })
      .then(json<ReportResponse>),

  updateReport: (id: string, definition: ReportDefinition, token?: string): Promise<ReportResponse> =>
    fetch(`/api/reports/${id}`, {
      method: "PUT",
      headers: { ...jsonHeaders, ...(token ? { "If-Match": `"${token}"` } : {}) },
      body: JSON.stringify(definition),
    }).then(json<ReportResponse>),

  deleteReport: (id: string): Promise<void> =>
    fetch(`/api/reports/${id}`, { method: "DELETE" }).then((r) => {
      if (!r.ok && r.status !== 404) throw new Error(`${r.status} ${r.statusText}`);
    }),

  schema: (source: DataSourceDefinition): Promise<{ fields: DataField[] }> =>
    fetch("/api/datasources/schema", { method: "POST", headers: jsonHeaders, body: JSON.stringify(source) })
      .then(json<{ fields: DataField[] }>),

  renderHtml: (definition: ReportDefinition): Promise<string> =>
    fetch("/api/render?format=html", {
      method: "POST",
      headers: jsonHeaders,
      body: JSON.stringify({ definition, parameters: {} }),
    }).then(async (r) => {
      if (!r.ok) throw new Error(`${r.status} ${await r.text()}`);
      return r.text();
    }),

  renderPdfBlob: (definition: ReportDefinition): Promise<Blob> =>
    fetch("/api/render?format=pdf", {
      method: "POST",
      headers: jsonHeaders,
      body: JSON.stringify({ definition, parameters: {} }),
    }).then(async (r) => {
      if (!r.ok) throw new Error(`${r.status} ${await r.text()}`);
      return r.blob();
    }),
};
