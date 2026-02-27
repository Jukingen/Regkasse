# API Contract

## Routing
- Base: /api/[controller]
- JSON request/response
- Auth çoğunlukla Bearer token ([Authorize])

## Response & Error yaklaşımı
- Başarılı: Ok(...) / Created(...) gibi standart ASP.NET Core sonuçları
- Hata: Mevcut controller'ların döndüğü error formatını koru (AI varsayım yapmasın)

## Örnek Controller Pattern
- Controller: ince
- Service: iş mantığı
- DB: EF Core (AppDbContext)

## Yeni Endpoint Ekleme Kuralları
- Mevcut controller naming ve route düzenini koru
- DTO gerekiyorsa `backend/DTOs` altında konumlandır (namespace: KasseAPI_Final.DTOs)
- Authorization gereksinimlerini açıkça ekle
