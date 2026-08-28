import { describe, expect, it } from 'vitest';

import {
  DEFAULT_THANK_YOU_MESSAGE,
  mapReceiptSettingsFromApi,
} from '@/features/settings/api/receiptSettingsApi';

describe('mapReceiptSettingsFromApi', () => {
  it('maps camelCase payload and keeps stored custom message', () => {
    const mapped = mapReceiptSettingsFromApi({
      thankYouMessage: 'Danke schön!',
      effectiveThankYouMessage: 'Danke schön!',
      defaultThankYouMessage: DEFAULT_THANK_YOU_MESSAGE,
    });
    expect(mapped.thankYouMessage).toBe('Danke schön!');
    expect(mapped.effectiveThankYouMessage).toBe('Danke schön!');
    expect(mapped.defaultThankYouMessage).toBe(DEFAULT_THANK_YOU_MESSAGE);
    expect(mapped.companyDescription).toBe('');
  });

  it('maps PascalCase payload and empty stored message', () => {
    const mapped = mapReceiptSettingsFromApi({
      ThankYouMessage: null,
      EffectiveThankYouMessage: DEFAULT_THANK_YOU_MESSAGE,
      DefaultThankYouMessage: DEFAULT_THANK_YOU_MESSAGE,
      CompanyDescription: 'Dev company description',
    });
    expect(mapped.thankYouMessage).toBe('');
    expect(mapped.effectiveThankYouMessage).toBe(DEFAULT_THANK_YOU_MESSAGE);
    expect(mapped.companyDescription).toBe('Dev company description');
  });

  it('falls back to default when payload is empty', () => {
    const mapped = mapReceiptSettingsFromApi(undefined);
    expect(mapped.thankYouMessage).toBe('');
    expect(mapped.effectiveThankYouMessage).toBe(DEFAULT_THANK_YOU_MESSAGE);
    expect(mapped.defaultThankYouMessage).toBe(DEFAULT_THANK_YOU_MESSAGE);
    expect(mapped.companyDescription).toBe('');
  });
});
