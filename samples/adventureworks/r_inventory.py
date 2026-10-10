from lib import *

INV = """
SELECT l.Name AS Location, p.ProductNumber, p.Name AS Product, ISNULL(p.Color, '—') AS Color, pi.Shelf, pi.Bin,
       pi.Quantity, p.SafetyStockLevel AS Safety, p.ReorderPoint AS Reorder,
       CASE WHEN pi.Quantity < p.SafetyStockLevel THEN 'Critical' WHEN pi.Quantity < p.ReorderPoint THEN 'Low' ELSE 'OK' END AS Status,
       CASE WHEN pi.Quantity < p.SafetyStockLevel THEN 2 WHEN pi.Quantity < p.ReorderPoint THEN 1 ELSE 0 END AS Severity
FROM Production.ProductInventory pi
JOIN Production.Product p ON p.ProductID = pi.ProductID
JOIN Production.Location l ON l.LocationID = pi.LocationID
WHERE pi.Quantity < p.ReorderPoint * @threshold / 100.0
ORDER BY l.Name, Severity DESC, p.Name"""

SUMMARY = """
SELECT l.Name AS Location,
  SUM(CASE WHEN pi.Quantity < p.SafetyStockLevel THEN 1 ELSE 0 END) AS Critical,
  SUM(CASE WHEN pi.Quantity >= p.SafetyStockLevel AND pi.Quantity < p.ReorderPoint THEN 1 ELSE 0 END) AS Low
FROM Production.ProductInventory pi
JOIN Production.Product p ON p.ProductID = pi.ProductID
JOIN Production.Location l ON l.LocationID = pi.LocationID
GROUP BY l.Name HAVING SUM(CASE WHEN pi.Quantity < p.ReorderPoint THEN 1 ELSE 0 END) > 0 ORDER BY 2 DESC"""

MUTED = "#6b7280"


def build():
    row_rules = [rule("Severity", "eq", 2, bg="#fef2f2"), rule("Severity", "eq", 1, bg="#fffbeb")]
    bands = [
        band("reportHeader", 316, [
            rect(0, 0, 722, 54, fill="#7f1d1d"),
            label(14, 8, 400, 22, "Inventory Reorder Alert", size=17, bold=True, color="#ffffff"),
            field(14, 33, 690, 14, "Stock lines below {param:threshold}% of their reorder point, grouped by warehouse location", size=8.5, color="#fecaca"),
            label(0, 66, 722, 16, "Lines needing attention by location", size=10, bold=True),
            chart(0, 84, 722, 226, "bar", "sum", "{sum.Location}",
                  [series("Critical (below safety stock)", "{sum.Critical}", "#dc2626"), series("Low (below reorder point)", "{sum.Low}", "#f59e0b")]),
        ]),
        band("pageHeader", 22, [
            label(8, 4, 90, 14, "NUMBER", size=7.5, bold=True, color=MUTED),
            label(100, 4, 280, 14, "PRODUCT", size=7.5, bold=True, color=MUTED),
            label(390, 4, 70, 14, "SHELF/BIN", size=7.5, bold=True, color=MUTED),
            label(468, 4, 50, 14, "QTY", size=7.5, bold=True, color=MUTED, align="right"),
            label(526, 4, 56, 14, "SAFETY", size=7.5, bold=True, color=MUTED, align="right"),
            label(590, 4, 60, 14, "REORDER", size=7.5, bold=True, color=MUTED, align="right"),
            label(660, 4, 56, 14, "STATUS", size=7.5, bold=True, color=MUTED),
            line(0, 20, 722, "#9ca3af"),
        ]),
        band("groupHeader", 24, [
            rect(0, 2, 722, 20, fill="#f1f5f9"),
            field(8, 5, 400, 14, "{inv.Location}", size=10, bold=True, color="#0f172a"),
        ], group=("inv", "{inv.Location}"), level=0, repeat=True),
        band("detail", 16, [
            field(8, 1, 90, 14, "{inv.ProductNumber}", size=8, color=MUTED),
            field(100, 1, 280, 14, "{inv.Product}", size=8.5),
            field(390, 1, 70, 14, "{inv.Shelf}-{inv.Bin}", size=8, color=MUTED),
            field(468, 1, 50, 14, "{inv.Quantity}", "n0", size=8.5, align="right", bold=True),
            field(526, 1, 56, 14, "{inv.Safety}", "n0", size=8.5, align="right", color=MUTED),
            field(590, 1, 60, 14, "{inv.Reorder}", "n0", size=8.5, align="right", color=MUTED),
            field(660, 1, 56, 14, "{inv.Status}", size=8, bold=True,
                  rules=[rule("Status", "eq", "Critical", color="#b91c1c"), rule("Status", "eq", "Low", color="#b45309")]),
        ], source="inv", rules=row_rules),
        band("groupFooter", 20, [
            label(8, 3, 100, 14, "Lines at this location:", size=8, italic=True, color=MUTED),
            field(110, 3, 30, 14, "{inv.Product}", "n0", agg="count", scope="group", size=8.5, bold=True),
            label(300, 3, 150, 14, "units on hand", size=8, italic=True, color=MUTED, align="right"),
            field(468, 3, 50, 14, "{inv.Quantity}", "n0", agg="sum", scope="group", size=8.5, bold=True, align="right"),
        ], group=("inv", "{inv.Location}"), level=0),
        band("pageFooter", 22, [
            line(0, 2, 722, "#d1d5db"),
            field(0, 6, 300, 14, "Page {pageNumber()} of {totalPages()}", size=8, color=MUTED),
        ]),
        band("reportFooter", 28, [
            line(0, 4, 722, "#7f1d1d", 1.5),
            label(8, 9, 200, 16, "TOTAL LINES BELOW REORDER POINT", size=9, bold=True),
            field(390, 9, 50, 16, "{inv.Product}", "n0", agg="count", scope="report", size=10, bold=True, color="#b91c1c"),
        ]),
    ]
    return report(
        "AdventureWorks — Inventory Reorder Alert", "aw-inventory-reorder", "banded", bands=bands,
        sources=[sql("inv", INV, {"threshold": "{param:threshold}"}), sql("sum", SUMMARY)],
        params=[param("threshold", "number", "% of reorder point", 40, allowed=[25, 40, 60, 80, 100])],
        description="A chart in the report header above grouped detail, with band-level row highlighting by severity.",
    )
