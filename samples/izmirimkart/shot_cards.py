"""Renders every saved card with a sample JSON (incl. a generated passport photo) to out/<code>.png.
    JRD_PASSWORD=... python shot_cards.py [slug ...]"""
import base64, io, sys
from PIL import Image, ImageDraw
from ik import *

def sample_photo():
    im = Image.new("RGB", (300, 400), "#cfe3f5")
    d = ImageDraw.Draw(im)
    d.ellipse((95, 70, 205, 200), fill="#e0b08a")
    d.pieslice((40, 220, 260, 520), 180, 360, fill="#34548a")
    b = io.BytesIO(); im.save(b, "PNG")
    return "data:image/png;base64," + base64.b64encode(b.getvalue()).decode()

PHOTO = sample_photo()
t = login()
reports = {r["code"]: r["id"] for r in aw.call("GET", "/api/reports", token=t) if r.get("code", "").startswith("izmirim-kart-")}
os.makedirs("out", exist_ok=True)
only = set(sys.argv[1:])
for code, rid in sorted(reports.items()):
    if only and code.replace("izmirim-kart-", "") not in only:
        continue
    row = {"tcKimlikNo": "12345678901", "ad": "AYŞE", "soyad": "YILMAZ", "kartNo": "1000 2000 3000 4000",
           "verilisTarihi": "15.01.2026", "sicilNo": "A-100245", "fotograf": PHOTO}
    png = aw.call("POST", f"/api/reports/{rid}/render?format=png&page=1&dpi=96", {"parameters": {}, "data": {"data": [row]}}, t, raw=True)
    open(f"out/{code}.png", "wb").write(png)
    print("ok", code)
