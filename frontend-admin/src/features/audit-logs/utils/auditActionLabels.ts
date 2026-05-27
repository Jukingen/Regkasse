/** Backend `AuditLogActions` values exposed in the audit log action filter. */
export const AUDIT_ACTION_FILTER_VALUES = [
    'Login',
    'CreateInvoice',
    'Payment',
    'USER_NAME_CHANGE',
    'USER_UPDATE',
    'USER_ROLE_CHANGE',
    'USER_CREATED',
    'USER_DEACTIVATE',
    'USER_REACTIVATE',
    'FORCE_RESET_PASSWORD',
] as const;

export type AuditActionFilter = (typeof AUDIT_ACTION_FILTER_VALUES)[number];

const ACTION_LABEL_KEYS: Record<string, string> = {
    Login: 'common.auditLogs.actionLabels.login',
    CreateInvoice: 'common.auditLogs.actionLabels.createInvoice',
    Payment: 'common.auditLogs.actionLabels.payment',
    USER_NAME_CHANGE: 'common.auditLogs.actionLabels.userNameChange',
    USER_UPDATE: 'common.auditLogs.actionLabels.userUpdate',
    USER_ROLE_CHANGE: 'common.auditLogs.actionLabels.userRoleChange',
    USER_CREATED: 'common.auditLogs.actionLabels.userCreated',
    USER_CREATE: 'common.auditLogs.actionLabels.userCreated',
    USER_DEACTIVATE: 'common.auditLogs.actionLabels.userDeactivate',
    USER_REACTIVATE: 'common.auditLogs.actionLabels.userReactivate',
    FORCE_RESET_PASSWORD: 'common.auditLogs.actionLabels.passwordResetForced',
    USER_PASSWORD_RESET: 'common.auditLogs.actionLabels.passwordResetForced',
    CHANGE_OWN_PASSWORD: 'common.auditLogs.actionLabels.changeOwnPassword',
};

export function getAuditActionLabelKey(action: string | null | undefined): string | null {
    const key = action?.trim();
    if (!key) return null;
    return ACTION_LABEL_KEYS[key] ?? null;
}

export function isAuditActionFilterValue(value: string): value is AuditActionFilter {
    return (AUDIT_ACTION_FILTER_VALUES as readonly string[]).includes(value);
}
