/**
 * Memory leak'leri önlemek için utility fonksiyonları
 */

// Debounce fonksiyonu - gereksiz re-render'ları önler
export const debounce = <T extends (...args: any[]) => any>(
  func: T,
  wait: number
): ((...args: Parameters<T>) => void) => {
  let timeout: NodeJS.Timeout;
  return (...args: Parameters<T>) => {
    clearTimeout(timeout);
    timeout = setTimeout(() => func(...args), wait);
  };
};

// Throttle fonksiyonu - fonksiyon çağrılarını sınırlar
export const throttle = <T extends (...args: any[]) => any>(
  func: T,
  limit: number
): ((...args: Parameters<T>) => void) => {
  let inThrottle: boolean;
  return (...args: Parameters<T>) => {
    if (!inThrottle) {
      func(...args);
      inThrottle = true;
      setTimeout(() => (inThrottle = false), limit);
    }
  };
};

// Memory temizleme fonksiyonu
export const cleanupMemory = () => {
  if (global.gc) {
    global.gc();
  }
  
  // Event listener'ları temizle
  if (typeof window !== 'undefined') {
    window.removeEventListener('beforeunload', cleanupMemory);
  }
};

// Memory kullanımını kontrol et
export const checkMemoryUsage = () => {
  if (__DEV__ && global.performance && (global.performance as any).memory) {
    const memory = (global.performance as any).memory;
    const usedMB = Math.round(memory.usedJSHeapSize / 1024 / 1024);
    const totalMB = Math.round(memory.totalJSHeapSize / 1024 / 1024);
    
    console.log(`Memory: ${usedMB}MB / ${totalMB}MB`);
    
    if (usedMB > 150) { // 150MB üzerinde uyarı
      console.warn('Memory usage is high! Consider cleanup.');
      cleanupMemory();
    }
  }
};

// Component unmount olduğunda otomatik cleanup
export const createCleanupFunction = (cleanupFns: (() => void)[]) => {
  return () => {
    cleanupFns.forEach(fn => {
      try {
        fn();
      } catch (error) {
        console.warn('Cleanup function error:', error);
      }
    });
  };
};
