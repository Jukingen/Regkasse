import { describe, expect, it } from 'vitest';

import { formatSessionDevice } from '../sessionDeviceLabel';

describe('formatSessionDevice', () => {
  it('keeps POS platform labels', () => {
    expect(
      formatSessionDevice(
        { clientApp: 'pos', platformLabel: 'POS (Android)', os: 'Android', browser: 'Mobile App' },
        'unknown'
      )
    ).toBe('POS (Android)');
    expect(
      formatSessionDevice({ clientApp: 'pos', platformLabel: 'POS (Web)', os: 'Windows' }, 'unknown')
    ).toBe('POS (Web)');
  });

  it('formats admin as OS - Browser', () => {
    expect(
      formatSessionDevice(
        { clientApp: 'admin', os: 'Windows', browser: 'Chrome', platformLabel: 'Admin' },
        'unknown'
      )
    ).toBe('Windows - Chrome');
    expect(
      formatSessionDevice(
        { clientApp: 'admin', os: 'macOS', browser: 'Chrome', platformLabel: 'Admin' },
        'unknown'
      )
    ).toBe('OS X - Chrome');
  });
});
