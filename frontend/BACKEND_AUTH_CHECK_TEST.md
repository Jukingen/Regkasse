# 🧪 BACKEND AUTH CHECK TEST REHBERİ

## 📋 **Test Amacı**
F5 refresh'te logout olma sorununun backend auth check ile çözüldüğünü doğrulamak.

## 🔧 **Test Öncesi Hazırlık**

### 1. **Backend Kontrolü**
```bash
# Backend'in çalıştığından emin ol
# http://localhost:5183/api/auth/me endpoint'i erişilebilir olmalı
```

### 2. **Frontend Başlatma**
```bash
cd frontend
npm run web
```

## 🧪 **Test Senaryoları**

### ✅ **Test 1: Normal Login Flow**
1. **Login Sayfasına Git**: `http://localhost:8081/(auth)/login`
2. **Demo Kullanıcı ile Giriş**:
   - Email: `cashier@demo.com`
   - Password: `Cashier123!`
3. **Beklenen Sonuç**:
   - Ana sayfaya yönlendirilmeli
   - Console'da login logları görünmeli
   - AsyncStorage'da token ve user kayıtlı olmalı

### ✅ **Test 2: F5 Refresh Test**
1. **Login Sonrası**: Ana sayfada olduğundan emin ol
2. **F5 Refresh**: Sayfayı yenile
3. **Beklenen Sonuç**:
   - Logout olmamalı
   - Console'da backend auth check logları görünmeli:
     ```
     🔐 Backend auth check başlatılıyor...
     🔐 Backend token validation başlatılıyor...
     ✅ Backend token validation başarılı: cashier@demo.com
     ✅ Backend auth check başarılı: cashier@demo.com
     ✅ User state backend'den güncellendi
     ```
   - Kullanıcı oturumu korunmalı

### ✅ **Test 3: Invalid Token Test**
1. **Login Sonrası**: Ana sayfada olduğundan emin ol
2. **Token'ı Manuel Sil**: Browser DevTools > Application > Storage > AsyncStorage
   - `token` key'ini sil
3. **F5 Refresh**: Sayfayı yenile
4. **Beklenen Sonuç**:
   - Console'da token bulunamadı logları görünmeli:
     ```
     ❌ Token bulunamadı, validation atlanıyor
     ❌ Backend auth check başarısız: user bulunamadı
     ```
   - Kullanıcı logout olmalı
   - Login sayfasına yönlendirilmeli

### ✅ **Test 4: Backend Down Test**
1. **Login Sonrası**: Ana sayfada olduğundan emin ol
2. **Backend'i Durdur**: Backend servisini kapat
3. **F5 Refresh**: Sayfayı yenile
4. **Beklenen Sonuç**:
   - Console'da network error logları görünmeli:
     ```
     ❌ Backend token validation hatası: [Network Error]
     ❌ Backend auth check hatası: [Network Error]
     ```
   - Kullanıcı logout olmalı
   - Login sayfasına yönlendirilmeli

## 🔍 **Console Log Kontrolü**

### **Başarılı Auth Check**
```
🔐 Backend auth check başlatılıyor...
🔐 Backend token validation başlatılıyor...
✅ Backend token validation başarılı: cashier@demo.com
✅ Backend auth check başarılı: cashier@demo.com
✅ User state backend'den güncellendi
```

### **Başarısız Auth Check**
```
🔐 Backend auth check başlatılıyor...
🔐 Backend token validation başlatılıyor...
❌ Token bulunamadı, validation atlanıyor
❌ Backend auth check başarısız: user bulunamadı
```

### **Network Error**
```
🔐 Backend auth check başlatılıyor...
🔐 Backend token validation başlatılıyor...
❌ Backend token validation hatası: [Network Error]
❌ Backend auth check hatası: [Network Error]
```

## 📊 **Test Sonuçları**

| Test Senaryosu | Beklenen Sonuç | Gerçek Sonuç | Durum |
|----------------|----------------|--------------|-------|
| Normal Login | ✅ Başarılı giriş | - | ⏳ Bekliyor |
| F5 Refresh | ✅ Logout olmaz | - | ⏳ Bekliyor |
| Invalid Token | ✅ Logout olur | - | ⏳ Bekliyor |
| Backend Down | ✅ Logout olur | - | ⏳ Bekliyor |

## 🚨 **Bilinen Sorunlar**

1. **Network Latency**: Backend'e bağlantı yavaşsa auth check gecikebilir
2. **Backend Availability**: Backend down ise auth check başarısız olur
3. **Token Expiry**: Token süresi dolmuşsa backend 401 döner

## 🔧 **Hata Ayıklama**

### **Console'da Hata Görürsen**
1. **Network Tab**: API call'ları kontrol et
2. **Storage Tab**: AsyncStorage'da token var mı kontrol et
3. **Console Tab**: Error stack trace'i incele

### **Backend API Hatası**
1. **Status Code**: 401, 500, 404 gibi
2. **Response Body**: Hata mesajını kontrol et
3. **Backend Logs**: Backend console'da hata var mı

## 📝 **Test Raporu**

**Test Tarihi**: _______________
**Test Eden**: _______________
**Backend Versiyon**: _______________
**Frontend Versiyon**: _______________

**Genel Değerlendirme**: _______________

**Öneriler**: _______________

---

**Not**: Bu testler sayesinde F5 refresh logout sorununun çözüldüğünü doğrulayabilirsiniz.
