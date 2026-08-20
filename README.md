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
        |   |-- AppDbContext.cs        (ADIM 5'te eklenecek)
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

## Namespace kuralı

C# namespace'leri proje kökü `QueryPilot.Api` ile başlar ve dosyanın klasör yolunu izler. Örneğin:

- `Features/Categories/Category.cs` -> `QueryPilot.Api.Features.Categories`
- `Data/Configurations/CategoryConfiguration.cs` -> `QueryPilot.Api.Data.Configurations`
- `Common/Exceptions/NotFoundException.cs` -> `QueryPilot.Api.Common.Exceptions`
- `Configuration/AiOptions.cs` -> `QueryPilot.Api.Configuration`

Bu README geliştirme ilerledikçe kurulum, çalıştırma, migration, test ve API kullanım bilgileriyle genişletilecektir.
