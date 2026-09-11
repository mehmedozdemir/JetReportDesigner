import { useState } from "react";
import { AlertTriangle, FileBarChart2, LogIn, UserPlus } from "lucide-react";
import { useAuth } from "../auth";

/** Full-screen gate shown whenever there is no signed-in user. Login and self-registration
 * both hit the API directly; the very first account ever registered becomes a Designer
 * (bootstrap admin), every account after that starts as a Viewer. */
export function LoginScreen() {
  const [mode, setMode] = useState<"login" | "register">("login");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const busy = useAuth((s) => s.busy);
  const error = useAuth((s) => s.error);
  const login = useAuth((s) => s.login);
  const register = useAuth((s) => s.register);

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    void (mode === "login" ? login(email, password) : register(email, password));
  };

  return (
    <div className="login-screen">
      <form className="login-card" onSubmit={submit}>
        <div className="login-brand">
          <FileBarChart2 size={22} />
          <span>JetReportDesigner</span>
        </div>

        <div className="segmented" role="group" aria-label="Sign in or register">
          <button type="button" className={mode === "login" ? "on" : ""} onClick={() => setMode("login")}>
            <LogIn size={14} /> Sign in
          </button>
          <button type="button" className={mode === "register" ? "on" : ""} onClick={() => setMode("register")}>
            <UserPlus size={14} /> Create account
          </button>
        </div>

        <label className="field">
          <span>Email</span>
          <input
            type="email"
            required
            autoFocus
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder="you@example.com"
          />
        </label>

        <label className="field">
          <span>Password</span>
          <input
            type="password"
            required
            minLength={8}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            placeholder="••••••••"
          />
        </label>

        {mode === "register" && (
          <p className="hint">
            The first account ever created becomes a Designer (full access). Every account after
            that starts as a Viewer — an existing Designer can promote it later.
          </p>
        )}

        {error && (
          <p className="login-error" role="alert">
            <AlertTriangle size={14} /> {error}
          </p>
        )}

        <button className="btn primary" type="submit" disabled={busy}>
          {mode === "login" ? "Sign in" : "Create account"}
        </button>
      </form>
    </div>
  );
}
