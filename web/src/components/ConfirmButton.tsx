import { useEffect, useState } from "react";
import type { LucideIcon } from "lucide-react";

/** A destructive action that asks first, in place. Reports and folders already did this with
 * their own inline "Delete X?" row; this is the same idea for the one-click actions that
 * didn't — revoking a share link people are already using, deleting a schedule, revoking an
 * invite, removing the mail account. Disarms itself after a few seconds so a row never sits
 * half-committed. */
export function ConfirmButton({
  icon: Icon,
  label,
  title,
  confirmLabel = "Confirm",
  onConfirm,
}: {
  icon: LucideIcon;
  /** Omit for an icon-only button (`title` then carries the accessible name). */
  label?: string;
  title: string;
  confirmLabel?: string;
  onConfirm: () => void;
}) {
  const [armed, setArmed] = useState(false);

  useEffect(() => {
    if (!armed) return;
    const t = window.setTimeout(() => setArmed(false), 5000);
    return () => window.clearTimeout(t);
  }, [armed]);

  if (!armed) {
    return (
      <button className="mini danger" title={title} aria-label={title} onClick={() => setArmed(true)}>
        <Icon size={13} />
        {label}
      </button>
    );
  }

  return (
    <span className="row" style={{ gap: 4 }}>
      <button
        className="mini danger"
        autoFocus
        onClick={() => {
          setArmed(false);
          onConfirm();
        }}
      >
        {confirmLabel}
      </button>
      <button className="mini" onClick={() => setArmed(false)}>
        Cancel
      </button>
    </span>
  );
}
