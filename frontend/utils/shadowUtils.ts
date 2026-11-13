import { Platform, type ViewStyle } from 'react-native';

/**
 * Expand #RGB / #RRGGBB (optional alpha) into rgba() for valid CSS box-shadow.
 * Avoids invalid colors like `#00040` from appending alpha to short hex.
 */
export function hexToRgba(hex: string, alpha: number): string {
  const raw = hex.trim().replace(/^#/, '');
  let r = 0;
  let g = 0;
  let b = 0;

  if (raw.length === 3) {
    r = Number.parseInt(raw[0] + raw[0], 16);
    g = Number.parseInt(raw[1] + raw[1], 16);
    b = Number.parseInt(raw[2] + raw[2], 16);
  } else if (raw.length === 6 || raw.length === 8) {
    r = Number.parseInt(raw.slice(0, 2), 16);
    g = Number.parseInt(raw.slice(2, 4), 16);
    b = Number.parseInt(raw.slice(4, 6), 16);
  } else {
    return `rgba(0, 0, 0, ${alpha})`;
  }

  const clamped = Math.min(1, Math.max(0, alpha));
  return `rgba(${r}, ${g}, ${b}, ${clamped})`;
}

/**
 * Platform-aware shadow style.
 * iOS: shadow*; Android: elevation; Web: boxShadow (rgba).
 */
export const createShadowStyle = (
  shadowColor: string = '#000',
  shadowOffset: { width: number; height: number } = { width: 0, height: 2 },
  shadowOpacity: number = 0.25,
  shadowRadius: number = 3.84,
  elevation: number = 5
): ViewStyle => {
  if (Platform.OS === 'ios') {
    return {
      shadowColor,
      shadowOffset,
      shadowOpacity,
      shadowRadius,
    };
  }
  if (Platform.OS === 'android') {
    return {
      elevation,
    };
  }

  // Web — use rgba(); RNW also accepts shadow* but callers often pass short hex.
  return {
    boxShadow: `${shadowOffset.width}px ${shadowOffset.height}px ${shadowRadius}px 0px ${hexToRgba(shadowColor, shadowOpacity)}`,
  };
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
  return createShadowStyle(shadowColor, shadowOffset, shadowOpacity, shadowRadius, elevation);
};
