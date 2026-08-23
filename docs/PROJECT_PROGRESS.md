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

### Adım 19 - Sipariş entegrasyon testleri

- Yerel PostgreSQL üzerinde geçerli müşteri ve ürünle tek item'lı sipariş 201 ile oluşturuldu.
- İki farklı ürün içeren sipariş 201 ile oluşturuldu; item toplamları `Order.TotalAmount` ile eşleşti.
- Olmayan müşteri 404, pasif müşteri 409 ile reddedildi.
- Olmayan ürün 404, pasif ürün 409 ile reddedildi.
- Sıfır ve negatif `Quantity` değerleri 400 validation yanıtıyla reddedildi.
- Request'e eklenen `UnitPrice`, `LineTotal` ve `TotalAmount` alanları kullanılmadı; tanımsız finansal alan politikası gereği request 400 ile reddedildi.
- Ürün fiyatı geçici olarak değiştirildiğinde eski siparişin `OrderItem.UnitPrice` snapshot'ı aynı kaldı; ürün fiyatı test sonunda geri alındı.
- Geçerli ve olmayan ürünün birlikte gönderildiği hatalı request öncesi ve sonrası database order sayısı eşitti; yarım order veya item kaydı oluşmadı.
- Yeni oluşturulan tek ve çok item'lı siparişler liste endpointinde bulundu ve detay endpointinden finansal alanlarıyla doğrulandı.

### Adım 20 - İade request'i, miktar limiti ve tutar politikası

- `CreateReturnRequest` yalnızca `OrderItemId`, `Quantity` ve `Reason` alanlarını kabul ediyor; tanımsız JSON alanları reddediliyor.
- `OrderItemId` ve `Quantity` pozitif olmak zorunda; `Reason` trim ediliyor, boş/whitespace ve 500 karakterden uzun değerler reddediliyor.
- OrderItem bulunamadığında 404 dönüyor.
- Yalnızca `Completed` durumundaki siparişlerin item'ları iade edilebiliyor; `Pending` ve `Cancelled` siparişler 409 ile reddediliyor.
- Satın alınan miktar `OrderItem.Quantity` değerinden, önceki iade miktarı aynı item'a ait `Return.Quantity` toplamından okunuyor.
- Yeni iade miktarı kalan iade edilebilir miktarı aşarsa 400 validation yanıtı dönüyor ve iade kaydı oluşmuyor.
- Miktar kontrolü ve kayıt aynı transaction içinde yapılıyor; OrderItem satırı `FOR UPDATE` ile kilitlenerek eşzamanlı iadelerin limiti aşması engelleniyor.
- `Return.Amount`, istemciden alınmıyor; `OrderItem.UnitPrice` fiyat snapshot'ı ile iade miktarının `decimal` çarpımından hesaplanıyor.
- `ReturnResponse`, iade tutarıyla birlikte işlem sonrası kalan iade edilebilir miktarı döndürüyor.

### Adım 21 - İade endpointi, transaction ve concurrency

- `POST /api/returns` başarılı işlemde 201 ve `ReturnResponse` döndürüyor.
- Satın alınan miktarı aşan veya tamamen iade edilmiş item'a yapılan yeni iade ortak 409 `ProblemDetails` yanıtıyla reddediliyor.
- OrderItem ve önceki iade miktarları transaction içinde yeniden okunuyor; `FOR UPDATE` satır kilidi aynı item'a gelen eşzamanlı istekleri sıraya alıyor.
- `Return.Amount`, `OrderItem.UnitPrice` fiyat snapshot'ı ve iade miktarından `decimal` olarak hesaplanıyor; `ReturnDate` backend tarafından UTC atanıyor.
- Başarılı iade olayı yalnızca `ReturnId`, `OrderItemId` ve `Quantity` ile loglanıyor; reason, amount, müşteri veya request body loglanmıyor.
- Yerel PostgreSQL runtime testinde olmayan item 404, Pending sipariş 409, geçersiz miktar/reason ve istemci `Amount` alanı 400, over-return 409 döndürdü.
- Kısmi iade 201 ile oluşturuldu; tutarın fiyat snapshot'ından hesaplandığı, tarihin UTC olduğu ve kalan miktarın doğru döndüğü doğrulandı.
- Aynı kalan miktar için eşzamanlı iki istekten biri 201, diğeri 409 aldı; toplam iade miktarı satın alınan miktarı aşmadı.
- Order detail iade özetinde toplam iade miktarı ve tutarın finansal olarak tutarlı olduğu doğrulandı.

