# Raporları ortamlar arasında taşıma (UAT → Prod)

Rapor, klasör ya da bir klasörün tamamı (alt klasörleri ve içindeki raporlarla) bir **paket dosyası**
(`.jrdpkg`) olarak dışa aktarılır, diğer ortamda **önizlenir** ve içe aktarılır.

## Kullanım

**UAT'ta (dışa aktarma)** — Raporlar sayfası:
- Bir raporun sağ tık menüsü → **Paket olarak dışa aktar…**
- Bir klasörün sağ tık menüsü → **Klasörü paket olarak dışa aktar…** (alt klasörler dahil, özyinelemeli)
- Birkaç raporu işaretleyip üstteki çubuktan **Paket olarak dışa aktar**

Açılan pencere paketin içeriğini indirmeden önce gösterir: raporlar, klasörler, görseller, kullanılan veritabanı
bağlantı adları, pakete konmayan gizli değerler. Alt rapor olarak gömülü raporlar **otomatik eklenir**, böylece paket
tek başına çalışır.

**Prod'da (içe aktarma)** — Raporlar sayfası → **İçe aktar** → dosyayı seç/bırak:
1. **Kontrol:** paket doğrulanır, hedefteki durumla karşılaştırılır. Bu aşamada hiçbir şey değişmez.
2. **İnceleme:** her rapor için durum (*Yeni / Değişmiş / Aynı / Geçersiz*), neyin değiştiği (eklenen/silinen/değişen
   öğeler, sayfa, veri kaynağı…), hedef klasör, eksik bağlantılar ve uyarılar görünür. Her rapor için ne yapılacağı seçilir:
   **Ekle**, **Üzerine yaz (yeni sürüm)**, **Kopya olarak ekle**, **Atla**.
3. **Uygula:** hepsi tek işlemde yapılır; hata olursa hiçbiri uygulanmaz.

## Nasıl eşleşir

Raporlar **rapor koduyla** eşleşir (kimlikler ortamdan ortama farklıdır). Aynı kodlu rapor hedefte varsa "Değişmiş/Aynı",
yoksa "Yeni" görünür. Bu yüzden bir raporun kodunu iki ortamda aynı tutmak gerekir; kodlar da pakete girer.

| Taşınan | Nasıl |
|---|---|
| Rapor tanımı | Kodla eşleşir; yoksa eklenir, varsa **yeni sürüm** olarak güncellenir |
| Alt raporlar | Pakete otomatik girer; içeren raporların alt rapor referansı hedefteki yeni kimliğe çevrilir |
| Görseller | Pakete girer; hedefte aynı içerik (SHA-256) varsa yenisi oluşturulmaz |
| Klasörler | Ağaç yeniden kurulur (aynı adlı klasör varsa kullanılır); mevcut raporların yeri **değişmez** |
| Veritabanı bağlantıları | **Yalnızca ad ve tür.** Hedefte aynı adlı bağlantı varsa raporlara bağlanır; yoksa uyarı verilir |

**Taşınmayanlar** (ortama özeldir): bağlantı dizeleri/parolalar, zamanlamalar, paylaşım bağlantıları, iş geçmişi, API anahtarları, kullanıcılar.

## Güvenlik

- **Sır yok:** pakete parola/bağlantı dizesi girmez. REST kaynaklarındaki `Authorization`, API anahtarı, token, parola
  gibi başlık/sorgu değerleri ve URL içindeki kimlik bilgileri dışa aktarırken **silinir**; sayısı pencerede yazar ve
  hedefte yeniden girilmesi gerekir. JSON veri kaynaklarındaki örnek veriler gerçek veri içerebilir; isterseniz
  *örnek verileri pakete koyma* seçeneğiyle çıkarılır.
- **Bütünlük:** her dosyanın SHA-256 özeti pakette tutulur; bozulmuş ya da sonradan değiştirilmiş paket reddedilir.
  (Bu bir imza değildir: paketi yetkili bir kullanıcı içe aktarır, yetki giriş yapan kullanıcıdan gelir.)
- **Yetki:** dışa ve içe aktarma yalnızca **Designer** rolüne açıktır. API anahtarları (salt okunur) kullanamaz.
- **Güvenli okuma:** yalnızca beklenen dosya adları okunur (yol atlatma, fazladan dosya reddedilir); paket en fazla
  64 MB, açılmış hâli en fazla 256 MB; sıkıştırma bombasına karşı okuma sırasında üst sınır uygulanır; yeni sürüm
  biçimli paket "sunucuyu güncelleyin" uyarısıyla reddedilir.
- **Doğrulama:** her rapor, normal kayıttaki aynı kurallarla doğrulanır; geçersiz olan atlanır, diğerleri aktarılabilir.
- **Denetim:** her içe aktarma günlüğe (kim, hangi ortamdan, kaç rapor) yazılır ve her rapor sürümünde
  **İçe aktarıldı** etiketiyle görünür.

## Geri alma

Üzerine yazılan her rapor **yeni bir sürüm** olarak kaydedilir; eski hâli silinmez. Yanlış gittiyse raporun
**Sürüm geçmişi**'nden önceki sürüm tek tıkla geri yüklenir. Aynı paketi tekrar içe aktarmak güvenlidir:
değişmeyen raporlar "Aynı" görünür ve atlanır, yeni sürüm oluşmaz.

## Ortam adı

İsteğe bağlı: `Transfer:EnvironmentName` (ör. `Transfer__EnvironmentName=UAT`) ayarı, paketin kaynağını içe aktarma
ekranında gösterir.

## Sınırlar

- Rapor kodu hedefte başka bir raporda kullanılıyorsa ve "Kopya olarak ekle" seçilirse kod `…-copy`, `…-copy-2` olur.
- Dosya tabanlı rapor deposunda (`Storage:ReportStore=filesystem`) içe aktarma tek işlem (transaction) değildir.
- Yalnızca raporlar taşınır; CI/CD ile otomatik terfi (API anahtarıyla içe aktarma) bu sürümde yok.
