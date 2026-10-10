from lib import *

MONTHS = ",".join(f"({i})" for i in range(1, 13))

REPS = f"""
WITH m AS (
  SELECT h.SalesPersonID, MONTH(h.OrderDate) AS mo, SUM(h.TotalDue) AS amt, COUNT(*) AS cnt
  FROM Sales.SalesOrderHeader h WHERE h.SalesPersonID IS NOT NULL AND YEAR(h.OrderDate) = @year GROUP BY h.SalesPersonID, MONTH(h.OrderDate)),
tot AS (SELECT SalesPersonID, SUM(amt) AS Sales, SUM(cnt) AS Orders FROM m GROUP BY SalesPersonID)
SELECT p.FirstName + ' ' + p.LastName AS Rep, ISNULL(t.Name, '—') AS Territory, e.JobTitle,
       p.LastName AS Short, tot.Sales, tot.Orders, tot.Sales / tot.Orders AS AvgOrder, tot.Sales * sp.CommissionPct AS Commission,
       tot.Sales * 100.0 / SUM(tot.Sales) OVER () AS Share,
       (SELECT STRING_AGG(CAST(CAST(ROUND(ISNULL(x.amt, 0), 0) AS bigint) AS varchar(20)), ',') WITHIN GROUP (ORDER BY v.n)
        FROM (VALUES {MONTHS}) v(n) LEFT JOIN m x ON x.SalesPersonID = tot.SalesPersonID AND x.mo = v.n) AS Trend
FROM tot
JOIN Sales.SalesPerson sp ON sp.BusinessEntityID = tot.SalesPersonID
JOIN Person.Person p ON p.BusinessEntityID = tot.SalesPersonID
JOIN HumanResources.Employee e ON e.BusinessEntityID = tot.SalesPersonID
LEFT JOIN Sales.SalesTerritory t ON t.TerritoryID = sp.TerritoryID
ORDER BY tot.Sales DESC"""

MUTED = "#6b7280"


def build():
    els = [
        label(0, 0, 800, 26, "Sales Team Leaderboard", size=18, bold=True, color="#0f172a"),
        field(0, 28, 1000, 14, "Calendar year {param:year}  ·  share = representative sales ÷ total team sales (colour scale: red = small, green = large)", size=9, color=MUTED),
        chart(0, 56, 520, 250, "bar", "reps", "{reps.Short}", [series("Sales", "{reps.Sales}", "#0891b2")], legend=False,
              title="Sales by representative"),
        chart(540, 56, 519, 250, "bar", "reps", "{reps.Short}", [series("Commission earned", "{reps.Commission}", "#f59e0b")], legend=False,
              title="Commission earned"),
        label(0, 322, 600, 16, "Representative detail", size=11, bold=True),
        table(0, 342, 1059, 340, "reps", [
            column("Representative", "{reps.Rep}", 170),
            column("Title", "{reps.JobTitle}", 190),
            column("Territory", "{reps.Territory}", 110),
            column("Sales", "{reps.Sales}", 100, "c0", "right", scale={"lowColor": "#ecfeff", "highColor": "#22d3ee"}),
            column("Orders", "{reps.Orders}", 70, "n0", "right"),
            column("Avg order", "{reps.AvgOrder}", 90, "c0", "right"),
            column("Share %", "{reps.Share}", 80, "n1", "right",
                   scale={"lowColor": "#fecaca", "midColor": "#fef08a", "highColor": "#86efac", "min": 0, "max": 20}),
            column("Commission", "{reps.Commission}", 90, "c0", "right"),
            column("Jan → Dec", "{reps.Trend}", 229, spark={"type": "bar", "color": "#0891b2", "showArea": False, "highlightColor": "#dc2626"}),
        ], size=9),
    ]
    return report(
        "AdventureWorks — Sales Team Leaderboard", "aw-sales-leaderboard", "free",
        body={"height": 700, "elements": els},
        sources=[sql("reps", REPS, {"year": "{param:year}"})],
        params=[param("year", "number", "Calendar year", 2013, allowed=[2011, 2012, 2013, 2014])],
        page=landscape(), description="Ranked bar/column charts plus a table with colour-scale attainment and a bar sparkline per representative.",
    )
