# AdventureWorks showcase

Twelve reports that pull **live data** from Microsoft's AdventureWorks2022 sample database through SQL data
sources — no inline sample JSON. Together they exercise most of the designer and renderer.

## Setup

1. Restore `AdventureWorks2022.bak` (from Microsoft's sample-databases page) into the `sqlserver` container
   from `docker-compose.yml`.
2. Create a read-only login so the showcase cannot change data:
   `CREATE LOGIN aw_reader WITH PASSWORD = '<password>'; USE AdventureWorks2022; CREATE USER aw_reader FOR LOGIN aw_reader; ALTER ROLE db_datareader ADD MEMBER aw_reader;`
3. `docker compose up -d` and open http://localhost:8081.
4. `JRD_PASSWORD=<choose> AW_READER_PASSWORD=<password> python build_all.py` registers `showcase@example.com` with that password (organisation *AdventureWorks
   Showcase*), creates the `adventureworks` connection and creates or updates every report by its code.
   Override with `JRD_URL`, `JRD_EMAIL`, `JRD_PASSWORD`, `AW_CONNECTION`.

The app needs a persistent Data Protection key ring (`DataProtection__KeyPath` + the `app-keys` volume in
`docker-compose.yml`); otherwise saved connection strings become unreadable after the container is recreated.

## Moving the reports to your own organisation

`adventureworks-showcase.jrdpkg` holds all reports. Create a connection named `adventureworks` in the target
organisation, then use the Transfer screen (import). `python export_package.py` regenerates the file.

## Reports

| Code | Folder | Highlights |
|---|---|---|
| aw-executive-dashboard | Sales | KPI cards, column / pie / bar charts, colour-scaled table with sparklines, year parameter |
| aw-satis-isi-haritasi | Sales | Matrix heat maps, tr-TR culture, growth table with colour scale |
| aw-sales-invoice | Sales | Banded layout, header/footer bound to a single row, QR + Code 128, totals box, order-id parameter |
| aw-sales-leaderboard | Sales | Bar charts, share-of-total column, per-row sparklines |
| aw-top-customers + aw-customer-recent-orders | Customers | Master/subreport with a custom-size child page, parameters |
| aw-product-catalog | Products | Grouped list (category → subcategory), group aggregates, conditional formatting, multi-page |
| aw-inventory-reorder | Products | Severity row rules, chart in the header, group subtotals, threshold parameter |
| aw-purchasing-vendors | Purchasing | Charts, scorecard with fixed-range colour scale, vendor × year matrix |
| aw-employee-directory | People | Groups with count / max / average footers, page numbers, department parameter |
| aw-employee-id-cards | People | Two-column pages, QR contact code, barcode, colour by group |
| aw-fulfilment-channels | Operations | Line / pie / bar charts, channel mix and freight tables with colour scales |

## Tools

- `shot.py <code> [page=N] [param=value]` renders a saved report to `out/<code>.png`.
- `run.py` previews an unsaved module; `lib.py` holds the builder helpers.
