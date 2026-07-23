import type { FormInstance, NamePath } from 'antd/es/form/interface';

import { normalizeApiError } from '@/shared/errors/normalizedApiError';

export type FormFieldErrorData = {
  name: NamePath;
  errors: string[];
};

/** ASP.NET ModelState / ProblemDetails keys → Ant Design form field names. */
const DEFAULT_FIELD_ALIASES: Record<string, string> = {
  // UpdateCompanySettingsRequest ↔ settings form aliases
  finanzOnlineUsername: 'finanzOnlineParticipantId',
  finanzOnlinePassword: 'finanzOnlinePin',
};

function toCamelCaseField(key: string): string {
  if (!key) return key;
  // ASP.NET sometimes prefixes with "$." or uses dotted paths
  const bare = key.replace(/^\$\./, '').split('.').pop() ?? key;
  return bare.charAt(0).toLowerCase() + bare.slice(1);
}

function resolveFormFieldName(
  rawKey: string,
  aliases: Record<string, string> = DEFAULT_FIELD_ALIASES
): string {
  const camel = toCamelCaseField(rawKey);
  return aliases[camel] ?? aliases[rawKey] ?? camel;
}

/**
 * Map ASP.NET / ProblemDetails `errors` onto Ant Design Form fields.
 * Returns the camelCase field names that received errors (for tab focus / scroll).
 */
export function mapAspNetFieldErrorsToFormData(
  fieldErrors: Record<string, string[]> | undefined,
  options?: {
    aliases?: Record<string, string>;
    /** Optional: replace backend messages per form field (e.g. i18n required copy). */
    localizeMessage?: (formField: string, messages: string[]) => string[];
  }
): FormFieldErrorData[] {
  if (!fieldErrors) return [];
  const aliases = { ...DEFAULT_FIELD_ALIASES, ...options?.aliases };
  const out: FormFieldErrorData[] = [];

  for (const [rawKey, messages] of Object.entries(fieldErrors)) {
    if (!messages.length) continue;
    const name = resolveFormFieldName(rawKey, aliases);
    const errors = options?.localizeMessage
      ? options.localizeMessage(name, messages)
      : messages;
    out.push({ name, errors });
  }
  return out;
}

/**
 * Apply validation field errors from an unknown thrown API error onto an Ant Design form.
 * @returns field names that were set (empty if no field-level errors).
 */
export function applyAspNetFieldErrorsToForm(
  form: FormInstance,
  error: unknown,
  options?: {
    aliases?: Record<string, string>;
    localizeMessage?: (formField: string, messages: string[]) => string[];
  }
): string[] {
  const { fieldErrors } = normalizeApiError(error);
  const data = mapAspNetFieldErrorsToFormData(fieldErrors, options);
  if (!data.length) return [];
  form.setFields(data);
  return data.map((d) => (Array.isArray(d.name) ? String(d.name[0]) : String(d.name)));
}

/** True when ASP.NET (or similar) reports a required-field validation failure. */
export function isRequiredFieldValidationMessage(message: string): boolean {
  return /required|erforderlich|zorunlu|pflicht/i.test(message);
}
