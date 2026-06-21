/**
 * Users modülü form validasyonu – backend contract ile hizalı (UserManagementController DTOs).
 * Tek kaynak: min/max uzunluk ve kurallar.
 *
 * Backend password policy (single source of truth): Program.cs
 *   options.Password.RequiredLength = 8;
 *   options.Password.RequireDigit = true;
 *   options.Password.RequireLowercase = true;
 *   options.Password.RequireUppercase = true;
 *   options.Password.RequireNonAlphanumeric = true;
 */

/** Backend: Identity Password.RequiredLength (Program.cs) */
export const PASSWORD_MIN_LENGTH = 8;
/** Backend: Identity default max; DTOs align. */
export const PASSWORD_MAX_LENGTH = 128;

/** Mirrors backend Identity options for UI copy and client-side validation. */
export const PASSWORD_POLICY = {
  minLength: PASSWORD_MIN_LENGTH,
  maxLength: PASSWORD_MAX_LENGTH,
  requireDigit: true,
  requireLowercase: true,
  requireUppercase: true,
  requireNonAlphanumeric: true,
} as const;

/** Backend: UserName, FirstName, LastName */
export const NAME_MAX_LENGTH = 50;

/** PATCH /api/admin/users/{id}/username — login name (not email). */
export const LOGIN_USERNAME_MIN_LENGTH = 3;
export const LOGIN_USERNAME_MAX_LENGTH = 50;
export const LOGIN_USERNAME_PATTERN = /^[a-zA-Z0-9_-]+$/;

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

export type LoginUserNameRuleMessages = {
  required: string;
  min: string;
  max: string;
  pattern: string;
};

export type RuleFactoryContext = {
  requiredMessage: string;
  emailInvalidMessage: string;
  passwordMinMessage: string;
  /** Single message when password fails policy (uppercase, lowercase, digit, special). */
  passwordPolicyMessage: string;
  maxLengthMessage: (max: number) => string;
  reasonRequiredMessage?: string;
  roleNameRequiredMessage?: string;
  /** When set, create-user username field uses login username rules (min/pattern). */
  loginUserNameMessages?: LoginUserNameRuleMessages;
};

export type UsersFormTranslate = (key: string, options?: Record<string, string | number>) => string;

/** Builds locale-aware validation messages for user forms. */
export function buildUsersFormRulesContext(t: UsersFormTranslate): RuleFactoryContext {
  return {
    requiredMessage: t('users.formValidation.required'),
    emailInvalidMessage: t('users.formValidation.emailInvalid'),
    passwordMinMessage: t('users.formValidation.passwordMin', { min: PASSWORD_MIN_LENGTH }),
    passwordPolicyMessage: t('users.formValidation.passwordPolicy'),
    maxLengthMessage: (max: number) => t('users.formValidation.maxLength', { max }),
    reasonRequiredMessage: t('users.formValidation.reasonRequired'),
    roleNameRequiredMessage: t('users.formValidation.roleNameRequired'),
    loginUserNameMessages: {
      required: t('users.username.validation.required'),
      min: t('users.username.validation.min'),
      max: t('users.username.validation.max'),
      pattern: t('users.username.validation.pattern'),
    },
  };
}

/** Maps backend Identity password errors to localized validation messages. */
export function mapBackendPasswordError(t: UsersFormTranslate, backendMessage: string): string {
  const lower = backendMessage.toLowerCase();
  if (lower.includes('at least') && lower.includes('character')) {
    return t('users.passwordErrors.minLength', { min: PASSWORD_MIN_LENGTH });
  }
  if (lower.includes('digit') || lower.includes('number')) return t('users.passwordErrors.digit');
  if (lower.includes('lowercase') || lower.includes('lower case')) return t('users.passwordErrors.lowercase');
  if (lower.includes('uppercase') || lower.includes('upper case')) return t('users.passwordErrors.uppercase');
  if (lower.includes('non-alphanumeric') || lower.includes('non alphanumeric') || lower.includes('special')) {
    return t('users.passwordErrors.nonAlphanumeric');
  }
  return t('users.passwordErrors.generic');
}

/** Returns first policy violation message or null if valid. Aligns with backend Identity. */
export function getPasswordPolicyError(
  value: string | undefined | null,
  policyMessage: string
): string | null {
  if (value == null || value.length === 0) return null;
  if (value.length < PASSWORD_POLICY.minLength) return null; // handled by min rule
  if (PASSWORD_POLICY.requireDigit && !/\d/.test(value)) return policyMessage;
  if (PASSWORD_POLICY.requireLowercase && !/[a-z]/.test(value)) return policyMessage;
  if (PASSWORD_POLICY.requireUppercase && !/[A-Z]/.test(value)) return policyMessage;
  if (PASSWORD_POLICY.requireNonAlphanumeric && !/[^A-Za-z0-9]/.test(value)) return policyMessage;
  return null;
}

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
/** Ant Design rules for admin username change (aligned with UpdateUsernameRequest). */
export function createLoginUserNameRules(messages: LoginUserNameRuleMessages) {
  return [
    { required: true, message: messages.required },
    { min: LOGIN_USERNAME_MIN_LENGTH, message: messages.min },
    { max: LOGIN_USERNAME_MAX_LENGTH, message: messages.max },
    { pattern: LOGIN_USERNAME_PATTERN, message: messages.pattern },
  ];
}

export function createUsersFormRules(copy: RuleFactoryContext) {
  const userNameRules = copy.loginUserNameMessages
    ? createLoginUserNameRules(copy.loginUserNameMessages)
    : [
        { required: true, message: copy.requiredMessage },
        maxLen(NAME_MAX_LENGTH, copy.maxLengthMessage(NAME_MAX_LENGTH)),
      ];

  return {
    userName: userNameRules,
    password: [
      { required: true, message: copy.requiredMessage },
      { min: PASSWORD_MIN_LENGTH, message: copy.passwordMinMessage },
      maxLen(PASSWORD_MAX_LENGTH, copy.maxLengthMessage(PASSWORD_MAX_LENGTH)),
      {
        validator: (_: unknown, value: string | undefined) => {
          const err = getPasswordPolicyError(value, copy.passwordPolicyMessage);
          return err ? Promise.reject(new Error(err)) : Promise.resolve();
        },
      },
    ],
    newPassword: [
      { required: true, message: copy.requiredMessage },
      { min: PASSWORD_MIN_LENGTH, message: copy.passwordMinMessage },
      maxLen(PASSWORD_MAX_LENGTH, copy.maxLengthMessage(PASSWORD_MAX_LENGTH)),
      {
        validator: (_: unknown, value: string | undefined) => {
          const err = getPasswordPolicyError(value, copy.passwordPolicyMessage);
          return err ? Promise.reject(new Error(err)) : Promise.resolve();
        },
      },
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
    employeeNumber: [
      { required: true, message: copy.requiredMessage },
      maxLen(SHORT_FIELD_MAX_LENGTH, copy.maxLengthMessage(SHORT_FIELD_MAX_LENGTH)),
    ],
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
