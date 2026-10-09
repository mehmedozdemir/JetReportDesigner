import { useEffect, useRef, useState } from "react";
import { pxToUnit, unitToPx, usePrefs } from "../prefs";

// Accepts both "." and "," as the decimal separator.
const parse = (v: string) => Number(v.replace(",", "."));
const show = (px: number, unit: Parameters<typeof pxToUnit>[1]) =>
  String(Math.round(pxToUnit(px, unit) * 1000) / 1000);

/**
 * Length input. The value is stored in px (1/96 inch) but shown and typed in the unit chosen in
 * Settings (mm / cm / px). A plain text box, so there are no spin buttons and any precision can
 * be typed; values below `min` (px) are ignored while typing and snapped back on blur.
 */
export function UnitInput({
  value,
  onChange,
  min = 0,
  style,
}: {
  value: number;
  onChange: (px: number) => void;
  min?: number;
  style?: React.CSSProperties;
}) {
  const unit = usePrefs((s) => s.rulerUnit);
  const [text, setText] = useState(show(value, unit));
  const focused = useRef(false);

  // Follow external changes (drag on canvas, undo, unit switch) but never rewrite while typing.
  useEffect(() => {
    if (!focused.current) setText(show(value, unit));
  }, [value, unit]);

  return (
    <input
      type="text"
      inputMode="decimal"
      style={style}
      value={text}
      onFocus={() => (focused.current = true)}
      onChange={(e) => {
        setText(e.target.value);
        const n = parse(e.target.value);
        if (e.target.value.trim() !== "" && Number.isFinite(n) && unitToPx(n, unit) >= min) onChange(unitToPx(n, unit));
      }}
      onBlur={() => {
        focused.current = false;
        setText(show(value, unit));
      }}
    />
  );
}

/** A labelled UnitInput, with the active unit shown next to the label. */
export function UnitField({
  label,
  value,
  onChange,
  min,
}: {
  label: string;
  value: number;
  onChange: (px: number) => void;
  min?: number;
}) {
  const unit = usePrefs((s) => s.rulerUnit);
  return (
    <label className="field">
      <span>{label} ({unit})</span>
      <UnitInput value={value} onChange={onChange} min={min} />
    </label>
  );
}
