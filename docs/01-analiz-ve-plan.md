# JetReportDesigner — Analiz, Mimari ve Fazlı Geliştirme Planı

> Durum: **Taslak / Onay bekliyor**
> Tarih: 2026-09-08
> Oturum: Analiz Toplantısı #1 çıktısı

---

## 0. Amaç

Web tarayıcıda çalışan, sürükle-bırak bir **rapor tasarımcısı** ve **render motoru**. Kullanıcı
veri kaynağı bağlar (REST API / statik JSON / ilişkisel veritabanı), sayfaya alanlar/tablolar
yerleştirir, önizler ve raporu **PDF / HTML** olarak üretir. Tasarımlar veritabanında saklanır.
DevExpress Report Designer benzeri bir hedef; V1 temel ihtiyaçları karşılar, sonraki sürümlerde
genişletilir.

---

## 1. Toplantıda Alınan Kararlar

| Konu | Karar |
|------|-------|
| Frontend | **React + TypeScript** (Vite) |
| Render motoru | **Kendi JSON şemamız** + PDF motoru **Faz 0'da spike ile seçilecek** (aday: QuestPDF / PdfSharp+MigraDoc / PuppeteerSharp-Chromium). Ayrıca HTML önizleme. |
| Rapor modeli | **İkisi de**: `banded` (bantlı) ve `free` (serbest yerleşim) — rapor bazında mod seçimi |
| Veri kaynakları | **REST API (GET/JSON)**, **Statik JSON**, **İlişkisel DB (SQL Server / Oracle / PostgreSQL)**, **Parametre-only** |
| Backend | **ASP.NET Core 10 + EF Core 10** Web API |
| Tasarım deposu | **Veritabanı** — SQL Server, Oracle ve PostgreSQL'de saklanabilir (çoklu provider). Oracle **Faz 3'te** devreye alınır. |
| Auth | **Yok (dev modu)** — V1'de açık, güvenli iç ağda çalışır. Sonraya bırakıldı. |
| Excel (XLSX) çıktı | **V1.1'e ertelendi** — mimaride yer açılır, V1'de PDF + HTML yeterli |
| Repo & dağıtım | **Monorepo** (`web/` bu repoda) + **tek Docker imajı** (API, build'lenmiş React'ı statik servis eder). Git: **`git init` + GitHub** uzak repo. |
| Bu adımın çıktısı | **Yazılı spec + plan** (kod yok); onay sonrası geliştirmeye geçilir |

### Toplantı #1 — Ek kararlar (2. tur)
- **PDF motoru sabitlenMEDİ.** Lisans istenmiyor → Faz 0'da kısa bir teknik spike ile karar:
  QuestPDF (Community lisans kısıtı), **PdfSharp + MigraDoc** (MIT, kısıtsız), **PuppeteerSharp**
  (headless Chromium, HTML→PDF; dağıtımda Chromium bağımlılığı). `.Rendering` içinde
  `IPdfRenderer` soyutlaması → motor değiştirilebilir kalır.
- **Oracle** Faz 0-2 kapsamı dışında; SQL Server + PostgreSQL ile ilerlenir, Oracle Faz 3'te eklenir.
- **Monorepo + tek container**: API `wwwroot`'tan React build'ini servis eder; SPA fallback route.
- **Git**: Faz 0'da `git init` + ilk commit + `.gitignore`. GitHub uzak repo kullanıcı tarafından
  oluşturulacak (bu makinede `gh` CLI yok); URL verilince `origin` bağlanır. Push kullanıcı onayıyla.

### Ortam kontrolü (bu makine)
- .NET SDK **10.0.400** ✓ · Node **v23.11.1** ✓ · git **2.55** ✓ · `gh` CLI **yok** (GitHub reposu elle açılacak)

### Kapsam uyarısı
"İkisi de" rapor modeli + 4 veri kaynağının tamamı V1 için geniş. **Mimari hepsini kaldıracak
şekilde** kurulur, ancak **implementasyon fazlara bölünür** (bkz. Bölüm 9). Serbest yerleşim +
statik JSON önce; bantlı raporlar ve canlı veri kaynakları sonraki fazlarda.

---

## 2. Sistem Bileşenleri

```
┌─────────────────────────────────────────────────────────────────────┐
│  Tarayıcı — React Designer (web/)                                    │
│  • WYSIWYG kanvas (free + banded)   • Özellik paneli                 │
│  • Alan/veri ağacı                  • Parametre & veri kaynağı UI    │
│  • HTML önizleme                    • Kaydet / Yükle / Liste         │
└───────────────┬─────────────────────────────────────────────────────┘
                │ REST/JSON (OpenAPI sözleşmesi)
┌───────────────▼─────────────────────────────────────────────────────┐
│  JetReportDesigner.Api  (ASP.NET Core 10)                           │
│  Controllers · DI · Validation · Data Protection (bağlantı şifreleme)│
└──┬───────────────┬──────────────────┬───────────────┬───────────────┘
   │               │                  │               │
┌──▼─────────┐ ┌───▼──────────┐ ┌─────▼─────────┐ ┌───▼────────────┐
│ .Core      │ │ .Storage     │ │ .DataSources  │ │ .Rendering     │
│ Rapor      │ │ EF Core 10   │ │ REST bağlacı  │ │ Render ağacı   │
│ modeli     │ │ DbContext    │ │ JSON bağlacı  │ │ Sayfalama      │
│ Arayüzler  │ │ Repos        │ │ SQL bağlacı   │ │ QuestPDF emit  │
│ Validation │ │ 3 provider + │ │ Şema keşfi    │ │ HTML emit      │
│ Expression │ │ migrations   │ │ Bağlantı yön. │ │ Aggregate      │
└────────────┘ └──────┬───────┘ └───────────────┘ └────────────────┘
                      │
            ┌─────────▼──────────┐
            │ SQL Server / Oracle │
            │ / PostgreSQL        │  ← tasarım deposu (ReportDefinition JSON)
            └─────────────────────┘
```

