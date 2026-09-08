import { useEffect, useRef, useState } from "react";
import { FunctionSquare, X } from "lucide-react";

type Item = { insert: string; sig: string; doc: string };
type Group = { id: string; label: string; items: Item[] };

const FN_GROUPS: Group[] = [
  {
    id: "agg",
    label: "Aggregate",
    items: [
      { insert: "sum()", sig: "sum(expr)", doc: "Total of expr over the band's scope — group / page / report, decided by where the element sits." },
      { insert: "avg()", sig: "avg(expr)", doc: "Average of expr over the scope rows." },
      { insert: "count()", sig: "count(expr)", doc: "Number of non-empty values in scope. count() with no argument = row count." },
      { insert: "min()", sig: "min(expr)", doc: "Smallest value in scope. min(a, b) = the smaller of two values." },
      { insert: "max()", sig: "max(expr)", doc: "Largest value in scope. max(a, b) = the larger of two values." },
      { insert: "first()", sig: "first(expr)", doc: "Value of expr from the first row in scope." },
      { insert: "last()", sig: "last(expr)", doc: "Value of expr from the last row in scope." },
    ],
  },
  {
    id: "report",
    label: "Report",
    items: [
      { insert: "pageNumber()", sig: "pageNumber()", doc: "Current page number." },
      { insert: "totalPages()", sig: "totalPages()", doc: "Total number of pages." },
      { insert: "rowNumber()", sig: "rowNumber()", doc: "1-based position of the current detail row." },
      { insert: "totalRows()", sig: "totalRows()", doc: "Number of rows in the current scope." },
    ],
  },
  {
    id: "math",
    label: "Math",
    items: [
      { insert: "round(, 2)", sig: "round(x, digits)", doc: "Round x to the given number of decimals (default 0)." },
      { insert: "abs()", sig: "abs(x)", doc: "Absolute value." },
      { insert: "floor()", sig: "floor(x)", doc: "Round down to a whole number." },
      { insert: "ceiling()", sig: "ceiling(x)", doc: "Round up to a whole number." },
      { insert: "trunc()", sig: "trunc(x)", doc: "Drop the fractional part." },
      { insert: "sqrt()", sig: "sqrt(x)", doc: "Square root." },
      { insert: "pow(, 2)", sig: "pow(x, y)", doc: "x raised to the power y." },
      { insert: "mod(, )", sig: "mod(x, y)", doc: "Remainder of x / y." },
      { insert: "sign()", sig: "sign(x)", doc: "-1, 0 or 1." },
    ],
  },
  {
    id: "text",
    label: "Text",
    items: [
      { insert: "upper()", sig: "upper(s)", doc: "Uppercase, using the report culture." },
      { insert: "lower()", sig: "lower(s)", doc: "Lowercase, using the report culture." },
      { insert: "trim()", sig: "trim(s)", doc: "Remove leading and trailing spaces." },
      { insert: "len()", sig: "len(s)", doc: "Number of characters." },
      { insert: "left(, 3)", sig: "left(s, n)", doc: "First n characters." },
      { insert: "right(, 3)", sig: "right(s, n)", doc: "Last n characters." },
      { insert: "substring(, 0, 3)", sig: "substring(s, start, len)", doc: "Substring from start (0-based)." },
      { insert: "replace(, '', '')", sig: "replace(s, find, with)", doc: "Replace every occurrence." },
      { insert: "contains(, '')", sig: "contains(s, sub)", doc: "True if s contains sub (case-insensitive)." },
    ],
  },
  {
    id: "date",
    label: "Date / Time",
    items: [
      { insert: "now()", sig: "now()", doc: "Current date and time." },
      { insert: "today()", sig: "today()", doc: "Current date at midnight." },
      { insert: "year()", sig: "year(d)", doc: "Year of a date." },
      { insert: "month()", sig: "month(d)", doc: "Month of a date (1–12)." },
      { insert: "day()", sig: "day(d)", doc: "Day of the month." },
      { insert: "adddays(, 7)", sig: "adddays(d, n)", doc: "Date n days after d." },
    ],
  },
  {
    id: "logic",
    label: "Logic",
    items: [
      { insert: "if(, , )", sig: "if(cond, a, b)", doc: "a when cond is true, otherwise b." },
      { insert: "coalesce(, )", sig: "coalesce(a, b, …)", doc: "First non-null argument." },
      { insert: "format(, 'N2')", sig: "format(value, code)", doc: "Format a value with a .NET format string." },
    ],
  },
];

/** Where to place the caret after inserting: between empty parens, just inside "(", else at the end. */
function caretAfterInsert(insert: string): number {
  if (insert.endsWith("()")) return insert.length - 1;
  const open = insert.indexOf("(");
  return open >= 0 ? open + 1 : insert.length;
}

