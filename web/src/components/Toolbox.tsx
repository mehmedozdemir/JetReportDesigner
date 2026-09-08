import type { ElementType } from "../types";
import { useDesigner } from "../store";

const TOOLS: { type: ElementType; label: string }[] = [
  { type: "label", label: "Label" },
  { type: "field", label: "Field" },
  { type: "rectangle", label: "Rectangle" },
  { type: "line", label: "Line" },
  { type: "image", label: "Image" },
  { type: "pageInfo", label: "Page info" },
];

export function Toolbox() {
  const addElement = useDesigner((s) => s.addElement);
  const report = useDesigner((s) => s.report);
  const selectedBand = useDesigner((s) => s.selectedBand);
  const hasReport = !!report;

  const add = (type: ElementType) => {
    const location =
      report?.layoutMode === "banded" && selectedBand !== null
        ? ({ container: "band", bandIndex: selectedBand } as const)
        : undefined;
    addElement(type, 40, 8, location);
  };

  const hint =
    report?.layoutMode === "banded"
      ? selectedBand !== null
        ? "Adds to the selected band"
        : "Adds to the detail band (or drag onto a band)"
      : "Drag onto the page, or click to drop at 40,8";

  return (
    <div className="panel">
      <h2>Toolbox</h2>
      <div className="tool-grid">
        {TOOLS.map((t) => (
          <button
            key={t.type}
            className="tool"
            disabled={!hasReport}
            draggable={hasReport}
            onDragStart={(e) => e.dataTransfer.setData("application/x-tool", t.type)}
            onClick={() => add(t.type)}
            title={hint}
          >
            {t.label}
          </button>
        ))}
      </div>
    </div>
  );
}
