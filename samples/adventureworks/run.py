"""Usage: python run.py <module> [param=value ...]  — render a report definition to PNG pages for a quick look."""
import importlib, sys, os, aw

mod = importlib.import_module(sys.argv[1])
params = {}
for a in sys.argv[2:]:
    k, v = a.split("=", 1)
    params[k] = float(v) if v.replace(".", "", 1).isdigit() else v
t = aw.login()
cid = aw.ensure_connection(t)
d = mod.build()
for c in d["connections"]:
    c["connectionId"] = cid
os.makedirs("out", exist_ok=True)
try:
    png = aw.render(t, d, params)
except RuntimeError as e:
    print("ERR", e); sys.exit(1)
open(f"out/{sys.argv[1]}.png", "wb").write(png)
print("ok", len(png))
