import { describe, expect, it, vi } from 'vitest';
import { getUserFacingApiErrorMessage } from '../userFacingApiError';

vi.mock('@/shared/dev/technicalConsole', () => ({
  technicalConsole: { error: vi.fn() },
}));

const t = (key: string) => key;

describe('getUserFacingApiErrorMessage', () => {
  it('maps 401 in loginContext to loginInvalidCredentials', () => {
    expect(
      getUserFacingApiErrorMessage(t, { response: { status: 401 } }, { logContext: 'x', loginContext: true }),
    ).toBe('common.auth.loginInvalidCredentials');
  });

  it('maps 401 without loginContext to http401', () => {
    expect(getUserFacingApiErrorMessage(t, { response: { status: 401 } }, { logContext: 'x' })).toBe(
      'common.errors.http401',
    );
  });

  it('uses fallbackKey when status unknown', () => {
    expect(
      getUserFacingApiErrorMessage(t, new Error('weird'), {
        logContext: 'x',
        fallbackKey: 'tagesabschluss.errors.unknown',
      }),
    ).toBe('tagesabschluss.errors.unknown');
  });
});
