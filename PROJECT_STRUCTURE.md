# Registrierkasse - Proje Dizin Yapısı

## 📁 Ana Dizin Yapısı

```
Registrierkasse/
├── backend/                    # ASP.NET Core Backend
│   └── Registrierkasse_API/
├── frontend/                   # Kasiyer Frontend (gelecek)
├── frontend-admin/             # Yönetim Frontend
├── DEVELOPMENT_LOG.md          # Geliştirme günlüğü
├── PROJECT_STRUCTURE.md        # Bu dosya
└── README.md                   # Proje açıklaması
```

## 🏗️ Backend Dizin Yapısı

```
backend/Registrierkasse_API/
├── Controllers/                # API Controllers
│   ├── AuthController.cs       # Authentication endpoints
│   ├── ProductsController.cs   # Ürün yönetimi
│   ├── CustomersController.cs  # Müşteri yönetimi
│   ├── InvoicesController.cs   # Fatura yönetimi
│   ├── CashRegistersController.cs # Kasa yönetimi
│   └── DashboardController.cs  # Dashboard verileri
├── Data/                       # Entity Framework
│   ├── AppDbContext.cs         # Database context
│   ├── SeedData.cs             # Demo veriler
│   └── Migrations/             # Database migrations
│       ├── 20240611_InitialCreate.cs
│       └── ...
├── Models/                     # Entity Models
│   ├── ApplicationUser.cs      # Kullanıcı modeli
│   ├── Product.cs              # Ürün modeli
│   ├── Customer.cs             # Müşteri modeli
│   ├── Invoice.cs              # Fatura modeli
│   ├── InvoiceItem.cs          # Fatura kalemi
│   ├── CashRegister.cs         # Kasa modeli
│   ├── CompanySettings.cs      # Şirket ayarları
│   ├── AuditLog.cs             # Audit log
│   └── SystemConfiguration.cs  # Sistem konfigürasyonu
├── Services/                   # Business Logic
│   ├── AuthService.cs          # Authentication service
│   ├── TseService.cs           # TSE entegrasyonu
│   └── InvoiceService.cs       # Fatura işlemleri
├── DTOs/                       # Data Transfer Objects
│   ├── LoginDto.cs
│   ├── ProductDto.cs
│   └── InvoiceDto.cs
├── Program.cs                  # Startup configuration
├── appsettings.json            # Konfigürasyon
├── appsettings.Development.json
└── Registrierkasse_API.csproj  # Proje dosyası
```

## 🎨 Frontend-Admin Dizin Yapısı

