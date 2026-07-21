import { useCameraPermissions } from 'expo-camera';
import { useCallback, useEffect, useState } from 'react';
import { Linking, Platform } from 'react-native';

export type PosCameraPermissionUi = 'loading' | 'web' | 'granted' | 'prompt' | 'blocked';

/**
 * Shared camera permission flow for POS scanners (voucher / customer QR / barcode modal).
 * - Requests once when `enabled` becomes true on native.
 * - Exposes `blocked` when the OS will not show the system prompt again (`canAskAgain === false`).
 */
export function usePosCameraPermission(enabled: boolean) {
  const [permission, requestPermission] = useCameraPermissions();
  const [requested, setRequested] = useState(false);

  useEffect(() => {
    if (!enabled || Platform.OS === 'web') return;
    if (!permission) return;
    if (permission.granted || requested) return;
    if (permission.canAskAgain === false) return;
    setRequested(true);
    void requestPermission();
  }, [enabled, permission, requested, requestPermission]);

  useEffect(() => {
    if (!enabled) setRequested(false);
  }, [enabled]);

  const ui: PosCameraPermissionUi = (() => {
    if (Platform.OS === 'web') return 'web';
    if (!permission) return 'loading';
    if (permission.granted) return 'granted';
    if (permission.canAskAgain === false) return 'blocked';
    return 'prompt';
  })();

  const openSettings = useCallback(() => {
    void Linking.openSettings();
  }, []);

  return {
    permission,
    ui,
    requestPermission: () => void requestPermission(),
    openSettings,
  };
}
