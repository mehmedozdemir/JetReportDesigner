from lib import *

EMP = """
SELECT d.Name AS Department, d.GroupName AS DeptGroup, p.FirstName + ' ' + p.LastName AS Name, e.JobTitle, e.HireDate,
       DATEDIFF(year, e.HireDate, '2014-06-30') AS Tenure, e.VacationHours, e.SickLeaveHours,
       ISNULL(ea.EmailAddress, '') AS Email
FROM HumanResources.Employee e
JOIN Person.Person p ON p.BusinessEntityID = e.BusinessEntityID
JOIN HumanResources.EmployeeDepartmentHistory edh ON edh.BusinessEntityID = e.BusinessEntityID AND edh.EndDate IS NULL
JOIN HumanResources.Department d ON d.DepartmentID = edh.DepartmentID
LEFT JOIN Person.EmailAddress ea ON ea.BusinessEntityID = e.BusinessEntityID
WHERE e.CurrentFlag = 1 AND (@department IS NULL OR @department IN ('', 'All') OR d.Name = @department)
ORDER BY d.GroupName, d.Name, p.LastName, p.FirstName"""

MUTED = "#6b7280"
DEPTS = ["All", "Document Control", "Engineering", "Executive", "Facilities and Maintenance", "Finance", "Human Resources",
         "Information Services", "Marketing", "Production", "Production Control", "Purchasing", "Quality Assurance",
         "Research and Development", "Sales", "Shipping and Receiving", "Tool Design"]


def build():
    bands = [
        band("reportHeader", 62, [
            rect(0, 0, 722, 54, fill="#1e3a8a"),
            label(14, 8, 500, 22, "Employee Directory", size=17, bold=True, color="#ffffff"),
            field(14, 33, 690, 14, "Current employees  ·  department: {param:department}  ·  tenure counted to 30 Jun 2014", size=8.5, color="#bfdbfe"),
        ]),
        band("pageHeader", 22, [
            label(8, 4, 150, 14, "NAME", size=7.5, bold=True, color=MUTED),
            label(166, 4, 200, 14, "JOB TITLE", size=7.5, bold=True, color=MUTED),
            label(372, 4, 70, 14, "HIRED", size=7.5, bold=True, color=MUTED),
            label(446, 4, 40, 14, "YEARS", size=7.5, bold=True, color=MUTED, align="right"),
            label(490, 4, 56, 14, "VACATION", size=7.5, bold=True, color=MUTED, align="right"),
            label(556, 4, 160, 14, "CONTACT", size=7.5, bold=True, color=MUTED),
            line(0, 20, 722, "#9ca3af"),
        ]),
        band("groupHeader", 26, [
            rect(0, 2, 722, 22, fill="#dbeafe"),
            field(8, 6, 400, 16, "{e.Department}", size=11, bold=True, color="#1e3a8a"),
            field(500, 8, 214, 14, "{e.DeptGroup}", size=8.5, color="#3b82f6", align="right"),
        ], group=("e", "{e.Department}"), level=0, repeat=True),
        band("detail", 17, [
            field(8, 1, 156, 15, "{e.Name}", size=8.5, bold=True),
            field(166, 1, 204, 15, "{e.JobTitle}", size=8.5),
            field(372, 1, 70, 15, "{e.HireDate}", "yyyy-MM-dd", size=8.5, color=MUTED),
            field(446, 1, 40, 15, "{e.Tenure}", "n0", size=8.5, align="right",
                  rules=[rule("Tenure", "ge", 10, color="#15803d", bold=True)]),
            field(490, 1, 56, 15, "{e.VacationHours}", "n0", size=8.5, align="right",
                  rules=[rule("VacationHours", "ge", 70, bg="#fef3c7", color="#b45309", bold=True)]),
            field(556, 1, 160, 15, "{e.Email}", size=7.5, color=MUTED),
        ], source="e"),
        band("groupFooter", 22, [
            label(8, 4, 60, 14, "Headcount", size=8, italic=True, color=MUTED),
            field(70, 4, 40, 14, "{e.Name}", "n0", agg="count", scope="group", size=9, bold=True),
            label(200, 4, 170, 14, "longest tenure (years)", size=8, italic=True, color=MUTED, align="right"),
            field(446, 4, 40, 14, "{e.Tenure}", "n0", agg="max", scope="group", size=9, bold=True, align="right"),
            field(490, 4, 56, 14, "{e.VacationHours}", "n1", agg="average", scope="group", size=9, bold=True, align="right"),
            label(556, 4, 150, 14, "← average vacation hours", size=7.5, italic=True, color=MUTED),
        ], group=("e", "{e.Department}"), level=0),
        band("pageFooter", 22, [
            line(0, 2, 722, "#d1d5db"),
            field(0, 6, 300, 14, "Page {pageNumber()} of {totalPages()}", size=8, color=MUTED),
            label(300, 6, 422, 14, "Confidential — internal use", size=8, color=MUTED, align="right"),
        ]),
        band("reportFooter", 30, [
            line(0, 4, 722, "#1e3a8a", 1.5),
            label(8, 10, 140, 16, "TOTAL EMPLOYEES", size=9, bold=True),
            field(130, 10, 60, 16, "{e.Name}", "n0", agg="count", scope="report", size=11, bold=True, color="#1e3a8a"),
        ]),
    ]
    return report(
        "AdventureWorks — Employee Directory", "aw-employee-directory", "banded", bands=bands,
        sources=[sql("e", EMP, {"department": "{param:department}"})],
        params=[param("department", "string", "Department", "All", allowed=DEPTS)],
        description="Grouped list with per-group headcount, average and maximum, plus threshold highlighting.",
    )
