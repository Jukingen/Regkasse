import { Alert, Platform, type AlertButton } from 'react-native';

/**
 * POS user-facing dialog. Native uses React Native Alert.
 * Expo web treats Alert.alert as a no-op, so web uses window.alert.
 */
export function showAlert(title: string, message?: string): void {
  if (Platform.OS === 'web') {
    const text = alertText(title, message);
    if (typeof window !== 'undefined' && typeof window.alert === 'function') {
      window.alert(text);
    }
    return;
  }

  Alert.alert(title, message);
}

/**
 * Confirm dialog. Native keeps Alert.alert buttons.
 * Expo web's Alert.alert (including button callbacks) is a no-op, so the
 * destructive action never ran for "Alle leeren". Web uses window.confirm.
 */
export function showConfirm(
  title: string,
  message: string | undefined,
  buttons: AlertButton[]
): void {
  if (Platform.OS === 'web') {
    const accepted =
      typeof window !== 'undefined' && typeof window.confirm === 'function'
        ? window.confirm(alertText(title, message))
        : false;
    const cancel = buttons.find((button) => button.style === 'cancel');
    const confirm = [...buttons].reverse().find((button) => button.style !== 'cancel');
    if (accepted) confirm?.onPress?.();
    else cancel?.onPress?.();
    return;
  }

  Alert.alert(title, message, buttons);
}

function alertText(title: string, message?: string): string {
  return typeof message === 'string' && message.length > 0 ? `${title}\n\n${message}` : title;
}
