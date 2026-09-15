import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import type { LucideIcon } from "lucide-react";
import {
  AlignCenter,
  AlignCenterHorizontal,
  AlignEndHorizontal,
  AlignLeft,
  AlignRight,
  AlignStartHorizontal,
  ArrowDown,
  ArrowDownToLine,
  ArrowUp,
  ArrowUpToLine,
  Ban,
  BarChart3,
  BoxSelect,
  FileStack,
  FileText,
  Grid3x3,
  Hash,
  Image as ImageIcon,
  Layers,
  ListChecks,
  Minus,
  MousePointerSquareDashed,
  PanelBottom,
  PanelLeft,
  PanelRight,
  PanelTop,
  Plus,
  QrCode,
  Rows3,
  Square,
  Table as TableIcon,
  Trash2,
  Type as TypeIcon,
  Variable,
} from "lucide-react";
import { useDesigner } from "../store";
import { FormatField } from "./FormatDialog";
import { FormulaField } from "./FormulaDialog";
import { ConditionalFormatDialog } from "./ConditionalFormatDialog";
import { ImagePicker } from "./ImagePicker";
import { SubreportPicker } from "./SubreportPicker";
import type {
  AggregateFunction,
  BackgroundFit,
  Band,
  BarcodeSymbology,
  BorderSpec,
  ChartType,
  ElementType,
  FormatRule,
  PageSize,
  ReportElement,
  TextAlign,
  VerticalAlign,
} from "../types";

const EMPTY_RULES: FormatRule[] = [];

export const PAGE_SIZES: [PageSize, string][] = [
  ["A4", "A4"],
  ["A5", "A5"],
  ["A6", "A6"],
  ["Letter", "Letter"],
  ["Legal", "Legal"],
  ["IDCard", "ID / credit card (85.6×54 mm)"],
  ["Badge", "Badge (105×74 mm)"],
];

/** "Conditional formatting" button + rule count, opening the editor dialog. */
function ConditionalFormatButton({
  rules,
  fields,
  allowHidden,
  onChange,
}: {
  rules: FormatRule[];
  fields: string[];
  allowHidden: boolean;
  onChange: (rules: FormatRule[]) => void;
}) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button className="mini" style={{ width: "100%", marginTop: 4 }} onClick={() => setOpen(true)}>
        <ListChecks /> Conditional formatting
        {rules.length > 0 && <span className="count-badge">{rules.length}</span>}
      </button>
      {open && (
        <ConditionalFormatDialog
          rules={rules}
          fields={fields}
          allowHidden={allowHidden}
          onChange={onChange}
          onClose={() => setOpen(false)}
        />
      )}
    </>
  );
}

const ELEMENT_ICON: Record<ElementType, LucideIcon> = {
  label: TypeIcon,
  field: Variable,
  table: TableIcon,
  rectangle: Square,
  line: Minus,
  image: ImageIcon,
  pageInfo: Hash,
  chart: BarChart3,
  subreport: FileStack,
  barcode: QrCode,
  matrix: Grid3x3,
};

export function PropertiesPanel() {
  const report = useDesigner((s) => s.report);
  const selectedIds = useDesigner((s) => s.selectedIds);
  const selectedBand = useDesigner((s) => s.selectedBand);
  const mutateElement = useDesigner((s) => s.mutateElement);
  if (!report) return null;

  if (selectedBand !== null && report.bands[selectedBand]) {
    return <BandProperties index={selectedBand} />;
  }

  if (selectedIds.length === 1) {
    const id = selectedIds[0];
    const el =
      report.body?.elements.find((e) => e.id === id) ??
      report.bands.flatMap((b) => b.elements).find((e) => e.id === id);
    if (el) {
      return <ElementProperties element={el} onPatch={(fn) => mutateElement(id, fn)} />;
    }
  }

  if (selectedIds.length > 1) {
    return <MultiProperties />;
  }

  return <PageProperties />;
}

export const FONT_FAMILIES = [
  "Helvetica",
  "Arial",
  "Verdana",
  "Times New Roman",
  "Georgia",
  "Courier New",
  "Consolas",
];

function FontFamilySelect({
  value,
  onChange,
}: {
  value: string | null | undefined;
  onChange: (v: string | null) => void;
}) {
  const { t } = useTranslation();
  const known = value && FONT_FAMILIES.includes(value);
  return (
    <label className="field">
      <span>{t("props.font")}</span>
      <select value={known ? (value as string) : ""} onChange={(e) => onChange(e.target.value || null)}>
        <option value="">— inherit{value && !known ? ` (${value})` : ""} —</option>
        {FONT_FAMILIES.map((f) => (
          <option key={f} value={f}>{f}</option>
        ))}
      </select>
    </label>
  );
}

/** The value shared by every element (via getter), or undefined when they differ. */
export function common<T>(elements: ReportElement[], getter: (el: ReportElement) => T): T | undefined {
  if (elements.length === 0) return undefined;
  const first = getter(elements[0]);
  return elements.every((el) => Object.is(getter(el), first)) ? first : undefined;
}

