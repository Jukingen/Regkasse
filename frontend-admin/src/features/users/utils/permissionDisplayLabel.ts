import { USER_FACING_MISSING_TRANSLATION_LABEL } from '@/i18n/translationFallback';

/**
 * Maps permission code (e.g. fiscal.export.compliance, daily-closing.view) to i18n leaf
 * under users.roleDrawer.permissionLabels. Dots and hyphens become underscores.
 */
export function permissionCodeToLabelLeaf(code: string): string {
  return code.replace(/[.-]/g, '_');
}

/**
 * Resolves a human-readable permission label; falls back to the raw permission code when no translation exists.
 */
export function resolvePermissionDisplayLabel(
  code: string,
  t: (key: string, options?: Record<string, string | number>) => string
): string {
  const leaf = permissionCodeToLabelLeaf(code);
  const resolved = t(`users.roleDrawer.permissionLabels.${leaf}`);
  if (resolved === USER_FACING_MISSING_TRANSLATION_LABEL) return code;
  return resolved;
}

/**
 * Resolves a catalog group slug to a localized label.
 * Falls back to a readable slug when the i18n key is missing (avoids "Übersetzung nicht verfügbar").
 */
export function resolvePermissionGroupLabel(
  groupSlug: string,
  t: (key: string, options?: Record<string, string | number>) => string
): string {
  const slug = groupSlug.trim() || 'other';
  const resolved = t(`users.roleDrawer.groups.${slug}`);
  if (resolved !== USER_FACING_MISSING_TRANSLATION_LABEL) return resolved;
  return slug.replace(/_/g, ' ');
}
