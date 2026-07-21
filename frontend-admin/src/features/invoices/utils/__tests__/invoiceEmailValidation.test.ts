import { describe, expect, it } from 'vitest';

import {
  isValidInvoiceRecipientEmail,
  resolveInvoiceRecipientEmail,
} from '../invoiceEmailValidation';

describe('invoiceEmailValidation', () => {
  it('accepts a basic email address', () => {
    expect(isValidInvoiceRecipientEmail('kunde@example.com')).toBe(true);
  });

  it('rejects invalid email addresses', () => {
    expect(isValidInvoiceRecipientEmail('not-an-email')).toBe(false);
    expect(isValidInvoiceRecipientEmail('@example.com')).toBe(false);
  });

  it('prefers typed input over stored customer email', () => {
    expect(resolveInvoiceRecipientEmail('  typed@example.com ', 'stored@example.com')).toBe(
      'typed@example.com'
    );
  });

  it('falls back to stored customer email when input is blank', () => {
    expect(resolveInvoiceRecipientEmail('   ', 'stored@example.com')).toBe('stored@example.com');
  });
});
