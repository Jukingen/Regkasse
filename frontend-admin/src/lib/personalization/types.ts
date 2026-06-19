export type ThemeMode = 'light' | 'dark' | 'system';

export type ResolvedTheme = 'light' | 'dark';

export type DensityMode = 'comfortable' | 'standard' | 'compact';

export type TimeFormatPreference = '24h' | '12h';

/** Admin UI uses German date display only (independent of text locale). */
export const DATE_FORMAT_PATTERNS = ['DD.MM.YYYY'] as const;

export type DateFormatPattern = (typeof DATE_FORMAT_PATTERNS)[number];

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
  reducedAnimations: boolean;
};

export const DEFAULT_PERSONALIZATION: PersonalizationPreferences = {
  themeMode: 'system',
  density: 'standard',
  defaultLandingPath: '/dashboard',
  dateFormat: 'DD.MM.YYYY',
  timeFormat: '24h',
  reducedAnimations: false,
};

/** UI-facing slice for {@link useUserPreferences}. */
export type UserPreferencesUi = {
  defaultPage: DefaultLandingPath;
  dateFormat: DateFormatPattern;
  timeFormat: TimeFormatPreference;
  reducedAnimations: boolean;
};
