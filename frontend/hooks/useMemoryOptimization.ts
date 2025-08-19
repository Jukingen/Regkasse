import { useEffect, useRef } from 'react';

/**
 * Memory leak'leri önlemek için kullanılan hook
 * Component unmount olduğunda cleanup işlemlerini otomatik yapar
 */
export const useMemoryOptimization = () => {
  const mountedRef = useRef(true);
  const cleanupRef = useRef<(() => void)[]>([]);

  useEffect(() => {
    mountedRef.current = true;
    
    return () => {
      mountedRef.current = false;
      // Tüm cleanup fonksiyonlarını çalıştır
      cleanupRef.current.forEach(cleanup => cleanup());
      cleanupRef.current = [];
    };
  }, []);

  const addCleanup = (cleanup: () => void) => {
    cleanupRef.current.push(cleanup);
  };

  const isMounted = () => mountedRef.current;

  return { addCleanup, isMounted };
};

/**
 * Memory kullanımını izlemek için hook
 */
export const useMemoryMonitor = () => {
  useEffect(() => {
    if (__DEV__) {
      const interval = setInterval(() => {
        if (global.performance && (global.performance as any).memory) {
          const memory = (global.performance as any).memory;
          const usedMB = Math.round(memory.usedJSHeapSize / 1024 / 1024);
          const totalMB = Math.round(memory.totalJSHeapSize / 1024 / 1024);
          
          if (usedMB > 100) { // 100MB üzerinde uyarı
            console.warn(`Memory usage high: ${usedMB}MB / ${totalMB}MB`);
          }
        }
      }, 60 * 1000); // OPTIMIZATION: 10 saniye yerine 1 dakikada bir kontrol

      return () => clearInterval(interval);
    }
  }, []);
};
