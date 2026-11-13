import { describe, expect, it } from 'vitest';

import { localizeSettingsFieldValidationMessages } from '@/features/settings/utils/localizeSettingsFieldValidationMessages';

const catalog: Record<string, string> = {
  'settings.form.localization.defaultCurrencyRequired': 'Währung ist erforderlich',
  'settings.form.general.companyNameRequired': 'Firmenname ist erforderlich',
  'settings.form.general.companyTaxNumberPattern': 'Format: ATU + 8 Ziffern',
  'settings.form.general.contactEmailInvalid': 'Ungültige E-Mail-Adresse',
  'settings.form.finanzOnline.apiUrlInvalid': 'Bitte eine gültige URL angeben',
  'settings.form.general.contactPerson': 'Ansprechpartner',
  'common.validation.requiredWithField': '{{field}} ist ein Pflichtfeld.',
  'common.validation.emailInvalid': 'Bitte eine gueltige E-Mail-Adresse eingeben.',
  'common.validation.urlInvalid': 'Bitte eine gueltige URL angeben.',
  'common.validation.maxLength': 'Hoechstens {{max}} Zeichen erlaubt.',
  'common.validation.minLength': 'Mindestens {{min}} Zeichen erforderlich.',
  'common.validation.numberBetween': 'Bitte einen Wert zwischen {{min}} und {{max}} eingeben.',
  'common.validation.invalidValue': 'Dieser Wert ist ungueltig.',
};

function t(key: string, options?: Record<string, string | number>): string {
  let s = catalog[key] ?? key;
  if (options) {
    for (const [k, v] of Object.entries(options)) {
      s = s.replace(`{{${k}}}`, String(v));
    }
  }
  return s;
}

describe('localizeSettingsFieldValidationMessages', () => {
  it('localizes ASP.NET required messages for known fields', () => {
    expect(
      localizeSettingsFieldValidationMessages(t, 'defaultCurrency', [
        'The DefaultCurrency field is required.',
      ])
    ).toEqual(['Währung ist erforderlich']);
  });

  it('uses requiredWithField when no field-specific key exists', () => {
    expect(
      localizeSettingsFieldValidationMessages(t, 'contactPerson', [
        'The ContactPerson field is required.',
      ])
    ).toEqual(['Ansprechpartner ist ein Pflichtfeld.']);
  });

  it('localizes email and max-length DataAnnotations messages', () => {
    expect(
      localizeSettingsFieldValidationMessages(t, 'contactEmail', [
        'The ContactEmail field is not a valid e-mail address.',
      ])
    ).toEqual(['Ungültige E-Mail-Adresse']);

    expect(
      localizeSettingsFieldValidationMessages(t, 'bankName', [
        "The field BankName must be a string or array type with a maximum length of '100'.",
      ])
    ).toEqual(['Hoechstens 100 Zeichen erlaubt.']);
  });

  it('keeps already-localized messages', () => {
    expect(
      localizeSettingsFieldValidationMessages(t, 'companyName', ['Firmenname ist erforderlich'])
    ).toEqual(['Firmenname ist erforderlich']);
  });
});
