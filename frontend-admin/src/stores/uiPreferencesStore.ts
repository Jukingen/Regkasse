'use client';

/**
 * Zustand store for FA UI preferences only (theme, density, landing, formats, a11y).
 * Never put auth tokens, user profiles, tenant secrets, or React Query cache here.
 * @see AGENTS.md Frontend-Admin conventions
 */
import { create } from 'zustand';
import { useShallow } from 'zustand/react/shallow';

import {
  THEME_MODE_STORAGE_KEY,
  patchStoredPersonalization,
  readStoredPersonalization,
  writeStoredPersonalization,
} from '@/lib/personalization/storage';
import { resolveEffectiveTheme } from '@/lib/personalization/theme';
import type {
  DateFormatPattern,
  DefaultLandingPath,
  DensityMode,
  PreferenceLanguage,
  PersonalizationPreferences,
  ResolvedTheme,
  ThemeMode,
  TimeFormatPreference,
  UserTimeZone,
} from '@/lib/personalization/types';
import { DEFAULT_PERSONALIZATION } from '@/lib/personalization/types';

function mirrorThemeModeKey(themeMode: ThemeMode): void {
  if (typeof window === 'undefined') return;
  try {
    window.localStorage.setItem(THEME_MODE_STORAGE_KEY, themeMode);
  } catch {
    /* restricted storage */
  }
}

function snapshotPreferences(state: {
  themeMode: ThemeMode;
  densityMode: DensityMode;
  defaultLandingPath: DefaultLandingPath;
  dateFormat: DateFormatPattern;
  timeFormat: TimeFormatPreference;
  timeZone: UserTimeZone;
  language: PreferenceLanguage;
  reducedAnimations: boolean;
}): PersonalizationPreferences {
  return {
    themeMode: state.themeMode,
    density: state.densityMode,
    defaultLandingPath: state.defaultLandingPath,
    dateFormat: state.dateFormat,
    timeFormat: state.timeFormat,
    timeZone: state.timeZone,
    language: state.language,
    reducedAnimations: state.reducedAnimations,
  };
}

export type UiPreferencesState = {
  themeMode: ThemeMode;
  densityMode: DensityMode;
  effectiveTheme: ResolvedTheme;
  defaultLandingPath: DefaultLandingPath;
  dateFormat: DateFormatPattern;
  timeFormat: TimeFormatPreference;
  timeZone: UserTimeZone;
  language: PreferenceLanguage;
  reducedAnimations: boolean;
  /** True after client hydrate from localStorage / legacy keys. */
  hydrated: boolean;
  /** RQ fetch/save in flight — set by PersonalizationSync, not business logic. */
  isSyncing: boolean;

  setThemeMode: (mode: ThemeMode) => void;
  setDensityMode: (mode: DensityMode) => void;
  setDefaultLandingPath: (path: DefaultLandingPath) => void;
  setDateFormat: (format: DateFormatPattern) => void;
  setTimeFormat: (format: TimeFormatPreference) => void;
  setTimeZone: (timeZone: UserTimeZone) => void;
  setLanguage: (language: PreferenceLanguage) => void;
  setReducedAnimations: (value: boolean) => void;
  replacePreferences: (next: PersonalizationPreferences) => void;
  /** Apply API / storage snapshot without marking as a local user edit. */
  applyRemotePreferences: (next: PersonalizationPreferences) => void;
  hydrateFromClientStorage: () => void;
  setEffectiveTheme: (theme: ResolvedTheme) => void;
  setHydrated: (value: boolean) => void;
  setIsSyncing: (value: boolean) => void;
  getPreferences: () => PersonalizationPreferences;
};

const initialStored =
  typeof window !== 'undefined' ? readStoredPersonalization() : { ...DEFAULT_PERSONALIZATION };

