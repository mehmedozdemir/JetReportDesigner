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
  const hasReport = useDesigner((s) => !!s.report?.body);

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
            onClick={() => addElement(t.type, 60, 60)}
            title={`Drag onto the page, or click to drop at 60,60`}
          >
            {t.label}
          </button>
        ))}
      </div>
    </div>
  );
}
