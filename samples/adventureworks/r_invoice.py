from lib import *

HEADER = """
SELECT h.SalesOrderID, h.SalesOrderNumber, h.OrderDate, h.DueDate, h.ShipDate, h.PurchaseOrderNumber, h.AccountNumber,
       CASE h.Status WHEN 1 THEN 'In process' WHEN 2 THEN 'Approved' WHEN 3 THEN 'Backordered' WHEN 4 THEN 'Rejected' WHEN 5 THEN 'Shipped' ELSE 'Cancelled' END AS Status,
       h.SubTotal, h.TaxAmt, h.Freight, h.TotalDue,
       ISNULL(pp.FirstName + ' ' + pp.LastName, st.Name) AS Customer, ISNULL(st.Name, 'Individual customer') AS Store,
       ba.AddressLine1 AS BillAddress, ba.City AS BillCity, bsp.Name AS BillState, ba.PostalCode AS BillZip,
       sa.AddressLine1 AS ShipAddress, sa.City AS ShipCity, ssp.Name AS ShipState, sa.PostalCode AS ShipZip,
       sm.Name AS ShipMethod, ISNULL(cc.CardType, '—') AS CardType, t.Name AS Territory,
       ISNULL(sp.FirstName + ' ' + sp.LastName, 'Web order') AS SalesRep
FROM Sales.SalesOrderHeader h
JOIN Sales.Customer c ON c.CustomerID = h.CustomerID
LEFT JOIN Person.Person pp ON pp.BusinessEntityID = c.PersonID
LEFT JOIN Sales.Store st ON st.BusinessEntityID = c.StoreID
JOIN Person.Address ba ON ba.AddressID = h.BillToAddressID
JOIN Person.StateProvince bsp ON bsp.StateProvinceID = ba.StateProvinceID
JOIN Person.Address sa ON sa.AddressID = h.ShipToAddressID
JOIN Person.StateProvince ssp ON ssp.StateProvinceID = sa.StateProvinceID
JOIN Purchasing.ShipMethod sm ON sm.ShipMethodID = h.ShipMethodID
LEFT JOIN Sales.CreditCard cc ON cc.CreditCardID = h.CreditCardID
LEFT JOIN Sales.SalesTerritory t ON t.TerritoryID = h.TerritoryID
LEFT JOIN Person.Person sp ON sp.BusinessEntityID = h.SalesPersonID
WHERE h.SalesOrderID = @id"""

LINES = """
SELECT ROW_NUMBER() OVER (ORDER BY d.SalesOrderDetailID) AS Line, p.ProductNumber, p.Name AS Product,
       d.OrderQty AS Qty, d.UnitPrice, d.UnitPriceDiscount * 100 AS DiscountPct, d.LineTotal
FROM Sales.SalesOrderDetail d JOIN Production.Product p ON p.ProductID = d.ProductID
WHERE d.SalesOrderID = @id ORDER BY d.SalesOrderDetailID"""

INK, MUTED = "#111827", "#6b7280"