export function FormulaDialog({
  initial,
  fields,
  onApply,
  onClose,
}: {
  initial: string;
  fields: string[];
  onApply: (v: string) => void;
  onClose: () => void;
}) {
  const [draft, setDraft] = useState(initial);
  const [group, setGroup] = useState<Group>(FN_GROUPS[0]);
  const [doc, setDoc] = useState<string>("");
  const taRef = useRef<HTMLTextAreaElement>(null);
  const caret = useRef<number | null>(null);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  useEffect(() => {
    if (caret.current == null) return;
    const ta = taRef.current;
    if (ta) {
      ta.focus();
      ta.setSelectionRange(caret.current, caret.current);
    }
    caret.current = null;
  }, [draft]);

  const insert = (text: string, caretOffset = text.length) => {
    const ta = taRef.current;
    const start = ta ? ta.selectionStart : draft.length;
    const end = ta ? ta.selectionEnd : draft.length;
    const next = draft.slice(0, start) + text + draft.slice(end);
    caret.current = start + caretOffset;
    setDraft(next);
  };

  const insertField = (name: string) => {
    const isExpr = /^\s*=/.test(draft) || /[(]/.test(draft);
    insert(isExpr ? name : `{${name}}`);
  };

  return (
    <div className="modal-backdrop" onMouseDown={onClose}>
      <div
        className="modal formula-dialog"
        role="dialog"
        aria-modal="true"
        aria-label="Formula"
        onMouseDown={(e) => e.stopPropagation()}
      >
        <header>
          <h2>
            <FunctionSquare /> Formula
          </h2>
          <button className="mini ghost" onClick={onClose} aria-label="Close">
            <X />
          </button>
        </header>

        <div className="formula-body">
          <textarea
            ref={taRef}
            className="formula-input"
            rows={4}
            spellCheck={false}
            placeholder="e.g. =upper(orders.customer)  ·  =sum(orders.total)  ·  now()"
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
          />
          <p className="hint">
            Start with <code>=</code> for an expression, or drop in a plain <code>{"{source.field}"}</code> binding. Bare
            function calls like <code>now()</code> also work.
          </p>

          <div className="formula-cats">
            <button
              type="button"
              className={group.id === "fields" ? "on" : ""}
              onClick={() => setGroup({ id: "fields", label: "Fields", items: [] })}
              disabled={fields.length === 0}
            >
              Fields
            </button>
            {FN_GROUPS.map((g) => (
              <button
                key={g.id}
                type="button"
                className={group.id === g.id ? "on" : ""}
                onClick={() => setGroup(g)}
              >
                {g.label}
              </button>
            ))}
          </div>

          <ul className="formula-list" onMouseLeave={() => setDoc("")}>
            {group.id === "fields"
              ? fields.map((f) => (
                  <li key={f}>
                    <button type="button" onClick={() => insertField(f)} onMouseEnter={() => setDoc(`Insert the ${f} value`)}>
                      <span className="formula-sig">{f}</span>
                    </button>
                  </li>
                ))
              : group.items.map((it) => (
                  <li key={it.sig}>
                    <button
                      type="button"
                      onClick={() => insert(it.insert, caretAfterInsert(it.insert))}
                      onMouseEnter={() => setDoc(it.doc)}
                    >
                      <span className="formula-sig">{it.sig}</span>
                    </button>
                  </li>
                ))}
          </ul>

          <p className="formula-doc">{doc || " "}</p>
        </div>

        <footer>
          <button className="btn" onClick={onClose}>
            Cancel
          </button>
          <button className="btn primary" onClick={() => onApply(draft)}>
            Apply
          </button>
        </footer>
      </div>
    </div>
  );
}

/** Single-line value field with an "fx" button that opens the formula editor. */
export function FormulaField({
  label,
  value,
  fields,
  onChange,
}: {
  label: string;
  value: string;
  fields: string[];
  onChange: (v: string) => void;
}) {
  const [open, setOpen] = useState(false);
  return (
    <div className="field">
      <span>{label}</span>
      <div className="format-input">
        <input value={value} placeholder="text, {source.field} or =expression" onChange={(e) => onChange(e.target.value)} />
        <button
          type="button"
          className="mini"
          title="Formula editor"
          aria-label="Open formula editor"
          onClick={() => setOpen(true)}
        >
          <FunctionSquare />
        </button>
      </div>
      {open && (
        <FormulaDialog
          initial={value}
          fields={fields}
          onApply={(v) => {
            onChange(v);
            setOpen(false);
          }}
          onClose={() => setOpen(false)}
        />
      )}
    </div>
  );
}
