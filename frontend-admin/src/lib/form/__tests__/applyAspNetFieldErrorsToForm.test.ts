import { describe, expect, it, vi } from 'vitest';

import {
  applyAspNetFieldErrorsToForm,
  isRequiredFieldValidationMessage,
  mapAspNetFieldErrorsToFormData,
} from '@/lib/form/applyAspNetFieldErrorsToForm';

describe('mapAspNetFieldErrorsToFormData', () => {
  it('maps PascalCase ASP.NET keys to camelCase form fields', () => {
    const data = mapAspNetFieldErrorsToFormData({
      DefaultCurrency: ['The DefaultCurrency field is required.'],
      CompanyTaxNumber: ['Invalid format'],
    });

    expect(data).toEqual([
      { name: 'defaultCurrency', errors: ['The DefaultCurrency field is required.'] },
      { name: 'companyTaxNumber', errors: ['Invalid format'] },
    ]);
  });

  it('aliases FinanzOnline credential fields to form names', () => {
    const data = mapAspNetFieldErrorsToFormData({
      FinanzOnlineUsername: ['Required'],
      FinanzOnlinePassword: ['Too short'],
    });

    expect(data).toEqual([
      { name: 'finanzOnlineParticipantId', errors: ['Required'] },
      { name: 'finanzOnlinePin', errors: ['Too short'] },
    ]);
  });

  it('localizes messages when callback provided', () => {
    const data = mapAspNetFieldErrorsToFormData(
      { DefaultCurrency: ['The DefaultCurrency field is required.'] },
      {
        localizeMessage: (field, msgs) =>
          field === 'defaultCurrency' ? ['Währung ist erforderlich'] : msgs,
      }
    );
    expect(data[0]?.errors).toEqual(['Währung ist erforderlich']);
  });
});

describe('applyAspNetFieldErrorsToForm', () => {
  it('sets form fields from axios ProblemDetails errors', () => {
    const setFields = vi.fn();
    const form = { setFields } as unknown as Parameters<typeof applyAspNetFieldErrorsToForm>[0];

    const names = applyAspNetFieldErrorsToForm(form, {
      response: {
        status: 400,
        data: {
          title: 'One or more validation errors occurred.',
          errors: {
            DefaultLanguage: ['The DefaultLanguage field is required.'],
            InvoiceNumbering: ['The InvoiceNumbering field is required.'],
          },
        },
      },
    });

    expect(names).toEqual(['defaultLanguage', 'invoiceNumbering']);
    expect(setFields).toHaveBeenCalledWith([
      { name: 'defaultLanguage', errors: ['The DefaultLanguage field is required.'] },
      { name: 'invoiceNumbering', errors: ['The InvoiceNumbering field is required.'] },
    ]);
  });

  it('returns empty when no field errors', () => {
    const setFields = vi.fn();
    const form = { setFields } as unknown as Parameters<typeof applyAspNetFieldErrorsToForm>[0];
    expect(applyAspNetFieldErrorsToForm(form, new Error('network'))).toEqual([]);
    expect(setFields).not.toHaveBeenCalled();
  });
});

describe('isRequiredFieldValidationMessage', () => {
  it('detects required wording', () => {
    expect(isRequiredFieldValidationMessage('The DefaultCurrency field is required.')).toBe(true);
    expect(isRequiredFieldValidationMessage('Währung ist erforderlich')).toBe(true);
    expect(isRequiredFieldValidationMessage('Invalid ATU format')).toBe(false);
  });
});
