# QueryPilot Database Modeli

Bu belge ADIM 6 kapsamındaki veri modeli kararlarını tanımlar. Entity sınıfları ve EF Core configuration dosyaları sonraki adımlarda bu sözleşmeye göre oluşturulacaktır.

## Entity ve ilişki diyagramı

```mermaid
erDiagram
    CATEGORY ||--o{ PRODUCT : contains
    CUSTOMER ||--o{ ORDER : places
    ORDER ||--|{ ORDER_ITEM : contains
    PRODUCT ||--o{ ORDER_ITEM : appears_in
    ORDER_ITEM ||--o{ RETURN : may_have

    CATEGORY {
        identifier Id PK
        string Name UK
        boolean IsActive
        datetime CreatedAt
    }

    PRODUCT {
        identifier Id PK
        string Name
        string SKU UK
        identifier CategoryId FK
        decimal UnitPrice
        boolean IsActive
        datetime CreatedAt
    }

    CUSTOMER {
        identifier Id PK
        string Name
        string Email UK
        string City
        boolean IsActive
        datetime CreatedAt
    }

    ORDER {
        identifier Id PK
        identifier CustomerId FK
        datetime OrderDate
        enum Status
        decimal TotalAmount
        datetime CreatedAt
    }

    ORDER_ITEM {
        identifier Id PK
        identifier OrderId FK
        identifier ProductId FK
        integer Quantity
        decimal UnitPrice
        decimal LineTotal
    }

    RETURN {
        identifier Id PK
        identifier OrderItemId FK
        integer Quantity
        string Reason
        datetime ReturnDate
        decimal Amount
    }
```

## Zorunlu alanlar

| Entity | Zorunlu alanlar | Temel sorumluluk |
| --- | --- | --- |
| `Category` | `Id`, `Name`, `IsActive`, `CreatedAt` | Ürünleri gruplar. `Name` benzersizdir. |
| `Product` | `Id`, `Name`, `SKU`, `CategoryId`, `UnitPrice`, `IsActive`, `CreatedAt` | Satılabilir ürünü ve güncel liste fiyatını tutar. `SKU` benzersizdir. |
| `Customer` | `Id`, `Name`, `Email`, `City`, `IsActive`, `CreatedAt` | Sipariş sahibini tutar. Normalize edilmiş email benzersizdir. |
| `Order` | `Id`, `CustomerId`, `OrderDate`, `Status`, `TotalAmount`, `CreatedAt` | Sipariş başlığını ve backend tarafından hesaplanan toplamı tutar. |
| `OrderItem` | `Id`, `OrderId`, `ProductId`, `Quantity`, `UnitPrice`, `LineTotal` | Satış miktarını ve satış anındaki fiyat snapshot'ını tutar. |
| `Return` | `Id`, `OrderItemId`, `Quantity`, `Reason`, `ReturnDate`, `Amount` | Bir sipariş satırının kısmi veya tam iadesini tutar. |

`Id` alanları kayıt kimliğidir ve database tarafından üretilir. Kesin kimlik tipi entity implementasyonu sırasında bütün model için tek ve tutarlı biçimde seçilecektir.

## İlişki kararları

- Bir `Category`, sıfır veya daha fazla `Product` içerir; her `Product` tam olarak bir `Category`ye bağlıdır.
- Bir `Customer`, sıfır veya daha fazla `Order` verebilir; her `Order` tam olarak bir `Customer`a bağlıdır.
- Bir `Order`, en az bir `OrderItem` içerir; her `OrderItem` tam olarak bir `Order`a bağlıdır.
- Bir `Product`, sıfır veya daha fazla `OrderItem` içinde yer alabilir; her `OrderItem` tam olarak bir `Product`a bağlıdır.
- Bir `OrderItem`, sıfır veya daha fazla `Return` kaydına sahip olabilir. Birden fazla kayıt kısmi iadeleri destekler; her `Return` tam olarak bir `OrderItem`a bağlıdır.

## Para kuralları

- `Product.UnitPrice`, `OrderItem.UnitPrice`, `OrderItem.LineTotal`, `Order.TotalAmount` ve `Return.Amount` C# tarafında `decimal`, PostgreSQL tarafında uygun `numeric` tipiyle tutulacaktır.
- Finansal alanlarda `float` veya `double` kullanılmayacaktır.
- `OrderItem.UnitPrice`, satış anındaki `Product.UnitPrice` değerinin snapshot'ıdır ve ürün fiyatı sonradan değişse bile değişmez.
- `OrderItem.LineTotal = Quantity x UnitPrice` ve `Order.TotalAmount`, sipariş satır toplamlarının toplamıdır.
- `Return.Amount`, güncel ürün fiyatından değil `OrderItem.UnitPrice` snapshot'ından hesaplanır.
- Kesin precision ve scale değerleri EF Core entity configuration aşamasında bütün para alanları için tutarlı biçimde belirlenecektir.

## UTC tarih kuralı

- `CreatedAt`, `OrderDate` ve `ReturnDate` dahil bütün tarihler UTC olarak saklanacaktır.
- Backend yeni tarihleri UTC saat kaynağından üretecek; yerel saat database'e yazılmayacaktır.
- API tarihleri ISO 8601 UTC biçiminde döndürecektir.

## Silme ve tarihsel kayıt politikası

- `Order`, `OrderItem` ve `Return` finansal/tarihsel kayıtlardır; hiçbir zaman hard delete edilmeyecektir.
- `Category`, `Product` ve `Customer` için normal kaldırma davranışı `IsActive = false` ile pasifleştirmedir.
- Sipariş geçmişinde kullanılan kategori, ürün veya müşteri kayıtları hard delete edilmeyecektir.
- Finansal kayıtları kaybettirecek cascade delete davranışı kullanılmayacak; foreign key silme davranışları EF Core configuration aşamasında kısıtlayıcı olarak tanımlanacaktır.
- İptal edilen siparişler silinmek yerine `Status` üzerinden korunacaktır.
