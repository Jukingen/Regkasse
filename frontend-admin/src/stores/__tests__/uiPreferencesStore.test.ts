import { beforeEach, describe, expect, it, vi } from 'vitest';

import { DEFAULT_PERSONALIZATION } from '@/lib/personalization/types';
import { useUiPreferencesStore } from '@/stores/uiPreferencesStore';

describe('useUiPreferencesStore', () => {
  beforeEach(() => {
    const storage: Record<string, string> = {};
    vi.stubGlobal('localStorage', {
      getItem: (k: string) => storage[k] ?? null,
      setItem: (k: string, v: string) => {
        storage[k] = v;
      },
      removeItem: (k: string) => {
        delete storage[k];
      },
    });
    useUiPreferencesStore.setState({
      themeMode: DEFAULT_PERSONALIZATION.themeMode,
      densityMode: DEFAULT_PERSONALIZATION.density,
      effectiveTheme: 'light',
      defaultLandingPath: DEFAULT_PERSONALIZATION.defaultLandingPath,
      dateFormat: DEFAULT_PERSONALIZATION.dateFormat,
      timeFormat: DEFAULT_PERSONALIZATION.timeFormat,
      timeZone: DEFAULT_PERSONALIZATION.timeZone,
      language: DEFAULT_PERSONALIZATION.language,
      reducedAnimations: DEFAULT_PERSONALIZATION.reducedAnimations,
      hydrated: false,
      isSyncing: false,
    });
  });

  it('updates theme mode and mirrors preferences snapshot', () => {
    useUiPreferencesStore.getState().setThemeMode('dark');
    const s = useUiPreferencesStore.getState();
    expect(s.themeMode).toBe('dark');
    expect(s.effectiveTheme).toBe('dark');
    expect(s.getPreferences().themeMode).toBe('dark');
  });

  it('updates density independently', () => {
    useUiPreferencesStore.getState().setDensityMode('compact');
    expect(useUiPreferencesStore.getState().densityMode).toBe('compact');
    expect(useUiPreferencesStore.getState().getPreferences().density).toBe('compact');
  });

  it('replacePreferences writes all fields', () => {
    useUiPreferencesStore.getState().replacePreferences({
      ...DEFAULT_PERSONALIZATION,
      themeMode: 'light',
      density: 'comfortable',
      reducedAnimations: true,
      defaultLandingPath: '/reporting',
    });
    const prefs = useUiPreferencesStore.getState().getPreferences();
    expect(prefs.density).toBe('comfortable');
    expect(prefs.reducedAnimations).toBe(true);
    expect(prefs.defaultLandingPath).toBe('/reporting');
  });
});
