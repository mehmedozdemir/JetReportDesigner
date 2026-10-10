"""Tiny builders for report-definition JSON, so each showcase report reads like a layout, not a wall of braces."""
from itertools import count

_ids = count(1)


def _id(prefix):
    return f"{prefix}{next(_ids)}"


def B(x, y, w, h):
    return {"x": x, "y": y, "width": w, "height": h}


def font(size=None, bold=None, italic=None):
    f = {}
    if size is not None:
        f["size"] = size
    if bold is not None:
        f["bold"] = bold
    if italic is not None:
        f["italic"] = italic
    return f


def style(size=None, bold=None, color=None, bg=None, align=None, valign=None, border=None, padding=None, italic=None):
    s = {}
    f = font(size, bold, italic)
    if f:
        s["font"] = f
    if color:
        s["color"] = color
    if bg:
        s["background"] = bg
    if align:
        s["align"] = align
    if valign:
        s["vAlign"] = valign
    if border:
        s["border"] = border
    if padding:
        s["padding"] = padding
    return s


def box(color="#e5e7eb", width=1, sides="trbl"):
    return {"top": width if "t" in sides else 0, "right": width if "r" in sides else 0,
            "bottom": width if "b" in sides else 0, "left": width if "l" in sides else 0, "color": color}


def _el(kind, bounds, **extra):
    el = {"id": _id(kind[:3]), "type": kind, "bounds": bounds, "formatRules": [], "canGrow": False,
          "aggregate": "none", "aggregateScope": "group"}
    el.update({k: v for k, v in extra.items() if v is not None})
    return el


def label(x, y, w, h, text, **st):
    return _el("label", B(x, y, w, h), text=text, style=style(**st) or None)


def field(x, y, w, h, value, fmt=None, agg=None, scope=None, rules=None, visible_when=None, grow=False, **st):
    el = _el("field", B(x, y, w, h), value=value, format=fmt, style=style(**st) or None, visibleWhen=visible_when)
    if agg:
        el["aggregate"] = agg
        el["aggregateScope"] = scope or "group"
    if rules:
        el["formatRules"] = rules
    if grow:
        el["canGrow"] = True
    return el


def rect(x, y, w, h, fill=None, border_color=None, border_width=1, rules=None):
    st = {}
    if fill:
        st["background"] = fill
    st["border"] = box(border_color, border_width) if border_color else box("#000000", 0)
    el = _el("rectangle", B(x, y, w, h), style=st)
    if rules:
        el["formatRules"] = rules
    return el


def line(x, y, w, color="#d1d5db", width=1, vertical=False, h=0):
    return _el("line", B(x, y, w, h), line={"orientation": "vertical" if vertical else "horizontal"},
               style={"border": box(color, width)})


def column(header, value, width, fmt=None, align="left", scale=None, spark=None):
    c = {"header": header, "value": value, "width": width, "align": align}
    if fmt:
        c["format"] = fmt
    if scale:
        c["colorScale"] = scale
    if spark:
        c["sparkline"] = spark
    return c


def table(x, y, w, h, source, columns, header=True, **st):
    return _el("table", B(x, y, w, h), style=style(**st) or None,
               table={"dataSource": source, "showHeader": header, "columns": columns})


def series(name, value, color=None, fmt=None):
    s = {"name": name, "value": value}
    if color:
        s["color"] = color
    if fmt:
        s["format"] = fmt
    return s


def chart(x, y, w, h, kind, source, category, serie, title=None, legend=True, grid=True):
    return _el("chart", B(x, y, w, h),
               chart={"type": kind, "dataSource": source, "category": category, "series": serie,
                      "title": title, "showLegend": legend, "showGrid": grid})


def matrix(x, y, w, h, source, row, col, value, row_header=None, agg="sum", fmt=None, scale=None, totals=(True, True)):
    m = {"dataSource": source, "rowField": row, "columnField": col, "valueField": value, "aggregate": agg,
         "showRowTotals": totals[0], "showColumnTotals": totals[1]}
    if row_header:
        m["rowHeader"] = row_header
    if fmt:
        m["format"] = fmt
    if scale:
        m["colorScale"] = scale
    return _el("matrix", B(x, y, w, h), matrix=m)


def barcode(x, y, w, h, value, symbology="code128", text=True):
    return _el("barcode", B(x, y, w, h),
               barcode={"symbology": symbology, "value": value, "foreColor": "#111827", "backColor": "#ffffff", "showText": text})


def subreport(x, y, w, h, report_id, params):
    return _el("subreport", B(x, y, w, h), subreport={"reportId": report_id, "parameters": params})


def rule(field_name, op, value, bg=None, color=None, bold=None, hidden=False):
    st = {}
    if bg:
        st["background"] = bg
    if color:
        st["color"] = color
    if bold is not None:
        st["font"] = {"bold": bold}
    return {"field": field_name, "op": op, "value": str(value), "style": st, "hidden": hidden}


def sql(name, command, params=None, max_rows=50000, connection="adventureworks"):
    return {"name": name, "kind": "sql", "fields": [],
            "sql": {"connection": connection, "commandText": command.strip(),
                    "parameters": [{"name": k, "value": v} for k, v in (params or {}).items()],
                    "timeoutSeconds": 60, "maxRows": max_rows}}


def band(kind, height, elements, source=None, group=None, level=0, repeat=False, rules=None):
    b = {"type": kind, "height": height, "visible": True, "repeatOnEveryPage": repeat, "elements": elements,
         "formatRules": rules or []}
    if source:
        b["dataSource"] = source
    if group:
        b["group"] = {"dataSource": group[0], "expression": group[1], "sort": group[2] if len(group) > 2 else "asc"}
        b["groupLevel"] = level
    return b


def param(name, kind, label_text, default=None, required=False, allowed=None):
    p = {"name": name, "type": kind, "label": label_text, "defaultValue": default, "required": required}
    if allowed:
        p["allowedValues"] = allowed
    return p


def report(name, code, layout, bands=None, body=None, sources=None, params=None, page=None, culture="en-US", description=None):
    r = {
        "schemaVersion": 1, "name": name, "code": code, "description": description, "layoutMode": layout, "unit": "px",
        "culture": culture,
        "page": page or {"size": "A4", "orientation": "portrait", "margins": {"top": 36, "right": 36, "bottom": 36, "left": 36}, "columns": 1},
        "parameters": params or [],
        "connections": [{"name": "adventureworks", "connectionId": "00000000-0000-0000-0000-000000000000", "provider": "sqlServer"}],
        "dataSources": sources or [], "styles": {}, "bands": bands or [],
    }
    if body is not None:
        r["body"] = body
    return {k: v for k, v in r.items() if v is not None}


def landscape(w_margin=32):
    return {"size": "A4", "orientation": "landscape", "margins": {"top": w_margin, "right": w_margin, "bottom": w_margin, "left": w_margin}, "columns": 1}
