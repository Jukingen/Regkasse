import type { Rule } from 'antd/es/form';

/** Austrian UID (ATU + 8 digits) — AGENTS.md / backend tax-number contract. */
export const ATU_TAX_NUMBER_PATTERN = /^ATU\d{8}$/;

/** Username: 3–50 chars, a-z / 0-9 / _ / - (case-insensitive identity). */
export const USERNAME_PATTERN = /^[a-zA-Z0-9_-]{3,50}$/;

export type ValidationTranslate = (
  key: string,
  options?: Record<string, string | number>
) => string;

/**
 * Shared Ant Design Form rules (i18n messages via `t`).
 * Shape mirrors the common FA validation helper API: required / email / min / max / pattern.
 */
export type ValidationRules = {
  required: (field: string) => Rule;
  /** Generic required without field name interpolation. */
  requiredField: () => Rule;
  email: Rule;
  min: (min: number) => Rule;
  max: (max: number) => Rule;
  pattern: (pattern: RegExp, message: string) => Rule;
  atuTaxNumber: (required?: boolean) => Rule[];
  username: (required?: boolean) => Rule[];
};

/**
 * Build locale-aware Ant Design Form rules.
 * Prefer this over ad-hoc hardcoded validation strings in FA forms.
 */
export function createValidationRules(t: ValidationTranslate): ValidationRules {
  return {
    required: (field: string) => ({
      required: true,
      message: t('common.validation.requiredWithField', { field }),
    }),
    requiredField: () => ({
      required: true,
      message: t('common.validation.fieldRequired'),
    }),
    email: {
      type: 'email',
      message: t('common.validation.emailInvalid'),
    },
    min: (min: number) => ({
      min,
      message: t('common.validation.minLength', { min }),
    }),
    max: (max: number) => ({
      max,
      message: t('common.validation.maxLength', { max }),
    }),
    pattern: (pattern: RegExp, message: string) => ({
      pattern,
      message,
    }),
    atuTaxNumber: (required = true) => {
      const rules: Rule[] = [];
      if (required) {
        rules.push({
          required: true,
          message: t('common.validation.atuTaxNumberRequired'),
        });
      }
      rules.push({
        validator: async (_, value) => {
          const trimmed = String(value ?? '').trim();
          if (!trimmed) {
            if (required) {
              throw new Error(t('common.validation.atuTaxNumberRequired'));
            }
            return;
          }
          if (!ATU_TAX_NUMBER_PATTERN.test(trimmed)) {
            throw new Error(t('common.validation.atuTaxNumberPattern'));
          }
        },
      });
      return rules;
    },
    username: (required = true) => {
      const rules: Rule[] = [];
      if (required) {
        rules.push({
          required: true,
          message: t('common.validation.usernameRequired'),
        });
      }
      rules.push(
        { min: 3, message: t('common.validation.minLength', { min: 3 }) },
        { max: 50, message: t('common.validation.maxLength', { max: 50 }) },
        {
          pattern: USERNAME_PATTERN,
          message: t('common.validation.usernamePattern'),
        }
      );
      return rules;
    },
  };
}

/** @deprecated Prefer {@link createValidationRules} — alias kept for snippet compatibility. */
export const validationRules = createValidationRules;
