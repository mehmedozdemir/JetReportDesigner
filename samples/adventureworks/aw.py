"""Helpers for building and checking the AdventureWorks showcase reports against a running instance."""
import json, os, sys, urllib.request, urllib.error

BASE = os.environ.get("JRD_URL", "http://localhost:8081")
EMAIL = os.environ.get("JRD_EMAIL", "showcase@example.com")
PASSWORD = os.environ["JRD_PASSWORD"]  # the showcase user's password; choose one when first registering
ORG = "AdventureWorks Showcase"
CONNECTION_NAME = "adventureworks"
# A read-only SQL login (db_datareader on AdventureWorks2022 only) — see README.md.
CONNECTION_STRING = os.environ.get(
    "AW_CONNECTION",
    "Server=sqlserver,1433;Database=AdventureWorks2022;User Id=aw_reader;Password=" + os.environ.get("AW_READER_PASSWORD", "") + ";TrustServerCertificate=True;Encrypt=False",
)


def call(method, path, body=None, token=None, raw=False):
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = "Bearer " + token
    data = json.dumps(body).encode("utf-8") if body is not None else None
    req = urllib.request.Request(BASE + path, method=method, data=data, headers=headers)
    try:
        with urllib.request.urlopen(req) as r:
            payload = r.read()
            return payload if raw else (json.loads(payload) if payload else None)
    except urllib.error.HTTPError as e:
        detail = e.read().decode("utf-8", "replace")
        raise RuntimeError(f"{method} {path} -> {e.code}: {detail[:1500]}") from None


def login():
    try:
        auth = call("POST", "/api/auth/register", {"email": EMAIL, "password": PASSWORD, "organizationName": ORG, "displayName": "AdventureWorks Showcase"})
    except RuntimeError:
        auth = call("POST", "/api/auth/login", {"email": EMAIL, "password": PASSWORD})
    return auth["token"]


def ensure_connection(token):
    for c in call("GET", "/api/connections", token=token):
        if c["name"] == CONNECTION_NAME:
            return c["id"]
    return call("POST", "/api/connections", {"name": CONNECTION_NAME, "provider": "sqlServer", "connectionString": CONNECTION_STRING}, token)["id"]


def render(token, definition, params=None, fmt="png", page=1, dpi=110):
    body = {"definition": definition, "parameters": params or {}}
    return call("POST", f"/api/render?format={fmt}&page={page}&dpi={dpi}", body, token, raw=True)
