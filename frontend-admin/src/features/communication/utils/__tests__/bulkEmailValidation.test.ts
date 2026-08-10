import { describe, expect, it } from 'vitest';

import { LicenseType } from '@/api/generated/model/licenseType';
import { TenantStatus } from '@/api/generated/model/tenantStatus';
import {
  isBulkEmailFormValid,
  stripHtmlToText,
  toBulkEmailRequest,
  validateBulkEmailForm,
} from '@/features/communication/utils/bulkEmailValidation';

describe('bulkEmailValidation', () => {
  it('requires subject and non-empty body', () => {
    expect(validateBulkEmailForm({})).toEqual({ subject: 'required', body: 'required' });
    expect(validateBulkEmailForm({ subject: '  ', body: '<p></p>' })).toEqual({
      subject: 'required',
      body: 'required',
    });
    expect(validateBulkEmailForm({ subject: 'Hello', body: '<p>Hi</p>' })).toEqual({});
    expect(isBulkEmailFormValid({ subject: 'Hello', body: '<p>Hi</p>' })).toBe(true);
  });

  it('strips HTML when checking empty body', () => {
    expect(stripHtmlToText('<p>&nbsp;</p>')).toBe('');
    expect(stripHtmlToText('<p>Hello <b>world</b></p>')).toBe('Hello world');
  });

  it('maps form values to API request and omits empty tenantIds', () => {
    expect(
      toBulkEmailRequest({
        subject: '  Subject  ',
        body: '<p>Body</p>',
        filterByStatus: TenantStatus.Active,
        filterByLicenseType: LicenseType.Business,
        tenantIds: [],
      })
    ).toEqual({
      subject: 'Subject',
      body: '<p>Body</p>',
      filterByStatus: 'Active',
      filterByLicenseType: 'Business',
      tenantIds: null,
    });

    expect(
      toBulkEmailRequest({
        subject: 'S',
        body: 'B',
        tenantIds: ['aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee'],
      })
    ).toMatchObject({
      tenantIds: ['aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee'],
    });
  });
});
