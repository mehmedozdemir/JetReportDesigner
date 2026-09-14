import { useMemo, useState } from "react";
import { ChevronLeft, FileBarChart2, LayoutTemplate, Rows3, Search, SquareDashed, X } from "lucide-react";
import { emptyBandedReport, emptyFreeReport, type Orientation, type PageSize, type ReportDefinition } from "../types";
import { useEscapeKey } from "../useEscapeKey";
import { PAGE_SIZES } from "./PropertiesPanel";
import { ReportThumbnail } from "./ReportThumbnail";

export interface PageChoice {
  size: PageSize;
  orientation: Orientation;
}

type Picked = { kind: "blank"; mode: "free" | "banded" } | { kind: "sample"; name: string };

export function NewReportDialog({
  samples,
  busy,
  onCreateBlank,
  onCreateFromSample,
  onClose,
}: {
  samples: { name: string; category: string; definition: ReportDefinition }[];
  busy: boolean;
  onCreateBlank: (mode: "free" | "banded", page: PageChoice) => void;
  onCreateFromSample: (name: string, page: PageChoice) => void;
  onClose: () => void;
}) {
  const [picked, setPicked] = useState<Picked | null>(null);
  const [query, setQuery] = useState("");
  const [category, setCategory] = useState<string>("All");
  const [size, setSize] = useState<PageSize>("A4");

  useEscapeKey(onClose);
  const [orientation, setOrientation] = useState<Orientation>("portrait");

  const categories = useMemo(() => ["All", ...new Set(samples.map((s) => s.category))], [samples]);
  const filteredSamples = useMemo(() => {
    const q = query.trim().toLowerCase();
    return samples.filter(
      (s) => (category === "All" || s.category === category) && (!q || s.name.toLowerCase().includes(q)),
    );
  }, [samples, query, category]);

  const previewDefinition = useMemo((): ReportDefinition | null => {
    if (!picked) return null;
    const base = picked.kind === "sample" ? samples.find((s) => s.name === picked.name)?.definition : undefined;
    const source = base ?? (picked.kind === "blank" && picked.mode === "banded" ? emptyBandedReport("") : emptyFreeReport(""));
    return { ...source, page: { ...source.page, size, orientation } };
  }, [picked, samples, size, orientation]);

  const create = () => {
    if (!picked) return;
    const page: PageChoice = { size, orientation };
    if (picked.kind === "blank") onCreateBlank(picked.mode, page);
    else onCreateFromSample(picked.name, page);
  };

  return (
    <div className="modal-backdrop" onMouseDown={onClose}>
      <div
        className="modal new-report-dialog"
        role="dialog"
        aria-modal="true"
        aria-label="New report"
        onMouseDown={(e) => e.stopPropagation()}
      >
        <header>
          <h2><LayoutTemplate /> New report</h2>
          <button className="mini ghost" onClick={onClose} aria-label="Close">
            <X />
          </button>
        </header>

        {!picked ? (
          <div className="nrd-body">
            <div className="nrd-blanks">
              <button className="start-card" onClick={() => setPicked({ kind: "blank", mode: "free" })}>
                <span className="start-card-icon"><SquareDashed /></span>
                <span className="start-card-title">Blank — Free layout</span>
                <span className="start-card-sub">Place elements anywhere on a fixed canvas</span>
              </button>
              <button className="start-card" onClick={() => setPicked({ kind: "blank", mode: "banded" })}>
                <span className="start-card-icon"><Rows3 /></span>
                <span className="start-card-title">Blank — Banded report</span>
                <span className="start-card-sub">Header / detail / footer bands that repeat per row</span>
              </button>
            </div>

            {samples.length > 0 && (
              <>
                <div className="nrd-divider"><span>or start from a template</span></div>

                <div className="nrd-gallery-head">
                  <label className="start-search">
                    <Search size={14} />
                    <input value={query} placeholder="Search templates" onChange={(e) => setQuery(e.target.value)} />
                  </label>
                  <div className="nrd-cats">
                    {categories.map((c) => (
                      <button key={c} className={`mini${c === category ? " on" : ""}`} onClick={() => setCategory(c)}>
                        {c}
                      </button>
                    ))}
                  </div>
                </div>

                {filteredSamples.length === 0 ? (
                  <p className="hint">No templates match.</p>
                ) : (
                  <div className="nrd-gallery">
                    {filteredSamples.map((s) => (
                      <button key={s.name} className="nrd-tpl" onClick={() => setPicked({ kind: "sample", name: s.name })}>
                        <ReportThumbnail definition={s.definition} className="nrd-tpl-thumb" />
                        <span className="nrd-tpl-name">{s.name.replace(/^Sample\s*—\s*/, "")}</span>
                      </button>
                    ))}
                  </div>
                )}
              </>
            )}
          </div>
        ) : (
          <div className="nrd-body">
            <button className="mini ghost nrd-back" onClick={() => setPicked(null)}>
              <ChevronLeft size={14} /> Choose a different start
            </button>

            <div className="nrd-format">
              <div className="nrd-format-fields">
                <label className="field">
                  <span>Page size</span>
                  <select value={size} onChange={(e) => setSize(e.target.value as PageSize)}>
                    {PAGE_SIZES.map(([value, label]) => (
                      <option key={value} value={value}>{label}</option>
                    ))}
                  </select>
                </label>
                <label className="field">
                  <span>Orientation</span>
                  <div className="segmented" role="group" aria-label="Orientation">
                    <button className={orientation === "portrait" ? "on" : ""} onClick={() => setOrientation("portrait")}>
                      Portrait
                    </button>
                    <button className={orientation === "landscape" ? "on" : ""} onClick={() => setOrientation("landscape")}>
                      Landscape
                    </button>
                  </div>
                </label>
                <p className="hint">Margins and columns can be fine-tuned later from the Page panel.</p>
              </div>

              {previewDefinition && (
                <div className="nrd-format-preview">
                  <ReportThumbnail definition={previewDefinition} />
                </div>
              )}
            </div>
          </div>
        )}

        {picked && (
          <footer>
            <button className="btn primary" onClick={create} disabled={busy}>
              <FileBarChart2 /> Create report
            </button>
          </footer>
        )}
      </div>
    </div>
  );
}
