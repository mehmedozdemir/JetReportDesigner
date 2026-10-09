# Masaüstü / dış uygulama entegrasyonu

Bir uygulamanın (WinForms, WPF, servis…) bu platformdaki bir raporu **kendi verisiyle** çalıştırıp
PDF / PNG / JPEG / HTML / XLSX olarak alması veya doğrudan yazıcıya göndermesi için yol.

```
Uygulama ──(JetReportDesigner.Client paketi)──► JetReportDesigner sunucusu
   veri (JSON)  +  X-Api-Key                      raporu bulur, veriyi yerine koyar, çizer
   ◄─────────── PDF / PNG / JPEG / HTML / XLSX ───┘
```

Rapor tasarımı sunucuda kalır; uygulama sadece **rapor kodunu** ve **veriyi** verir.

## 1. Sunucu tarafı: API anahtarı verme

Anahtarlar arayüzden yönetilir: sol menüde **Organizasyon → API anahtarları** (yalnızca Designer rolü görür).

- **Yeni anahtar:** ad (hangi uygulama için olduğu), isteğe bağlı not ve geçerlilik seçilir:
  *Süresiz*, 30 gün, 90 gün, 1 yıl ya da belirli bir tarih (o günün sonuna kadar çalışır).
- **Anahtar bir kez gösterilir.** Oluşturma sonrası açılan pencerede kopyalayın; sunucuda yalnızca SHA-256 özeti
  ve tanıma için ilk karakterleri (`jrd_a1b2c3d4…`) saklanır, sonradan görülemez. Kaybolursa yenisi oluşturulur.
- **Pasife al / Etkinleştir:** anahtarı silmeden geçici olarak durdurur; uygulama hemen `401` almaya başlar.
- **Düzenle:** ad, not ve bitiş tarihi değişir (anahtarın kendisi değişmez).
- **Sil:** kalıcıdır; o anahtarı kullanan uygulama çalışmaz hale gelir.
- Listede her anahtarın durumu (Etkin / Pasif / Süresi dolmuş), bitiş tarihi, **son kullanım zamanı** ve kimin
  oluşturduğu görünür. Hiç kullanılmayan veya uzun süredir kullanılmayan anahtarları buradan fark edip silebilirsiniz.

Bir anahtar tek bir organizasyona bağlıdır ve sadece **Viewer** yetkisi taşır: o organizasyonun raporlarını
listeleyebilir ve çalıştırabilir; rapor oluşturamaz/değiştiremez/silemez, anahtar yönetemez.
Her uygulamaya ayrı anahtar verin: biri sızarsa yalnızca onu kapatırsınız.

Ağ: sunucu yalnızca kurum ağında / VPN'de erişilebilir olmalı; internete açmayın. Mümkünse önüne HTTPS (ters vekil)
koyun, çünkü anahtar her istekte `X-Api-Key` başlığında gider.

## 2. Rapor kodu: dışarıdan raporu çağırmanın adı

Her raporun, organizasyon içinde **benzersiz** bir **kodu** vardır: küçük harf, rakam, `-` ve `_`; boşluk ve Türkçe
karakter yok (`barkod-rapor-claude`). Uygulamalar raporu bu kodla çağırır.

- Rapor ilk kaydedilirken adından otomatik üretilir: *Aylık Özet Raporu* → `aylik-ozet-raporu` (çakışırsa `-2`, `-3`…).
- Tasarım ekranında sağdaki **Sayfa** panelinin en üstündeki **Rapor kodu** alanından değiştirilebilir ve kopyalanabilir.
  Raporlar listesinde (liste görünümü) adın yanında da görünür.
- **Rapor adı değişse de kod aynı kalır**; kodu siz değiştirmedikçe uygulamalar etkilenmez. Kodu değiştirirseniz
  onu kullanan uygulamaları da güncellemeniz gerekir.
- Aynı kodu iki rapor taşıyamaz (`409`), geçersiz karakter `422` verir.
- Var olan raporlara sunucu ilk açılışta adlarından kod atar.

## 3. Uygulama tarafı: `JetReportDesigner.Client` paketi

Paket: `artifacts/nuget/JetReportDesigner.Client.1.0.0.nupkg` (`dotnet pack src/JetReportDesigner.Client -c Release -o artifacts/nuget`).
Kurum içi bir NuGet kaynağına koyun ya da Visual Studio'da "yerel kaynak" olarak o klasörü ekleyin.

Desteklenen hedefler: `netstandard2.0` (her .NET Framework 4.6.1+ ve .NET), ayrıca `net48` ve `net8.0-windows`
(yazıcıya doğrudan yazdırma yardımcısı bu ikisinde).

```csharp
using JetReportDesigner.Client;

// Uygulama açılırken bir kez oluşturun, kapanana kadar kullanın.
var reports = new JetReportClient(new JetReportClientOptions
{
    BaseUrl = "http://raporlar.firma.local:8081",
    ApiKey  = "jrd_..."
});

// Veri: herhangi bir nesne listesi, DataTable ya da hazır JSON dizisi.
// "data" = raporda tanımlı veri kaynağının adı. Alan adları aynen kullanılır: {data.Adi}
var veri = new ReportData().Add("data", new[]
{
    new { Adi = "Ahmet", Soyadi = "Yılmaz", TeslimAdresi = "...", TeslimIl = "İSTANBUL",
          TeslimIlce = "Kadıköy", Barkod = "2750365698456" },
});
```

