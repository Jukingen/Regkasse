import { describe, expect, it } from 'vitest';
import { apiNullableToUndefined } from '@/features/backup-dr/logic/backupDrDtoNormalize';

describe('apiNullableToUndefined', () => {
  it('maps null to undefined', () => {
    expect(apiNullableToUndefined<string>(null)).toBe(undefined);
  });

  it('preserves undefined and values', () => {
    expect(apiNullableToUndefined(undefined)).toBe(undefined);
    expect(apiNullableToUndefined('x')).toBe('x');
  });
});
