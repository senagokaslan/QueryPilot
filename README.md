# QueryPilot

QueryPilot, PostgreSQL verisini ASP.NET Core üzerinden analiz eden ve AI'ı yalnızca kullanıcı sorusunu anlamak ile backend sonucunu açıklamak için kullanan bir Business Intelligence API projesidir.

## Proje yapısı

```text
QueryPilot/
|-- QueryPilot.sln
|-- global.json
`-- src/
    `-- QueryPilot.Api/
        |-- Features/
        |   |-- Categories/
        |   |-- Products/
        |   |-- Customers/
        |   |-- Orders/
        |   |-- Returns/
        |   |-- Analytics/
        |   `-- Ai/
        |-- Data/
        |   |-- AppDbContext.cs
        |   |-- Configurations/
        |   |-- Migrations/
        |   `-- Seed/
        |-- Common/
        |   |-- Exceptions/
        |   |-- Validation/
        |   `-- Responses/
        |-- Configuration/
        `-- Program.cs
```

Tek deploy birimi `QueryPilot.Api` projesidir. Microservice, CQRS, MediatR veya ek bir uygulama katmanı bu yapının parçası değildir.

Veri modeli ve ilişki kararları için [Database Modeli](docs/database-model.md) belgesine bakın.

## Namespace kuralı

C# namespace'leri proje kökü `QueryPilot.Api` ile başlar ve dosyanın klasör yolunu izler. Örneğin:

- `Features/Categories/Category.cs` -> `QueryPilot.Api.Features.Categories`
- `Data/Configurations/CategoryConfiguration.cs` -> `QueryPilot.Api.Data.Configurations`
- `Common/Exceptions/NotFoundException.cs` -> `QueryPilot.Api.Common.Exceptions`
- `Configuration/AiOptions.cs` -> `QueryPilot.Api.Configuration`

Bu README geliştirme ilerledikçe kurulum, çalıştırma, migration, test ve API kullanım bilgileriyle genişletilecektir.

## Yerel secret ayarları

Yerel PostgreSQL connection string'i .NET User Secrets içinde `ConnectionStrings:DefaultConnection` anahtarıyla tutulur. Değer source code veya `appsettings` dosyalarına yazılmaz.

OpenAI adapter'ı Responses API kullanır. Provider, model, API key ve timeout değerleri `AI` configuration bölümünden okunur. API anahtarını source code veya `appsettings.json` içine yazmayın; yerel geliştirmede user-secrets kullanın.

```powershell
dotnet user-secrets set "AI:Provider" "OpenAI" --project src/QueryPilot.Api
dotnet user-secrets set "AI:Model" "<model-id>" --project src/QueryPilot.Api
dotnet user-secrets set "AI:ApiKey" "<your-api-key>" --project src/QueryPilot.Api
dotnet user-secrets set "AI:TimeoutSeconds" "30" --project src/QueryPilot.Api
```

Deployment ortamında aynı değerler `AI__Provider`, `AI__Model`, `AI__ApiKey` ve `AI__TimeoutSeconds` environment variable'larıyla sağlanabilir. Timeout 1-120 saniye arasında olmalıdır. API key eksikken uygulama ve analytics endpointleri çalışmaya devam eder; yalnız AI çağrısı ortak 503 contractıyla sonuçlanır.

AI request logları provider, model, operasyon, sonuç ve süreyi içerir. API key, kullanıcı sorusu, analytics JSON'u ve provider'a gönderilen tam prompt loglanmaz.

Soru anlama çağrısı yalnız structured JSON intent döndürür. Desteklenen analizler `salesSummary`, `salesTrend`, `topProducts`, `categoryPerformance` ve `returnAnalysis` değerleridir. Intent ayrıca dönem veya açık tarihleri, ürün/kategori adını, top-products metriğini ve sonucu sınırlandıran limit değerini taşıyabilir. AI katmanı SQL üretmez ve metrikleri kendisi hesaplamaz.

Typed intent backend'e iletilmeden önce doğrulanır. Bütün analizler geçerli ve en fazla beş yıllık bir UTC tarih aralığı gerektirir; desteklenen relative Türkçe dönemler backend `TimeProvider` değeriyle çözülür. Sales trend yalnız daily/weekly/monthly granularity, top-products yalnız quantity/revenue metric ve 1-50 limit kabul eder. Kategori adı verilirse gerçek database kaydı aranır. Doğrulama başarısız olursa analytics servisi çağrılmaz.

