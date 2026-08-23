# AI query akışı

`POST /api/ai/query` doğal dil sorusunu yorumlar; bütün satış ve iade hesapları backend analytics servisinde kalır.

```mermaid
flowchart TD
    A[POST /api/ai/query] --> B{Request geçerli mi?}
    B -- Hayır --> C[400 validation]
    B -- Evet --> D[AI intent extraction]
    D -- Provider hatası --> E[503 Problem Details]
    D --> F{Intent destekleniyor ve yeterli mi?}
    F -- Desteklenmiyor --> G[Unsupported + örnek sorular]
    F -- Eksik veya belirsiz --> H[NeedsClarification + soru]
    F -- Geçerli --> I[Typed enum routing]
    I --> J[Bir IAnalyticsService metodu]
    J --> K[Structured numeric data]
    K --> L[AI açıklama + grounding kontrolü]
    L --> M[Data + optional chart + açıklama/uyarı]
```

Unsupported ve clarification dalları analytics/database sorgusu çalıştırmaz. Provider açıklama aşamasında kullanılamazsa structured numeric data korunur ve response yalnız güvenli bir uyarı ekler. Direct `/api/analytics/*` endpointleri AI provider'dan bağımsızdır.
