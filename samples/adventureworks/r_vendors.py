from lib import *

TOP = """
SELECT TOP 10 LEFT(v.Name, 24) AS Vendor, v.CreditRating, CASE WHEN v.PreferredVendorStatus = 1 THEN 'Preferred' ELSE '' END AS Preferred,
       CASE WHEN v.ActiveFlag = 1 THEN 'Active' ELSE 'Inactive' END AS Active,
       COUNT(*) AS Orders, SUM(h.TotalDue) AS Spend, AVG(h.TotalDue) AS AvgOrder,
       AVG(DATEDIFF(day, h.OrderDate, h.ShipDate) * 1.0) AS AvgDaysToShip
FROM Purchasing.PurchaseOrderHeader h JOIN Purchasing.Vendor v ON v.BusinessEntityID = h.VendorID
GROUP BY LEFT(v.Name, 24), v.CreditRating, v.PreferredVendorStatus, v.ActiveFlag ORDER BY SUM(h.TotalDue) DESC"""

MATRIX = """
SELECT LEFT(v.Name, 24) AS Vendor, CAST(YEAR(h.OrderDate) AS varchar(4)) AS Year, SUM(h.TotalDue) AS Spend
FROM Purchasing.PurchaseOrderHeader h JOIN Purchasing.Vendor v ON v.BusinessEntityID = h.VendorID
WHERE v.BusinessEntityID IN (SELECT TOP 10 VendorID FROM Purchasing.PurchaseOrderHeader GROUP BY VendorID ORDER BY SUM(TotalDue) DESC)
GROUP BY LEFT(v.Name, 24), YEAR(h.OrderDate) ORDER BY YEAR(h.OrderDate)"""

STATUS = """
SELECT CASE Status WHEN 1 THEN 'Pending' WHEN 2 THEN 'Approved' WHEN 3 THEN 'Rejected' ELSE 'Complete' END AS Status,
       COUNT(*) AS Orders, SUM(TotalDue) AS Value
FROM Purchasing.PurchaseOrderHeader GROUP BY Status ORDER BY Status"""

SHIP = """
SELECT REPLACE(REPLACE(sm.Name, 'CARGO TRANSPORT 5', 'CARGO 5'), 'OVERSEAS - DELUXE', 'OVERSEAS') AS Method, COUNT(*) AS Orders, SUM(h.Freight) AS Freight, SUM(h.TotalDue) AS Spend
FROM Purchasing.PurchaseOrderHeader h JOIN Purchasing.ShipMethod sm ON sm.ShipMethodID = h.ShipMethodID
GROUP BY sm.Name ORDER BY SUM(h.TotalDue) DESC"""


def build():
    els = [
        rect(0, 0, 1059, 48, fill="#134e4a"),
        label(16, 8, 700, 22, "Purchasing & Vendor Spend", size=16, bold=True, color="#ffffff"),
        label(16, 30, 900, 14, "Top 10 vendors by purchase-order value · credit rating 1 (best) to 5 · amber/red = weaker credit", size=9, color="#99f6e4"),
        chart(0, 62, 500, 215, "bar", "top", "{top.Vendor}", [series("Spend", "{top.Spend}", "#0d9488")], legend=False, title="Spend by vendor"),
        chart(515, 62, 250, 215, "pie", "status", "{status.Status}", [series("Orders", "{status.Orders}")], title="Orders by status"),
        chart(780, 62, 279, 215, "bar", "ship", "{ship.Method}", [series("Freight", "{ship.Freight}", "#f59e0b")], legend=False, title="Freight by ship method"),
        label(0, 292, 560, 16, "Vendor scorecard", size=11, bold=True),
        table(0, 312, 560, 250, "top", [
            column("Vendor", "{top.Vendor}", 190),
            column("Rating", "{top.CreditRating}", 55, "n0", "right",
                   scale={"lowColor": "#bbf7d0", "midColor": "#fef08a", "highColor": "#fca5a5", "min": 1, "max": 5}),
            column("Status", "{top.Preferred}", 75),
            column("Orders", "{top.Orders}", 55, "n0", "right"),
            column("Spend", "{top.Spend}", 100, "c0", "right", scale={"lowColor": "#f0fdfa", "highColor": "#5eead4"}),
            column("Ship days", "{top.AvgDaysToShip}", 85, "n1", "right"),
        ], size=9),
        label(580, 292, 479, 16, "Spend by vendor and year", size=11, bold=True),
        matrix(580, 312, 479, 270, "m", "{m.Vendor}", "{m.Year}", "{m.Spend}", row_header="Vendor", fmt="c0",
               scale={"lowColor": "#f0fdfa", "highColor": "#0f766e"}),
    ]
    return report(
        "AdventureWorks — Purchasing & Vendor Spend", "aw-purchasing-vendors", "free",
        body={"height": 600, "elements": els},
        sources=[sql("top", TOP), sql("m", MATRIX), sql("status", STATUS), sql("ship", SHIP)],
        page=landscape(), description="Bar, pie and column charts, a colour-scaled scorecard table and a vendor x year matrix.",
    )