Soru gerekli alanları içermiyorsa coordinator `NeedsClarification` sonucu döndürür. Bu response özgün soruyu, mevcut typed intenti, eksik veya belirsiz alanları, izin verilen değerleri ve yeniden gönderme talimatını içerir. Örneğin “En iyi ürünler hangileri?” sorusu için tarih aralığı ile quantity/revenue metriği istenir; rastgele tarih veya metric seçilmez. Ürün ve kategori adlarında yalnız kesin database eşleşmesi kabul edilir.

Doğrulanmış analytics sonucu provider'a JSON DTO olarak yalnız kısa Türkçe açıklama üretmesi için gönderilir. AI'a DTO dışında sayı, yüzde, tarih, ürün, müşteri veya kategori eklememesi ve değerleri yeniden hesaplamaması açıkça bildirilir. Açıklama en fazla 500 karakterdir; kaynak JSON'da bulunmayan numeric değer içeren veya sınırı aşan metin `RejectedUnsafe` olarak işaretlenir ve frontend'e gösterilecek metin taşınmaz. Provider hatasında açıklama `Unavailable` olur. Her iki durumda da backend'in hesapladığı `data` aynen korunur; frontend `explanation.status` alanına göre açıklamayı isteğe bağlı gösterebilir.

BI kapsamı dışındaki sorular `Unsupported` durumuyla, desteklenen beş analiz ve örnek soru listesiyle cevaplanır; analytics sorgusu çalıştırılmaz. Intent provider timeout'u, 429/5xx cevabı, bağlantı hatası veya geçersiz provider çıktısı nedeniyle çıkarılamazsa ortak ve detay sızdırmayan 503 response'u kullanılır. Açıklama çağrısı analytics hesabından sonra başarısız olursa response numeric `data` alanını korur ve kullanıcıya güvenli bir `warning` verir. Doğrudan `/api/analytics/*` endpointleri AI servisinden ve API key'den bağımsızdır.

`AiAnalyticsCoordinator`, doğrulanmış typed intenti sabit bir enum switch'iyle yalnız ilgili `IAnalyticsService` metoduna yönlendirir. Sales trend granularity; top products metric, limit ve kesin eşleşmiş category id parametreleriyle çağrılır. AI tarafından sağlanan method adı, reflection veya SQL çalıştırılmaz. Relative Türkçe dönemler backend `TimeProvider` üzerinden kesin UTC başlangıç-dahil/bitiş-hariç aralığına çevrilmeden analytics çağrısı yapılmaz.

Doğal dil analytics endpointi `POST /api/ai/query` adresindedir. `question` zorunludur, yalnız boşluk içeremez ve en fazla 2000 karakter olabilir.

```json
{
  "question": "Son 3 ayın aylık satış trendini göster."
}
```

Başarılı response `status`, analiz türü ve kullanılan UTC parametreleri taşıyan `intent`, gerçek backend sonucu olan `data`, trend analizlerinde ayrıca `chartData` ve isteğe bağlı `explanation` alanlarını içerir. Clarification ve unsupported sonuçları aynı contract içinde kendi typed alanlarını kullanır. Provider tamamen kullanılamıyorsa endpoint ortak, detay sızdırmayan Problem Details 503 cevabı döndürür. Swagger XML açıklamaları request modeli, endpoint davranışı ve 200/400/503 response tiplerini gösterir.

Intent extraction, validation, analytics routing ve açıklama adımlarının özeti için [AI query akışı](docs/ai-query-flow.md) belgesine bakın.

`QueryPilot.Api.Tests` projesi solution'a dahildir. Order testleri istemcinin finansal toplam gönderemediğini, `OrderItem.UnitPrice`/`LineTotal` snapshotlarını ve `Order.TotalAmount` değerini backend ürün fiyatlarından hesaplandığını doğrular. Return testleri partial/full/over-return kurallarını ve `Return.Amount` değerinin güncel Product fiyatı yerine OrderItem fiyat snapshotından üretildiğini kapsar. Invalid business senaryolarında bir EF `SaveChangesInterceptor` ile `SaveChanges` çağrı sayısının sıfır kaldığı kontrol edilir.

`QueryPilot.Api.IntegrationTests` ayrı bir PostgreSQL integration test projesidir. Testler yalnız loopback PostgreSQL sunucusunu kabul eder, `querypilot_test_<guid>` adında benzersiz bir database oluşturur, migrationları uygular, elle hesaplanabilir fixture'ı yükler ve test sonunda yalnız bu kesin isim kalıbındaki database'i kaldırıp silindiğini doğrular. Development veya production database adı hiçbir zaman test bağlantısı olarak kullanılmaz.

