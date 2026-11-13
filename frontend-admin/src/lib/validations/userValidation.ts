/**
 * User create/edit / username / role form validation.
 * Backend: UserManagementController DTOs + Identity username rules (AGENTS.md).
 */
import type { Rule } from 'antd/es/form';

import {
  USERNAME_CHAR_PATTERN,
  USERNAME_MAX_LENGTH,
  USERNAME_MIN_LENGTH,
  maxLengthRule,
} from '@/lib/validations/common';
import {
  PASSWORD_MAX_LENGTH,
  PASSWORD_MIN_LENGTH,
  createPasswordFormRules,
} from '@/lib/validations/passwordValidation';

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

/** @deprecated Import from `@/lib/validations/common` — kept for callers expecting user module names. */
export {
  isValidEmail,
  USERNAME_MAX_LENGTH as LOGIN_USERNAME_MAX_LENGTH,
  USERNAME_MIN_LENGTH as LOGIN_USERNAME_MIN_LENGTH,
  USERNAME_CHAR_PATTERN as LOGIN_USERNAME_PATTERN,
} from '@/lib/validations/common';

/** @deprecated Import from `@/lib/validations/passwordValidation`. */
export {
  getPasswordPolicyError,
  mapBackendPasswordError,
  PASSWORD_MAX_LENGTH,
  PASSWORD_MIN_LENGTH,
  PASSWORD_POLICY,
} from '@/lib/validations/passwordValidation';

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

/** Ant Design rules for admin username change (aligned with UpdateUsernameRequest). */
export function createLoginUserNameRules(messages: LoginUserNameRuleMessages): Rule[] {
  return [
    { required: true, message: messages.required },
    { min: USERNAME_MIN_LENGTH, message: messages.min },
    { max: USERNAME_MAX_LENGTH, message: messages.max },
    { pattern: USERNAME_CHAR_PATTERN, message: messages.pattern },
  ];
}

export function createUsersFormRules(copy: RuleFactoryContext) {
  const userNameRules = copy.loginUserNameMessages
    ? createLoginUserNameRules(copy.loginUserNameMessages)
    : [
        { required: true, message: copy.requiredMessage },
        maxLengthRule(NAME_MAX_LENGTH, copy.maxLengthMessage(NAME_MAX_LENGTH)),
      ];

  const passwordRules = createPasswordFormRules({
    required: copy.requiredMessage,
    min: copy.passwordMinMessage,
    max: copy.maxLengthMessage(PASSWORD_MAX_LENGTH),
    policy: copy.passwordPolicyMessage,
  });

  return {
    userName: userNameRules,
    password: passwordRules,
    newPassword: passwordRules,
    firstName: [
      { required: true, message: copy.requiredMessage },
      maxLengthRule(NAME_MAX_LENGTH, copy.maxLengthMessage(NAME_MAX_LENGTH)),
    ],
    lastName: [
      { required: true, message: copy.requiredMessage },
      maxLengthRule(NAME_MAX_LENGTH, copy.maxLengthMessage(NAME_MAX_LENGTH)),
    ],
    email: [
      { type: 'email' as const, message: copy.emailInvalidMessage },
      maxLengthRule(EMAIL_MAX_LENGTH, copy.maxLengthMessage(EMAIL_MAX_LENGTH)),
    ],
    employeeNumber: [
      { required: true, message: copy.requiredMessage },
      maxLengthRule(SHORT_FIELD_MAX_LENGTH, copy.maxLengthMessage(SHORT_FIELD_MAX_LENGTH)),
    ],
    role: [
      { required: true, message: copy.requiredMessage },
      maxLengthRule(SHORT_FIELD_MAX_LENGTH, copy.maxLengthMessage(SHORT_FIELD_MAX_LENGTH)),
    ],
    taxNumber: [
      maxLengthRule(SHORT_FIELD_MAX_LENGTH, copy.maxLengthMessage(SHORT_FIELD_MAX_LENGTH)),
    ],
    notes: [maxLengthRule(NOTES_MAX_LENGTH, copy.maxLengthMessage(NOTES_MAX_LENGTH))],
    reason: [
      { required: true, message: copy.reasonRequiredMessage ?? copy.requiredMessage },
      maxLengthRule(NOTES_MAX_LENGTH, copy.maxLengthMessage(NOTES_MAX_LENGTH)),
    ],
    roleName: [
      { required: true, message: copy.roleNameRequiredMessage ?? copy.requiredMessage },
      maxLengthRule(ROLE_NAME_MAX_LENGTH, copy.maxLengthMessage(ROLE_NAME_MAX_LENGTH)),
    ],
  };
}
