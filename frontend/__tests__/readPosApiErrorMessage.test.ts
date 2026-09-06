import { describe, expect, it } from '@jest/globals';

import { readPosApiErrorMessage } from '../utils/readPosApiErrorMessage';

describe('readPosApiErrorMessage', () => {
  it('reads nested Fiskaly error envelope', () => {
    const message = readPosApiErrorMessage(
      {
        response: {
          data: {
            success: false,
            error: {
              code: 'FISKALY_AUTH_FAILED',
              message: 'Fiskaly authentication failed',
              details: 'Invalid API key or secret',
            },
          },
        },
      },
      'fallback'
    );
    expect(message).toContain('Fiskaly authentication failed');
    expect(message).toContain('FISKALY_AUTH_FAILED');
  });

  it('falls back to data.message', () => {
    expect(
      readPosApiErrorMessage({ response: { data: { message: 'Nope' } } }, 'fallback')
    ).toBe('Nope');
  });
});
