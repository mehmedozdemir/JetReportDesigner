import type { ReactNode } from "react";

/** One page title per screen, with the page's own actions beside it.
 *
 * Before this, a page's name was just an <h3> — and `.start-section > h3` is a *direct child*
 * selector, so Jobs and Reports (whose h3 sat inside a wrapper div) rendered a big default
 * heading while Team/Email/Schedules rendered an 11px grey uppercase label. Same thing,
 * two completely different looks, which is most of why the screens didn't feel like one app.
 * Section headings keep the small uppercase label style; the page name is this. */
export function PageHeader({
  title,
  description,
  actions,
  narrow,
}: {
  title: string;
  description?: string;
  actions?: ReactNode;
  /** Match the 640px cap that form pages (Email, Settings) use for their sections. */
  narrow?: boolean;
}) {
  return (
    <header className={`page-head${narrow ? " page-head-narrow" : ""}`}>
      <div className="page-head-text">
        <h1 className="page-title">{title}</h1>
        {description && <p className="page-desc">{description}</p>}
      </div>
      {actions && <div className="page-head-actions">{actions}</div>}
    </header>
  );
}
