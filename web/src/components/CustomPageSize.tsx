import { useTranslation } from "react-i18next";
import { UnitField } from "./UnitInput";

/** Free width/height entry for a "Custom" page size, in the unit chosen in Settings. */
export function CustomPageSize({
  width,
  height,
  onChange,
}: {
  width: number;
  height: number;
  onChange: (width: number, height: number) => void;
}) {
  const { t } = useTranslation();
  return (
    <div className="grid2">
      <UnitField label={t("props.width")} value={width} min={1} onChange={(w) => onChange(w, height)} />
      <UnitField label={t("props.height")} value={height} min={1} onChange={(h) => onChange(width, h)} />
    </div>
  );
}
