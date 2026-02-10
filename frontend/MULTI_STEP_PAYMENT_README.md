# Multi-Step Ödeme Sistemi

## Genel Bakış

Bu sistem, Avusturya RKSV uyumluluğu sağlayan, kullanıcı dostu bir multi-step ödeme deneyimi sunar. Her adımda ilgili API endpoint'ine istek atarak güvenli ve doğrulanmış ödeme işlemleri gerçekleştirir.

## Özellikler

### 🔄 6 Adımlı Ödeme Süreci
1. **Müşteri Seçimi** - 8 haneli müşteri ID doğrulama
2. **Ödeme Yöntemi** - Nakit, kredi kartı, kupon seçimi
3. **Ödeme Tutarı** - Müşterinin ödediği tutar girişi
4. **TSE Doğrulama** - RKSV uyumlu TSE imza doğrulama
5. **Onay** - Ödeme detaylarının son kontrolü
6. **Fiş** - Başarılı ödeme sonrası fiş oluşturma

### 🛡️ Güvenlik ve Uyumluluk
- **RKSV Uyumlu**: Avusturya kasa sistemi yasal gereklilikleri
- **TSE Entegrasyonu**: Güvenli elektronik imza sistemi
- **Müşteri Doğrulama**: 8 haneli zorunlu müşteri ID
- **Steuernummer**: ATU formatında vergi numarası desteği

### 📱 Kullanıcı Deneyimi
- **Progress Bar**: Mevcut adım gösterimi
- **Responsive Design**: Tüm ekran boyutlarına uyum
- **Error Handling**: Kapsamlı hata yönetimi
- **Loading States**: İşlem durumu göstergeleri

## Kullanım

### Temel Kullanım

```tsx
import MultiStepPaymentScreen from './components/MultiStepPaymentScreen';

const PaymentComponent = () => {
  const handlePaymentComplete = (receipt) => {
    console.log('Ödeme tamamlandı:', receipt);
  };

  const handlePaymentCancel = () => {
    console.log('Ödeme iptal edildi');
  };

  return (
    <MultiStepPaymentScreen
      totalAmount={39.75}
      cartItems={cartItems}
      onComplete={handlePaymentComplete}
      onCancel={handlePaymentCancel}
    />
  );
};
```

### Demo Bileşeni

```tsx
import MultiStepPaymentDemo from './components/MultiStepPaymentDemo';

// Demo bileşeni ile test edin
<MultiStepPaymentDemo />
```

## API Entegrasyonu

### Ödeme İşlemi

```typescript
// PaymentRequest interface
interface PaymentRequest {
  items: PaymentItem[];
  payment: {
    method: 'cash' | 'card' | 'voucher';
    amount: number;
    tseRequired: boolean;
  };
  customerId?: string;
}

// API çağrısı
const response = await paymentService.processPayment(paymentRequest);
```

### TSE Doğrulama

```typescript
// TSE doğrulama (RKSV zorunlu)
if (requiresTSE) {
  const tseSignature = await getTSESignature();
  // TSE imzası backend'e gönderilir
}
```

## Stil ve Tema

### Renk Paleti
- **Primary**: #1976d2 (Mavi)
- **Success**: #28a745 (Yeşil)
- **Warning**: #ff9800 (Turuncu)
- **Error**: #d32f2f (Kırmızı)
- **Neutral**: #6c757d (Gri)

### Bileşen Stilleri
- **Border Radius**: 8px (modern görünüm)
- **Shadow**: Subtle elevation efekti
- **Spacing**: 16px grid sistemi
- **Typography**: iOS/Android native fontlar

## Test Etme

### Demo Senaryoları
1. **Başarılı Ödeme**: Tüm adımları tamamlayın
2. **Ödeme İptali**: Herhangi bir adımda iptal edin
3. **Hata Durumları**: Geçersiz veri girişi test edin
4. **TSE Bağlantısı**: TSE cihazı bağlı/bağlı değil durumları

### Test Verileri
```typescript
const testCartItems = [
  {
    id: '1',
    product: {
      id: 'prod-1',
      name: 'Test Ürün',
      price: 15.50,
      taxType: 'Standard'
    },
    quantity: 2,
    unitPrice: 15.50,
    totalAmount: 31.00
  }
];
```

## Geliştirme

### Yeni Ödeme Yöntemi Ekleme

```typescript
const PAYMENT_METHODS = [
  // Mevcut yöntemler...
  { 
    key: 'newMethod', 
    label: 'Yeni Yöntem', 
    icon: 'new-icon', 
    requiresTSE: true 
  }
];
```

### Yeni Adım Ekleme

```typescript
enum PaymentStep {
  // Mevcut adımlar...
  NEW_STEP = 6
}

// renderNewStep fonksiyonu ekleyin
const renderNewStep = () => (
  <View style={styles.stepContainer}>
    {/* Yeni adım içeriği */}
  </View>
);
```

## Hata Yönetimi

### Validation Hataları
- **Müşteri ID**: 8 haneli zorunlu
- **Ödeme Tutarı**: Toplam tutardan az olamaz
- **TSE İmza**: Gerektiğinde zorunlu

### Network Hataları
- **Offline Mode**: PouchDB ile çevrimdışı kayıt
- **Retry Logic**: Otomatik yeniden deneme
- **Error Messages**: Kullanıcı dostu hata mesajları

## Performans

### Optimizasyonlar
- **Lazy Loading**: Gerektiğinde bileşen yükleme
- **Memoization**: Gereksiz re-render'ları önleme
- **Debouncing**: API çağrılarında gecikme
- **Image Optimization**: Lazy image loading

### Bundle Size
- **Tree Shaking**: Kullanılmayan kodları kaldırma
- **Code Splitting**: Route bazlı kod bölme
- **Dynamic Imports**: Gerektiğinde modül yükleme

## Güvenlik

### Veri Koruma
- **Input Validation**: Client-side doğrulama
- **API Security**: Backend güvenlik kontrolleri
- **Sensitive Data**: Hassas veri maskeleme
- **Session Management**: Güvenli oturum yönetimi

### RKSV Uyumluluğu
- **TSE Integration**: Güvenli elektronik imza
- **Audit Trail**: Tüm işlemlerin kaydı
- **Data Retention**: 7 yıl veri saklama
- **Compliance Checks**: Yasal gereklilik kontrolleri

## Destek

### Sorun Giderme
1. **Console Logs**: Detaylı hata mesajları
2. **Network Tab**: API çağrılarını izleme
3. **React DevTools**: Component state kontrolü
4. **Performance Monitor**: Performans metrikleri

### İletişim
- **GitHub Issues**: Bug raporları
- **Documentation**: API dokümantasyonu
- **Support Team**: Teknik destek

## Lisans

Bu proje MIT lisansı altında lisanslanmıştır. Detaylar için LICENSE dosyasına bakın.

---

**Not**: Bu sistem Avusturya RKSV yasal gerekliliklerine uygun olarak tasarlanmıştır. Farklı ülkelerde kullanım için yerel yasal gereklilikleri kontrol edin.