| # | Bileşen | Sorumluluk | Ana risk |
|---|---------|-----------|----------|
| 1 | **Designer (web/)** | Kanvas, DnD, özellik paneli, önizleme, kaydet/yükle | Kanvas & DnD karmaşıklığı; cetvel/hizalama/zoom/undo |
| 2 | **Report Definition (.Core)** | Raporu tanımlayan JSON modeli + doğrulama | Şemayı baştan doğru kurmak; sonradan kırıcı değişiklik pahalı |
| 3 | **DataSources (.DataSources)** | REST/JSON/SQL bağlantısı, sorgu, parametre, şema keşfi | Kimlik bilgisi saklama, SSRF, SQL injection, provider çeşitliliği |
| 4 | **Rendering (.Rendering)** | JSON + veri → sayfalanmış çıktı (PDF/HTML) | **En büyük teknik risk**: sayfalama, bant taşması, tablo kırılması |
| 5 | **Storage (.Storage)** | Tasarımları kaydet/yükle/listele/versiyonla; 3 DB provider | Çoklu-provider migration bakım yükü |
| 6 | **Api** | HTTP yüzeyi, DI, doğrulama, hata yönetimi | Standart |

---

## 3. Teknoloji Yığını (kesinleşmiş)

### Backend
- **.NET 10 / ASP.NET Core 10** — Web API (controllers)
- **EF Core 10** — çoklu provider:
  - `Microsoft.EntityFrameworkCore.SqlServer`
  - `Npgsql.EntityFrameworkCore.PostgreSQL`
  - `Oracle.EntityFrameworkCore` — ⚠️ **Faz 0'da sürüm uyumu doğrulanacak** (Oracle provider'ı .NET major sürümlerinin gerisinde kalabiliyor; gerekiyorsa pinlenir)
- **PDF motoru — Faz 0 spike ile seçilecek.** `.Rendering` bir `IPdfRenderer` arayüzü tanımlar; adaylar:
  - **PdfSharp + MigraDoc** — MIT, lisans kısıtı yok. Alçak seviye çizim + akış tabanlı belge. Ön aday.
  - **QuestPDF** — modern API, iyi sayfalama; Community lisansı gelir eşiğiyle sınırlı (kısıt istenmiyor).
  - **PuppeteerSharp** — HTML→PDF (Chromium). HTML emitter'ı tek çıktı yoluna indirger; ama container'a Chromium bağımlılığı ekler, kaynak tüketimi yüksek.
  - Spike çıktısı: aynı örnek raporu 2-3 motorla render edip fidelity/performans/dağıtım maliyeti kıyası → karar `docs/`'a yazılır.
- **ASP.NET Core Data Protection** — kayıtlı DB bağlantı string'lerinin şifrelenmesi
- **FluentValidation** (veya minimal custom) — ReportDefinition doğrulama
- **Serilog** — yapılandırılmış loglama
- **xUnit** — testler

### Frontend (`web/`)
- **React 18+ / TypeScript / Vite**
- Kanvas & DnD: **react-konva** (Konva.js) *veya* HTML overlay + **dnd-kit** — Faz 1 başında PoC ile karar
- State: **Zustand** (hafif, tasarımcı state'i için yeterli)
- HTTP: **openapi-typescript** ile backend sözleşmesinden tip üretimi + fetch wrapper
- UI kit: **Radix UI** primitives + kendi stil sistemimiz (ağır bir component lib'e bağımlı olmamak için)

### Ortam
- **Docker Compose** — dev'de sqlserver + postgres + oracle-xe (veya gvenli-uzak Oracle) ayağa kaldırır
- **.editorconfig**, Directory.Build.props, merkezi paket sürüm yönetimi (`Directory.Packages.props`)

### Uygulanacak standartlar
- `aspnet-core` skill'i (Clean/Onion katmanlama, DI, controller ince, iş mantığı servis/handler'da)
- `engineering-standards` skill'i (proje yapısı, commit mesajları, versiyonlama, CHANGELOG)
- `frontend-design` skill'i (designer UI'si için)

---

## 4. Çözüm Yapısı

```
JetReportDesigner/
├── JetReportDesigner.sln
├── Directory.Build.props
├── Directory.Packages.props
├── docker-compose.yml
├── .editorconfig
├── docs/
│   ├── 01-analiz-ve-plan.md              ← bu dosya
│   ├── 02-report-definition-schema.md    ← şema referansı (Faz 0'da kesinleşir)
│   └── 03-api-contract.md                ← OpenAPI özeti
├── src/
│   ├── JetReportDesigner.Core/           # Rapor modeli, arayüzler, expression, validation
│   ├── JetReportDesigner.DataSources/    # REST / JSON / SQL bağlaçları, şema keşfi
│   ├── JetReportDesigner.Rendering/      # Render ağacı, sayfalama, QuestPDF & HTML emitter
│   ├── JetReportDesigner.Storage/        # EF Core DbContext, entity'ler, repository'ler
│   ├── JetReportDesigner.Storage.Migrations.SqlServer/
│   ├── JetReportDesigner.Storage.Migrations.PostgreSql/
│   ├── JetReportDesigner.Storage.Migrations.Oracle/
│   └── JetReportDesigner.Api/            # ASP.NET Core 10 host
├── web/                                   # React + TS + Vite designer
├── tests/
│   ├── JetReportDesigner.Core.Tests/
│   ├── JetReportDesigner.Rendering.Tests/
│   ├── JetReportDesigner.DataSources.Tests/
│   └── JetReportDesigner.Api.Tests/       # WebApplicationFactory entegrasyon
└── samples/
    ├── data/*.json                        # örnek veri setleri
    └── reports/*.json                     # örnek rapor tanımları
```

**Bağımlılık yönü:** `Api → {Rendering, DataSources, Storage} → Core`. `Core` hiçbir şeye bağımlı değil.

### Çoklu-provider EF Core deseni
- `DbContext` tek yerde (`.Storage`), provider'a özgü kod yok.
- Her provider için ayrı **migrations assembly** (yukarıdaki 3 proje).
- Runtime'da `appsettings` içindeki `Storage:Provider` (`SqlServer|Oracle|PostgreSql`) + `Storage:ConnectionString` ile `UseSqlServer/UseOracle/UseNpgsql` ve ilgili `MigrationsAssembly` seçilir.
- CI'da 3 provider için de `dotnet ef migrations` derlenir; entegrasyon testleri Testcontainers ile en az SQL Server + PostgreSQL üzerinde koşar (Oracle opsiyonel/nightly).

---

## 5. Report Definition — JSON Şeması (taslak v0)

> Bu, sistemin kalbidir. Faz 0'da C# modeli + JSON Schema + validation olarak kesinleşir.
> `docs/02-report-definition-schema.md` canlı referans olur. Kırıcı değişiklikler `schemaVersion` ile yönetilir.

