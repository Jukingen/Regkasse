/**
 * Users modülü form validasyonu – backend contract ile hizalı (UserManagementController DTOs).
 * Tek kaynak: min/max uzunluk ve kurallar.
 */

/** Backend: CreateUserRequest / ResetPasswordRequest */
export const PASSWORD_MIN_LENGTH = 6;

/** Backend: UserName, FirstName, LastName */
export const NAME_MAX_LENGTH = 50;

/** Backend: Email */
export const EMAIL_MAX_LENGTH = 100;

/** Backend: EmployeeNumber, Role, TaxNumber */
export const SHORT_FIELD_MAX_LENGTH = 20;

/** Backend: Notes, Deactivation reason */
export const NOTES_MAX_LENGTH = 500;

/** Backend: CreateRoleRequest.Name */
export const ROLE_NAME_MAX_LENGTH = 50;

/** Basit email regex (backend EmailAddress attribute ile uyumlu) */
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export type RuleFactoryContext = {
  requiredMessage: string;
  emailInvalidMessage: string;
  passwordMinMessage: string;
  maxLengthMessage: (max: number) => string;
  reasonRequiredMessage?: string;
  roleNameRequiredMessage?: string;
};

/** String max-length validator (Ant Design Rule). */
function maxLen(max: number, message: string) {
  return {
    validator: (_: unknown, value: string | undefined) =>
      value == null || value.length <= max ? Promise.resolve() : Promise.reject(new Error(message)),
  };
}

/**
 * Ant Design Form rule objeleri için factory.
 * copy (messages) dışarıdan verilir; validasyon sabitleri burada.
 */
export function createUsersFormRules(copy: RuleFactoryContext) {
  return {
    userName: [
      { required: true, message: copy.requiredMessage },
      maxLen(NAME_MAX_LENGTH, copy.maxLengthMessage(NAME_MAX_LENGTH)),
    ],
    password: [
      { required: true, message: copy.requiredMessage },
      { min: PASSWORD_MIN_LENGTH, message: copy.passwordMinMessage },
      maxLen(128, copy.maxLengthMessage(128)),
    ],
    newPassword: [
      { required: true, message: copy.requiredMessage },
      { min: PASSWORD_MIN_LENGTH, message: copy.passwordMinMessage },
      maxLen(128, copy.maxLengthMessage(128)),
    ],
    firstName: [
      { required: true, message: copy.requiredMessage },
      maxLen(NAME_MAX_LENGTH, copy.maxLengthMessage(NAME_MAX_LENGTH)),
    ],
    lastName: [
      { required: true, message: copy.requiredMessage },
      maxLen(NAME_MAX_LENGTH, copy.maxLengthMessage(NAME_MAX_LENGTH)),
    ],
    email: [
      { type: 'email' as const, message: copy.emailInvalidMessage },
      maxLen(EMAIL_MAX_LENGTH, copy.maxLengthMessage(EMAIL_MAX_LENGTH)),
    ],
    employeeNumber: [maxLen(SHORT_FIELD_MAX_LENGTH, copy.maxLengthMessage(SHORT_FIELD_MAX_LENGTH))],
    role: [
      { required: true, message: copy.requiredMessage },
      maxLen(SHORT_FIELD_MAX_LENGTH, copy.maxLengthMessage(SHORT_FIELD_MAX_LENGTH)),
    ],
    taxNumber: [maxLen(SHORT_FIELD_MAX_LENGTH, copy.maxLengthMessage(SHORT_FIELD_MAX_LENGTH))],
    notes: [maxLen(NOTES_MAX_LENGTH, copy.maxLengthMessage(NOTES_MAX_LENGTH))],
    reason: [
      { required: true, message: copy.reasonRequiredMessage ?? copy.requiredMessage },
      maxLen(NOTES_MAX_LENGTH, copy.maxLengthMessage(NOTES_MAX_LENGTH)),
    ],
    roleName: [
      { required: true, message: copy.roleNameRequiredMessage ?? copy.requiredMessage },
      maxLen(ROLE_NAME_MAX_LENGTH, copy.maxLengthMessage(ROLE_NAME_MAX_LENGTH)),
    ],
  };
}

/** Email pattern (programatik kontrol için) */
export function isValidEmail(value: string | undefined | null): boolean {
  if (value == null || value.trim() === '') return true;
  return EMAIL_PATTERN.test(value.trim());
}
