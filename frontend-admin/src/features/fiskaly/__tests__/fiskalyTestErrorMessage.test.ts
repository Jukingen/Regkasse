import { describe, expect, it } from 'vitest';

import { displayFiskalyTestError } from '@/features/fiskaly/fiskalyTestErrorMessage';

describe('displayFiskalyTestError', () => {
  const t = (key: string) => `i18n:${key}`;

  it('maps known codes to German i18n keys', () => {
    const view = displayFiskalyTestError(
      t,
      { code: 'STARTBELEG_REQUIRED', message: 'Startbeleg is required', details: 'Create a Startbeleg first.' },
      'tseFiskaly.test.signFailed'
    );
    expect(view.title).toBe('i18n:tseFiskaly.test.errors.startbelegRequired');
    expect(view.details).toBe('Create a Startbeleg first.');
    expect(view.code).toBe('STARTBELEG_REQUIRED');
  });

  it('maps period-not-completed to i18n', () => {
    const view = displayFiskalyTestError(
      t,
      { code: 'PERIOD_NOT_COMPLETED', message: 'Monatsbeleg can only be created for completed months' },
      'tseFiskaly.test.signFailed'
    );
    expect(view.title).toBe('i18n:tseFiskaly.test.errors.periodNotCompleted');
  });

  it('uses API message when code is unknown', () => {
    const view = displayFiskalyTestError(
      t,
      { code: 'UNKNOWN_CODE', message: 'Backend said this' },
      'tseFiskaly.test.signFailed'
    );
    expect(view.title).toBe('Backend said this');
  });
});