```jsonc
{
  "schemaVersion": 1,
  "id": "e2c1...guid",
  "name": "Musteri Siparis Listesi",
  "description": "",
  "layoutMode": "banded",              // "banded" | "free"

  "page": {
    "size": "A4",                       // A4 | A5 | Letter | Legal | Custom
    "orientation": "portrait",          // portrait | landscape
    "customWidth": null,                // Custom ise (birim: 1/96 inch = "px")
    "customHeight": null,
    "margins": { "top": 40, "right": 40, "bottom": 40, "left": 40 },
    "columns": 1                        // V1: yalnızca 1 (çok kolonlu sonraya)
  },

  "unit": "px",                         // İç koordinat birimi: 1/96 inch. UI mm/cm gösterir, dönüştürür.

  "parameters": [
    {
      "name": "startDate",
      "type": "string|number|boolean|date|datetime",
      "label": "Başlangıç Tarihi",
      "defaultValue": null,
      "required": true,
      "allowedValues": null              // opsiyonel sabit liste
    }
  ],

  "connections": [                       // kayıtlı DB bağlantısına referans (string burada TUTULMAZ)
    { "name": "erp", "connectionId": "9f...guid", "provider": "sqlserver" }
  ],

  "dataSources": [
    {
      "name": "orders",
      "kind": "json",                    // "rest" | "json" | "sql" | "none"

      "json":  { "inlineData": "[ ... ]" },

      "rest":  {
        "url": "https://api.ornek.com/orders",
        "method": "GET",
        "headers": { "Authorization": "Bearer {param:token}" },
        "query":   { "from": "{param:startDate}" },
        "resultPath": "$.data.items"     // JSONPath; kök dizi ise "$"
      },

      "sql":   {
        "connection": "erp",
        "commandText": "SELECT * FROM orders WHERE order_date >= :startDate",
        "parameters": [ { "name": "startDate", "value": "{param:startDate}" } ],
        "timeoutSeconds": 30,
        "maxRows": 50000
      },

      "fields": [                        // keşfedilen/beyan edilen şema (designer alan ağacı için)
        { "name": "id",        "type": "number" },
        { "name": "customer",  "type": "string" },
        { "name": "total",     "type": "number" },
        { "name": "orderDate", "type": "date"   }
      ]
    }
  ],

  "styles": {                            // adlandırılmış stiller (opsiyonel, elemanlar "styleRef" ile kullanır)
    "h1": { "font": { "family": "Arial", "size": 16, "bold": true }, "color": "#111827" }
  },

  // ── layoutMode = "banded" ──────────────────────────────────────────
  "bands": [
    {
      "type": "reportHeader",           // reportHeader | pageHeader | groupHeader
                                        // | detail | groupFooter | pageFooter | reportFooter
      "height": 60,
      "visible": true,
      "elements": [ /* Element[] */ ]
    },
    {
      "type": "groupHeader",
      "height": 24,
      "group": { "dataSource": "orders", "expression": "{orders.customer}", "sort": "asc" },
      "repeatOnEveryPage": true,
      "elements": [ ... ]
    },
    {
      "type": "detail",
      "height": 20,
      "dataSource": "orders",
      "elements": [ ... ]               // her satır için bir kez render edilir
    },
    {
      "type": "groupFooter",
      "height": 22,
      "elements": [ /* aggregate alanları */ ]
    }
  ],

  // ── layoutMode = "free" ───────────────────────────────────────────
  "body": {
    "height": 1000,                     // sabit; sığmayınca sayfalara bölünür
    "elements": [ /* Element[] — mutlak konumlu, tekrar yok */ ]
  }
}
```

### Element ortak alanları

```jsonc
{
  "id": "el_001",
  "type": "label",                      // label | field | table | image | line | rectangle | pageInfo
  "bounds": { "x": 0, "y": 0, "width": 120, "height": 20 },  // band'e (banded) veya sayfaya (free) göreli
  "styleRef": "h1",
  "style": {                            // styleRef üstüne yazar
    "font": { "family": "Arial", "size": 10, "bold": false, "italic": false },
    "color": "#000000",
    "background": null,
    "align": "left",                    // left | center | right | justify
    "valign": "top",                    // top | middle | bottom
    "border": { "top": 0, "right": 0, "bottom": 0, "left": 0, "color": "#000000" },
    "padding": { "top": 2, "right": 2, "bottom": 2, "left": 2 }
  },
  "visibleWhen": null,                  // opsiyonel boolean expression

  // tipe özgü:
  "text": "Statik metin",                       // label
  "value": "{orders.total}",                    // field — binding veya expression
  "format": "n2",                               // n0/n2/c/d/dd.MM.yyyy/... (.NET format + özel)
  "aggregate": null,                            // field: sum|count|avg|min|max|first|last
  "aggregateScope": "group",                    // group | report | page
  "image": { "source": "url|base64|{binding}", "fit": "contain" },
  "line":  { "orientation": "horizontal" },
  "table": {
    "dataSource": "orders",
    "showHeader": true,
    "columns": [
      { "header": "Müşteri", "value": "{orders.customer}", "width": 200, "format": null },
      { "header": "Tutar",   "value": "{orders.total}",    "width": 80,  "format": "n2", "align": "right" }
    ]
  }
}
```

### Binding & Expression
- **Binding**: `{dataSource.field}` — geçerli satır bağlamından değer.
- **Parametre**: `{param:name}` — REST/SQL config'inde ve expression'da.
- **Expression** (V1 minimal): `+ - * /`, string birleştirme, `if(cond, a, b)`, `sum()/count()/avg()/min()/max()`, `format(value, fmt)`, `pageNumber()`, `totalPages()`, `now()`.
- Parser: küçük bir Pratt/recursive-descent değerlendirici (`.Core` içinde). Faz 1'de yalnızca binding + format; expression Faz 2.

### V1 sınırları (bilinçli)
- Bantlar sabit yükseklik; "auto-height / can-grow", "push-up", "collapse" **yok** (V1.1).
- Elemanlar band/sayfa sınırını aşamaz (designer engeller).
- Tek seviye gruplama; iç içe grup **yok** (V1.1).
- Çok kolonlu düzen, alt rapor, chart, barkod, matris/pivot **yok** (backlog).

---

## 6. Rendering Motoru

