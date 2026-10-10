# Rapor Önizleyici (WinUI 3)

UAT / Prod gibi birden fazla JetReportDesigner sistemine adres + API anahtarı ile bağlanır, raporlara veri verip render eder
ve önizler. `JetReportDesigner.Client` paketini (proje referansı) uçtan uca kullanır.

- **Sistemler:** üstteki "Sistemleri yönet…" ile ad, adres ve API anahtarı eklenir/düzenlenir/silinir, "Bağlantıyı dene" vardır.
  Liste `%LOCALAPPDATA%\RaporOnizleyici\systems.json` içinde durur; API anahtarları Windows DPAPI ile (yalnızca bu Windows
  kullanıcısı çözebilir) şifreli saklanır.
- **Dinamik alanlar:** bir rapor seçilince `client.GetSchemaAsync(kod)` raporun parametrelerini ve veri kaynaklarını (tanımlı
  alanlar + tasarımda `{kaynak.alan}` olarak kullanılanlar + örnek verinin anahtarları) getirir; form buna göre oluşur.
  Parametre türüne göre kontrol seçilir (izinli değerler → liste, sayı, tarih, evet/hayır). Her veri kaynağı için tablo veya
  tek kayıtsa form, istenirse "JSON olarak düzenle". `foto/photo/image/resim/logo/imza` adlı alanlar için dosya seçici
  (data URI olarak gönderilir). Kaynak için "kendi verimi gönder" işaretli değilse rapor sunucudaki veriyle çalışır.
- **Önizleme** PNG sayfaları (96–300 dpi, yakınlaştırma); **Kaydet** PDF / Excel / HTML ve gönderilen isteğin JSON'u.

```
dotnet run -c Debug -p:Platform=x64      # clients/RaporOnizleyici içinde
```
Gereken: Windows 10 1809+, .NET 8 SDK (Windows App SDK kendi içinde paketli). Sunucu çözümüne dahil değildir (CI'ı etkilemesin).
