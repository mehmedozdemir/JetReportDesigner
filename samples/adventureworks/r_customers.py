from lib import *

# ---- child: the last five orders of one customer, sized to sit inside a band as a subreport ----
RECENT = """
SELECT TOP 5 h.SalesOrderNumber AS OrderNo, h.OrderDate,
       CASE h.Status WHEN 5 THEN 'Shipped' WHEN 1 THEN 'In process' WHEN 2 THEN 'Approved' ELSE 'Other' END AS Status,
       h.TotalDue AS Total
FROM Sales.SalesOrderHeader h WHERE h.CustomerID = @customerId ORDER BY h.OrderDate DESC"""

MUTED = "#6b7280"


def build_child():
    els = [
        table(0, 0, 420, 100, "recent", [
            column("Order", "{recent.OrderNo}", 100),
            column("Date", "{recent.OrderDate}", 100, "yyyy-MM-dd"),
            column("Status", "{recent.Status}", 90),
            column("Total", "{recent.Total}", 130, "c0", "right"),
        ], size=8.5),
    ]
    return report(
        "AdventureWorks — Customer recent orders (subreport)", "aw-customer-recent-orders", "free",
        body={"height": 100, "elements": els},
        sources=[sql("recent", RECENT, {"customerId": "{param:customerId}"})],
        params=[param("customerId", "number", "Customer ID", 29825)],
        page={"size": "Custom", "orientation": "portrait", "customWidth": 420, "customHeight": 100,
              "margins": {"top": 0, "right": 0, "bottom": 0, "left": 0}, "columns": 1},
        description="Embedded by 'Top customers' through a subreport element, one instance per customer row.",
    )


# ---- master ----
TOP = """
SELECT TOP (CAST(@top AS int)) c.CustomerID, ISNULL(st.Name, p.FirstName + ' ' + p.LastName) AS Customer,
       CASE WHEN c.StoreID IS NULL THEN 'Individual' ELSE 'Store' END AS Kind,
       ISNULL(t.Name, '—') AS Territory,
       COUNT(*) AS Orders, SUM(h.TotalDue) AS Lifetime, MAX(h.OrderDate) AS LastOrder, AVG(h.TotalDue) AS AvgOrder
FROM Sales.Customer c
JOIN Sales.SalesOrderHeader h ON h.CustomerID = c.CustomerID
LEFT JOIN Person.Person p ON p.BusinessEntityID = c.PersonID
LEFT JOIN Sales.Store st ON st.BusinessEntityID = c.StoreID
LEFT JOIN Sales.SalesTerritory t ON t.TerritoryID = c.TerritoryID
WHERE (@territory IS NULL OR @territory IN ('', 'All') OR t.Name = @territory)
GROUP BY c.CustomerID, st.Name, p.FirstName, p.LastName, c.StoreID, t.Name
ORDER BY SUM(h.TotalDue) DESC"""


def build(refs=None):
    child_id = (refs or {}).get("aw-customer-recent-orders", "00000000-0000-0000-0000-000000000000")
    detail = band("detail", 126, [
        rect(0, 4, 722, 116, fill="#ffffff", border_color="#e5e7eb"),
        rect(0, 4, 5, 116, fill="#7c3aed"),
        field(18, 12, 270, 18, "{c.Customer}", size=12, bold=True, color="#111827"),
        field(18, 32, 270, 13, "{c.Kind} · {c.Territory} · customer #{c.CustomerID}", size=8.5, color=MUTED),
        label(18, 54, 110, 12, "LIFETIME SALES", size=7.5, bold=True, color=MUTED),
        field(18, 66, 130, 22, "{c.Lifetime}", "c0", size=16, bold=True, color="#5b21b6"),
        label(150, 54, 60, 12, "ORDERS", size=7.5, bold=True, color=MUTED),
        field(150, 66, 70, 22, "{c.Orders}", "n0", size=16, bold=True),
        label(18, 92, 270, 12, "Last order", size=8, color=MUTED),
        field(70, 92, 100, 12, "{c.LastOrder}", "yyyy-MM-dd", size=8, bold=True),
        label(180, 92, 60, 12, "avg order", size=8, color=MUTED),
        field(236, 92, 60, 12, "{c.AvgOrder}", "c0", size=8, bold=True),
        label(300, 12, 260, 12, "MOST RECENT ORDERS", size=7.5, bold=True, color=MUTED),
        subreport(300, 26, 410, 98, child_id, {"customerId": "{c.CustomerID}"}),
    ], source="c")
    bands = [
        band("reportHeader", 62, [
            rect(0, 0, 722, 54, fill="#4c1d95"),
            label(14, 8, 500, 22, "Top Customers — 360° view", size=17, bold=True, color="#ffffff"),
            field(14, 33, 690, 14, "Top {param:top} customers by lifetime sales  ·  territory: {param:territory}", size=8.5, color="#ddd6fe"),
        ]),
        detail,
        band("pageFooter", 22, [
            line(0, 2, 722, "#d1d5db"),
            field(0, 6, 300, 14, "Page {pageNumber()} of {totalPages()}", size=8, color=MUTED),
        ]),
        band("reportFooter", 30, [
            line(0, 4, 722, "#4c1d95", 1.5),
            label(0, 10, 160, 16, "COMBINED LIFETIME SALES", size=9, bold=True),
            field(170, 8, 140, 18, "{c.Lifetime}", "c0", agg="sum", scope="report", size=12, bold=True, color="#5b21b6"),
        ]),
    ]
    return report(
        "AdventureWorks — Top Customers (with order history)", "aw-top-customers", "banded", bands=bands,
        sources=[sql("c", TOP, {"top": "{param:top}", "territory": "{param:territory}"})],
        params=[param("top", "number", "How many customers", 12),
                param("territory", "string", "Territory", "All",
                      allowed=["All", "Australia", "Canada", "Central", "France", "Germany", "Northeast", "Northwest", "Southeast", "Southwest", "United Kingdom"])],
        description="Master report: each customer row embeds the 'recent orders' report as a subreport, passing the customer id.",
    )