### Adım 22 - İade kabul senaryoları

- Beş adetlik sipariş satırından önce 2, sonra kalan 3 adet iade edildi; istekler 201, kalan miktarlar sırasıyla 3 ve 0 döndürdü.
- Tamamı iade edilmiş satıra yeni iade ve ayrı bir satırda 2 adetten sonra kalanı aşan 4 adetlik iade 409 ile reddedildi.
- `Quantity` değeri 0 ve negatif olan istekler 400, olmayan `OrderItem` isteği 404 döndürdü.
- Test ürününün fiyatı siparişten sonra 12,34'ten 99,99'a değiştirildi; iadeler eski `OrderItem.UnitPrice` değeriyle 24,68 ve 37,02 olarak hesaplandı.
- Boşlukları temizlenen iade nedeni veritabanında `Checklist partial return` olarak saklandı.
- SQL doğrulamasında test satırlarının toplam iade miktarları 5/5, 2/5 ve 3/5 bulundu; hiçbiri satın alınan miktarı aşmadı.
- Aynı beş adetlik satıra eşzamanlı gönderilen iki adet 3'lük iade isteğinden biri 201, diğeri 409 aldı; veritabanında yalnızca 3 adet iade oluştu.

### Adım 23 - Analytics tarih ve response temeli

- `IAnalyticsService` ve AI katmanına bağımlılığı olmayan `AnalyticsService` oluşturulup dependency injection'a kaydedildi.
- Analytics tarih aralığı UTC'ye normalize edildi; başlangıç dahil, bitiş hariç yarı-açık aralık ortak `AnalyticsDateRange` modeliyle tanımlandı.
- Başlangıcı bitişten önce olmayan aralıklar ve beş yılı aşan sorgular ortak validation hatasıyla reddediliyor.
- Günlük, haftalık ve aylık seçenekler `AnalyticsGranularity` enum'unda tanımlandı ve JSON'da anlaşılır string değer olarak sunuluyor.
- Grafikler için UTC `periodStart`, okunabilir `label` ve `decimal value` alanlarını taşıyan ortak `AnalyticsChartPoint` modeli eklendi.
- Verisiz dönem politikası para ve yüzde için numeric `0`, adet için numeric `0`, koleksiyonlar için boş liste olarak `AnalyticsResponseDefaults` ile tanımlandı.
- Davranış kontrolünde farklı offsetli tarihler UTC'ye çevrildi; aralığın ilk anı dahil, son anı hariç tutuldu ve chart `value` alanı JSON'da string yerine sayı döndü.
- Analytics kodunda `DateTime.Now`/`DateTimeOffset.Now`, ay adına göre grouping veya AI servis bağımlılığı bulunmadığı statik taramayla doğrulandı; sonraki analizler ortak UTC tarih ve chart contractını kullanacak.

### Adım 24 - Sales Summary analizi

