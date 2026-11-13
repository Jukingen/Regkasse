import { PASSWORD_MIN_LENGTH } from '@/features/users/constants/validation';

export const PASSWORD_REQUIREMENT_KEYS = [
  'minLength',
  'uppercase',
  'lowercase',
  'digit',
  'special',
] as const;

export type PasswordRequirementKey = (typeof PASSWORD_REQUIREMENT_KEYS)[number];

const PASSWORD_REQUIREMENT_TESTS: Record<PasswordRequirementKey, (password: string) => boolean> = {
  minLength: (password) => password.length >= PASSWORD_MIN_LENGTH,
  uppercase: (password) => /[A-Z]/.test(password),
  lowercase: (password) => /[a-z]/.test(password),
  digit: (password) => /\d/.test(password),
  special: (password) => /[^a-zA-Z0-9]/.test(password),
};

export function getMetPasswordRequirementKeys(password: string): PasswordRequirementKey[] {
  return PASSWORD_REQUIREMENT_KEYS.filter((key) => PASSWORD_REQUIREMENT_TESTS[key](password));
}

export function allPasswordRequirementsMet(password: string): boolean {
  return getMetPasswordRequirementKeys(password).length === PASSWORD_REQUIREMENT_KEYS.length;
}

export const PASSWORD_REQUIREMENT_I18N_KEY: Record<PasswordRequirementKey, string> = {
  minLength: 'settings.changePassword.requirementMinLength',
  uppercase: 'settings.changePassword.requirementUppercase',
  lowercase: 'settings.changePassword.requirementLowercase',
  digit: 'settings.changePassword.requirementDigit',
  special: 'settings.changePassword.requirementSpecial',
};