Integration testleri yerelde `IntegrationTests:AdminConnectionString` user-secret'ından, CI ortamında ise `QUERYPILOT_INTEGRATION_ADMIN_CONNECTION_STRING` değişkeninden database oluşturma/silme yetkisi olan yalnız test amaçlı PostgreSQL rolünü okur. İki ayar da yoksa PostgreSQL testleri açıkça skipped olarak raporlanır; uygulamanın development connection string'ine geri düşülmez.

```powershell
.\scripts\setup-integration-tests.ps1
dotnet test tests/QueryPilot.Api.IntegrationTests/QueryPilot.Api.IntegrationTests.csproj
```

Setup scripti mevcut `Development:PostgresAdminPassword` user-secret'ını yalnız rol kurulumunda kullanır. `querypilot_test_admin` rolünü `LOGIN` ve `CREATEDB` yetkileriyle oluşturur veya parolasını yeniler; role `SUPERUSER` ya da `CREATEROLE` vermez. Oluşturulan bağlantı parolasını terminale yazmadan user-secrets'a kaydeder.

Fixture; summary, günlük/haftalık/aylık trend, top-products quantity/revenue sıralaması, kategori payı ve önceki dönem karşılaştırması, iade oranı/nedenleri/ürünleri, boş dönem, yıl geçişi ve sıfır satış paydası senaryolarını kapsar.

`WebApplicationFactory` contract testleri aynı disposable PostgreSQL database üzerinde Category, Product, Order, Return, Analytics ve AI endpointlerini gerçek HTTP pipeline'ından geçirir. AI çağrılarında deterministik `FakeAiService` kullanılır; model validation ve ortak 400/404/409/500/503 Problem Details response'ları HTTP seviyesinde doğrulanır.

Gerçek OpenAI testi normal test paketinden ayrı ve varsayılan olarak skipped bir smoke testtir. Yalnız bilinçli olarak aşağıdaki üç environment variable sağlandığında ücretli provider çağrısı yapar:

```powershell
$env:QUERYPILOT_RUN_OPENAI_SMOKE = "true"
$env:AI__ApiKey = "<your-api-key>"
$env:AI__Model = "<model-id>"
dotnet test tests/QueryPilot.Api.Tests/QueryPilot.Api.Tests.csproj --filter "Category=OpenAiSmoke"
```

## Yerel PostgreSQL

Development veritabanı PostgreSQL 18 üzerinde çalışır. Cluster verisi `%LOCALAPPDATA%\QueryPilot\PostgreSQL18\data` altında tutulur ve yalnız `127.0.0.1:5432` üzerinden erişilir. Host bağlantıları SCRAM-SHA-256 parola doğrulaması kullanır.

`QueryPilotPostgreSQL18` adlı kullanıcı görevi, PostgreSQL'i Windows oturumu açıldığında otomatik olarak başlatır. Görevin durumunu kontrol etmek için:

```powershell
Get-ScheduledTask -TaskName "QueryPilotPostgreSQL18"
```

Sunucuyu başlatmak için:

```powershell
& "$env:ProgramFiles\PostgreSQL\18\bin\pg_ctl.exe" start `
  -D "$env:LOCALAPPDATA\QueryPilot\PostgreSQL18\data" `
  -l "$env:LOCALAPPDATA\QueryPilot\PostgreSQL18\postgres.log"
```

Sunucuyu durdurmak için:

```powershell
& "$env:ProgramFiles\PostgreSQL\18\bin\pg_ctl.exe" stop `
  -D "$env:LOCALAPPDATA\QueryPilot\PostgreSQL18\data"
```

Uygulama `querypilot_dev` veritabanına yalnız `querypilot_app` rolüyle bağlanır. Connection string ve parolalar Git dışında .NET User Secrets içinde tutulur.

## Migration ve demo verisi

Repository-local EF Core aracını ve migrationları çalıştırmak için:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update `
  --project src/QueryPilot.Api/QueryPilot.Api.csproj `
  --startup-project src/QueryPilot.Api/QueryPilot.Api.csproj
```

Development ortamında `DemoSeed:Enabled` açık olduğunda boş veritabanına deterministik demo veri eklenir. Seed yaklaşık bir yıllık döneme yayılan 10 kategori, 100 ürün, 500 müşteri ve bunlardan türetilen sipariş, sipariş satırı ve iade kayıtlarını oluşturur. Herhangi bir business verisi varsa seed atlanır; böylece yeniden çalıştırma duplicate kayıt üretmez.

Temel `appsettings.json` içinde seed kapalıdır ve uygulama ayrıca yalnız `Development` ortamında seed çalıştırır. Bu nedenle production ortamında demo veri oluşturulmaz.
