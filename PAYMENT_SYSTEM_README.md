# 💳 **ÖDEME SİSTEMİ ENTEGRASYONU - README**

## 🎯 **GENEL BAKIŞ**

Bu dokümanda, Expo frontend'de backend Payment API'ları ile entegre çalışan ödeme sistemi ve sipariş yönetimi açıklanmaktadır.

## 🏗️ **MİMARİ YAPISI**

### **Backend (C# .NET)**
- ✅ `PaymentController` - Ödeme işlemleri
- ✅ `IPaymentService` - Ödeme servis interface'i
- ✅ `PaymentService` - Ödeme işlem implementasyonu
- ✅ `PaymentDTOs` - Ödeme veri modelleri

### **Frontend (React Native/Expo)**
- ✅ `PaymentService` - Backend API entegrasyonu
- ✅ `usePayment` - Ödeme işlemleri hook'u
- ✅ `PaymentModal` - Ödeme alma modal'ı
- ✅ `useOrders` - Sipariş yönetimi hook'u
- ✅ `OrderManagement` - Sipariş yönetimi component'i

## 🔌 **API ENDPOINT'LERİ**

### **Ödeme İşlemleri**
```http
GET    /api/Payment/methods          # Ödeme yöntemlerini getir
POST   /api/Payment                  # Yeni ödeme oluştur
GET    /api/Payment/{id}            # Ödeme detayları
POST   /api/Payment/{id}/cancel     # Ödeme iptal
POST   /api/Payment/{id}/refund     # Ödeme iade
GET    /api/Payment/statistics      # Ödeme istatistikleri
POST   /api/Payment/{id}/tse-signature # TSE imzası
```

### **Sepet/Sipariş İşlemleri**
```http
GET    /api/Cart/current            # Aktif sepeti getir
POST   /api/Cart                    # Yeni sepet oluştur
POST   /api/Cart/{id}/items        # Sepete ürün ekle
PUT    /api/Cart/{id}/items/{itemId} # Ürün miktarını güncelle
DELETE /api/Cart/{id}/items/{itemId} # Ürünü sepetten çıkar
DELETE /api/Cart/{id}              # Sepeti temizle
```

## 📱 **FRONTEND KULLANIMI**

### **1. Ödeme Hook'u Kullanımı**
```typescript
import { usePayment } from '../hooks/usePayment';

const { 
  loading, 
  error, 
  processPayment, 
  createReceipt 
} = usePayment();

// Ödeme işlemi
const handlePayment = async () => {
  const response = await processPayment(paymentRequest);
  if (response.success) {
    // Ödeme başarılı
  }
};
```

### **2. Sipariş Hook'u Kullanımı**
```typescript
import { useOrders } from '../hooks/useOrders';

const { 
  loading, 
  error, 
  createOrder, 
  addItemToOrder 
} = useOrders();

// Yeni sipariş oluştur
const order = await createOrder(tableNumber, customerId);
```

### **3. PaymentModal Kullanımı**
```typescript
import PaymentModal from '../components/PaymentModal';

<PaymentModal
  visible={paymentModalVisible}
  onClose={() => setPaymentModalVisible(false)}
  onSuccess={handlePaymentSuccess}
  cartItems={cartItems}
  customerId={customerId}
  tableNumber={tableNumber}
/>
```

## 🔄 **VERİ AKIŞI**

### **Ödeme Süreci**
1. **Sepet Oluşturma** → `useOrders.createOrder()`
2. **Ürün Ekleme** → `useOrders.addItemToOrder()`
3. **Ödeme Modal Açma** → `PaymentModal` component'i
4. **Ödeme İşlemi** → `usePayment.processPayment()`
5. **TSE İmzası** → `usePayment.createReceipt()`
6. **Başarılı Ödeme** → Sepet temizleme ve masa sıfırlama

### **Veri Modelleri**
```typescript
// Ödeme Request
interface PaymentRequest {
  customerId: string;
  items: PaymentItem[];
  payment: {
    method: 'cash' | 'card' | 'voucher';
    tseRequired: boolean;
    amount?: number;
  };
  notes?: string;
}

// Ödeme Response
interface PaymentResponse {
  success: boolean;
  paymentId: string;
  error?: string;
  message?: string;
  tseSignature?: string;
}
```

## 🚀 **KURULUM VE TEST**

### **1. Backend Çalıştırma**
```bash
cd backend
dotnet run
```

### **2. Frontend Çalıştırma**
```bash
cd frontend
npm start
# veya
expo start
```

### **3. Test Senaryoları**
- ✅ Masa seçimi ve sepet oluşturma
- ✅ Ürün ekleme/çıkarma
- ✅ Ödeme modal açma
- ✅ Nakit/kart ödeme işlemi
- ✅ TSE imzası oluşturma
- ✅ Başarılı ödeme sonrası masa temizleme

## 🔧 **HATA GİDERME**

### **Yaygın Hatalar**
1. **"User not authenticated"** → Auth token kontrol edin
2. **"Customer ID required"** → Müşteri ID set edin
3. **"Cart is empty"** → Sepete ürün ekleyin
4. **"TSE connection failed"** → TSE cihaz bağlantısını kontrol edin

### **Debug Logları**
```typescript
// Console'da detaylı loglar
console.log('Payment request:', paymentRequest);
console.log('Payment response:', response);
console.log('TSE signature:', tseSignature);
```

## 📋 **AVUSTURYA UYUMLULUK GEREKSİNİMLERİ**

### **RKSV & DSGVO Uyumluluğu**
- ✅ TSE imzası zorunlu
- ✅ Müşteri verisi 7 yıl saklama
- ✅ FinanzOnline entegrasyonu
- ✅ Günlük rapor (Tagesabschluss)
- ✅ Ödeme iptal/iade kayıtları

### **TSE Cihaz Gereksinimleri**
- ✅ EPSON-TSE, fiskaly destek
- ✅ USB bağlantı (VID_04B8&PID_0E15)
- ✅ 30 saniye timeout
- ✅ Offline mod desteği

## 🔮 **GELECEK GELİŞTİRMELER**

### **Planlanan Özellikler**
- [ ] Çoklu dil desteği (de-DE, en, tr)
- [ ] Offline ödeme senkronizasyonu
- [ ] Gelişmiş raporlama
- [ ] Mobil ödeme entegrasyonu
- [ ] Barcode/QR kod desteği

### **Performans İyileştirmeleri**
- [ ] Ödeme işlem cache'leme
- [ ] Lazy loading
- [ ] Optimistic updates
- [ ] Background sync

## 📞 **DESTEK**

### **Teknik Destek**
- Backend: C# .NET Core
- Frontend: React Native/Expo
- Database: PostgreSQL
- API: RESTful HTTP

### **İletişim**
- Geliştirici: AI Assistant
- Proje: Registrierkasse
- Versiyon: 2.1
- Tarih: 2025

---

**Not**: Bu sistem Avusturya yasalarına (RKSV) ve veri koruma düzenlemelerine (DSGVO) uygun olarak tasarlanmıştır.
