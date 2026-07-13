import { describe, expect, it } from 'vitest';

import {
    formatRoleBadgeLabel,
    formatRoleDisplayLabel,
    isCanonicalRoleName,
} from '@/features/users/utils/roleDisplayLabel';

const t = (key: string) => key;

describe('roleDisplayLabel', () => {
    it('maps canonical roles to displayNames and badgeLabels', () => {
        expect(formatRoleDisplayLabel(t, 'Manager')).toBe('users.roles.displayNames.Manager');
        expect(formatRoleBadgeLabel(t, 'Manager')).toBe('users.roles.badgeLabels.Manager');
        expect(formatRoleDisplayLabel(t, 'SuperAdmin')).toBe('users.roles.displayNames.SuperAdmin');
        expect(formatRoleBadgeLabel(t, 'SuperAdmin')).toBe('users.roles.badgeLabels.SuperAdmin');
    });

    it('returns custom role names unchanged', () => {
        expect(formatRoleDisplayLabel(t, 'CustomRole')).toBe('CustomRole');
        expect(formatRoleBadgeLabel(t, 'CustomRole')).toBe('CustomRole');
    });

    it('recognizes canonical role names', () => {
        expect(isCanonicalRoleName('Manager')).toBe(true);
        expect(isCanonicalRoleName('TenantAdmin')).toBe(false);
    });
});
