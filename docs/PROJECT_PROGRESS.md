# QueryPilot Proje İlerleme Kaydı

Son güncelleme: 21 Ağustos 2026

Bu dosya, tamamlanan geliştirme adımlarını, önemli teknik kararları ve doğrulama sonuçlarını kalıcı olarak izlemek için kullanılır. Parola, API anahtarı, gerçek müşteri verisi veya başka hassas bilgiler bu dosyaya yazılmaz.

## Tamamlanan adımlar

### Adım 11 - Ortak API altyapısı

- Validation hataları ve uygulama hataları ortak `ProblemDetails` biçiminde dönüyor.
- 400, 404, 409, 500 ve AI servisi için 503 durumları destekleniyor.
- Hata yanıtlarında takip amacıyla `traceId` bulunuyor.
- Global exception handling kullanılıyor; endpointlerde tekrar eden `try/catch` blokları yok.
- Ortak pagination modeli eklendi. Varsayılan sayfa boyutu 20, maksimum sayfa boyutu 100.
- Beklenmeyen hatalar loglanıyor; hassas request içeriği, parola, API anahtarı ve e-posta loglanmıyor.
- Production yanıtlarında stack trace gösterilmiyor.

İlgili commit: `02f990d feat: add shared API error and pagination infrastructure`

### Adım 12 - Category modülü

- Category create, update, list ve response DTO'ları oluşturuldu.
- Ad doğrulaması, duplicate kontrolü, pagination ve aktiflik filtresi eklendi.
- Create, update, list, detail ve pasifleştirme işlemleri service katmanında uygulanıyor.
- Hard delete yerine `IsActive` ile soft delete kullanılıyor.
- API entity yerine DTO döndürüyor.
- Kategori adları PostgreSQL `citext` ve unique index ile büyük/küçük harfe duyarsız benzersiz hale getirildi.
- Update isteğinde `IsActive` alanı zorunlu; alan gönderilmezse 400 validation yanıtı dönüyor.

İlgili commit: `f9c2475 feat: add category management API`

### Adım 13 - Product modülü

- Product create, update, list ve response DTO'ları oluşturuldu.
- Name, SKU, CategoryId, UnitPrice ve IsActive alanları doğrulanıyor.
- UnitPrice sıfırdan büyük olmak zorunda.
- SKU normalize ediliyor ve duplicate SKU engelleniyor.
- Var olmayan kategoriyle ürün oluşturulamıyor.
- Create, update, list, detail ve pasifleştirme işlemleri service katmanında uygulanıyor.
- Liste endpointinde pagination, aktiflik ve kategori filtreleri bulunuyor.
- Ürün adı veya SKU ile arama yapılabiliyor.
- Hard delete yerine `IsActive` ile soft delete kullanılıyor.
- Response içinde kategori bilgisi özet DTO olarak dönüyor.
- Ürün güncellemesi geçmiş `OrderItem.UnitPrice` değerlerini değiştirmiyor.
- Update isteğinde `IsActive` alanı zorunlu; alan gönderilmezse 400 validation yanıtı dönüyor.

### Adım 14 - Customer modülü

- Customer create, update, list ve response DTO'ları oluşturuldu.
- Name, Email, City ve IsActive alanları doğrulanıyor; boşluklardan oluşan ad ve şehir engelleniyor.
- E-posta formatı kontrol ediliyor.
- E-posta `trim + lowercase` politikasıyla normalize ediliyor ve `NormalizedEmail` unique indexiyle duplicate kayıt engelleniyor.
- Create, update, list, detail ve pasifleştirme işlemleri service katmanında uygulanıyor.
- Liste endpointinde pagination, ad/e-posta araması, şehir ve aktiflik filtreleri bulunuyor.
- Hard delete yerine `IsActive` ile soft delete kullanılıyor; sipariş geçmişi olan müşteri ve siparişleri korunuyor.
- Response DTO yalnızca gerekli müşteri alanlarını içeriyor; `NormalizedEmail` ve entity ilişkileri dönmüyor.
- Customer işlemleri e-posta, query string veya request body loglamıyor.
- Update isteğinde `IsActive` alanı zorunlu; alan gönderilmezse 400 validation yanıtı dönüyor.

### Adım 15 - Sipariş request ve response modelleri

