import { create } from "zustand";
import { persist } from "zustand/middleware";
import { problemMessage } from "./httpError";

export interface AuthUser {
  id: string;
  email: string;
  roles: string[];
}

interface AuthResponse {
  token: string;
  expiresAtUtc: string;
  user: AuthUser;
}

interface AuthState {
  token: string | null;
  user: AuthUser | null;
  busy: boolean;
  error: string | null;
  login(email: string, password: string): Promise<void>;
  register(email: string, password: string): Promise<void>;
  logout(): void;
}

async function post(path: string, email: string, password: string): Promise<AuthResponse> {
  const res = await fetch(path, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password }),
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
          const auth = await post("/api/auth/login", email, password);
          set({ token: auth.token, user: auth.user, busy: false });
        } catch (e) {
          set({ busy: false, error: String(e instanceof Error ? e.message : e) });
          throw e;
        }
      },
      register: async (email, password) => {
        set({ busy: true, error: null });
        try {
          const auth = await post("/api/auth/register", email, password);
          set({ token: auth.token, user: auth.user, busy: false });
        } catch (e) {
          set({ busy: false, error: String(e instanceof Error ? e.message : e) });
          throw e;
        }
      },
      logout: () => set({ token: null, user: null, error: null }),
    }),
    { name: "jrd.auth", version: 1, partialize: (s) => ({ token: s.token, user: s.user }) },
  ),
);

export const isDesigner = (user: AuthUser | null): boolean => !!user?.roles.includes("Designer");
