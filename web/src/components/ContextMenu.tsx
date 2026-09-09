import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { Check, type LucideIcon } from "lucide-react";

export type MenuItem =
  | { sep: true }
  | {
      label: string;
      onClick: () => void;
      icon?: LucideIcon;
      checked?: boolean;
      disabled?: boolean;
      danger?: boolean;
    };

export function ContextMenu({
  x,
  y,
  items,
  onClose,
}: {
  x: number;
  y: number;
  items: MenuItem[];
  onClose: () => void;
}) {
  const ref = useRef<HTMLDivElement>(null);
  const [pos, setPos] = useState({ x, y });

  useLayoutEffect(() => {
    const el = ref.current;
    if (!el) return;
    const r = el.getBoundingClientRect();
    setPos({
      x: Math.max(6, Math.min(x, window.innerWidth - r.width - 6)),
      y: Math.max(6, Math.min(y, window.innerHeight - r.height - 6)),
    });
  }, [x, y]);

  useEffect(() => {
    const away = (e: Event) => {
      if (!ref.current?.contains(e.target as Node)) onClose();
    };
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("pointerdown", away, true);
    window.addEventListener("blur", onClose);
    window.addEventListener("resize", onClose);
    document.addEventListener("scroll", onClose, true);
    window.addEventListener("keydown", onKey);
    return () => {
      window.removeEventListener("pointerdown", away, true);
      window.removeEventListener("blur", onClose);
      window.removeEventListener("resize", onClose);
      document.removeEventListener("scroll", onClose, true);
      window.removeEventListener("keydown", onKey);
    };
  }, [onClose]);

  const hasChecks = items.some((it) => "checked" in it && it.checked !== undefined);

  return createPortal(
    <div
      ref={ref}
      className="ctx-menu"
      style={{ left: pos.x, top: pos.y }}
      onContextMenu={(e) => e.preventDefault()}
    >
      {items.map((it, i) =>
        "sep" in it ? (
          <div key={i} className="ctx-sep" />
        ) : (
          <button
            key={i}
            type="button"
            className={`ctx-item${it.danger ? " danger" : ""}`}
            disabled={it.disabled}
            onClick={() => {
              it.onClick();
              onClose();
            }}
          >
            {hasChecks && <span className="ctx-check">{it.checked ? <Check size={13} /> : null}</span>}
            {it.icon && <it.icon size={14} />}
            <span>{it.label}</span>
          </button>
        ),
      )}
    </div>,
    document.body,
  );
}
