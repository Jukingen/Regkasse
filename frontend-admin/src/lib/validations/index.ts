/**
 * Central form validation schemas for frontend-admin.
 * Domain modules align with AGENTS.md / backend Identity + DTO contracts.
 *
 * Prefer importing from a domain file (e.g. `userValidation`) to avoid ambiguous re-exports.
 */

export {
  BACKUP_RETENTION_DEFAULT_DAYS,
  BACKUP_RETENTION_MAX_DAYS,
  BACKUP_RETENTION_MIN_DAYS,
  type BackupRetentionValidationCode,
  clampBackupRetentionDays,
  isValidBackupRetentionDays,
  validateBackupRetentionDays,
} from '@/lib/validations/backupValidation';
export {
  ATU_TAX_NUMBER_PATTERN,
  EMAIL_PATTERN,
  isValidAtuTaxNumber,
  isValidEmail,
  isValidUsername,
  maxLengthRule,
  USERNAME_CHAR_PATTERN,
  USERNAME_MAX_LENGTH,
  USERNAME_MIN_LENGTH,
  USERNAME_PATTERN,
  type ValidationTranslate,
} from '@/lib/validations/common';
export {
  createValidationRules,
  type ValidationRules,
  validationRules,
} from '@/lib/validations/formRules';
export {
  createLicenseKeyFormRules,
  detectLicenseKeyKind,
  isValidBillingLicenseKey,
  isValidDisplayLicenseKey,
  isValidLicenseKey,
  LICENSE_KEY_DISPLAY_PATTERN,
  type LicenseKeyKind,
  type LicenseKeyRuleMessages,
  type LicenseKeyRuleMode,
  normalizeLicenseKeyInput,
} from '@/lib/validations/licenseValidation';
export {
  allPasswordRequirementsMet,
  createPasswordFormRules,
  getMetPasswordRequirementKeys,
  getPasswordPolicyError,
  isPasswordPolicySatisfied,
  mapBackendPasswordError,
  PASSWORD_MAX_LENGTH,
  PASSWORD_MIN_LENGTH,
  PASSWORD_POLICY,
  PASSWORD_REQUIREMENT_KEYS,
  type PasswordRequirementKey,
  type PasswordRuleMessages,
} from '@/lib/validations/passwordValidation';
export {
  type AddressValidationCode,
  type CompanyNameValidationCode,
  type ContactEmailValidationCode,
  createTenantAdminPasswordRules,
  isOptionalFieldValid,
  type PhoneValidationCode,
  TENANT_ADDRESS_MAX,
  TENANT_COMPANY_NAME_MAX,
  TENANT_COMPANY_NAME_MIN,
  validateAddress,
  validateCompanyName,
  validateContactEmail,
  validatePhone,
} from '@/lib/validations/tenantValidation';
export {
  buildUsersFormRulesContext,
  createLoginUserNameRules,
  createUsersFormRules,
  EMAIL_MAX_LENGTH,
  type LoginUserNameRuleMessages,
  NAME_MAX_LENGTH,
  NOTES_MAX_LENGTH,
  ROLE_NAME_MAX_LENGTH,
  type RuleFactoryContext,
  SHORT_FIELD_MAX_LENGTH,
  type UsersFormTranslate,
} from '@/lib/validations/userValidation';