### Pipeline
```
ReportDefinition + parametre değerleri
  │
  ├─ 1. Doğrula (şema + binding referansları)
  ├─ 2. Parametreleri çöz (default + kullanıcı girdisi + tip zorlama)
  ├─ 3. Veri kaynaklarını çalıştır → DataSet[]  (List<IDictionary<string,object?>>)
  │       REST → HTTP + JSONPath | JSON → parse | SQL → ADO.NET/Dapper
  ├─ 4. Render ağacı kur
  │       free   → body elemanları → sayfalara böl (y taşınca yeni sayfa)
  │       banded → reportHeader → [grup değişiminde groupHeader/Footer]
  │                → her detay satırı için detail → pageHeader/Footer her sayfada
  │                → aggregate'leri hesapla (page/group/report kapsamı)
  ├─ 5. Emit
  │       PDF  → QuestPDF (elemanları QuestPDF composable'larına eşle)
  │       HTML → deterministik HTML/CSS (designer önizleme + tarayıcıdan yazdır)
  └─ 6. Stream döndür  (application/pdf | text/html)
```

### PDF emitter (`IPdfRenderer`)
- Render ağacı motordan bağımsız: sayfalar → bloklar → konumlu elemanlar (mutlak x/y/w/h + stil).
- Emitter bu ağacı seçilen motora çevirir:
  - **PdfSharp/MigraDoc**: MigraDoc akış modeli banded rapora yakın; konumlu (free) için PdfSharp `XGraphics` ile mutlak çizim.
  - **QuestPDF**: `Page`+`Column`+sabit yükseklikli bloklar; konumlu eleman için `Layers`/translate.
  - **PuppeteerSharp**: HTML emitter çıktısını Chromium'a bas.
- Eleman eşlemesi: `label`/`field` → metin; `table` → tablo; `image` → resim; `line`/`rectangle` → çizim; `pageInfo` → motorun sayfa numarası API'si.
- **Sayfalama**: motorun kendi sayfa kırma yeteneğine yaslan. Banded'da detay tekrarını biz üretiriz; taşma kırılımını motor yapar. Motor bunu yapamıyorsa (saf PdfSharp) basit bir dikey akış/sayfalayıcı `.Rendering` içinde yazılır.

### HTML emitter
- Önizleme ve "tarayıcıdan yazdır" için. `@page` CSS + mm ölçüler.
- Amaç birebir PDF eşitliği değil; hızlı geri bildirim. PDF referans çıktıdır.

### Test stratejisi
- Golden-file: örnek rapor + sabit veri → üretilen PDF'ten metin katmanı çıkar, beklenen metin/konum snapshot'ı ile karşılaştır.
- Sayfalama birim testleri: N satır → beklenen sayfa sayısı, grup footer yerleşimi.

---

## 7. Data Source Katmanı

| Bağlayıcı | Giriş | Şema keşfi | Güvenlik |
|-----------|-------|-----------|----------|
| **JSON (statik)** | Yapıştırılan/yüklenen JSON | İlk N kaydı örnekle → alan+tip çıkar | Boyut limiti |
| **REST** | URL, method(GET), headers, query, resultPath(JSONPath) | Bir kez çağır → örnek yanıttan alan çıkar | **SSRF**: izinli host allow-list; iç IP/loopback engel; timeout; yanıt boyutu limiti; redirect kontrolü |
| **SQL** | Kayıtlı bağlantı + `commandText` + parametreler | `SELECT ... WHERE 1=0` / schema-only reader → kolon+tip | Parametreli sorgu zorunlu; salt-okunur hesap **önerisi** dokümante; `maxRows` + `timeout`; komut whitelist (yalnız `SELECT`/`WITH` — V1 heuristik) |

### Kayıtlı bağlantılar
- `Connection` entity: `Id, Name, Provider, EncryptedConnectionString, CreatedAt`.
- String **ASP.NET Data Protection** ile şifreli saklanır; API asla düz metin döndürmez (yalnız `Name/Provider`).
- CRUD + "test et" ucu (bağlanır, `SELECT 1`).

### Parametre akışı
- Designer parametreleri tanımlar → render isteğinde `{ "startDate": "2026-01-01" }` gövdesi → REST query / SQL parametresi / expression'a enjekte.

---

## 8. API Yüzeyi (taslak — `docs/03-api-contract.md`)

```
# Raporlar
GET    /api/reports                      → [{ id, name, layoutMode, updatedAt }]
GET    /api/reports/{id}                 → ReportDefinition (tam JSON)
POST   /api/reports                      → oluştur (body: ReportDefinition)   → { id }
PUT    /api/reports/{id}                 → güncelle (optimistic concurrency: rowVersion)
DELETE /api/reports/{id}
GET    /api/reports/{id}/versions        → [ { version, savedAt } ]           (Faz 4)
GET    /api/reports/{id}/versions/{v}    → o sürümün JSON'u                    (Faz 4)

# Önizleme & Render
POST   /api/reports/{id}/preview         body: { parameters }  → { pages: [...] } | text/html
POST   /api/reports/{id}/render          body: { parameters }  ?format=pdf|html → dosya
POST   /api/render                       body: { definition, parameters }       → geçici (kaydetmeden) render

# Veri kaynağı yardımcıları
POST   /api/datasources/schema           body: { kind, rest|json|sql }  → { fields: [...] }
POST   /api/datasources/preview          body: { kind, ... , take: 20 } → { rows: [...] }

# Bağlantılar
GET    /api/connections                  → [{ id, name, provider }]
POST   /api/connections                  body: { name, provider, connectionString } → { id }
PUT    /api/connections/{id}
DELETE /api/connections/{id}
POST   /api/connections/{id}/test        → { ok: true } | 400

# Meta
GET    /api/meta/schema                  → JSON Schema (ReportDefinition)
GET    /api/meta/health
```

- Hata modeli: RFC 7807 `ProblemDetails`.
- CORS: `web/` origin'i için açık (dev).
- OpenAPI: `Microsoft.AspNetCore.OpenApi` + Scalar/Swagger UI. `web/` tipleri buradan üretilir.

---

## 9. Fazlı Geliştirme Planı

Her faz **çalışan, gösterilebilir** bir dikey dilim üretir. Faz sonunda "Doğrulama" kriterleri geçmeden sonrakine geçilmez.

