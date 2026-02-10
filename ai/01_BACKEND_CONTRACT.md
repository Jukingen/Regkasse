# Backend Contract (ASP.NET Core)

## Mimari
- API: Controller-based (Minimal API değil)
- Servis Katmanı: İş mantığı çoğunlukla service layer'da (örn: IPaymentService)
- Data Access: EF Core + Fluent API (AppDbContext)
- Identity: AppDbContext, IdentityDbContext<ApplicationUser>

## Controller Standards
- Route: [Route("api/[controller]")]
- [ApiController] kullanılır
- Çoğu endpoint [Authorize] ile korunur (AuthController hariç)
- Controller ince olmalı: doğrulama + servis çağrısı + response

## Logging
- Bazı controller'lar BaseController üzerinden ILogger alır (örn: PaymentController : BaseController)
- Yeni controller eklerken mevcut BaseController kullanımını takip et.

## Validation
- Basit guard/validation Controller veya Service katmanında
- Yeni endpoint yazarken: 
  - Request null/empty kontrol
  - Domain kurallarını service tarafında uygula

## Transaction / Money
- Ödeme, fiş, closing gibi akışlar transaction-heavy.
- Money alanları: decimal ve DB’de decimal(18,2) (bazı oranlar decimal(5,4)/(5,2))
- Rounding politikasını değiştirme; sadece mevcut davranışı koru.

## Dosya yerleşimi (mevcut örnekleri takip et)
- Controllers: KasseAPI_Final.Controllers
- Services: KasseAPI_Final.Services
- DTOs: KasseAPI_Final.DTOs (varsa)
- Data: KasseAPI_Final.Data (AppDbContext)

## Yeni endpoint ekleme checklist
1) Controller method + route
2) Request/Response DTO (gerekirse)
3) Service interface + implementation
4) EF model / migration gerekiyorsa AppDbContext + migration
5) Auth/role kontrolü
6) Log & error response formatı
