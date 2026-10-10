from lib import *

QUARTERS = """
SELECT t.Name AS Region, CONCAT(YEAR(h.OrderDate), ' Ç', DATEPART(quarter, h.OrderDate)) AS Quarter, SUM(h.TotalDue) AS Revenue
FROM Sales.SalesOrderHeader h JOIN Sales.SalesTerritory t ON t.TerritoryID = h.TerritoryID
WHERE h.OrderDate >= DATEFROMPARTS(@fromYear, 1, 1)
GROUP BY t.Name, YEAR(h.OrderDate), DATEPART(quarter, h.OrderDate)
ORDER BY YEAR(h.OrderDate), DATEPART(quarter, h.OrderDate)"""

CATYEAR = """
SELECT pc.Name AS Category, CAST(YEAR(h.OrderDate) AS varchar(4)) AS Year, SUM(d.LineTotal) AS Revenue
FROM Sales.SalesOrderDetail d
JOIN Sales.SalesOrderHeader h ON h.SalesOrderID = d.SalesOrderID
JOIN Production.Product p ON p.ProductID = d.ProductID
JOIN Production.ProductSubcategory ps ON ps.ProductSubcategoryID = p.ProductSubcategoryID
JOIN Production.ProductCategory pc ON pc.ProductCategoryID = ps.ProductCategoryID
GROUP BY pc.Name, YEAR(h.OrderDate) ORDER BY YEAR(h.OrderDate)"""

GROWTH = """
SELECT t.Name AS Region, t.[Group] AS Continent,
  SUM(CASE WHEN YEAR(h.OrderDate) = 2013 THEN h.TotalDue ELSE 0 END) AS Y2013,
  SUM(CASE WHEN YEAR(h.OrderDate) = 2012 THEN h.TotalDue ELSE 0 END) AS Y2012,
  (SUM(CASE WHEN YEAR(h.OrderDate) = 2013 THEN h.TotalDue ELSE 0 END) - SUM(CASE WHEN YEAR(h.OrderDate) = 2012 THEN h.TotalDue ELSE 0 END)) * 100.0
     / NULLIF(SUM(CASE WHEN YEAR(h.OrderDate) = 2012 THEN h.TotalDue ELSE 0 END), 0) AS Growth
FROM Sales.SalesOrderHeader h JOIN Sales.SalesTerritory t ON t.TerritoryID = h.TerritoryID
GROUP BY t.Name, t.[Group] ORDER BY Growth DESC"""


def build():
    els = [
        label(0, 0, 900, 26, "Bölge × Çeyrek Satış Isı Haritası", size=18, bold=True, color="#0f172a"),
        label(0, 28, 1000, 16, "Koyu hücre = yüksek ciro. Rapor tr-TR kültürüyle biçimlendirilir: binlik ayracı nokta, ondalık ayracı virgül.", size=9, color="#6b7280"),
        label(0, 58, 600, 16, "Bölgelere göre çeyreklik ciro (USD)", size=11, bold=True),
        matrix(0, 78, 1059, 270, "q", "{q.Region}", "{q.Quarter}", "{q.Revenue}", row_header="Bölge", fmt="n0",
               scale={"lowColor": "#eff6ff", "midColor": "#60a5fa", "highColor": "#1e3a8a"}),
        label(0, 372, 500, 16, "Kategori × yıl cirosu", size=11, bold=True),
        matrix(0, 392, 500, 130, "cy", "{cy.Category}", "{cy.Year}", "{cy.Revenue}", row_header="Kategori", fmt="n0",
               scale={"lowColor": "#f0fdf4", "highColor": "#15803d"}),
        label(530, 372, 529, 16, "2012 → 2013 büyüme (bölge bazında)", size=11, bold=True),
        table(530, 392, 529, 250, "g", [
            column("Bölge", "{g.Region}", 120),
            column("Kıta", "{g.Continent}", 90),
            column("2012", "{g.Y2012}", 100, "n0", "right"),
            column("2013", "{g.Y2013}", 100, "n0", "right"),
            column("Büyüme %", "{g.Growth}", 119, "n1", "right",
                   scale={"lowColor": "#fecaca", "midColor": "#fef9c3", "highColor": "#86efac", "min": -50, "max": 150}),
        ], size=9),
    ]
    return report(
        "AdventureWorks — Satış Isı Haritası (TR)", "aw-satis-isi-haritasi", "free",
        body={"height": 660, "elements": els},
        sources=[sql("q", QUARTERS, {"fromYear": "{param:fromYear}"}), sql("cy", CATYEAR), sql("g", GROWTH)],
        params=[param("fromYear", "number", "Başlangıç yılı", 2011, allowed=[2011, 2012, 2013])],
        page=landscape(), culture="tr-TR",
        description="Matris + renk skalası (ısı haritası), tr-TR kültürü ve Türkçe etiketler.",
    )
