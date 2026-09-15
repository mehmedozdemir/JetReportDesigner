import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { createPortal } from "react-dom";
import { ImageOff, ImagePlus, Link2, Trash2, Upload, X } from "lucide-react";
import { api } from "../api";
import type { AssetResponse, BackgroundFit, BackgroundImageSpec } from "../types";
import { FIT_LABELS, imageSrc } from "../image";

const msg = (e: unknown) => (e instanceof Error ? e.message : String(e));
const ALL_FITS: BackgroundFit[] = ["cover", "contain", "fill", "tile"];

export function ImagePicker({
  label,
  value,
  onChange,
  fitOptions = ALL_FITS,
}: {
  label: string;
  value: BackgroundImageSpec | null | undefined;
  onChange: (v: BackgroundImageSpec | null) => void;
  fitOptions?: BackgroundFit[];
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const src = imageSrc(value?.source);

  return (
    <div className="field">
      <span>{label}</span>
      <div className="imgpick">
        <button
          type="button"
          className="imgpick-preview"
          title={value?.source ? t("designer.changeImage") : t("designer.chooseImage")}
          onClick={() => setOpen(true)}
        >
          {src ? (
            <img src={src} alt="" />
          ) : (
            <span className="imgpick-empty">
              <ImagePlus size={16} /> {t("designer.choose")}
            </span>
          )}
        </button>
        {value?.source && (
          <div className="imgpick-side">
            <select
              value={value.fit}
              onChange={(e) => onChange({ source: value.source, fit: e.target.value as BackgroundFit })}
              aria-label={`${label} fit`}
            >
              {fitOptions.map((f) => (
                <option key={f} value={f}>{FIT_LABELS[f]}</option>
              ))}
            </select>
            <button
              type="button"
              className="mini danger"
              title="Remove image"
              aria-label="Remove image"
              onClick={() => onChange(null)}
            >
              <Trash2 size={13} />
            </button>
          </div>
        )}
      </div>

      {open && (
        <ImageDialog
          current={value?.source ?? ""}
          onPick={(source) =>
            onChange({ source, fit: value?.fit ?? fitOptions[0] ?? "cover" })
          }
          onClose={() => setOpen(false)}
        />
      )}
    </div>
  );
}

function ImageDialog({
  current,
  onPick,
  onClose,
}: {
  current: string;
  onPick: (source: string) => void;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const [assets, setAssets] = useState<AssetResponse[] | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [url, setUrl] = useState(current.startsWith("asset:") ? "" : current);
  const fileRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    api.listAssets().then(setAssets).catch((e) => {
      setErr(msg(e));
      setAssets([]);
    });
  }, []);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  const upload = async (file: File) => {
    setBusy(true);
    setErr(null);
    try {
      const a = await api.uploadAsset(file);
      setAssets((prev) => [a, ...(prev ?? []).filter((x) => x.id !== a.id)]);
      onPick(`asset:${a.id}`);
      onClose();
    } catch (e) {
      setErr(msg(e));
    } finally {
      setBusy(false);
    }
  };

  return createPortal(
    <div className="modal-backdrop" onMouseDown={onClose}>
      <div
        className="modal imgpick-dialog"
        role="dialog"
        aria-modal="true"
        aria-label={t("designer.chooseImage")}
        onMouseDown={(e) => e.stopPropagation()}
      >
        <header>
          <h2><ImagePlus size={16} /> Choose image</h2>
          <button className="mini" onClick={onClose} aria-label="Close"><X size={14} /></button>
        </header>

        <div className="imgpick-body">
          <div className="imgpick-row">
            <button
              className="btn"
              disabled={busy}
              onClick={() => fileRef.current?.click()}
            >
              <Upload size={14} /> Upload from computer
            </button>
            <input
              ref={fileRef}
              type="file"
              accept="image/png,image/jpeg,image/gif,image/webp,image/bmp"
              hidden
              onChange={(e) => {
                const f = e.target.files?.[0];
                e.target.value = "";
                if (f) void upload(f);
              }}
            />
          </div>

          <label className="field">
            <span><Link2 size={12} /> Image URL</span>
            <div className="format-input">
              <input
                value={url}
                placeholder="https://…"
                onChange={(e) => setUrl(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter" && url.trim()) {
                    onPick(url.trim());
                    onClose();
                  }
                }}
              />
              <button
                className="mini"
                disabled={!url.trim()}
                onClick={() => { onPick(url.trim()); onClose(); }}
              >
                Use
              </button>
            </div>
          </label>

          <div className="imgpick-lib-head">Uploaded images</div>
          {assets === null ? (
            <div className="imgpick-hint">Loading…</div>
          ) : assets.length === 0 ? (
            <div className="imgpick-hint"><ImageOff size={14} /> Nothing uploaded yet.</div>
          ) : (
            <div className="imgpick-grid">
              {assets.map((a) => (
                <button
                  key={a.id}
                  type="button"
                  className={`imgpick-tile${current === `asset:${a.id}` ? " sel" : ""}`}
                  title={a.fileName}
                  onClick={() => { onPick(`asset:${a.id}`); onClose(); }}
                >
                  <img src={`/api/assets/${a.id}`} alt={a.fileName} loading="lazy" />
                </button>
              ))}
            </div>
          )}

          {err && <div className="imgpick-err">{err}</div>}
        </div>
      </div>
    </div>,
    document.body,
  );
}
