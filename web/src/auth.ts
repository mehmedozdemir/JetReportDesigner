import { create } from "zustand";
import { persist } from "zustand/middleware";
import { problemMessage } from "./httpError";
import { useDesigner } from "./store";

export interface AuthUser {
  id: string;
  email: string;
  roles: string[];
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
}

interface AuthResponse {
  token: string;
  expiresAtUtc: string;
  user: AuthUser;
}

/** Exactly one of the two must be set — create a brand-new organization, or join one via a
 * Designer's invite code. */
export interface RegisterJoin {
  organizationName?: string;
  inviteCode?: string;
}

interface AuthState {
  token: string | null;
  user: AuthUser | null;
  busy: boolean;
  error: string | null;
  login(email: string, password: string): Promise<void>;
  register(email: string, password: string, join: RegisterJoin): Promise<void>;
  logout(): void;
}

async function post(path: string, body: Record<string, unknown>): Promise<AuthResponse> {
  const res = await fetch(path, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    throw new Error(problemMessage(await res.text()));
  }
  return (await res.json()) as AuthResponse;
}

export const useAuth = create<AuthState>()(
  persist(
    (set) => ({
      token: null,
      user: null,
      busy: false,
      error: null,
      login: async (email, password) => {
        set({ busy: true, error: null });
        try {
          const auth = await post("/api/auth/login", { email, password });
          set({ token: auth.token, user: auth.user, busy: false });
        } catch (e) {
          set({ busy: false, error: String(e instanceof Error ? e.message : e) });
          throw e;
        }
      },
      register: async (email, password, join) => {
        set({ busy: true, error: null });
        try {
          const auth = await post("/api/auth/register", {
            email,
            password,
            organizationName: join.organizationName ?? null,
            inviteCode: join.inviteCode ?? null,
          });
          set({ token: auth.token, user: auth.user, busy: false });
        } catch (e) {
          set({ busy: false, error: String(e instanceof Error ? e.message : e) });
          throw e;
        }
      },
      logout: () => {
        set({ token: null, user: null, error: null });
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
    { name: "jrd.auth", version: 1, partialize: (s) => ({ token: s.token, user: s.user }) },
  ),
);

export const isDesigner = (user: AuthUser | null): boolean => !!user?.roles.includes("Designer");