- Satış analizine yalnızca `Completed` siparişlerin dahil edilmesi `AnalyticsService.IncludedOrderStatus` ile açıkça tanımlandı; `Pending` ve `Cancelled` siparişler hariç tutuluyor.
- Seçilen UTC yarı-açık tarih aralığı için toplam gelir PostgreSQL'de `SUM`, sipariş sayısı `COUNT` ve satılan adet `OrderItem.Quantity` üzerinden `SUM` ile hesaplanıyor.
- Ortalama sipariş tutarı toplam gelirin sipariş sayısına bölünmesiyle hesaplanıyor; siparişsiz aralık sorgusu erken boş response döndürdüğünden sıfıra bölme oluşmuyor.
- `SalesSummaryResponse`, UTC aralıkla birlikte numeric `totalRevenue`, `orderCount`, `unitsSold` ve `averageOrderValue` alanlarını döndürüyor.
- Sorgular `AsNoTracking`, filtre, grouping ve aggregate projection kullanıyor; entity veya ilişkili koleksiyonlar belleğe alınmıyor.
- `querypilot_dev` üzerinde rollback edilen fixture ile elle beklenen 65 gelir, 2 sipariş, 5 adet ve 32,5 ortalama değerlerinin servis sonucuyla birebir eşleştiği doğrulandı.
- Boş tarih aralığı ve yalnız `Cancelled` sipariş içeren aralık bütün metriklerde numeric sıfır döndürdü; test verisi transaction sonunda rollback edildi.

### Adım 25 - Sales Trend analizi

- `GetSalesTrendAsync`, tanımsız granularity değerlerini ortak validation hatasıyla reddediyor; `Daily`, `Weekly` ve `Monthly` destekleniyor.
- Tamamlanmış siparişler PostgreSQL'de UTC gün başlangıcına göre aggregate ediliyor; gelir, sipariş sayısı ve `OrderItem.Quantity` toplamı her bucket için hesaplanıyor.
- Haftalar Pazartesi 00:00 UTC'de başlıyor; aylık bucket anahtarı yıl ve ayı birlikte temsil eden ayın ilk UTC anıdır, ay adına göre database grouping yapılmıyor.
- Sonuçlar en eski bucket'tan en yeniye üretiliyor ve satış olmayan ara gün, hafta veya aylar numeric sıfır noktalarla dolduruluyor.
- `SalesTrendResponse`, revenue, order count ve units sold için ortak `periodStart`, `label`, numeric `value` chart serilerini döndürüyor.
- PostgreSQL rollback fixture testinde 28 ve 29 Şubat 2024 ayrı günler olarak, 1 Mart satışsız sıfır nokta olarak ve iki haftalık serinin başlangıçları Pazartesi olarak doğrulandı.
- Aralık 2023 ve Ocak 2024 ayrı aylık bucket'larda doğrulandı; böylece yıl geçişi ve yıl+ay gruplama politikası test edildi.
- EF komut kaydında aggregate sorguların PostgreSQL'e gönderildiği ve gün gruplamasının `date_trunc('day', ...)` olarak çevrildiği görüldü; bireysel sipariş entityleri belleğe alınmadı.

### Adım 26 - Top Products analizi

- `GetTopProductsAsync`, seçilen UTC aralıktaki yalnız `Completed` sipariş satırlarını ProductId bazında grupluyor.
- Satılan adet `OrderItem.Quantity`, gelir ise finansal snapshot olan `OrderItem.LineTotal` üzerinden PostgreSQL `SUM` ile hesaplanıyor.
- Kullanıcı `Quantity` veya `Revenue` metriğini seçebiliyor; tanımsız metric validation hatasıyla reddediliyor.
- Sonuç limiti varsayılan 5 ve maksimum 50 olarak belirlendi; sıfır, negatif veya maksimumu aşan limitler reddediliyor.
- Pozitif `CategoryId` ile opsiyonel kategori filtresi uygulanıyor; geçersiz kategori kimliği formatı validation hatası döndürüyor.
- Adet sıralamasında eşitlik revenue, gelir sıralamasında eşitlik quantity ile bozuluyor; sonraki kararlı kriter artan ProductId.
- `TopProductResponse`; rank, product id, name, SKU, quantity ve revenue alanlarını numeric finansal/adet değerleriyle döndürüyor.
- Rollback fixture testinde A ürünü adette, B ürünü gelirde lider oldu; eşit B/C sonuçları ProductId ile kararlı sıralandı ve kategori filtresi diğer kategoriyi dışladı.
- B ürününe yeni büyük sipariş eklendikten sonra adet liderliği A'dan B'ye dinamik olarak geçti; miktar 15 ve gelir 450 olarak yeniden hesaplandı.
- EF komut kaydında gruplama, toplam, sıralama ve limitin PostgreSQL'e çevrildiği doğrulandı; test transaction'ı rollback edildi.

