'use client';

import { useCallback, useMemo } from 'react';

import { usePersonalization } from '../PersonalizationProvider';
import type {
  DateFormatPattern,
  DefaultLandingPath,
  PreferenceLanguage,
  TimeFormatPreference,
  UserPreferencesUi,
  UserTimeZone,
} from '../types';

export function useUserPreferences() {
  const {
    preferences,
    setDefaultLandingPath,
    setDateFormat,
    setTimeFormat,
    setTimeZone,
    setLanguage,
    setReducedAnimations,
    isSyncing,
  } = usePersonalization();

  const uiPreferences = useMemo<UserPreferencesUi>(
    () => ({
      defaultPage: preferences.defaultLandingPath,
      dateFormat: preferences.dateFormat,
      timeFormat: preferences.timeFormat,
      timeZone: preferences.timeZone,
      language: preferences.language,
      reducedAnimations: preferences.reducedAnimations,
    }),
    [preferences]
  );

  const updatePreferences = useCallback(
    (patch: Partial<UserPreferencesUi>) => {
      if (patch.defaultPage !== undefined) {
        setDefaultLandingPath(patch.defaultPage as DefaultLandingPath);
      }
      if (patch.dateFormat !== undefined) {
        setDateFormat(patch.dateFormat as DateFormatPattern);
      }
      if (patch.timeFormat !== undefined) {
        setTimeFormat(patch.timeFormat as TimeFormatPreference);
      }
      if (patch.timeZone !== undefined) {
        setTimeZone(patch.timeZone as UserTimeZone);
      }
      if (patch.language !== undefined) {
        setLanguage(patch.language as PreferenceLanguage);
      }
      if (patch.reducedAnimations !== undefined) {
        setReducedAnimations(patch.reducedAnimations);
      }
    },
    [
      setDefaultLandingPath,
      setDateFormat,
      setTimeFormat,
      setTimeZone,
      setLanguage,
      setReducedAnimations,
    ]
  );

  return {
    preferences: uiPreferences,
    updatePreferences,
    isSyncing,
  };
}
