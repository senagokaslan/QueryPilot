# Production deployment

Bu belge QueryPilot backend'inin bir Docker hostu ve managed PostgreSQL üzerinde güvenli biçimde yayınlanması için uygulanacak runbook'tur. Gerçek connection string, parola veya API key repository'ye yazılmaz.

> Durum: Cloud deployment bilinçli olarak ertelenmiştir. Render ve Neon üzerinde çalışan QueryPilot kaynağı yoktur. Aşağıdaki Render yerleşimi yalnız gelecekte yeniden yayın kararı verilirse kullanılacak referans blueprint'tir; `render.yaml` uygulanmamıştır.

## Referans production yerleşimi

- Backend: Render paid Web Service (`starter`), Docker runtime, Frankfurt region
- Database: Render Postgres `basic-256mb`, PostgreSQL 18, 15 GB autoscaling disk, Frankfurt region
- Frontend: Ayrı HTTPS origin; bu kesin origin backend CORS listesine eklenir
- Migration: Render pre-deploy command; runtime başlangıcından ayrı ve her release'te yalnız bir kez

Repository kökündeki `render.yaml` bu kaynakları Blueprint olarak tanımlar. Database public IP allow list'i boş bırakılmıştır; yalnız aynı region'daki Render private network erişimi kullanılır. Ücretli Render Postgres sürekli backup/PITR sağlar; Hobby workspace'te 3 günlük, Pro ve üzeri workspace'te 7 günlük recovery window bulunur. `basic-256mb` planının 100 bağlantı sınırı vardır. Tek backend instance için runtime connection string'inde `Maximum Pool Size=20` kullanın; 80 bağlantı migration, yönetim ve ölçekleme rezervi olarak kalır. Instance sayısı arttığında `(instance sayısı × 20) + rezerv` toplamını 100 altında tutun.

Render edge TLS'i sonlandırır, HTTP isteklerini HTTPS'e yönlendirir ve container'a private HTTP üzerinden proxy eder. `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` özgün şema bilgisinin ASP.NET Core'a taşınmasını sağlar.

## Database kullanıcıları

Provider'ın admin hesabını uygulamada kullanmayın. Aynı database için iki login kullanılır:

- `querypilot_migrator`: Yalnız deployment job'unda kullanılır; application schema'sının sahibi olur ve migration/extension oluşturabilir.
- `querypilot_app`: Yalnız runtime'da kullanılır; schema usage ile tablo/sequence CRUD yetkilerine sahiptir, DDL çalıştıramaz.

Render Blueprint database owner olarak `querypilot_migrator` hesabını oluşturur. `querypilot_app` hesabını farklı bir parola ile hazırlamak için repository'deki script'i interaktif bir psql oturumunda çalıştırın:

```powershell
psql "<Render external PSQL URL>" `
  --file scripts/configure-production-roles.sql
```

psql parola değerini interaktif olarak sorar. Script idempotent role oluşturma, parola rotasyonu, public schema hardening, default privileges ve mevcut tablo/sequence grant'lerini birlikte uygular. İlk migration'dan sonra script'i ikinci kez çalıştırmak mevcut nesne grant'lerini tamamlar.

Migration'lar `citext` extension'ı kullanır. Seçilen managed PostgreSQL planında bu extension'ın desteklendiğini deployment öncesinde doğrulayın. Provider database oluşturmayı kısıtlıyorsa mevcut database'in ownership/schema adımlarını provider'ın admin hesabıyla uygulayın.

## Secret ve environment contract'ı

Host secret store'a aşağıdaki değerleri ekleyin. Placeholder'ları gerçek değerle yalnız host panelinde değiştirin:

| Anahtar | Kaynak | Açıklama |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | Secret | `querypilot_app` runtime connection string'i; TLS ve sınırlı connection pool zorunlu olmalı |
| `AI__ApiKey` | Secret | AI provider API key'i |
| `AI__Provider` | Environment | `Gemini` |
| `AI__Model` | Environment | `gemini-3.5-flash-lite` veya kullanılacak geçerli Gemini model kimliği |
| `AI__TimeoutSeconds` | Environment | `30` |
| `ASPNETCORE_ENVIRONMENT` | Environment | `Production` |
| `ASPNETCORE_URLS` | Environment | `http://+:8080` |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | Environment | Host HTTPS'i reverse proxy'de sonlandırıyorsa `true` |
| `Cors__AllowedOrigins__0` | Environment | Kesin frontend origin'i, ör. `https://app.example.com` |
| `Cors__AllowCredentials` | Environment | Cookie authentication yoksa `false` |
| `DemoSeed__Enabled` | Environment | `false` |
| `Swagger__Enabled` | Environment | `false` |