```
frontend-admin/
├── src/
│   ├── components/             # React Components
│   │   ├── Layout/
│   │   │   ├── Sidebar.tsx
│   │   │   ├── Header.tsx
│   │   │   └── Layout.tsx
│   │   ├── Dashboard/
│   │   │   ├── StatsCard.tsx
│   │   │   ├── RecentInvoices.tsx
│   │   │   └── SalesChart.tsx
│   │   ├── Products/
│   │   │   ├── ProductList.tsx
│   │   │   ├── ProductForm.tsx
│   │   │   └── ProductCard.tsx
│   │   ├── Customers/
│   │   │   ├── CustomerList.tsx
│   │   │   ├── CustomerForm.tsx
│   │   │   └── CustomerCard.tsx
│   │   ├── Invoices/
│   │   │   ├── InvoiceList.tsx
│   │   │   ├── InvoiceForm.tsx
│   │   │   └── InvoiceDetail.tsx
│   │   └── Common/
│   │       ├── LoadingSpinner.tsx
│   │       ├── ErrorMessage.tsx
│   │       └── ConfirmDialog.tsx
│   ├── pages/                  # Sayfa Bileşenleri
│   │   ├── Dashboard.tsx       # Ana dashboard
│   │   ├── Products.tsx        # Ürün yönetimi
│   │   ├── Customers.tsx       # Müşteri yönetimi
│   │   ├── Invoices.tsx        # Fatura yönetimi
│   │   ├── Settings.tsx        # Ayarlar
│   │   └── Login.tsx           # Giriş sayfası
│   ├── services/               # API Services
│   │   ├── api.ts              # Axios configuration
│   │   ├── authService.ts      # Authentication
│   │   ├── productService.ts   # Ürün API
│   │   ├── customerService.ts  # Müşteri API
│   │   └── invoiceService.ts   # Fatura API
│   ├── types/                  # TypeScript Types
│   │   ├── auth.ts
│   │   ├── product.ts
│   │   ├── customer.ts
│   │   ├── invoice.ts
│   │   └── common.ts
│   ├── hooks/                  # Custom Hooks
│   │   ├── useAuth.ts
│   │   ├── useApi.ts
│   │   └── useLocalStorage.ts
│   ├── utils/                  # Utility Functions
│   │   ├── constants.ts
│   │   ├── helpers.ts
│   │   └── validators.ts
│   ├── styles/                 # CSS/SCSS Files
│   │   ├── globals.css
│   │   ├── components.css
│   │   └── variables.css
│   ├── App.tsx                 # Ana uygulama
│   ├── main.tsx                # Entry point
│   └── vite-env.d.ts           # Vite types
├── public/                     # Static Files
│   ├── index.html
│   ├── favicon.ico
│   └── images/
├── package.json                # Dependencies
├── package-lock.json
├── vite.config.ts              # Vite configuration
├── tsconfig.json               # TypeScript config
├── .env                        # Environment variables
├── .env.local
└── .gitignore
```

## 🔧 Konfigürasyon Dosyaları

### Backend Konfigürasyonu
```json
// appsettings.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=Registrierkasse;Username=postgres;Password=password"
  },
  "JwtSettings": {
    "SecretKey": "your-secret-key-here",
    "Issuer": "Registrierkasse",
    "Audience": "RegistrierkasseUsers",
    "ExpirationHours": 24
  }
}
```

### Frontend Konfigürasyonu
```typescript
// vite.config.ts
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5183',
        changeOrigin: true
      }
    }
  }
})
```

## 📊 Veritabanı Şeması

### Ana Tablolar
- **AspNetUsers** - Kullanıcılar
- **AspNetRoles** - Roller
- **Products** - Ürünler
- **Customers** - Müşteriler
- **Invoices** - Faturalar
- **InvoiceItems** - Fatura kalemleri
- **CashRegisters** - Kasalar
- **CompanySettings** - Şirket ayarları
- **AuditLogs** - Audit logları
- **SystemConfigurations** - Sistem konfigürasyonu

### İlişkiler
- Users ↔ Roles (Many-to-Many)
- Invoices ↔ InvoiceItems (One-to-Many)
- Invoices ↔ Customers (Many-to-One)
- Invoices ↔ CashRegisters (Many-to-One)
- InvoiceItems ↔ Products (Many-to-One)

## 🚀 Çalıştırma Komutları

### Backend
```bash
cd backend/Registrierkasse_API
dotnet restore
dotnet ef database update
dotnet run
```

### Frontend-Admin
```bash
cd frontend-admin
npm install
npm run dev
```

## 📝 Önemli Dosyalar

### Backend
- `Program.cs` - Uygulama başlangıç noktası
- `AppDbContext.cs` - Veritabanı context
- `SeedData.cs` - Demo veriler
- `Controllers/` - API endpointleri

### Frontend
- `src/App.tsx` - Ana uygulama bileşeni
- `src/services/api.ts` - API konfigürasyonu
- `src/pages/` - Sayfa bileşenleri
- `src/components/` - Yeniden kullanılabilir bileşenler

## 🔍 Test Dosyaları

### Backend Tests
```
backend/Registrierkasse_API.Tests/
├── Controllers/
├── Services/
└── Data/
```

### Frontend Tests
```
frontend-admin/src/
├── __tests__/
├── components/
└── pages/
```

---
**Son Güncelleme**: 11 Haziran 2024  
**Proje Durumu**: ✅ Temel Yapı Tamamlandı 