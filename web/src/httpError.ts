/** Pull a readable message out of an RFC 7807 ProblemDetails body, falling back to the raw text. */
export function problemMessage(body: string): string {
  try {
    const p = JSON.parse(body) as {
      title?: string;
      detail?: string;
      errors?: Record<string, string[] | string>;
    };
    const fieldErrors = p.errors
      ? Object.values(p.errors)
          .flatMap((v) => (Array.isArray(v) ? v : [v]))
          .filter(Boolean)
      : [];
    const parts = [p.title ?? p.detail, ...fieldErrors].filter(Boolean) as string[];
    return parts.length ? parts.join(" — ") : body;
  } catch {
    return body;
  }
}
