import { describe, expect, it } from 'vitest';
import { readFileSync } from 'node:fs';
import path from 'node:path';

const LOCALES = ['de', 'en', 'tr'] as const;

const REQUIRED_BULK_EMAIL_KEYS = [
  'title',
  'subject',
  'body',
  'filterByStatus',
  'filterByLicenseType',
  'recipientCount',
  'send',
  'confirmSend',
  'confirmMessage',
  'sending',
  'sent',
  'failed',
  'resultSummary',
  'noRecipients',
  'rateLimitWarning',
  'success',
  'error',
] as const;

function loadLocaleJson(locale: string, ns: string): Record<string, unknown> {
  const filePath = path.join(
    process.cwd(),
    'src',
    'i18n',
    'locales',
    locale,
    `${ns}.json`
  );
  return JSON.parse(readFileSync(filePath, 'utf8')) as Record<string, unknown>;
}

describe('communication bulk email i18n keys', () => {
  for (const locale of LOCALES) {
    it(`has required communication.bulkEmail keys in ${locale}`, () => {
      const root = loadLocaleJson(locale, 'communication');
      const bulkEmail = root.bulkEmail as Record<string, unknown> | undefined;
      expect(bulkEmail, `${locale}/communication.json missing bulkEmail`).toBeTruthy();
      for (const key of REQUIRED_BULK_EMAIL_KEYS) {
        expect(typeof bulkEmail?.[key], `${locale} communication.bulkEmail.${key}`).toBe(
          'string'
        );
        expect(String(bulkEmail?.[key]).length).toBeGreaterThan(0);
      }
    });

    it(`has nav.communication keys in ${locale}`, () => {
      const nav = loadLocaleJson(locale, 'nav');
      const communication = nav.communication as Record<string, unknown> | undefined;
      expect(communication).toBeTruthy();
      expect(typeof communication?.title).toBe('string');
      expect(typeof communication?.bulkEmail).toBe('string');
    });
  }
});