### PDF / HTML / Excel al

```csharp
var pdf = await reports.RenderAsync("barkod-rapor-claude", veri, format: ReportFormat.Pdf);
pdf.Save(@"C:\Temp");                 // dosya adı sunucudan gelir: "Barkod Rapor - Claude.pdf"
// pdf.Content (byte[]), pdf.ContentType, pdf.FileName
```

`ReportFormat.Html` uygulama içinde WebView2'de göstermek için, `Xlsx` Excel için.

### PNG / JPEG (ekranda göstermek, resim olarak saklamak)

```csharp
var sayfalar = await reports.RenderPagesAsync("barkod-rapor-claude", veri,
                                              format: ReportFormat.Png, dpi: 200);
pictureBox1.Image = Image.FromStream(new MemoryStream(sayfalar[0].Content));
```

Tek sayfa için `RenderAsync(..., format: ReportFormat.Png, page: 2, dpi: 300)`; `PageCount` toplam sayfa sayısıdır.
Ekran için 150 dpi, yazdırma için 300 dpi yeterlidir.

### Yazdır

```csharp
await reports.PrintAsync("barkod-rapor-claude", veri,
                         printerName: "Zebra ZD420",   // null = varsayılan yazıcı
                         copies: 1);
```

Kâğıt boyutu raporun kendi sayfa boyutundan alınır (ör. 103,9 × 53,1 mm etiket), kenar boşluğu sıfırdır.
Yazıcı listesi: `ReportPrinter.InstalledPrinters()`. Not: `PrintAsync` yalnızca `net48` / `net8.0-windows`
hedeflerinde vardır; `netstandard2.0` projesinde PNG alıp kendiniz yazdırın.

### DataTable ile

```csharp
var veri = new ReportData().Add("data", dataTable);   // DBNull -> null; sütun adları alan adı olur
```

### Parametre

```csharp
await reports.RenderAsync("aylik-ozet", veri,
    parameters: new Dictionary<string, object> { ["donem"] = "2026-10" });
```

### Hatalar

Her başarısızlık `ReportClientException` atar; `Message` nedeni, `StatusCode` HTTP kodunu söyler:

| Durum | Anlamı |
|---|---|
| `Unauthorized` | API anahtarı yanlış / sunucuda tanımlı değil |
| `NotFound` | Bu kodda rapor yok (büyük/küçük harf fark etmez) |
| `Conflict` | Kod yerine görünen ad verilmiş ve aynı adda birden fazla rapor var — rapor kodunu kullanın |
| `BadRequest` | Veri kaynağı adı raporda yok, ya da sayfa numarası aralık dışı |

Rapor kodu yerine rapor id'si (GUID) de verilebilir. Kod bulunamazsa görünen ad da denenir, ama kod kalıcıdır — ad değişebilir.

## 4. Sunucudaki uç nokta (paketi kullanmadan çağırmak için)

```
POST /api/reports/by-code/{kod}/render?format=pdf|html|xlsx|png|jpeg&page=1&dpi=150
POST /api/reports/{id}/render?...            (aynısı, id ile)
X-Api-Key: <anahtar>
Content-Type: application/json

{ "parameters": { "donem": "2026-10" },
  "data": { "data": [ { "Adi": "Ahmet", "Barkod": "2750365698456" } ] } }
```

`data` içindeki her anahtar bir veri kaynağının adıdır; değeri nesne dizisidir. Bu çağrı için o kaynağın
satırlarının yerine geçer (kaynak JSON, REST ya da SQL olabilir); kayıtlı rapor değişmez.
PNG/JPEG yanıtında `X-Page-Count` başlığı toplam sayfa sayısını verir. `GET /api/reports` raporları (`id`, `code`, `name`) listeler.

## 5. Tasarımcının dikkat edeceği şeyler

- Raporun veri kaynağı **adı** uygulamanın gönderdiğiyle aynı olmalı (örnek raporda `data`).
  Tasarımdaki örnek JSON, önizleme içindir; uygulama veri gönderdiğinde onun yerine geçer.
- Raporun alan adları ile gönderilen nesnelerin özellik adları birebir aynı olmalı (büyük/küçük harf dahil).
- EAN-13 barkod değeri geçerli bir kontrol hanesi taşımalıdır; aksi halde kutuda `Invalid ean13 value` görünür.
  Her değer geçerli olmayacaksa Code 128 kullanın.

## 6. DevExpress raporlarını taşıma

`.repx` dosyaları bu formata otomatik çevrilmez; raporlar tasarımcıda yeniden çizilir (etiket, alan, barkod,
tablo gibi temel öğeler birebir karşılık bulur). Taşınan her rapor için uygulamadaki veri sınıfı/DataSet
alanları ile rapordaki alan adlarını eşleyin; geri kalanı yukarıdaki üç satırlık çağrıdır.
