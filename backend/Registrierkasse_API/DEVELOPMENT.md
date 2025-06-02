# KasseAPP Geliştirme Dokümanı

## Güvenlik ve Oturum Yönetimi

### Kimlik Doğrulama (Authentication)
- JWT tabanlı kimlik doğrulama sistemi
- Access Token ve Refresh Token kullanımı
- Token süresi: 1 saat
- Refresh Token süresi: 7 gün
- Otomatik token yenileme
- Periyodik oturum kontrolü (60 saniyede bir)

### Yetkilendirme (Authorization)
- Kullanıcı rolleri:
  - `admin`: Tüm menülere erişim
  - `manager`: Tüm menülere erişim
  - `cashier`: Sadece kasa ve ayarlar menüsüne erişim

### Oturum Yönetimi
- Oturum açma:
  - Kullanıcı adı/şifre doğrulaması
  - JWT token üretimi
  - Refresh token üretimi
  - Rol bazlı yönlendirme
- Oturum kontrolü:
  - Token geçerlilik kontrolü
  - Otomatik token yenileme
  - Oturum sonlandırma
- Oturum kapatma:
  - Token silme
  - Refresh token silme
  - Backend oturum sonlandırma

### Güvenlik Önlemleri
- HTTPS zorunluluğu
- Token güvenliği:
  - AsyncStorage'da şifreli saklama
  - HTTP-only cookie kullanımı
- XSS koruması
- CSRF koruması
- Rate limiting
- Input validasyonu

## API Endpoints

### Auth Endpoints
```typescript
POST /api/auth/login
Request: { username: string, password: string }
Response: { token: string, refreshToken: string, user: User }

POST /api/auth/refresh
Request: { refreshToken: string }
Response: { token: string, refreshToken: string }

POST /api/auth/logout
Request: { token: string }
Response: { success: boolean }

GET /api/auth/me
Request: { token: string }
Response: { user: User }
```

### User Interface
- Login sayfası:
  - Kullanıcı adı/şifre girişi
  - Hata mesajları
  - Yükleme durumu
- Ana sayfa:
  - Rol bazlı menü erişimi
  - Oturum durumu göstergesi
  - Otomatik yönlendirme

## Güvenlik Kontrol Listesi
- [x] JWT implementasyonu
- [x] Refresh token desteği
- [x] Rol bazlı yetkilendirme
- [x] Otomatik token yenileme
- [x] Güvenli token saklama
- [x] Oturum timeout yönetimi
- [x] XSS koruması
- [x] CSRF koruması
- [x] Rate limiting
- [x] Input validasyonu

## Önemli Notlar
1. Tüm API istekleri HTTPS üzerinden yapılmalı
2. Token'lar güvenli şekilde saklanmalı
3. Kullanıcı rolleri backend'de de kontrol edilmeli
4. Oturum timeout'ları dikkatli yönetilmeli
5. Hata mesajları kullanıcı dostu olmalı
6. Loglar güvenli şekilde tutulmalı

## Backend Gereksinimleri
1. JWT token üretimi ve doğrulama
2. Refresh token yönetimi
3. Rol bazlı yetkilendirme
4. Rate limiting
5. Input validasyonu
6. Güvenli şifre saklama
7. Oturum yönetimi
8. Log yönetimi

## Frontend Gereksinimleri
1. Token yönetimi
2. Oturum durumu kontrolü
3. Rol bazlı UI
4. Hata yönetimi
5. Yükleme durumları
6. Yönlendirme mantığı
7. Güvenli depolama
8. API istek yönetimi 