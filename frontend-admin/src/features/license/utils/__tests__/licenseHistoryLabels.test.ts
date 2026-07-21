import { describe, expect, it } from 'vitest';

import {
  getLicenseHistoryEventLabel,
  getLicenseHistoryEventTagColor,
} from '@/features/license/utils/licenseHistoryLabels';

const t = (key: string) => key;

describe('licenseHistoryLabels', () => {
  it('maps known event types to i18n keys', () => {
    expect(getLicenseHistoryEventLabel('activated', t)).toBe('license.history.events.activated');
    expect(getLicenseHistoryEventLabel('EXTENDED', t)).toBe('license.history.events.extended');
  });

  it('falls back to raw event type for unknown values', () => {
    expect(getLicenseHistoryEventLabel('custom_event', t)).toBe('custom_event');
  });

  it('assigns tag colors by event category', () => {
    expect(getLicenseHistoryEventTagColor('activated')).toBe('green');
    expect(getLicenseHistoryEventTagColor('extended')).toBe('blue');
    expect(getLicenseHistoryEventTagColor('unknown')).toBe('default');
  });
});