### Faz 0 — İskelet, Sözleşmeler ve PDF Spike  *(temel)*
**İş:**
- `git init` + `.gitignore` + ilk commit. (GitHub reposu kullanıcı açınca `origin` bağlanır.)
- Solution + projeler + test projeleri; `Directory.Packages.props`; `.editorconfig`; Serilog.
- `docker-compose.yml`: SQL Server + PostgreSQL. (Oracle Faz 3.)
- **ReportDefinition C# modeli** (Bölüm 5) + JSON Schema + FluentValidation kuralları.
- EF Core `DbContext` + `Report`, `Connection` entity'leri + SqlServer & PostgreSql migrations projeleri; runtime provider seçimi. (Oracle migrations projesi Faz 3.)
- `Reports` CRUD API + `Storage` repository; `GET /api/meta/schema`.
- API `wwwroot`'tan SPA servis + fallback route (tek container hedefi).
- React app (Vite) iskeleti: rota, boş kanvas, `/api/reports` listesini gösteren ekran, "yeni/kaydet" round-trip.
- **PDF motoru spike**: `IPdfRenderer` arayüzü + aynı örnek raporu PdfSharp/MigraDoc ve QuestPDF (gerekirse PuppeteerSharp) ile render → fidelity/perf/dağıtım kıyası → **motor kararı** `docs/04-pdf-motoru-karari.md`'ye yazılır.
- CI: build + test + 2 provider migration derleme.

**Doğrulama:** Web'den yeni rapor oluştur → kaydet → sayfa yenile → listeden aç → JSON aynı geliyor. Aynı senaryo SQL Server ve PostgreSQL için geçiyor. PDF motoru seçilmiş ve "hello world" PDF üretiyor. `dotnet test` yeşil.

---

### Faz 1 — Serbest Yerleşim + Statik JSON + PDF  *(ilk uçtan uca rapor)*
**İş:**
- **Designer (free mode):** Label / Field / Image / Line / Rectangle elemanlarını kanvasa sürükle; taşı/boyutlandır; çoklu seçim; özellik paneli (font, renk, hizalama, kenarlık, bounds); sayfa kurulumu (boyut/yön/kenar boşluğu); cetvel + grid + snap.
- **Veri:** Statik JSON kaynağı; yapıştır → alan ağacı (isim + tip). Alan ağacından kanvasa sürükleyince `field` elemanı bağlanır.
- **Binding:** `{ds.field}` çözümü + .NET format string'leri.
- **Rendering:** QuestPDF free-layout emitter; `POST /api/reports/{id}/render?format=pdf`.
- **HTML önizleme:** `preview` ucu + designer'da "Önizleme" sekmesi.
- Kanvas PoC kararı: react-konva vs dnd-kit+overlay (faz başında 1 gün).

**Doğrulama:** Yapıştırılan bir sipariş JSON'ından tek sayfalık, alanları bağlı bir fatura/kart tasarla → üretilen PDF designer'daki yerleşimle eşleşiyor (±2px tolerans, golden-file testi). Format string'leri (para, tarih) doğru.

---

### Faz 2 — Bantlı Raporlar
**İş:**
- `layoutMode` anahtarı; **bant editörü** (bant ekle/sil/sırala/yükseklik; tip seçimi).
- **Detail band** iterasyonu; **tek seviye gruplama** (groupHeader/groupFooter, grup ifadesi + sıralama).
- **Aggregate'ler:** sum/count/avg/min/max — grup / rapor / sayfa kapsamı.
- PageHeader / PageFooter; `pageInfo` elemanı ("Sayfa N / M", tarih).
- **Table** elemanı (kolonlar, başlık satırı).
- **Expression** değerlendirici (Bölüm 5) — `if`, aritmetik, `format()`, `pageNumber()`.
- **Rendering:** banded emitter + çok sayfalı sayfalama; grup footer'ın sayfa sonu davranışı.

**Doğrulama:** Statik JSON'dan müşteriye göre gruplanmış, grup ara toplamlı, rapor genel toplamlı, çok sayfalı bir liste raporu üret → sayfa sayısı, grup kırılımları ve toplamlar doğru (golden-file + sayfalama birim testleri).

---

### Faz 3 — REST + SQL Veri Kaynakları
**İş:**
- **Kayıtlı bağlantılar** CRUD + Data Protection şifreleme + "test et".
- **REST bağlacı:** URL/headers/query/resultPath; örnek yanıttan şema keşfi; parametre enjeksiyonu; SSRF korumaları.
- **SQL bağlacı:** SQL Server / Oracle / PostgreSQL; parametreli sorgu; şema keşfi; `maxRows` + timeout; SELECT-only heuristik.
- **Oracle devreye alma:** `Oracle.EntityFrameworkCore` sürüm/uyum doğrulaması + `Storage.Migrations.Oracle` projesi + storage entity'lerinin Oracle'da test edilmesi (JSON kolon tipi = CLOB). Storage tarafında da Oracle artık desteklenir.
- Designer'da veri kaynağı sihirbazı (kind seç → yapılandır → önizle → şema al).
- Parametre paneli: render öncesi kullanıcıdan değer toplama.

**Doğrulama:** Bir rapor canlı bir REST ucuna, başka bir rapor bir SQL sorgusuna bağlı; ikisi de runtime parametreleriyle (tarih aralığı) doğru render ediliyor. Şifreli bağlantı string'i DB'de düz metin değil. SSRF testleri (iç IP reddi) geçiyor.

---

### Faz 4 — Sağlamlaştırma ve Cila
**İş:**
- **Storage:** opsiyonel dosya sistemi provider'ı; klasör/etiket ile organizasyon; **rapor versiyonlama** (her kaydette snapshot).
- **Designer UX:** undo/redo, hizalama kılavuzları, kopyala/yapıştır, klavye kısayolları, zoom, z-order.
- **Doğrulama & hata yüzeyleri:** geçersiz binding / eksik alan / kırık bağlantı için net uyarılar.
- Örnek rapor galerisi + seed veri; kullanıcı dokümantasyonu (`docs/`).
- Render ucunda temel yük testi; PDF üretimi için eşzamanlılık/kaynak limitleri.

**Doğrulama:** 3-4 uçtan uca demo senaryosu (fatura, gruplanmış liste, REST dashboard özeti) sorunsuz. Render ucu makul eşzamanlı yükte stabil. `CHANGELOG.md` V1.0 girdisi hazır.

