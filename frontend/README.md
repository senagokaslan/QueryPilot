# QueryPilot Frontend

QueryPilot ASP.NET Core API'sine bağlanan React, TypeScript, Vite ve Tailwind CSS tabanlı responsive BI dashboard.

## Gereksinimler

- Node.js 22.13 veya üzeri
- `http://localhost:5199` adresinde çalışan QueryPilot backend

## Kurulum ve çalıştırma

```powershell
cd frontend
Copy-Item .env.example .env.local
npm install
npm run dev
```

Tarayıcı adresi: `http://localhost:5173`

Backend farklı bir adreste çalışıyorsa `.env.local` içindeki değeri güncelleyin:

```text
VITE_API_BASE_URL=http://localhost:5199
```

## Kontroller

```powershell
npm run lint
npm run test
npm run build
```

## Ekranlar

- Genel bakış dashboard'u
- Ayrıntılı satış, ürün, kategori ve iade analytics ekranı
- Kategori, ürün ve müşteri yönetimi
- Sipariş listeleme, oluşturma ve detay görüntüleme
- Tamamlanmış sipariş kaleminden iade oluşturma
- Doğal dil QueryPilot AI sorgu ekranı

## Backend kaynaklı sınırlar

- Yeni siparişler `Pending` oluşturulur. Mevcut API'de sipariş durumunu değiştiren endpoint yoktur.
- Analytics yalnızca `Completed` siparişleri kullanır.
- İadeler yalnızca tamamlanmış siparişler için oluşturulabilir.
- İade listeleme endpointi yoktur; iade bilgileri sipariş detayındaki kalem özetlerinden görülür.
- Authentication/authorization henüz backend'de bulunmaz.
