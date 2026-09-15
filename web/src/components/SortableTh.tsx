import { ChevronDown, ChevronUp } from "lucide-react";
import { useState } from "react";

export interface Sort<K extends string> {
  key: K;
  dir: "asc" | "desc";
}

/** Sort state plus a comparator, so a table only has to say what each column's value is. */
export function useSort<K extends string>(initial: Sort<K>) {
  const [sort, setSort] = useState<Sort<K>>(initial);

  const toggle = (key: K) =>
    setSort((cur) => (cur.key === key ? { key, dir: cur.dir === "asc" ? "desc" : "asc" } : { key, dir: "asc" }));

  /** Sorts a copy — callers hold their rows in state and must not have them mutated. */
  const apply = <T,>(rows: T[], value: (row: T, key: K) => string | number | null | undefined): T[] =>
    [...rows].sort((a, b) => {
      const av = value(a, sort.key);
      const bv = value(b, sort.key);
      // Blanks last regardless of direction: a missing value isn't "smallest", it's unknown.
      if (av == null || av === "") return bv == null || bv === "" ? 0 : 1;
      if (bv == null || bv === "") return -1;
      const cmp = typeof av === "number" && typeof bv === "number" ? av - bv : String(av).localeCompare(String(bv));
      return sort.dir === "asc" ? cmp : -cmp;
    });

  return { sort, toggle, apply };
}

export function SortableTh<K extends string>({
  column,
  sort,
  onToggle,
  children,
}: {
  column: K;
  sort: Sort<K>;
  onToggle: (key: K) => void;
  children: React.ReactNode;
}) {
  const active = sort.key === column;
  return (
    <th aria-sort={active ? (sort.dir === "asc" ? "ascending" : "descending") : "none"}>
      <button className="th-sort" onClick={() => onToggle(column)}>
        {children}
        {active && (sort.dir === "asc" ? <ChevronUp size={12} /> : <ChevronDown size={12} />)}
      </button>
    </th>
  );
}