---

### V1.1+ Backlog
Sıra ile: Excel/XLSX (ClosedXML) ✅, alt raporlar ✅, chart ✅, barkod/QR ✅, matris/pivot ✅,
çok seviyeli gruplama ✅, auto-height/can-grow/push-up bantlar ✅, çok kolonlu düzen ✅, gelişmiş
expression fonksiyon kütüphanesi ✅, **auth (JWT + Identity, rol: Designer/Viewer)** ✅,
**multi-tenant (organizasyon oluşturma + davet kodu ile katılma + takım yönetimi)** ✅,
**e-posta hesabı tanımlama (Exchange/Gmail/özel SMTP + test gönderimi, MailKit)** ✅ — kalanlar:
i18n (TR/EN UI), **rapor zamanlama/dağıtım** ✅, **URL/routing tabanlı sayfa geçişleri**
(planlandı — bkz. aşağıdaki detay), şablon galerisi
(kullanıcının kendi raporunu organizasyon şablonu olarak kaydetmesi), canlı işbirliği, **ardından**
(bkz. aşağıdaki detay) AI destekli rapor asistanı ve modern görselleştirme (3D/gauge/heatmap/
sparkline/harita). Önce bu listedeki mevcut kalan maddeler bitirilecek, AI/görselleştirme işi ondan
sonra ele alınacak — iki liste tek backlog'ta birleştirildi.

**Rapor zamanlama/dağıtım — plan:**
- ✅ E-posta hesabı tanımlama ekranı (Exchange/Gmail/özel sunucu presetleri, host/port/security/
  kullanıcı-şifre/gönderen adresi, "test e-postası gönder" — şifre `IConnectionSecretProtector` ile
  şifrelenmiş saklanıyor, tenant başına tek hesap).
- ✅ Arka plan iş kuyruğu (`ReportJob`: kuyruğa alınan bir render işini arka planda işler, sonucu
  kendi satırında DB-blob olarak saklar, kullanıcı uygulama içinden "İşler" ekranından — 3 saniyede
  bir otomatik yenilenen — durumu takip eder, bitince indirir). Broker yok — tek process kendi
  DB'sine bakıyor, `IHostedService` polling (Hangfire'ın da altyapısı). Bu arada tenant-scope'lu
  repository'lerin (asset/subreport/SQL bağlantı) arka plan işçisinde de doğru çalışması için
  `ICurrentTenant`'a `AsyncLocal` tabanlı bir "ambient tenant" yolu eklendi (`CurrentTenant.Use`) —
  HTTP isteği olmayan bir bağlamda da JWT claim'i varmış gibi tenant çözülebiliyor, böylece
  render pipeline'ının geri kalanı (görsel/alt rapor/SQL veri kaynağı çözümleme) hiç değişmeden
  çalışıyor. Görsel içeren bir raporla uçtan uca doğrulandı (arka planda render edilen PDF'te
  resim doğru göründü — bu tam da ambient-tenant düzeltmesinin sınadığı senaryo).
