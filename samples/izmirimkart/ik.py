"""Shared helpers for the İzmirim Kart card designs (login via JRD_EMAIL / JRD_PASSWORD, never stored)."""
import json, os, sys, uuid, urllib.request
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "adventureworks"))
import aw
from lib import *  # noqa
from lib import _el  # noqa

aw.EMAIL = os.environ.get("JRD_EMAIL", "mehmet.ozdemir@asisct.com")
aw.PASSWORD = os.environ["JRD_PASSWORD"]
DL = os.path.join(os.path.expanduser("~"), "Downloads", "IzmirimKart-Kart-Tasarimlari")
FOLDER = "Kart Tasarımları"


def login():
    return aw.call("POST", "/api/auth/login", {"email": aw.EMAIL, "password": aw.PASSWORD})["token"]


def upload(token, path):
    boundary = uuid.uuid4().hex
    data = open(path, "rb").read()
    mime = "image/jpeg" if path.lower().endswith(("jpg", "jpeg")) else "image/png"
    body = (f"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"{os.path.basename(path)}\"\r\nContent-Type: {mime}\r\n\r\n").encode() + data + f"\r\n--{boundary}--\r\n".encode()
    req = urllib.request.Request(aw.BASE + "/api/assets", data=body, method="POST",
                                 headers={"Authorization": "Bearer " + token, "Content-Type": "multipart/form-data; boundary=" + boundary})
    return json.loads(urllib.request.urlopen(req).read())["id"]


def image(x, y, w, h, source, fit="cover", border=None):
    return _el("image", B(x, y, w, h), image={"source": source, "fit": fit},
               style=style(border=border) if border else None)
