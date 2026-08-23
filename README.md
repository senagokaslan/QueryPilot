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
