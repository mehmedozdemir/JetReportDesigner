/** Compact relative time ("3 dk önce" / "3m ago"), falling back to a plain date past a week.
 * Uses the platform's own relative-time formatting so each language gets its real phrasing
 * rather than an English template with the words swapped. */
export function timeAgo(iso: string, language = "en"): string {
  const t = new Date(iso).getTime();
  if (Number.isNaN(t)) return "";

  const seconds = Math.max(0, (Date.now() - t) / 1000);
  const rtf = new Intl.RelativeTimeFormat(language, { numeric: "auto", style: "narrow" });

  if (seconds < 60) return rtf.format(0, "minute");
  const minutes = seconds / 60;
  if (minutes < 60) return rtf.format(-Math.floor(minutes), "minute");
  const hours = minutes / 60;
  if (hours < 24) return rtf.format(-Math.floor(hours), "hour");
  const days = hours / 24;
  if (days < 7) return rtf.format(-Math.floor(days), "day");
  return new Date(iso).toLocaleDateString(language);
}
