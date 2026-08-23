# CV Demo - Database değişince analytics ve AI cevabı güncellenir

## Amaç

QueryPilot'ın ürün sıralamasını veya satış sayılarını hazır veriden ya da AI tahmininden almadığını; her istekte PostgreSQL'deki güncel tamamlanmış siparişlerden hesapladığını kısa bir before/after akışıyla göstermek.

## Demo akışı

1. Ocak 2042 tarih aralığında Top Products, Sales Summary ve Category Performance endpointlerini çalıştır.
2. Aynı dönem için AI endpointine “2042 Ocak ayında en çok satan 5 ürün hangileri?” sorusunu gönder.
3. Başlangıç sonucunu göster:
   - Baseline Product: 10 adet, 1.000 revenue ve ilk sıra.
   - Growth Product: 2 adet, 400 revenue.
   - Toplam revenue: 1.400.
   - Growth kategorisinin revenue payı: `400 / 1.400 × 100`, yaklaşık %28,57.
4. Aktif Growth Product için iki yeni completed sipariş oluştur. Her sipariş 10 adet × 200 fiyatla 2.000 revenue üretir.
5. Aynı analytics endpointlerini aynı tarih aralığıyla yeniden çağır:
   - Growth Product toplam 22 adet ve 4.400 revenue ile ilk sıraya yükselir.
   - Toplam revenue 1.400'den 5.400'e çıkar; artış iki yeni siparişin toplamı olan tam 4.000'dir.
   - Growth kategorisinin revenue değeri 400'den 4.400'e, payı `4.400 / 5.400 × 100` ile yaklaşık %81,48'e çıkar.
6. Aynı AI sorusunu tekrar gönder. Structured data ve açıklama artık eski 10 adetlik lideri değil, PostgreSQL'den gelen yeni 22 adetlik lideri göstermelidir.
7. Aynı döneme 1.000 adetlik canceled sipariş ekle ve olmayan ürünle invalid order isteği gönder. Analytics sonuçlarının 5.400 revenue ve 22 adetlik lider seviyesinde değişmeden kaldığını göster.

## Teknik kanıt

Akış `DynamicAnalyticsDemoTests.New_completed_orders_change_live_analytics_and_ai_explanation` integration testiyle disposable PostgreSQL database üzerinde otomatik doğrulanır. Ürün ve kategori adları her koşuda GUID ile üretilir; production kod belirli bir lider ürün bilemez. Test beklenen yeni revenue ve quantity değerlerini sabit sonuçtan değil, database'e eklediği order satırlarının toplamından `GrowthDelta` olarak hesaplar. Aynı HTTP endpointlerini before/after çağırır, fake AI provider'a gönderilen iki analytics JSON'unun farklı olduğunu kontrol eder ve ikinci açıklamanın güncel lider değerini kullandığını kanıtlar.

```powershell
dotnet test tests/QueryPilot.Api.IntegrationTests/QueryPilot.Api.IntegrationTests.csproj `
  --filter "FullyQualifiedName~DynamicAnalyticsDemoTests"
```

Test verisi development veya production database'e yazılmaz; her çalıştırmada benzersiz `querypilot_test_<guid>` database kullanılır ve test sonunda kaldırılır.
