import { describe, expect, it, vi } from 'vitest';

import { USER_FACING_MISSING_TRANSLATION_LABEL } from '@/i18n/translationFallback';

import {
  permissionCodeToLabelLeaf,
  resolvePermissionDisplayLabel,
} from '../permissionDisplayLabel';

describe('permissionDisplayLabel', () => {
  it('maps code to i18n leaf with underscores', () => {
    expect(permissionCodeToLabelLeaf('fiscal.export.compliance')).toBe('fiscal_export_compliance');
    expect(permissionCodeToLabelLeaf('voucher.read')).toBe('voucher_read');
  });

  it('falls back to raw code when translation is missing', () => {
    const t = vi.fn(() => USER_FACING_MISSING_TRANSLATION_LABEL);
    expect(resolvePermissionDisplayLabel('unknown.permission', t)).toBe('unknown.permission');
    expect(t).toHaveBeenCalledWith('users.roleDrawer.permissionLabels.unknown_permission');
  });

  it('returns translated string when t resolves', () => {
    const t = vi.fn(() => 'Gutscheine anzeigen');
    expect(resolvePermissionDisplayLabel('voucher.read', t)).toBe('Gutscheine anzeigen');
    expect(t).toHaveBeenCalledWith('users.roleDrawer.permissionLabels.voucher_read');
  });
});