- ✅ `ReportSchedule` (günlük/haftalık/aylık + UTC saat — basit form, cron değil; salt fonksiyon
  `ScheduleRecurrence.NextRun` olarak yazıldı, ay-sonu kırpma dahil 12 birim testle doğrulandı).
  30 saniyede bir tetiklenen ayrı bir `ReportScheduleTrigger`, süresi gelen her programı job
  kuyruğuna atıyor (`ReportJob.ScheduleId` ile işaretli); "sonraki çalışma" her zaman **o anki
  "şimdi"den** yeniden hesaplanıyor (programın kaçırdığı zamandan değil) — sunucu bir süre kapalı
  kalırsa yığılıp birikmiş eski çalıştırmaları art arda ateşlemiyor. İş başarıyla bitince
  `ReportJobProcessor` dağıtımı yapıyor: iste­nirse yeni bir paylaşım linki (mevcut Share
  altyapısı), istenirse yapılandırılmış e-posta hesabından ek dosyalı e-posta (MailKit) —
  dağıtım hatası (ör. yanlış SMTP şifresi) işin kendi "Succeeded" durumunu asla bozmuyor, sadece
  loglanıyor. "Start ekranı → Schedules" sekmesinden yönetiliyor (aç/kapat, sil); oluşturma rapor
  sağ-tık menüsünden ("Schedule…").
  Gerçek Exchange Online'a karşı uçtan uca doğrulandı: program ateşlendi → iş render edildi →
  paylaşım linki otomatik oluştu (API'den doğrulandı) → e-posta denemesi ger­çek sunucuya ulaşıp
  "535 Authentication unsuccessful" ile temiz biçimde başarısız oldu (kasıtlı yanlış şifreyle) —
  hem başarı hem hata yollarının gerçekten çalıştığının kanıtı.
- ⬜ **Backlog'a eklendi (ayrı iş turu, şimdi değil):** harici depolama hedefleri — MinIO, S3,
  Google Drive, OneDrive — iş çıktısının yerel/DB-blob dışında bu hedeflere de yazılabilmesi.

**URL/routing tabanlı sayfa geçişleri — plan (ayrı iş turu, şimdi değil):**
Arka plan iş bildirimlerini (yukarıdaki madde) test ederken gerçek bir bug'a rastlandı: Start
ekranı, designer'ın üzerine `position: fixed` bir overlay (`.start-overlay`, z-index 90) olarak
biniyor — ayrı bir sayfa değil. Bildirim toast'ı (z-index 60) bu overlay'in arkasında sessizce
render oluyordu, görünmüyordu (z-index 100'e çıkarılarak geçici olarak düzeltildi). Bu, mevcut
"tek sayfa + overlay" mimarisinin yapısal bir zaafı: her ekranın kendi URL'i olsaydı bu bug sınıfı
hiç oluşmazdı. Ayrıca kullanıcı isteği: yeni rapor oluşturma/var olan raporu açma ayrı sekmede
yapılabilsin.

Karar: her ekran kendi route'una sahip olacak (`react-router-dom` eklenecek — şu an proje bunu
kullanmıyor, `package.json`'da yok).

*Route haritası:*
| URL | Ekran |
|---|---|
| `/login` | Giriş (mevcut `LoginScreen`) |
| `/reports` | Start ekranı — Reports sekmesi (varsayılan) |
| `/reports/:id/design` | Designer (Canvas) |
| `/reports/:id/preview` | Preview |
| `/jobs` | Jobs sekmesi |
| `/schedules` | Schedules sekmesi (Designer-only) |
| `/email-settings` | Email sekmesi (Designer-only) |
| `/team` | Team sekmesi (Designer-only) |
| `/settings` | Ayarlar — şu an modal (`SettingsDialog`), kendi route'u olacak (karar verildi) |

Rapor oluşturma (Blank/Sample) bir `POST` gerektirdiği için doğrudan link olamaz — buton API
çağrısından sonra dönen id ile `navigate('/reports/:id/design')` yapar. "Yeni sekmede aç" sadece
**var olan** raporlar için gerçek `<a href>` ile mümkün olur (Ctrl+tık/orta tık).

*Aşamalar:*
1. `react-router-dom` ekle, `App.tsx`'i `BrowserRouter` + `<Routes>` ile sarmalayıp mevcut
   ekranları route bileşenlerine taşı. Auth guard: token yoksa her route `/login`'e yönlendirir
   (auth zaten `zustand/persist` ile localStorage'da — yeni sekme otomatik login'li açılır).
2. StartScreen'in iç sekmeleri (Reports/Jobs/Team/Email/Schedules) local `useState` yerine URL
   segmenti olur.
3. Designer/Preview tab'ı da route'a taşınır (`design`/`preview`); `showStart` overlay state'i
   tamamen kalkar — bug'ın kaynağı olan overlay yapısı ortadan kalkmış olur.
4. Deep-link yükleme: `/reports/:id/design` direkt açıldığında (yeni sekme/refresh)
   `api.getReport(id)` ile rapor çekilip store'a yüklenir; 404/yanlış tenant durumunda
   `/reports`'a dönüş linkli, anlaşılır bir hata ekranı gösterilir.

*Bilinçli kapsam dışı (v1 için):*
- Parametre değerleri (`paramValues`) URL'e yansımayacak, hâlâ bellekte kalacak.
- Aynı raporun iki sekmede açılması: her sekme kendi auto-save/job-notification poller'ını
  bağımsız çalıştırır — aynı job bitince birden fazla sekmede ayrı toast görülebilir. Şimdilik
  çözülmüyor (ileride `BroadcastChannel` ile dedup edilebilir), rahatsız ederse ayrı iş olarak ele
  alınır.

---

### AI Destekli Raporlama ve Modern Görselleştirme (backlog'un devamı — mevcut sıradaki işler bitince)
Pazar araştırması (2026): pixel-perfect/paginated raporlama (DevExpress, Telerik, Stimulsoft,
FastReport) hâlâ kurumsal zorunluluk — fatura, mali tablo, denetim belgesi gibi çıktılar dashboard
ile değiştirilemiyor. Ama beklenti kayıyor: embedded analytics artık varsayılan, self-servis talep
artıyor, ve 2026'da enterprise analitik ekiplerinin çoğunluğu artık conversational AI kullanıyor.
Karar: **hem kurumsal pixel-perfect temeli koru, hem de AI/self-servis katmanını üstüne ekle** —
ikisi birbirini dışlamıyor, JSON şema + FluentValidation temelimiz AI entegrasyonu için aslında
ideal bir zemin (LLM yapılandırılmış JSON üretir, validator "gerçeklik kontrolü" yapar).

**AI Rapor Asistanı — akış:**
1. Kullanıcı tasarım ekranını hiç açmadan bir chat/istek kutusuna doğal dille ne istediğini yazar
   ("son 3 ayın müşteri bazlı satış raporu, ülkelere göre gruplu, toplamlarla").
2. Asistan mevcut veri kaynaklarını (alan adları/tipleri zaten şemada var) bağlam olarak kullanır.
3. Kullanıcı bir rapor tasarımını ayrıntılı tarif edemez (en fazla %60-70) — asistan **proaktif**
   davranır: eksik/belirsiz noktalar için kısa, hızlı yanıtlanabilir netleştirme soruları sorar
   (veri kaynağı seçimi, tarih aralığı sabit mi parametre mi, öncelikli çıktı formatı, gruplama
   kırılımı vb.) — serbest metin yerine mümkün olduğunca çoktan seçmeli/hızlı form.
4. Asistan mevcut `ReportDefinition` şemasına uygun bir taslak üretir; **FluentValidation'dan
   geçmeyen taslağı kendi kendine düzeltmeyi dener** (retry-fix loop), geçemezse kullanıcıya
   net biçimde neyi tam yapamadığını söyler.
5. Taslak doğrudan **mevcut Designer ekranında** açılır — AI, designer'ın yerine geçmiyor, "sıfırdan
   başlamak yerine akıllı bir başlangıç noktası" oluyor. Kullanıcı isterse hiç dokunmadan
   Preview/Export yapar, isterse ince ayar için tasarım ekranını kullanır.
6. Aynı asistan var olan bir raporu da doğal dille değiştirebilir ("bir grup toplamı daha ekle",
   "başlığı büyüt") — formül editöründeki doğal-dil-ile-ifade-önerisi de (örn. "toplamı TL
   formatında göster" → `=format(sum(...), 'C')`) bu akışın bir parçası.

**Modern görselleştirme (yöneticiler/son kullanıcılar için görsel çekicilik):**
- 3D pasta/bar/column grafikler, gauge/KPI göstergesi, heatmap, sparkline (tablo hücresi içi mini
  trend), basit coğrafi/harita görselleştirme.
- **Teknik not:** render motoru PdfSharp/MigraDoc (2D, server-side .NET çizim) olduğu için "gerçek"
  3D (WebGL tarzı) PDF'e taşınamaz — gölge/perspektif ile **pseudo-3D** (Excel'in 3D grafikleri
  gibi) PDF'te uygulanabilir; tam interaktif/gerçek 3D ise HTML önizlemede (WebGL/Canvas ile)
  mümkün olur ama PDF export'ta bu sadeleşir. Beklenti bu ayrım net kurularak yönetilmeli.

**Doğrulama (gelecekte):** Kullanıcı hiç tasarım ekranını açmadan tek bir doğal dil isteğiyle
kullanılabilir bir rapor taslağı üretebilmeli; taslak validator'dan geçmeli; kullanıcı istekle
üretilen raporu ince ayar yapmadan da export edebilmeli.

---

## 10. Multi-Agent Geliştirme Yöntemi

### İş bölümü (fazlar içinde paralel)
| Ajan | Alan | Projeler |
|------|------|----------|
| **A — Backend/Storage** | Rapor modeli, EF Core çoklu-provider, repository, Reports/Connections API | `.Core`, `.Storage`, `.Storage.Migrations.*`, `.Api` (controller'lar) |
| **B — Rendering** | Render ağacı, sayfalama, QuestPDF & HTML emitter, aggregate, expression | `.Rendering`, `.Core` (expression) |
| **C — DataSources** | REST/JSON/SQL bağlaçları, şema keşfi, bağlantı yönetimi, güvenlik | `.DataSources` |
| **D — Frontend** | React designer: kanvas, DnD, özellik paneli, veri/parametre UI, önizleme | `web/` |

### Koordinasyon kuralları
1. **Faz 0 paylaşılan sözleşmeleri dondurur:** `ReportDefinition` şeması (`docs/02`) + OpenAPI (`docs/03`). Tüm ajanlar bunlara karşı kod yazar. Sözleşme değişikliği = önce doküman PR'ı + entegratör onayı.
2. Her ajan görevi **kendi kendine yeten** olmalı (subagent'lar soğuk başlar): görev metni ilgili şema/kontrat bölümlerini ve dosya yollarını içerir.
3. Ajanlar **faz bazında** çalışır; faz sonunda entegrasyon + review tek elden (entegratör = ben).
4. Testler görevle birlikte gelir; "Doğrulama" kriterleri PR kabul şartı.
5. Çakışma alanları (`.Api` controller'ları, `.Core` modeli) A'nın sahipliğinde; B/C/D PR açar, A merge eder.

### Bu araçta pratik uygulama
- Paralelleştirilebilir, iyi sınırlı parçalar için `Agent` (subagent) spawn edilir — örn. "Oracle migrations projesini kur", "özellik paneli komponentini yaz", "REST SSRF korumalarını + testlerini ekle".
- Mimari kararlar, şema değişiklikleri ve entegrasyon merkezi kalır (spawn edilmez).
- Kullanıcı açıkça istemedikçe subagent açılmaz; her faz başında hangi görevlerin paralelleştirileceği birlikte kararlaştırılır.

---

## 11. Riskler ve Önlemler

| # | Risk | Etki | Önlem |
|---|------|------|-------|
| 1 | Bantlı sayfalama fidelity'si (taşma, grup footer, sayfa kırılması) | Yüksek | QuestPDF'in native paging'ine yaslan; V1'de auto-height/push-up yok; golden-file + birim testleri |
| 2 | **PDF motoru seçimi** (lisans / fidelity / dağıtım maliyeti dengesi) | Orta | Faz 0 spike'ında 2-3 aday kıyaslanır; `IPdfRenderer` soyutlaması ile motor değiştirilebilir kalır; karar dokümante edilir |
| 3 | **Oracle EF Core 10 provider** olgunluğu / sürüm gecikmesi | Orta | Faz 3'e ertelendi; Faz 0-2 SQL Server + PostgreSQL ile ilerler; Faz 3 başında sürüm uyumu doğrulanır |
| 4 | Çoklu-provider migration bakım yükü (3×) | Orta | Tek DbContext; CI'da 3 provider derleme; entity tasarımında provider'a özgü tipten kaçın (JSON kolonu `nvarchar(max)`/`text`/`CLOB` soyutla) |
| 5 | REST SSRF / SQL injection yüzeyi | Yüksek (güvenlik) | Faz 3: host allow-list, iç IP reddi, parametreli sorgu zorunlu, SELECT-only, timeout + row cap; bağlantı string'i şifreli |
| 6 | Designer kapsam kayması (DevExpress'e benzetme cazibesi) | Yüksek | Katı faz kapıları; V1 sınırları (Bölüm 5) yazılı; her yeni istek backlog'a |
| 7 | Auth'un V1'de olmaması | Orta | Yalnız güvenli iç ağ; `/api` için IP kısıtı/reverse-proxy notu; V1.1'de JWT+Identity ilk madde |
| 8 | React kanvas kütüphanesi seçimi yanlış çıkarsa | Orta | Faz 1 başında 1 günlük PoC ile karar; soyutlama katmanı ("CanvasRenderer" arayüzü) ile değiştirilebilir tut |
| 9 | JSON tabanlı rapor tanımının DB'de sorgulanabilirliği | Düşük | Meta alanlar (name, layoutMode, updatedAt) ayrı kolonlarda; tam tanım JSON kolonunda |

---

## 12. Açık Sorular — Durum

| # | Soru | Karar |
|---|------|-------|
| 1 | QuestPDF lisansı | ✅ Lisans istenmiyor → Faz 0 spike'ında motor seçilir (`IPdfRenderer`) |
| 2 | Oracle V1'de zorunlu mu | ✅ Hayır → mimari hazır, **Faz 3'te** devreye alınır |
| 3 | Dağıtım | ✅ **Monorepo + tek container** (API, React build'ini `wwwroot`'tan servis eder) |
| 4 | `web/` konumu | ✅ Bu repo içinde (monorepo) |
| 5 | Git / uzak repo | ✅ Faz 0'da `git init`; **GitHub** uzak repo (kullanıcı açacak, `gh` CLI yok) |
| 6 | .NET 10 SDK | ✅ Kurulu (10.0.400) · Node v23.11.1 · git 2.55 |
| 7 | Designer ölçü birimi | ⏳ **Varsayılan: mm** (iç birim 1/96 inch sabit; UI'da mm/cm/inch değiştirilebilir). Aksini istersen belirt. |

---

## 13. Onay Sonrası İlk Adım

Onaylarsan **Faz 0**'a başlarım:
1. `git init` + `.gitignore` + ilk commit.
2. `docs/02-report-definition-schema.md` ve `docs/03-api-contract.md` yazılır (sözleşmeler donar).
3. Solution + projeler + Docker Compose (SQL Server + PostgreSQL) + CI iskeleti.
4. `ReportDefinition` modeli + EF Core + SqlServer/PostgreSql provider + Reports CRUD + boş React app.
5. **PDF motoru spike** → karar `docs/04-pdf-motoru-karari.md`.
6. Faz 0 "Doğrulama" kriterleri gösterilir; GitHub reposu hazırsa `origin` bağlanır (push kullanıcı onayıyla).

**Bu doküman onaylanmadan kod yazılmaz.**
