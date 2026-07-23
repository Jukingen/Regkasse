import { normalizeThemeMode } from './theme';
import {
  DATE_FORMAT_PATTERNS,
  DEFAULT_LANDING_PATHS,
  DEFAULT_PERSONALIZATION,
  PREFERENCE_LANGUAGES,
  TIME_ZONE_OPTIONS,
  type DateFormatPattern,
  type DefaultLandingPath,
  type DensityMode,
  type PreferenceLanguage,
  type PersonalizationPreferences,
  type ThemeMode,
  type UserTimeZone,
} from './types';

export const PERSONALIZATION_STORAGE_KEY = 'regkasse.admin.personalization.v1';

/** Legacy quick-read key mirrored when theme changes. */
export const THEME_MODE_STORAGE_KEY = 'themeMode';

const LEGACY_LANDING: Record<string, DefaultLandingPath> = {
  '/users': '/admin/users',
  '/receipts': '/dashboard',
  '/settings': '/dashboard',
};

function normalizeDensity(value: unknown): DensityMode {
  if (value === 'comfortable' || value === 'standard' || value === 'compact') return value;
  return DEFAULT_PERSONALIZATION.density;
}

function normalizeTimeFormat(value: unknown): PersonalizationPreferences['timeFormat'] {
  return value === '12h' ? '12h' : '24h';
}

function normalizeDefaultLandingPath(value: unknown): DefaultLandingPath {
  if (typeof value === 'string') {
    const legacy = LEGACY_LANDING[value];
    if (legacy) return legacy;
    if ((DEFAULT_LANDING_PATHS as readonly string[]).includes(value)) {
      return value as DefaultLandingPath;
    }
  }
  return DEFAULT_PERSONALIZATION.defaultLandingPath;
}

function normalizeDateFormat(value: unknown, legacyPreset?: unknown): DateFormatPattern {
  if (legacyPreset === 'en-US' || value === 'en-US') return 'MM/DD/YYYY';
  if (legacyPreset === 'de-AT' || legacyPreset === 'tr-TR') return 'DD.MM.YYYY';
  if (typeof value === 'string' && (DATE_FORMAT_PATTERNS as readonly string[]).includes(value)) {
    return value as DateFormatPattern;
  }
  return DEFAULT_PERSONALIZATION.dateFormat;
}

function normalizeTimeZone(value: unknown): UserTimeZone {
  if (typeof value === 'string' && (TIME_ZONE_OPTIONS as readonly string[]).includes(value)) {
    return value as UserTimeZone;
  }
  return DEFAULT_PERSONALIZATION.timeZone;
}

function normalizeLanguage(value: unknown): PreferenceLanguage {
  if (typeof value === 'string' && (PREFERENCE_LANGUAGES as readonly string[]).includes(value)) {
    return value as PreferenceLanguage;
  }
  return DEFAULT_PERSONALIZATION.language;
}

export function normalizePersonalization(raw: unknown): PersonalizationPreferences {
  if (!raw || typeof raw !== 'object') return { ...DEFAULT_PERSONALIZATION };
  const o = raw as Record<string, unknown>;
  return {
    themeMode: normalizeThemeMode(o.themeMode) as ThemeMode,
    density: normalizeDensity(o.density),
    defaultLandingPath: normalizeDefaultLandingPath(o.defaultLandingPath ?? o.defaultPage),
    dateFormat: normalizeDateFormat(o.dateFormat, o.dateTimeFormatPreset),
    timeFormat: normalizeTimeFormat(o.timeFormat),
    timeZone: normalizeTimeZone(o.timeZone),
    language: normalizeLanguage(o.language),
    reducedAnimations: o.reducedAnimations === true,
  };
}

export function readStoredPersonalization(): PersonalizationPreferences {
  if (typeof window === 'undefined') return { ...DEFAULT_PERSONALIZATION };
  try {
    const raw = window.localStorage.getItem(PERSONALIZATION_STORAGE_KEY);
    if (!raw) return { ...DEFAULT_PERSONALIZATION };
    return normalizePersonalization(JSON.parse(raw));
  } catch {
    return { ...DEFAULT_PERSONALIZATION };
  }
}

export function writeStoredPersonalization(prefs: PersonalizationPreferences): void {
  if (typeof window === 'undefined') return;
  try {
    window.localStorage.setItem(PERSONALIZATION_STORAGE_KEY, JSON.stringify(prefs));
  } catch {
    /* restricted storage */
  }
}

export function patchStoredPersonalization(
  patch: Partial<PersonalizationPreferences>
): PersonalizationPreferences {
  const next = { ...readStoredPersonalization(), ...patch };
  writeStoredPersonalization(next);
  return next;
}
