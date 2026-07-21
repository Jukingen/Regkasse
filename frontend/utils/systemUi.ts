/**
 * Imperative SystemUI / Android navigation-bar helpers (no React).
 * Theme wiring lives in components/ThemedSystemUI.tsx.
 */
import * as SystemUI from 'expo-system-ui';

import { isAndroid, isNative } from './platformUtils';

/**
 * POS keeps the Android navigation bar visible
 * (system gestures / accessibility). Immersive hide is intentionally off.
 */
export const ANDROID_NAVIGATION_BAR_HIDDEN = false;

type NavigationBarModule = {
  NavigationBar?: {
    setStyle?: (style: 'auto' | 'inverted' | 'light' | 'dark') => void;
    setHidden?: (hidden: boolean) => void;
  };
  setVisibilityAsync?: (visibility: 'visible' | 'hidden') => Promise<void>;
  setStyle?: (style: 'auto' | 'inverted' | 'light' | 'dark') => void;
  setHidden?: (hidden: boolean) => void;
};

function loadNavigationBarModule(): NavigationBarModule | null {
  if (!isAndroid) return null;
  try {
    return require('expo-navigation-bar') as NavigationBarModule;
  } catch {
    return null;
  }
}

/**
 * Apply theme colors to OS chrome outside the React tree.
 * Safe no-op on web / unsupported platforms.
 */
export async function applySystemUiForTheme(
  backgroundColor: string,
  isDark: boolean
): Promise<void> {
  if (!isNative) return;

  try {
    await SystemUI.setBackgroundColorAsync(backgroundColor);
  } catch {
    // Expo Go / web stubs may reject; ignore.
  }

  const mod = loadNavigationBarModule();
  if (!mod) return;

  const buttonStyle = isDark ? 'light' : 'dark';

  try {
    if (typeof mod.NavigationBar?.setHidden === 'function') {
      mod.NavigationBar.setHidden(ANDROID_NAVIGATION_BAR_HIDDEN);
    } else if (typeof mod.setHidden === 'function') {
      mod.setHidden(ANDROID_NAVIGATION_BAR_HIDDEN);
    } else if (typeof mod.setVisibilityAsync === 'function') {
      await mod.setVisibilityAsync(ANDROID_NAVIGATION_BAR_HIDDEN ? 'hidden' : 'visible');
    }

    if (typeof mod.NavigationBar?.setStyle === 'function') {
      mod.NavigationBar.setStyle(buttonStyle);
    } else if (typeof mod.setStyle === 'function') {
      mod.setStyle(buttonStyle);
    }
  } catch {
    // Platform stub — root background still applied via SystemUI.
  }
}
