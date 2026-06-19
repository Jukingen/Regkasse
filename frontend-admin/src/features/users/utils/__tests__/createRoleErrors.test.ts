import { beforeEach, describe, expect, it, vi } from 'vitest';

import { clearApiErrorCodeRegistryForTests } from '@/shared/errors/apiErrorCodeRegistry';
import { getCreateRoleErrorMessage } from '../createRoleErrors';

describe('getCreateRoleErrorMessage', () => {
    const t = (key: string) => key;

    beforeEach(() => {
        clearApiErrorCodeRegistryForTests();
    });

    it('maps ROLE_ALREADY_EXISTS to i18n key', () => {
        const message = getCreateRoleErrorMessage(t, {
            response: { data: { code: 'ROLE_ALREADY_EXISTS', message: 'Role already exists' } },
        });
        expect(message).toBe('users.createRole.errors.alreadyExists');
    });

    it('maps INHERIT_SUPERADMIN_FORBIDDEN to i18n key', () => {
        const message = getCreateRoleErrorMessage(t, {
            response: { data: { code: 'INHERIT_SUPERADMIN_FORBIDDEN' } },
        });
        expect(message).toBe('users.createRole.errors.inheritSuperAdminForbidden');
    });

    it('falls back to generic key for unknown errors', () => {
        const message = getCreateRoleErrorMessage(t, new Error('boom'));
        expect(message).toBe('users.createRole.errors.generic');
    });
});
