/**
 * Stable license feature ids — must stay aligned with backend `LicenseFeatureIds` (KasseAPI_Final).
 * Used for the development-mode "Aktivierte Features" multi-select.
 */
export const DEVELOPMENT_MODE_FEATURE_IDS = [
  'pos_fiscal',
  'pos_offline',
  'admin_basic',
  'admin_rksv',
  'admin_license_manage',
] as const;

export type DevelopmentModeFeatureId = (typeof DEVELOPMENT_MODE_FEATURE_IDS)[number];
