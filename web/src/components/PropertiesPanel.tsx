import { useMemo, useState } from "react";
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
  BoxSelect,
  FileText,
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
import type {
  BackgroundFit,
  Band,
  BorderSpec,
  ElementType,
  FormatRule,
  PageSize,
  ReportElement,
  TextAlign,
  VerticalAlign,
} from "../types";

const EMPTY_RULES: FormatRule[] = [];

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

const FONT_FAMILIES = [
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
  const known = value && FONT_FAMILIES.includes(value);
  return (
    <label className="field">
      <span>Font</span>
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
function common<T>(elements: ReportElement[], getter: (el: ReportElement) => T): T | undefined {
  if (elements.length === 0) return undefined;
  const first = getter(elements[0]);
  return elements.every((el) => Object.is(getter(el), first)) ? first : undefined;
}

function MultiProperties() {
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
      <h2><BoxSelect /> {selectedIds.length} selected</h2>
      <p className="hint">Edits apply to all selected elements.</p>

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
              <span>Style</span>
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
  const band = useDesigner((s) => s.report!.bands[index]) as Band;
  const sources = useDesigner((s) => s.report!.dataSources);
  const patchBand = useDesigner((s) => s.patchBand);
  const removeBand = useDesigner((s) => s.removeBand);
  const moveBand = useDesigner((s) => s.moveBand);
  const bandCount = useDesigner((s) => s.report!.bands.length);
  const isGroup = band.type === "groupHeader" || band.type === "groupFooter";

  return (
    <div className="panel">
      <h2><Rows3 /> {titleCase(band.type)} band</h2>
      <div className="row" style={{ marginBottom: 8 }}>
        <button className="mini" onClick={() => moveBand(index, -1)} disabled={index === 0} title="Move band up" aria-label="Move band up">
          <ArrowUp />
        </button>
        <button className="mini" onClick={() => moveBand(index, 1)} disabled={index === bandCount - 1} title="Move band down" aria-label="Move band down">
          <ArrowDown />
        </button>
        <button className="mini danger" onClick={() => removeBand(index)} title="Delete band">
          <Trash2 /> Delete
        </button>
      </div>

      <label className="field">
        <span>Height</span>
        <input
          type="number"
          value={Math.round(band.height)}
          onChange={(e) => patchBand(index, (b) => (b.height = Math.max(8, Number(e.target.value) || 8)))}
        />
      </label>

      {band.type === "detail" && (
        <label className="field">
          <span>Data source</span>
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
            <span>Group by</span>
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
            <span>Sort</span>
            <select
              value={band.group?.sort ?? "asc"}
              onChange={(e) =>
                patchBand(index, (b) => {
                  b.group ??= { dataSource: sources[0]?.name ?? "", expression: "", sort: "asc" };
                  b.group.sort = e.target.value as "asc" | "desc";
                })
              }
            >
              <option value="asc">Ascending</option>
              <option value="desc">Descending</option>
            </select>
          </label>
        </>
      )}

      {(band.type === "groupHeader" || band.type === "reportHeader" || band.type === "pageHeader") && (
        <label className="row" style={{ marginTop: 6 }}>
          <input
            type="checkbox"
            checked={band.repeatOnEveryPage}
            onChange={(e) => patchBand(index, (b) => (b.repeatOnEveryPage = e.target.checked))}
          />
          <span>Repeat on every page</span>
        </label>
      )}

      <ImagePicker
        label="Background image"
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
        <button className="mini" title="Send to back (Ctrl+Shift+[)" aria-label="Send to back" onClick={() => reorder("back")}>
          <ArrowDownToLine />
        </button>
        <button className="mini" title="Send backward (Ctrl+[)" aria-label="Send backward" onClick={() => reorder("backward")}>
          <ArrowDown />
        </button>
        <button className="mini" title="Bring forward (Ctrl+])" aria-label="Bring forward" onClick={() => reorder("forward")}>
          <ArrowUp />
        </button>
        <button className="mini" title="Bring to front (Ctrl+Shift+])" aria-label="Bring to front" onClick={() => reorder("front")}>
          <ArrowUpToLine />
        </button>
        <span style={{ marginLeft: "auto", color: "var(--text-secondary)", display: "flex", alignItems: "center" }} title="Z-order">
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
          label="Text"
          value={element.text ?? ""}
          fields={fieldNames}
          onChange={(v) => onPatch((e) => (e.text = v))}
        />
      )}
      {(element.type === "field" || element.type === "pageInfo") && (
        <>
          <FormulaField
            label="Value / binding"
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
          label="Image"
          value={element.image?.source ? { source: element.image.source, fit: (element.image.fit as BackgroundFit) ?? "contain" } : null}
          fitOptions={["contain", "cover", "fill"]}
          onChange={(v) => onPatch((e) => (e.image = v ? { source: v.source, fit: v.fit } : null))}
        />
      )}

      {element.type === "table" && element.table && (
        <div className="table-props">
          <label className="field">
            <span>Data source</span>
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
            <span>Header row</span>
          </label>

          <h3>Columns</h3>
          {element.table.columns.map((col, i) => (
            <div key={i} className="col-row">
              <input
                value={col.header}
                placeholder="Header"
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
                <button className="mini danger" onClick={() => onPatch((e) => e.table!.columns.splice(i, 1))} aria-label="Remove column">
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
            <Num label="Size (pt)" value={s.font?.size ?? 10} onChange={(v) => onPatch((e) => setFont(e, "size", v))} />
          </div>
          <div className="row" style={{ flexWrap: "wrap" }}>
            <Toggle label="B" active={!!s.font?.bold} onClick={() => onPatch((e) => setFont(e, "bold", !e.style?.font?.bold))} />
            <Toggle label="I" active={!!s.font?.italic} onClick={() => onPatch((e) => setFont(e, "italic", !e.style?.font?.italic))} />
            <span className="align-divider" />
            <AlignPicker value={(s.align as TextAlign) ?? "left"} onChange={(v) => onPatch((e) => setStyle(e, "align", v))} />
            <VAlignPicker value={(s.vAlign as VerticalAlign) ?? "top"} onChange={(v) => onPatch((e) => setStyle(e, "vAlign", v))} />
          </div>
        </>
      )}

      <div className="grid2">
        <Color label="Color" value={s.color ?? "#111827"} onChange={(v) => onPatch((e) => setStyle(e, "color", v))} />
        <Color label="Background" value={s.background ?? "#ffffff"} onChange={(v) => onPatch((e) => setStyle(e, "background", v))} allowClear cleared={!s.background} onClear={() => onPatch((e) => setStyle(e, "background", null))} />
      </div>

      {element.type !== "image" && element.type !== "line" && (
        <ImagePicker
          label="Background image"
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
  const report = useDesigner((s) => s.report)!;
  const mutate = useDesigner((s) => s.mutate);
  const p = report.page;
  const set = (fn: (page: typeof p) => void) => mutate((r) => fn(r.page));

  return (
    <div className="panel">
      <h2><FileText /> Page</h2>
      <label className="field">
        <span>Size</span>
        <select value={p.size} onChange={(e) => set((page) => (page.size = e.target.value as PageSize))}>
          {["A4", "A5", "Letter", "Legal"].map((v) => (
            <option key={v}>{v}</option>
          ))}
        </select>
      </label>
      <label className="field">
        <span>Orientation</span>
        <select value={p.orientation} onChange={(e) => set((page) => (page.orientation = e.target.value as "portrait" | "landscape"))}>
          <option value="portrait">Portrait</option>
          <option value="landscape">Landscape</option>
        </select>
      </label>
      <div className="grid2">
        <Num label="Margin T" value={p.margins.top} onChange={(v) => set((page) => (page.margins.top = v))} />
        <Num label="Margin R" value={p.margins.right} onChange={(v) => set((page) => (page.margins.right = v))} />
        <Num label="Margin B" value={p.margins.bottom} onChange={(v) => set((page) => (page.margins.bottom = v))} />
        <Num label="Margin L" value={p.margins.left} onChange={(v) => set((page) => (page.margins.left = v))} />
      </div>

      <ImagePicker
        label="Background image"
        value={p.backgroundImage}
        onChange={(v) => set((page) => (page.backgroundImage = v))}
      />

      <label className="field">
        <span>Culture</span>
        <select
          value={report.culture ?? ""}
          onChange={(e) => mutate((r) => (r.culture = e.target.value || null))}
        >
          {CULTURES.map((c) => (
            <option key={c.value} value={c.value}>{c.label}</option>
          ))}
        </select>
        <span className="hint" style={{ fontWeight: 400 }}>Number &amp; date formatting</span>
      </label>

      <p className="hint">Select an element to edit its properties.</p>
    </div>
  );
}

const CULTURES: { value: string; label: string }[] = [
  { value: "", label: "System (server default)" },
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
  return (
    <label className="field">
      <span>{label}</span>
      <span className="row">
        <input type="color" value={cleared ? "#ffffff" : value} onChange={(e) => onChange(e.target.value)} />
        {allowClear && (
          <button className={`mini ${cleared ? "on" : ""}`} onClick={onClear} title="No fill" aria-label="No fill" type="button">
            <Ban />
          </button>
        )}
      </span>
    </label>
  );
}

function Toggle({ label, active, onClick }: { label: string; active: boolean; onClick: () => void }) {
  return (
    <button type="button" className={`mini ${active ? "on" : ""}`} onClick={onClick}>
      {label}
    </button>
  );
}

function AlignPicker({ value, onChange }: { value: TextAlign; onChange: (v: TextAlign) => void }) {
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

function VAlignPicker({ value, onChange }: { value: VerticalAlign; onChange: (v: VerticalAlign) => void }) {
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
          title="Border width (pt)"
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
          title="Border colour"
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

function setStyle<K extends keyof NonNullable<ReportElement["style"]>>(
  el: ReportElement,
  key: K,
  value: NonNullable<ReportElement["style"]>[K],
) {
  el.style = { ...(el.style ?? {}), [key]: value };
}

function setFont(el: ReportElement, key: keyof NonNullable<NonNullable<ReportElement["style"]>["font"]>, value: unknown) {
  const style = el.style ?? {};
  el.style = { ...style, font: { ...(style.font ?? {}), [key]: value } };
}
