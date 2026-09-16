import { useTranslation } from "react-i18next";
import { Ban, Plus } from "lucide-react";
import { scaleFill } from "../colorScale";
import type { ColorScale } from "../types";

/** Edits one colour scale: the stops, and optional fixed bounds. Shared by the matrix section
 *  and the per-column editor, since a heatmap and a column scale are the same thing. */
export function ColorScaleEditor({ scale, onChange }: { scale: ColorScale; onChange: (patch: (s: ColorScale) => void) => void }) {
  const { t } = useTranslation();
  const preview = Array.from({ length: 12 }, (_, i) => scaleFill(scale, i, 0, 11));

  return (
    <div className="scale-editor">
      <div className="scale-strip" aria-hidden="true">
        {preview.map((c, i) => (
          <span key={i} style={{ background: c }} />
        ))}
      </div>

      <div className="row">
        <input
          type="color"
          value={scale.lowColor}
          title={t("props.scaleLow")}
          aria-label={t("props.scaleLow")}
          onChange={(e) => onChange((s) => (s.lowColor = e.target.value))}
        />
        {scale.midColor ? (
          <>
            <input
              type="color"
              value={scale.midColor}
              title={t("props.scaleMid")}
              aria-label={t("props.scaleMid")}
              onChange={(e) => onChange((s) => (s.midColor = e.target.value))}
            />
            <button
              className="mini"
              type="button"
              title={t("props.scaleRemoveMid")}
              aria-label={t("props.scaleRemoveMid")}
              onClick={() => onChange((s) => (s.midColor = null))}
            >
              <Ban />
            </button>
          </>
        ) : (
          <button
            className="mini"
            type="button"
            title={t("props.scaleAddMid")}
            aria-label={t("props.scaleAddMid")}
            onClick={() => onChange((s) => (s.midColor = "#fde68a"))}
          >
            <Plus />
          </button>
        )}
        <input
          type="color"
          value={scale.highColor}
          title={t("props.scaleHigh")}
          aria-label={t("props.scaleHigh")}
          onChange={(e) => onChange((s) => (s.highColor = e.target.value))}
        />
      </div>

      <div className="row">
        <input
          type="number"
          className="mini-num"
          style={{ width: 62 }}
          value={scale.min ?? ""}
          placeholder={t("props.scaleAuto")}
          title={t("props.scaleMin")}
          aria-label={t("props.scaleMin")}
          onChange={(e) => onChange((s) => (s.min = e.target.value === "" ? null : Number(e.target.value)))}
        />
        <span className="hint">…</span>
        <input
          type="number"
          className="mini-num"
          style={{ width: 62 }}
          value={scale.max ?? ""}
          placeholder={t("props.scaleAuto")}
          title={t("props.scaleMax")}
          aria-label={t("props.scaleMax")}
          onChange={(e) => onChange((s) => (s.max = e.target.value === "" ? null : Number(e.target.value)))}
        />
      </div>
    </div>
  );
}
