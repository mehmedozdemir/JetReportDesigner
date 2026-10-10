"""Usage: python shot.py <report-code> [page=1] [param=value ...] — render a SAVED showcase report to out/<code>.png"""
import sys, os, aw

code = sys.argv[1]
page = 1
body = {"parameters": {}}
for a in sys.argv[2:]:
    k, v = a.split("=", 1)
    if k == "page":
        page = int(v)
    else:
        body["parameters"][k] = float(v) if v.replace(".", "", 1).isdigit() else v
t = aw.login()
rid = next(r["id"] for r in aw.call("GET", "/api/reports", token=t) if r.get("code") == code)
os.makedirs("out", exist_ok=True)
png = aw.call("POST", f"/api/reports/{rid}/render?format=png&page={page}&dpi=110", body, t, raw=True)
open(f"out/{code}.png", "wb").write(png)
print("ok", len(png))
