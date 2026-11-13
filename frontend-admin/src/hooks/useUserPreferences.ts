'use client';

/**
 * Preferences API + apply helpers for date/time/locale formatting.
 * Persistence goes through the existing personalization + `/api/admin/user/preferences` pipeline.
 */
import { useCallback, useMemo } from 'react';

import { useI18n } from '@/i18n';
import { usePersonalization } from '@/lib/personalization/PersonalizationProvider';
import { resolveFormatLocaleForDateFormat } from '@/lib/personalization/formatLocale';
import type {
  DateFormatPattern,
  PreferenceLanguage,
  TimeFormatPreference,
  UserPreferencesUi,
  UserTimeZone,
} from '@/lib/personalization/types';
import {
  getUserPreferences,
  updateUserPreferences,
} from '@/lib/personalization/userPreferencesApi';

export type UserPreferences = UserPreferencesUi & {
  use24HourFormat: boolean;
};

export { getUserPreferences, updateUserPreferences };

export function useUserPreferences() {
  const { preferences, replacePreferences, isSyncing, setDateFormat, setTimeFormat, setTimeZone, setLanguage } =
    usePersonalization();
  const { setFormatLocale, setTextLocale, textLocale } = useI18n();

  const mapped = useMemo<UserPreferences>(
    () => ({
      defaultPage: preferences.defaultLandingPath,
      dateFormat: preferences.dateFormat,
      timeFormat: preferences.timeFormat,
      timeZone: preferences.timeZone,
      language: preferences.language,
      reducedAnimations: preferences.reducedAnimations,
      use24HourFormat: preferences.timeFormat !== '12h',
    }),
    [preferences]
  );

  const applyPreferences = useCallback(
    (next: Partial<UserPreferences>) => {
      if (next.dateFormat) {
        setDateFormat(next.dateFormat);
        setFormatLocale(resolveFormatLocaleForDateFormat(next.dateFormat, textLocale));
      }
      if (next.timeFormat) {
        setTimeFormat(next.timeFormat);
      } else if (next.use24HourFormat !== undefined) {
        setTimeFormat(next.use24HourFormat ? '24h' : '12h');
      }
      if (next.timeZone) {
        setTimeZone(next.timeZone);
      }
      if (next.language) {
        setLanguage(next.language);
        setTextLocale(next.language);
      }
    },
    [
      setDateFormat,
      setTimeFormat,
      setTimeZone,
      setLanguage,
      setFormatLocale,
      setTextLocale,
      textLocale,
    ]
  );

  const updatePreferences = useCallback(
    (
      patch: Partial<{
        dateFormat: DateFormatPattern;
        timeFormat: TimeFormatPreference;
        timeZone: UserTimeZone;
        language: PreferenceLanguage;
        use24HourFormat: boolean;
        reducedAnimations: boolean;
        defaultPage: UserPreferences['defaultPage'];
      }>
    ) => {
      const nextTimeFormat =
        patch.timeFormat ??
        (patch.use24HourFormat === undefined
          ? undefined
          : patch.use24HourFormat
            ? '24h'
            : '12h');

      replacePreferences({
        ...preferences,
        dateFormat: patch.dateFormat ?? preferences.dateFormat,
        timeFormat: nextTimeFormat ?? preferences.timeFormat,
        timeZone: patch.timeZone ?? preferences.timeZone,
        language: patch.language ?? preferences.language,
        reducedAnimations: patch.reducedAnimations ?? preferences.reducedAnimations,
        defaultLandingPath: patch.defaultPage ?? preferences.defaultLandingPath,
      });

      applyPreferences({
        dateFormat: patch.dateFormat,
        timeFormat: nextTimeFormat,
        timeZone: patch.timeZone,
        language: patch.language,
        use24HourFormat: patch.use24HourFormat,
      });
    },
    [preferences, replacePreferences, applyPreferences]
  );

  return {
    preferences: mapped,
    isLoading: isSyncing && !preferences,
    updatePreferences,
    isUpdating: isSyncing,
    applyPreferences,
  };
}