Connection string'e provider'ın istediği TLS parametrelerini ekleyin. Provider CA doğrulamasını destekliyorsa `SSL Mode=VerifyFull`; desteklenen güvenli bağlantı biçimini ve sertifika zincirini provider dokümanına göre ayarlayın. `Maximum Pool Size=<instance-budget>` değeriyle her instance'ın havuzunu sınırlandırın ve `(instance sayısı × pool sınırı) + migration/admin rezervi` toplamını managed database connection limitinin altında tutun. Runtime connection string'i migrator veya admin kullanıcısına ait olmamalıdır. Birden fazla frontend origin'i gerekiyorsa `Cors__AllowedOrigins__1`, `__2` şeklinde devam edin; wildcard kullanmayın.

## Build ve kontrollü migration

Release image'i repository kökünde oluşturun. Image, runtime uygulamasına ek olarak kontrollü pre-deploy adımında kullanılan self-contained `querypilot-migrate` EF bundle'ını içerir:

```powershell
docker build --tag querypilot-api:<commit-sha> .
```

Migration'ı her uygulama instance'ının başlangıcında çalıştırmayın. Render Blueprint aşağıdaki pre-deploy command'i tanımlar:

```powershell
ConnectionStrings__DefaultConnection="$MIGRATION_CONNECTION_STRING" ./querypilot-migrate
```

`MIGRATION_CONNECTION_STRING` yalnız `querypilot_migrator`, `ConnectionStrings__DefaultConnection` yalnız `querypilot_app` değeridir. İki değer de Render secret olarak (`sync: false`) istenir ve source control'e yazılmaz. Pre-deploy command migrator secret'ını process argument'ine koymadan yalnız migration process'inin environment'ına aktarır. Migration başarısızsa Render yeni release'i yayınlamaz. Runtime uygulaması migration secret'ını okumaz ve startup sırasında migration çalıştırmaz.

## Render Blueprint kurulumu

1. Render Dashboard'da **New > Blueprint** ile GitHub repository'sini ve kökteki `render.yaml` dosyasını seçin.
2. Secret prompt'larında runtime ve migrator için farklı Npgsql connection string'leri, kesin HTTPS frontend origin'i ve Gemini API key'i girin.
3. Runtime string'inde `Username=querypilot_app;SSL Mode=Require;Maximum Pool Size=20`, migration string'inde `Username=querypilot_migrator;SSL Mode=Require;Maximum Pool Size=5` kullanın. Host, port, database ve parolaları Render database detaylarından alın; loglara veya commit'e kopyalamayın.
4. İlk deploy öncesinde Render Shell/psql üzerinden yukarıdaki `querypilot_app` rol ve grant SQL'ini uygulayın. Blueprint'in oluşturduğu `querypilot_migrator` rolü database owner'dır.
5. Blueprint sync ve ilk deploy'u başlatın. CI check'leri geçmeden otomatik production deploy yapılmaz.

## HTTPS, health ve log kontrolü

Host public endpoint'i HTTPS üzerinden yayınlamalı, HTTP isteklerini HTTPS'e yönlendirmeli ve container'ın `8080` portuna proxy etmelidir. Readiness kontrolü `GET /api/health` endpoint'idir; database erişilemezse endpoint `503` döndürür.

Deployment sonrasında:

1. Public URL'nin `https://` ile açıldığını ve HTTP'nin HTTPS'e yönlendiğini doğrulayın.
2. `/api/health` için `200` ve geçerli frontend origin'inden bir API çağrısı için doğru CORS header'ını doğrulayın.
3. `/swagger` endpoint'inin Production'da `404` olduğunu doğrulayın.
4. Demo tablolarının otomatik doldurulmadığını doğrulayın.
5. Build, migration ve runtime loglarında connection string, database parolası, `AI__ApiKey`, Authorization header, kullanıcı sorusu veya AI prompt'u bulunmadığını arayın.
6. Backup restore prosedürünü ayrı, geçici bir database'e en az bir kez test edin.

Demo gerekiyorsa production database'ini seed etmeyin. Production startup'ı `DemoSeed__Enabled=true` değerini reddeder. Ayrı bir demo environment/database oluşturun, `ASPNETCORE_ENVIRONMENT=Development` ve `DemoSeed__Enabled=true` değerlerini yalnız o izole ortamda kullanın.
