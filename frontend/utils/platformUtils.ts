import { Platform } from 'react-native';

/**
 * Platform kontrolü için utility fonksiyonları
 */

export const isWeb = Platform.OS === 'web';
export const isIOS = Platform.OS === 'ios';
export const isAndroid = Platform.OS === 'android';
export const isNative = isIOS || isAndroid;

/**
 * Platform'a göre stil oluşturur
 */
export const createPlatformStyle = <T extends Record<string, any>>(
  webStyle: T,
  nativeStyle: T,
  iosStyle?: Partial<T>,
  androidStyle?: Partial<T>
): T => {
  if (isWeb) {
    return webStyle;
  } else if (isIOS && iosStyle) {
    return { ...nativeStyle, ...iosStyle };
  } else if (isAndroid && androidStyle) {
    return { ...nativeStyle, ...androidStyle };
  }
  return nativeStyle;
};

/**
 * Web platform için güvenli window kontrolü
 */
export const safeWindow = () => {
  if (isWeb && typeof window !== 'undefined') {
    return window;
  }
  return null;
};

/**
 * AsyncStorage kullanımı için güvenli kontrol
 */
export const canUseAsyncStorage = () => {
  return !isWeb || (isWeb && typeof window !== 'undefined');
};

/**
 * Platform'a göre console mesajı
 */
export const platformLog = (message: string, platform?: 'web' | 'ios' | 'android' | 'all') => {
  if (platform === 'web' && isWeb) {
    console.log(`[WEB] ${message}`);
  } else if (platform === 'ios' && isIOS) {
    console.log(`[iOS] ${message}`);
  } else if (platform === 'android' && isAndroid) {
    console.log(`[Android] ${message}`);
  } else if (platform === 'all' || !platform) {
    console.log(`[${Platform.OS.toUpperCase()}] ${message}`);
  }
};
