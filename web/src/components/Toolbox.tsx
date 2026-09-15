import { useTranslation } from "react-i18next";
import { BarChart3, FileStack, Grid3x3, Hash, Image, Minus, QrCode, Square, Table, Type, Variable, Wrench } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import type { ElementType } from "../types";
import { useDesigner } from "../store";

const TOOLS: { type: ElementType; labelKey: string; Icon: LucideIcon }[] = [
  { type: "label", labelKey: "toolbox.label", Icon: Type },
  { type: "field", labelKey: "toolbox.field", Icon: Variable },
  { type: "table", labelKey: "toolbox.table", Icon: Table },
  { type: "chart", labelKey: "toolbox.chart", Icon: BarChart3 },
  { type: "subreport", labelKey: "toolbox.subreport", Icon: FileStack },
  { type: "barcode", labelKey: "toolbox.barcode", Icon: QrCode },
  { type: "matrix", labelKey: "toolbox.matrix", Icon: Grid3x3 },
  { type: "rectangle", labelKey: "toolbox.rectangle", Icon: Square },
  { type: "line", labelKey: "toolbox.line", Icon: Minus },
  { type: "image", labelKey: "toolbox.image", Icon: Image },
  { type: "pageInfo", labelKey: "toolbox.pageInfo", Icon: Hash },
];

export function Toolbox() {
  const { t } = useTranslation();
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
        <Wrench /> {t("toolbox.tab")}
      </h2>
      <div className="tool-grid">
        {TOOLS.map(({ type, labelKey, Icon }) => (
          <button
            key={type}
            className="tool"
            disabled={!hasReport}
            draggable={hasReport}
            onDragStart={(e) => e.dataTransfer.setData("application/x-tool", type)}
            onClick={() => add(type)}
            title={`${t(labelKey)} — ${hint}`}
            aria-label={t(labelKey)}
          >
            <Icon size={15} />
            {t(labelKey)}
          </button>
        ))}
      </div>
    </div>
  );
}
