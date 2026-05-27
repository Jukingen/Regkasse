import { customInstance } from '@/lib/axios';
import type { PersonalizationPreferences } from './types';
import type {
  DateFormatPattern,
  DefaultLandingPath,
  DensityMode,
} from './types';
import { normalizeThemeMode } from './theme';
import { DATE_FORMAT_PATTERNS, DEFAULT_PERSONALIZATION, DEFAULT_LANDING_PATHS } from './types';

export type UserPreferencesApiResponse = {
  themeMode: string;
  densityMode: string;
  defaultPage: string;
  dateFormat?: string | null;
  timeFormat?: string | null;
  reducedAnimations: boolean;
  updatedAtUtc?: string | null;
};

export type SaveUserPreferencesApiRequest = {
  themeMode: string;
  densityMode: string;
  defaultPage: string;
  dateFormat?: string | null;
  timeFormat?: string | null;
  reducedAnimations: boolean;
};

export const userPreferencesQueryKey = ['admin', 'user', 'preferences'] as const;

export async function fetchUserPreferences(): Promise<UserPreferencesApiResponse> {
  return customInstance<UserPreferencesApiResponse>({
    url: '/api/admin/user/preferences',
    method: 'GET',
  });
}

export async function saveUserPreferences(
  body: SaveUserPreferencesApiRequest,
): Promise<UserPreferencesApiResponse> {
  return customInstance<UserPreferencesApiResponse>({
    url: '/api/admin/user/preferences',
    method: 'PUT',
    data: body,
  });
}

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

function normalizeDateFormatFromApi(value: string | null | undefined): DateFormatPattern {
  if (value && (DATE_FORMAT_PATTERNS as readonly string[]).includes(value)) {
    return value as DateFormatPattern;
  }
  if (value === 'de-AT') return 'DD.MM.YYYY';
  if (value === 'en-US') return 'MM/DD/YYYY';
  if (value === 'tr-TR') return 'DD.MM.YYYY';
  return DEFAULT_PERSONALIZATION.dateFormat;
}

export function mapApiToPersonalization(api: UserPreferencesApiResponse): PersonalizationPreferences {
  const timeFormat = api.timeFormat === '12h' || api.timeFormat === '24h' ? api.timeFormat : '24h';
  return {
    themeMode: normalizeThemeMode(api.themeMode),
    density: normalizeDensityFromApi(api.densityMode),
    defaultLandingPath: normalizeLandingFromApi(api.defaultPage),
    dateFormat: normalizeDateFormatFromApi(api.dateFormat),
    timeFormat,
    reducedAnimations: api.reducedAnimations === true,
  };
}

export function mapPersonalizationToApi(prefs: PersonalizationPreferences): SaveUserPreferencesApiRequest {
  return {
    themeMode: prefs.themeMode,
    densityMode: prefs.density,
    defaultPage: prefs.defaultLandingPath,
    dateFormat: prefs.dateFormat,
    timeFormat: prefs.timeFormat,
    reducedAnimations: prefs.reducedAnimations,
  };
}
