/**
 * Excel-style format catalogue plus a best-effort ".NET format string -> text"
 * previewer. The server formats with `IFormattable.ToString(format, culture)`;
 * this module only needs to be close enough to show the user what a format does.
 */

export type FormatCategory =
  | "general"
  | "number"
  | "currency"
  | "percentage"
  | "scientific"
  | "date"
  | "time"
  | "text";

export interface FormatPreset {
  code: string; // stored on the element ("" = General / no formatting)
  label: string;
}

export const FORMAT_CATEGORIES: { id: FormatCategory; label: string }[] = [
  { id: "general", label: "General" },
  { id: "number", label: "Number" },
  { id: "currency", label: "Currency" },
  { id: "percentage", label: "Percentage" },
  { id: "scientific", label: "Scientific" },
  { id: "date", label: "Date" },
  { id: "time", label: "Time" },
  { id: "text", label: "Text" },
];

export const FORMAT_PRESETS: Record<FormatCategory, FormatPreset[]> = {
  general: [{ code: "", label: "General" }],
  number: [
    { code: "0", label: "Integer" },
    { code: "N0", label: "Thousands, no decimals" },
    { code: "N2", label: "Thousands, 2 decimals" },
    { code: "#,##0.00", label: "Custom — #,##0.00" },
    { code: "0.00", label: "2 decimals, no grouping" },
    { code: "#,##0", label: "Custom — #,##0" },
  ],
  currency: [
    { code: "C", label: "Currency" },
    { code: "C0", label: "Currency, no decimals" },
    { code: "C2", label: "Currency, 2 decimals" },
  ],
  percentage: [
    { code: "P0", label: "Percent, no decimals" },
    { code: "P1", label: "Percent, 1 decimal" },
    { code: "P2", label: "Percent, 2 decimals" },
    { code: "0.00%", label: "Custom — 0.00%" },
  ],
  scientific: [
    { code: "E2", label: "Exponential, 2 decimals" },
    { code: "E4", label: "Exponential, 4 decimals" },
    { code: "0.000E+00", label: "Custom — 0.000E+00" },
  ],
  date: [
    { code: "d", label: "Short date" },
    { code: "D", label: "Long date" },
    { code: "dd.MM.yyyy", label: "31.12.2026" },
    { code: "yyyy-MM-dd", label: "2026-12-31" },
    { code: "dd/MM/yyyy", label: "31/12/2026" },
    { code: "MMM d, yyyy", label: "Dec 31, 2026" },
    { code: "MMMM yyyy", label: "December 2026" },
  ],
  time: [
    { code: "t", label: "Short time" },
    { code: "T", label: "Long time" },
    { code: "HH:mm", label: "14:07" },
    { code: "HH:mm:ss", label: "14:07:05" },
    { code: "hh:mm tt", label: "02:07 PM" },
  ],
  text: [{ code: "", label: "Text (shown as-is)" }],
};

const SAMPLE_DATE = new Date(2026, 2, 9, 14, 7, 5); // 2026-03-09 14:07:05, local

function isoLocal(d: Date): string {
  const p = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
}

export function defaultSample(cat: FormatCategory): string {
  switch (cat) {
    case "percentage":
      return "0.1235";
    case "date":
    case "time":
      return isoLocal(SAMPLE_DATE);
    case "text":
      return "Sample text";
    default:
      return "1234.567";
  }
}

/** A sample value suited to the given format string (for the inline preview). */
export function previewSeed(fmt: string): string {
  if (/^[Pp]\d*$/.test(fmt.trim()) || fmt.includes("%")) return "0.1235";
  return guessDomain(fmt) === "date" ? isoLocal(SAMPLE_DATE) : "1234.567";
}

