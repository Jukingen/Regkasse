import { Platform } from 'react-native';

/** Reloads the app after dev tenant change (web: full reload; native: DevSettings). */
export function reloadApp(): void {
  if (Platform.OS === 'web' && typeof window !== 'undefined') {
    window.location.reload();
    return;
  }

  try {
    const { DevSettings } = require('react-native') as typeof import('react-native');
    if (DevSettings?.reload) {
      DevSettings.reload();
    }
  } catch {
    /* dev switcher best-effort */
  }
}
