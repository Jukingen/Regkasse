# Regkasse AI Context Pack

Bu klasör, AI'nin projeyi doğru anlaması ve aynı kod stilinde geliştirme yapması için "tek doğruluk kaynağıdır".

## Project Summary
- Repo: Regkasse / KasseAPI_Final
- Backend: ASP.NET Core (Controller-based), IdentityDbContext<ApplicationUser>, EF Core Fluent API
- DB: PostgreSQL (decimal(18,2) yaygın, jsonb alanlar var)
- Frontend: React Native + Expo Router (app/(tabs), app/(auth)), POS ana ekranı: cash-register.tsx
- FE API yaklaşımı: services/api/* (örn: productService, config)

## Golden Rules (AI için)
1) Mevcut Controller/Service pattern'ini bozma.
2) EF Core model mapping tahmin etme; AppDbContext'teki yaklaşımı kopyala.
3) Para (decimal) ve rounding konularında varsayım yapma; policy dokümanlarına uy.
4) TSE / FinanzOnline / DailyClosing gibi regülasyon alanlarında "sadece isteneni" değiştir.
5) Emin olmadığın yerde önce “Assumptions” yaz ve soru sor.
