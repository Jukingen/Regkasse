import { customInstance } from '@/lib/axios';

import { normalizeThemeMode } from './theme';
import type {
  DateFormatPattern,
  DefaultLandingPath,
  DensityMode,
  PreferenceLanguage,
  PersonalizationPreferences,
  UserTimeZone,
} from './types';
import {
  DATE_FORMAT_PATTERNS,
  DEFAULT_LANDING_PATHS,
  DEFAULT_PERSONALIZATION,
  PREFERENCE_LANGUAGES,
  TIME_ZONE_OPTIONS,
} from './types';

export type UserPreferencesApiResponse = {
  themeMode: string;
  densityMode: string;
  defaultPage: string;
  dateFormat?: string | null;
  timeFormat?: string | null;
  timeZone?: string | null;
  language?: string | null;
  reducedAnimations: boolean;
  updatedAtUtc?: string | null;
};

export type SaveUserPreferencesApiRequest = {
  themeMode: string;
  densityMode: string;
  defaultPage: string;
  dateFormat?: string | null;
  timeFormat?: string | null;
  timeZone?: string | null;
  language?: string | null;
  reducedAnimations: boolean;
};

export const userPreferencesQueryKey = ['user-preferences'] as const;

export async function fetchUserPreferences(): Promise<UserPreferencesApiResponse> {
  return customInstance<UserPreferencesApiResponse>({
    url: '/api/admin/user/preferences',
    method: 'GET',
  });
}

export async function saveUserPreferences(
  body: SaveUserPreferencesApiRequest
): Promise<UserPreferencesApiResponse> {
  return customInstance<UserPreferencesApiResponse>({
    url: '/api/admin/user/preferences',
    method: 'PUT',
    data: body,
  });
}

/** Aliases used by preferences feature modules. */
export const getUserPreferences = fetchUserPreferences;
export const updateUserPreferences = saveUserPreferences;

function normalizeDensityFromApi(value: string | undefined): DensityMode {
  if (value === 'comfortable' || value === 'standard' || value === 'compact') return value;
  return DEFAULT_PERSONALIZATION.density;
}

function normalizeLandingFromApi(value: string | undefined): DefaultLandingPath {
  const v = value === '/users' ? '/admin/users' : value;
  if (typeof v === 'string' && (DEFAULT_LANDING_PATHS as readonly string[]).includes(v)) {
    return v as DefaultLandingPath;
  }
  return DEFAULT_PERSONALIZATION.defaultLandingPath;
}

export function normalizeDateFormatFromApi(value: string | null | undefined): DateFormatPattern {
  if (value === 'de-AT' || value === 'tr-TR') return 'DD.MM.YYYY';
  if (value === 'en-US') return 'MM/DD/YYYY';
  if (value && (DATE_FORMAT_PATTERNS as readonly string[]).includes(value)) {
    return value as DateFormatPattern;
  }
  return DEFAULT_PERSONALIZATION.dateFormat;
}

export function normalizeTimeZoneFromApi(value: string | null | undefined): UserTimeZone {
  if (value && (TIME_ZONE_OPTIONS as readonly string[]).includes(value)) {
    return value as UserTimeZone;
  }
  return DEFAULT_PERSONALIZATION.timeZone;
}

export function normalizeLanguageFromApi(value: string | null | undefined): PreferenceLanguage {
  if (value && (PREFERENCE_LANGUAGES as readonly string[]).includes(value)) {
    return value as PreferenceLanguage;
  }
  return DEFAULT_PERSONALIZATION.language;
}

export function mapApiToPersonalization(
  api: UserPreferencesApiResponse
): PersonalizationPreferences {
  const timeFormat = api.timeFormat === '12h' || api.timeFormat === '24h' ? api.timeFormat : '24h';
  return {
    themeMode: normalizeThemeMode(api.themeMode),
    density: normalizeDensityFromApi(api.densityMode),
    defaultLandingPath: normalizeLandingFromApi(api.defaultPage),
    dateFormat: normalizeDateFormatFromApi(api.dateFormat),
    timeFormat,
    timeZone: normalizeTimeZoneFromApi(api.timeZone),
    language: normalizeLanguageFromApi(api.language),
    reducedAnimations: api.reducedAnimations === true,
  };
}

export function mapPersonalizationToApi(
  prefs: PersonalizationPreferences
): SaveUserPreferencesApiRequest {
  return {
    themeMode: prefs.themeMode,
    densityMode: prefs.density,
    defaultPage: prefs.defaultLandingPath,
    dateFormat: prefs.dateFormat,
    timeFormat: prefs.timeFormat,
    timeZone: prefs.timeZone,
    language: prefs.language,
    reducedAnimations: prefs.reducedAnimations,
  };
}
