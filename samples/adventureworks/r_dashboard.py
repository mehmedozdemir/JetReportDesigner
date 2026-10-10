from lib import *

YEAR = {"year": "{param:year}"}

MONTHS = ",".join(f"({i})" for i in range(1, 13))

KPI = """
SELECT SUM(h.TotalDue) AS TotalSales, COUNT(*) AS Orders, COUNT(DISTINCT h.CustomerID) AS Customers,
       AVG(h.TotalDue) AS AvgOrder,
       SUM(CASE WHEN h.OnlineOrderFlag = 1 THEN h.TotalDue ELSE 0 END) * 100.0 / NULLIF(SUM(h.TotalDue), 0) AS OnlinePct
FROM Sales.SalesOrderHeader h WHERE YEAR(h.OrderDate) = @year"""

MONTHLY = f"""
SELECT v.n AS MonthNum, LEFT(DATENAME(month, DATEFROMPARTS(2000, v.n, 1)), 3) AS Month,
  ISNULL((SELECT SUM(TotalDue) FROM Sales.SalesOrderHeader WHERE YEAR(OrderDate) = @year AND MONTH(OrderDate) = v.n), 0) AS ThisYear,
  ISNULL((SELECT SUM(TotalDue) FROM Sales.SalesOrderHeader WHERE YEAR(OrderDate) = @year - 1 AND MONTH(OrderDate) = v.n), 0) AS LastYear
FROM (VALUES {MONTHS}) v(n) ORDER BY v.n"""

CATEGORY = """
SELECT pc.Name AS Category, SUM(d.LineTotal) AS Sales
FROM Sales.SalesOrderDetail d
JOIN Sales.SalesOrderHeader h ON h.SalesOrderID = d.SalesOrderID
JOIN Production.Product p ON p.ProductID = d.ProductID
JOIN Production.ProductSubcategory ps ON ps.ProductSubcategoryID = p.ProductSubcategoryID
JOIN Production.ProductCategory pc ON pc.ProductCategoryID = ps.ProductCategoryID
WHERE YEAR(h.OrderDate) = @year GROUP BY pc.Name ORDER BY Sales DESC"""

TERRITORY = """
SELECT t.Name AS Region, SUM(h.TotalDue) AS Sales
FROM Sales.SalesOrderHeader h JOIN Sales.SalesTerritory t ON t.TerritoryID = h.TerritoryID
WHERE YEAR(h.OrderDate) = @year GROUP BY t.Name ORDER BY Sales DESC"""

TOP_PRODUCTS = f"""
WITH sales AS (
  SELECT d.ProductID, MONTH(h.OrderDate) AS m, SUM(d.LineTotal) AS amt
  FROM Sales.SalesOrderDetail d JOIN Sales.SalesOrderHeader h ON h.SalesOrderID = d.SalesOrderID
  WHERE YEAR(h.OrderDate) = @year GROUP BY d.ProductID, MONTH(h.OrderDate)),
top10 AS (SELECT TOP 10 ProductID, SUM(amt) AS Total FROM sales GROUP BY ProductID ORDER BY SUM(amt) DESC)
SELECT p.Name AS Product, t.Total,
  (SELECT STRING_AGG(CAST(CAST(ROUND(ISNULL(s.amt, 0), 0) AS bigint) AS varchar(20)), ',') WITHIN GROUP (ORDER BY v.n)
   FROM (VALUES {MONTHS}) v(n) LEFT JOIN sales s ON s.ProductID = t.ProductID AND s.m = v.n) AS Trend
FROM top10 t JOIN Production.Product p ON p.ProductID = t.ProductID ORDER BY t.Total DESC"""

INK, MUTED, LINE_C = "#111827", "#6b7280", "#e5e7eb"
PALETTE = ["#2563eb", "#7c3aed", "#0891b2", "#16a34a", "#ea580c", "#dc2626"]


def kpi_card(x, label_text, value, fmt, accent, sub=None):
    els = [
        rect(x, 62, 200, 76, fill="#ffffff", border_color=LINE_C),
        rect(x, 62, 4, 76, fill=accent),
        label(x + 16, 70, 176, 16, label_text, size=9, color=MUTED, bold=True),
        field(x + 16, 88, 176, 30, value, fmt=fmt, size=20, bold=True, color=INK),
    ]
    if sub:
        els.append(label(x + 16, 118, 176, 14, sub, size=8, color=MUTED))
    return els


def build():
    els = [
        rect(0, 0, 1059, 50, fill="#0f172a"),
        label(16, 8, 700, 22, "AdventureWorks Cycles — Executive Sales Dashboard", size=16, bold=True, color="#ffffff"),
        field(16, 30, 700, 14, "Calendar year {param:year}  ·  compared with the previous year", size=9, color="#cbd5e1"),
        label(760, 18, 283, 16, "Source: AdventureWorks2022 (SQL Server)", size=8, color="#94a3b8", align="right"),
    ]
    els += kpi_card(0, "TOTAL SALES", "{kpi.TotalSales}", "c0", "#2563eb")
    els += kpi_card(215, "ORDERS", "{kpi.Orders}", "n0", "#7c3aed")
    els += kpi_card(430, "ACTIVE CUSTOMERS", "{kpi.Customers}", "n0", "#0891b2")
    els += kpi_card(645, "AVG ORDER VALUE", "{kpi.AvgOrder}", "c0", "#16a34a")
    els += kpi_card(860, "ONLINE SHARE", "{kpi.OnlinePct}", "n1", "#ea580c", sub="% of sales value")

    els += [
        label(0, 150, 640, 16, "Monthly sales — this year vs last year", size=10, bold=True, color=INK),
        chart(0, 168, 640, 250, "column", "monthly", "{monthly.Month}",
              [series("This year", "{monthly.ThisYear}", "#2563eb"), series("Last year", "{monthly.LastYear}", "#cbd5e1")]),
        label(655, 150, 404, 16, "Sales by product category", size=10, bold=True, color=INK),
        chart(655, 168, 404, 250, "pie", "category", "{category.Category}", [series("Sales", "{category.Sales}")]),
        label(0, 432, 400, 16, "Sales by territory", size=10, bold=True, color=INK),
        chart(0, 450, 400, 270, "bar", "territory", "{territory.Region}", [series("Sales", "{territory.Sales}", "#7c3aed")], legend=False),
        label(415, 432, 644, 16, "Top 10 products — monthly trend", size=10, bold=True, color=INK),
        table(415, 450, 644, 270, "products", [
            column("Product", "{products.Product}", 250),
            column("Sales", "{products.Total}", 110, "c0", "right", scale={"lowColor": "#eff6ff", "highColor": "#93c5fd"}),
            column("Jan → Dec", "{products.Trend}", 284, spark={"type": "line", "color": "#2563eb", "showArea": True, "highlightColor": "#dc2626"}),
        ], size=9),
    ]
    return report(
        "AdventureWorks — Executive Sales Dashboard", "aw-executive-dashboard", "free",
        body={"height": 735, "elements": els},
        sources=[sql("kpi", KPI, YEAR), sql("monthly", MONTHLY, YEAR), sql("category", CATEGORY, YEAR),
                 sql("territory", TERRITORY, YEAR), sql("products", TOP_PRODUCTS, YEAR)],
        params=[param("year", "number", "Calendar year", 2013, allowed=[2011, 2012, 2013, 2014])],
        page=landscape(), description="KPIs, monthly trend, category mix, territory ranking and product sparklines from one report.",
    )
