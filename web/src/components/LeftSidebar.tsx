import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Database, SlidersHorizontal, Wrench } from "lucide-react";
import { useDesigner } from "../store";
import { Toolbox } from "./Toolbox";
import { DataPanel } from "./DataPanel";
import { ParametersPanel } from "./ParametersPanel";
import { ProblemsPanel } from "./ProblemsPanel";

type Tab = "toolbox" | "data" | "parameters";

/**
 * Left sidebar: Toolbox / Data source / Parameters as switchable tabs instead of one long
 * stacked scroll — each is its own concern and only one is usually needed at a time.
 * Problems stays outside the tabs, pinned at the bottom, since validation issues are worth
 * seeing regardless of which tab is open.
 */
export function LeftSidebar({ reportId }: { reportId: string | null }) {
  const [tab, setTab] = useState<Tab>("toolbox");
  const { t } = useTranslation();
  const hasReport = useDesigner((s) => !!s.report);
  const paramCount = useDesigner((s) => s.report?.parameters?.length ?? 0);
  const dataConfigured = useDesigner((s) => {
    const src = s.report?.dataSources?.[0];
    return !!src && src.kind !== "none";
  });

  if (!hasReport) {
    // Nothing to switch between yet — Data/Parameters/Problems all render nothing without
    // a report, so just show the (disabled) toolbox on its own.
    return (
      <div className="left">
        <Toolbox />
      </div>
    );
  }

  return (
    <div className="left left-tabs">
      <div className="sidebar-tabbar" role="tablist" aria-label={t("toolbox.panels")}>
        <button role="tab" aria-selected={tab === "toolbox"} className={tab === "toolbox" ? "on" : ""} onClick={() => setTab("toolbox")}>
          <Wrench size={15} />
          <span>{t("toolbox.tab")}</span>
        </button>
        <button role="tab" aria-selected={tab === "data"} className={tab === "data" ? "on" : ""} onClick={() => setTab("data")}>
          <Database size={15} />
          <span>{t("toolbox.data")}</span>
          {dataConfigured && <span className="tab-dot" aria-label="configured" />}
        </button>
        <button role="tab" aria-selected={tab === "parameters"} className={tab === "parameters" ? "on" : ""} onClick={() => setTab("parameters")}>
          <SlidersHorizontal size={15} />
          <span>{t("toolbox.params")}</span>
          {paramCount > 0 && <span className="count-badge">{paramCount}</span>}
        </button>
      </div>

      <div className="sidebar-tab-content">
        {tab === "toolbox" && <Toolbox />}
        {tab === "data" && <DataPanel key={reportId ?? "none"} />}
        {tab === "parameters" && <ParametersPanel />}
      </div>

      <div className="sidebar-problems">
        <ProblemsPanel />
      </div>
    </div>
  );
}
