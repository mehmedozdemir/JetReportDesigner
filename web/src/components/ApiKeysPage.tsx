import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  AlertTriangle,
  Ban,
  Check,
  CheckCircle2,
  Clock3,
  Copy,
  KeyRound,
  Loader2,
  Pencil,
  Play,
  Plus,
  Trash2,
  X,
} from "lucide-react";
import { api, type ApiKeyInfo, type ApiKeyStatus } from "../api";
import { timeAgo } from "../time";
import { useEscapeKey } from "../useEscapeKey";
import { useFocusTrap } from "../useFocusTrap";
import { ConfirmButton } from "./ConfirmButton";
import { PageHeader } from "./PageHeader";
import { SortableTh, useSort } from "./SortableTh";

const msg = (e: unknown) => (e instanceof Error ? e.message : String(e));
const DAY_MS = 86_400_000;

/** Whole days from now until `iso` (rounded up), so "1 day left" never reads as "0". */
const daysLeft = (iso: string) => Math.ceil((new Date(iso).getTime() - Date.now()) / DAY_MS);

/** A local calendar date (yyyy-mm-dd) as the end of that day, in UTC for the server. */
const endOfLocalDayUtc = (ymd: string) => {
  const [y, m, d] = ymd.split("-").map(Number);
  return new Date(y, m - 1, d, 23, 59, 59).toISOString();
};

