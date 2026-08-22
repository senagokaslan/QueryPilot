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

### Adım 16 - Sipariş oluşturma doğrulamaları ve fiyat snapshot'ı

- Sipariş oluşturulurken müşteri veritabanından bulunuyor; bulunamayan müşteri için 404 dünüyor.
- Pasif müşterinin yeni sipariş vermesi 409 ile engelleniyor.
- Request içindeki tüm benzersiz `ProductId` değerleri tek bir `WHERE IN` sorgusuyla getiriliyor; item sayısına bağlı N+1 ürün sorgusu oluşmuyor.
- Eksik ürünlerin ID değerleri 404 yanıtında, pasif ürünlerin ID değerleri 409 yanıtında bildiriliyor.
- Her satırın `UnitPrice` değeri veritabanındaki güncel ürün fiyatından kopyalanıyor; frontend fiyat belirleyemiyor.
- `LineTotal` ve `TotalAmount` backend tarafından fiyat snapshot'ı kullanılarak hesaplanıyor.
- Request, müşteri ve tüm ürün kontrolleri tamamlanmadan `Order` context'e eklenmiyor ve `SaveChanges` çağrılmıyor.
- Başarılı sipariş, satırlarıyla birlikte tek `SaveChanges` işlemiyle atomik olarak kaydediliyor.

### Adım 17 - Sipariş toplamları, transaction ve create endpointi

- Her `OrderItem.LineTotal`, `decimal` fiyat ile `Quantity * UnitPrice` olarak backend tarafından hesaplanıyor.
- `Order.TotalAmount`, aggregate içindeki tüm `LineTotal` değerlerinin toplamından üretiliyor.
- `Order` ve tüm `OrderItem` kayıtları tek aggregate olarak oluşturulup tek `SaveChangesAsync` çağrısıyla kaydediliyor.
- Müşteri ve ürün okumalarından kayıt sonrası toplam kontrolüne kadar bütün sipariş oluşturma akışı açık database transaction'ı içinde çalışıyor; hata durumunda rollback yapılıyor.
- Kayıt sonrasında database'deki `TotalAmount` ile database'deki item toplamı transaction commit edilmeden önce karşılaştırılıyor.
- `POST /api/orders` başarılı işlemde 201 Created, order detail DTO ve `GET /api/orders/{id}` adresini gösteren `Location` header'ı döndürüyor.
- `GET /api/orders/{id}` entity graph yerine doğrudan `OrderDetailResponse` projection'ı döndürüyor.
- Başarılı sipariş olayı yalnızca `OrderId` ve item sayısıyla loglanıyor; request, müşteri, ürün veya fiyat içeriği loglanmıyor.
- Yerel runtime testinde POST 201 ve Location doğrulandı; kaydedilen toplam item toplamıyla eşleşti.
- Runtime testinde ürün fiyatı sonradan değiştirildiğinde eski `OrderItem.UnitPrice`, `LineTotal` ve `Order.TotalAmount` değerlerinin korunduğu doğrulandı; ürün fiyatı test sonunda geri alındı.

### Adım 18 - Sipariş listeleme, filtreleme ve detay projection'ı

- `GET /api/orders` pagination ile sipariş listesi döndürüyor.
- Liste endpointinde dahil edici `From` ve `To` tarih aralığı, `Status` ve `CustomerId` filtreleri bulunuyor.
- Tarihler `DateTimeOffset` olarak kabul edilip sorguda UTC'ye çevriliyor; `From > To` ortak validation formatında 400 döndürüyor.
- Liste en yeni sipariş önce olacak biçimde `OrderDate DESC, Id DESC` ile kararlı sıralanıyor.
- Liste sorgusu entity graph yerine yalnızca `OrderListResponse` alanlarını projection ile getiriyor.
- `GET /api/orders/{id}` müşteri özeti, item'lar, fiyat snapshot'ları ve toplamları `OrderDetailResponse` projection'ıyla döndürüyor.
- İadesi bulunan item'larda iade kaydı sayısı, toplam iade adedi ve toplam iade tutarı; bulunmayanlarda `null` iade özeti dönüyor.
- Olmayan sipariş ortak 404 `ProblemDetails` yanıtı döndürüyor; entity navigation'ları doğrudan serialize edilmiyor.
- Swagger testinde pagination ve bütün filtrelerin query string'e bağlandığı, geçerli isteğin 200, ters tarih aralığının 400 ve olmayan siparişin 404 döndürdüğü doğrulandı.
- Yerel PostgreSQL runtime testinde iade kayıtları olan bir siparişin detay projection'ı, item iade özetleri ve sipariş toplamları doğrulandı.

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
