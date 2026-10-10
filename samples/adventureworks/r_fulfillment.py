from lib import *

MONTHS = ",".join(f"({i})" for i in range(1, 13))
YEAR = {"year": "{param:year}"}

CHANNEL = f"""
SELECT v.n AS MonthNum, LEFT(DATENAME(month, DATEFROMPARTS(2000, v.n, 1)), 3) AS Month,
  (SELECT COUNT(*) FROM Sales.SalesOrderHeader h WHERE YEAR(h.OrderDate) = @year AND MONTH(h.OrderDate) = v.n AND h.OnlineOrderFlag = 1) AS Online,
  (SELECT COUNT(*) FROM Sales.SalesOrderHeader h WHERE YEAR(h.OrderDate) = @year AND MONTH(h.OrderDate) = v.n AND h.OnlineOrderFlag = 0) AS Store
FROM (VALUES {MONTHS}) v(n) ORDER BY v.n"""

SPEED = """
SELECT t.Name AS Territory, COUNT(*) AS Orders, SUM(CAST(h.OnlineOrderFlag AS int)) AS OnlineOrders,
       SUM(CAST(h.OnlineOrderFlag AS int)) * 100.0 / COUNT(*) AS OnlinePct, AVG(h.TotalDue) AS AvgOrder
FROM Sales.SalesOrderHeader h JOIN Sales.SalesTerritory t ON t.TerritoryID = h.TerritoryID
WHERE YEAR(h.OrderDate) = @year GROUP BY t.Name ORDER BY COUNT(*) DESC"""

METHOD = """
SELECT sm.Name AS Method, COUNT(*) AS Orders, SUM(h.Freight) AS Freight, AVG(h.Freight) AS AvgFreight,
       SUM(h.Freight) * 100.0 / NULLIF(SUM(h.SubTotal), 0) AS FreightPct
FROM Sales.SalesOrderHeader h JOIN Purchasing.ShipMethod sm ON sm.ShipMethodID = h.ShipMethodID
WHERE YEAR(h.OrderDate) = @year GROUP BY sm.Name ORDER BY SUM(h.Freight) DESC"""

STATUS = """
SELECT t.[Group] AS Status, COUNT(*) AS Orders
FROM Sales.SalesOrderHeader h JOIN Sales.SalesTerritory t ON t.TerritoryID = h.TerritoryID
WHERE YEAR(h.OrderDate) = @year GROUP BY t.[Group]"""


def build():
    els = [
        rect(0, 0, 1059, 48, fill="#312e81"),
        label(16, 8, 700, 22, "Fulfilment & Channel Analysis", size=16, bold=True, color="#ffffff"),
        field(16, 30, 900, 14, "Calendar year {param:year}  ·  online vs store orders, channel mix by territory, freight by ship method", size=9, color="#c7d2fe"),
        chart(0, 62, 520, 250, "line", "ch", "{ch.Month}",
              [series("Online orders", "{ch.Online}", "#4f46e5"), series("Store orders", "{ch.Store}", "#f59e0b")], title="Orders per month by channel"),
        chart(540, 62, 250, 250, "pie", "st", "{st.Status}", [series("Orders", "{st.Orders}")], title="Orders by region"),
        chart(810, 62, 249, 250, "bar", "sp", "{sp.Territory}", [series("Orders", "{sp.Orders}", "#0ea5e9")], legend=False, title="Orders by territory"),
        label(0, 328, 600, 16, "Freight by ship method", size=11, bold=True),
        table(0, 348, 640, 90, "m", [
            column("Ship method", "{m.Method}", 190),
            column("Orders", "{m.Orders}", 80, "n0", "right"),
            column("Freight", "{m.Freight}", 130, "c0", "right", scale={"lowColor": "#eef2ff", "highColor": "#818cf8"}),
            column("Avg / order", "{m.AvgFreight}", 110, "c2", "right"),
            column("Freight % of sales", "{m.FreightPct}", 130, "n1", "right",
                   scale={"lowColor": "#f0fdf4", "midColor": "#fef9c3", "highColor": "#fecaca"}),
        ], size=9),
        label(660, 328, 399, 16, "Channel mix by territory", size=11, bold=True),
        table(660, 348, 399, 250, "sp", [
            column("Territory", "{sp.Territory}", 140),
            column("Orders", "{sp.Orders}", 70, "n0", "right"),
            column("Online %", "{sp.OnlinePct}", 90, "n1", "right",
                   scale={"lowColor": "#dcfce7", "midColor": "#fef08a", "highColor": "#a5b4fc"}),
            column("Avg order", "{sp.AvgOrder}", 99, "c0", "right"),
        ], size=9),
    ]
    return report(
        "AdventureWorks — Fulfilment & Channel Analysis", "aw-fulfilment-channels", "free",
        body={"height": 610, "elements": els},
        sources=[sql("ch", CHANNEL, YEAR), sql("st", STATUS, YEAR), sql("sp", SPEED, YEAR), sql("m", METHOD, YEAR)],
        params=[param("year", "number", "Calendar year", 2013, allowed=[2011, 2012, 2013, 2014])],
        page=landscape(), description="Line, pie and bar charts plus colour-scaled tables over order, channel and shipping data.",
    )
