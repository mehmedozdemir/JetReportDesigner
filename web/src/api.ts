import type {
  AssetResponse,
  ConnectionResponse,
  DataField,
  DataSourceDefinition,
  FolderSummary,
  ReportDefinition,
  ReportIssue,
  ReportResponse,
  ReportSummary,
  SqlQueryResponse,
} from "./types";
import { useAuth, type PendingInvite, type TeamMember, type TenantInfo } from "./auth";
import { problemMessage } from "./httpError";

/** Every API call goes through this so the JWT is always attached; a 401 means the
 * token is missing/expired/revoked, so it signs the user out back to the login screen. */
function fetchWithAuth(input: string, init: RequestInit = {}): Promise<Response> {
  const token = useAuth.getState().token;
  const headers = new Headers(init.headers);
  if (token) headers.set("Authorization", `Bearer ${token}`);
  return fetch(input, { ...init, headers }).then((res) => {
    if (res.status === 401) useAuth.getState().logout();
    return res;
  });
}

async function json<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const body = await response.text();
    throw new Error(body ? problemMessage(body) : `${response.status} ${response.statusText}`);
  }
  return (await response.json()) as T;
}

const jsonHeaders = { "Content-Type": "application/json" };

export type ParamValues = Record<string, unknown>;

export const api = {
  listReports: (): Promise<ReportSummary[]> => fetchWithAuth("/api/reports").then(json<ReportSummary[]>),

  listSamples: (): Promise<{ name: string; category: string; definition: ReportDefinition }[]> =>
    fetchWithAuth("/api/meta/samples").then(json<{ name: string; category: string; definition: ReportDefinition }[]>),

  getReport: (id: string): Promise<ReportResponse> =>
    fetchWithAuth(`/api/reports/${id}`).then(json<ReportResponse>),

  createReport: (definition: ReportDefinition): Promise<ReportResponse> =>
    fetchWithAuth("/api/reports", { method: "POST", headers: jsonHeaders, body: JSON.stringify(definition) })
      .then(json<ReportResponse>),

  updateReport: (id: string, definition: ReportDefinition, token?: string): Promise<ReportResponse> =>
    fetchWithAuth(`/api/reports/${id}`, {
      method: "PUT",
      headers: { ...jsonHeaders, ...(token ? { "If-Match": `"${token}"` } : {}) },
      body: JSON.stringify(definition),
    }).then(json<ReportResponse>),

  deleteReport: (id: string): Promise<void> =>
    fetchWithAuth(`/api/reports/${id}`, { method: "DELETE" }).then((r) => {
      if (!r.ok && r.status !== 404) throw new Error(`${r.status} ${r.statusText}`);
    }),

  setReportFolder: (id: string, folderId: string | null): Promise<void> =>
    fetchWithAuth(`/api/reports/${id}/folder`, {
      method: "PUT",
      headers: jsonHeaders,
      body: JSON.stringify({ folderId }),
    }).then((r) => {
      if (!r.ok) throw new Error(`${r.status} ${r.statusText}`);
    }),

  // --- folders ---
  listFolders: (): Promise<FolderSummary[]> => fetchWithAuth("/api/folders").then(json<FolderSummary[]>),

  createFolder: (name: string, parentFolderId: string | null): Promise<FolderSummary> =>
    fetchWithAuth("/api/folders", {
      method: "POST",
      headers: jsonHeaders,
      body: JSON.stringify({ name, parentFolderId }),
    }).then(json<FolderSummary>),

  renameFolder: (id: string, name: string): Promise<FolderSummary> =>
    fetchWithAuth(`/api/folders/${id}`, { method: "PUT", headers: jsonHeaders, body: JSON.stringify({ name }) }).then(
      json<FolderSummary>,
    ),

  deleteFolder: (id: string): Promise<void> =>
    fetchWithAuth(`/api/folders/${id}`, { method: "DELETE" }).then(async (r) => {
      if (!r.ok) throw new Error(problemMessage(await r.text()));
    }),

  moveFolder: (id: string, parentFolderId: string | null): Promise<void> =>
    fetchWithAuth(`/api/folders/${id}/move`, {
      method: "PUT",
      headers: jsonHeaders,
      body: JSON.stringify({ parentFolderId }),
    }).then(async (r) => {
      if (!r.ok) throw new Error(problemMessage(await r.text()));
    }),

  // --- data sources ---
  validate: (definition: ReportDefinition): Promise<ReportIssue[]> =>
    fetchWithAuth("/api/reports/validate", { method: "POST", headers: jsonHeaders, body: JSON.stringify(definition) })
      .then(json<ReportIssue[]>),

  schema: (source: DataSourceDefinition): Promise<{ fields: DataField[] }> =>
    fetchWithAuth("/api/datasources/schema", { method: "POST", headers: jsonHeaders, body: JSON.stringify(source) })
      .then(json<{ fields: DataField[] }>),

  previewSource: (
    source: DataSourceDefinition,
    take = 20,
  ): Promise<{ fields: DataField[]; rows: Record<string, unknown>[] }> =>
    fetchWithAuth(`/api/datasources/preview?take=${take}`, {
      method: "POST",
      headers: jsonHeaders,
      body: JSON.stringify(source),
    }).then(json<{ fields: DataField[]; rows: Record<string, unknown>[] }>),

  // --- connections ---
  listConnections: (): Promise<ConnectionResponse[]> =>
    fetchWithAuth("/api/connections").then(json<ConnectionResponse[]>),

  createConnection: (name: string, provider: string, connectionString: string): Promise<ConnectionResponse> =>
    fetchWithAuth("/api/connections", {
      method: "POST",
      headers: jsonHeaders,
      body: JSON.stringify({ name, provider, connectionString }),
    }).then(json<ConnectionResponse>),

  updateConnection: (
    id: string,
    name: string,
    provider: string,
    connectionString: string | null,
  ): Promise<ConnectionResponse> =>
    fetchWithAuth(`/api/connections/${id}`, {
      method: "PUT",
      headers: jsonHeaders,
      body: JSON.stringify({ name, provider, connectionString }),
    }).then(json<ConnectionResponse>),

  deleteConnection: (id: string): Promise<void> =>
    fetchWithAuth(`/api/connections/${id}`, { method: "DELETE" }).then((r) => {
      if (!r.ok && r.status !== 404) throw new Error(`${r.status} ${r.statusText}`);
    }),

  // --- saved SQL queries ---
  listSqlQueries: (connectionId: string): Promise<SqlQueryResponse[]> =>
    fetchWithAuth(`/api/sqlqueries?connectionId=${connectionId}`).then(json<SqlQueryResponse[]>),

  createSqlQuery: (connectionId: string, name: string, commandText: string): Promise<SqlQueryResponse> =>
    fetchWithAuth("/api/sqlqueries", {
      method: "POST",
      headers: jsonHeaders,
      body: JSON.stringify({ connectionId, name, commandText }),
    }).then(json<SqlQueryResponse>),

  updateSqlQuery: (id: string, name: string, commandText: string): Promise<SqlQueryResponse> =>
    fetchWithAuth(`/api/sqlqueries/${id}`, {
      method: "PUT",
      headers: jsonHeaders,
      body: JSON.stringify({ name, commandText }),
    }).then(json<SqlQueryResponse>),

  deleteSqlQuery: (id: string): Promise<void> =>
    fetchWithAuth(`/api/sqlqueries/${id}`, { method: "DELETE" }).then((r) => {
      if (!r.ok && r.status !== 404) throw new Error(`${r.status} ${r.statusText}`);
    }),

  // --- tenant / team ---
  getTenant: (): Promise<TenantInfo> => fetchWithAuth("/api/tenant").then(json<TenantInfo>),

  listTeam: (): Promise<TeamMember[]> => fetchWithAuth("/api/auth/users").then(json<TeamMember[]>),

  setUserRole: (id: string, role: string): Promise<TeamMember> =>
    fetchWithAuth(`/api/auth/users/${id}/role`, {
      method: "PUT",
      headers: jsonHeaders,
      body: JSON.stringify({ role }),
    }).then(json<TeamMember>),

  createInvite: (role: string, expiresInHours?: number): Promise<PendingInvite> =>
    fetchWithAuth("/api/tenant/invites", {
      method: "POST",
      headers: jsonHeaders,
      body: JSON.stringify({ role, expiresInHours: expiresInHours ?? null }),
    }).then(json<PendingInvite>),

  listInvites: (): Promise<PendingInvite[]> => fetchWithAuth("/api/tenant/invites").then(json<PendingInvite[]>),

  revokeInvite: (code: string): Promise<void> =>
    fetchWithAuth(`/api/tenant/invites/${code}`, { method: "DELETE" }).then((r) => {
      if (!r.ok && r.status !== 404) throw new Error(`${r.status} ${r.statusText}`);
    }),

  // --- assets ---
  listAssets: (): Promise<AssetResponse[]> => fetchWithAuth("/api/assets").then(json<AssetResponse[]>),

  uploadAsset: (file: File): Promise<AssetResponse> => {
    const form = new FormData();
    form.append("file", file);
    return fetchWithAuth("/api/assets", { method: "POST", body: form }).then(json<AssetResponse>);
  },

  // --- render ---
  renderHtml: (definition: ReportDefinition, parameters: ParamValues = {}): Promise<string> =>
    fetchWithAuth("/api/render?format=html", {
      method: "POST",
      headers: jsonHeaders,
      body: JSON.stringify({ definition, parameters }),
    }).then(async (r) => {
      if (!r.ok) throw new Error(problemMessage(await r.text()));
      return r.text();
    }),

  renderPdfBlob: (definition: ReportDefinition, parameters: ParamValues = {}): Promise<Blob> =>
    renderBlob("pdf", definition, parameters),

  renderXlsxBlob: (definition: ReportDefinition, parameters: ParamValues = {}): Promise<Blob> =>
    renderBlob("xlsx", definition, parameters),
};

function renderBlob(
  format: "pdf" | "xlsx",
  definition: ReportDefinition,
  parameters: ParamValues,
): Promise<Blob> {
  return fetchWithAuth(`/api/render?format=${format}`, {
    method: "POST",
    headers: jsonHeaders,
    body: JSON.stringify({ definition, parameters }),
  }).then(async (r) => {
    if (!r.ok) throw new Error(problemMessage(await r.text()));
    return r.blob();
  });
}