- `CreateOrderRequest` yalnızca `CustomerId` ve sipariş satırlarını kabul ediyor.
- Her sipariş satırında yalnızca `ProductId` ve `Quantity` bulunuyor; fiyat ve toplam alanları istemciden alınmıyor.
- Request DTO'ları tanımsız JSON alanlarını reddediyor. Böylece `UnitPrice`, `LineTotal`, `TotalAmount` ve `OrderDate` istemciden gönderilemiyor.
- Frontend fiyat veya toplam belirleyemiyor; `UnitPrice` ürünün kayıtlı fiyatından, `LineTotal` ve `TotalAmount` ise backend tarafından hesaplanacak.
- En az bir sipariş satırı, pozitif `CustomerId`, `ProductId` ve `Quantity` validation ile zorunlu.
- Aynı `ProductId` değerinin bir request içinde birden fazla kez gelmesi 400 validation hatası olarak reddediliyor; satırlar sessizce birleştirilmiyor.
- Liste için hafif `OrderListResponse`, detay için müşteri ve ürün özetleriyle birlikte `OrderDetailResponse` oluşturuldu.
- API response'larında `Order`, `OrderItem`, `Customer` veya `Product` entity graphı doğrudan serialize edilmeyecek; yalnızca liste ve detay DTO'ları kullanılacak.
- `UnitPrice`, `LineTotal` ve `TotalAmount` response modellerinde sunucu tarafından hesaplanan finansal snapshot değerleri olarak yer alıyor.
- `OrderDate` request modelinde bulunmuyor; sipariş oluşturulurken backend tarafından `DateTime.UtcNow` ile atanacak.
- Data annotation ve model-level validation hataları mevcut ortak `ValidationProblemDetails` formatına bağlı.

## Veritabanı ve migration durumu

- Yerel geliştirme veritabanı PostgreSQL 18 üzerinde çalışıyor.
- PostgreSQL yalnızca `127.0.0.1:5432` üzerinden erişilebilir.
- Oturum açıldığında PostgreSQL'i başlatan `QueryPilotPostgreSQL18` zamanlanmış görevi bulunuyor.
- İlk şema migration'ı: `20260820131333_InitialCreate`
- Kategori adını büyük/küçük harfe duyarsız yapan migration: `20260821084529_MakeCategoryNameCaseInsensitive`
- Bağlantı bilgileri ve parolalar repository yerine .NET user-secrets içinde tutuluyor.

## Son doğrulamalar

- Solution build sonucu: 0 hata, 0 uyarı.
- Product create isteği 201 döndürdü.
- Duplicate SKU 409 döndürdü.
- Geçersiz CategoryId 404 döndürdü.
- Sıfır fiyat ve eksik zorunlu alanlar 400 döndürdü.
- Product update, pagination, isim/SKU araması, kategori/aktiflik filtreleri ve soft delete çalıştı.
- Test ürününün PostgreSQL'de pasif olarak tutulduğu doğrulandı.
- Büyük/küçük harfi değiştirilmiş duplicate kategori doğrudan PostgreSQL unique constraint tarafından engellendi.
- Category ve Product update isteklerinde eksik `IsActive` alanının 400 döndürdüğü Swagger üzerinden doğrulandı.
- Customer create ve update işlemleri çalıştı; e-posta lowercase olarak normalize edildi.
- Büyük/küçük harfi değiştirilmiş duplicate e-posta 409, geçersiz e-posta 400 döndürdü.
- Customer pagination sınırı, ad/e-posta araması, şehir ve aktiflik filtreleri doğrulandı.
- Boşluklardan oluşan müşteri adı ve şehir 400 döndürdü.
- Sipariş geçmişi olan müşteri DELETE sonrasında PostgreSQL'de pasif olarak ve siparişleriyle birlikte kaldı.
- Customer testlerinde uygulama loglarına e-posta, müşteri adı veya request body yazılmadığı doğrulandı.

## Sıradaki adım

Sipariş oluşturma, listeleme ve detay işlemlerini service ve controller katmanlarında uygulamak.

## Git geçmişi

- `02f990d` - Ortak API hata ve pagination altyapısı
- `f9c2475` - Category yönetim API'si
- `137f6ba` - Yerel PostgreSQL 18 kurulum dokümantasyonu
- `5653bd3` - Product modülü ve kategori/update sağlamlaştırmaları
