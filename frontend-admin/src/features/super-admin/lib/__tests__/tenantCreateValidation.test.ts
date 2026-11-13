import { describe, expect, it } from 'vitest';

import {
  validateAddress,
  validateCompanyName,
  validateContactEmail,
  validatePhone,
} from '../tenantCreateValidation';

describe('validateCompanyName', () => {
  it('requires non-empty name', () => {
    expect(validateCompanyName('')).toBe('required');
    expect(validateCompanyName('  ')).toBe('required');
  });

  it('accepts valid company names', () => {
    expect(validateCompanyName('Café Beispiel GmbH')).toBeNull();
  });

  it('rejects too short names', () => {
    expect(validateCompanyName('A')).toBe('tooShort');
  });
});

describe('validateContactEmail', () => {
  it('requires non-empty email', () => {
    expect(validateContactEmail('')).toBe('required');
    expect(validateContactEmail('  ')).toBe('required');
  });

  it('accepts valid email', () => {
    expect(validateContactEmail('kontakt@cafe-example.at')).toBeNull();
  });

  it('rejects invalid email', () => {
    expect(validateContactEmail('not-an-email')).toBe('invalid');
  });
});

describe('validatePhone', () => {
  it('allows empty optional phone', () => {
    expect(validatePhone('')).toBeNull();
  });

  it('accepts formatted phone numbers', () => {
    expect(validatePhone('+43 1 234 5678')).toBeNull();
  });
});

describe('validateAddress', () => {
  it('allows empty optional address', () => {
    expect(validateAddress('')).toBeNull();
  });
});
