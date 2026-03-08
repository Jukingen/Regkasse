/**
 * 403 Forbidden – backend ReasonCode ile eşleşen kullanıcı dostu mesajlar (de-DE).
 * Backend ApiError.Forbidden(detail, reasonCode) veya response.data.reasonCode kullanılabilir.
 */

export type ForbiddenReasonCode =
  | 'FORBIDDEN'
  | 'USERS_VIEW_REQUIRED'
  | 'USERS_MANAGE_REQUIRED'
  | 'USERS_EXPORT_REQUIRED'
  | 'USERS_ASSIGN_ROLE_REQUIRED'
  | 'USERS_TRANSFER_BRANCH_REQUIRED'
  | 'SCOPE_BRANCH'
  | string;

const MESSAGES: Record<string, string> = {
  FORBIDDEN: 'Sie haben keine Berechtigung für diese Aktion.',
  USERS_VIEW_REQUIRED: 'Sie haben keine Berechtigung, Benutzer anzuzeigen.',
  USERS_MANAGE_REQUIRED: 'Sie haben keine Berechtigung, Benutzer zu verwalten.',
  USERS_EXPORT_REQUIRED: 'Sie haben keine Berechtigung, Benutzer zu exportieren.',
  USERS_ASSIGN_ROLE_REQUIRED: 'Sie haben keine Berechtigung, Rollen zuzuweisen.',
  USERS_TRANSFER_BRANCH_REQUIRED: 'Sie haben keine Berechtigung, Benutzer zu versetzen.',
  SCOPE_BRANCH: 'Diese Aktion ist nur innerhalb Ihres Standorts erlaubt.',
};

export function getForbiddenMessage(reasonCode?: ForbiddenReasonCode | null): string {
  if (reasonCode && MESSAGES[reasonCode]) return MESSAGES[reasonCode];
  return MESSAGES.FORBIDDEN;
}

/** Map backend requiredPolicy (e.g. UsersView, UsersManage) to reason code for i18n. */
export function mapRequiredPolicyToReasonCode(requiredPolicy?: string | null): string | null {
  if (!requiredPolicy) return null;
  const map: Record<string, string> = {
    UsersView: 'USERS_VIEW_REQUIRED',
    UsersManage: 'USERS_MANAGE_REQUIRED',
    AdminUsers: 'USERS_MANAGE_REQUIRED',
  };
  return map[requiredPolicy] ?? null;
}
