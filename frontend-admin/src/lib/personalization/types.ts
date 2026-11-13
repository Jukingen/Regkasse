export type ThemeMode = 'light' | 'dark' | 'system';

export type ResolvedTheme = 'light' | 'dark';

export type DensityMode = 'comfortable' | 'standard' | 'compact';

export type TimeFormatPreference = '24h' | '12h';

/** User-selectable FA date display patterns. */
export const DATE_FORMAT_PATTERNS = ['DD.MM.YYYY', 'MM/DD/YYYY', 'YYYY-MM-DD'] as const;

export type DateFormatPattern = (typeof DATE_FORMAT_PATTERNS)[number];

export const TIME_ZONE_OPTIONS = [
  'Europe/Vienna',
  'Europe/Berlin',
  'Europe/Zurich',
  'Europe/London',
  'Europe/Istanbul',
  'America/New_York',
  'UTC',
] as const;

export type UserTimeZone = (typeof TIME_ZONE_OPTIONS)[number];

export const PREFERENCE_LANGUAGES = ['de', 'en', 'tr'] as const;

export type PreferenceLanguage = (typeof PREFERENCE_LANGUAGES)[number];

/** Post-login targets (real App Router paths). */
export const DEFAULT_LANDING_PATHS = [
  '/dashboard',
  '/admin/users',
  '/kassenverwaltung',
  '/reporting',
] as const;

export type DefaultLandingPath = (typeof DEFAULT_LANDING_PATHS)[number];

export type PersonalizationPreferences = {
  themeMode: ThemeMode;
  density: DensityMode;
  defaultLandingPath: DefaultLandingPath;
  dateFormat: DateFormatPattern;
  timeFormat: TimeFormatPreference;
  timeZone: UserTimeZone;
  language: PreferenceLanguage;
  reducedAnimations: boolean;
};

export const DEFAULT_PERSONALIZATION: PersonalizationPreferences = {
  themeMode: 'system',
  density: 'standard',
  defaultLandingPath: '/dashboard',
  dateFormat: 'DD.MM.YYYY',
  timeFormat: '24h',
  timeZone: 'Europe/Vienna',
  language: 'de',
  reducedAnimations: false,
};

/** UI-facing slice for {@link useUserPreferences}. */
export type UserPreferencesUi = {
  defaultPage: DefaultLandingPath;
  dateFormat: DateFormatPattern;
  timeFormat: TimeFormatPreference;
  timeZone: UserTimeZone;
  language: PreferenceLanguage;
  reducedAnimations: boolean;
};
