import { StatusBar } from 'expo-status-bar';
import React from 'react';

import { useTheme } from '../contexts/ThemeContext';

/**
 * Status bar text/icons follow the app theme (including forced light/dark),
 * not only the OS color scheme (`style="auto"` would ignore ThemeProvider).
 * Root view / Android nav bar colors are synced by ThemedSystemUI.
 */
export function ThemedStatusBar() {
  const { isDark } = useTheme();

  return <StatusBar style={isDark ? 'light' : 'dark'} animated />;
}
