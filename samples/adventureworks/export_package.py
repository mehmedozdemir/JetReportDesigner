"""Exports every showcase report to adventureworks-showcase.jrdpkg (import it from the Transfer screen)."""
import json
import urllib.request

import aw

token = aw.login()
reports = aw.call("GET", "/api/reports", token=token)
ids = [r["id"] for r in reports if r["code"].startswith("aw-")]
req = urllib.request.Request(
    aw.BASE + "/api/transfer/export",
    data=json.dumps({"reportIds": ids, "folderIds": None, "stripSampleData": False}).encode(),
    headers={"Authorization": "Bearer " + token, "Content-Type": "application/json"},
    method="POST",
)
with urllib.request.urlopen(req) as resp:
    data = resp.read()
open("adventureworks-showcase.jrdpkg", "wb").write(data)
print(f"{len(ids)} reports, {len(data)} bytes")
