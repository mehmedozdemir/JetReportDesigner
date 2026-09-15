import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Check, Copy, Link2, Loader2, Trash2, X } from "lucide-react";
import { api, type ShareInfo } from "../api";
import type { ReportSummary } from "../types";
import { useEscapeKey } from "../useEscapeKey";
import { useFocusTrap } from "../useFocusTrap";
import { ConfirmButton } from "./ConfirmButton";

const msg = (e: unknown) => (e instanceof Error ? e.message : String(e));
const urlFor = (token: string) => `${window.location.origin}/share/${token}`;

/** OneDrive-style "get a link" dialog: anyone with the link can view/export the report's
 * current, working output — never the design surface. No sign-in required on their end. */
export function ShareDialog({ report, onClose }: { report: ReportSummary; onClose: () => void }) {
  const [shares, setShares] = useState<ShareInfo[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [copiedToken, setCopiedToken] = useState<string | null>(null);
  const { t } = useTranslation();

  useEscapeKey(onClose);
  const dialogRef = useFocusTrap<HTMLDivElement>();

  const refresh = () => api.listShares(report.id).then(setShares).catch((e) => setError(msg(e)));
  useEffect(() => {
    void refresh();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [report.id]);

  const create = async () => {
    setCreating(true);
    setError(null);
    try {
      await api.createShare(report.id);
      await refresh();
    } catch (e) {
      setError(msg(e));
    } finally {
      setCreating(false);
    }
  };

  const revoke = async (token: string) => {
    setError(null);
    try {
      await api.revokeShare(report.id, token);
      await refresh();
    } catch (e) {
      setError(msg(e));
    }
  };

  const copy = async (token: string) => {
    try {
      await navigator.clipboard.writeText(urlFor(token));
      setCopiedToken(token);
      setTimeout(() => setCopiedToken((cur) => (cur === token ? null : cur)), 1500);
    } catch {
      setError(t("share.copyFailed"));
    }
  };

  return (
    <div className="modal-backdrop" onMouseDown={onClose}>
      <div
        ref={dialogRef}
        className="modal share-dialog"
        role="dialog"
        aria-modal="true"
        aria-label={`Share ${report.name}`}
        onMouseDown={(e) => e.stopPropagation()}
      >
        <header>
          <h2><Link2 /> {t("share.title", { name: report.name })}</h2>
          <button className="mini ghost" onClick={onClose} aria-label={t("common.close")}>
            <X />
          </button>
        </header>

        <div className="share-body">
          <p className="hint">
            {t("share.description")}
          </p>
          {error && <p className="hint" style={{ color: "var(--error)" }}>{error}</p>}

          {shares === null ? (
            <div className="share-loading">
              <Loader2 size={16} className="spin" />
            </div>
          ) : shares.length === 0 ? (
            <button className="btn primary" onClick={() => void create()} disabled={creating}>
              {creating ? t("share.creating") : t("share.createLink")}
            </button>
          ) : (
            <>
              <ul className="share-list">
                {shares.map((s) => (
                  <li key={s.token} className="share-row">
                    <input
                      className="share-link-input"
                      readOnly
                      value={urlFor(s.token)}
                      onFocus={(e) => e.target.select()}
                    />
                    <button className="mini" onClick={() => void copy(s.token)}>
                      {copiedToken === s.token ? <Check size={13} /> : <Copy size={13} />}
                      {copiedToken === s.token ? t("share.copied") : t("share.copy")}
                    </button>
                    <ConfirmButton
                      icon={Trash2}
                      title={t("share.revoke")}
                      confirmLabel={t("common.remove")}
                      onConfirm={() => void revoke(s.token)}
                    />
                  </li>
                ))}
              </ul>
              <button className="mini" onClick={() => void create()} disabled={creating}>
                {creating ? t("share.creating") : t("share.createAnother")}
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
