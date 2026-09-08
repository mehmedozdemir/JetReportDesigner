# API Contract

> Base path: `/api`. Auth: none (dev mode, V1). Errors: RFC 7807 `ProblemDetails`.
> OpenAPI document: `GET /openapi/v1.json` (Development only).
> Bold rows exist today (Phase 0); the rest are planned for the phase noted.

## Reports

| Method | Path | Body | Response | Phase |
|--------|------|------|----------|-------|
| **GET** | **`/api/reports`** | — | `ReportSummaryResponse[]` | **0** |
| **GET** | **`/api/reports/{id}`** | — | `ReportResponse` + `ETag` header; `404` if missing | **0** |
| **POST** | **`/api/reports`** | `ReportDefinition` | `201` + `Location` + `ETag` + `ReportResponse`; `422` on validation failure | **0** |
| **PUT** | **`/api/reports/{id}`** | `ReportDefinition` (+ `If-Match: "<token>"`) | `ReportResponse`; `404`; `409` on stale `If-Match`; `422` | **0** |
| **DELETE** | **`/api/reports/{id}`** | — | `204`; `404` | **0** |
| GET | `/api/reports/{id}/versions` | — | `[{ version, savedAt }]` | 4 |
| POST | `/api/reports/{id}/preview` | `{ parameters }` | `{ pages: [...] }` or `text/html` | 1 |
| POST | `/api/reports/{id}/render?format=pdf\|html` | `{ parameters }` | file stream | 1 |
| POST | `/api/render` | `{ definition, parameters }` | file stream (unsaved render) | 1 |

### ReportSummaryResponse
```json
{ "id": "uuid", "name": "string", "layoutMode": "free|banded",
  "createdAtUtc": "iso-8601", "updatedAtUtc": "iso-8601" }
```

### ReportResponse
```json
{ "id": "uuid", "definition": { /* ReportDefinition, see docs/02 */ },
  "createdAtUtc": "iso-8601", "updatedAtUtc": "iso-8601",
  "concurrencyToken": "uuid" }
```

### Concurrency
Every `ReportResponse` carries `concurrencyToken` and an `ETag: "<token>"` header.
`PUT` without `If-Match` overwrites unconditionally; with `If-Match` a stale token
returns `409`.

## Data source helpers  (Phase 3)

| Method | Path | Body | Response |
|--------|------|------|----------|
| POST | `/api/datasources/schema` | `{ kind, json\|rest\|sql }` | `{ fields: [{ name, type }] }` |
| POST | `/api/datasources/preview` | `{ kind, ..., take }` | `{ rows: [...] }` |

## Connections  (Phase 3)

| Method | Path | Notes |
|--------|------|-------|
| GET | `/api/connections` | `[{ id, name, provider }]` — never returns the string |
| POST | `/api/connections` | `{ name, provider, connectionString }` → `{ id }` |
| PUT | `/api/connections/{id}` | |
| DELETE | `/api/connections/{id}` | |
| POST | `/api/connections/{id}/test` | `{ ok: true }` or `400` |

## Meta

| Method | Path | Response | Phase |
|--------|------|----------|-------|
| **GET** | **`/api/meta/schema`** | JSON Schema for `ReportDefinition` (`application/schema+json`) | **0** |
| **GET** | **`/api/meta/health`** | `{ "status": "ok" }` | **0** |
| **GET** | **`/health/live`** | liveness (process up) | **0** |
| **GET** | **`/health/ready`** | readiness (storage reachable) | **0** |
