import { Platform } from 'react-native';

/**
 * Runtime font map for `useFonts` / `Font.loadAsync`.
 * Keys must match `fontFamily` style values and the expo-font config plugin assets.
 */
export const CUSTOM_FONT_MAP = {
  'OCRA-B': require('../assets/fonts/OCRA-B.ttf'),
} as const;

/** Primary OCR-A-B family name (receipt / fiscal print preview). */
export const FONT_FAMILY_OCRA_B = 'OCRA-B';

/**
 * Native fallback when the custom face is unavailable.
 * iOS: Courier is a built-in monospaced face; Android: generic monospace.
 */
export const RECEIPT_FONT_FALLBACK = Platform.select({
  ios: 'Courier',
  android: 'monospace',
  default: 'monospace',
});

/**
 * Receipt font family with platform-appropriate fallbacks.
 * - Web: CSS font stack (react-native-web)
 * - Native: loaded custom face (gate rendering on useFonts); system mono if unloaded
 */
export const RECEIPT_FONT_FAMILY =
  Platform.OS === 'web'
    ? `${FONT_FAMILY_OCRA_B}, "Courier New", Courier, monospace`
    : FONT_FAMILY_OCRA_B;
