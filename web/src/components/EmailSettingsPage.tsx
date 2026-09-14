import { useEffect, useState } from "react";
import { AlertTriangle, CheckCircle2, Loader2, Mail, Send, Trash2 } from "lucide-react";
import { api, type SmtpSecurity, type SmtpSettingsInfo } from "../api";

const msg = (e: unknown) => (e instanceof Error ? e.message : String(e));

type Preset = "exchange" | "gmail" | "custom";

const PRESETS: Record<Exclude<Preset, "custom">, { host: string; port: number; security: SmtpSecurity }> = {
  exchange: { host: "smtp.office365.com", port: 587, security: "StartTls" },
  gmail: { host: "smtp.gmail.com", port: 587, security: "StartTls" },
};

/** Tenant's outgoing-mail account — a page reached from the Start screen (like Team), since
 * it's an organization-wide setting, not something tied to whatever report you have open. */
export function EmailSettingsPage() {
  const [loaded, setLoaded] = useState(false);
  const [existing, setExisting] = useState<SmtpSettingsInfo | null>(null);
  const [preset, setPreset] = useState<Preset>("custom");
  const [host, setHost] = useState("");
  const [port, setPort] = useState(587);
  const [security, setSecurity] = useState<SmtpSecurity>("StartTls");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [fromEmail, setFromEmail] = useState("");
  const [fromName, setFromName] = useState("");
  const [saving, setSaving] = useState(false);
  const [err, setErr] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  const [testTo, setTestTo] = useState("");
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState<"ok" | string | null>(null);

  const refresh = () =>
    api
      .getEmailSettings()
      .then((s) => {
        setExisting(s);
        if (s) {
          setHost(s.host);
          setPort(s.port);
          setSecurity(s.security);
          setUsername(s.username);
          setFromEmail(s.fromEmail);
          setFromName(s.fromName ?? "");
        }
      })
      .finally(() => setLoaded(true));

  useEffect(() => {
    refresh().catch((e) => setErr(msg(e)));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const applyPreset = (p: Preset) => {
    setPreset(p);
    if (p !== "custom") {
      setHost(PRESETS[p].host);
      setPort(PRESETS[p].port);
      setSecurity(PRESETS[p].security);
    }
  };

  const save = async () => {
    setSaving(true);
    setErr(null);
    setSaved(false);
    try {
      await api.setEmailSettings({
        host: host.trim(),
        port,
        security,
        username: username.trim(),
        password: password || undefined,
        fromEmail: fromEmail.trim(),
        fromName: fromName.trim() || null,
      });
      setPassword("");
      setSaved(true);
      await refresh();
    } catch (e) {
      setErr(msg(e));
    } finally {
      setSaving(false);
    }
  };

  const remove = async () => {
    setErr(null);
    try {
      await api.deleteEmailSettings();
      setExisting(null);
      setHost("");
      setPort(587);
      setSecurity("StartTls");
      setUsername("");
      setFromEmail("");
      setFromName("");
    } catch (e) {
      setErr(msg(e));
    }
  };

  const sendTest = async () => {
    setTesting(true);
    setTestResult(null);
    try {
      // Test whatever's currently in the form — no need to Save first and find out it was
      // wrong. A blank password here still falls back to the saved one (see api.ts).
      await api.sendTestEmail(testTo.trim(), {
        host: host.trim(),
        port,
        security,
        username: username.trim(),
        password: password || undefined,
        fromEmail: fromEmail.trim(),
        fromName: fromName.trim() || null,
      });
      setTestResult("ok");
    } catch (e) {
      setTestResult(msg(e));
    } finally {
      setTesting(false);
    }
  };

  if (!loaded) {
    return (
      <section className="start-section">
        <div className="share-loading">
          <Loader2 size={16} className="spin" />
        </div>
      </section>
    );
  }

  return (
    <>
      <section className="start-section">
        <h3>Mail account</h3>
        <p className="hint">
          Used to send scheduled reports by email. Any standard SMTP account works — Exchange/Office 365, Gmail
          (with an app password), or your own mail server.
        </p>

        <div className="settings-row">
          <span>Provider</span>
          <div className="settings-control">
            <div className="segmented" role="group" aria-label="Mail provider preset">
              <button className={preset === "exchange" ? "on" : ""} onClick={() => applyPreset("exchange")}>
                Exchange
              </button>
              <button className={preset === "gmail" ? "on" : ""} onClick={() => applyPreset("gmail")}>
                Gmail
              </button>
              <button className={preset === "custom" ? "on" : ""} onClick={() => applyPreset("custom")}>
                Custom
              </button>
            </div>
          </div>
        </div>

        <div className="settings-row">
          <span>Host</span>
          <div className="settings-control">
            <input value={host} placeholder="smtp.example.com" onChange={(e) => setHost(e.target.value)} />
          </div>
        </div>

        <div className="settings-row">
          <span>Port</span>
          <div className="settings-control">
            <input
              type="number"
              className="mini-num"
              style={{ width: 72 }}
              value={port}
              onChange={(e) => setPort(Number(e.target.value))}
            />
            <select value={security} onChange={(e) => setSecurity(e.target.value as SmtpSecurity)}>
              <option value="StartTls">STARTTLS</option>
              <option value="SslOnConnect">SSL/TLS</option>
              <option value="None">None</option>
            </select>
          </div>
        </div>

        <div className="settings-row">
          <span>Username</span>
          <div className="settings-control">
            <input value={username} placeholder="you@example.com" onChange={(e) => setUsername(e.target.value)} />
          </div>
        </div>

        <div className="settings-row">
          <span>Password</span>
          <div className="settings-control">
            <input
              type="password"
              value={password}
              placeholder={existing?.hasPassword ? "Unchanged — leave blank to keep it" : "Password or app password"}
              onChange={(e) => setPassword(e.target.value)}
            />
          </div>
        </div>

        <div className="settings-row">
          <span>From address</span>
          <div className="settings-control">
            <input value={fromEmail} placeholder="reports@example.com" onChange={(e) => setFromEmail(e.target.value)} />
          </div>
        </div>

        <div className="settings-row">
          <span>From name</span>
          <div className="settings-control">
            <input value={fromName} placeholder="Optional" onChange={(e) => setFromName(e.target.value)} />
          </div>
        </div>

        <div className="settings-row">
          <span />
          <div className="settings-control">
            <button className="btn primary" onClick={() => void save()} disabled={saving}>
              {saving ? "Saving…" : "Save"}
            </button>
            {saved && <CheckCircle2 size={16} style={{ color: "var(--success)" }} />}
            {existing && (
              <button className="mini danger" onClick={() => void remove()}>
                <Trash2 size={13} /> Remove
              </button>
            )}
          </div>
        </div>
      </section>

      {(existing || host.trim()) && (
        <section className="start-section">
          <h3>Send a test email</h3>
          <p className="hint">Tests whatever's in the form above — no need to save first.</p>
          <div className="settings-row">
            <span>To</span>
            <div className="settings-control">
              <input value={testTo} placeholder="you@example.com" onChange={(e) => setTestTo(e.target.value)} />
              <button className="mini" onClick={() => void sendTest()} disabled={testing || !testTo.trim() || !host.trim()}>
                <Send size={13} /> {testing ? "Sending…" : "Send test"}
              </button>
            </div>
          </div>
          {testResult === "ok" && (
            <div className="settings-row">
              <span style={{ color: "var(--success)", display: "flex", alignItems: "center", gap: 6 }}>
                <CheckCircle2 size={14} /> Sent — check the inbox.
              </span>
            </div>
          )}
          {testResult && testResult !== "ok" && (
            <div className="error small">
              <AlertTriangle /> <span>{testResult}</span>
            </div>
          )}
        </section>
      )}

      {err && (
        <section className="start-section">
          <div className="error small">
            <AlertTriangle /> <span>{err}</span>
          </div>
        </section>
      )}

      {!existing && (
        <section className="start-section">
          <div className="start-empty">
            <Mail />
            <div>No mail account configured yet.</div>
            <p>Fill in the form above and save to enable emailing scheduled reports.</p>
          </div>
        </section>
      )}
    </>
  );
}
