"""Creates / updates the İzmirim Kart card designs in the "Kart Tasarımları" folder of the İzmirim Kart tenant.

    JRD_PASSWORD=... python build_cards.py            # all cards
    JRD_PASSWORD=... python build_cards.py ucretsiz-polis

Each card is a free-layout report: the downloaded card artwork is the page background (uploaded once to the asset
store), the printed labels stay part of the artwork, and the values are fields bound to the JSON data source "data":

    [ { "tcKimlikNo": "...", "ad": "...", "soyad": "...", "kartNo": "...", "verilisTarihi": "...", "fotograf": "..." } ]

`fotograf` is the passport photo: a data: URI (base64), an http(s) URL or an "asset:{id}" reference. The data is not
stored in the report — the calling system sends it with every render (see docs/entegrasyon.md); the sample row only
drives previews.
"""
import sys
from ik import *

W = 692
INK = "#111827"

# slug -> (title, source file, page height, photo box (x, y, w, h), value column x, [(field, value-y), ...])
# Coordinates are page pixels, read off each artwork's printed labels.
STD_PANEL = [("tcKimlikNo", 173), ("ad", 217), ("soyad", 260), ("verilisTarihi", 303), ("kartNo", 351, 172)]
STD_PANEL_PHOTO = (101, 154, 134, 162)
STD_BAR = [("ad", 160), ("soyad", 198), ("tcKimlikNo", 238), ("kartNo", 281)]

CARDS = {
    # Free of charge
    "ucretsiz-er-erbas": ("Er/Erbaş İzmirim Kartı", 415, STD_PANEL_PHOTO, 362, STD_PANEL),
    "ucretsiz-jandarma": ("Jandarma Kartı", 415, STD_PANEL_PHOTO, 362, STD_PANEL),
    "ucretsiz-sahil-guvenlik": ("Sahil Güvenlik Kartı", 415, STD_PANEL_PHOTO, 362, STD_PANEL),
    "ucretsiz-tuik": ("TÜİK Kartı", 415, (88, 135, 140, 185), 394, STD_BAR),
    "ucretsiz-zabita": ("Zabıta Kartı", 415, (93, 140, 139, 180), 396, [("ad", 162), ("soyad", 200), ("tcKimlikNo", 240), ("kartNo", 283)]),
    "ucretsiz-basin": ("Basın Kartı", 415, (88, 135, 140, 187), 394, [("tcKimlikNo", 191), ("ad", 230), ("soyad", 270), ("verilisTarihi", 312)]),
    "ucretsiz-65-yas": ("65 Yaş Kartı", 415, (88, 177, 114, 146), 364, [("tcKimlikNo", 193), ("ad", 232), ("soyad", 272), ("verilisTarihi", 312)]),
    "ucretsiz-polis": ("Emniyet Hizmetleri Personeli (POLİS) Kartı", 415, (88, 137, 140, 183), 360, [("ad", 192), ("soyad", 230), ("sicilNo", 270)]),
    # Discounted
    "indirimli-60-yas": ("60 Yaş Kartı", 415, (89, 137, 138, 183), 394, STD_BAR),
    "indirimli-ogretmen": ("Öğretmen Kartı", 415, (89, 160, 139, 205), 362, [("tcKimlikNo", 217), ("ad", 256), ("soyad", 296), ("kartNo", 357)]),
    "indirimli-ogrenci": ("Genç İzmirimkart", 437, (30, 170, 167, 235), 358, [("tcKimlikNo", 227), ("ad", 274), ("soyad", 320), ("kartNo", 369)]),
    # Other
    "diger-personal": ("Personel Kartı", 415, (86, 135, 140, 185), 392, STD_BAR),
    "diger-resmi-kurum": ("Resmi Kurum Kartı", 415, (88, 137, 140, 183), 392, STD_BAR),
    "diger-cocuk": ("Anne Dayanışma Kartı", 434, (48, 157, 153, 210), 355,
                    [("tcKimlikNo", 176), ("ad", 229), ("soyad", 280), ("verilisTarihi", 333), ("kartNo", 389, 125)]),
    # One artwork showing both the "Özel" (front) and "Refakatçı" (behind) cards; values go on the front card.
    "ucretsiz-refakatci": ("Özel ve Refakatçı Kartı", 415, (13, 183, 93, 135), 195, None),
}

