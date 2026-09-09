import type { CSSProperties } from "react";
import type { BackgroundFit, BackgroundImageSpec } from "./types";

const ASSET_PREFIX = "asset:";

/** Resolve an image reference (`asset:{id}`, an http(s) URL or a data URI) to a usable src. */
export function imageSrc(ref: string | null | undefined): string {
  if (!ref) return "";
  return ref.startsWith(ASSET_PREFIX) ? `/api/assets/${ref.slice(ASSET_PREFIX.length)}` : ref;
}

/** CSS `background-*` shorthand values for a background image spec, or null when there is none. */
export function backgroundImageCss(
  spec: BackgroundImageSpec | null | undefined,
): CSSProperties | null {
  if (!spec?.source) return null;
  const url = `url("${imageSrc(spec.source)}")`;
  if (spec.fit === "tile") {
    return { backgroundImage: url, backgroundRepeat: "repeat", backgroundPosition: "top left" };
  }
  return {
    backgroundImage: url,
    backgroundRepeat: "no-repeat",
    backgroundPosition: "center",
    backgroundSize: spec.fit === "fill" ? "100% 100%" : spec.fit,
  };
}

export const FIT_LABELS: Record<BackgroundFit, string> = {
  cover: "Cover",
  contain: "Contain",
  fill: "Stretch",
  tile: "Tile",
};
