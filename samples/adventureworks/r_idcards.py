from lib import *

CARDS = """
SELECT TOP (CAST(@top AS int)) e.BusinessEntityID AS EmpId, p.FirstName + ' ' + p.LastName AS Name, e.JobTitle,
       d.Name AS Department, e.HireDate, e.NationalIDNumber, ISNULL(ea.EmailAddress, '') AS Email,
       'MECARD:N:' + p.LastName + ',' + p.FirstName + CHAR(59) + 'EMAIL:' + ISNULL(ea.EmailAddress, '') + CHAR(59) + 'ORG:AdventureWorks Cycles' + CHAR(59) + CHAR(59) AS Card,  -- the SQL safety check rejects a literal semicolon
       CASE WHEN d.GroupName = 'Manufacturing' THEN 'Manufacturing' WHEN d.GroupName = 'Sales and Marketing' THEN 'Sales' ELSE 'Corporate' END AS Kind
FROM HumanResources.Employee e
JOIN Person.Person p ON p.BusinessEntityID = e.BusinessEntityID
JOIN HumanResources.EmployeeDepartmentHistory edh ON edh.BusinessEntityID = e.BusinessEntityID AND edh.EndDate IS NULL
JOIN HumanResources.Department d ON d.DepartmentID = edh.DepartmentID
LEFT JOIN Person.EmailAddress ea ON ea.BusinessEntityID = e.BusinessEntityID
WHERE e.CurrentFlag = 1 AND (@department IS NULL OR @department IN ('', 'All') OR d.Name = @department)
ORDER BY d.Name, p.LastName"""

W = 353  # two columns on A4 portrait: (722 - 16) / 2


def build():
    kind_bg = [rule("Kind", "eq", "Manufacturing", bg="#b45309"), rule("Kind", "eq", "Sales", bg="#0e7490"), rule("Kind", "eq", "Corporate", bg="#1e3a8a")]
    card = band("detail", 178, [
        rect(0, 4, W, 168, fill="#ffffff", border_color="#cbd5e1"),
        rect(0, 4, W, 38, fill="#1e3a8a", rules=kind_bg),
        label(14, 12, 220, 16, "ADVENTUREWORKS CYCLES", size=10, bold=True, color="#ffffff"),
        field(14, 27, 220, 12, "{c.Kind} · {c.Department}", size=7.5, color="#e0f2fe"),
        field(W - 90, 14, 76, 14, "ID {c.EmpId}", size=9, bold=True, color="#ffffff", align="right"),
        field(14, 54, 230, 22, "{c.Name}", size=15, bold=True, color="#0f172a"),
        field(14, 78, 230, 14, "{c.JobTitle}", size=9, color="#475569"),
        field(14, 100, 230, 12, "{c.Email}", size=8, color="#64748b"),
        label(14, 118, 80, 11, "HIRED", size=6.5, bold=True, color="#94a3b8"),
        field(14, 128, 80, 13, "{c.HireDate}", "yyyy-MM-dd", size=9, bold=True),
        label(100, 118, 100, 11, "NATIONAL ID", size=6.5, bold=True, color="#94a3b8"),
        field(100, 128, 110, 13, "{c.NationalIDNumber}", size=9, bold=True),
        barcode(14, 146, 200, 24, "{c.EmpId}", "code128", text=False),
        barcode(W - 92, 62, 80, 80, "{c.Card}", "qr", text=False),
        label(W - 92, 146, 80, 11, "scan to save contact", size=6, color="#94a3b8", align="center"),
    ], source="c")
    return report(
        "AdventureWorks — Employee ID Cards", "aw-employee-id-cards", "banded", bands=[
            band("reportHeader", 40, [label(0, 2, 600, 20, "Employee ID cards", size=14, bold=True, color="#0f172a"),
                                      field(0, 22, 600, 12, "Two cards per row · {param:top} cards for {param:department}", size=7.5, color="#64748b")]),
            card,
        ],
        sources=[sql("c", CARDS, {"top": "{param:top}", "department": "{param:department}"})],
        params=[param("top", "number", "How many cards", 12),
                param("department", "string", "Department", "Engineering",
                      allowed=["All", "Engineering", "Marketing", "Production", "Sales", "Finance", "Purchasing", "Quality Assurance"])],
        page={"size": "A4", "orientation": "portrait", "margins": {"top": 36, "right": 36, "bottom": 36, "left": 36}, "columns": 2, "columnSpacing": 16},
        description="A printable sheet of badges: two columns of repeating detail bands, QR contact code, barcode, colour by group.",
    )
