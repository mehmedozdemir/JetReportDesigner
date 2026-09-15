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

  removeUser: (id: string): Promise<void> =>
    fetchWithAuth(`/api/auth/users/${id}`, { method: "DELETE" }).then(async (r) => {
      if (!r.ok) throw new Error(problemMessage(await r.text()));
    }),

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

  // --- email account (scheduled-report distribution) ---
  getEmailSettings: (): Promise<SmtpSettingsInfo | null> =>
    fetchWithAuth("/api/email-settings").then(async (r) => {
      if (r.status === 404) return null;
      if (!r.ok) throw new Error(problemMessage(await r.text()));
      return json<SmtpSettingsInfo>(r);
    }),

  setEmailSettings: (settings: SmtpSettingsInput): Promise<SmtpSettingsInfo> =>
    fetchWithAuth("/api/email-settings", { method: "PUT", headers: jsonHeaders, body: JSON.stringify(settings) }).then(
      async (r) => {
        if (!r.ok) throw new Error(problemMessage(await r.text()));
        return json<SmtpSettingsInfo>(r);
      },
    ),

  deleteEmailSettings: (): Promise<void> =>
    fetchWithAuth("/api/email-settings", { method: "DELETE" }).then((r) => {
      if (!r.ok && r.status !== 404) throw new Error(`${r.status} ${r.statusText}`);
    }),

  /** Pass `draft` to test the in-progress form before saving it — a blank/omitted password
   * falls back to the already-saved one, same as Save's own convention. Omit `draft` to test
   * the already-saved account (fails if there isn't one). */
  sendTestEmail: (toEmail: string, draft?: SmtpSettingsInput): Promise<void> =>
    fetchWithAuth("/api/email-settings/test", {
      method: "POST",
      headers: jsonHeaders,
      body: JSON.stringify({ toEmail, ...draft }),
    }).then(async (r) => {
      if (!r.ok) throw new Error(problemMessage(await r.text()));
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

  /** Preview/export a saved report by id — no need to open it in the designer first. */
  previewSavedHtml: (id: string): Promise<string> =>
    fetchWithAuth(`/api/reports/${id}/preview`, { method: "POST", headers: jsonHeaders, body: "{}" }).then(
      async (r) => {
        if (!r.ok) throw new Error(problemMessage(await r.text()));
        return r.text();
      },
    ),

  exportSavedBlob: (id: string, format: "pdf" | "xlsx"): Promise<Blob> =>
    fetchWithAuth(`/api/reports/${id}/render?format=${format}`, { method: "POST", headers: jsonHeaders, body: "{}" }).then(
      async (r) => {
        if (!r.ok) throw new Error(problemMessage(await r.text()));
        return r.blob();
      },
    ),

  // --- sharing ---
  listShares: (reportId: string): Promise<ShareInfo[]> =>
    fetchWithAuth(`/api/reports/${reportId}/shares`).then(json<ShareInfo[]>),

  createShare: (reportId: string): Promise<ShareInfo> =>
    fetchWithAuth(`/api/reports/${reportId}/shares`, { method: "POST" }).then(async (r) => {
      if (!r.ok) throw new Error(problemMessage(await r.text()));
      return json<ShareInfo>(r);
    }),

  revokeShare: (reportId: string, token: string): Promise<void> =>
    fetchWithAuth(`/api/reports/${reportId}/shares/${token}`, { method: "DELETE" }).then((r) => {
      if (!r.ok && r.status !== 404) throw new Error(`${r.status} ${r.statusText}`);
    }),

  // --- background report jobs ("export this without making me wait") ---
  enqueueReportJob: (reportId: string, format: "pdf" | "xlsx"): Promise<ReportJob> =>
    fetchWithAuth(`/api/reports/${reportId}/jobs`, {
      method: "POST",
      headers: jsonHeaders,
      body: JSON.stringify({ format }),
    }).then(async (r) => {
      if (!r.ok) throw new Error(problemMessage(await r.text()));
      return json<ReportJob>(r);
    }),

  listJobs: (): Promise<ReportJob[]> => fetchWithAuth("/api/jobs").then(json<ReportJob[]>),

  getJob: (id: string): Promise<ReportJob> => fetchWithAuth(`/api/jobs/${id}`).then(json<ReportJob>),

  cancelJob: (id: string): Promise<void> =>
    fetchWithAuth(`/api/jobs/${id}/cancel`, { method: "POST" }).then(async (r) => {
      if (!r.ok) throw new Error(problemMessage(await r.text()));
    }),

  downloadJobBlob: (id: string): Promise<Blob> =>
    fetchWithAuth(`/api/jobs/${id}/download`).then(async (r) => {
      if (!r.ok) throw new Error(problemMessage(await r.text()));
      return r.blob();
    }),

  // --- report schedules ---
  createSchedule: (reportId: string, fields: ReportScheduleInput): Promise<ReportSchedule> =>
    fetchWithAuth(`/api/reports/${reportId}/schedules`, {
      method: "POST",
      headers: jsonHeaders,
      body: JSON.stringify(fields),
    }).then(async (r) => {
      if (!r.ok) throw new Error(problemMessage(await r.text()));
      return json<ReportSchedule>(r);
    }),

  listSchedules: (): Promise<ReportSchedule[]> => fetchWithAuth("/api/schedules").then(json<ReportSchedule[]>),

  updateSchedule: (id: string, fields: ReportScheduleInput): Promise<ReportSchedule> =>
    fetchWithAuth(`/api/schedules/${id}`, { method: "PUT", headers: jsonHeaders, body: JSON.stringify(fields) }).then(
      async (r) => {
        if (!r.ok) throw new Error(problemMessage(await r.text()));
        return json<ReportSchedule>(r);
      },
    ),

  deleteSchedule: (id: string): Promise<void> =>
    fetchWithAuth(`/api/schedules/${id}`, { method: "DELETE" }).then((r) => {
      if (!r.ok && r.status !== 404) throw new Error(`${r.status} ${r.statusText}`);
    }),
};

export interface ShareInfo {
  token: string;
  createdAtUtc: string;
  createdByEmail?: string | null;
}

export type ReportJobStatus = "Queued" | "Running" | "Succeeded" | "Failed" | "Cancelled";

export interface ReportJob {
  id: string;
  reportId: string;
  reportName: string;
  format: "pdf" | "xlsx";
  status: ReportJobStatus;
  errorMessage?: string | null;
  createdAtUtc: string;
  startedAtUtc?: string | null;
  completedAtUtc?: string | null;
}

export type ScheduleFrequency = "Daily" | "Weekly" | "Monthly";

export interface ReportScheduleInput {
  format: "pdf" | "xlsx";
  frequency: ScheduleFrequency;
  minuteOfDayUtc: number;
  dayOfWeek?: number | null;
  dayOfMonth?: number | null;
  enabled: boolean;
  createShareLink: boolean;
  emailRecipients?: string | null;
}

export interface ReportSchedule {
  id: string;
  reportId: string;
  reportName: string;
  format: "pdf" | "xlsx";
  frequency: ScheduleFrequency;
  minuteOfDayUtc: number;
  dayOfWeek?: number | null;
  dayOfMonth?: number | null;
  enabled: boolean;
  createShareLink: boolean;
  emailRecipients?: string | null;
  createdAtUtc: string;
  nextRunAtUtc: string;
  lastRunAtUtc?: string | null;
  lastJobId?: string | null;
  lastDistributionAtUtc?: string | null;
  lastDistributionError?: string | null;
}

export type SmtpSecurity = "None" | "StartTls" | "SslOnConnect";

export interface SmtpSettingsInfo {
  host: string;
  port: number;
  security: SmtpSecurity;
  username: string;
  fromEmail: string;
  fromName?: string | null;
  hasPassword: boolean;
  updatedAtUtc: string;
}

export interface SmtpSettingsInput {
  host: string;
  port: number;
  security: SmtpSecurity;
  username: string;
  /** Omit or leave blank to keep the previously saved password. */
  password?: string;
  fromEmail: string;
  fromName?: string | null;
}

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