### Adım 27 - Category Performance analizi

- `GetCategoryPerformanceAsync`, seçilen UTC yarı-açık tarih aralığındaki `Completed` sipariş satırlarını Product ve Category ile birleştiriyor.
- Her kategori için snapshot `OrderItem.LineTotal` geliri ve `OrderItem.Quantity` satış adedi PostgreSQL'de gruplanıp toplanıyor.
- Genel gelir kategori aggregate sonuçlarının toplamından hesaplanıyor; her kategori payı `category revenue / total revenue * 100` formülüyle backend'de numeric olarak üretiliyor.
- Genel gelir sıfırken bölme yapılmıyor; boş dönem numeric sıfır toplam ve boş kategori listesi döndürüyor.
- Kategoriler revenue azalan, eşitlikte CategoryId artan sırada dönüyor; response rank, category id, name, revenue, units sold ve revenue share percentage içeriyor.
- Product veya Category `IsActive` filtresi uygulanmıyor; pasifleşmiş kayıtların tarihsel satışları analytics sonucunda korunuyor.
- Rollback fixture testinde pasif kategorinin 300 geliri ve 3 adedi, aktif kategorinin 100 geliri ve 10 adedi hesaplandı; kategori gelirlerinin toplamı 400 genel gelire, payların toplamı yüzde 100'e eşit bulundu.
- İptal edilmiş ve bitiş anındaki siparişler sonuca girmedi; EF komut kaydında Product/Category join, grouping, sum ve ordering PostgreSQL'e çevrildi, `IsActive` filtresi bulunmadı.

### Adım 28 - Önceki dönem karşılaştırması

- Mevcut UTC aralığın elapsed tick uzunluğu hesaplanıyor; önceki aralık aynı uzunlukta ve mevcut başlangıca bitişik `[currentFrom - duration, currentFrom)` olarak oluşturuluyor.
- Sales Summary mevcut ve önceki dönem revenue değerlerini aynı `Completed` sipariş, UTC ve yarı-açık tarih kurallarıyla ayrı ayrı hesaplayıp ortak comparison response'u döndürüyor.
- Yüzde değişimi `(current - previous) / previous * 100` formülüyle backend'de hesaplanıyor ve iki ondalığa `AwayFromZero` kuralıyla yuvarlanıyor.
- Önceki değer sıfırsa yüzde `null` ve durum `NoBaseline`; diğer sonuçlar `Increase`, `Decrease` veya `NoChange` olarak string enum ile işaretleniyor.
- Category Performance aynı `MetricComparisonResponse` ve `ComparisonPeriodResponse` modellerini hem genel gelir hem kategori geliri için yeniden kullanıyor; yalnız önceki dönemde bulunan kategoriler de sıfır mevcut değerle korunuyor.
- Response, mevcut ve önceki UTC sınırlarını, exact duration tick değerini ve takvim ayı kaydırmak yerine aynı elapsed UTC süresinin kullanıldığını açıklayan policy metadata'sını döndürüyor.
- Farklı ay uzunluğu testinde 1 Mart-1 Nisan aralığının 31 günlük önceki dönemi 29 Ocak-1 Mart olarak oluştu; aralıklar bitişik ve eşit tick uzunluğunda bulundu.
- Rollback fixture testinde 500.000 mevcut ve 600.000 önceki revenue sonucu `-16,67` ve `Decrease` döndü; ayrı aralıklarda `Increase`, `NoChange` ve `NoBaseline` durumları doğrulandı.

### Adım 29 - Return Analytics

