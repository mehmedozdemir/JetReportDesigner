import { create } from "zustand";
import { persist } from "zustand/middleware";
import { problemMessage } from "./httpError";
import { useDesigner } from "./store";

export interface AuthUser {
  id: string;
  email: string;
  roles: string[];
  displayName?: string | null;
  lastLoginAtUtc?: string | null;
  lockedOut?: boolean;
}

/** A teammate in the current tenant, as returned by GET /api/auth/users. */
export type TeamMember = AuthUser;

export interface TenantInfo {
  id: string;
  name: string;
  createdAtUtc: string;
}

export interface PendingInvite {
  code: string;
  role: string;
  createdAtUtc: string;
  expiresAtUtc: string;
  link: string;
  email?: string | null;
}

export interface CreatedInvite {
  code: string;
  role: string;
  expiresAtUtc: string;
  link: string;
  email?: string | null;
  emailSent: boolean;
}

export interface InvitePreview {
  organizationName: string;
  role: string;
  email: string | null;
  expiresAtUtc: string;
}

interface AuthResponse {
  token: string;
  expiresAtUtc: string;
  user: AuthUser;
}

/**
 * A failed account request. <see cref="fields"/> carries the server's per-field messages (already in
 * the user's language) so a form can show each one under the input it belongs to.
 */
export class AuthError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly fields: Record<string, string[]> = {},
  ) {
    super(message);
  }

  /** The message for one field, if the server named it. */
  field(name: string): string | undefined {
    return this.fields[name]?.[0] ?? this.fields[name.charAt(0).toUpperCase() + name.slice(1)]?.[0];
  }
}

/** Reads a ProblemDetails body into an AuthError: field errors by name, else the title/detail. */
export async function toAuthError(res: Response): Promise<AuthError> {
  const text = await res.text();
  try {
    const p = JSON.parse(text) as { title?: string; detail?: string; errors?: Record<string, string[] | string> };
    const fields: Record<string, string[]> = {};
    for (const [key, value] of Object.entries(p.errors ?? {})) {
      fields[key] = Array.isArray(value) ? value : [value];
    }
    const general = fields[""]?.[0];
    const message = p.detail ?? general ?? (Object.keys(fields).length ? Object.values(fields)[0][0] : p.title) ?? text;
    return new AuthError(message, res.status, fields);
  } catch {
    return new AuthError(text ? problemMessage(text) : `${res.status}`, res.status);
  }
}

/** The UI language, sent so the server answers in it (read lazily to avoid an import cycle with prefs). */
function language(): string {
  try {
    const raw = localStorage.getItem("jrd.prefs");
    return (raw && (JSON.parse(raw) as { state?: { language?: string } }).state?.language) || navigator.language.slice(0, 2);
  } catch {
    return "en";
  }
}

export async function accountRequest<T>(path: string, method: "GET" | "POST" | "PUT", body?: unknown, token?: string | null): Promise<T> {
  const headers: Record<string, string> = { "Accept-Language": language() };
  if (body !== undefined) headers["Content-Type"] = "application/json";
  if (token) headers.Authorization = `Bearer ${token}`;
  const res = await fetch(path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  if (res.status === 429) {
    throw new AuthError(language() === "tr" ? "Çok fazla deneme yapıldı. Bir dakika sonra tekrar deneyin." : "Too many attempts. Try again in a minute.", 429);
  }
  if (!res.ok) throw await toAuthError(res);
  if (res.status === 202 || res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

/** Exactly one of organizationName / inviteCode. */
export interface RegisterInput {
  email: string;
  password: string;
  displayName: string;
  organizationName?: string;
  inviteCode?: string;
}

interface AuthState {
  token: string | null;
  user: AuthUser | null;
  expiresAtUtc: string | null;
  /** The token stopped working mid-session: the app stays on screen (no work lost) behind a sign-in prompt. */
  sessionExpired: boolean;
  login(email: string, password: string): Promise<void>;
  register(input: RegisterInput): Promise<void>;
  setUser(user: AuthUser): void;
  /** Called when the API answers 401 to a signed-in request. */
  expire(): void;
  logout(): void;
}

export const useAuth = create<AuthState>()(
  persist(
    (set, get) => ({
      token: null,
      user: null,
      expiresAtUtc: null,
      sessionExpired: false,
      login: async (email, password) => {
        const auth = await accountRequest<AuthResponse>("/api/auth/login", "POST", { email, password });
        set({ token: auth.token, user: auth.user, expiresAtUtc: auth.expiresAtUtc, sessionExpired: false });
      },
      register: async (input) => {
        const auth = await accountRequest<AuthResponse>("/api/auth/register", "POST", {
          email: input.email,
          password: input.password,
          displayName: input.displayName || null,
          organizationName: input.organizationName ?? null,
          inviteCode: input.inviteCode ?? null,
        });
        set({ token: auth.token, user: auth.user, expiresAtUtc: auth.expiresAtUtc, sessionExpired: false });
      },
      setUser: (user) => set({ user: { ...get().user, ...user } }),
      expire: () => {
        if (get().token) set({ sessionExpired: true });
      },
      logout: () => {
        set({ token: null, user: null, expiresAtUtc: null, sessionExpired: false });
        // Otherwise the next sign-in (even a different user, same tab) would land back in
        // whatever report was open at logout instead of the Start screen.
        useDesigner.setState({
          report: null,
          reportId: null,
          concurrencyToken: null,
          selectedIds: [],
          selectedBand: null,
          past: [],
          future: [],
          dirty: false,
          savedAtUtc: null,
        });
      },
    }),
    {
      name: "jrd.auth",
      version: 1,
      partialize: (s) => ({ token: s.token, user: s.user, expiresAtUtc: s.expiresAtUtc }),
    },
  ),
);

// A token that already expired while the tab was closed: start at the sign-in page, not behind a prompt.
{
  const restored = useAuth.getState();
  if (restored.token && restored.expiresAtUtc && new Date(restored.expiresAtUtc).getTime() <= Date.now()) {
    restored.logout();
  }
}

export const isDesigner = (user: AuthUser | null): boolean => !!user?.roles.includes("Designer");

/** "Ayşe Yılmaz" → "AY", "ayse@x.com" → "A". */
export function initials(user: Pick<AuthUser, "displayName" | "email"> | null | undefined): string {
  const name = user?.displayName?.trim();
  if (name) {
    const parts = name.split(/\s+/);
    return ((parts[0]?.[0] ?? "") + (parts.length > 1 ? parts[parts.length - 1][0] : "")).toLocaleUpperCase();
  }
  return (user?.email?.[0] ?? "?").toLocaleUpperCase();
}

export const displayNameOf = (user: Pick<AuthUser, "displayName" | "email"> | null | undefined): string =>
  user?.displayName?.trim() || user?.email || "";
