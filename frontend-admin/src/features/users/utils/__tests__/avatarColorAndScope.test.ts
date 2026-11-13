import { describe, expect, it } from 'vitest';

import { getColorFromEmail, getColorFromName } from '@/features/users/utils/avatarColor';
import {
  activityRoleFilterValues,
  isBusinessTenantSlug,
  isPlatformUserRole,
  roleFilterValuesForActor,
} from '@/features/users/utils/userScope';
import { maskQrPayloadPreview } from '@/features/receipts/utils/maskQrPayloadPreview';

describe('avatarColor', () => {
  it('returns gray for empty name and stable hsl for names', () => {
    expect(getColorFromName('')).toBe('#8c8c8c');
    expect(getColorFromName('   ')).toBe('#8c8c8c');
    const a = getColorFromName('Ada');
    const b = getColorFromName('Ada');
    expect(a).toMatch(/^hsl\(\d+, 55%, 45%\)$/);
    expect(a).toBe(b);
    expect(getColorFromEmail('ada@example.com')).toBe(getColorFromName('ada@example.com'));
  });
});

describe('userScope', () => {
  it('detects platform roles and business tenant slugs', () => {
    expect(isPlatformUserRole(null)).toBe(false);
    expect(isPlatformUserRole('Manager')).toBe(false);
    expect(isPlatformUserRole('SuperAdmin')).toBe(true);
    expect(isPlatformUserRole('PlatformAdmin')).toBe(true);
    expect(isBusinessTenantSlug(null)).toBe(false);
    expect(isBusinessTenantSlug('admin')).toBe(false);
    expect(isBusinessTenantSlug('platform')).toBe(false);
    expect(isBusinessTenantSlug('default')).toBe(true); // leftover slug string is not a special sentinel
    expect(isBusinessTenantSlug('cafe')).toBe(true);
    expect(isBusinessTenantSlug('dev')).toBe(true);
    expect(roleFilterValuesForActor(true)).toContain('SuperAdmin');
    expect(roleFilterValuesForActor(false)).not.toContain('SuperAdmin');
    expect(roleFilterValuesForActor(false)).toContain('Manager');
    expect(activityRoleFilterValues(false)).not.toContain('SuperAdmin');
    expect(activityRoleFilterValues(false)).not.toContain('PlatformAdmin');
    expect(activityRoleFilterValues(false)).toContain('Manager');
    expect(activityRoleFilterValues(true)).toContain('SuperAdmin');
    expect(activityRoleFilterValues(true)).toContain('PlatformAdmin');
  });
});

describe('maskQrPayloadPreview', () => {
  it('handles empty and short payloads', () => {
    expect(maskQrPayloadPreview('')).toBe('');
    expect(maskQrPayloadPreview('short')).toBe('short');
  });

  it('masks long payloads with head and tail', () => {
    const payload = 'A'.repeat(80);
    const masked = maskQrPayloadPreview(payload, 10, 5);
    expect(masked.startsWith('AAAAAAAAAA')).toBe(true);
    expect(masked.endsWith('AAAAA')).toBe(true);
    expect(masked).toContain('…');
    expect(masked.length).toBeLessThan(payload.length);
  });
});