- `GetReturnAnalyticsAsync`, seçilen UTC yarı-açık aralıktaki iade kaydı sayısını, toplam iade adedini ve snapshot `Return.Amount` toplamını PostgreSQL aggregate sorgularıyla hesapıyor.
- İade oranı `ReturnDate` aralığındaki iade adedi / aynı UTC aralıkta `OrderDate` ile filtrelenen `Completed` siparişlerin satış adedi * 100 olarak tanımlandı.
- Satış paydası sıfırsa yüzde `null` ve durum `NoSalesBaseline`; aksi durumda iki ondalıklı numeric yüzde ve `Calculated` dönüyor.
- Response metadata'sı oran formülünü, iade metriklerinin `ReturnDate`, satış paydasının `OrderDate` kullandığını ve iki filtrenin de başlangıç dahil/bitiş hariç olduğunu açıklıyor.
- İadeler Reason bazında kayıt sayısı, quantity ve amount ile gruplanıyor; ürünler quantity azalan, amount azalan ve ProductId artan kararlı sırada ilk 10 sonuç olarak dönüyor.
- Rollback fixture testinde aynı item'a ait 2 ve 3 adetlik iki kısmi iade `Damaged` grubunda 2 kayıt, 5 adet ve 100 tutar olarak birleşti.
- Bilinen fixture için 3 iade kaydı, 6 iade adedi, 150 tutar ve 12 satış adedi hesaplandı; iade oranı elle hesaplanan yüzde 50 ile eşleşti.
- Product güncel fiyatı 999 iken iade tutarları OrderItem snapshot fiyatlarından gelen 40, 60 ve 50 olarak toplandı; güncel fiyat analytics tutarını etkilemedi.
- Eski siparişe ait fakat seçilen dönemde gerçekleşen iade `ReturnDate` ile sayıldı; aynı dönemde satış olmadığından `NoSalesBaseline` doğrulandı. Boş dönem sıfır metrikler ve boş listeler döndürdü.

### Adım 30 - Analytics API endpointleri ve sorgu incelemesi

- `GET /api/analytics/summary`, `sales-trend`, `top-products`, `categories` ve `returns` endpointleri ortak `AnalyticsController` üzerinden yayınlandı.
- Bütün endpointler nullable request DTO'larıyla zorunlu `From`/`To` alanlarını doğruluyor; trend granularity, top-products metric, limit ve isteğe bağlı category id değerleri model validation ile kontrol ediliyor.
- Tarih aralığının UTC'ye çevrilmesi, başlangıç dahil/bitiş hariç kuralı, sıralama ve maksimum aralık doğrulamaları AnalyticsService'teki ortak kuralları kullanmaya devam ediyor.
- AnalyticsController yalnızca `IAnalyticsService`, AnalyticsService yalnızca `AppDbContext` bağımlılığı alıyor; endpointlerin çalışması için AI servisi veya AI key gerekmiyor.
- Tarih aralığı en fazla 5 yıl, top-products sonucu varsayılan 5 ve en fazla 50 kayıtla sınırlı; return analytics ürün listesi de 10 kayıtla sınırlandırılıyor.
- Model validation ve servis validation hataları aynı `application/problem+json` contractıyla dönüyor. Exception handler runtime ProblemDetails tipini serialize ederek `HttpValidationProblemDetails.Errors` alanını koruyor.
- Seed veritabanında beş endpoint Swagger UI üzerinden `2025-01-01T00:00:00Z`–`2027-01-01T00:00:00Z` aralığıyla çalıştırıldı ve tamamı 200 döndürdü.
- Summary sonucu 1.291.549,48 revenue, 986 sipariş ve 7.513 ürün; trend Monthly için 24 nokta; top-products Revenue için 5 kayıt; categories için 10 kayıt; returns için 285 kayıt ve 487 adet döndürdü.
- Trend response'unda `periodStart` değerlerinin `+00:00` UTC, label alanlarının okunabilir ve para/adet/value alanlarının JSON number olduğu Swagger response'unda doğrulandı.
- EF Core komut kayıtlarında aggregate, grouping, ordering ve limit işlemlerinin PostgreSQL'e çevrildiği görüldü. Endpointler sabit sayıda sorgu çalıştırıyor; N+1 veya tüm entity/tabloyu belleğe alan sorgu bulunmuyor.
- Gerçek seed verisiyle `EXPLAIN (ANALYZE, BUFFERS)` incelemesinde Orders tarih filtresi `IX_Orders_OrderDate` indeksini kullandı. Küçük OrderItems ve Returns tablolarında planner'ın seçtiği sequential scan'ler yaklaşık 0,09–1,03 ms aralığında tamamlandı.
- `OrderDate`, `ReturnDate`, `OrderItemId`, `OrderId`, `ProductId` ve `CategoryId` foreign-key/tarih indeksleri mevcut. Bu veri ve planlar için yeni indeks eklenmedi; üretim ölçeğinde gerçek sorgu istatistikleriyle yeniden değerlendirme not edildi.

