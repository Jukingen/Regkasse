/**
 * Sidebar subtitles live under `nav.subtitle.*` (parallel to `nav.*` labels).
 * Flat label keys cannot nest `.subtitle` without restructuring labels.
 */
import { isAdminTranslationKey } from '@/i18n/translationKey';
import { USER_FACING_MISSING_TRANSLATION_LABEL } from '@/i18n/translationFallback';

const NON_NAV_LABEL_SUBTITLE: Record<string, string> = {
  'settings.tabs.tse': 'nav.subtitle.settingsTse',
  'settings.tabs.finanzOnline': 'nav.subtitle.settingsFinanzOnline',
  'settings.manager.advanced.backup': 'nav.subtitle.settingsBackup',
};

/** Resolve i18n key for a sidebar labelKey, or null when not applicable. */
export function sidebarSubtitleKey(labelKey: string): string | null {
  if (labelKey.startsWith('nav.')) {
    return `nav.subtitle.${labelKey.slice('nav.'.length)}`;
  }
  return NON_NAV_LABEL_SUBTITLE[labelKey] ?? null;
}

/** Translate subtitle when the key exists; otherwise undefined. */
export function resolveSidebarSubtitle(
  t: (key: string) => string,
  labelKey: string
): string | undefined {
  const key = sidebarSubtitleKey(labelKey);
  if (!key || !isAdminTranslationKey(key)) return undefined;
  const value = t(key);
  if (!value || value === USER_FACING_MISSING_TRANSLATION_LABEL) return undefined;
  return value;
}
