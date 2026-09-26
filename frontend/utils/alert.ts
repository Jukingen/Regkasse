import { Alert, Platform } from 'react-native';

/**
 * POS user-facing dialog. Native uses React Native Alert.
 * Expo web treats Alert.alert as a no-op, so web uses window.alert.
 */
export function showAlert(title: string, message?: string): void {
  if (Platform.OS === 'web') {
    const text =
      typeof message === 'string' && message.length > 0 ? `${title}\n\n${message}` : title;
    if (typeof window !== 'undefined' && typeof window.alert === 'function') {
      window.alert(text);
    }
    return;
  }

  Alert.alert(title, message);
}