### Adım 31 - AI servis soyutlaması ve OpenAI adapter'ı

- `IAiService`, kullanıcı sorusunu anlamak ve backend analytics sonucunu açıklamak için iki ayrı async/cancellable metotla oluşturuldu.
- Feature'a ait `AiQuestionUnderstanding` ve `AiResultExplanation` modelleri provider response tiplerinin uygulama sözleşmesine sızmasını engelliyor.
- `OpenAiService`, OpenAI Responses API'ye typed `HttpClient` üzerinden bağlanan adapter olarak Dependency Injection'a kaydedildi.
- Provider, model, API key ve 1-120 saniye aralığındaki timeout `AI` configuration bölümünden okunuyor; key user-secrets veya `AI__ApiKey` environment variable ile sağlanabiliyor.
- Eksik AI configuration uygulama başlangıcını veya analytics endpointlerini engellemiyor; yalnız AI metodu çağrıldığında kontrollü `AiServiceUnavailableException` üretiyor.
- Caller cancellation doğrudan korunuyor, provider timeout'u ortak 503 hatasına çevriliyor; 408, 429 ve 5xx geçici durumları en fazla üç denemeyle ele alınıyor.
- Loglarda provider, model, operasyon, sonuç ve elapsed milliseconds bulunuyor; API key, kullanıcı sorusu, analytics JSON'u ve tam prompt yazılmıyor.
- `FakeAiService` deterministik sonuç veya test delegate'leriyle gerçek API çağrısı olmadan kullanılabiliyor.
- Fake contract, OpenAI response mapping, Authorization header, transient retry, caller cancellation, timeout ve hassas log içeriği testleri eklendi.
- Repository taramasında gerçek veya sabitlenmiş API key bulunmadı; business/analytics servisleri provider adapter ya da OpenAI/Gemini tiplerine doğrudan bağımlı değil.

### Adım 32 - Yapılandırılmış analytics intent çıkarımı

