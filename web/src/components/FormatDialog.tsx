import { useEffect, useState } from "react";
import { Wand2, X } from "lucide-react";
import {
  applyFormat,
  defaultSample,
  FORMAT_CATEGORIES,
  FORMAT_PRESETS,
  guessCategory,
  previewSeed,
  type FormatCategory,
} from "../format";

/** Format text input with a trailing button that opens the builder dialog. */
export function FormatField({
  value,
  onChange,
}: {
  value: string;
  onChange: (v: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const trimmed = value.trim();

  return (
    <div className="field">
      <span>Format</span>
      <div className="format-input">
        <input
          value={value}
          placeholder="e.g. N2, dd.MM.yyyy, C"
          onChange={(e) => onChange(e.target.value)}
        />
        <button
          type="button"
          className="mini"
          title="Format builder"
          aria-label="Open format builder"
          onClick={() => setOpen(true)}
        >
          <Wand2 />
        </button>
      </div>
      {trimmed && (
        <span className="format-preview">
          Preview: {applyFormat(previewSeed(trimmed), trimmed) || "—"}
        </span>
      )}
      {open && (
        <FormatDialog
          initial={value}
          onApply={(v) => {
            onChange(v);
            setOpen(false);
          }}
          onClose={() => setOpen(false)}
        />
      )}
    </div>
  );
}

function FormatDialog({
  initial,
  onApply,
  onClose,
}: {
  initial: string;
  onApply: (v: string) => void;
  onClose: () => void;
}) {
  const [cat, setCat] = useState<FormatCategory>(() => guessCategory(initial));
  const [draft, setDraft] = useState(initial.trim());
  const [sample, setSample] = useState<string>(() => defaultSample(guessCategory(initial)));

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  const pickCategory = (c: FormatCategory) => {
    setCat(c);
    setSample(defaultSample(c));
    const first = FORMAT_PRESETS[c][0]?.code ?? "";
    if (c === "general" || c === "text") setDraft("");
    else setDraft(first);
  };

  const domain: "number" | "date" = cat === "date" || cat === "time" ? "date" : "number";
  const presets = FORMAT_PRESETS[cat];
  const result = draft ? applyFormat(sample, draft, domain) : sample;

  return (
    <div className="modal-backdrop" onMouseDown={onClose}>
      <div
        className="modal fmt-dialog"
        role="dialog"
        aria-modal="true"
        aria-label="Format builder"
        onMouseDown={(e) => e.stopPropagation()}
      >
        <header>
          <h2>
            <Wand2 /> Number format
          </h2>
          <button className="mini ghost" onClick={onClose} aria-label="Close">
            <X />
          </button>
        </header>

        <div className="fmt-body">
          <ul className="fmt-cats">
            {FORMAT_CATEGORIES.map((c) => (
              <li key={c.id}>
                <button className={c.id === cat ? "on" : ""} onClick={() => pickCategory(c.id)}>
                  {c.label}
                </button>
              </li>
            ))}
          </ul>

          <div className="fmt-detail">
            <label className="field">
              <span>Sample value</span>
              <input value={sample} onChange={(e) => setSample(e.target.value)} />
            </label>

            {cat === "general" || cat === "text" ? (
              <p className="hint">
                {cat === "text"
                  ? "The value is shown exactly as it comes from the data."
                  : "No formatting is applied — numbers and dates keep their default representation."}
              </p>
            ) : (
              <ul className="fmt-presets">
                {presets.map((p) => (
                  <li key={p.code || "none"}>
                    <button
                      className={p.code === draft ? "on" : ""}
                      onClick={() => setDraft(p.code)}
                    >
                      <span className="fmt-sample">{applyFormat(sample, p.code, domain) || "—"}</span>
                      <span className="fmt-code">{p.code || "General"}</span>
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>

        <footer>
          <label className="field" style={{ flex: 1, margin: 0 }}>
            <span>Format code</span>
            <input
              value={draft}
              placeholder="type a .NET format string"
              onChange={(e) => setDraft(e.target.value)}
            />
          </label>
          <div className="fmt-result">
            <span>Result</span>
            <strong>{result || "—"}</strong>
          </div>
          <div className="row">
            <button className="btn" onClick={onClose}>
              Cancel
            </button>
            <button className="btn primary" onClick={() => onApply(draft.trim())}>
              Use format
            </button>
          </div>
        </footer>
      </div>
    </div>
  );
}
