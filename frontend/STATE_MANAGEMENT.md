# React Native Expo State Yönetim Yapısı

Bu dokümantasyon, Registrierkasse uygulamasında kullanılan state yönetim yapısını açıklar.

## 📋 İçindekiler

1. [Genel Bakış](#genel-bakış)
2. [Hook'lar](#hooklar)
3. [Context'ler](#contextler)
4. [UI Bileşenleri](#ui-bileşenleri)
5. [Kullanım Örnekleri](#kullanım-örnekleri)
6. [Best Practices](#best-practices)

## 🎯 Genel Bakış

Uygulama, modern React Native state yönetim prensiplerini takip eden kapsamlı bir yapı kullanır:

- **useAsyncState**: Async işlemler için state yönetimi
- **useFormState**: Form state yönetimi ve validasyon
- **AppStateContext**: Global uygulama state'i
- **UI Bileşenleri**: Loading, Error, Notification bileşenleri

## 🪝 Hook'lar

### useAsyncState

Async işlemler için genel state yönetim hook'u.

```typescript
const [state, actions] = useAsyncState(
  asyncFunction,
  {
    showErrorAlert: true,
    showSuccessAlert: true,
    successMessage: 'İşlem başarılı!',
    onSuccess: (data) => console.log('Başarılı:', data),
    onError: (error) => console.log('Hata:', error)
  }
);
```

**Özellikler:**
- Loading state yönetimi
- Error handling
- Success state
- Auto-abort önceki istekler
- Alert gösterimi
- Callback fonksiyonları

### useFormState

Form state yönetimi ve validasyon için hook.

```typescript
const [formState, formActions] = useFormState(
  initialValues,
  validationSchema,
  {
    showErrorAlert: true,
    showSuccessAlert: true
  }
);
```

**Özellikler:**
- Form değerleri yönetimi
- Validation
- Error state'leri
- Touch state'leri
- Submit handling
- Reset fonksiyonu

### useProductOperations

Ürün işlemleri için özel hook.

```typescript
const {
  products,      // Ürün listesi state'i
  create,        // Oluşturma state'i
  update,        // Güncelleme state'i
  delete,        // Silme state'i
  createProduct, // Oluşturma fonksiyonu
  updateProduct, // Güncelleme fonksiyonu
  deleteProduct, // Silme fonksiyonu
  refreshProducts // Yenileme fonksiyonu
} = useProductOperations();
```

## 🏗️ Context'ler

### AppStateContext

Global uygulama state yönetimi.

```typescript
const {
  globalLoading,
  globalError,
  globalSuccess,
  notifications,
  showError,
  showSuccess,
  addNotification,
  setGlobalLoading
} = useAppState();
```

**Özellikler:**
- Global loading state
- Global error/success mesajları
- Notification sistemi
- Offline state
- App ready state

## 🎨 UI Bileşenleri

### LoadingOverlay

Tam ekran loading göstergesi.

```typescript
<LoadingOverlay
  visible={isLoading}
  message="Veriler yükleniyor..."
  size="large"
/>
```

### ErrorBoundary

Hata yakalama bileşeni.

```typescript
<ErrorBoundary
  onError={(error, errorInfo) => {
    console.log('Hata yakalandı:', error);
  }}
>
  <YourComponent />
</ErrorBoundary>
```

### NotificationToast

Toast bildirim bileşeni.

```typescript
<NotificationToast
  visible={true}
  type="success"
  title="Başarılı!"
  message="İşlem tamamlandı"
  duration={3000}
  onClose={() => setVisible(false)}
/>
```

## 📝 Kullanım Örnekleri

### 1. Basit API Çağrısı

```typescript
const ProductList = () => {
  const [state, actions] = useAsyncState(
    productService.getProducts,
    {
      showErrorAlert: true,
      onSuccess: (products) => {
        console.log('Ürünler yüklendi:', products.length);
      }
    }
  );

  return (
    <View>
      {state.loading && <LoadingOverlay visible={true} />}
      {state.error && <Text>Hata: {state.error}</Text>}
      {state.data && (
        <FlatList
          data={state.data}
          renderItem={({ item }) => <ProductItem product={item} />}
        />
      )}
      <TouchableOpacity onPress={() => actions.execute()}>
        <Text>Yenile</Text>
      </TouchableOpacity>
    </View>
  );
};
```

### 2. Form Yönetimi

```typescript
const ProductForm = () => {
  const validationSchema = (values: any) => {
    const errors: any = {};
    if (!values.name) errors.name = 'Ürün adı gerekli';
    if (!values.price) errors.price = 'Fiyat gerekli';
    return errors;
  };

  const [formState, formActions] = useFormState(
    { name: '', price: '', description: '' },
    validationSchema
  );

  const handleSubmit = async (values: any) => {
    await productService.createProduct(values);
  };

  return (
    <View>
      <TextInput
        style={formState.touched.name && formState.errors.name ? styles.error : styles.input}
        placeholder="Ürün Adı"
        value={formState.values.name}
        onChangeText={(text) => formActions.setValue('name', text)}
        onBlur={() => formActions.setTouched('name', true)}
      />
      
      <TouchableOpacity
        onPress={() => formActions.submit(handleSubmit)}
        disabled={!formState.isValid || formState.isSubmitting}
      >
        <Text>{formState.isSubmitting ? 'Kaydediliyor...' : 'Kaydet'}</Text>
      </TouchableOpacity>
    </View>
  );
};
```

### 3. Global State Kullanımı

```typescript
const ProductScreen = () => {
  const { showError, showSuccess, addNotification } = useAppState();

  const handleDelete = async (productId: string) => {
    try {
      await productService.deleteProduct(productId);
      showSuccess('Ürün başarıyla silindi');
      addNotification({
        type: 'success',
        title: 'Ürün Silindi',
        message: 'Ürün başarıyla silindi',
        duration: 3000
      });
    } catch (error) {
      showError('Ürün silinirken hata oluştu');
    }
  };

  return (
    <View>
      {/* Component içeriği */}
    </View>
  );
};
```

## ✅ Best Practices

### 1. Error Handling

```typescript
// ✅ Doğru kullanım
const [state, actions] = useAsyncState(
  asyncFunction,
  {
    showErrorAlert: false, // Global error handling kullan
    onError: (error) => {
      // Özel error handling
      addNotification({
        type: 'error',
        title: 'Hata',
        message: error,
        duration: 5000
      });
    }
  }
);

// ❌ Yanlış kullanım
const [state, actions] = useAsyncState(
  asyncFunction,
  {
    showErrorAlert: true // Her yerde alert gösterme
  }
);
```

### 2. Loading States

```typescript
// ✅ Doğru kullanım
{state.loading && <LoadingOverlay visible={true} message="Yükleniyor..." />}

// ❌ Yanlış kullanım
{state.loading && <Text>Loading...</Text>}
```

### 3. Form Validation

```typescript
// ✅ Doğru kullanım
const validationSchema = (values: any) => {
  const errors: any = {};
  if (!values.email) errors.email = 'Email gerekli';
  if (values.email && !/\S+@\S+\.\S+/.test(values.email)) {
    errors.email = 'Geçerli email girin';
  }
  return errors;
};

// ❌ Yanlış kullanım
const validate = () => {
  // Inline validation
};
```

### 4. State Güncellemeleri

```typescript
// ✅ Doğru kullanım
const handleSuccess = (data: any) => {
  setData(data);
  showSuccess('İşlem başarılı');
  refreshList(); // İlgili listeyi yenile
};

// ❌ Yanlış kullanım
const handleSuccess = (data: any) => {
  setData(data);
  // Diğer state'leri güncellemeyi unutma
};
```

## 🔧 Kurulum

1. Context'leri ana layout'a ekleyin:

```typescript
// app/_layout.tsx
export default function RootLayout() {
  return (
    <ThemeProvider>
      <AppStateProvider>
        <SystemProvider>
          <AuthProvider>
            <Stack />
            <NotificationManager />
          </AuthProvider>
        </SystemProvider>
      </AppStateProvider>
    </ThemeProvider>
  );
}
```

2. Hook'ları kullanmaya başlayın:

```typescript
import { useAsyncState } from '../hooks/useAsyncState';
import { useFormState } from '../hooks/useFormState';
import { useAppState } from '../contexts/AppStateContext';
```

## 📚 Ek Kaynaklar

- [React Native State Management](https://reactnative.dev/docs/state)
- [React Context API](https://react.dev/reference/react/createContext)
- [Custom Hooks](https://react.dev/learn/reusing-logic-with-custom-hooks)

## 🤝 Katkıda Bulunma

Bu state yönetim yapısını geliştirmek için:

1. Yeni hook'lar ekleyin
2. Mevcut hook'ları iyileştirin
3. Dokümantasyonu güncelleyin
4. Test'ler ekleyin

## 📄 Lisans

Bu proje MIT lisansı altında lisanslanmıştır. 