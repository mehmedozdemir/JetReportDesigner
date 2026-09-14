import type { ElementType, ReportDefinition } from "../types";
import { pageDimensions } from "../types";

/** Rough visual family used to color/pattern a thumbnail box — not the real
 * element style, just enough to tell a table from a label at a glance. */
function elementFamily(type: ElementType): "text" | "data" | "graphic" {
  switch (type) {
    case "label":
    case "field":
    case "pageInfo":
      return "text";
    case "table":
    case "matrix":
    case "chart":
    case "subreport":
      return "data";
    default:
      return "graphic";
  }
}

interface ThumbBox {
  key: string;
  x: number;
  y: number;
  width: number;
  height: number;
  family: "text" | "data" | "graphic";
}

function layoutBoxes(definition: ReportDefinition): ThumbBox[] {
  const boxes: ThumbBox[] = [];
  if (definition.layoutMode === "free") {
    for (const el of definition.body?.elements ?? []) {
      boxes.push({ key: el.id, ...el.bounds, family: elementFamily(el.type) });
    }
    return boxes;
  }

  let y = 0;
  for (const band of definition.bands) {
    if (band.visible) {
      for (const el of band.elements) {
        boxes.push({
          key: el.id,
          x: el.bounds.x,
          y: y + el.bounds.y,
          width: el.bounds.width,
          height: el.bounds.height,
          family: elementFamily(el.type),
        });
      }
    }
    y += band.height;
  }
  return boxes;
}

/** A tiny, purely visual approximation of a report's layout — actual element
 * positions and sizes scaled down into a fixed box, colored by rough element
 * family (text / data / graphic). Not a real render; just enough shape for a
 * template gallery card to look different from its neighbors. */
export function ReportThumbnail({ definition, className }: { definition: ReportDefinition; className?: string }) {
  const { width, height } = pageDimensions(definition.page);
  const originX = definition.page.margins.left;
  const originY = definition.page.margins.top;
  const boxes = layoutBoxes(definition);

  return (
    <svg
      className={`report-thumb${className ? ` ${className}` : ""}`}
      viewBox={`0 0 ${width} ${height}`}
      preserveAspectRatio="xMidYMin slice"
      aria-hidden="true"
    >
      <rect x={0} y={0} width={width} height={height} className="thumb-page" />
      {boxes.map((b) => (
        <rect
          key={b.key}
          x={originX + b.x}
          y={originY + b.y}
          width={Math.max(b.width, 4)}
          height={Math.max(b.height, 4)}
          rx={2}
          className={`thumb-el thumb-el-${b.family}`}
        />
      ))}
    </svg>
  );
}
