"""Build (or update) every showcase report in the AdventureWorks showcase organization.

    python build_all.py              create/update all reports and folders
    python build_all.py r_invoice    only the named module(s)
"""
import importlib, sys, aw

# (module, folder). Order matters: a subreport must exist before the report that embeds it.
PLAN = [
    ("r_customers:build_child", "Customers"),
    ("r_dashboard", "Sales"),
    ("r_heatmap", "Sales"),
    ("r_customers", "Customers"),
    ("r_invoice", "Sales"),
    ("r_catalog", "Products"),
    ("r_inventory", "Products"),
    ("r_salespeople", "Sales"),
    ("r_vendors", "Purchasing"),
    ("r_employees", "People"),
    ("r_idcards", "People"),
    ("r_fulfillment", "Operations"),
]


def main(only):
    token = aw.login()
    cid = aw.ensure_connection(token)
    folders = {f["name"]: f["id"] for f in aw.call("GET", "/api/folders", token=token)}
    refs = {r["code"]: r["id"] for r in aw.call("GET", "/api/reports", token=token) if r.get("code")}
    for spec, folder in PLAN:
        mod_name, _, fn = spec.partition(":")
        if only and mod_name not in only:
            continue
        try:
            mod = importlib.import_module(mod_name)
        except ModuleNotFoundError:
            print("skip (not written yet):", mod_name)
            continue
        build = getattr(mod, fn or "build")
        d = build(refs) if build.__code__.co_argcount else build()
        for c in d["connections"]:
            c["connectionId"] = cid
        if folder not in folders:
            folders[folder] = aw.call("POST", "/api/folders", {"name": folder, "parentFolderId": None}, token)["id"]
        existing = refs.get(d["code"])
        if existing:
            cur = aw.call("GET", f"/api/reports/{existing}", token=token)
            d["id"] = existing
            saved = aw.call("PUT", f"/api/reports/{existing}", d, token)
            print("updated", d["code"])
        else:
            saved = aw.call("POST", "/api/reports", d, token)
            print("created", d["code"])
        refs[d["code"]] = saved["id"]
        aw.call("PUT", f"/api/reports/{saved['id']}/folder", {"folderId": folders[folder]}, token)


if __name__ == "__main__":
    main(set(sys.argv[1:]))