- AI sistem talimatında QueryPilot'ın desteklediği `salesSummary`, `salesTrend`, `topProducts`, `categoryPerformance` ve `returnAnalysis` seçenekleri ile her analizin kapsamı açıkça tanımlandı.
- `AiQuestionUnderstanding`, analysis, period, from, to, productName, categoryName, metric ve limit alanlarını taşıyan provider bağımsız uygulama contractına dönüştürüldü.
- Relative dönem ifadeleri ayrı `period`, açık tarih sınırları ayrı `from`/`to`, ürün ve kategori adları kendi alanlarında çıkarılıyor; belirtilmeyen alanlar tahmin edilmeden `null` kalıyor.
- Top-products için adet/satış miktarı `quantity`, gelir/ciro `revenue` metriğine eşleniyor; kullanıcı tarafından verilen limit 1-50 aralığında yapılandırılmış alana alınıyor.
- OpenAI Responses isteği resmi Structured Outputs biçimindeki `text.format`, `json_schema` ve `strict: true` ayarlarını kullanıyor; bütün alanlar required-nullable ve ek alanlar yasak.
- Prompt ve adapter AI'dan SQL, metrik hesabı veya serbest business cevabı kabul etmiyor; JSON parse hataları, ek alanlar ve sınır dışı limitler kontrollü AI unavailable hatasına çevriliyor.
- Soru metni `question.Contains` benzeri hazır kalıplarla yönlendirilmiyor. AI yalnız analiz türü ve parametreleri çıkarıyor; satış rakamlarını hesaplama, ürünleri sıralama ve database sonucunu üretme görevi backend analytics servisinde kalıyor.
- `Son 3 ayda en çok satan 5 ürün ne?` örneğinde `topProducts`, `son 3 ay`, `quantity` ve `5` çıktıları gerçek HTTP çağrısı olmadan adapter seviyesinde doğrulandı.
- Satış özeti, tarih aralıklı trend, kategori performansı, iade nedenleri, gelire göre ilk 10 ürün ve ürün adına göre özet içeren altı farklı Türkçe soru kalıbı test edildi.

### Adım 33 - AI intent doğrulama ve güvenli analytics yönlendirmesi

- AI JSON cevabı `AiQuestionUnderstanding` typed modeline deserialize ediliyor; bilinmeyen enum değerleri, malformed JSON ve beklenmeyen alanlar adapter sınırında reddediliyor.
- `AiIntentValidator`, unknown analysis değerini ve her analiz için zorunlu ortak tarih aralığını doğruluyor; sales-trend granularity, top-products metric ve limit alanlarına özel kurallar uyguluyor.
- Açık ISO-8601 tarihleri UTC'ye çevriliyor. `son N gün/hafta/ay/yıl`, bugün, bu/geçen hafta, ay ve yıl dönemleri merkezi `TimeProvider` üzerinden deterministik UTC aralığına dönüştürülüyor.
- Tarih başlangıcı bitişten önce olmak ve aralık en fazla AnalyticsService ile aynı 5 yıllık backend sınırında kalmak zorunda.
- Granularity yalnız Daily, Weekly veya Monthly; metric yalnız Quantity veya Revenue; top-products limit varsayılan 5 ve 1-50 aralığında.
- Kategori adı verilirse inactive filtresi uygulanmadan database'de case-insensitive kategori kaydı aranıyor ve CategoryId typed komuta ekleniyor; bulunmayan kategori reddediliyor.
- `rawSql`, `command` ve diğer beklenmeyen JSON alanları `additionalProperties: false` ve unmapped-member reddiyle hiçbir zaman komuta dönüşmüyor veya çalıştırılmıyor.
- `AiAnalyticsCoordinator` sırasıyla AI parse ve intent validation çalıştırıyor; ancak ikisi de başarılı olursa ilgili `IAnalyticsService` metoduna yönlendiriyor.
- Malformed JSON, unknown analysis, invalid/çok geniş tarih, eksik veya geçersiz granularity/metric, sıfır/aşırı limit ve bulunmayan kategori test edildi. Invalid intent testlerinde analytics çağrı sayısı sıfır kaldı.
- Semantic intent validation hataları güvenli ortak 400 validation response'una, malformed veya provider kaynaklı çıktılar prompt/key/raw provider içeriğini açığa çıkarmayan ortak 503 response'una bağlı; sınırsız AI tarih veya limit değeri service metoduna ulaşmıyor.

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

Adım 34 kapsamında doğal dil analytics akışını HTTP endpointi ve ortak response contractıyla yayınlamak.

## Git geçmişi

- `02f990d` - Ortak API hata ve pagination altyapısı
- `f9c2475` - Category yönetim API'si
- `137f6ba` - Yerel PostgreSQL 18 kurulum dokümantasyonu
- `5653bd3` - Product modülü ve kategori/update sağlamlaştırmaları
