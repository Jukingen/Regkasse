# Memory Optimizasyonu ve Leak Önleme

Bu dosya, Expo/React Native uygulamasında memory leak'leri önlemek ve performansı artırmak için yapılan optimizasyonları açıklar.

## 🔧 Yapılan Optimizasyonlar

### 1. Babel Konfigürasyonu
- `@babel/plugin-transform-runtime`: Gereksiz polyfill'leri önler
- `@babel/plugin-transform-optional-chaining`: Modern JavaScript syntax desteği
- `@babel/plugin-transform-nullish-coalescing-operator`: Modern JavaScript syntax desteği
- Memory optimizasyon ayarları: `compact: false`, `sourceMaps: false`

### 2. Metro Konfigürasyonu
- Worker sayısı sınırlandırıldı (`maxWorkers: 2`)
- Bundle optimizasyonu
- Cache temizleme (`resetCache: true`)

### 3. Memory Hook'ları
- `useMemoryOptimization`: Component unmount'ta otomatik cleanup
- `useMemoryMonitor`: Memory kullanımını izleme ve uyarı

### 4. Utility Fonksiyonları
- `debounce`: Gereksiz re-render'ları önler
- `throttle`: Fonksiyon çağrılarını sınırlar
- `cleanupMemory`: Memory temizleme
- `checkMemoryUsage`: Memory kullanım kontrolü

## 🚀 Kullanım

### Memory Hook Kullanımı
```typescript
import { useMemoryOptimization } from '../hooks/useMemoryOptimization';

function MyComponent() {
  const { addCleanup, isMounted } = useMemoryOptimization();
  
  useEffect(() => {
    const interval = setInterval(() => {
      if (isMounted()) {
        // Component hala mount'ta, işlemi yap
      }
    }, 1000);
    
    // Cleanup fonksiyonu ekle
    addCleanup(() => clearInterval(interval));
  }, []);
}
```

### Memory Utility Kullanımı
```typescript
import { debounce, throttle, checkMemoryUsage } from '../utils/memoryUtils';

// Debounce örneği
const debouncedSearch = debounce(searchFunction, 300);

// Throttle örneği
const throttledScroll = throttle(scrollHandler, 100);

// Memory kontrolü
setInterval(checkMemoryUsage, 30000); // 30 saniyede bir
```

## 📱 Script Komutları

```bash
# Normal başlatma (memory optimizasyonlu)
npm start

# Cache temizleyerek başlatma
npm run start:clean

# Tam temizlik ve yeniden başlatma
npm run clean

# Proje sıfırlama (son çare)
npm run reset
```

## ⚠️ Memory Leak Belirtileri

1. **Uygulama yavaşlaması**
2. **Crash'ler**
3. **"JavaScript heap out of memory" hatası**
4. **Battery tüketiminde artış**
5. **Uygulama boyutunda sürekli artış**

## 🛠️ Sorun Giderme

### Memory Hatası Alırsanız:
1. `npm run clean` komutunu çalıştırın
2. Metro cache'i temizleyin: `expo start --clear`
3. Node modules'ı yeniden yükleyin: `npm run reset`
4. Memory kullanımını izleyin: `checkMemoryUsage()`

### Performans İyileştirme:
1. Gereksiz re-render'ları önleyin
2. `useCallback` ve `useMemo` kullanın
3. Event listener'ları temizleyin
4. Büyük listelerde `FlatList` kullanın
5. Image'ları optimize edin

## 📊 Memory Monitoring

Development modunda memory kullanımı otomatik olarak izlenir:
- 100MB üzerinde: Uyarı
- 150MB üzerinde: Otomatik cleanup
- Console'da detaylı bilgi

## 🔍 Debug İpuçları

1. **React DevTools** kullanın
2. **Flipper** ile memory profiling yapın
3. **Chrome DevTools** ile memory heap'i inceleyin
4. **Performance Monitor** ile CPU/Memory kullanımını izleyin

## 📝 Notlar

- Tüm optimizasyonlar production build'de otomatik aktif
- Development modunda ek memory monitoring
- Regular cleanup işlemleri otomatik yapılır
- Memory leak'ler genellikle event listener'lardan kaynaklanır
