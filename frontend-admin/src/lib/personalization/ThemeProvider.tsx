'use client';

import React, {
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { useQuery } from '@tanstack/react-query';
import { ConfigProvider } from 'antd';
import { applyDocumentDensity, applyDocumentTheme, applyReducedAnimations } from './applyDocumentTheme';
import { antdComponentSizeForDensity } from './density';
import { DensityProvider } from './DensityProvider';
import {
  patchStoredPersonalization,
  readStoredPersonalization,
  writeStoredPersonalization,
  THEME_MODE_STORAGE_KEY,
} from './storage';
import { resolveEffectiveTheme } from './theme';
import { ThemeContext } from './ThemeContext';
import type { DensityMode, ResolvedTheme, ThemeMode } from './types';
import {
  fetchUserPreferences,
  mapApiToPersonalization,
  mapPersonalizationToApi,
  saveUserPreferences,
  userPreferencesQueryKey,
} from './userPreferencesApi';
import { buildAntdTheme } from '@/theme/buildAntdTheme';
import { authStorage } from '@/features/auth/services/authStorage';

function applyAppearance(themeMode: ThemeMode, densityMode: DensityMode): ResolvedTheme {
  const resolved = applyDocumentTheme(themeMode);
  applyDocumentDensity(densityMode);
  return resolved;
}

function mirrorThemeModeKey(themeMode: ThemeMode): void {
  try {
    window.localStorage.setItem(THEME_MODE_STORAGE_KEY, themeMode);
  } catch {
    /* restricted storage */
  }
}

type ThemeProviderProps = {
  children: ReactNode;
};

export function ThemeProvider({ children }: ThemeProviderProps) {
  const storedOnInit = readStoredPersonalization();
  const [themeMode, setThemeModeState] = useState<ThemeMode>(storedOnInit.themeMode);
  const [densityMode, setDensityModeState] = useState<DensityMode>(storedOnInit.density);
  const [effectiveTheme, setEffectiveTheme] = useState<ResolvedTheme>(() =>
    resolveEffectiveTheme(storedOnInit.themeMode),
  );
  const hydrated = useRef(false);
  const skipRemoteApply = useRef(false);

  const isAuthenticated =
    typeof window !== 'undefined' && !!authStorage.getAccessToken();

  const remoteQuery = useQuery({
    queryKey: userPreferencesQueryKey,
    queryFn: fetchUserPreferences,
    enabled: isAuthenticated,
    staleTime: 60_000,
  });

  useLayoutEffect(() => {
    const legacyTheme = window.localStorage.getItem(THEME_MODE_STORAGE_KEY) as ThemeMode | null;
    const stored = readStoredPersonalization();
    const initialTheme =
      legacyTheme === 'light' || legacyTheme === 'dark' || legacyTheme === 'system'
        ? legacyTheme
        : stored.themeMode;

    setThemeModeState(initialTheme);
    setDensityModeState(stored.density);
    setEffectiveTheme(applyAppearance(initialTheme, stored.density));
    applyReducedAnimations(stored.reducedAnimations);
    mirrorThemeModeKey(initialTheme);
    hydrated.current = true;
  }, []);

  useLayoutEffect(() => {
    if (!remoteQuery.isSuccess || !remoteQuery.data) return;
    if (skipRemoteApply.current) {
      skipRemoteApply.current = false;
      return;
    }
    const fromApi = mapApiToPersonalization(remoteQuery.data);
    setThemeModeState(fromApi.themeMode);
    setDensityModeState(fromApi.density);
    setEffectiveTheme(applyAppearance(fromApi.themeMode, fromApi.density));
    applyReducedAnimations(fromApi.reducedAnimations);
    mirrorThemeModeKey(fromApi.themeMode);
    writeStoredPersonalization(fromApi);
  }, [remoteQuery.isSuccess, remoteQuery.data]);

  useEffect(() => {
    if (!hydrated.current) return;

    const next = patchStoredPersonalization({
      themeMode,
      density: densityMode,
    });
    mirrorThemeModeKey(themeMode);
    setEffectiveTheme(applyAppearance(themeMode, densityMode));
    applyReducedAnimations(next.reducedAnimations);

    if (authStorage.getAccessToken()) {
      skipRemoteApply.current = true;
      void saveUserPreferences(mapPersonalizationToApi(next));
    }
  }, [themeMode, densityMode]);

  useLayoutEffect(() => {
    if (!hydrated.current || themeMode !== 'system') return undefined;
    const media = window.matchMedia('(prefers-color-scheme: dark)');
    const onChange = () => {
      setEffectiveTheme(applyDocumentTheme('system'));
    };
    media.addEventListener('change', onChange);
    return () => media.removeEventListener('change', onChange);
  }, [themeMode]);

  const setThemeMode = useCallback((mode: ThemeMode) => {
    setThemeModeState(mode);
  }, []);

  const setDensityMode = useCallback((mode: DensityMode) => {
    setDensityModeState(mode);
  }, []);

  const themeContextValue = useMemo(
    () => ({ themeMode, setThemeMode, effectiveTheme }),
    [themeMode, setThemeMode, effectiveTheme],
  );

  const antdTheme = useMemo(
    () => buildAntdTheme(effectiveTheme, densityMode),
    [effectiveTheme, densityMode],
  );

  const componentSize = antdComponentSizeForDensity(densityMode);

  return (
    <ThemeContext.Provider value={themeContextValue}>
      <ConfigProvider theme={antdTheme} componentSize={componentSize}>
        <DensityProvider densityMode={densityMode} setDensityMode={setDensityMode}>
          {children}
        </DensityProvider>
      </ConfigProvider>
    </ThemeContext.Provider>
  );
}
