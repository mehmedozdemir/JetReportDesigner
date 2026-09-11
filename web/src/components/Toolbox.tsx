import { BarChart3, FileStack, Hash, Image, Minus, Square, Table, Type, Variable, Wrench } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import type { ElementType } from "../types";
import { useDesigner } from "../store";

const TOOLS: { type: ElementType; label: string; Icon: LucideIcon }[] = [
  { type: "label", label: "Label", Icon: Type },
  { type: "field", label: "Field", Icon: Variable },
  { type: "table", label: "Table", Icon: Table },
  { type: "chart", label: "Chart", Icon: BarChart3 },
  { type: "subreport", label: "Subreport", Icon: FileStack },
  { type: "rectangle", label: "Rectangle", Icon: Square },
  { type: "line", label: "Line", Icon: Minus },
  { type: "image", label: "Image", Icon: Image },
  { type: "pageInfo", label: "Page info", Icon: Hash },
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
        : "Adds to the detail band — or drag onto any band"
      : "Click to add at 40,8 — or drag onto the page";

  return (
    <div className="panel">
      <h2>
        <Wrench /> Toolbox
      </h2>
      <div className="tool-grid">
        {TOOLS.map(({ type, label, Icon }) => (
          <button
            key={type}
            className="tool"
            disabled={!hasReport}
            draggable={hasReport}
            onDragStart={(e) => e.dataTransfer.setData("application/x-tool", type)}
            onClick={() => add(type)}
            title={`${label} — ${hint}`}
            aria-label={`Add ${label}`}
          >
            <Icon size={15} />
            {label}
          </button>
        ))}
      </div>
    </div>
  );
}