function MultiProperties() {
  const { t } = useTranslation();
  const report = useDesigner((s) => s.report);
  const selectedIds = useDesigner((s) => s.selectedIds);
  const mutateSelected = useDesigner((s) => s.mutateSelected);

  const elements = useMemo(() => {
    if (!report) return [] as ReportElement[];
    const all = [...(report.body?.elements ?? []), ...report.bands.flatMap((b) => b.elements)];
    const set = new Set(selectedIds);
    return all.filter((e) => set.has(e.id));
  }, [report, selectedIds]);

  const textCount = elements.filter((e) => e.type === "label" || e.type === "field" || e.type === "pageInfo").length;
  const fontFamily = common(elements, (e) => e.style?.font?.family ?? null);
  const fontSize = common(elements, (e) => e.style?.font?.size ?? null);
  const bold = common(elements, (e) => !!e.style?.font?.bold);
  const italic = common(elements, (e) => !!e.style?.font?.italic);
  const align = common(elements, (e) => (e.style?.align as TextAlign | undefined) ?? undefined);
  const vAlign = common(elements, (e) => (e.style?.vAlign as VerticalAlign | undefined) ?? undefined);
  const color = common(elements, (e) => e.style?.color ?? null);
  const borderMixed = common(elements, (e) => JSON.stringify(e.style?.border ?? null)) === undefined;

  return (
    <div className="panel">
      <h2><BoxSelect /> {t("designer.selected", { count: selectedIds.length })}</h2>
      <p className="hint">{t("designer.multiSelect")}</p>

      {textCount > 0 && (
        <>
          <FontFamilySelect
            value={fontFamily ?? ""}
            onChange={(v) => mutateSelected((e) => setFont(e, "family", v))}
          />
          <div className="grid2">
            <label className="field">
              <span>Size (pt){fontSize === undefined ? " — mixed" : ""}</span>
              <input
                type="number"
                value={fontSize ?? ""}
                placeholder="mixed"
                onChange={(e) => e.target.value && mutateSelected((el) => setFont(el, "size", Number(e.target.value)))}
              />
            </label>
            <div className="field">
              <span>{t("props.style")}</span>
              <div className="row">
                <Toggle label="B" active={!!bold} onClick={() => mutateSelected((e) => setFont(e, "bold", !bold))} />
                <Toggle label="I" active={!!italic} onClick={() => mutateSelected((e) => setFont(e, "italic", !italic))} />
              </div>
            </div>
          </div>
          <div className="row" style={{ flexWrap: "wrap", marginBottom: 10 }}>
            <AlignPicker value={align ?? "left"} onChange={(v) => mutateSelected((e) => setStyle(e, "align", v))} />
            <span className="align-divider" />
            <VAlignPicker value={vAlign ?? "top"} onChange={(v) => mutateSelected((e) => setStyle(e, "vAlign", v))} />
          </div>
        </>
      )}

      <label className="field">
        <span>Color{color === undefined ? " — mixed" : ""}</span>
        <input type="color" value={color ?? "#111827"} onChange={(e) => mutateSelected((el) => setStyle(el, "color", e.target.value))} />
      </label>

      <BorderPicker
        border={elements[0]?.style?.border ?? null}
        mixed={borderMixed}
        onChange={(next) => mutateSelected((el) => setStyle(el, "border", next))}
      />
    </div>
  );
}

