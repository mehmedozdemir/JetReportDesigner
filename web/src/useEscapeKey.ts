import { useEffect } from "react";

/** Closes a dialog on Escape. Every modal in the app should take it — only the settings dialog
 * used to, so the others trapped you into reaching for the mouse. */
export function useEscapeKey(onEscape: () => void) {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onEscape();
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onEscape]);
}
