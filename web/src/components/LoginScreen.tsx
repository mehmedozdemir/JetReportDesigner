import { useState } from "react";
import { AlertTriangle, Building2, FileBarChart2, LogIn, Ticket, UserPlus } from "lucide-react";
import { useAuth } from "../auth";

type Mode = "login" | "register";
type JoinMode = "org" | "invite";

/** Full-screen gate shown whenever there is no signed-in user. Registering either starts a
 * brand-new organization (the registering user becomes its Designer) or joins an existing one
 * via a Designer's invite code (with whatever role the invite carries). */
export function LoginScreen() {
  const [mode, setMode] = useState<Mode>("login");
  const [joinMode, setJoinMode] = useState<JoinMode>("org");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [organizationName, setOrganizationName] = useState("");
  const [inviteCode, setInviteCode] = useState("");
  const busy = useAuth((s) => s.busy);
  const error = useAuth((s) => s.error);
  const login = useAuth((s) => s.login);
  const register = useAuth((s) => s.register);

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    if (mode === "login") {
      void login(email, password);
      return;
    }
    void register(
      email,
      password,
      joinMode === "org" ? { organizationName } : { inviteCode: inviteCode.trim() },
    );
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

        {mode === "register" && (
          <div className="segmented" role="group" aria-label="New organization or join with invite">
            <button type="button" className={joinMode === "org" ? "on" : ""} onClick={() => setJoinMode("org")}>
              <Building2 size={14} /> New organization
            </button>
            <button type="button" className={joinMode === "invite" ? "on" : ""} onClick={() => setJoinMode("invite")}>
              <Ticket size={14} /> Join with invite
            </button>
          </div>
        )}

        {mode === "register" && joinMode === "org" && (
          <label className="field">
            <span>Organization name</span>
            <input
              required
              value={organizationName}
              onChange={(e) => setOrganizationName(e.target.value)}
              placeholder="Acme Corp"
            />
          </label>
        )}

        {mode === "register" && joinMode === "invite" && (
          <label className="field">
            <span>Invite code</span>
            <input
              required
              value={inviteCode}
              onChange={(e) => setInviteCode(e.target.value.toUpperCase())}
              placeholder="e.g. VQZTW4U5"
              style={{ textTransform: "uppercase" }}
            />
          </label>
        )}

        <label className="field">
          <span>Email</span>
          <input
            type="email"
            required
            autoFocus={mode === "login"}
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
            {joinMode === "org"
              ? "You'll be the Designer (full access) of this new organization."
              : "Your role (Designer or Viewer) is set by whoever gave you this code."}
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
