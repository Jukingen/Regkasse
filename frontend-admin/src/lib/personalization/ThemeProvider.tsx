'use client';

import { App, ConfigProvider } from 'antd';
import React, {
  type ReactNode,
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from 'react';

import { authStorage } from '@/features/auth/services/authStorage';
import { useUserPreferences } from '@/features/user/hooks/useUserPreferences';
import { useI18n } from '@/i18n';
import { AntdAppBridgeRegistrar } from '@/lib/AntdAppBridgeRegistrar';
import { getAntdLocale } from '@/lib/antdLocale';
import { buildAntdTheme } from '@/theme/buildAntdTheme';

import { DensityProvider } from './DensityProvider';
import { ThemeContext } from './ThemeContext';
import {
  applyDocumentDensity,
  applyDocumentTheme,
  applyReducedAnimations,
} from './applyDocumentTheme';
import { antdComponentSizeForDensity } from './density';
import {
  THEME_MODE_STORAGE_KEY,
  patchStoredPersonalization,
  readStoredPersonalization,
  writeStoredPersonalization,
} from './storage';
import { resolveEffectiveTheme } from './theme';
import type { DensityMode, ResolvedTheme, ThemeMode } from './types';
import {
  mapApiToPersonalization,
  mapPersonalizationToApi,
  saveUserPreferences,
} from './userPreferencesApi';

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
  const { textLocale } = useI18n();
  const storedOnInit = readStoredPersonalization();
  const [themeMode, setThemeModeState] = useState<ThemeMode>(storedOnInit.themeMode);
  const [densityMode, setDensityModeState] = useState<DensityMode>(storedOnInit.density);
  const [effectiveTheme, setEffectiveTheme] = useState<ResolvedTheme>(() =>
    resolveEffectiveTheme(storedOnInit.themeMode)
  );
  const hydrated = useRef(false);
  const skipRemoteApply = useRef(false);

  const remoteQuery = useUserPreferences();

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

    if (authStorage.hasToken()) {
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
    [themeMode, setThemeMode, effectiveTheme]
  );

  const antdTheme = useMemo(
    () => buildAntdTheme(effectiveTheme, densityMode),
    [effectiveTheme, densityMode]
  );

  const antdLocale = useMemo(() => getAntdLocale(textLocale), [textLocale]);

  const componentSize = antdComponentSizeForDensity(densityMode);

  return (
    <ThemeContext.Provider value={themeContextValue}>
      <ConfigProvider locale={antdLocale} theme={antdTheme} componentSize={componentSize}>
        <App>
          <AntdAppBridgeRegistrar />
          <DensityProvider densityMode={densityMode} setDensityMode={setDensityMode}>
            {children}
          </DensityProvider>
        </App>
      </ConfigProvider>
    </ThemeContext.Provider>
  );
}