def build():
    header = [
        rect(0, 0, 722, 70, fill="#0f172a"),
        label(20, 14, 380, 26, "AdventureWorks Cycles", size=20, bold=True, color="#ffffff"),
        label(20, 42, 380, 14, "1 Bicycle Way · Bothell, WA 98011 · sales@adventure-works.com", size=8.5, color="#94a3b8"),
        label(480, 12, 222, 30, "INVOICE", size=24, bold=True, color="#ffffff", align="right"),
        field(480, 46, 222, 14, "{h.SalesOrderNumber}", size=10, color="#93c5fd", align="right", bold=True),

        label(0, 92, 220, 14, "BILL TO", size=8, bold=True, color=MUTED),
        field(0, 108, 220, 16, "{h.Customer}", size=11, bold=True),
        field(0, 126, 220, 14, "{h.Store}", size=9, color=MUTED),
        field(0, 140, 220, 14, "{h.BillAddress}", size=9),
        field(0, 154, 220, 14, "{h.BillCity}, {h.BillState} {h.BillZip}", size=9),

        label(250, 92, 220, 14, "SHIP TO", size=8, bold=True, color=MUTED),
        field(250, 108, 220, 14, "{h.ShipAddress}", size=9),
        field(250, 122, 220, 14, "{h.ShipCity}, {h.ShipState} {h.ShipZip}", size=9),
        field(250, 140, 220, 14, "Via {h.ShipMethod}", size=9, color=MUTED),

        rect(500, 88, 222, 92, fill="#f8fafc", border_color="#e2e8f0"),
        label(512, 96, 90, 13, "Order date", size=8.5, color=MUTED),
        field(600, 96, 110, 13, "{h.OrderDate}", "yyyy-MM-dd", size=9, bold=True, align="right"),
        label(512, 112, 90, 13, "Due date", size=8.5, color=MUTED),
        field(600, 112, 110, 13, "{h.DueDate}", "yyyy-MM-dd", size=9, bold=True, align="right"),
        label(512, 128, 90, 13, "Sales rep", size=8.5, color=MUTED),
        field(580, 128, 130, 13, "{h.SalesRep}", size=9, bold=True, align="right"),
        label(512, 144, 90, 13, "Territory", size=8.5, color=MUTED),
        field(600, 144, 110, 13, "{h.Territory}", size=9, bold=True, align="right"),
        label(512, 160, 90, 13, "Status", size=8.5, color=MUTED),
        field(600, 160, 110, 13, "{h.Status}", size=9, bold=True, align="right",
              rules=[rule("Status", "eq", "Shipped", color="#15803d"), rule("Status", "eq", "Cancelled", color="#b91c1c"),
                     rule("Status", "eq", "Backordered", color="#b45309")]),
    ]
    cols = [(0, 34, "#", "{lines.Line}", "n0", "right"), (44, 90, "PRODUCT NO.", "{lines.ProductNumber}", None, "left"),
            (140, 264, "DESCRIPTION", "{lines.Product}", None, "left"), (410, 44, "QTY", "{lines.Qty}", "n0", "right"),
            (456, 84, "UNIT PRICE", "{lines.UnitPrice}", "c2", "right"), (544, 52, "DISC. %", "{lines.DiscountPct}", "n0", "right"),
            (600, 122, "LINE TOTAL", "{lines.LineTotal}", "c2", "right")]
    footer = [
        line(0, 4, 722, "#9ca3af"),
        rect(0, 20, 340, 118, fill="#f8fafc", border_color="#e2e8f0"),
        label(12, 28, 316, 14, "Scan to open the order", size=8.5, bold=True, color=MUTED),
        barcode(12, 46, 90, 86, "https://adventure-works.example/orders/{h.SalesOrderID}", "qr", text=False),
        barcode(112, 62, 218, 54, "{h.SalesOrderNumber}", "code128"),
        label(460, 22, 130, 16, "Subtotal", size=10, color=MUTED),
        field(580, 22, 142, 16, "{h.SubTotal}", "c2", size=10, align="right"),
        label(460, 44, 130, 16, "Tax", size=10, color=MUTED),
        field(580, 44, 142, 16, "{h.TaxAmt}", "c2", size=10, align="right"),
        label(460, 66, 130, 16, "Freight", size=10, color=MUTED),
        field(580, 66, 142, 16, "{h.Freight}", "c2", size=10, align="right"),
        rect(460, 92, 262, 40, fill="#0f172a"),
        label(472, 104, 110, 18, "TOTAL DUE", size=11, bold=True, color="#ffffff"),
        field(560, 102, 152, 22, "{h.TotalDue}", "c2", size=15, bold=True, color="#ffffff", align="right"),
        line(0, 150, 722, "#e5e7eb"),
        field(0, 156, 722, 14, "Payment: {h.CardType} · PO {h.PurchaseOrderNumber} · Account {h.AccountNumber}", size=8.5, color=MUTED),
        label(0, 172, 722, 14, "Thank you for riding with AdventureWorks Cycles.", size=8.5, color=MUTED, italic=True),
    ]
    bands = [
        band("reportHeader", 196, header),
        band("pageHeader", 24, [label(x, 6, w, 14, t, size=7.5, bold=True, color=MUTED, align=al) for x, w, t, _, _, al in cols] + [line(0, 22, 722, "#9ca3af")]),
        band("detail", 17, [field(x, 1, w, 15, v, fmt=f, size=9, align=al, color=MUTED if t == "#" else None)
                            for x, w, t, v, f, al in cols], source="lines"),
        band("reportFooter", 190, footer),
        band("pageFooter", 22, [field(0, 6, 300, 14, "{h.SalesOrderNumber} · page {pageNumber()} of {totalPages()}", size=8, color=MUTED)]),
    ]
    return report(
        "AdventureWorks — Sales Invoice", "aw-sales-invoice", "banded", bands=bands,
        sources=[sql("h", HEADER, {"id": "{param:salesOrderId}"}), sql("lines", LINES, {"id": "{param:salesOrderId}"})],  # header source first: header/footer bands read its row 0
       
        params=[param("salesOrderId", "number", "Sales order ID", 43659, required=True)],
        description="A parameterised document: header, flowing line items over several pages, totals, QR code and barcode.",
    )