const toYmd = (date: Date) =>
  `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;

type ExpiryMode = "never" | "30" | "90" | "365" | "custom";

function StatusBadge({ status }: { status: ApiKeyStatus }) {
  const { t } = useTranslation();
  const Icon = status === "active" ? CheckCircle2 : status === "disabled" ? Ban : Clock3;
  return (
    <span className={`key-status key-status-${status}`}>
      <Icon size={12} /> {t(`apiKeys.status.${status}`)}
    </span>
  );
}

/** Create / edit form. Creating returns the secret to the caller; editing only changes name, note and expiry. */
function KeyDialog({
  existing,
  onClose,
  onSaved,
}: {
  existing: ApiKeyInfo | null;
  onClose: () => void;
  onSaved: (key: ApiKeyInfo, secret?: string) => void;
}) {
  const { t } = useTranslation();
  const dialogRef = useFocusTrap<HTMLDivElement>();
  useEscapeKey(onClose);

  const [name, setName] = useState(existing?.name ?? "");
  const [description, setDescription] = useState(existing?.description ?? "");
  const [mode, setMode] = useState<ExpiryMode>(existing?.expiresAtUtc ? "custom" : "never");
  const [date, setDate] = useState(existing?.expiresAtUtc ? toYmd(new Date(existing.expiresAtUtc)) : "");
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  const tomorrow = toYmd(new Date(Date.now() + DAY_MS));

  const expiresAtUtc = useMemo(() => {
    if (mode === "never") return null;
    if (mode === "custom") return date ? endOfLocalDayUtc(date) : undefined;
    return endOfLocalDayUtc(toYmd(new Date(Date.now() + Number(mode) * DAY_MS)));
  }, [mode, date]);

  const valid = name.trim().length > 0 && expiresAtUtc !== undefined;

  const submit = async () => {
    if (!valid || expiresAtUtc === undefined) return;
    setBusy(true);
    setErr(null);
    try {
      const body = { name: name.trim(), description: description.trim() || null, expiresAtUtc };
      if (existing) {
        onSaved(await api.updateApiKey(existing.id, body));
      } else {
        const created = await api.createApiKey(body);
        onSaved(created.key, created.secret);
      }
    } catch (e) {
      setErr(msg(e));
      setBusy(false);
    }
  };

  const options: [ExpiryMode, string][] = [
    ["never", t("apiKeys.expiry.never")],
    ["30", t("apiKeys.expiry.days30")],
    ["90", t("apiKeys.expiry.days90")],
    ["365", t("apiKeys.expiry.year1")],
    ["custom", t("apiKeys.expiry.custom")],
  ];

  return (
    <div className="modal-backdrop" onMouseDown={onClose}>
      <div
        ref={dialogRef}
        className="modal key-dialog"
        role="dialog"
        aria-modal="true"
        aria-label={existing ? t("apiKeys.dialog.editTitle") : t("apiKeys.dialog.createTitle")}
        onMouseDown={(e) => e.stopPropagation()}
      >
        <header>
          <h2>
            <KeyRound /> {existing ? t("apiKeys.dialog.editTitle") : t("apiKeys.dialog.createTitle")}
          </h2>
          <button className="mini ghost" onClick={onClose} aria-label={t("common.close")}>
            <X />
          </button>
        </header>

        <form
          className="key-dialog-body"
          onSubmit={(e) => {
            e.preventDefault();
            void submit();
          }}
        >
          <label className="field">
            <span>{t("apiKeys.dialog.name")}</span>
            <input
              autoFocus
              value={name}
              maxLength={200}
              placeholder={t("apiKeys.dialog.namePlaceholder")}
              onChange={(e) => setName(e.target.value)}
            />
          </label>

          <label className="field">
            <span>{t("apiKeys.dialog.description")}</span>
            <textarea
              className="key-notes"
              rows={2}
              value={description}
              maxLength={1000}
              onChange={(e) => setDescription(e.target.value)}
            />
          </label>

          <div className="field">
            <span>{t("apiKeys.dialog.validity")}</span>
            <div className="segmented key-expiry" role="group" aria-label={t("apiKeys.dialog.validity")}>
              {options.map(([value, label]) => (
                <button type="button" key={value} className={mode === value ? "on" : ""} onClick={() => setMode(value)}>
                  {label}
                </button>
              ))}
            </div>
            {mode === "custom" && (
              <input
                type="date"
                className="key-date"
                min={tomorrow}
                value={date}
                onChange={(e) => setDate(e.target.value)}
                aria-label={t("apiKeys.expiry.pickDate")}
              />
            )}
            <p className="hint" style={{ margin: "4px 0 0" }}>
              {mode === "never"
                ? t("apiKeys.expiry.neverHint")
                : expiresAtUtc
                  ? t("apiKeys.expiry.until", { date: new Date(expiresAtUtc).toLocaleDateString() })
                  : t("apiKeys.expiry.pickDate")}
            </p>
          </div>

          {err && (
            <div className="error small">
              <AlertTriangle /> <span>{err}</span>
            </div>
          )}

          <footer className="key-dialog-footer">
            <button type="button" className="btn" onClick={onClose}>
              {t("common.cancel")}
            </button>
            <button type="submit" className="btn primary" disabled={!valid || busy}>
              {busy ? <Loader2 size={14} className="spin" /> : existing ? <Check size={14} /> : <Plus size={14} />}{" "}
              {existing ? t("common.save") : t("apiKeys.dialog.create")}
            </button>
          </footer>
        </form>
      </div>
    </div>
  );
}

/** Shown once, right after creation — the only moment the full key exists. */
function SecretDialog({ keyInfo, secret, onClose }: { keyInfo: ApiKeyInfo; secret: string; onClose: () => void }) {
  const { t } = useTranslation();
  const dialogRef = useFocusTrap<HTMLDivElement>();
  const [copied, setCopied] = useState(false);

  const copy = () => {
    void navigator.clipboard.writeText(secret).then(() => {
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1800);
    });
  };

  // No Escape/backdrop dismissal: closing by accident would lose the key for good.
  return (
    <div className="modal-backdrop">
      <div ref={dialogRef} className="modal key-dialog" role="dialog" aria-modal="true" aria-label={t("apiKeys.reveal.title")}>
        <header>
          <h2>
            <KeyRound /> {t("apiKeys.reveal.title")}
          </h2>
        </header>
        <div className="key-dialog-body">
          <div className="key-warning">
            <AlertTriangle size={16} />
            <span>{t("apiKeys.reveal.warning")}</span>
          </div>

          <div className="field">
            <span>{keyInfo.name}</span>
            <div className="key-secret">
              <code>{secret}</code>
              <button className="btn" onClick={copy} autoFocus>
                {copied ? <Check size={14} /> : <Copy size={14} />} {copied ? t("apiKeys.reveal.copied") : t("apiKeys.reveal.copy")}
              </button>
            </div>
          </div>

          <div className="field">
            <span>{t("apiKeys.reveal.usage")}</span>
            <pre className="key-snippet">{`var reports = new JetReportClient(new JetReportClientOptions
{
    BaseUrl = "${window.location.origin}",
    ApiKey  = "${secret.slice(0, 12)}…"
});`}</pre>
          </div>

          <footer className="key-dialog-footer">
            <button className="btn primary" onClick={onClose}>
              <Check size={14} /> {t("apiKeys.reveal.done")}
            </button>
          </footer>
        </div>
      </div>
    </div>
  );
}

/** API-key management: create, expire, disable and delete the credentials external apps call the API with. */
export function ApiKeysPage() {
  const { t, i18n } = useTranslation();
  const [keys, setKeys] = useState<ApiKeyInfo[] | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [dialog, setDialog] = useState<{ existing: ApiKeyInfo | null } | null>(null);
  const [revealed, setRevealed] = useState<{ key: ApiKeyInfo; secret: string } | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const { sort, toggle, apply } = useSort<"name" | "status" | "expiresAtUtc" | "lastUsedAtUtc" | "createdAtUtc">({
    key: "createdAtUtc",
    dir: "desc",
  });

  const refresh = () =>
    api
      .listApiKeys()
      .then(setKeys)
      .catch((e) => setErr(msg(e)));

  useEffect(() => {
    void refresh();
  }, []);

  const counts = useMemo(() => {
    const c = { active: 0, disabled: 0, expired: 0 };
    keys?.forEach((k) => (c[k.status] += 1));
    return c;
  }, [keys]);

  const act = async (id: string, fn: () => Promise<unknown>) => {
    setBusyId(id);
    setErr(null);
    try {
      await fn();
      await refresh();
    } catch (e) {
      setErr(msg(e));
    } finally {
      setBusyId(null);
    }
  };

  if (!keys) {
    return (
      <section className="start-section">
        <div className="share-loading">
          <Loader2 size={16} className="spin" />
        </div>
      </section>
    );
  }

  const rows = apply(keys, (k, column) => (column === "status" ? k.status : (k[column] ?? null)));

  return (
    <section className="start-section">
      <PageHeader
        title={t("apiKeys.title")}
        description={t("apiKeys.description")}
        actions={
          <button className="btn primary" onClick={() => setDialog({ existing: null })}>
            <Plus size={14} /> {t("apiKeys.newKey")}
          </button>
        }
      />

      {err && (
        <div className="error small">
          <AlertTriangle /> <span>{err}</span>
        </div>
      )}

      {keys.length === 0 ? (
        <div className="start-empty">
          <KeyRound />
          <div>{t("apiKeys.empty")}</div>
          <p>{t("apiKeys.emptyHint")}</p>
          <button className="btn primary" onClick={() => setDialog({ existing: null })}>
            <Plus size={14} /> {t("apiKeys.newKey")}
          </button>
        </div>
      ) : (
        <>
          <div className="key-summary">
            <span className="key-status key-status-active">
              <CheckCircle2 size={12} /> {counts.active} {t("apiKeys.status.active")}
            </span>
            <span className="key-status key-status-disabled">
              <Ban size={12} /> {counts.disabled} {t("apiKeys.status.disabled")}
            </span>
            <span className="key-status key-status-expired">
              <Clock3 size={12} /> {counts.expired} {t("apiKeys.status.expired")}
            </span>
          </div>

          <div className="data-card">
            <table className="drive-table key-table">
              <thead>
                <tr>
                  <SortableTh column="name" sort={sort} onToggle={toggle}>{t("apiKeys.col.name")}</SortableTh>
                  <th>{t("apiKeys.col.key")}</th>
                  <SortableTh column="status" sort={sort} onToggle={toggle}>{t("apiKeys.col.status")}</SortableTh>
                  <SortableTh column="expiresAtUtc" sort={sort} onToggle={toggle}>{t("apiKeys.col.expires")}</SortableTh>
                  <SortableTh column="lastUsedAtUtc" sort={sort} onToggle={toggle}>{t("apiKeys.col.lastUsed")}</SortableTh>
                  <SortableTh column="createdAtUtc" sort={sort} onToggle={toggle}>{t("apiKeys.col.created")}</SortableTh>
                  <th />
                </tr>
              </thead>
              <tbody>
                {rows.map((k) => {
                  const left = k.expiresAtUtc ? daysLeft(k.expiresAtUtc) : null;
                  return (
                    <tr key={k.id} className={k.status === "active" ? undefined : "key-row-off"}>
                      <td>
                        <div className="key-name">
                          <strong>{k.name}</strong>
                          {k.description && <span className="hint">{k.description}</span>}
                        </div>
                      </td>
                      <td>
                        <code className="key-prefix">{k.keyPrefix}…</code>
                      </td>
                      <td>
                        <StatusBadge status={k.status} />
                      </td>
                      <td>
                        {k.expiresAtUtc ? (
                          <span title={new Date(k.expiresAtUtc).toLocaleString(i18n.language)}>
                            {new Date(k.expiresAtUtc).toLocaleDateString(i18n.language)}
                            {k.status !== "expired" && left !== null && left <= 14 && (
                              <span className="key-soon">
                                {left <= 1 ? t("apiKeys.expiry.today") : t("apiKeys.expiry.daysLeft", { count: left })}
                              </span>
                            )}
                          </span>
                        ) : (
                          <span className="hint" style={{ margin: 0 }}>{t("apiKeys.expiry.never")}</span>
                        )}
                      </td>
                      <td>
                        {k.lastUsedAtUtc ? (
                          <span title={new Date(k.lastUsedAtUtc).toLocaleString(i18n.language)}>
                            {timeAgo(k.lastUsedAtUtc, i18n.language)}
                          </span>
                        ) : (
                          <span className="hint" style={{ margin: 0 }}>{t("apiKeys.neverUsed")}</span>
                        )}
                      </td>
                      <td title={new Date(k.createdAtUtc).toLocaleString(i18n.language)}>
                        {timeAgo(k.createdAtUtc, i18n.language)}
                        {k.createdByEmail && <span className="hint key-by">{k.createdByEmail}</span>}
                      </td>
                      <td className="key-actions">
                        <button
                          className="mini"
                          disabled={busyId === k.id}
                          onClick={() => void act(k.id, () => api.setApiKeyActive(k.id, !k.isActive))}
                          title={k.isActive ? t("apiKeys.disableHint") : t("apiKeys.enableHint")}
                        >
                          {k.isActive ? <Ban size={13} /> : <Play size={13} />}
                          {k.isActive ? t("apiKeys.disable") : t("apiKeys.enable")}
                        </button>
                        <button className="mini" onClick={() => setDialog({ existing: k })} aria-label={t("apiKeys.edit")} title={t("apiKeys.edit")}>
                          <Pencil size={13} />
                        </button>
                        <ConfirmButton
                          icon={Trash2}
                          title={t("apiKeys.deleteTitle", { name: k.name })}
                          confirmLabel={t("common.delete")}
                          onConfirm={() => void act(k.id, () => api.deleteApiKey(k.id))}
                        />
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <p className="hint">{t("apiKeys.footnote")}</p>
        </>
      )}

      {dialog && (
        <KeyDialog
          existing={dialog.existing}
          onClose={() => setDialog(null)}
          onSaved={(key, secret) => {
            setDialog(null);
            if (secret) setRevealed({ key, secret });
            void refresh();
          }}
        />
      )}
      {revealed && <SecretDialog keyInfo={revealed.key} secret={revealed.secret} onClose={() => setRevealed(null)} />}
    </section>
  );
}
