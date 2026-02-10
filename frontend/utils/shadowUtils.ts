import { Platform } from 'react-native';

/**
 * Platform'a göre shadow stilini oluşturur
 * iOS: shadowColor, shadowOffset, shadowOpacity, shadowRadius
 * Android/Web: elevation (Android), boxShadow (Web)
 */
export const createShadowStyle = (
  shadowColor: string = '#000',
  shadowOffset: { width: number; height: number } = { width: 0, height: 2 },
  shadowOpacity: number = 0.25,
  shadowRadius: number = 3.84,
  elevation: number = 5
) => {
  if (Platform.OS === 'ios') {
    return {
      shadowColor,
      shadowOffset,
      shadowOpacity,
      shadowRadius,
    };
  } else if (Platform.OS === 'android') {
    return {
      elevation,
    };
  } else {
    // Web platform için boxShadow
    const shadowX = shadowOffset.width;
    const shadowY = shadowOffset.height;
    const blurRadius = shadowRadius;
    const spreadRadius = 0;
    
    return {
      boxShadow: `${shadowX}px ${shadowY}px ${blurRadius}px ${spreadRadius}px ${shadowColor}${Math.round(shadowOpacity * 255).toString(16).padStart(2, '0')}`,
    };
  }
};

/**
 * Yaygın shadow stilleri için hazır fonksiyonlar
 */
export const shadowStyles = {
  small: createShadowStyle('#000', { width: 0, height: 1 }, 0.1, 2, 2),
  medium: createShadowStyle('#000', { width: 0, height: 2 }, 0.1, 4, 4),
  large: createShadowStyle('#000', { width: 0, height: 4 }, 0.15, 8, 8),
  card: createShadowStyle('#000', { width: 0, height: 2 }, 0.25, 3.84, 5),
  button: createShadowStyle('#000', { width: 0, height: 2 }, 0.1, 4, 4),
  modal: createShadowStyle('#000', { width: 0, height: 10 }, 0.3, 20, 10),
};

/**
 * Deprecated shadow props'ları modern stillere dönüştürür
 * @deprecated Bu fonksiyon eski shadow props'ları için kullanılır
 */
export const convertShadowProps = (
  shadowColor?: string,
  shadowOffset?: { width: number; height: number },
  shadowOpacity?: number,
  shadowRadius?: number,
  elevation?: number
) => {
  return createShadowStyle(
    shadowColor,
    shadowOffset,
    shadowOpacity,
    shadowRadius,
    elevation
  );
};