export const useUiPreferencesStore = create<UiPreferencesState>((set, get) => ({
  themeMode: initialStored.themeMode,
  densityMode: initialStored.density,
  effectiveTheme: resolveEffectiveTheme(initialStored.themeMode),
  defaultLandingPath: initialStored.defaultLandingPath,
  dateFormat: initialStored.dateFormat,
  timeFormat: initialStored.timeFormat,
  timeZone: initialStored.timeZone,
  language: initialStored.language,
  reducedAnimations: initialStored.reducedAnimations,
  hydrated: false,
  isSyncing: false,

  setThemeMode: (themeMode) => {
    const next = patchStoredPersonalization({ themeMode });
    mirrorThemeModeKey(themeMode);
    set({
      themeMode,
      effectiveTheme: resolveEffectiveTheme(themeMode),
      densityMode: next.density,
      defaultLandingPath: next.defaultLandingPath,
      dateFormat: next.dateFormat,
      timeFormat: next.timeFormat,
      timeZone: next.timeZone,
      language: next.language,
      reducedAnimations: next.reducedAnimations,
    });
  },

  setDensityMode: (densityMode) => {
    const next = patchStoredPersonalization({ density: densityMode });
    set({
      densityMode,
      themeMode: next.themeMode,
      defaultLandingPath: next.defaultLandingPath,
      dateFormat: next.dateFormat,
      timeFormat: next.timeFormat,
      timeZone: next.timeZone,
      language: next.language,
      reducedAnimations: next.reducedAnimations,
    });
  },

  setDefaultLandingPath: (defaultLandingPath) => {
    patchStoredPersonalization({ defaultLandingPath });
    set({ defaultLandingPath });
  },

  setDateFormat: (dateFormat) => {
    patchStoredPersonalization({ dateFormat });
    set({ dateFormat });
  },

  setTimeFormat: (timeFormat) => {
    patchStoredPersonalization({ timeFormat });
    set({ timeFormat });
  },

  setTimeZone: (timeZone) => {
    patchStoredPersonalization({ timeZone });
    set({ timeZone });
  },

  setLanguage: (language) => {
    patchStoredPersonalization({ language });
    set({ language });
  },

  setReducedAnimations: (reducedAnimations) => {
    patchStoredPersonalization({ reducedAnimations });
    set({ reducedAnimations });
  },

  replacePreferences: (next) => {
    writeStoredPersonalization(next);
    mirrorThemeModeKey(next.themeMode);
    set({
      themeMode: next.themeMode,
      densityMode: next.density,
      effectiveTheme: resolveEffectiveTheme(next.themeMode),
      defaultLandingPath: next.defaultLandingPath,
      dateFormat: next.dateFormat,
      timeFormat: next.timeFormat,
      timeZone: next.timeZone,
      language: next.language,
      reducedAnimations: next.reducedAnimations,
    });
  },

  applyRemotePreferences: (next) => {
    writeStoredPersonalization(next);
    mirrorThemeModeKey(next.themeMode);
    set({
      themeMode: next.themeMode,
      densityMode: next.density,
      effectiveTheme: resolveEffectiveTheme(next.themeMode),
      defaultLandingPath: next.defaultLandingPath,
      dateFormat: next.dateFormat,
      timeFormat: next.timeFormat,
      timeZone: next.timeZone,
      language: next.language,
      reducedAnimations: next.reducedAnimations,
    });
  },

  hydrateFromClientStorage: () => {
    const legacyTheme = window.localStorage.getItem(THEME_MODE_STORAGE_KEY) as ThemeMode | null;
    const stored = readStoredPersonalization();
    const themeMode =
      legacyTheme === 'light' || legacyTheme === 'dark' || legacyTheme === 'system'
        ? legacyTheme
        : stored.themeMode;
    const next = { ...stored, themeMode };
    writeStoredPersonalization(next);
    mirrorThemeModeKey(themeMode);
    set({
      themeMode,
      densityMode: next.density,
      effectiveTheme: resolveEffectiveTheme(themeMode),
      defaultLandingPath: next.defaultLandingPath,
      dateFormat: next.dateFormat,
      timeFormat: next.timeFormat,
      timeZone: next.timeZone,
      language: next.language,
      reducedAnimations: next.reducedAnimations,
      hydrated: true,
    });
  },

  setEffectiveTheme: (effectiveTheme) => set({ effectiveTheme }),
  setHydrated: (hydrated) => set({ hydrated }),
  setIsSyncing: (isSyncing) => set({ isSyncing }),
  getPreferences: () => snapshotPreferences(get()),
}));

/** Full preferences object for consumers that previously used Context value.preferences. */
export function useUiPreferences(): PersonalizationPreferences {
  return useUiPreferencesStore(
    useShallow((s) => ({
      themeMode: s.themeMode,
      density: s.densityMode,
      defaultLandingPath: s.defaultLandingPath,
      dateFormat: s.dateFormat,
      timeFormat: s.timeFormat,
      timeZone: s.timeZone,
      language: s.language,
      reducedAnimations: s.reducedAnimations,
    }))
  );
}