/** Which "Format Cells" category best matches an existing format string. */
export function guessCategory(fmt: string): FormatCategory {
  const f = (fmt ?? "").trim();
  if (!f) return "number";
  if (/^[Cc]\d*$/.test(f)) return "currency";
  if (/^[Pp]\d*$/.test(f) || f.includes("%")) return "percentage";
  if (/^[Ee]\d*$/.test(f) || /[0#]E[+-]?0/i.test(f)) return "scientific";
  if (guessDomain(f) === "date") {
    return /[Hhst]/.test(f) && !/[yMd]/.test(f) ? "time" : "date";
  }
  return "number";
}

export function guessDomain(fmt: string): "number" | "date" {
  const f = (fmt ?? "").trim();
  if (!f) return "number";
  if (/^[dDfFgGmMyYtT]$/.test(f)) return "date"; // lone standard date/time specifier
  if (/[0#]/.test(f)) return "number";
  if (/^[NnCcPpFfEeGgXxRr]\d*$/.test(f)) return "number";
  if (/[yMdHhs]/.test(f)) return "date";
  return "number";
}

/**
 * Format `rawValue` (a string as it would arrive from the data source) with a
 * .NET-style `format`. `domain` forces the number/date interpretation; when
 * omitted it is inferred from the value itself.
 */
export function applyFormat(
  rawValue: string,
  format: string,
  domain?: "number" | "date",
): string {
  const fmt = (format ?? "").trim();
  const value = rawValue ?? "";
  if (!fmt) return value;

  const asNum = parseNumber(value);
  const asDate = parseDate(value);

  if (domain === "date" || (domain !== "number" && asDate && asNum === null)) {
    const d = asDate ?? (domain === "date" ? SAMPLE_DATE : null);
    if (d) return formatDate(d, fmt);
  }

  if (asNum !== null) {
    const out = formatNumber(asNum, fmt);
    if (out !== null) return out;
  }

  return value;
}

// ---- value parsing ------------------------------------------------------------

function parseNumber(s: string): number | null {
  const t = (s ?? "").trim();
  if (t === "") return null;
  const cleaned = t.replace(/,/g, "");
  if (!/^-?(\d+\.?\d*|\.\d+)$/.test(cleaned)) return null;
  const n = Number(cleaned);
  return Number.isFinite(n) ? n : null;
}

function parseDate(s: string): Date | null {
  const t = (s ?? "").trim();
  if (!t) return null;
  if (/^-?(\d+\.?\d*|\.\d+)$/.test(t.replace(/,/g, ""))) return null; // a plain number is not a date
  const dateOnly = /^(\d{4})-(\d{2})-(\d{2})$/.exec(t);
  if (dateOnly) return new Date(+dateOnly[1], +dateOnly[2] - 1, +dateOnly[3]);
  const d = new Date(t);
  return Number.isNaN(d.getTime()) ? null : d;
}

// ---- number formatting ------------------------------------------------------

function guessCurrency(): string {
  try {
    const loc = new Intl.NumberFormat().resolvedOptions().locale;
    const region = loc.split("-").find((p) => /^[A-Z]{2}$/.test(p));
    const map: Record<string, string> = {
      US: "USD", GB: "GBP", TR: "TRY", JP: "JPY", CN: "CNY", IN: "INR",
      CA: "CAD", AU: "AUD", CH: "CHF", SE: "SEK", NO: "NOK", DK: "DKK",
      PL: "PLN", BR: "BRL", RU: "RUB", DE: "EUR", FR: "EUR", ES: "EUR",
      IT: "EUR", NL: "EUR", IE: "EUR", PT: "EUR", AT: "EUR", FI: "EUR",
    };
    return (region && map[region]) || "USD";
  } catch {
    return "USD";
  }
}

function formatNumber(n: number, fmt: string): string | null {
  const std = /^([A-Za-z])(\d*)$/.exec(fmt);
  if (std) {
    const c = std[1].toLowerCase();
    const prec = std[2] === "" ? undefined : parseInt(std[2], 10);
    switch (c) {
      case "n":
        return new Intl.NumberFormat(undefined, {
          minimumFractionDigits: prec ?? 2,
          maximumFractionDigits: prec ?? 2,
        }).format(n);
      case "f":
        return n.toFixed(prec ?? 2);
      case "d":
        return (n < 0 ? "-" : "") + Math.abs(Math.trunc(n)).toString().padStart(prec ?? 0, "0");
      case "c":
        return new Intl.NumberFormat(undefined, {
          style: "currency",
          currency: guessCurrency(),
          minimumFractionDigits: prec ?? 2,
          maximumFractionDigits: prec ?? 2,
        }).format(n);
      case "p":
        return new Intl.NumberFormat(undefined, {
          style: "percent",
          minimumFractionDigits: prec ?? 2,
          maximumFractionDigits: prec ?? 2,
        }).format(n);
      case "e":
        return toDotNetExponential(n, prec ?? 6, 3);
      case "x":
        return Math.trunc(n).toString(16).toUpperCase().padStart(prec ?? 0, "0");
      case "g":
        return String(n);
      default:
        return null;
    }
  }
  if (/[0#]/.test(fmt)) return formatCustomNumber(n, fmt);
  return null;
}

function toDotNetExponential(n: number, fracDigits: number, expDigits: number): string {
  return n
    .toExponential(fracDigits)
    .replace(/e([+-])(\d+)/i, (_, sign, digits) => `E${sign}${digits.padStart(expDigits, "0")}`);
}

function formatCustomNumber(n: number, fmt: string): string | null {
  const sci = /^([0#]+)(?:\.([0#]+))?E([+-])(0+)$/i.exec(fmt);
  if (sci) {
    const fracLen = (sci[2] ?? "").length;
    return toDotNetExponential(n, fracLen, sci[4].length);
  }

  let f = fmt;
  let percent = false;
  if (f.includes("%")) {
    percent = true;
    f = f.replace(/%/g, "");
  }

  const pat = /[0#][0#,]*(?:\.[0#]+)?/.exec(f);
  if (!pat) return null;
  const prefix = f.slice(0, pat.index).replace(/["']/g, "");
  const suffix = f.slice(pat.index + pat[0].length).replace(/["']/g, "");

  const grouping = pat[0].includes(",");
  const [intPart, fracPart = ""] = pat[0].replace(/,/g, "").split(".");
  const minInt = (intPart.match(/0/g) || []).length;
  const minFrac = (fracPart.match(/0/g) || []).length;
  const maxFrac = fracPart.length;

  const body = new Intl.NumberFormat(undefined, {
    useGrouping: grouping,
    minimumIntegerDigits: Math.max(1, minInt),
    minimumFractionDigits: minFrac,
    maximumFractionDigits: Math.max(minFrac, maxFrac),
  }).format(n * (percent ? 100 : 1));

  return `${prefix}${body}${suffix}${percent ? "%" : ""}`;
}

// ---- date formatting ------------------------------------------------------

const STD_DATE: Record<string, Intl.DateTimeFormatOptions> = {
  d: { year: "numeric", month: "numeric", day: "numeric" },
  D: { year: "numeric", month: "long", day: "numeric", weekday: "long" },
  t: { hour: "numeric", minute: "2-digit" },
  T: { hour: "numeric", minute: "2-digit", second: "2-digit" },
  f: { year: "numeric", month: "long", day: "numeric", weekday: "long", hour: "numeric", minute: "2-digit" },
  F: { year: "numeric", month: "long", day: "numeric", weekday: "long", hour: "numeric", minute: "2-digit", second: "2-digit" },
  g: { year: "numeric", month: "numeric", day: "numeric", hour: "numeric", minute: "2-digit" },
  G: { year: "numeric", month: "numeric", day: "numeric", hour: "numeric", minute: "2-digit", second: "2-digit" },
  M: { month: "long", day: "numeric" },
  m: { month: "long", day: "numeric" },
  Y: { year: "numeric", month: "long" },
  y: { year: "numeric", month: "long" },
};

const DATE_TOKENS = [
  "dddd", "ddd", "dd", "d",
  "MMMM", "MMM", "MM", "M",
  "yyyy", "yy",
  "HH", "H", "hh", "h",
  "mm", "m", "ss", "s",
  "tt", "t", "fff", "ff", "f",
];

function formatDate(d: Date, fmt: string): string {
  if (fmt.length === 1 && STD_DATE[fmt]) {
    return new Intl.DateTimeFormat(undefined, STD_DATE[fmt]).format(d);
  }

  const pad = (x: number, n = 2) => String(x).padStart(n, "0");
  const name = (opt: Intl.DateTimeFormatOptions, ref: Date) =>
    new Intl.DateTimeFormat(undefined, opt).format(ref);
  const h12 = d.getHours() % 12 || 12;

  let out = "";
  for (let i = 0; i < fmt.length; ) {
    const ch = fmt[i];
    if (ch === "'" || ch === '"') {
      const end = fmt.indexOf(ch, i + 1);
      out += end === -1 ? fmt.slice(i + 1) : fmt.slice(i + 1, end);
      i = end === -1 ? fmt.length : end + 1;
      continue;
    }
    if (ch === "\\") {
      out += fmt[i + 1] ?? "";
      i += 2;
      continue;
    }
    const rest = fmt.slice(i);
    const tok = DATE_TOKENS.find((t) => rest.startsWith(t));
    if (!tok) {
      out += ch;
      i += 1;
      continue;
    }
    i += tok.length;
    switch (tok) {
      case "yyyy": out += d.getFullYear(); break;
      case "yy": out += pad(d.getFullYear() % 100); break;
      case "MMMM": out += name({ month: "long" }, d); break;
      case "MMM": out += name({ month: "short" }, d); break;
      case "MM": out += pad(d.getMonth() + 1); break;
      case "M": out += d.getMonth() + 1; break;
      case "dddd": out += name({ weekday: "long" }, d); break;
      case "ddd": out += name({ weekday: "short" }, d); break;
      case "dd": out += pad(d.getDate()); break;
      case "d": out += d.getDate(); break;
      case "HH": out += pad(d.getHours()); break;
      case "H": out += d.getHours(); break;
      case "hh": out += pad(h12); break;
      case "h": out += h12; break;
      case "mm": out += pad(d.getMinutes()); break;
      case "m": out += d.getMinutes(); break;
      case "ss": out += pad(d.getSeconds()); break;
      case "s": out += d.getSeconds(); break;
      case "tt": out += d.getHours() < 12 ? "AM" : "PM"; break;
      case "t": out += d.getHours() < 12 ? "A" : "P"; break;
      case "fff": out += pad(d.getMilliseconds(), 3); break;
      case "ff": out += pad(Math.floor(d.getMilliseconds() / 10)); break;
      case "f": out += Math.floor(d.getMilliseconds() / 100); break;
      default: out += tok;
    }
  }
  return out;
}
