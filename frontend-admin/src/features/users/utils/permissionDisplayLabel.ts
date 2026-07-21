import { USER_FACING_MISSING_TRANSLATION_LABEL } from '@/i18n/translationFallback';

/** Maps permission code (e.g. fiscal.export.compliance) to i18n leaf under users.roleDrawer.permissionLabels. */
export function permissionCodeToLabelLeaf(code: string): string {
  return code.replace(/\./g, '_');
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
