import { describe, expect, it, vi } from 'vitest';

import { USER_FACING_MISSING_TRANSLATION_LABEL } from '@/i18n/translationFallback';

import {
  permissionCodeToLabelLeaf,
  resolvePermissionDisplayLabel,
  resolvePermissionGroupLabel,
} from '../permissionDisplayLabel';

describe('permissionDisplayLabel', () => {
  it('maps code to i18n leaf with underscores for dots and hyphens', () => {
    expect(permissionCodeToLabelLeaf('fiscal.export.compliance')).toBe('fiscal_export_compliance');
    expect(permissionCodeToLabelLeaf('voucher.read')).toBe('voucher_read');
    expect(permissionCodeToLabelLeaf('cash_register.view')).toBe('cash_register_view');
    expect(permissionCodeToLabelLeaf('user.view')).toBe('user_view');
    expect(permissionCodeToLabelLeaf('daily-closing.view')).toBe('daily_closing_view');
    expect(permissionCodeToLabelLeaf('rksv.test-helper')).toBe('rksv_test_helper');
    expect(permissionCodeToLabelLeaf('rksv.tse-simulation')).toBe('rksv_tse_simulation');
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

  it('resolves group labels and falls back to readable slug', () => {
    const t = vi.fn((key: string) =>
      key === 'users.roleDrawer.groups.mitarbeiter'
        ? 'Mitarbeiter'
        : USER_FACING_MISSING_TRANSLATION_LABEL
    );
    expect(resolvePermissionGroupLabel('mitarbeiter', t)).toBe('Mitarbeiter');
    expect(resolvePermissionGroupLabel('unknown_group', t)).toBe('unknown group');
  });
});
