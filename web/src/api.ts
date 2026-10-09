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
import { useAuth, type AuthUser, type CreatedInvite, type PendingInvite, type TeamMember, type TenantInfo } from "./auth";
import { problemMessage } from "./httpError";
import { usePrefs } from "./prefs";

/** Resolves once the person has signed in again after their session expired (true), or signed out instead (false). */
function sessionRestored(): Promise<boolean> {
  return new Promise((resolve) => {
    const stop = useAuth.subscribe((s) => {
      if (!s.sessionExpired) {
        stop();
        resolve(!!s.token);
      }
    });
  });
}

/** Every API call goes through this so the JWT is always attached. A 401 means the token expired or
 * was revoked: the app stays on screen (no unsaved work is lost), a sign-in prompt appears, and the
 * request is sent again once the person has signed back in — so screens never show a raw "401". */
async function fetchWithAuth(input: string, init: RequestInit = {}, retried = false): Promise<Response> {
  const token = useAuth.getState().token;
  const headers = new Headers(init.headers);
  if (token) headers.set("Authorization", `Bearer ${token}`);
  // The API localizes its own messages from this header. It has to be the language chosen in
  // Settings, not the browser's own preference — otherwise a Turkish UI still gets English errors.
  headers.set("Accept-Language", usePrefs.getState().language);
  const res = await fetch(input, { ...init, headers });
  if (res.status !== 401 || !token || retried) return res;

  useAuth.getState().expire();
  return (await sessionRestored()) ? fetchWithAuth(input, init, true) : res;
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

function packageForm(file: File, options: unknown): FormData {
  const form = new FormData();
  form.append("file", file);
  form.append("options", JSON.stringify(options));
  return form;
}

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

  // --- export / import packages ---
  planExport: (request: ExportRequest): Promise<ExportPlan> =>
    fetchWithAuth("/api/transfer/export/plan", { method: "POST", headers: jsonHeaders, body: JSON.stringify(request) }).then(
      json<ExportPlan>,
    ),

  exportPackage: (request: ExportRequest): Promise<{ blob: Blob; fileName: string }> =>
    fetchWithAuth("/api/transfer/export", { method: "POST", headers: jsonHeaders, body: JSON.stringify(request) }).then(
      async (r) => {
        if (!r.ok) throw new Error(problemMessage(await r.text()));
        const disposition = r.headers.get("Content-Disposition") ?? "";
        const name = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition)?.[1];
        return { blob: await r.blob(), fileName: name ? decodeURIComponent(name) : "reports.jrdpkg" };
      },
    ),

  previewImport: (file: File, options: ImportOptions): Promise<ImportPlan> =>
    fetchWithAuth("/api/transfer/import/preview", { method: "POST", body: packageForm(file, options) }).then(json<ImportPlan>),

  importPackage: (file: File, options: ImportOptions): Promise<ImportResult> =>
    fetchWithAuth("/api/transfer/import", { method: "POST", body: packageForm(file, options) }).then(json<ImportResult>),

  // --- version history ---
  listVersions: (id: string): Promise<ReportVersionInfo[]> =>
    fetchWithAuth(`/api/reports/${id}/versions`).then(json<ReportVersionInfo[]>),

  getVersion: (id: string, version: number): Promise<ReportVersionDetail> =>
    fetchWithAuth(`/api/reports/${id}/versions/${version}`).then(json<ReportVersionDetail>),

  /** Saves an old version again as the newest one. Nothing is deleted. */
  restoreVersion: (id: string, version: number): Promise<ReportResponse> =>
    fetchWithAuth(`/api/reports/${id}/versions/${version}/restore`, { method: "POST" }).then(json<ReportResponse>),

  // --- API keys ---
  listApiKeys: (): Promise<ApiKeyInfo[]> => fetchWithAuth("/api/api-keys").then(json<ApiKeyInfo[]>),

  createApiKey: (body: ApiKeyInput): Promise<{ key: ApiKeyInfo; secret: string }> =>
    fetchWithAuth("/api/api-keys", { method: "POST", headers: jsonHeaders, body: JSON.stringify(body) }).then(
      json<{ key: ApiKeyInfo; secret: string }>,
    ),

  updateApiKey: (id: string, body: ApiKeyInput): Promise<ApiKeyInfo> =>
    fetchWithAuth(`/api/api-keys/${id}`, { method: "PUT", headers: jsonHeaders, body: JSON.stringify(body) }).then(
      json<ApiKeyInfo>,
    ),

  setApiKeyActive: (id: string, isActive: boolean): Promise<ApiKeyInfo> =>
    fetchWithAuth(`/api/api-keys/${id}/active`, {
      method: "PUT",
      headers: jsonHeaders,
      body: JSON.stringify({ isActive }),
    }).then(json<ApiKeyInfo>),

  deleteApiKey: (id: string): Promise<void> =>
    fetchWithAuth(`/api/api-keys/${id}`, { method: "DELETE" }).then(async (r) => {
      if (!r.ok && r.status !== 404) throw new Error(problemMessage(await r.text()));
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

  createInvite: (role: string, expiresInHours?: number, email?: string | null): Promise<CreatedInvite> =>
    fetchWithAuth("/api/tenant/invites", {
      method: "POST",
      headers: jsonHeaders,
      body: JSON.stringify({ role, expiresInHours: expiresInHours ?? null, email: email || null }),
    }).then(json<CreatedInvite>),

  renameTenant: (name: string): Promise<TenantInfo> =>
    fetchWithAuth("/api/tenant", { method: "PUT", headers: jsonHeaders, body: JSON.stringify({ name }) }).then(json<TenantInfo>),

  tenantCapabilities: (): Promise<{ emailConfigured: boolean }> =>
    fetchWithAuth("/api/tenant/capabilities").then(json<{ emailConfigured: boolean }>),

  /** A one-time link a Designer passes to a teammate so they can choose a new password. */
  createResetLink: (userId: string): Promise<{ url: string; expiresAtUtc: string }> =>
    fetchWithAuth(`/api/auth/users/${userId}/reset-link`, { method: "POST" }).then(json<{ url: string; expiresAtUtc: string }>),

  updateMe: (displayName: string): Promise<AuthUser> =>
    fetchWithAuth("/api/auth/me", { method: "PUT", headers: jsonHeaders, body: JSON.stringify({ displayName }) }).then(json<AuthUser>),

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

  /** One page of jobs. Filtering, sorting and paging are the server's job — sorting only the
   *  rows that happen to be on screen would claim to sort a history it can't see. */
  listJobs: (opts: JobQuery = {}): Promise<JobPage> => {
    const q = new URLSearchParams();
    for (const s of opts.statuses ?? []) q.append("status", s);
    if (opts.sort) q.set("sort", opts.sort);
    if (opts.desc !== undefined) q.set("desc", String(opts.desc));
    if (opts.skip) q.set("skip", String(opts.skip));
    if (opts.take !== undefined) q.set("take", String(opts.take));
    const qs = q.toString();
    return fetchWithAuth(`/api/jobs${qs ? `?${qs}` : ""}`).then(json<JobPage>);
  },

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

export interface ExportRequest {
  reportIds: string[];
  folderIds: string[];
  stripSampleData: boolean;
}

export interface ExportPlan {
  reports: { id: string; code: string; name: string; role: "selected" | "dependency"; folder: string | null }[];
  folders: string[];
  assets: number;
  connections: { name: string; provider: string }[];
  redactedValues: number;
  /** Tokens "kind|detail", e.g. "subreportMissing|Report name". */
  warnings: string[];
}

export type ImportAction = "create" | "update" | "copy" | "skip";

export interface ImportOptions {
  targetFolderId: string | null;
  decisions: { code: string; action: ImportAction }[];
}

export interface ImportPlanItem {
  code: string;
  name: string;
  role: string;
  folder: string | null;
  status: "new" | "changed" | "identical" | "invalid";
  changes: string[];
  existingName: string | null;
  existingVersion: number | null;
  errors: string[];
  defaultAction: ImportAction;
  allowedActions: ImportAction[];
  action: ImportAction;
}

export interface ImportPlan {
  source: { environment: string | null; exportedAtUtc: string; exportedBy: string | null; appVersion: string | null };
  items: ImportPlanItem[];
  folders: string[];
  assetsNew: number;
  assetsReused: number;
  connections: { name: string; provider: string; status: "found" | "missing" | "providerMismatch" }[];
  redactedValues: number;
  warnings: string[];
}

export interface ImportResult {
  created: number;
  updated: number;
  copies: number;
  skipped: number;
  items: { code: string; name: string; id: string | null; action: ImportAction; version: number | null }[];
  warnings: string[];
}

export interface ReportVersionInfo {
  version: number;
  name: string;
  savedAtUtc: string;
  savedByEmail: string | null;
  /** What the save changed, as tokens: name, code, page, parameters, dataSources, styles, bands, other, added:N, removed:N, edited:N. */
  changes: string[];
  /** Set when the version was made by restoring an older one. */
  restoredFromVersion: number | null;
}

export interface ReportVersionDetail extends ReportVersionInfo {
  definition: ReportDefinition;
}

export type ApiKeyStatus = "active" | "disabled" | "expired";

export interface ApiKeyInfo {
  id: string;
  name: string;
  description: string | null;
  /** First characters of the key, so keys can be told apart. The full key is shown only once, at creation. */
  keyPrefix: string;
  isActive: boolean;
  expiresAtUtc: string | null;
  createdAtUtc: string;
  createdByEmail: string | null;
  lastUsedAtUtc: string | null;
  status: ApiKeyStatus;
}

export interface ApiKeyInput {
  name: string;
  description: string | null;
  /** Null = never expires. */
  expiresAtUtc: string | null;
}

export type JobSortKey = "reportName" | "format" | "status" | "createdAtUtc";

export interface JobQuery {
  statuses?: ReportJobStatus[];
  sort?: JobSortKey;
  desc?: boolean;
  skip?: number;
  take?: number;
}

/** `total` counts every job matching the filter, not just the returned page. */
export interface JobPage {
  items: ReportJob[];
  total: number;
}

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
  /** Set when a schedule created the job; null when someone ran it by hand. */
  scheduleId?: string | null;
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
