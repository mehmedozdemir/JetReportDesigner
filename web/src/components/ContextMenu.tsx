import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { Check, ChevronRight, type LucideIcon } from "lucide-react";

export type MenuItem =
  | { sep: true }
  | {
      label: string;
      onClick?: () => void;
      icon?: LucideIcon;
      children?: MenuItem[];
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

  return createPortal(
    <div
      ref={ref}
      className="ctx-menu"
      style={{ left: pos.x, top: pos.y }}
      onContextMenu={(e) => e.preventDefault()}
    >
      <MenuList items={items} onClose={onClose} />
    </div>,
    document.body,
  );
}

function MenuList({ items, onClose }: { items: MenuItem[]; onClose: () => void }) {
  const [openSub, setOpenSub] = useState<number | null>(null);
  const subRef = useRef<HTMLDivElement>(null);
  const [flip, setFlip] = useState(false);
  const hasChecks = items.some((it) => "checked" in it && it.checked !== undefined);

  useLayoutEffect(() => {
    if (openSub === null) {
      setFlip(false);
      return;
    }
    const r = subRef.current?.getBoundingClientRect();
    if (r) setFlip(r.right > window.innerWidth - 6);
  }, [openSub]);

  return (
    <>
      {items.map((it, i) =>
        "sep" in it ? (
          <div key={i} className="ctx-sep" />
        ) : it.children ? (
          <div
            key={i}
            className="ctx-row"
            onMouseEnter={() => setOpenSub(i)}
            onMouseLeave={() => setOpenSub((cur) => (cur === i ? null : cur))}
          >
            <button
              type="button"
              className="ctx-item"
              disabled={it.disabled}
              onClick={() => setOpenSub((cur) => (cur === i ? null : i))}
            >
              {hasChecks && <span className="ctx-check" />}
              {it.icon && <it.icon size={14} />}
              <span>{it.label}</span>
              <ChevronRight size={13} className="ctx-caret" />
            </button>
            {openSub === i && (
              <div ref={subRef} className={`ctx-menu ctx-sub${flip ? " flip" : ""}`}>
                <MenuList items={it.children} onClose={onClose} />
              </div>
            )}
          </div>
        ) : (
          <button
            key={i}
            type="button"
            className={`ctx-item${it.danger ? " danger" : ""}`}
            disabled={it.disabled}
            onClick={() => {
              it.onClick?.();
              onClose();
            }}
          >
            {hasChecks && <span className="ctx-check">{it.checked ? <Check size={13} /> : null}</span>}
            {it.icon && <it.icon size={14} />}
            <span>{it.label}</span>
          </button>
        ),
      )}
    </>
  );
}
