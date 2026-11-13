/**
 * Sync root SystemUI chrome with the active app theme.
 * Status bar text/icons: ThemedStatusBar (expo-status-bar).
 */
import { useEffect } from 'react';

import { useTheme } from '../contexts/ThemeContext';
import { applySystemUiForTheme } from '../utils/systemUi';

export { ANDROID_NAVIGATION_BAR_HIDDEN, applySystemUiForTheme } from '../utils/systemUi';

/**
 * Keeps SystemUI root background aligned with ThemeProvider.
 * Mount once under ThemeProvider next to ThemedStatusBar.
 */
export function ThemedSystemUI(): null {
  const { theme, isDark } = useTheme();

  useEffect(() => {
    void applySystemUiForTheme(theme.background, isDark);
  }, [theme.background, isDark]);

  return null;
}
