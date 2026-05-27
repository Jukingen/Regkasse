'use client';

import { useCallback, useMemo } from 'react';
import { usePersonalization } from '../PersonalizationProvider';
import type {
  DateFormatPattern,
  DefaultLandingPath,
  TimeFormatPreference,
  UserPreferencesUi,
} from '../types';

export function useUserPreferences() {
  const {
    preferences,
    setDefaultLandingPath,
    setDateFormat,
    setTimeFormat,
    setReducedAnimations,
    isSyncing,
  } = usePersonalization();

  const uiPreferences = useMemo<UserPreferencesUi>(
    () => ({
      defaultPage: preferences.defaultLandingPath,
      dateFormat: preferences.dateFormat,
      timeFormat: preferences.timeFormat,
      reducedAnimations: preferences.reducedAnimations,
    }),
    [preferences],
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
      if (patch.reducedAnimations !== undefined) {
        setReducedAnimations(patch.reducedAnimations);
      }
    },
    [setDefaultLandingPath, setDateFormat, setTimeFormat, setReducedAnimations],
  );

  return {
    preferences: uiPreferences,
    updatePreferences,
    isSyncing,
  };
}
