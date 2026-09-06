import { describe, expect, it } from 'vitest';

import {
  knownErrorSolutionKey,
  knownErrorSolutionTitleKey,
  normalizeFiskalyErrorCode,
} from '../knownErrorSolutions';

describe('knownErrorSolutions', () => {
  it('normalizes aliases to canonical codes', () => {
    expect(normalizeFiskalyErrorCode('client_error')).toBe('E_CLIENT_ERROR');
    expect(normalizeFiskalyErrorCode('E-SCU-NOT-FOUND')).toBe('E_SCU_NOT_FOUND');
    expect(normalizeFiskalyErrorCode('unknown')).toBeNull();
  });

  it('returns i18n keys for known codes', () => {
    expect(knownErrorSolutionKey('E_UNAUTHORIZED')).toBe('tseFiskaly.errors.solutions.E_UNAUTHORIZED');
    expect(knownErrorSolutionTitleKey('E_RECEIPT_NOT_FOUND')).toBe(
      'tseFiskaly.errors.solutionTitles.E_RECEIPT_NOT_FOUND'
    );
    expect(knownErrorSolutionKey('TIMEOUT')).toBeNull();
  });
});
