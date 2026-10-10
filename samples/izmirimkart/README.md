# İzmirim Kart — kart tasarımları

`build_cards.py` izmirimkart.com.tr'deki 15 kart tasarımını (İndirimli / Ücretsiz / Diğer) "İzmirim Kart" organizasyonundaki
**Kart Tasarımları** klasörüne rapor olarak oluşturur (kod: `izmirim-kart-<slug>`). Görseller `~/Downloads/IzmirimKart-Kart-Tasarimlari`
klasöründedir; kartın görseli sayfa arka planı olarak (`asset:`), basılı etiketler görselin parçası olarak kalır.

```
JRD_PASSWORD=... python build_cards.py            # hepsini oluştur / güncelle
JRD_PASSWORD=... python shot_cards.py             # örnek veri + örnek fotoğrafla out/*.png önizleme
```

## Dışarıdan gelen veri

Veri kaynağı adı `data`. Uygulama her çağrıda satırı gönderir (`POST /api/reports/by-code/{kod}/render`, bkz. `docs/entegrasyon.md`):

```json
{ "data": { "data": [ {
  "tcKimlikNo": "12345678901", "ad": "AYŞE", "soyad": "YILMAZ", "kartNo": "1000 2000 3000 4000",
  "verilisTarihi": "15.01.2026", "fotograf": "data:image/jpeg;base64,..."
} ] } }
```

`fotograf` vesikalık fotoğraftır: base64 data URI, http(s) URL veya `asset:{id}`. EHS (POLİS) kartında `verilisTarihi`/`kartNo`
yerine `sicilNo` vardır; Öğretmen, Genç, 60 Yaş, Personel, Resmi Kurum, TÜİK, Zabıta kartlarında veriliş tarihi basılı değildir.
"Özel ve Refakatçı" tek görsel olarak yayınlanmıştır; değerler öndeki (kırmızı) Özel karta yazılır.
