import { Platform } from 'react-native';

/**
 * Console uyarılarını kontrol etmek için logging utility
 */

// Development modunda uyarıları göster, production'da gizle
const isDevelopment = __DEV__;
const isWeb = Platform.OS === 'web';

/**
 * Güvenli console.log - production'da gizlenebilir
 */
export const safeLog = (...args: any[]) => {
  if (isDevelopment) {
    console.log(...args);
  }
};

/**
 * Güvenli console.warn - production'da gizlenebilir
 */
export const safeWarn = (...args: any[]) => {
  if (isDevelopment) {
    console.warn(...args);
  }
};

/**
 * Güvenli console.error - her zaman göster
 */
export const safeError = (...args: any[]) => {
  console.error(...args);
};

/**
 * Platform-specific logging
 */
export const platformLog = (message: string, level: 'log' | 'warn' | 'error' = 'log') => {
  const prefix = `[${Platform.OS.toUpperCase()}]`;
  
  switch (level) {
    case 'warn':
      safeWarn(`${prefix} ${message}`);
      break;
    case 'error':
      safeError(`${prefix} ${message}`);
      break;
    default:
      safeLog(`${prefix} ${message}`);
  }
};

/**
 * Memory kullanım logları (sadece web'de)
 */
export const logMemoryUsage = () => {
  if (isWeb && isDevelopment && global.performance && (global.performance as any).memory) {
    const memory = (global.performance as any).memory;
    const usedMB = Math.round(memory.usedJSHeapSize / 1024 / 1024);
    const totalMB = Math.round(memory.totalJSHeapSize / 1024 / 1024);
    
    safeLog(`Memory: ${usedMB}MB / ${totalMB}MB`);
    
    if (usedMB > 100) {
      safeWarn(`Memory usage high: ${usedMB}MB / ${totalMB}MB`);
    }
  }
};

/**
 * Performance timing logları
 */
export const logPerformance = (label: string, startTime: number) => {
  if (isDevelopment) {
    const endTime = performance.now();
    const duration = endTime - startTime;
    safeLog(`${label}: ${duration.toFixed(2)}ms`);
  }
};
