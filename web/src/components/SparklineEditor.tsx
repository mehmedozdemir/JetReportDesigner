import { useTranslation } from "react-i18next";
import { Ban, Plus } from "lucide-react";
import type { SparklineSpec } from "../types";

/** Edits one sparkline: chart type, colour, area fill, and an optional highlight for the
 *  series' last point — "where things stand right now". */
export function SparklineEditor({ spec, onChange }: { spec: SparklineSpec; onChange: (patch: (s: SparklineSpec) => void) => void }) {
  const { t } = useTranslation();
  return (
    <div className="scale-editor">
      <div className="row">
        <select value={spec.type} onChange={(e) => onChange((s) => (s.type = e.target.value as SparklineSpec["type"]))}>
          <option value="line">{t("props.sparklineLine")}</option>
          <option value="bar">{t("props.sparklineBar")}</option>
        </select>
        <input
          type="color"
          value={spec.color}
          title={t("props.sparklineColor")}
          aria-label={t("props.sparklineColor")}
          onChange={(e) => onChange((s) => (s.color = e.target.value))}
        />
        {spec.highlightColor ? (
          <>
            <input
              type="color"
              value={spec.highlightColor}
              title={t("props.sparklineHighlight")}
              aria-label={t("props.sparklineHighlight")}
              onChange={(e) => onChange((s) => (s.highlightColor = e.target.value))}
            />
            <button
              className="mini"
              type="button"
              title={t("props.sparklineRemoveHighlight")}
              aria-label={t("props.sparklineRemoveHighlight")}
              onClick={() => onChange((s) => (s.highlightColor = null))}
            >
              <Ban />
            </button>
          </>
        ) : (
          <button
            className="mini"
            type="button"
            title={t("props.sparklineAddHighlight")}
            aria-label={t("props.sparklineAddHighlight")}
            onClick={() => onChange((s) => (s.highlightColor = "#dc2626"))}
          >
            <Plus />
          </button>
        )}
      </div>
      {spec.type === "line" && (
        <label className="row">
          <input type="checkbox" checked={spec.showArea} onChange={(e) => onChange((s) => (s.showArea = e.target.checked))} />
          <span>{t("props.sparklineShowArea")}</span>
        </label>
      )}
    </div>
  );
}