SAMPLE = {"tcKimlikNo": "12345678901", "ad": "AYŞE", "soyad": "YILMAZ", "kartNo": "1000 2000 3000 4000",
          "verilisTarihi": "15.01.2026", "sicilNo": "A-100245", "fotograf": ""}
LABEL = {"tcKimlikNo": "T.C. Kimlik No", "ad": "Ad", "soyad": "Soyad", "kartNo": "Kart No", "verilisTarihi": "Veriliş tarihi",
         "sicilNo": "Sicil No"}


def special_refakatci():
    """Front (red "Özel") card of the combined artwork; coordinates are relative to the artwork, which is 692x415."""
    return [("tcKimlikNo", 250, 300), ("ad", 280, 300), ("soyad", 310, 300), ("verilisTarihi", 341, 300), ("kartNo", 374, 168)]


def build(slug, token, existing_assets):
    src = next(f for f in os.listdir(DL) if f.startswith(slug + "."))
    if src not in existing_assets:
        existing_assets[src] = upload(token, os.path.join(DL, src))
    asset_id = existing_assets[src]
    title, height, photo, col, rows = CARDS[slug]
    px, py, pw, ph = photo
    if rows is None:
        # Özel (front) card of the combined artwork
        rows = special_refakatci()
        col = 300
        px, py, pw, ph = 121, 240, 88, 120
    els = [image(px, py, pw, ph, "{data.fotograf}")]
    names = ["fotograf"]
    size = 10 if slug == "ucretsiz-refakatci" else 13 if slug in ("indirimli-ogrenci", "indirimli-ogretmen") else 15
    for name, y, *x in rows:
        names.append(name)
        left = x[0] if x else col
        els.append(field(left, y - 12, W - left - 24, 24, "{data." + name + "}", size=size, bold=True, color=INK, valign="middle"))
    sample = [{k: SAMPLE[k] for k in names}]
    d = report(
        title, "izmirim-kart-" + slug, "free",
        body={"height": height, "elements": els},
        sources=[{"name": "data", "kind": "json", "fields": [{"name": n, "type": "string"} for n in names],
                  "json": {"inlineData": json.dumps(sample, ensure_ascii=False), "resultPath": "$"}}],
        page={"size": "Custom", "orientation": "portrait", "customWidth": W, "customHeight": height,
              "margins": {"top": 0, "right": 0, "bottom": 0, "left": 0}, "columns": 1,
              "backgroundImage": {"source": "asset:" + asset_id, "fit": "fill"}},
        description="İzmirim Kart — " + title + ". Alanlar dışarıdan JSON ile gelir: " + ", ".join(names) + ".",
    )
    d["connections"] = []
    return d


def main(only):
    token = login()
    folders = {f["name"]: f["id"] for f in aw.call("GET", "/api/folders", token=token)}
    folder = folders[FOLDER]
    refs = {r["code"]: r["id"] for r in aw.call("GET", "/api/reports", token=token) if r.get("code")}
    assets = {}
    for slug in CARDS:
        if only and slug not in only:
            continue
        d = build(slug, token, assets)
        rid = refs.get(d["code"])
        if rid:
            d["id"] = rid
            saved = aw.call("PUT", f"/api/reports/{rid}", d, token)
            print("updated", d["code"])
        else:
            saved = aw.call("POST", "/api/reports", d, token)
            print("created", d["code"])
        aw.call("PUT", f"/api/reports/{saved['id']}/folder", {"folderId": folder}, token)


if __name__ == "__main__":
    main(set(sys.argv[1:]))
