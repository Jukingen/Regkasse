'use client';

import React, {
  createContext,
  useCallback,
  useContext,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { applyReducedAnimations } from './applyDocumentTheme';
import { resolveFormatLocaleForDateFormat } from './formatLocale';
import { useDensityContext } from './DensityContext';
import { useThemeContext } from './ThemeContext';
import {
  patchStoredPersonalization,
  readStoredPersonalization,
  writeStoredPersonalization,
} from './storage';
import type {
  DateFormatPattern,
  DefaultLandingPath,
  DensityMode,
  PersonalizationPreferences,
  ResolvedTheme,
  ThemeMode,
  TimeFormatPreference,
} from './types';
import {
  fetchUserPreferences,
  mapApiToPersonalization,
  mapPersonalizationToApi,
  saveUserPreferences,
  userPreferencesQueryKey,
} from './userPreferencesApi';
import { useI18n } from '@/i18n';
import { authStorage } from '@/features/auth/services/authStorage';

type PersonalizationContextValue = {
  preferences: PersonalizationPreferences;
  effectiveTheme: ResolvedTheme;
  isSyncing: boolean;
  setThemeMode: (mode: ThemeMode) => void;
  setDensity: (density: DensityMode) => void;
  setDefaultLandingPath: (path: DefaultLandingPath) => void;
  setDateFormat: (format: DateFormatPattern) => void;
  setTimeFormat: (format: TimeFormatPreference) => void;
  setReducedAnimations: (value: boolean) => void;
  replacePreferences: (next: PersonalizationPreferences) => void;
};

const PersonalizationContext = createContext<PersonalizationContextValue | null>(null);

function PersonalizationBridge({ children }: { children: ReactNode }) {
  const { textLocale, setFormatLocale } = useI18n();
  const ctx = useContext(PersonalizationContext);
  const dateFormat = ctx?.preferences.dateFormat ?? 'DD.MM.YYYY';

  useLayoutEffect(() => {
    setFormatLocale(resolveFormatLocaleForDateFormat(dateFormat, textLocale));
  }, [dateFormat, textLocale, setFormatLocale]);

  useLayoutEffect(() => {
    if (ctx?.preferences.reducedAnimations !== undefined) {
      applyReducedAnimations(ctx.preferences.reducedAnimations);
    }
  }, [ctx?.preferences.reducedAnimations]);

  return <>{children}</>;
}

function PersonalizationStateBridge({ children }: { children: ReactNode }) {
  const { themeMode, setThemeMode, effectiveTheme } = useThemeContext();
  const { densityMode, setDensityMode } = useDensityContext();
  const queryClient = useQueryClient();
  const skipNextRemoteSave = useRef(false);

  const [userPrefs, setUserPrefs] = useState(() => {
    const s = readStoredPersonalization();
    return {
      defaultLandingPath: s.defaultLandingPath,
      dateFormat: s.dateFormat,
      timeFormat: s.timeFormat,
      reducedAnimations: s.reducedAnimations,
    };
  });

  const isAuthenticated =
    typeof window !== 'undefined' && !!authStorage.getAccessToken();

  const remoteQuery = useQuery({
    queryKey: userPreferencesQueryKey,
    queryFn: fetchUserPreferences,
    enabled: isAuthenticated,
    staleTime: 60_000,
  });

  const saveMutation = useMutation({
    mutationFn: saveUserPreferences,
    onSuccess: (data) => {
      queryClient.setQueryData(userPreferencesQueryKey, data);
    },
  });

  useLayoutEffect(() => {
    if (!remoteQuery.isSuccess || !remoteQuery.data) return;
    if (skipNextRemoteSave.current) {
      skipNextRemoteSave.current = false;
      return;
    }
    const fromApi = mapApiToPersonalization(remoteQuery.data);
    setUserPrefs({
      defaultLandingPath: fromApi.defaultLandingPath,
      dateFormat: fromApi.dateFormat,
      timeFormat: fromApi.timeFormat,
      reducedAnimations: fromApi.reducedAnimations,
    });
    writeStoredPersonalization(fromApi);
  }, [remoteQuery.isSuccess, remoteQuery.data]);

  const preferences = useMemo<PersonalizationPreferences>(
    () => ({
      themeMode,
      density: densityMode,
      ...userPrefs,
    }),
    [themeMode, densityMode, userPrefs],
  );

  const persistUserFields = useCallback(
    (patch: Partial<typeof userPrefs>) => {
      const nextUser = { ...userPrefs, ...patch };
      setUserPrefs(nextUser);
      const full = patchStoredPersonalization({
        themeMode,
        density: densityMode,
        ...nextUser,
      });
      applyReducedAnimations(full.reducedAnimations);
      if (authStorage.getAccessToken()) {
        skipNextRemoteSave.current = true;
        saveMutation.mutate(mapPersonalizationToApi(full));
      }
      return full;
    },
    [userPrefs, themeMode, densityMode, saveMutation],
  );

  const setDefaultLandingPath = useCallback(
    (defaultLandingPath: DefaultLandingPath) => persistUserFields({ defaultLandingPath }),
    [persistUserFields],
  );

  const setDateFormat = useCallback(
    (dateFormat: DateFormatPattern) => persistUserFields({ dateFormat }),
    [persistUserFields],
  );

  const setTimeFormat = useCallback(
    (timeFormat: TimeFormatPreference) => persistUserFields({ timeFormat }),
    [persistUserFields],
  );

  const setReducedAnimations = useCallback(
    (reducedAnimations: boolean) => persistUserFields({ reducedAnimations }),
    [persistUserFields],
  );

  const replacePreferences = useCallback(
    (next: PersonalizationPreferences) => {
      setThemeMode(next.themeMode);
      setDensityMode(next.density);
      setUserPrefs({
        defaultLandingPath: next.defaultLandingPath,
        dateFormat: next.dateFormat,
        timeFormat: next.timeFormat,
        reducedAnimations: next.reducedAnimations,
      });
      writeStoredPersonalization(next);
      applyReducedAnimations(next.reducedAnimations);
      if (authStorage.getAccessToken()) {
        skipNextRemoteSave.current = true;
        saveMutation.mutate(mapPersonalizationToApi(next));
      }
    },
    [setThemeMode, setDensityMode, saveMutation],
  );

  const value = useMemo<PersonalizationContextValue>(
    () => ({
      preferences,
      effectiveTheme,
      isSyncing: remoteQuery.isFetching || saveMutation.isPending,
      setThemeMode,
      setDensity: setDensityMode,
      setDefaultLandingPath,
      setDateFormat,
      setTimeFormat,
      setReducedAnimations,
      replacePreferences,
    }),
    [
      preferences,
      effectiveTheme,
      remoteQuery.isFetching,
      saveMutation.isPending,
      setThemeMode,
      setDensityMode,
      setDefaultLandingPath,
      setDateFormat,
      setTimeFormat,
      setReducedAnimations,
      replacePreferences,
    ],
  );

  return (
    <PersonalizationContext.Provider value={value}>
      <PersonalizationBridge>{children}</PersonalizationBridge>
    </PersonalizationContext.Provider>
  );
}

/** User preference fields (landing, formats, a11y). Wrap with {@link ThemeProvider} at app root. */
export function PersonalizationProvider({ children }: { children: ReactNode }) {
  return <PersonalizationStateBridge>{children}</PersonalizationStateBridge>;
}

export function usePersonalization() {
  const ctx = useContext(PersonalizationContext);
  if (!ctx) {
    throw new Error('usePersonalization must be used inside PersonalizationProvider');
  }
  return ctx;
}

export function getDefaultLandingPathFromStorage(): DefaultLandingPath {
  return readStoredPersonalization().defaultLandingPath;
}
