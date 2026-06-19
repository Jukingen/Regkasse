import { registerApiErrorCodeTranslation } from '@/shared/errors/apiErrorCodeRegistry';
import { getUserFacingApiErrorMessage, type TranslateFn } from '@/shared/errors/userFacingApiError';

function ensureCreateRoleErrorCodesRegistered(): void {
    registerApiErrorCodeTranslation('ROLE_ALREADY_EXISTS', 'users.createRole.errors.alreadyExists');
    registerApiErrorCodeTranslation('ROLE_NAME_RESERVED', 'users.createRole.errors.reservedName');
    registerApiErrorCodeTranslation('INHERIT_ROLE_NOT_FOUND', 'users.createRole.errors.inheritNotFound');
    registerApiErrorCodeTranslation('INHERIT_SUPERADMIN_FORBIDDEN', 'users.createRole.errors.inheritSuperAdminForbidden');
    registerApiErrorCodeTranslation('ROLE_CREATE_FAILED', 'users.createRole.errors.generic');
}

export function getCreateRoleErrorMessage(t: TranslateFn, error: unknown): string {
    ensureCreateRoleErrorCodesRegistered();
    return getUserFacingApiErrorMessage(t, error, {
        logContext: 'createRole',
        fallbackKey: 'users.createRole.errors.generic',
    });
}