function BandProperties({ index }: { index: number }) {
  const { t } = useTranslation();
  const band = useDesigner((s) => s.report!.bands[index]) as Band;
  const sources = useDesigner((s) => s.report!.dataSources);
  const patchBand = useDesigner((s) => s.patchBand);
  const removeBand = useDesigner((s) => s.removeBand);
  const moveBand = useDesigner((s) => s.moveBand);
  const bandCount = useDesigner((s) => s.report!.bands.length);
  const addGroupLevel = useDesigner((s) => s.addGroupLevel);
  const isGroup = band.type === "groupHeader" || band.type === "groupFooter";
  const groupLevelCount = useDesigner((s) =>
    new Set(s.report!.bands.filter((b) => b.type === "groupHeader" || b.type === "groupFooter").map((b) => b.groupLevel ?? 0)).size,
  );

  return (
    <div className="panel">
      <h2>
        <Rows3 /> {titleCase(band.type)} band
        {isGroup && groupLevelCount > 1 && <span className="count-badge">level {band.groupLevel ?? 0}</span>}
      </h2>
      <div className="row" style={{ marginBottom: 8 }}>
        <button className="mini" onClick={() => moveBand(index, -1)} disabled={index === 0} title={t("props.moveBandUp")} aria-label={t("props.moveBandUp")}>
          <ArrowUp />
        </button>
        <button className="mini" onClick={() => moveBand(index, 1)} disabled={index === bandCount - 1} title={t("props.moveBandDown")} aria-label={t("props.moveBandDown")}>
          <ArrowDown />
        </button>
        <button className="mini danger" onClick={() => removeBand(index)} title={t("props.deleteBand")}>
          <Trash2 /> Delete
        </button>
      </div>

      <label className="field">
        <span>{t("props.height")}</span>
        <input
          type="number"
          value={Math.round(band.height)}
          onChange={(e) => patchBand(index, (b) => (b.height = Math.max(8, Number(e.target.value) || 8)))}
        />
      </label>

      {band.type === "detail" && (
        <label className="field">
          <span>{t("props.dataSource")}</span>
          <select
            value={band.dataSource ?? ""}
            onChange={(e) => patchBand(index, (b) => (b.dataSource = e.target.value || undefined))}
          >
            <option value="">—</option>
            {sources.map((s) => (
              <option key={s.name}>{s.name}</option>
            ))}
          </select>
        </label>
      )}

      {isGroup && (
        <>
          <label className="field">
            <span>{t("props.groupBy")}</span>
            <input
              value={band.group?.expression ?? ""}
              placeholder="{orders.customer}"
              onChange={(e) =>
                patchBand(index, (b) => {
                  b.group ??= { dataSource: sources[0]?.name ?? "", expression: "", sort: "asc" };
                  b.group.expression = e.target.value;
                })
              }
            />
          </label>
          <label className="field">
            <span>{t("props.sort")}</span>
            <select
              value={band.group?.sort ?? "asc"}
              onChange={(e) =>
                patchBand(index, (b) => {
                  b.group ??= { dataSource: sources[0]?.name ?? "", expression: "", sort: "asc" };
                  b.group.sort = e.target.value as "asc" | "desc";
                })
              }
            >
              <option value="asc">{t("props.ascending")}</option>
              <option value="desc">{t("props.descending")}</option>
            </select>
          </label>
          <button className="mini" style={{ width: "100%", marginBottom: 6 }} onClick={() => addGroupLevel()}>
            <Plus /> Add nested group
          </button>
        </>
      )}

      {(band.type === "groupHeader" || band.type === "reportHeader" || band.type === "pageHeader") && (
        <label className="row" style={{ marginTop: 6 }}>
          <input
            type="checkbox"
            checked={band.repeatOnEveryPage}
            onChange={(e) => patchBand(index, (b) => (b.repeatOnEveryPage = e.target.checked))}
          />
          <span>{t("props.repeatEveryPage")}</span>
        </label>
      )}

      <ImagePicker
        label={t("props.backgroundImage")}
        value={band.backgroundImage}
        onChange={(v) => patchBand(index, (b) => (b.backgroundImage = v))}
      />

      <ConditionalFormatButton
        rules={band.formatRules ?? EMPTY_RULES}
        fields={sources.flatMap((s) => s.fields.map((f) => f.name))}
        allowHidden={false}
        onChange={(next) => patchBand(index, (b) => (b.formatRules = next))}
      />
    </div>
  );
}

function ElementProperties({
  element,
  onPatch,
}: {
  element: ReportElement;
  onPatch: (fn: (el: ReportElement) => void) => void;
}) {
  const { t } = useTranslation();
  const sources = useDesigner((st) => st.report!.dataSources);
  const culture = useDesigner((st) => st.report?.culture) || undefined;
  const fieldNames = sources.flatMap((src) => src.fields.map((f) => f.name));
  const s = element.style ?? {};
  const isText = element.type === "label" || element.type === "field" || element.type === "pageInfo";

  const reorder = useDesigner((st) => st.reorderSelection);
  const Icon = ELEMENT_ICON[element.type] ?? MousePointerSquareDashed;

  return (
    <div className="panel">
      <h2><Icon /> {titleCase(element.type)}</h2>

      <div className="row" style={{ marginBottom: 8 }}>
        <button className="mini" title={t("props.sendToBackHint")} aria-label={t("props.sendToBack")} onClick={() => reorder("back")}>
          <ArrowDownToLine />
        </button>
        <button className="mini" title={t("props.sendBackwardHint")} aria-label={t("props.sendBackward")} onClick={() => reorder("backward")}>
          <ArrowDown />
        </button>
        <button className="mini" title={t("props.bringForwardHint")} aria-label={t("props.bringForward")} onClick={() => reorder("forward")}>
          <ArrowUp />
        </button>
        <button className="mini" title={t("props.bringToFrontHint")} aria-label={t("props.bringToFront")} onClick={() => reorder("front")}>
          <ArrowUpToLine />
        </button>
        <span style={{ marginLeft: "auto", color: "var(--text-secondary)", display: "flex", alignItems: "center" }} title={t("props.zOrder")}>
          <Layers size={13} />
        </span>
      </div>

      <div className="grid2">
        <Num label="X" value={element.bounds.x} onChange={(v) => onPatch((e) => (e.bounds.x = v))} />
        <Num label="Y" value={element.bounds.y} onChange={(v) => onPatch((e) => (e.bounds.y = v))} />
        <Num label="W" value={element.bounds.width} onChange={(v) => onPatch((e) => (e.bounds.width = v))} />
        <Num label="H" value={element.bounds.height} onChange={(v) => onPatch((e) => (e.bounds.height = v))} />
      </div>

      {element.type === "label" && (
        <FormulaField
          label={t("props.text")}
          value={element.text ?? ""}
          fields={fieldNames}
          onChange={(v) => onPatch((e) => (e.text = v))}
        />
      )}
      {(element.type === "field" || element.type === "pageInfo") && (
        <>
          <FormulaField
            label={t("props.valueBinding")}
            value={element.value ?? ""}
            fields={fieldNames}
            onChange={(v) => onPatch((e) => (e.value = v))}
          />
          <FormatField
            value={element.format ?? ""}
            locale={culture}
            onChange={(v) => onPatch((e) => (e.format = v || null))}
          />
        </>
      )}

      {element.type === "image" && (
        <ImagePicker
          label={t("props.image")}
          value={element.image?.source ? { source: element.image.source, fit: (element.image.fit as BackgroundFit) ?? "contain" } : null}
          fitOptions={["contain", "cover", "fill"]}
          onChange={(v) => onPatch((e) => (e.image = v ? { source: v.source, fit: v.fit } : null))}
        />
      )}

      {element.type === "chart" && element.chart && (
        <div className="table-props">
          <label className="field">
            <span>{t("props.chartType")}</span>
            <select
              value={element.chart.type}
              onChange={(v) => onPatch((e) => (e.chart!.type = v.target.value as ChartType))}
            >
              <option value="column">{t("props.column")}</option>
              <option value="bar">{t("props.bar")}</option>
              <option value="line">{t("props.line")}</option>
              <option value="area">{t("props.area")}</option>
              <option value="pie">{t("props.pie")}</option>
            </select>
          </label>
          <label className="field">
            <span>{t("props.dataSource")}</span>
            <select
              value={element.chart.dataSource}
              onChange={(v) => onPatch((e) => (e.chart!.dataSource = v.target.value))}
            >
              <option value="">— (first source)</option>
              {sources.map((src) => (
                <option key={src.name}>{src.name}</option>
              ))}
            </select>
          </label>
          <FormulaField
            label={t("props.category")}
            value={element.chart.category}
            fields={fieldNames}
            onChange={(val) => onPatch((e) => (e.chart!.category = val))}
          />
          <label className="field">
            <span>{t("props.title")}</span>
            <input
              value={element.chart.title ?? ""}
              placeholder="(none)"
              onChange={(v) => onPatch((e) => (e.chart!.title = v.target.value || null))}
            />
          </label>
          <label className="row" style={{ marginBottom: 4 }}>
            <input
              type="checkbox"
              checked={element.chart.showLegend}
              onChange={(v) => onPatch((e) => (e.chart!.showLegend = v.target.checked))}
            />
            <span>{t("props.legend")}</span>
          </label>
          {element.chart.type !== "pie" && (
            <label className="row" style={{ marginBottom: 6 }}>
              <input
                type="checkbox"
                checked={element.chart.showGrid}
                onChange={(v) => onPatch((e) => (e.chart!.showGrid = v.target.checked))}
              />
              <span>{t("props.gridlines")}</span>
            </label>
          )}

          <h3>Series{element.chart.type === "pie" ? " (pie uses the first)" : ""}</h3>
          {element.chart.series.map((s, i) => (
            <div key={i} className="col-row">
              <input
                value={s.name}
                placeholder={t("props.name")}
                onChange={(v) => onPatch((e) => (e.chart!.series[i].name = v.target.value))}
              />
              <input
                value={s.value}
                placeholder="{src.field}"
                onChange={(v) => onPatch((e) => (e.chart!.series[i].value = v.target.value))}
              />
              <div className="row">
                <input
                  type="color"
                  style={{ width: 34, padding: 0 }}
                  value={s.color ?? "#2563eb"}
                  onChange={(v) => onPatch((e) => (e.chart!.series[i].color = v.target.value))}
                />
                <button
                  className="mini"
                  title={t("props.usePalette")}
                  onClick={() => onPatch((e) => (e.chart!.series[i].color = null))}
                >
                  <Ban />
                </button>
                <button
                  className="mini danger"
                  disabled={element.chart!.series.length <= 1}
                  onClick={() => onPatch((e) => e.chart!.series.splice(i, 1))}
                  aria-label={t("props.removeSeries")}
                >
                  <Trash2 />
                </button>
              </div>
            </div>
          ))}
          {element.chart.type !== "pie" && (
            <button
              className="mini"
              onClick={() =>
                onPatch((e) =>
                  e.chart!.series.push({ name: `Series ${e.chart!.series.length + 1}`, value: "{src.field}", color: null }),
                )
              }
            >
              <Plus /> Series
            </button>
          )}
        </div>
      )}

      {element.type === "subreport" && element.subreport && (
        <div className="table-props">
          <SubreportPicker
            value={element.subreport}
            fieldNames={fieldNames}
            onChange={(next) => onPatch((e) => (e.subreport = next))}
          />
        </div>
      )}

      {element.type === "barcode" && element.barcode && (
        <div className="table-props">
          <label className="field">
            <span>{t("props.symbology")}</span>
            <select
              value={element.barcode.symbology}
              onChange={(v) => onPatch((e) => (e.barcode!.symbology = v.target.value as BarcodeSymbology))}
            >
              <option value="qr">{t("props.qrCode")}</option>
              <option value="dataMatrix">{t("props.dataMatrix")}</option>
              <option value="code128">Code 128</option>
              <option value="ean13">EAN-13</option>
              <option value="code39">Code 39</option>
            </select>
          </label>
          <FormulaField
            label={t("props.value")}
            value={element.barcode.value}
            fields={fieldNames}
            onChange={(v) => onPatch((e) => (e.barcode!.value = v))}
          />
          <div className="grid2">
            <Color label={t("props.bars")} value={element.barcode.foreColor} onChange={(v) => onPatch((e) => (e.barcode!.foreColor = v))} />
            <Color label={t("props.background")} value={element.barcode.backColor} onChange={(v) => onPatch((e) => (e.barcode!.backColor = v))} />
          </div>
          {element.barcode.symbology !== "qr" && element.barcode.symbology !== "dataMatrix" && (
            <label className="row" style={{ marginBottom: 6 }}>
              <input
                type="checkbox"
                checked={element.barcode.showText}
                onChange={(v) => onPatch((e) => (e.barcode!.showText = v.target.checked))}
              />
              <span>{t("props.showValueBelowBars")}</span>
            </label>
          )}
        </div>
      )}

      {element.type === "matrix" && element.matrix && (
        <div className="table-props">
          <label className="field">
            <span>{t("props.dataSource")}</span>
            <select
              value={element.matrix.dataSource}
              onChange={(v) => onPatch((e) => (e.matrix!.dataSource = v.target.value))}
            >
              <option value="">— (first source)</option>
              {sources.map((src) => (
                <option key={src.name}>{src.name}</option>
              ))}
            </select>
          </label>
          <FormulaField
            label={t("props.rowField")}
            value={element.matrix.rowField}
            fields={fieldNames}
            onChange={(v) => onPatch((e) => (e.matrix!.rowField = v))}
          />
          <label className="field">
            <span>{t("props.rowHeader")}</span>
            <input
              value={element.matrix.rowHeader ?? ""}
              placeholder="(field name)"
              onChange={(v) => onPatch((e) => (e.matrix!.rowHeader = v.target.value || null))}
            />
          </label>
          <FormulaField
            label={t("props.columnField")}
            value={element.matrix.columnField}
            fields={fieldNames}
            onChange={(v) => onPatch((e) => (e.matrix!.columnField = v))}
          />
          <FormulaField
            label={t("props.valueField")}
            value={element.matrix.valueField}
            fields={fieldNames}
            onChange={(v) => onPatch((e) => (e.matrix!.valueField = v))}
          />
          <div className="grid2">
            <label className="field">
              <span>{t("props.aggregate")}</span>
              <select
                value={element.matrix.aggregate}
                onChange={(v) => onPatch((e) => (e.matrix!.aggregate = v.target.value as AggregateFunction))}
              >
                <option value="sum">{t("props.sum")}</option>
                <option value="count">{t("props.count")}</option>
                <option value="average">{t("props.average")}</option>
                <option value="min">{t("props.min")}</option>
                <option value="max">{t("props.max")}</option>
                <option value="first">{t("props.first")}</option>
                <option value="last">{t("props.last")}</option>
              </select>
            </label>
            <label className="field">
              <span>{t("props.format")}</span>
              <input
                value={element.matrix.format ?? ""}
                placeholder="n2, c, ..."
                onChange={(v) => onPatch((e) => (e.matrix!.format = v.target.value || null))}
              />
            </label>
          </div>
          <label className="row" style={{ marginBottom: 4 }}>
            <input
              type="checkbox"
              checked={element.matrix.showRowTotals}
              onChange={(v) => onPatch((e) => (e.matrix!.showRowTotals = v.target.checked))}
            />
            <span>{t("props.rowTotals")}</span>
          </label>
          <label className="row" style={{ marginBottom: 6 }}>
            <input
              type="checkbox"
              checked={element.matrix.showColumnTotals}
              onChange={(v) => onPatch((e) => (e.matrix!.showColumnTotals = v.target.checked))}
            />
            <span>{t("props.columnTotals")}</span>
          </label>
        </div>
      )}

      {element.type === "table" && element.table && (
        <div className="table-props">
          <label className="field">
            <span>{t("props.dataSource")}</span>
            <select
              value={element.table.dataSource}
              onChange={(v) => onPatch((e) => (e.table!.dataSource = v.target.value))}
            >
              <option value="">— (first source)</option>
              {sources.map((src) => (
                <option key={src.name}>{src.name}</option>
              ))}
            </select>
          </label>
          <label className="row" style={{ marginBottom: 6 }}>
            <input
              type="checkbox"
              checked={element.table.showHeader}
              onChange={(v) => onPatch((e) => (e.table!.showHeader = v.target.checked))}
            />
            <span>{t("props.headerRow")}</span>
          </label>

          <h3>{t("props.columns")}</h3>
          {element.table.columns.map((col, i) => (
            <div key={i} className="col-row">
              <input
                value={col.header}
                placeholder={t("props.header")}
                onChange={(v) => onPatch((e) => (e.table!.columns[i].header = v.target.value))}
              />
              <input
                value={col.value}
                placeholder="{src.field}"
                onChange={(v) => onPatch((e) => (e.table!.columns[i].value = v.target.value))}
              />
              <div className="row">
                <input
                  type="number"
                  style={{ width: 56 }}
                  value={Math.round(col.width)}
                  onChange={(v) => onPatch((e) => (e.table!.columns[i].width = Number(v.target.value) || 0))}
                />
                <select
                  value={col.align}
                  onChange={(v) => onPatch((e) => (e.table!.columns[i].align = v.target.value as TextAlign))}
                >
                  <option value="left">L</option>
                  <option value="center">C</option>
                  <option value="right">R</option>
                </select>
                <input
                  style={{ width: 60 }}
                  value={col.format ?? ""}
                  placeholder="fmt"
                  onChange={(v) => onPatch((e) => (e.table!.columns[i].format = v.target.value || null))}
                />
                <button className="mini danger" onClick={() => onPatch((e) => e.table!.columns.splice(i, 1))} aria-label={t("props.removeColumn")}>
                  <Trash2 />
                </button>
              </div>
            </div>
          ))}
          <button
            className="mini"
            onClick={() =>
              onPatch((e) =>
                e.table!.columns.push({ header: "Column", value: "{src.field}", width: 100, align: "left" }),
              )
            }
          >
            <Plus /> Column
          </button>
        </div>
      )}

      {isText && (
        <>
          <div className="grid2">
            <FontFamilySelect value={s.font?.family} onChange={(v) => onPatch((e) => setFont(e, "family", v))} />
            <Num label={t("props.sizePt")} value={s.font?.size ?? 10} onChange={(v) => onPatch((e) => setFont(e, "size", v))} />
          </div>
          <div className="row" style={{ flexWrap: "wrap" }}>
            <Toggle label="B" active={!!s.font?.bold} onClick={() => onPatch((e) => setFont(e, "bold", !e.style?.font?.bold))} />
            <Toggle label="I" active={!!s.font?.italic} onClick={() => onPatch((e) => setFont(e, "italic", !e.style?.font?.italic))} />
            <span className="align-divider" />
            <AlignPicker value={(s.align as TextAlign) ?? "left"} onChange={(v) => onPatch((e) => setStyle(e, "align", v))} />
            <VAlignPicker value={(s.vAlign as VerticalAlign) ?? "top"} onChange={(v) => onPatch((e) => setStyle(e, "vAlign", v))} />
          </div>
          <label className="row" style={{ marginBottom: 6 }}>
            <input
              type="checkbox"
              checked={!!element.canGrow}
              onChange={(v) => onPatch((e) => (e.canGrow = v.target.checked))}
            />
            <span>Grow to fit text (don't clip)</span>
          </label>
        </>
      )}

      <div className="grid2">
        <Color label={t("props.color")} value={s.color ?? "#111827"} onChange={(v) => onPatch((e) => setStyle(e, "color", v))} />
        <Color label={t("props.background")} value={s.background ?? "#ffffff"} onChange={(v) => onPatch((e) => setStyle(e, "background", v))} allowClear cleared={!s.background} onClear={() => onPatch((e) => setStyle(e, "background", null))} />
      </div>

      {element.type !== "image" && element.type !== "line" && (
        <ImagePicker
          label={t("props.backgroundImage")}
          value={s.backgroundImage}
          onChange={(v) => onPatch((e) => setStyle(e, "backgroundImage", v))}
        />
      )}

      <BorderPicker border={s.border} onChange={(next) => onPatch((e) => setStyle(e, "border", next))} />

      <ConditionalFormatButton
        rules={element.formatRules ?? EMPTY_RULES}
        fields={sources.flatMap((src) => src.fields.map((f) => f.name))}
        allowHidden
        onChange={(next) => onPatch((e) => (e.formatRules = next))}
      />
    </div>
  );
}

function PageProperties() {
  const { t } = useTranslation();
  const report = useDesigner((s) => s.report)!;
  const mutate = useDesigner((s) => s.mutate);
  const p = report.page;
  const set = (fn: (page: typeof p) => void) => mutate((r) => fn(r.page));

  return (
    <div className="panel">
      <h2><FileText /> {t("designer.page")}</h2>
      <label className="field">
        <span>{t("props.size")}</span>
        <select value={p.size} onChange={(e) => set((page) => (page.size = e.target.value as PageSize))}>
          {PAGE_SIZES.map(([value, label]) => (
            <option key={value} value={value}>{label}</option>
          ))}
        </select>
      </label>
      <label className="field">
        <span>{t("props.orientation")}</span>
        <select value={p.orientation} onChange={(e) => set((page) => (page.orientation = e.target.value as "portrait" | "landscape"))}>
          <option value="portrait">{t("props.portrait")}</option>
          <option value="landscape">{t("props.landscape")}</option>
        </select>
      </label>
      <div className="grid2">
        <Num label={t("props.marginT")} value={p.margins.top} onChange={(v) => set((page) => (page.margins.top = v))} />
        <Num label={t("props.marginR")} value={p.margins.right} onChange={(v) => set((page) => (page.margins.right = v))} />
        <Num label={t("props.marginB")} value={p.margins.bottom} onChange={(v) => set((page) => (page.margins.bottom = v))} />
        <Num label={t("props.marginL")} value={p.margins.left} onChange={(v) => set((page) => (page.margins.left = v))} />
      </div>

      <div className="grid2">
        <label className="field">
          <span>{t("props.detailColumns")}</span>
          <select
            value={p.columns}
            onChange={(e) => set((page) => (page.columns = Number(e.target.value)))}
          >
            {[1, 2, 3, 4, 5, 6].map((n) => (
              <option key={n} value={n}>{n}</option>
            ))}
          </select>
        </label>
        {p.columns > 1 && (
          <Num label={t("props.columnGap")} value={p.columnSpacing ?? 16} onChange={(v) => set((page) => (page.columnSpacing = v))} />
        )}
      </div>
      {p.columns > 1 && (
        <p className="hint" style={{ marginTop: -4 }}>
          The detail band flows left to right across {p.columns} columns, then wraps down — for
          mailing labels or a catalog grid. Headers, footers and group bands stay full width.
        </p>
      )}

      <ImagePicker
        label={t("props.backgroundImage")}
        value={p.backgroundImage}
        onChange={(v) => set((page) => (page.backgroundImage = v))}
      />

      <label className="field">
        <span>{t("props.culture")}</span>
        <select
          value={report.culture ?? ""}
          onChange={(e) => mutate((r) => (r.culture = e.target.value || null))}
        >
          {CULTURES.map((c) => (
            <option key={c.value} value={c.value}>{c.label || t("designer.systemCulture")}</option>
          ))}
        </select>
        <span className="hint" style={{ fontWeight: 400 }}>{t("designer.numberDateFormatting")}</span>
      </label>

      <p className="hint">{t("designer.selectElement")}</p>
    </div>
  );
}

const CULTURES: { value: string; label: string }[] = [
  { value: "", label: "" },
  { value: "en-US", label: "English (US)" },
  { value: "en-GB", label: "English (UK)" },
  { value: "tr-TR", label: "Türkçe (Türkiye)" },
  { value: "de-DE", label: "Deutsch (Deutschland)" },
  { value: "fr-FR", label: "Français (France)" },
  { value: "es-ES", label: "Español (España)" },
  { value: "it-IT", label: "Italiano (Italia)" },
  { value: "nl-NL", label: "Nederlands (Nederland)" },
  { value: "pt-BR", label: "Português (Brasil)" },
  { value: "pt-PT", label: "Português (Portugal)" },
  { value: "ru-RU", label: "Русский (Россия)" },
  { value: "pl-PL", label: "Polski (Polska)" },
  { value: "ja-JP", label: "日本語 (日本)" },
  { value: "zh-CN", label: "中文 (简体)" },
  { value: "ar-SA", label: "العربية (السعودية)" },
];

// ---- small inputs ----

function Num({ label, value, onChange }: { label: string; value: number; onChange: (v: number) => void }) {
  return (
    <label className="field">
      <span>{label}</span>
      <input type="number" value={Math.round(value)} onChange={(e) => onChange(Number(e.target.value) || 0)} />
    </label>
  );
}

function Color({
  label,
  value,
  onChange,
  allowClear,
  cleared,
  onClear,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  allowClear?: boolean;
  cleared?: boolean;
  onClear?: () => void;
}) {
  const { t } = useTranslation();
  return (
    <label className="field">
      <span>{label}</span>
      <span className="row">
        <input type="color" value={cleared ? "#ffffff" : value} onChange={(e) => onChange(e.target.value)} />
        {allowClear && (
          <button className={`mini ${cleared ? "on" : ""}`} onClick={onClear} title={t("props.noFill")} aria-label={t("props.noFill")} type="button">
            <Ban />
          </button>
        )}
      </span>
    </label>
  );
}

export function Toggle({ label, active, onClick }: { label: string; active: boolean; onClick: () => void }) {
  return (
    <button type="button" className={`mini ${active ? "on" : ""}`} onClick={onClick}>
      {label}
    </button>
  );
}

export function AlignPicker({ value, onChange }: { value: TextAlign; onChange: (v: TextAlign) => void }) {
  const items: [TextAlign, LucideIcon][] = [
    ["left", AlignLeft],
    ["center", AlignCenter],
    ["right", AlignRight],
  ];
  return (
    <span className="row">
      {items.map(([a, Icon]) => (
        <button
          key={a}
          type="button"
          className={`mini ${value === a ? "on" : ""}`}
          onClick={() => onChange(a)}
          title={`Align ${a}`}
          aria-label={`Align ${a}`}
        >
          <Icon />
        </button>
      ))}
    </span>
  );
}

export function VAlignPicker({ value, onChange }: { value: VerticalAlign; onChange: (v: VerticalAlign) => void }) {
  const items: [VerticalAlign, LucideIcon, string][] = [
    ["top", AlignStartHorizontal, "Align top"],
    ["middle", AlignCenterHorizontal, "Align middle"],
    ["bottom", AlignEndHorizontal, "Align bottom"],
  ];
  return (
    <span className="row">
      {items.map(([a, Icon, label]) => (
        <button
          key={a}
          type="button"
          className={`mini ${value === a ? "on" : ""}`}
          onClick={() => onChange(a)}
          title={label}
          aria-label={label}
        >
          <Icon />
        </button>
      ))}
    </span>
  );
}

type Sides = { top: boolean; right: boolean; bottom: boolean; left: boolean };
const ALL_SIDES: Sides = { top: true, right: true, bottom: true, left: true };

/** Border width + per-side toggles + colour. Emits a full BorderSpec, or null when nothing is enabled. */
function BorderPicker({
  border,
  onChange,
  mixed,
}: {
  border: BorderSpec | null | undefined;
  onChange: (next: BorderSpec | null) => void;
  mixed?: boolean;
}) {
  const { t } = useTranslation();
  const b = border ?? null;
  const width = b ? Math.max(b.top, b.right, b.bottom, b.left) : 0;
  const on: Sides = {
    top: !!b && b.top > 0,
    right: !!b && b.right > 0,
    bottom: !!b && b.bottom > 0,
    left: !!b && b.left > 0,
  };
  const anySide = on.top || on.right || on.bottom || on.left;
  const color = b?.color ?? "#111827";

  const compose = (sides: Sides, w: number, c: string): BorderSpec | null =>
    w <= 0 || !(sides.top || sides.right || sides.bottom || sides.left)
      ? null
      : {
          top: sides.top ? w : 0,
          right: sides.right ? w : 0,
          bottom: sides.bottom ? w : 0,
          left: sides.left ? w : 0,
          color: c,
        };

  const items: [keyof Sides, LucideIcon, string][] = [
    ["top", PanelTop, "Top border"],
    ["left", PanelLeft, "Left border"],
    ["bottom", PanelBottom, "Bottom border"],
    ["right", PanelRight, "Right border"],
  ];

  return (
    <div className="field">
      <span>Border{mixed ? " — mixed" : ""}</span>
      <div className="border-ctl">
        <input
          type="number"
          min={0}
          value={width || ""}
          placeholder="0"
          title={t("props.borderWidth")}
          onChange={(e) => onChange(compose(anySide ? on : ALL_SIDES, Number(e.target.value) || 0, color))}
        />
        <span className="border-sides">
          {items.map(([side, Icon, label]) => (
            <button
              key={side}
              type="button"
              className={`mini ${on[side] ? "on" : ""}`}
              title={label}
              aria-label={label}
              aria-pressed={on[side]}
              onClick={() => onChange(compose({ ...on, [side]: !on[side] }, width > 0 ? width : 1, color))}
            >
              <Icon />
            </button>
          ))}
        </span>
        <input
          type="color"
          value={color}
          title={t("props.borderColour")}
          onChange={(e) => onChange(compose(anySide ? on : ALL_SIDES, width, e.target.value))}
        />
      </div>
      <span className="row" style={{ gap: 4, marginTop: 4 }}>
        <button
          type="button"
          className="mini"
          onClick={() => onChange(compose(ALL_SIDES, width > 0 ? width : 1, color))}
        >
          All
        </button>
        <button type="button" className="mini" onClick={() => onChange(null)}>
          None
        </button>
      </span>
    </div>
  );
}

function titleCase(s: string): string {
  return s.replace(/([A-Z])/g, " $1").replace(/^./, (c) => c.toUpperCase());
}

// ---- style mutation helpers ----

export function setStyle<K extends keyof NonNullable<ReportElement["style"]>>(
  el: ReportElement,
  key: K,
  value: NonNullable<ReportElement["style"]>[K],
) {
  el.style = { ...(el.style ?? {}), [key]: value };
}

export function setFont(el: ReportElement, key: keyof NonNullable<NonNullable<ReportElement["style"]>["font"]>, value: unknown) {
  const style = el.style ?? {};
  el.style = { ...style, font: { ...(style.font ?? {}), [key]: value } };
}
